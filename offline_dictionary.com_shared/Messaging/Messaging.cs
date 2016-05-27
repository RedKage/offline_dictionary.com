using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace offline_dictionary.com_shared.Messaging
{
    public static class Messaging
    {
        private const int MessagePoolingIntervalMs = 1000;
        private static readonly List<MessageObject> Messages = new List<MessageObject>();

        public static event NewMessages MessagePooling;
        public delegate void NewMessages(ICollection<MessageObject> newMessages);
        
        static Messaging()
        {
            Task messagePumping = new Task(() =>
            {
                while (true)
                {
                    lock (Messages)
                    {
                        if (MessagePooling != null && Messages.Count > 0)
                        {
                            MessagePooling(Messages.ToList());
                        }
                        Messages.Clear();
                    }

                    Thread.Sleep(MessagePoolingIntervalMs);
                }

            }, TaskCreationOptions.LongRunning);
            messagePumping.Start();
        }

        public static void Send(string message)
        {
            Send(MessageLevel.Info, message);
        }

        public static void Send(MessageLevel level, string message)
        {
            MessageObject messageObject = new MessageObject
            {
                Level = level,
                Message = message
            };
            Send(messageObject);
        }

        public static void Send(MessageObject message)
        {
            lock (Messages)
            {
                Messages.Add(message);
            }
        }
    }
}
