#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class ClientRequestHandler .
    /// The <see cref="RmqMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
    /// It handles connection establishment, request sending and error handling
    /// </summary>
    public class RmqMessageProducer : RMQMessageGateway, IAmAMessageProducer, IAmAMessageProducerAsync
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RmqMessageProducer>);

        static readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RMQMessageGateway" /> class.
        /// </summary>
        /// <param name="connection">The connection information needed to talk to RMQ</param>
        public RmqMessageProducer(RmqMessagingGatewayConnection connection) : base(connection, 1)
        {
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            SendWithDelay(message);
        }

        /// <summary>
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        /// <returns>Task.</returns>
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            try
            {
                lock (_lock)
                {
                    _logger.Value.DebugFormat("RmqMessageProducer: Preparing  to send message via exchange {0}",
                        Connection.Exchange.Name);
                    EnsureChannel();
                    var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection.Exchange.Name);

                    message.Persist = Connection.PersistMessages;

                    _logger.Value.DebugFormat(
                        "RmqMessageProducer: Publishing message to exchange {0} on connection {1} with a delay of {5} and topic {2} and persisted {6} and id {3} and body: {4}",
                        Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), message.Header.Topic,
                        message.Id, message.Body.Value, delayMilliseconds, message.Persist);
                    if (DelaySupported)
                    {
                        rmqMessagePublisher.PublishMessage(message, delayMilliseconds);
                    }
                    else
                    {
                        Task.Delay(delayMilliseconds).Wait();
                        rmqMessagePublisher.PublishMessage(message, 0);
                    }

                    _logger.Value.InfoFormat(
                        "RmqMessageProducer: Published message to exchange {0} on connection {1} with a delay of {6} and topic {2} and persisted {7} and id {3} and message: {4} at {5}",
                        Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), message.Header.Topic,
                        message.Id, JsonConvert.SerializeObject(message), DateTime.UtcNow, delayMilliseconds, message.Persist);
                }
            }
            catch (IOException io)
            {
                _logger.Value.ErrorFormat(
                    "RmqMessageProducer: Error talking to the socket on {0}, resetting connection",
                    Connection.AmpqUri.GetSanitizedUri()
                    );
                ResetConnectionToBroker();
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", io);
            }
        }

        public Task SendAsync(Message message)
        {
            throw new NotImplementedException();
        }
    }
}
