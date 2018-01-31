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
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusMessageProducer : MessageGateway, IAmAMessageProducer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<AzureServiceBusMessageProducer>);

        private readonly MessageSenderPool _pool;

        public AzureServiceBusMessageProducer(AzureServiceBusMessagingGatewayConfiguration configuration)
            : base(configuration)
        {
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
         public void Send(Message message)
        {
            _logger.Value.DebugFormat("AzureServiceBusMessageProducer: Publishing message to topic {0}", message.Header.Topic);
            var messageSender = _pool.GetMessageSender(message.Header.Topic);
            EnsureTopicExists(message.Header.Topic);
            messageSender.Send(new Amqp.Message(message.Body.Value));
            _logger.Value.DebugFormat("AzureServiceBusMessageProducer: Published message with id {0} to topic '{1}' and content: {2}", message.Id, message.Header.Topic, JsonConvert.SerializeObject(message));
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">The sending delay</param>
        /// <returns>Task.</returns>
         public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Send(message);
        }

        public bool DelaySupported => false;

        ~AzureServiceBusMessageProducer()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pool?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}