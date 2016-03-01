#region Licence
/* The MIT License (MIT)
Copyright © 2015 Yiannis Triantafyllopoulos <yiannis.triantafyllopoulos@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Concurrent;
using Microsoft.ServiceBus.Messaging;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.azureservicebus
{
    public class MessageSenderPool : IDisposable
    {
        private readonly ILog logger;
        private readonly MessagingFactory factory;
        private readonly ConcurrentDictionary<string, MessageSender> senders = new ConcurrentDictionary<string, MessageSender>();

        public MessageSenderPool(ILog logger, MessagingFactory factory)
        {
            this.logger = logger;
            this.factory = factory;
        }

        public MessageSender GetMessageSender(string path)
        {
            if (senders.ContainsKey(path))
                return senders[path];

            var sender = factory.CreateMessageSender(path);
            senders.TryAdd(path, sender);
            return sender;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageSenderPool()
        {
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var sender in senders.Values)
                {
                    if (!sender.IsClosed)
                    {
                        sender.Close();
                    }
                }
            }
        }
    }
}