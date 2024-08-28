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

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    public class RmqMessageProducerFactory
        : IAmAMessageProducerFactory
    {
        private readonly RmqMessagingGatewayConnection _connection;
        private readonly IEnumerable<RmqPublication> _publications;

        /// <summary>
        /// Creates a collection of RabbitMQ message producers from the RabbitMQ publication information
        /// </summary>
        /// <param name="connection">The connection to use to connect to RabbitMQ</param>
        /// <param name="publications">The publications describing the RabbitMQ topics that we want to use</param>
        public RmqMessageProducerFactory(RmqMessagingGatewayConnection connection, IEnumerable<RmqPublication> publications)
        {
            _connection = connection;
            _publications = publications;
        }

        /// <inheritdoc />
        public Dictionary<string,IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<string, IAmAMessageProducer>();
            foreach (var publication in _publications)
            {
                producers[publication.Topic] = new RmqMessageProducer(_connection, publication);
            }

            return producers;
        }
    }
}
