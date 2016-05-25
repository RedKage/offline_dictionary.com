using System;
using offline_dictionary.com_export_stardict;
using offline_dictionary.com_export_xdxf;
using offline_dictionary.com_reader;
using offline_dictionary.com_reader.Model;
using offline_dictionary.com_shared;

namespace offline_dictionary.com
{
    public partial class MainWindow
    {
        //private static string DbFilePath = @"Dictionaries\dictionary.com_5.2.2\android-08-08-primary.sqlite";
        private const string DbFilePath = @"F:\android-08-08-primary.sqlite";

        private const string OutDirPath =
            @"D:\Work\Dev\offline_dictionary.com\out\X-StarDict_3.0.4_rev10\Bin\StarDict\dic\dictionary.com-5.5.2_08-08";

        public GenericDictionary Dictionary { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ProgressionCheck(object sender, ExtractingProgressInfo extractingProgressInfo)
        {
            ConsoleOut(extractingProgressInfo.ToString());
        }

        private void ExportCheck(object sender, ExportingProgressInfo exportingProgressInfo)
        {
            ConsoleOut(exportingProgressInfo.ToString());
        }

        private void ConsoleOut(string message)
        {
            Console.AppendText($"{message}{Environment.NewLine}");
            Console.ScrollToEnd();
        }

        private async void ExtractFromDbButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ConsoleOut($"Extracting {DbFilePath} ...");

            ConvertToXdxfButton.IsEnabled = false;
            ConvertToStarDictButton.IsEnabled = false;

            ExtractFromDb extractFromDb = new ExtractFromDb(DbFilePath);

            Progress<ExtractingProgressInfo> extractProgress = new Progress<ExtractingProgressInfo>();
            extractProgress.ProgressChanged += ProgressionCheck;

            Dictionary = await extractFromDb.ExtractAsync(extractProgress);

            ConvertToXdxfButton.IsEnabled = true;
            ConvertToStarDictButton.IsEnabled = true;

            ConsoleOut("Extracting done:");
            ConsoleOut($"{Dictionary}{Environment.NewLine}");
        }

        private async void ConvertToXdxfButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ConsoleOut($"Exporting to XDXF in {OutDirPath} ...");

            ExportXdxf exportXdxf = new ExportXdxf(Dictionary, OutDirPath);

            Progress<ExportingProgressInfo> exportingProgress = new Progress<ExportingProgressInfo>();
            exportingProgress.ProgressChanged += ExportCheck;

            await exportXdxf.ExportAsync(exportingProgress);

            ConsoleOut($"Exported!{Environment.NewLine}");
        }

        private async void ConvertToStarDictButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ConsoleOut($"Exporting to StarDict in {OutDirPath} ...");

            ExportStarDict exportStarDict = new ExportStarDict(Dictionary, OutDirPath);

            Progress<ExportingProgressInfo> exportingProgress = new Progress<ExportingProgressInfo>();
            exportingProgress.ProgressChanged += ExportCheck;

            await exportStarDict.ExportAsync(exportingProgress);

            ConsoleOut($"Exported!{Environment.NewLine}");
        }
    }
}