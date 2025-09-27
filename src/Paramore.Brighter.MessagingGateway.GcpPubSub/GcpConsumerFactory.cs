using System.Collections.Concurrent;
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

        return BrighterAsyncContext.Run(async () => (IAmAMessageConsumerSync) await CreateAsync(pubSubSubscription));
    }

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is not GcpSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        return BrighterAsyncContext.Run(async () => (IAmAMessageConsumerAsync) await CreateAsync(pubSubSubscription));
    }

    private static readonly ConcurrentDictionary<GcpSubscription, GcpStreamConsumer> s_consumers = new(); 
    private async Task<object> CreateAsync(GcpSubscription subscription)
    {
        await EnsureSubscriptionExistsAsync(subscription);

        var subscriptionName = Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(subscription.ProjectId ?? _connection.ProjectId, subscription.ChannelName.Value);
        if(subscription.SubscriptionMode == SubscriptionMode.Pull)
        {
            return new GcpPullMessageConsumer(_connection, subscriptionName,
                subscription.BufferSize, subscription.DeadLetter != null, subscription.TimeProvider);
        }

        var consumer = s_consumers.GetOrAdd(subscription, sub => new GcpStreamConsumer(_connection, subscriptionName, sub));
        consumer.Start();
        return new GcpStreamMessageConsumer(
            _connection,
            consumer,
            subscriptionName,
            subscription.TimeProvider);
    }
}
