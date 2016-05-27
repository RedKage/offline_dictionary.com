using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Messaging;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_export_stardict
{
    public class ExportStarDict : IExporter
    {
        public const string StarDictVersion = "2.4.2";

        private readonly string _outputDirPath;
        private readonly GenericDictionary _genericDictionary;
        private SortedDictionary<string, IdxStructure> _indexByWord;

        // ReSharper disable InconsistentNaming
        private static readonly byte[] NUL = { 0 };
        // ReSharper restore InconsistentNaming

        private static readonly string DictZipBinaryPath = $@"{AppDomain.CurrentDomain.BaseDirectory}\dictzip.exe";

        public ExportStarDict(GenericDictionary genericDictionary, string outputDirPath)
        {
            _genericDictionary = genericDictionary;
            _outputDirPath = outputDirPath;
        }

        public async Task ExportAsync()
        {
            string basePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}";

            string outDictFilePath = $@"{basePath}.dict";
            string outIdxFilePath = $@"{basePath}.idx.gz";
            string outIfoFilePath = $@"{basePath}.ifo";

            await CreateIdxInMemory();

            await WriteDict(outDictFilePath);

            long idxSize = WriteIdx(outIdxFilePath);

            CompressDictZip(outDictFilePath);
            WriteIfo(outIfoFilePath, idxSize);
        }

        /// <summary>
        /// StarDict 2.4.2 doesn't support synonyms file (.syn),
        /// nor does it support multiple same words pointing to a different definition.
        /// This is supported in 2.4.8 but not 2.4.2.
        /// So, here we 'merge' the meanings for a same words together.
        ///
        /// eg:
        /// fan (a supporter), fan (stuff that creates air), Fan (cant remember what it means)
        /// Instead of having 3 entries in the IDX that points to 3 differents definitions,
        /// we create 1 entry 'fan' and we put all the meanings together.
        ///
        /// As a result we will have something like:
        /// 1. fan (supporter)
        ///     ...
        /// 2. fan (stuff for air)
        ///     ...
        /// 3. Fan (another meaning)
        /// </summary>
        private async Task CreateIdxInMemory()
        {
            Messaging.Send(MessageLevel.Info, "Creating .idx in memory ...");

            Task indexCreation = new Task(() =>
            {
                _indexByWord = new SortedDictionary<string, IdxStructure>(new AsciiComparer());

                // Group meanings for the same words (homonyms become 1 word with multiple meanings)
                Dictionary<string, List<Meaning>> meaningsForWord =
                    _genericDictionary.AllWords.Keys
                        .GroupBy(m => m.Word, StringComparer.InvariantCultureIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Id).ToList(), StringComparer.InvariantCultureIgnoreCase);

                // Add all of the alternate words so they point to the definitions of their 'main word'
                foreach (var meaningByWord in meaningsForWord)
                {
                    string mainWord = meaningByWord.Key;
                    List<Meaning> meanings = meaningByWord.Value;

                    IdxStructure mainWordStructure = new IdxStructure
                    {
                        ParentWord = null,
                        Meanings = meanings
                    };

                    // Word is also a synonym for another word
                    // and has already been added
                    if (_indexByWord.ContainsKey(mainWord))
                    {
                        // Make sure this word stays a main word as it has its own definition
                        _indexByWord[mainWord] = mainWordStructure;
                    }
                    else
                    {
                        // Add main word to .idx
                        _indexByWord.Add(mainWord, mainWordStructure);
                    }

                    // Reference the 'main word' for each synonym
                    foreach (string synonym in meanings.SelectMany(m => m.AlternateWords))
                    {
                        if (_indexByWord.ContainsKey(synonym))
                            continue;

                        // Add aletrnate word to .idx specifying the parent word
                        IdxStructure alternateWordStructure = new IdxStructure
                        {
                            ParentWord = mainWordStructure,
                            Meanings = meanings
                        };
                        _indexByWord.Add(synonym, alternateWordStructure);
                    }
                }
            });
            indexCreation.Start();
            await indexCreation;

            Messaging.Send(MessageLevel.Info, $"Done, {_indexByWord.Count} word entries ready.");
        }

        private async Task WriteDict(string targetDictFilePath)
        {
            Messaging.Send(MessageLevel.Info, "Writing .dict ...");
            int wordsToWrite = _indexByWord.Count;
            long dictSize = 0;

            Task export = new Task(() =>
            {
                int wordsWritten = 0;

                using (FileStream dictStream = new FileStream(targetDictFilePath, FileMode.Create))
                {
                    using (BinaryWriter dictWriter = new BinaryWriter(dictStream, Encoding.UTF8))
                    {
                        // Get a word and its meanings
                        foreach (string word in _indexByWord.Keys)
                        {
                            IdxStructure idxStructure = _indexByWord[word];
                            List<Meaning> meanings = idxStructure.Meanings;

                            // Get this word entry in the .idx structure
                            if (!_indexByWord.ContainsKey(word))
                                throw new KeyNotFoundException(word);

                            // Update position in .idx
                            uint definitionPostionBegin = Convert.ToUInt32(dictWriter.BaseStream.Position);
                            idxStructure.DefinitionPosition = definitionPostionBegin;

                            // Write the word's meanings one after another
                            for (int index = 0; index < meanings.Count; index++)
                            {
                                Meaning meaning = meanings[index];

                                // Visually separate meanings
                                WriteWordMeaningHtmlHeader(dictWriter, meaning, index + 1, meanings.Count);

                                // A meaning can have multiple definitions, re-order them
                                List<Definition> orderedDefinitions =
                                    _genericDictionary.AllWords[meaning]
                                        .OrderBy(d => d.MeaningId)
                                        .ThenBy(d => d.Position)
                                        .Distinct()
                                        .ToList();

                                // Write definitions for this meaning one after the other
                                foreach (Definition definition in orderedDefinitions)
                                {
                                    string tweakedHtml = TweakHtml(definition.DefinitionHtml);
                                    dictWriter.Write(Encoding.UTF8.GetBytes(tweakedHtml));
                                }
                            }

                            // Notify progression
                            wordsWritten++;
                            if (wordsWritten % 1000 == 0)
                            {
                                int percent = (int)(wordsWritten * 100.0 / wordsToWrite);
                                Messaging.Send(MessageLevel.Info, $"Writing ... {percent:000}%");
                            }

                            // Update length in .idx
                            uint definitionPostionEnd = Convert.ToUInt32(dictWriter.BaseStream.Position);
                            idxStructure.DefinitionLength = definitionPostionEnd - definitionPostionBegin;

                            // Check if this word has alternate words, and use the same position/lenght for them
                            IEnumerable<string> alternateWords = meanings.SelectMany(m => m.AlternateWords).Distinct();
                            foreach (string alternateWord in alternateWords)
                            {
                                IdxStructure alternateWordIdxStructure = _indexByWord[alternateWord];

                                if (alternateWordIdxStructure.ParentWord != null)
                                {
                                    alternateWordIdxStructure.DefinitionPosition = alternateWordIdxStructure.ParentWord.DefinitionPosition;
                                    alternateWordIdxStructure.DefinitionLength = alternateWordIdxStructure.ParentWord.DefinitionLength;
                                }
                            }
                        }
                        dictSize = dictStream.Position;
                    }
                }
            });
            export.Start();
            await export;

            Messaging.Send(MessageLevel.Info, $"Done, .dict written ({dictSize} bytes)");
        }

        private long WriteIdx(string targetIdxFilePath)
        {
            Messaging.Send(MessageLevel.Info, "Writing .idx ...");

            long idxUncompressedLength = 0;

            using (FileStream idxSteam = new FileStream(targetIdxFilePath, FileMode.Create))
            {
                using (GZipStream idxZipStream = new GZipStream(idxSteam, CompressionLevel.Optimal, false))
                {
                    using (BinaryWriter idxWriter = new BinaryWriter(idxZipStream, Encoding.UTF8))
                    {
                        foreach (string word in _indexByWord.Keys)
                        {
                            // word_str
                            if (word.Length >= 256)
                                throw new NotSupportedException($"Cannot have a word > 256 chars: '{word}'");

                            // check empty definition
                            if (_indexByWord[word].DefinitionLength == 0)
                                throw new ArgumentNullException($"Defintion empty for '{word}'");

                            // word_str + \0
                            byte[] wordBytes = Encoding.UTF8.GetBytes(word).Concat(NUL).ToArray();
                            idxWriter.Write(wordBytes);

                            // word_data_offset
                            byte[] dictDefinitionOffset = ToBigEndian(_indexByWord[word].DefinitionPosition);
                            idxWriter.Write(dictDefinitionOffset);

                            // word_data_size;
                            byte[] dictDefinitionLength = ToBigEndian(_indexByWord[word].DefinitionLength);
                            idxWriter.Write(dictDefinitionLength);

                            idxUncompressedLength += wordBytes.Length + dictDefinitionOffset.Length + dictDefinitionLength.Length;
                        }
                    }
                }
            }

            Messaging.Send(MessageLevel.Info, $"Done, .idx written ({idxUncompressedLength} bytes uncompressed)");
            return idxUncompressedLength;
        }

        private static void CompressDictZip(string targetDict, bool removeDict = true)
        {
            Messaging.Send(MessageLevel.Info, "Compression, .dict ...");

            if (!File.Exists(targetDict))
                throw new FileNotFoundException(targetDict, targetDict);

            FileInfo targetDictInfo = new FileInfo(targetDict);

            if (targetDictInfo.Directory == null)
                throw new DirectoryNotFoundException(targetDict);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = DictZipBinaryPath,
                WorkingDirectory = targetDictInfo.Directory.FullName,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = $"--force {(removeDict ? string.Empty : "--keep")} \"{targetDictInfo.Name}\""
            };

            using (Process exeProcess = Process.Start(startInfo))
            {
                if (exeProcess == null)
                    throw new NullReferenceException($"{startInfo.FileName} {startInfo.Arguments}");

                exeProcess.WaitForExit();
            }

            Messaging.Send(MessageLevel.Info, "Done, .dict compressed");
        }

        private void WriteIfo(string targetIfoFilePath, long idxSize)
        {
            Messaging.Send(MessageLevel.Info, "Writing, .ifo ...");

            using (TextWriter textWriter = new StreamWriter(targetIfoFilePath, false, new UTF8Encoding(false)))
            {
                textWriter.NewLine = "\n";

                DateTime utcNow = DateTime.UtcNow;

                textWriter.WriteLine("StarDict's dict ifo file");
                textWriter.WriteLine($"version={StarDictVersion}");
                textWriter.WriteLine($"wordcount={_indexByWord.Count}");
                textWriter.WriteLine($"idxfilesize={idxSize}");
                textWriter.WriteLine($"bookname={_genericDictionary.Name} ({_genericDictionary.Version})");
                textWriter.WriteLine($"date={utcNow.Year}.{utcNow.Month:00}.{utcNow.Day:00}");
                textWriter.WriteLine($"website={_genericDictionary.Website}");
                textWriter.WriteLine($"description={_genericDictionary.Description}");
                textWriter.WriteLine("sametypesequence=h");
            }

            Messaging.Send(MessageLevel.Info, "Done, .ifo written");
        }

        private static void WriteWordMeaningHtmlHeader(BinaryWriter dictWriter, Meaning meaning, int meaningIndex, int totalMeanings)
        {
            StringBuilder htmlHeader = new StringBuilder($"{(meaningIndex > 1 ? "<br>" : string.Empty)}");

            htmlHeader.AppendFormat(
                totalMeanings > 1
                    ? meaningIndex > 1
                        ? "<hr><br><b>{0}. {1}</b><br>\n"
                        : "<b>{0}. {1}</b><br>\n"
                    : "<b>{1}</b><br>\n",
                meaningIndex, meaning.Word);

            if (!string.IsNullOrWhiteSpace(meaning.PronounciationIpa))
            {
                htmlHeader.AppendFormat("    <span>IPA: /{0}/</span><br>\n", meaning.PronounciationIpa);
            }

            if (!string.IsNullOrWhiteSpace(meaning.PronounciationSpell))
            {
                htmlHeader.AppendFormat("    <span>Spell: [{0}]</span><br>\n", meaning.PronounciationSpell);
            }

            if (!string.IsNullOrWhiteSpace(meaning.Syllable))
            {
                htmlHeader.AppendFormat("    <span>Syllable: {0}</span><br>\n", meaning.Syllable);
            }

            dictWriter.Write(Encoding.UTF8.GetBytes(htmlHeader.ToString()));
        }

        private string TweakHtml(string html)
        {
            html = html.Replace("<br/>", "<br>");
            return html;
        }

        private byte[] ToBigEndian(uint stuff)
        {
            byte[] bytes = BitConverter.GetBytes(stuff);
            return bytes.Reverse().ToArray();
        }
    }
}