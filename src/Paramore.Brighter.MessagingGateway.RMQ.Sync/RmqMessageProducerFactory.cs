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

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    /// <summary>
    /// Creates a collection of RabbitMQ message producers from the RabbitMQ publication information
    /// </summary>
    /// <remarks>This version of the consumer supports the RMQ V6 Client and its blocking API. For support of the V7 non-blocking API, please use
    /// the package Paramore.Brighter.MessagingGateway.RMQ.Async. As such, its SendAsync methods do not do true Async. Instead they rely on Run.Thread to mimic
    /// an Async operation.
    /// </remarks>
    /// <param name="connection">The connection to use to connect to RabbitMQ</param>
    /// <param name="publications">The publications describing the RabbitMQ topics that we want to use</param>
    public class RmqMessageProducerFactory(
        RmqMessagingGatewayConnection connection,
        IEnumerable<RmqPublication> publications)
        : IAmAMessageProducerFactory
    {
        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <returns>A dictionary of middleware clients by topic/routing key, for sending messages to the middleware</returns>
        public Dictionary<RoutingKey,IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
            foreach (var publication in publications)
            {
                if (publication.Topic is null)
                    throw new ConfigurationException("RmqMessageProducerFactory.Create => An RmqPublication must have a topic/routing key");
                producers[publication.Topic] = new RmqMessageProducer(connection, publication);
            }

            return producers;
        }

        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <remarks>Not implemented in this package. This package supports only RMQ.Client V6 which is blocking, use the Paramore.Brighter.MessagingGateway.RMQ.Async for async clients</remarks>
        /// <returns>A dictionary of middleware clients by topic/routing key, for sending messages to the middleware</returns>
        public Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
