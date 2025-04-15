using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pull-based Pub/Sub consumer factory
/// </summary>
public class PullConsumerFactory : PubSubMessageGateway, IAmAMessageConsumerFactory
{
    private readonly GcpMessagingGatewayConnection _connection;

    /// <summary>
    /// The Pull-based Pub/Sub consumer factory
    /// </summary>
    public PullConsumerFactory(GcpMessagingGatewayConnection connection) : base(connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        if (subscription is not PullSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => await CreateAsync(pubSubSubscription));
    }

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is not PullSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => await CreateAsync(pubSubSubscription));
    }

    private async Task<SubscriptionConsumer> CreateAsync(PullSubscription subscription)
    {
        await EnsureSubscriptionExistsAsync(subscription);

        return new SubscriptionConsumer(_connection,
            Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(
                subscription.ProjectId ?? _connection.ProjectId, subscription.ChannelName.Value),
            subscription.BufferSize,
            subscription.DeadLetterTopic != null,
            subscription.TimeProvider);
    }
}
