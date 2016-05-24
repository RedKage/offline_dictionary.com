namespace offline_dictionary.com_reader
{
    public class ExtractingProgressInfo
    {
        public int WordsCountToAdd { get; set; }
        public int WordsAdded { get; set; }

        public bool Done => WordsAdded >= WordsCountToAdd;

        public override string ToString()
        {
            int completionPercent =
                WordsCountToAdd == 0
                    ? 0
                    : WordsAdded * 100 / WordsCountToAdd;

            return $"Add...\t{completionPercent}%\t\t({WordsAdded})";
        }
    }
}
