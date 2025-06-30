using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pull-based Pub/Sub consumer factory
/// </summary>
public class GcpConsumerFactory : GcpPubSubMessageGateway, IAmAMessageConsumerFactory
{
    private readonly GcpMessagingGatewayConnection _connection;

    /// <summary>
    /// The Pull-based Pub/Sub consumer factory
    /// </summary>
    public GcpConsumerFactory(GcpMessagingGatewayConnection connection) : base(connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        if (subscription is not GcpSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => await CreateAsync(pubSubSubscription));
    }

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is not GcpSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => await CreateAsync(pubSubSubscription));
    }

    private async Task<GcpPullMessageConsumer> CreateAsync(GcpSubscription subscription)
    {
        await EnsureSubscriptionExistsAsync(subscription);

        return new GcpPullMessageConsumer(_connection,
            Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(
                subscription.ProjectId ?? _connection.ProjectId, subscription.ChannelName.Value),
            subscription.BufferSize,
            subscription.DeadLetter != null,
            subscription.TimeProvider);
    }
}
