#region Licence
/* The MIT License (MIT)
Copyright © 2024 Dominic Hickie <dominichickie@gmail.com>

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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    /// <summary>
    /// A factory for creating Redis message producers. This is used to create a collection of producers
    /// that can send messages to Redis topics.
    /// </summary>
    public class RedisMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly RedisMessagingGatewayConfiguration _redisConfiguration;
        private readonly IEnumerable<RedisMessagePublication> _publications;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisMessageProducerFactory"/> class.
        /// </summary>
        /// <param name="redisConfiguration">The configuration settings for connecting to Redis.</param>
        /// <param name="publications">The collection of Redis message publications.</param>
        public RedisMessageProducerFactory(
            RedisMessagingGatewayConfiguration redisConfiguration,
            IEnumerable<RedisMessagePublication> publications)
        {
            _redisConfiguration = redisConfiguration;
            _publications = publications;
        }

        /// <summary>
        /// Creates a dictionary of Redis message producers.
        /// </summary>
        /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/>.</returns>
        public Dictionary<RoutingKey, IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                producers[publication.Topic!] = new RedisMessageProducer(_redisConfiguration, publication);
            }

            return producers;
        }

        /// <summary>
        /// Asynchronously creates a dictionary of Redis message producers.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/>.</returns>
        public Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
        {
            return Task.FromResult(Create());
        }
    }
}
