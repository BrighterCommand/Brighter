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
    /// <inheritdoc />
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (publication.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var schemaName = publication.SchemaName ?? Connection.Configuration.SchemaName ?? "public";
            var tableName = publication.QueueStoreTable ?? Connection.Configuration.QueueStoreTable;
            var binaryMessagePayload = publication.BinaryMessagePayload ?? Connection.Configuration.BinaryMessagePayload;
            
            EnsureQueueStoreExists(schemaName, tableName, binaryMessagePayload, publication.MakeChannels);
            
            var producer = new PostgresMessageProducer(Connection.Configuration, publication);
            producers[publication.Topic] = producer;
        }

        return producers;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
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
            producers[publication.Topic] = producer;
        }

        return producers;
    }
}
