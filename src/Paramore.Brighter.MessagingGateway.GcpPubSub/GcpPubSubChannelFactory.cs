using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The <see cref="GcpPubSubChannelFactory"/> class is responsible for creating and managing Pub/Sub channels.
/// </summary>
/// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
public class GcpPubSubChannelFactory(GcpMessagingGatewayConnection connection)
    : GcpPubSubMessageGateway(connection), IAmAChannelFactory
{
    private readonly GcpConsumerFactory _consumerFactory = new(connection);

    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not GcpSubscription pullSubscription)
        {
            throw new ConfigurationException("We expect a PullPubSubSubscription as a parameter");
        }

        return BrighterAsyncContext.Run(async () =>
        {
            await EnsureSubscriptionExistsAsync(pullSubscription);

            return new Channel(
                new ChannelName(pullSubscription.ChannelName.Value),
                pullSubscription.RoutingKey,
                _consumerFactory.Create(pullSubscription),
                pullSubscription.BufferSize);
        });
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        => BrighterAsyncContext.Run(async () => await CreateAsyncChannelAsync(subscription));

    /// <inheritdoc />
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken ct = default)
    {
        if (subscription is not GcpSubscription pullSubscription)
        {
            throw new ConfigurationException("We expect a PullPubSubSubscription as a parameter");
        }

        await EnsureSubscriptionExistsAsync(pullSubscription);

        return new ChannelAsync(
            new ChannelName(pullSubscription.ChannelName.Value),
            pullSubscription.RoutingKey,
            _consumerFactory.CreateAsync(pullSubscription),
            pullSubscription.BufferSize
        );
    }

    public async Task DeleteTopicAsync(GcpSubscription subscription, CancellationToken cancellation = default)
    {
        var publisher = await Connection.CreatePublisherServiceApiClientAsync();
        await publisher.DeleteTopicAsync(new DeleteTopicRequest
        {
            TopicAsTopicName = GetTopicName(subscription.ProjectId, subscription.RoutingKey.Value)
        }, CallSettings.FromCancellationToken(cancellation));
    }
    
    public void DeleteTopic(GcpSubscription subscription)
    {
        var publisher = Connection.CreatePublisherServiceApiClient();
        publisher.DeleteTopic(new DeleteTopicRequest
        {
            TopicAsTopicName = GetTopicName(subscription.ProjectId, subscription.RoutingKey.Value)
        });
    }
    
    public async Task DeleteSubscriptionAsync(GcpSubscription subscription, CancellationToken cancellation = default)
    {
        var publisher = await Connection.CreateSubscriberServiceApiClientAsync();
        await publisher.DeleteSubscriptionAsync(new DeleteSubscriptionRequest() 
        {
            SubscriptionAsSubscriptionName = GetSubscriptionName(subscription.ProjectId, subscription.ChannelName.Value)
        }, CallSettings.FromCancellationToken(cancellation));
    }
    
    public void DeleteSubscription(GcpSubscription subscription)
    {
        var publisher = Connection.CreateSubscriberServiceApiClient();
        publisher.DeleteSubscription(new DeleteSubscriptionRequest
        {
            SubscriptionAsSubscriptionName = GetSubscriptionName(subscription.ProjectId, subscription.ChannelName.Value)
        });
    }
}
