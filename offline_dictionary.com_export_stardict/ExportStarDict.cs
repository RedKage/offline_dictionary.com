using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using offline_dictionary.com_export_xdxf;
using offline_dictionary.com_reader.Model;

namespace offline_dictionary.com_export_stardict
{
    public class ExportStarDict
    {
        public const string StarDictVersion = "2.4.2";

        private readonly string _outputDirPath;
        private readonly GenericDictionary _genericDictionary;
        private readonly List<string> _sortedWords;

        public ExportStarDict(GenericDictionary genericDictionary, string outputDirPath)
        {
            _genericDictionary = genericDictionary;
            _outputDirPath = outputDirPath;
            _sortedWords = new List<string>(_genericDictionary.AllWords.Count);
        }

        public async Task ExportAsync(IProgress<ExportingProgressInfo> progress)
        {
            long idxFileSizeBytes = await CreateDict(progress);

            CreateIfo(idxFileSizeBytes);
        }

        private async Task<long> CreateDict(IProgress<ExportingProgressInfo> progress)
        {
            string outDictFilePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}.dict";
            string outIdxFilePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}.idx";

            long idxFileSizeBytes = 0;
            byte[] nul = { 0 };

            Task export = new Task(() =>
            {
                using (FileStream dictStream = new FileStream(outDictFilePath, FileMode.Create))
                {
                    using (BinaryWriter dictWriter = new BinaryWriter(dictStream, Encoding.UTF8))
                    {
                        using (FileStream idxSteam = new FileStream(outIdxFilePath, FileMode.Create))
                        {
                            using (BinaryWriter idxWriter = new BinaryWriter(idxSteam, Encoding.UTF8))
                            {
                                ExportingProgressInfo exportingProgressInfo = new ExportingProgressInfo
                                {
                                    WordsCountToWrite = _genericDictionary.AllWords.Count,
                                    WordsWritten = 0
                                };

                                // Get all words and put them into a list
                                _sortedWords.AddRange(_genericDictionary.AllWords.Keys.Select(k => k.Word));

                                // Now sort the list
                                _sortedWords.Sort(g_ascii_strcasecmp);

                                // Browse alphabetically
                                foreach (string word in _sortedWords)
                                {
                                    var articlesForWord = _genericDictionary.AllWords.Where(w => w.Key.Word == word).OrderBy(w => w.Key.Id);
                                    foreach (KeyValuePair<Meaning, List<Definition>> article in articlesForWord)
                                    {
                                        Meaning meaning = article.Key;

                                        // word_str
                                        if (meaning.Word.Length >= 256)
                                            throw new NotSupportedException();

                                        idxWriter.Write(Encoding.UTF8.GetBytes(meaning.Word));
                                        idxWriter.Write(nul);

                                        // word_data_offset
                                        uint definitionPostionBegin = Convert.ToUInt32(dictStream.Position);
                                        idxWriter.Write(ToBigEndian(definitionPostionBegin));

                                        // Re-order definitions
                                        List<Definition> definitions = article.Value;
                                        List<Definition> orderedDefinitions =
                                            definitions
                                                .OrderBy(d => d.MeaningId)
                                                .ThenBy(d => d.Position)
                                                .Distinct()
                                                .ToList();

                                        // Write definitions in .dict
                                        foreach (Definition definition in orderedDefinitions)
                                        {
                                            dictWriter.Write(Encoding.UTF8.GetBytes(definition.DefinitionHtml));
                                        }

                                        // word_data_size;
                                        uint definitionPostionEnd = Convert.ToUInt32(dictStream.Position);
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

        private void CreateIfo(long idxFileSizeBytes)
        {
            string outFilePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}.ifo";

            using (TextWriter textWriter = new StreamWriter(outFilePath, false, new UTF8Encoding(false)))
            {
                textWriter.NewLine = "\n";
                
                DateTime utcNow = DateTime.UtcNow;

                textWriter.WriteLine("StarDict's dict ifo file");
                textWriter.WriteLine($"version={StarDictVersion}");
                textWriter.WriteLine($"wordcount={_genericDictionary.AllWords.Count}");
                textWriter.WriteLine($"idxfilesize={idxFileSizeBytes}");
                textWriter.WriteLine($"bookname={_genericDictionary.FullName} ({_genericDictionary.Version})");
                textWriter.WriteLine($"date={utcNow.Year}.{utcNow.Month:00}.{utcNow.Day:00}");
                textWriter.WriteLine($"website={_genericDictionary.Website}");
                textWriter.WriteLine($"description={_genericDictionary.Description}");
                textWriter.WriteLine("sametypesequence=h");
            }
        }

        private void CreateGzipDict()
        {

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
                ? (char)(c - 'A' + 'a')
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
                return -s2[indexS2];        // 0 - s2[indexS2]

            if (indexS2 >= s2.Length && indexS1 < s1.Length)
                return s1[indexS2];         // s1[indexS1] - 0

            return 0;
        }
    }
}
