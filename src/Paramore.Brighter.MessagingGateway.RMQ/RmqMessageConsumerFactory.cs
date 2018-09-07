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

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    public class RmqMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly RmqMessagingGatewayConnection _rmqConnection;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageConsumerFactory"/> class.
        /// <param name="rmqConnection">The connection to the broker hosting the queue</param>
        /// </summary>
        public RmqMessageConsumerFactory(RmqMessagingGatewayConnection  rmqConnection)
        {
            _rmqConnection = rmqConnection;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="connection">The queue to connect to</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(Connection connection)
        {
            return new RmqMessageConsumer(
                _rmqConnection, 
                connection.ChannelName, //RMQ Queue Name 
                connection.RoutingKey, 
                connection.IsDurable, 
                connection.HighAvailability);
        }
    }
}
