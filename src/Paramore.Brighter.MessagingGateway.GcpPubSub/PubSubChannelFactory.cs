using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The <see cref="PubSubChannelFactory"/> class is responsible for creating and managing Pub/Sub channels.
/// </summary>
/// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
public class PubSubChannelFactory(GcpMessagingGatewayConnection connection)
    : PubSubMessageGateway(connection), IAmAChannelFactory
{
    private readonly PullConsumerFactory _consumerFactory = new(connection);

    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not PullSubscription pullSubscription)
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
        if (subscription is not PullSubscription pullSubscription)
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
}
