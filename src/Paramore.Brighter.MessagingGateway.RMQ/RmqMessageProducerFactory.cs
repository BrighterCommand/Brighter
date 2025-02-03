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

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Creates a collection of RabbitMQ message producers from the RabbitMQ publication information
    /// </summary>
    /// <param name="connection">The connection to use to connect to RabbitMQ</param>
    /// <param name="publications">The publications describing the RabbitMQ topics that we want to use</param>
    public class RmqMessageProducerFactory(
        RmqMessagingGatewayConnection connection,
        IEnumerable<RmqPublication> publications)
        : IAmAMessageProducerFactory
    {
        /// <inheritdoc />
        public Dictionary<RoutingKey, IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
            foreach (var publication in publications)
            {
                if (publication.Topic is null || RoutingKey.IsNullOrEmpty(publication.Topic))
                {
                    throw new ConfigurationException($"A RabbitMQ publication must have a topic");
                }

                producers[publication.Topic] = new RmqMessageProducer(connection, publication);
            }

            return producers;
        }

        /// <inheritdoc /> 
        public Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
        {
            return Task.FromResult(Create());
        }
    }
}
