using HtmlAgilityPack;
using offline_dictionary.com_shared.Messaging;
using offline_dictionary.com_shared.Model;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SQLite;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using offline_dictionary.com_shared;

namespace offline_dictionary.com_reader_sqlite
{
    public class LoadFromSqlite : IReader
    {
        private const string DictionayName =
            "dictionary.com";

        private const string DictionayWebsite =
            "http://www.dictionary.com";

        private const string DictionayFullName =
            "Dictionary.com Unabridged";

        private const string DictionayDescription =
            "Dictionary.com Unabridged. Based on the Random House Dictionary, © Random House, Inc. 2016";

        private const string DictionayVersion =
            "5.5.2_08-08"; // app version + sqlite file version

        private readonly GenericDictionary _genericDictionary;
        private static string _connectionString;
        private static int _loadWordsLimit;
        private int? _totalWordsToAdd;
        private int _wordsAdded = 0;

        public LoadFromSqlite(string dbFilePath, int loadWordsLimit = -1)
        {
            var connectionStringParams = new SQLiteConnectionStringBuilder
            {
                DataSource = dbFilePath,
                Version = 3,
                CacheSize = 4096,
                DefaultTimeout = 100,
                BusyTimeout = 100,
                UseUTF16Encoding = true,
                ReadOnly = true,
                SyncMode = SynchronizationModes.Off,
                JournalMode = SQLiteJournalModeEnum.Off
            };

            _genericDictionary = new GenericDictionary
            {
                Name = DictionayName,
                FullName = DictionayFullName,
                Description = DictionayDescription,
                Website = DictionayWebsite,
                Version = DictionayVersion
            };

            _connectionString = connectionStringParams.ToString();
            _loadWordsLimit = loadWordsLimit;
        }

        public async Task<GenericDictionary> LoadAsync()
        {
            Task loadAllWords = new Task(() =>
           {
               Messaging.Send("Creating tasks ...");

               List<Task> loadWordTask = new List<Task>();

               // Browse each word from entry table
               foreach (Meaning currentMeaning in GetAllWords(_loadWordsLimit))
               {
                   Task item = new Task(() => UpdateBigAssDictionary(currentMeaning));
                   item.Start();
                   //item.Wait(); // todo debug
                   loadWordTask.Add(item);
               }
               _totalWordsToAdd = loadWordTask.Count;
               
               Task.WaitAll(loadWordTask.ToArray());
           });

           loadAllWords.Start();
           await loadAllWords;

           return _genericDictionary;
        }

        private void UpdateBigAssDictionary(Meaning currentMeaning)
        {
            try
            {
                // When we add words, we also get their 'alternateWords' at the same time.
                // Those could be alternative spellings, other words with the same meaning, etc.
                // --> Pretty much, every word that will lead to this definition
                currentMeaning.AlternateWords = GetAlternateWords(currentMeaning).ToArray();

                // Get definition for meaning
                List<Definition> definitions = GetDefinition(currentMeaning).ToList();
                if (!definitions.Any())
                {
                    Messaging.Send(MessageLevel.Warn, $@"Weird, word {currentMeaning} has no definition");
                    return;
                }

                // Associate meaning --> definition into big ass dictionary
                bool added = _genericDictionary.AllWords.TryAdd(currentMeaning, definitions);
                if (!added)
                {
                    Messaging.Send(MessageLevel.Error, $@"Could not add word {currentMeaning}");
                }
            }
            catch (Exception ex)
            {
                Messaging.Send($@"Unknown error occured for word {currentMeaning}\n{ex.Message}\n{ex.StackTrace}\n\n");
            }

            Interlocked.Increment(ref _wordsAdded);
            int wordsAdded = _wordsAdded;

            if (wordsAdded % 100 == 0)
            {
                if (_totalWordsToAdd.HasValue)
                {
                    int percent = (int)(wordsAdded * 100.0 / _totalWordsToAdd);
                    Messaging.Send(MessageLevel.Info, $@"Loading... {percent:000}% ({wordsAdded}/{_totalWordsToAdd} words loaded)");
                }
                else
                {
                    Messaging.Send(MessageLevel.Info, $@"Loading... {000}% ({wordsAdded} words loaded)");
                }
            }
        }

