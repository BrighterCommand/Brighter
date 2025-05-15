using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// A factory for creating a <see cref="ProducerRegistry"/> that uses <see cref="PostgresMessageProducer"/> instances
/// to publish messages to a PostgreSQL message queue. This factory takes a connection configuration and a collection
/// of <see cref="PostgresPublication"/> configurations to build the registry.
/// </summary>
public class PostgresProducerRegistryFactory(PostgresMessagingGatewayConnection connection, IEnumerable<PostgresPublication> publications) : IAmAProducerRegistryFactory
{
    /// <inheritdoc />
    public IAmAProducerRegistry Create()
    { 
        var producerFactory = new PostgresMessageProducerFactory(connection, publications);
        return new ProducerRegistry(producerFactory.Create());
    }

    /// <inheritdoc />
    public async Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
    {
        var producerFactory = new PostgresMessageProducerFactory(connection, publications);
        return new ProducerRegistry(await producerFactory.CreateAsync());
    }
}
