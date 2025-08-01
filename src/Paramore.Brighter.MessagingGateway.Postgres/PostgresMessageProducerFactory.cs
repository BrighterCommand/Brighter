using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// A factory for creating a dictionary of message producers for interacting with a PostgreSQL message queue.
/// This factory iterates through a collection of <see cref="PostgresPublication"/> configurations,
/// ensures the underlying queue store exists for each publication, and creates a corresponding
/// <see cref="PostgresMessageProducer"/> instance, keyed by the publication's topic.
/// </summary>
public class PostgresMessageProducerFactory(PostgresMessagingGatewayConnection connection, IEnumerable<PostgresPublication> publications) :  PostgresMessagingGateway(connection), IAmAMessageProducerFactory
{
    /// <summary>
    /// Creates a dictionary of in-memory message producers.
    /// </summary>
    /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
    /// <exception cref="ConfigurationException">Thrown when a publication does not have a topic</exception>
    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (publication.Topic is null)
                throw new ConfigurationException("Missing topic on Publication");

            var schemaName = publication.SchemaName ?? Connection.Configuration.SchemaName ?? "public";
            var tableName = publication.QueueStoreTable ?? Connection.Configuration.QueueStoreTable;
            var binaryMessagePayload = publication.BinaryMessagePayload ?? Connection.Configuration.BinaryMessagePayload;
            
            EnsureQueueStoreExists(schemaName, tableName, binaryMessagePayload, publication.MakeChannels);
            
            var producer = new PostgresMessageProducer(Connection.Configuration, publication);
            
            producer.Publication = publication;
            var producerKey = new ProducerKey(publication.Topic, publication.Type);
            if (producers.ContainsKey(producerKey))
                throw new ConfigurationException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
            producers[producerKey] = producer;
        }

        return producers;
    }

    /// <summary>
    /// Creates a dictionary of in-memory message producers.
    /// </summary>
    /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
    /// <exception cref="ConfigurationException">Thrown when a publication does not have a topic</exception>
    public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (publication.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var schemaName = publication.SchemaName ?? Connection.Configuration.SchemaName ?? "public";
            var tableName = publication.QueueStoreTable ?? Connection.Configuration.QueueStoreTable;
            var binaryMessagePayload = publication.BinaryMessagePayload ?? Connection.Configuration.BinaryMessagePayload;
            
            await EnsureQueueStoreExistsAsync(schemaName, tableName, binaryMessagePayload, publication.MakeChannels, CancellationToken.None);
            
            var producer = new PostgresMessageProducer(Connection.Configuration, publication);
            
            producer.Publication = publication;
            var producerKey = new ProducerKey(publication.Topic, publication.Type);
            if (producers.ContainsKey(producerKey))
                throw new ConfigurationException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
            producers[producerKey] = producer;

        }

        return producers;
    }
}
