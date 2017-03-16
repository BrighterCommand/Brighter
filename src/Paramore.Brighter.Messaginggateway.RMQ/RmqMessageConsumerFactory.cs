#region Licence
/* The MIT License (MIT)
Copyright © 2014 Toby Henderson 

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

using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class RmqMessageConsumerFactory.
    /// </summary>
    public class RmqMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly RmqMessagingGatewayConnection _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageConsumerFactory"/> class.
        /// </summary>
        public RmqMessageConsumerFactory(RmqMessagingGatewayConnection  connection)
        {
            _connection = connection;
        }

        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">Is the consumer target durable i.e. channel stores messages between restarts of consumer</param>
        /// <param name="preFetchSize">0="Don't send me a new message until I?ve finished",  1= "Send me one message at a time", n = number to grab (take care with competing consumers)</param>
        /// <param name="highAvailability">Does the queue exist in multiple nodes</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(string queueName, string routingKey, bool isDurable, ushort preFetchSize = 1, bool highAvailability = false)
        {
            return new RmqMessageConsumer(_connection, queueName, routingKey, isDurable, preFetchSize, highAvailability);
        }
    }
}