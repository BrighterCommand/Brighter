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
using System.Linq;
using Amqp;
using paramore.brighter.commandprocessor.messaginggateway.azureservicebus.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.azureservicebus
{
    public class MessageSenderPool : IDisposable
    {
        private static readonly ILog _logger = LogProvider.For<MessageSenderPool>();

        private readonly ConcurrentDictionary<string, SenderLink> _senders = new ConcurrentDictionary<string, SenderLink>();
        private bool _closing;

        public SenderLink GetMessageSender(string topic)
        {
            if (_senders.ContainsKey(topic))
                return _senders[topic];

            Address address = new Address("amqp://guest:guest@localhost:5672");
            Connection connection = Connection.Factory.CreateAsync(address).Result;
            Session session = new Session(connection);
            SenderLink sender = new SenderLink(session, "sender", topic);

            _senders.TryAdd(topic, sender);
            sender.Closed += Sender_Closed;
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
            _closing = true;
            if (disposing)
            {
                foreach (var sender in _senders.Values)
                {
                    sender.Close();
                }
            }
        }

        private void Sender_Closed(AmqpObject sender, Amqp.Framing.Error error)
        {
            if (!_closing)
            {
                var senderLink = (SenderLink) sender;
                var key =
                    _senders.ToArray().Where(pair => pair.Value == senderLink).Select(pair => pair.Key).FirstOrDefault();
                if (key != null)
                {
                    SenderLink returnedLink;
                    _senders.TryRemove(key, out returnedLink);
                }
            }
        }
    }
}