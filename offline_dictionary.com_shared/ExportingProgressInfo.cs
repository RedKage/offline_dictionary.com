namespace offline_dictionary.com_shared
{
    public class ExportingProgressInfo
    {
        public int WordsCountToWrite { get; set; }
        public int WordsWritten { get; set; }

        public bool Done => WordsWritten >= WordsCountToWrite;

        public override string ToString()
        {
            int completionPercent =
                WordsCountToWrite == 0
                    ? 0
                    : WordsWritten*100/WordsCountToWrite;

            return $"Write...\t{completionPercent}%\t\t({WordsWritten})";
        }
    }
}