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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public class SnsMessageProducerFactory : IAmAMessageProducerFactory
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly IEnumerable<SnsPublication> _publications;

    /// <summary>
    /// Creates a collection of SNS message producers from the SNS publication information
    /// </summary>
    /// <param name="connection">The Connection to use to connect to AWS</param>
    /// <param name="publications">The publications describing the SNS topics that we want to use</param>
    public SnsMessageProducerFactory(
        AWSMessagingGatewayConnection connection,
        IEnumerable<SnsPublication> publications)
    {
        _connection = connection;
        _publications = publications;
    }

    /// <summary>
    /// Creates a dictionary of message producers.
    /// </summary>
    /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
    /// <exception cref="ArgumentException">Thrown when a publication does not have a topic</exception>
    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in _publications)
        {
            if (publication.Topic is null)
                throw new ConfigurationException("Missing topic on Publication");

            var producer = new SnsMessageProducer(_connection, publication);
            producer.Publication = publication;
            
            if (producer.ConfirmTopicExists())
            {
                var producerKey = new ProducerKey(publication.Topic, publication.Type);
                if (producers.ContainsKey(producerKey))
                    throw new ArgumentException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
                producers[producerKey] = producer;
            }
            else
            {
                throw new ConfigurationException($"Missing SNS topic: {publication.Topic}");
            }
        }

        return producers;
    }

    /// <summary>
    /// Creates a dictionary of in-memory message producers.
    /// </summary>
    /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
    /// <exception cref="ArgumentException">Thrown when a publication does not have a topic</exception>
    public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in _publications)
        {
            if (publication.Topic is null)
                throw new ConfigurationException("Missing topic on Publication");

            var producer = new SnsMessageProducer(_connection, publication);
            producer.Publication = publication;
            
            if (await producer.ConfirmTopicExistsAsync())
            {
                var producerKey = new ProducerKey(publication.Topic, publication.Type);
                if (producers.ContainsKey(producerKey))
                    throw new ArgumentException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
                producers[producerKey] = producer;
            }
            else
            {
                throw new ConfigurationException($"Missing SNS topic: {publication.Topic}");
            }
        }

        return producers;
    }
}
