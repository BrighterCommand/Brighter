#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly RedisMessagingGatewayConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageConsumerFactory"/> class.
        /// </summary>
        public RedisMessageConsumerFactory(RedisMessagingGatewayConfiguration configuration)
        {
            _configuration = configuration;
        }


        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">Ignored, depends on Redis persistence of database.</param>
        /// <param name="highAvailability">Does the queue exist in multiple nodes (depends on Redis clustering,  not Brighter</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(string channelName, string routingKey, bool isDurable,
            bool highAvailability)
        {
            return new RedisMessageConsumer(_configuration, channelName, routingKey);
        }
    }
}
