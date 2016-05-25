using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_export_stardict
{
    public class ExportStarDict : IExporter
    {
        public const string StarDictVersion = "2.4.2";

        private readonly string _outputDirPath;
        private readonly GenericDictionary _genericDictionary;
        private readonly List<string> _sortedWords;
        private static readonly byte[] NUL = {0};
        private static readonly string DictZipBinaryPath = $@"{AppDomain.CurrentDomain.BaseDirectory}\dictzip.exe";

        public ExportStarDict(GenericDictionary genericDictionary, string outputDirPath)
        {
            _genericDictionary = genericDictionary;
            _outputDirPath = outputDirPath;
            _sortedWords = new List<string>(_genericDictionary.AllWords.Count);
        }

        public async Task ExportAsync(IProgress<ExportingProgressInfo> progress)
        {
            string basePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}";

            string outDictFilePath = $@"{basePath}.dict";
            string outIdxFilePath = $@"{basePath}.idx";
            string outIdxGzFilePath = $@"{outIdxFilePath}.gz";
            string outIfoFilePath = $@"{basePath}.ifo";

            long idxFileSizeBytes = await CreateDict(progress, outDictFilePath, outIdxFilePath);

            CompressIdx(outIdxFilePath, outIdxGzFilePath);
            CompressDictZip(outDictFilePath);
            CreateIfo(outIfoFilePath, idxFileSizeBytes);
        }

        private async Task<long> CreateDict(IProgress<ExportingProgressInfo> progress, string targetDictFilePath,
            string targetIdxFilePath)
        {
            long idxFileSizeBytes = 0;

            Task export = new Task(() =>
            {
                using (FileStream dictStream = new FileStream(targetDictFilePath, FileMode.Create))
                {
                    using (BinaryWriter dictWriter = new BinaryWriter(dictStream, Encoding.UTF8))
                    {
                        using (FileStream idxSteam = new FileStream(targetIdxFilePath, FileMode.Create))
                        {
                            using (BinaryWriter idxWriter = new BinaryWriter(idxSteam, Encoding.UTF8))
                            {
                                // Get all words and put them into a list
                                _sortedWords.AddRange(_genericDictionary.AllWords.Keys.Select(k => k.Word));

                                // Now sort the list
                                _sortedWords.Sort(g_ascii_strcasecmp);

                                // Browse alphabetically
                                foreach (string word in _sortedWords)
                                {
                                    WriteWord(progress, word, idxWriter, dictWriter);
                                }

                                idxFileSizeBytes = idxSteam.Length;
                            }
                        }
                    }
                }
            });
            export.Start();
            await export;

            return idxFileSizeBytes;
        }

        private void CompressIdx(string targetIdxFilePath, string outIdxGzFilePath, bool removeDict = true)
        {
            using (FileStream inDictStream = new FileStream(targetIdxFilePath, FileMode.Open))
            {
                using (FileStream outDictStream = new FileStream(outIdxGzFilePath, FileMode.Create))
                {
                    using (GZipStream dictZipStream = new GZipStream(outDictStream, CompressionLevel.Optimal, false))
                    {
                        inDictStream.CopyTo(dictZipStream);
                    }
                }
            }

            if (removeDict)
                File.Delete(targetIdxFilePath);
        }

        private static void CompressDictZip(string targetDict, bool removeDict = true)
        {
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
        }

        private void CreateIfo(string targetIfoFilePath, long idxFileSizeBytes)
        {
            using (TextWriter textWriter = new StreamWriter(targetIfoFilePath, false, new UTF8Encoding(false)))
            {
                textWriter.NewLine = "\n";

                DateTime utcNow = DateTime.UtcNow;

                textWriter.WriteLine("StarDict's dict ifo file");
                textWriter.WriteLine($"version={StarDictVersion}");
                textWriter.WriteLine($"wordcount={_genericDictionary.AllWords.Count}");
                textWriter.WriteLine($"idxfilesize={idxFileSizeBytes}");
                textWriter.WriteLine($"bookname={_genericDictionary.Name} ({_genericDictionary.Version})");
                textWriter.WriteLine($"date={utcNow.Year}.{utcNow.Month:00}.{utcNow.Day:00}");
                textWriter.WriteLine($"website={_genericDictionary.Website}");
                textWriter.WriteLine($"description={_genericDictionary.Description}");
                textWriter.WriteLine("sametypesequence=h");
            }
        }

        private void WriteWord(IProgress<ExportingProgressInfo> progress, string word, BinaryWriter idxWriter, BinaryWriter dictWriter)
        {
            ExportingProgressInfo exportingProgressInfo = new ExportingProgressInfo
            {
                WordsCountToWrite = _genericDictionary.AllWords.Count,
                WordsWritten = 0
            };

            // word_str
            if (word.Length >= 256)
                throw new NotSupportedException(); // todo handle this

            idxWriter.Write(Encoding.UTF8.GetBytes(word));
            idxWriter.Write(NUL);

            // word_data_offset
            uint definitionPostionBegin = Convert.ToUInt32(dictWriter.BaseStream.Position);
            idxWriter.Write(ToBigEndian(definitionPostionBegin));

            // Get meanings linked to the same words (homonyms)
            var articlesForWord = _genericDictionary.AllWords.Where(w => w.Key.Word == word).OrderBy(w => w.Key.Id);

            // Put each meaning in the whole definition, numbering them
            int meaningIndex = 0;
            foreach (KeyValuePair<Meaning, List<Definition>> article in articlesForWord)
            {
                meaningIndex++;
                Meaning meaning = article.Key;

                // Add a meaning in the definition + a numbered bullet point
                WriteWordMeaningHtmlHeader(dictWriter, meaningIndex, meaning);

                // Re-order definitions
                List<Definition> orderedDefinitions =
                    article.Value
                        .OrderBy(d => d.MeaningId)
                        .ThenBy(d => d.Position)
                        .Distinct()
                        .ToList();

                // Write definitions for this meaning one after the other
                foreach (Definition definition in orderedDefinitions)
                {
                    dictWriter.Write(Encoding.UTF8.GetBytes(definition.DefinitionHtml));
                }

                // word_data_size;
                uint definitionPostionEnd = Convert.ToUInt32(dictWriter.BaseStream.Position);
                idxWriter.Write(ToBigEndian(definitionPostionEnd - definitionPostionBegin));

                // Alternate keywords todo SYN
                //foreach (string word in meaning.AlternateWords)
                //{
                //}

                exportingProgressInfo.WordsWritten++;

                if (progress != null && exportingProgressInfo.WordsWritten % 100 == 0)
                    progress.Report(exportingProgressInfo);
            }
        }

        private static void WriteWordMeaningHtmlHeader(BinaryWriter dictWriter, int meaningIndex, Meaning meaning)
        {
            StringBuilder htmlHeader =
                new StringBuilder($"{(meaningIndex > 1 ? "<br>" : string.Empty)}<b>{meaningIndex}. {meaning.Word}</b><br/>\n");

            if (!string.IsNullOrWhiteSpace(meaning.PronounciationIpa))
            {
                htmlHeader.AppendFormat("    <span>IPA: /{0}/</span><br/>\n", meaning.PronounciationIpa);
            }

            if (!string.IsNullOrWhiteSpace(meaning.PronounciationSpell))
            {
                htmlHeader.AppendFormat("    <span>Spell: [{0}]</span><br/>\n", meaning.PronounciationSpell);
            }

            if (!string.IsNullOrWhiteSpace(meaning.Syllable))
            {
                htmlHeader.AppendFormat("    <span>Syllable: {0}</span><br/>\n", meaning.Syllable);
            }

            dictWriter.Write(Encoding.UTF8.GetBytes(htmlHeader.ToString()));
        }

        private byte[] ToBigEndian(uint stuff)
        {
            byte[] bytes = BitConverter.GetBytes(stuff);
            return bytes.Reverse().ToArray();
        }

        /// <summary>
        /// Ported to C# from gstrfuncs.c
        /// https://github.com/GNOME/glib/blob/master/glib/gstrfuncs.c
        /// </summary>
        private static bool IsUpper(char c)
        {
            return c >= 'A' && c <= 'Z';
        }

        /// <summary>
        /// Ported to C# from gstrfuncs.c
        /// https://github.com/GNOME/glib/blob/master/glib/gstrfuncs.c
        /// </summary>
        private static char ToLower(char c)
        {
            return IsUpper(c)
                ? (char) (c - 'A' + 'a')
                : c;
        }

        /// <summary>
        /// Ported to C# from gstrfuncs.c
        /// https://github.com/GNOME/glib/blob/master/glib/gstrfuncs.c
        /// </summary>
        private static int g_ascii_strcasecmp(string s1, string s2)
        {
            int indexS1 = 0, indexS2 = 0;

            if (string.IsNullOrEmpty(s1))
                return 0;

            if (string.IsNullOrEmpty(s2))
                return 0;

            while (indexS1 < s1.Length && indexS2 < s2.Length)
            {
                int c1 = ToLower(s1[indexS1]);
                int c2 = ToLower(s2[indexS2]);
                if (c1 != c2)
                    return c1 - c2;

                indexS1++;
                indexS2++;
            }

            if (indexS1 >= s1.Length && indexS2 < s2.Length)
                return -s2[indexS2]; // 0 - s2[indexS2]

            if (indexS2 >= s2.Length && indexS1 < s1.Length)
                return s1[indexS2]; // s1[indexS1] - 0

            return 0;
        }
    }
}