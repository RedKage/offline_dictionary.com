namespace offline_dictionary.com_reader.Model
{
    public class Meaning
    {
        public int Id { get; set; }
        public string Word { get; set; }
        public string[] AlternateWords { get; set; }
        public string Syllable { get; set; }
        public string PronounciationSpell { get; set; }
        public string PronounciationIpa { get; set; }
        public string AudioFile { get; set; }

        public override bool Equals(object obj)
        {
            Meaning meaning = obj as Meaning;
            if (meaning == null)
            {
                return false;
            }

            // Return true if the ID match
            return Id == meaning.Id;
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override string ToString()
        {
            return $"'{Word}' #{Id}";
        }
    }
}