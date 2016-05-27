using offline_dictionary.com_export_jsondump;
using offline_dictionary.com_export_stardict;
using offline_dictionary.com_export_xdxf;
using offline_dictionary.com_reader_jsondump;
using offline_dictionary.com_reader_sqlite;
using offline_dictionary.com_shared.Messaging;
using offline_dictionary.com_shared.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;

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
        private const int LoadWordsLimit = 1000;
#endif
#if !DEBUG
        private const int LoadWordsLimit = -1;
#endif

        private SynchronizationContext UiContext { get; }

        private GenericDictionary Dictionary { get; set; }

        private KewlConsole KewlConsole { get; }


        public MainWindow()
        {
            UiContext = SynchronizationContext.Current;

            InitializeComponent();

            DisableExports();

            KewlConsole = new KewlConsole(UiContext, Console);

            Messaging.MessagePooling += Messaging_OnMessagePooling;
            Messaging.Send($"Ready{Environment.NewLine}");
        }

        private async void Messaging_OnMessagePooling(ICollection<MessageObject> newMessages)
        {
            await KewlConsole.Out(newMessages);
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
            Messaging.Send($"Loading SQLite '{SqliteFilePath}' ...");

            DisableExports();
            DisableLoaders();

            try
            {
                LoadFromSqlite loadFromSqlite = new LoadFromSqlite(SqliteFilePath, LoadWordsLimit);
                Dictionary = await loadFromSqlite.LoadAsync();

                Messaging.Send("Loading done:");
                Messaging.Send($"{Dictionary}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Messaging.Send(MessageLevel.Fatal, ex.Message);
            }
            finally
            {
                EnableExports();
                EnableLoaders();
            }
        }

        private async void LoadFromJsonDumpButton_Click(object sender, RoutedEventArgs e)
        {
            Messaging.Send($"Loading JSON dump '{JsonDumpFilePath}' ...");

            DisableExports();
            DisableLoaders();

            LoadFromJsonDump loadFromJsonDump = new LoadFromJsonDump(JsonDumpFilePath);

            Dictionary = await loadFromJsonDump.LoadAsync(null);

            EnableExports();
            EnableLoaders();

            Messaging.Send("Loading done:");
            Messaging.Send($"{Dictionary}{Environment.NewLine}");
        }

        private async void ConvertToXdxfButton_Click(object sender, RoutedEventArgs e)
        {
            Messaging.Send($"Exporting to XDXF to '{OutDirPath}' ...");

            DisableExports();
            DisableLoaders();

            ExportXdxf exportXdxf = new ExportXdxf(Dictionary, OutDirPath);

            await exportXdxf.ExportAsync(null);

            EnableExports();
            EnableLoaders();

            Messaging.Send($"Exported!{Environment.NewLine}");
        }

        private async void ConvertToStarDictButton_Click(object sender, RoutedEventArgs e)
        {
            Messaging.Send($"Exporting to StarDict to '{OutDirPath}' ...");

            DisableExports();
            DisableLoaders();

            ExportStarDict exportStarDict = new ExportStarDict(Dictionary, OutDirPath);

            await exportStarDict.ExportAsync(null);

            EnableExports();
            EnableLoaders();

            Messaging.Send($"Exported!{Environment.NewLine}");
        }

        private async void ConvertToJsonDumpButton_Click(object sender, RoutedEventArgs e)
        {
            Messaging.Send($"Exporting to JSON dump to '{JsonDumpOutDirPath}' ...");

            DisableExports();
            DisableLoaders();

            ExportJsonDump exportJsonDump = new ExportJsonDump(Dictionary, JsonDumpOutDirPath);

            await exportJsonDump.ExportAsync(null);

            EnableExports();
            EnableLoaders();

            Messaging.Send($"Exported!{Environment.NewLine}");
        }

        #endregion
    }
}