namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// A factory for creating synchronous and asynchronous message consumers that interact with a PostgreSQL message queue.
/// This factory is responsible for instantiating <see cref="PostgresMessageConsumer"/> instances based on the
/// provided <see cref="Subscription"/> configuration.
/// </summary>
public class PostgresConsumerFactory(PostgresMessagingGatewayConnection connection) : IAmAMessageConsumerFactory
{
    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription)
        => CreateMessageConsumer(subscription);

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription) 
        => CreateMessageConsumer(subscription);

    private PostgresMessageConsumer CreateMessageConsumer(Subscription subscription)
    {
        if (subscription is not PostgresSubscription postgresSubscription)
        {
            throw new ConfigurationException("We expect an PostgresSubscription or PostgresSubscription<T> as a parameter");
        }

        var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
        var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

        return new PostgresMessageConsumer(
            connection.Configuration,
            postgresSubscription,
            deadLetterRoutingKey,
            invalidMessageRoutingKey);
    }
}
