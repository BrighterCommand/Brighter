using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWS.V4;

/// <summary>
/// The <see cref="SqsMessageProducer"/> factory
/// </summary>
public class SqsMessageProducerFactory : IAmAMessageProducerFactory
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly IEnumerable<SqsPublication> _publications;

    /// <summary>
    /// Initialize new instance of <see cref="SqsMessageProducerFactory"/>.
    /// </summary>
    /// <param name="connection">The <see cref="AWSMessagingGatewayConnection"/>.</param>
    /// <param name="publications">The collection of <see cref="SqsPublication"/>.</param>
    public SqsMessageProducerFactory(AWSMessagingGatewayConnection connection,
        IEnumerable<SqsPublication> publications)
    {
        _connection = connection;
        _publications = publications;
    }

    /// <summary>
    /// Creates a dictionary of in-memory message producers.
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

            var producer = new SqsMessageProducer(_connection, publication);
            if (producer.ConfirmQueueExists())
            {
                var producerKey = new ProducerKey(publication.Topic, publication.Type);
                if (producers.ContainsKey(producerKey))
                    throw new ArgumentException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
                producers[producerKey] = producer;

            }
            else
            {
                throw new ConfigurationException($"Missing SQS queue: {publication.Topic}");
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

            var producer = new SqsMessageProducer(_connection, publication);
            if (await producer.ConfirmQueueExistsAsync())
            {
                var producerKey = new ProducerKey(publication.Topic, publication.Type);
                if (producers.ContainsKey(producerKey))
                    throw new ArgumentException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
                producers[producerKey] = producer;

            }
            else
            {
                throw new ConfigurationException($"Missing SQS queue: {publication.Topic}");
            }
        }

        return producers;
    }
}
