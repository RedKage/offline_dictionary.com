﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Messaging;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_export_xdxf
{
    public class ExportXdxf : IExporter
    {
        public const string XdxfVersion = "032beta";

        private static readonly XmlWriterSettings XdxfWriterSettings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            Encoding = Encoding.Unicode,
            CheckCharacters = true,
            ConformanceLevel = ConformanceLevel.Document,
            OmitXmlDeclaration = false
        };

        private static readonly XmlReaderSettings DefinitionReaderSettings = new XmlReaderSettings
        {
            CheckCharacters = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            IgnoreWhitespace = true,
            ValidationType = ValidationType.None,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            DtdProcessing = DtdProcessing.Ignore,
            ValidationFlags = XmlSchemaValidationFlags.None
        };

        private readonly Encoding _encoding;
        private readonly string _outputDirPath;
        private readonly GenericDictionary _genericDictionary;

        public ExportXdxf(GenericDictionary genericDictionary, string outputDirPath, Encoding encoding = null)
        {
            _genericDictionary = genericDictionary;
            _outputDirPath = outputDirPath;
            _encoding = encoding;
        }

        public async Task ExportAsync()
        {
            Encoding enc = _encoding ?? Encoding.Unicode; // UTF-16 is default

            XdxfWriterSettings.Encoding = enc;

            string outFilePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}.xdxf";

            using (TextWriter textWriter = new StreamWriter(outFilePath, false, enc))
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(textWriter, XdxfWriterSettings))
                {
                    await CreateXdxfToStreamAsync(xmlWriter);
                }
            }
        }

        private async Task CreateXdxfToStreamAsync(XmlWriter xmlWriter)
        {
            Messaging.Send(MessageLevel.Info, "Writing .xdxf ...");
            int wordsToWrite = _genericDictionary.AllWords.Count;

            Task export = new Task(() =>
            {
                int wordsWritten = 0;

                xmlWriter.WriteStartDocument();

                // XDXF
                xmlWriter.WriteStartElement("xdxf");
                xmlWriter.WriteAttributeString("lang_from", "ENG");
                xmlWriter.WriteAttributeString("lang_to", "ENG");
                xmlWriter.WriteAttributeString("format", "visual");
                xmlWriter.WriteAttributeString("revision", XdxfVersion); // app version + sqlite version

                xmlWriter.WriteStartElement("description");
                xmlWriter.WriteRaw(_genericDictionary.Description);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("full_name");
                xmlWriter.WriteRaw(_genericDictionary.FullName);
                xmlWriter.WriteEndElement();

                // Meta
                xmlWriter.WriteStartElement("meta_info");

                xmlWriter.WriteStartElement("title");
                xmlWriter.WriteRaw(_genericDictionary.Name);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("full_title");
                xmlWriter.WriteRaw(_genericDictionary.FullName);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("description");
                xmlWriter.WriteRaw(_genericDictionary.Description);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("file_ver");
                xmlWriter.WriteRaw(_genericDictionary.Version);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("creation_date");
                xmlWriter.WriteRaw($"{DateTime.UtcNow:dd-MM-yyyy}");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteEndElement();
                xmlWriter.Flush();

                // Lexicon
                xmlWriter.WriteStartElement("lexicon");

                foreach (KeyValuePair<Meaning, List<Definition>> article in _genericDictionary.AllWords)
                {
                    CreateArticle(xmlWriter, article.Key, article.Value);

                    // Notify progression
                    wordsWritten++;
                    if (wordsWritten % 5000 == 0)
                    {
                        int percent = (int)(wordsWritten * 100.0 / wordsToWrite);
                        Messaging.Send(MessageLevel.Info, $"Writing ... {percent:000}%");
                    }
                }

                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
            });
            export.Start();
            await export;

            Messaging.Send(MessageLevel.Info, "Done, .xdxf written ...");
        }

        private static void CreateArticle(XmlWriter xmlWriter, Meaning meaning, IEnumerable<Definition> definitions)
        {
            // Article
            xmlWriter.WriteStartElement("ar");

            // Main keyword
            xmlWriter.WriteStartElement("k");
            xmlWriter.WriteRaw(meaning.Word);
            xmlWriter.WriteEndElement();

            // Alternate keywords
            foreach (string word in meaning.AlternateWords)
            {
                xmlWriter.WriteStartElement("k");
                xmlWriter.WriteRaw(word);
                xmlWriter.WriteEndElement();
            }

            // Re-order definitions
            List<Definition> orderedDefinitions =
                definitions
                    .OrderBy(d => d.MeaningId)
                    .ThenBy(d => d.Position)
                    .Distinct()
                    .ToList();

            // Definitions
            foreach (Definition definition in orderedDefinitions)
            {
                xmlWriter.WriteStartElement("def");

                // Grammar (already in the def as an "&emdash; verb"
                //XmlElement gr = xml.CreateElement("gr");
                //gr.InnerText = definition.WordType;
                //def.InsertBefore(gr, first);

                // Transcription/pronunciation 
                if (!string.IsNullOrWhiteSpace(meaning.PronounciationIpa))
                {
                    xmlWriter.WriteStartElement("tr");
                    xmlWriter.WriteAttributeString("format", "IPA");
                    xmlWriter.WriteRaw($"IPA: /{meaning.PronounciationIpa}");
                    xmlWriter.WriteEndElement();
                }
                if (!string.IsNullOrWhiteSpace(meaning.PronounciationSpell))
                {
                    xmlWriter.WriteStartElement("tr");
                    xmlWriter.WriteAttributeString("format", "Spelling");
                    xmlWriter.WriteRaw($"Spell: [{meaning.PronounciationSpell}]");
                    xmlWriter.WriteEndElement();
                }
                if (!string.IsNullOrWhiteSpace(meaning.Syllable))
                {
                    xmlWriter.WriteStartElement("tr");
                    xmlWriter.WriteAttributeString("format", "Syllable");
                    xmlWriter.WriteRaw($"Syllable: {meaning.Syllable}");
                    xmlWriter.WriteEndElement();
                }

                // Internet ressource shows in the definition, not good for now
                //XmlElement iref = xml.CreateElement("rref");
                //iref.SetAttribute("lctn", word.AudioFile);
                //iref.SetAttribute("type", "audio/opus");
                //iref.InnerText = "Audio";
                //def.InsertBefore(iref, defTop);

                // TODO Add <sr> antonyms and shit from thesaurus?

                // Write definition as a DOM structure
                using (StringReader stringReader = new StringReader(definition.DefinitionHtml))
                {
                    using (XmlReader reader = XmlReader.Create(stringReader, DefinitionReaderSettings))
                    {
                        xmlWriter.WriteNode(reader, true);
                    }
                }

                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
        }
    }
}