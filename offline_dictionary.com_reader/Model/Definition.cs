using System.Collections.Generic;

namespace offline_dictionary.com_reader.Model
{
    public class Definition
    {
        public string Headword { get; set; }
        public int MeaningId { get; set; }
        public string WordType { get; set; }
        public int Position { get; set; }
        public string DefinitionHtml { get; set; }
        public List<string> Synonyms { get; set; }
        public List<string> Antonyms { get; set; }

        public override string ToString()
        {
            return $"#{MeaningId} *{Position} '{Headword}' ({WordType})";
        }
    }
}