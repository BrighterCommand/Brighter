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

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SnsMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly IEnumerable<SnsPublication> _snsPublications;

        /// <summary>
        /// Creates a collection of SNS message producers from the SNS publication information
        /// </summary>
        /// <param name="connection">The Connection to use to connect to AWS</param>
        /// <param name="snsPublications">The publications describing the SNS topics that we want to use</param>
        public SnsMessageProducerFactory(
            AWSMessagingGatewayConnection connection,
            IEnumerable<SnsPublication> snsPublications)
        {
            _connection = connection;
            _snsPublications = snsPublications;
        }

        /// <inheritdoc />
        public Dictionary<string,IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<string, IAmAMessageProducer>();
            foreach (var p in _snsPublications)
            {
                var producer = new SqsMessageProducer(_connection, p);
                if (producer.ConfirmTopicExists())
                    producers[p.Topic] = producer;
                else
                    throw new ConfigurationException($"Missing SNS topic: {p.Topic}");
            }

            return producers;
        }
    }
}
