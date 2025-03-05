using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pull-based Pub/Sub consumer factory
/// </summary>
public class PullPubSubConsumerFactory : PubSubMessageGateway, IAmAMessageConsumerFactory
{
    private readonly GcpMessagingGatewayConnection _connection;

    /// <summary>
    /// The Pull-based Pub/Sub consumer factory
    /// </summary>
    public PullPubSubConsumerFactory(GcpMessagingGatewayConnection connection) : base(connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        if (subscription is not PubSubSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expection a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => await CreateAsync(pubSubSubscription));
    }

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is not PubSubSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expection a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => await CreateAsync(pubSubSubscription));
    }

    private async Task<PullPubSubConsumer> CreateAsync(PubSubSubscription subscription)
    {
        await EnsureSubscriptionExistsAsync(subscription);
        new SubscriberServiceApiClientBuilder()
            .Build();
        var client = await SubscriberServiceApiClient.CreateAsync();

        return new PullPubSubConsumer(client,
            Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(
                subscription.ProjectId ?? _connection.ProjectId, subscription.ChannelName.Value),
            subscription.BufferSize,
            subscription.DeadLetterTopic != null,
            subscription.TimeProvider);
    }
}
