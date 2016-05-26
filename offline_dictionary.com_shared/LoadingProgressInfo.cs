namespace offline_dictionary.com_shared
{
    public class LoadingProgressInfo
    {
        public int WordsCountToAdd { get; set; }
        public int WordsAdded { get; set; }

        public bool Done => WordsAdded >= WordsCountToAdd;

        public override string ToString()
        {
            int completionPercent =
                WordsCountToAdd == 0
                    ? 0
                    : WordsAdded*100/WordsCountToAdd;

            return $"Loading...\t{completionPercent:000}%\t({WordsAdded})";
        }
    }
}