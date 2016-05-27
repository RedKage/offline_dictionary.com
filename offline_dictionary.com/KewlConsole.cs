using offline_dictionary.com_shared.Messaging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace offline_dictionary.com
{
    public class KewlConsole
    {
        private static readonly Brush ConsoleBgColor = Brushes.LightGray;
        private readonly SynchronizationContext _uiContext;
        private readonly RichTextBox _console;
        
        public KewlConsole(SynchronizationContext uiContext, RichTextBox consoleRichTextBox)
        {
            _uiContext = uiContext;
            _console = consoleRichTextBox;
            _console.Background = ConsoleBgColor;
        }

        public async Task Out(ICollection<MessageObject> messages)
        {
            foreach (MessageObject messageObject in messages)
            {
                await Out(messageObject);
            }
        }

        public async Task Out(MessageObject message)
        {
            Task consoleOut = new Task(() =>
            {
                DateTime now = DateTime.Now;
                string kewlLevel;
                Brush foregroundColor, backgroundColor;

                switch (message.Level)
                {
                    case MessageLevel.Debug:
                        kewlLevel = "[Ò___Ó] ";
                        foregroundColor = Brushes.Black;
                        backgroundColor = Brushes.DarkGray;
                        break;
                    case MessageLevel.Info:
                        kewlLevel = "[o___o] ";
                        foregroundColor = Brushes.Black;
                        backgroundColor = ConsoleBgColor;
                        break;
                    case MessageLevel.Warn:
                        kewlLevel = "[O___o;]";
                        foregroundColor = Brushes.Black;
                        backgroundColor = Brushes.Yellow;
                        break;
                    case MessageLevel.Error:
                        kewlLevel = "[X___-;]";
                        foregroundColor = Brushes.Black;
                        backgroundColor = Brushes.DarkOrange;
                        break;
                    case MessageLevel.Fatal:
                        kewlLevel = "[X___x;]";
                        foregroundColor = Brushes.White;
                        backgroundColor = Brushes.DarkRed;
                        break;
                    default:
                        kewlLevel = "[      ]";
                        foregroundColor = Brushes.Black;
                        backgroundColor = ConsoleBgColor;
                        break;
                }

                string[] lines = message.Message.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    _uiContext.Send(x =>
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            kewlLevel = string.Empty;

                        TextRange textRange = new TextRange(_console.Document.ContentEnd, _console.Document.ContentEnd)
                        {
                            Text = $" [{now.Hour:00}:{now.Minute:00}:{now.Second:00}] {kewlLevel} {line} {Environment.NewLine}"
                        };

                        textRange.ApplyPropertyValue(TextElement.ForegroundProperty, foregroundColor);
                        textRange.ApplyPropertyValue(TextElement.BackgroundProperty, backgroundColor);
                    }, null);
                }

                _uiContext.Send(x => _console.ScrollToEnd(), null);
            }, TaskCreationOptions.LongRunning);
            consoleOut.Start();
            await consoleOut;
        }
    }
}
