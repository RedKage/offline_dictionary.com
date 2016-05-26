using System;
using System.Windows;
using offline_dictionary.com_export_jsondump;
using offline_dictionary.com_export_stardict;
using offline_dictionary.com_export_xdxf;
using offline_dictionary.com_reader_jsondump;
using offline_dictionary.com_reader_sqlite;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com
{
    public partial class MainWindow
    {
        private const string SqliteFilePath =
            @"F:\android-08-08-primary.sqlite";

        private const string JsonDumpFilePath =
            @"F:\dictionary.com-5.5.2_08-08.json.gz";

        private const string JsonDumpOutDirPath =
            @"F:\";

        private const string OutDirPath =
            @"D:\Work\Dev\offline_dictionary.com\out\X-StarDict_3.0.4_rev10\Bin\StarDict\dic\dictionary.com-5.5.2_08-08";

#if DEBUG
        private const int LoadWordsLimit = -1;
#endif
#if !DEBUG
        private const int LoadWordsLimit = -1;
#endif

        public GenericDictionary Dictionary { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            DisableExports();
            ConsoleOut("Ready.");
        }

        private void LoadingProgression(object sender, LoadingProgressInfo loadingProgressInfo)
        {
            ConsoleOut(loadingProgressInfo.ToString());
        }

        private void ExportingProgression(object sender, ExportingProgressInfo exportingProgressInfo)
        {
            ConsoleOut(exportingProgressInfo.ToString());
        }

        private void ConsoleOut(string message)
        {
            DateTime now = DateTime.Now;

            string[] lines = message.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                Console.AppendText($"[{now.Hour:00}:{now.Minute:00}:{now.Second:00}] {line}{Environment.NewLine}");
            }
            
            Console.ScrollToEnd();
        }

        private void DisableLoaders()
        {
            LoadFromSqliteButton.IsEnabled =
            LoadFromJsonDumpButton.IsEnabled =
                false;
        }

        private void EnableLoaders()
        {
            LoadFromSqliteButton.IsEnabled =
            LoadFromJsonDumpButton.IsEnabled =
                true;
        }

        private void DisableExports()
        {
            ExportToXdxfButton.IsEnabled =
            ExportToStarDictButton.IsEnabled =
            ExportToJsonDumpButton.IsEnabled =
                false;
        }

        private void EnableExports()
        {
            ExportToXdxfButton.IsEnabled = 
            ExportToStarDictButton.IsEnabled =
            ExportToJsonDumpButton.IsEnabled =
                true;
        }

        #region Events

        private async void LoadFromSqliteButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOut($"Loading SQLite '{SqliteFilePath}' ...");

            DisableExports();
            DisableLoaders();

            LoadFromSqlite loadFromSqlite = new LoadFromSqlite(SqliteFilePath, LoadWordsLimit);

            Progress<LoadingProgressInfo> extractProgress = new Progress<LoadingProgressInfo>();
            extractProgress.ProgressChanged += LoadingProgression;

            Dictionary = await loadFromSqlite.LoadAsync(extractProgress);

            EnableExports();
            EnableLoaders();

            ConsoleOut("Loading done:");
            ConsoleOut($"{Dictionary}{Environment.NewLine}");
        }

        private async void LoadFromJsonDumpButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOut($"Loading JSON dump '{JsonDumpFilePath}' ...");

            DisableExports();
            DisableLoaders();

            LoadFromJsonDump loadFromJsonDump = new LoadFromJsonDump(JsonDumpFilePath);

            Progress<LoadingProgressInfo> extractProgress = new Progress<LoadingProgressInfo>();
            extractProgress.ProgressChanged += LoadingProgression;

            Dictionary = await loadFromJsonDump.LoadAsync(extractProgress);

            EnableExports();
            EnableLoaders();

            ConsoleOut("Loading done:");
            ConsoleOut($"{Dictionary}{Environment.NewLine}");
        }

        private async void ConvertToXdxfButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOut($"Exporting to XDXF to '{OutDirPath}' ...");

            DisableExports();
            DisableLoaders();

            ExportXdxf exportXdxf = new ExportXdxf(Dictionary, OutDirPath);

            Progress<ExportingProgressInfo> exportingProgress = new Progress<ExportingProgressInfo>();
            exportingProgress.ProgressChanged += ExportingProgression;

            await exportXdxf.ExportAsync(exportingProgress);

            EnableExports();
            EnableLoaders();

            ConsoleOut($"Exported!{Environment.NewLine}");
        }

        private async void ConvertToStarDictButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOut($"Exporting to StarDict to '{OutDirPath}' ...");

            DisableExports();
            DisableLoaders();

            ExportStarDict exportStarDict = new ExportStarDict(Dictionary, OutDirPath);

            Progress<ExportingProgressInfo> exportingProgress = new Progress<ExportingProgressInfo>();
            exportingProgress.ProgressChanged += ExportingProgression;

            await exportStarDict.ExportAsync(exportingProgress);

            EnableExports();
            EnableLoaders();

            ConsoleOut($"Exported!{Environment.NewLine}");
        }

        private async void ConvertToJsonDumpButton_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOut($"Exporting to JSON dump to '{JsonDumpOutDirPath}' ...");

            DisableExports();
            DisableLoaders();

            ExportJsonDump exportJsonDump = new ExportJsonDump(Dictionary, JsonDumpOutDirPath);

            Progress<ExportingProgressInfo> exportingProgress = new Progress<ExportingProgressInfo>();
            exportingProgress.ProgressChanged += ExportingProgression;

            await exportJsonDump.ExportAsync(exportingProgress);

            EnableExports();
            EnableLoaders();

            ConsoleOut($"Exported!{Environment.NewLine}");
        }

        #endregion
    }
}