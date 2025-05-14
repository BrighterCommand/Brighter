using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// A channel factory for creating synchronous and asynchronous channels that interact with a PostgreSQL message queue.
/// This factory is responsible for ensuring the underlying queue store exists and for creating channels
/// configured according to the provided <see cref="PostgresSubscription"/>.
/// </summary>
public class PostgresChannelFactory(PostgresMessagingGatewayConnection connection): PostgresMessagingGateway(connection), IAmAChannelFactory
{
    private readonly PostgresConsumerFactory _factory = new(connection);
    
    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not PostgresSubscription postgresSubscription)
        {
            throw new ConfigurationException("We expect an PostgresSubscription or PostgresSubscription <T> as a parameter");
        }

        var schema = postgresSubscription.SchemaName ?? Connection.Configuration.SchemaName ?? "public";
        var tableName = postgresSubscription.QueueStoreTable ?? Connection.Configuration.QueueStoreTable;
        var binaryMessagePayload = postgresSubscription.BinaryMessagePayload ?? Connection.Configuration.BinaryMessagePayload;

        EnsureQueueStoreExists(schema, tableName, binaryMessagePayload, postgresSubscription.MakeChannels);
        return new Channel(
            postgresSubscription.ChannelName,
            postgresSubscription.RoutingKey,
            _factory.Create(subscription),
            postgresSubscription.BufferSize
        );
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        if (subscription is not PostgresSubscription postgresSubscription)
        {
            throw new ConfigurationException("We expect an PostgresSubscription or PostgresSubscription <T> as a parameter");
        }

        var schema = postgresSubscription.SchemaName ?? Connection.Configuration.SchemaName ?? "public";
        var tableName = postgresSubscription.QueueStoreTable ?? Connection.Configuration.QueueStoreTable;
        var binaryMessagePayload = postgresSubscription.BinaryMessagePayload ?? Connection.Configuration.BinaryMessagePayload;

        EnsureQueueStoreExists(schema, tableName, binaryMessagePayload, postgresSubscription.MakeChannels);
        return new ChannelAsync(
            postgresSubscription.ChannelName,
            postgresSubscription.RoutingKey,
            _factory.CreateAsync(subscription),
            postgresSubscription.BufferSize
        );
    }

    /// <inheritdoc />
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        if (subscription is not PostgresSubscription postgresSubscription)
        {
            throw new ConfigurationException("We expect an PostgresSubscription or PostgresSubscription <T> as a parameter");
        }

        var schema = postgresSubscription.SchemaName ?? Connection.Configuration.SchemaName ?? "public";
        var tableName = postgresSubscription.QueueStoreTable ?? Connection.Configuration.QueueStoreTable;
        var binaryMessagePayload = postgresSubscription.BinaryMessagePayload ?? Connection.Configuration.BinaryMessagePayload;

        await EnsureQueueStoreExistsAsync(schema, tableName, binaryMessagePayload, postgresSubscription.MakeChannels, ct);
        return new ChannelAsync(
            postgresSubscription.ChannelName,
            postgresSubscription.RoutingKey,
            _factory.CreateAsync(subscription),
            postgresSubscription.BufferSize
        );
    }
}
