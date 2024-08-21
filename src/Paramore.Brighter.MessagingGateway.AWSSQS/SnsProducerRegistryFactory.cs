﻿#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SnsProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly IEnumerable<SnsPublication> _snsPublications;

        /// <summary>
        /// Create a collection of producers from the publication information
        /// </summary>
        /// <param name="connection">The Connection to use to connect to AWS</param>
        /// <param name="snsPublications">The publication describing the SNS topic that we want to use</param>
        public SnsProducerRegistryFactory(
            AWSMessagingGatewayConnection connection,
            IEnumerable<SnsPublication> snsPublications)
        {
            _connection = connection;
            _snsPublications = snsPublications;
        }

        /// <summary>
        /// Create a message producer for each publication, add it into the registry under the key of the topic
        /// </summary>
        /// <returns></returns>
        public IAmAProducerRegistry Create()
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

            return new ProducerRegistry(producers);
        }
    }
}
