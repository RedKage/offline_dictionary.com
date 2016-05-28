using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using offline_dictionary.com_export_jsondump;
using offline_dictionary.com_export_stardict;
using offline_dictionary.com_export_xdxf;
using offline_dictionary.com_reader_jsondump;
using offline_dictionary.com_reader_sqlite;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Messaging;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com
{
    public partial class MainWindow
    {
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

        private string ChooseFile(string title, string extensionTitle, string extension)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                Filters = {
                    new CommonFileDialogFilter
                    {
                        DisplayName = extensionTitle,
                        Extensions = { $"{extension}" },
                        ShowExtensions = true
                    }
                },
                EnsureFileExists = true,
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Multiselect = false,
                Title = title
            };

            CommonFileDialogResult result = dialog.ShowDialog();
            return result == CommonFileDialogResult.Ok
                ? dialog.FileName
                : null;
        }

        private string ChooseFolder(string title)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsureFileExists = true,
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Multiselect = false,
                Title = title
            };

            CommonFileDialogResult result = dialog.ShowDialog();
            return result == CommonFileDialogResult.Ok
                ? dialog.FileName
                : null;
        }

        #region Events

        private async void LoadFromSqliteButton_Click(object sender, RoutedEventArgs e)
        {
            string sqliteFilePath = ChooseFile("Select the SQLite file", "SQLite database", "sqlite");
            if(string.IsNullOrEmpty(sqliteFilePath))
                return;

            Messaging.Send($"Loading SQLite '{sqliteFilePath}' ...");

            DisableExports();
            DisableLoaders();

            try
            {
                IReader loadFromSqlite = new LoadFromSqlite(sqliteFilePath, LoadWordsLimit);
                Dictionary = await loadFromSqlite.LoadAsync();

                EnableExports();

                Messaging.Send("Loading done:");
                Messaging.Send($"{Dictionary}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Messaging.Send(MessageLevel.Fatal, ex.Message);
            }
            finally
            {
                EnableLoaders();
            }
        }

        private async void LoadFromJsonDumpButton_Click(object sender, RoutedEventArgs e)
        {
            string jsonDumpFilePath = ChooseFile("Select the JSON dump file", "Compressed JSON dump", "json.gz");
            if (string.IsNullOrEmpty(jsonDumpFilePath))
                return;

            Messaging.Send($"Loading JSON dump '{jsonDumpFilePath}' ...");

            DisableExports();
            DisableLoaders();

            try
            {
                IReader loadFromJsonDump = new LoadFromJsonDump(jsonDumpFilePath);
                Dictionary = await loadFromJsonDump.LoadAsync();

                EnableExports();

                Messaging.Send("Loading done:");
                Messaging.Send($"{Dictionary}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Messaging.Send(MessageLevel.Fatal, ex.Message);
            }
            finally
            {
                EnableLoaders();
            }
        }

        private async void ConvertToXdxfButton_Click(object sender, RoutedEventArgs e)
        {
            string outDirPath = ChooseFolder("Select where to save the XDXF");
            if (string.IsNullOrEmpty(outDirPath))
                return;

            Messaging.Send($"Exporting to XDXF to '{outDirPath}' ...");

            DisableExports();
            DisableLoaders();

            try
            {
                IExporter exportXdxf = new ExportXdxf(Dictionary, outDirPath);
                await exportXdxf.ExportAsync();

                Messaging.Send($"Exported!{Environment.NewLine}");
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

        private async void ConvertToStarDictButton_Click(object sender, RoutedEventArgs e)
        {
            string outDirPath = ChooseFolder("Select where to save the StarDict files");
            if (string.IsNullOrEmpty(outDirPath))
                return;

            Messaging.Send($"Exporting to StarDict to '{outDirPath}' ...");

            DisableExports();
            DisableLoaders();

            try
            {
                IExporter exportStarDict = new ExportStarDict(Dictionary, outDirPath);
                await exportStarDict.ExportAsync();

                Messaging.Send($"Exported!{Environment.NewLine}");
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

        private async void ConvertToJsonDumpButton_Click(object sender, RoutedEventArgs e)
        {
            string outDirPath = ChooseFolder("Select where to save the JSON dump file");
            if (string.IsNullOrEmpty(outDirPath))
                return;

            Messaging.Send($"Exporting to JSON dump to '{outDirPath}' ...");

            DisableExports();
            DisableLoaders();

            try
            {
                IExporter exportJsonDump = new ExportJsonDump(Dictionary, outDirPath);
                await exportJsonDump.ExportAsync();

                Messaging.Send($"Exported!{Environment.NewLine}");
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

        #endregion
    }
}