        private IEnumerable<Meaning> GetAllWords(int limit = -1)
        {
            const string querySameWords =
                @"SELECT * FROM entries LIMIT @limit";

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(querySameWords, connection))
                {
                    command.Parameters.Add(new SQLiteParameter("limit", limit));

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            NameValueCollection values = reader.GetValues();
                            var entry = new Meaning
                            {
                                Id = int.Parse(values.Get("id")),
                                Word = values.Get("entry"),
                                AlternateWords = null,
                                Syllable = values.Get("entry_rich"),
                                PronounciationIpa = values.Get("pronunciation_ipa"),
                                PronounciationSpell = values.Get("pronunciation_spell"),
                                AudioFile = values.Get("audio_file")
                            };

                            // Clean stuff just in case
                            entry.Word = HtmlToPlainText(entry.Word);
                            entry.Syllable = HtmlToPlainText(entry.Syllable);
                            entry.PronounciationIpa = HtmlToPlainText(entry.PronounciationIpa);
                            entry.PronounciationSpell = HtmlToPlainText(entry.PronounciationSpell);

                            yield return entry;
                        }
                    }
                }
            }
        }

        private IEnumerable<string> GetAlternateWords(Meaning meaning)
        {
            const string querySameWords =
                @"SELECT * FROM entries e

                    INNER JOIN headword_entries he
                    ON he.entry_id = e.id
                    
                    INNER JOIN headwords h
                    ON h.id = he.headword_id

                    WHERE e.id = @entryId";

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(querySameWords, connection))
                {
                    command.Parameters.Add(new SQLiteParameter("entryId", meaning.Id));

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            NameValueCollection values = reader.GetValues();

                            string word = values.Get("headword");

                            // Clean stuff just in case
                            word = HtmlToPlainText(word);

                            if (word.Equals(meaning.Word, StringComparison.InvariantCultureIgnoreCase))
                                continue;

                            yield return word;
                        }
                    }
                }
            }
        }

        private IEnumerable<Definition> GetDefinition(Meaning meaning)
        {
            const string queryEntryDefinition =
                @"SELECT *
                    FROM content_blocks
                    WHERE entry_Id = @entryId";

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(queryEntryDefinition, connection))
                {
                    command.Parameters.Add(new SQLiteParameter("entryId", meaning.Id));

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            NameValueCollection values = reader.GetValues();
                            var definition = new Definition
                            {
                                Headword = meaning.Word,
                                MeaningId = meaning.Id,
                                WordType = values.Get("pos"),
                                Position = int.Parse(values.Get("position")),
                                DefinitionHtml = values.Get("content")
                            };

                            // Clean stuff just in case
                            definition.Headword = HtmlToPlainText(definition.Headword);
                            definition.DefinitionHtml = FixHtml(definition.DefinitionHtml);

                            yield return definition;
                        }
                    }
                }
            }
        }

        private string FixHtml(string potentiallyFuckedUpHtml)
        {
            HtmlDocument agileHtmlDocument = new HtmlDocument
            {
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true,
                OptionOutputAsXml = true,
                OptionWriteEmptyNodes = true,
                OptionOutputOptimizeAttributeValues = true
            };

            // Fixes broken tags and stuff
            agileHtmlDocument.LoadHtml(potentiallyFuckedUpHtml);

            // Decode crazy HTML entities to unicode, and then re-encode only XML entities
            HtmlNodeCollection textNodes =
                agileHtmlDocument.DocumentNode.SelectNodes(
                    "//text()[(normalize-space(.) != '') and not(parent::script) and not(*)]");

            foreach (HtmlNode htmlNode in textNodes)
            {
                string text = htmlNode.InnerText;

                // Decode
                text = HttpUtility.HtmlDecode(text);
                text = WebUtility.HtmlDecode(text);

                // Escape invalid characters for XML: (') and (") ARE valid within tags, but NOT in attributes
                text = text.Replace("&", "&amp;");
                text = text.Replace("<", "&lt;");
                text = text.Replace(">", "&gt;");

                // Encapsulate text nodes so we are sure there are no text left between closed tags...
                text = $"<span>{text}</span>";

                // Re-inject
                HtmlNode newChild = HtmlNode.CreateNode(text);
                htmlNode.ParentNode.ReplaceChild(newChild, htmlNode);
            }

            string innerHtml = agileHtmlDocument.DocumentNode.InnerHtml;

            // Convert line breaks to Unix style
            innerHtml = innerHtml.Replace("\r\n", "\n");
            innerHtml = innerHtml.Replace("\r", "\n");

            // Convert tabs to 4 spaces
            innerHtml = innerHtml.Replace("\t", "    ");

            return innerHtml;
        }

        private string HtmlToPlainText(string html)
        {
            HtmlDocument agileHtmlDocument = new HtmlDocument
            {
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true,
                OptionOutputAsXml = true,
                OptionWriteEmptyNodes = true,
                OptionOutputOptimizeAttributeValues = true
            };

            agileHtmlDocument.LoadHtml(html);

            string cleanHtml = agileHtmlDocument.DocumentNode.InnerText;
            cleanHtml = HttpUtility.HtmlDecode(cleanHtml);
            cleanHtml = HttpUtility.HtmlDecode(cleanHtml);

            return cleanHtml;
        }
    }
}