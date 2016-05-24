namespace offline_dictionary.com_reader.Model
{
    public class WordSuggestion
    {
        public int Position { get; set; }
        public int RootWordId { get; set; }
        public string RootWord { get; set; }
        public string Sources { get; set; }
        public SourceType Source { get; set; }
    }

    public enum SourceType
    {
        D = 68,
        T = 84,
        Dictionary = D,
        Thesaurus = T
    }
}
