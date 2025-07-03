using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Factory class for creating RocketMQ channels in Brighter pipeline.
/// Implements RocketMQ's consumer group pattern for parallel message processing [[4]].
/// </summary>
public class RocketMqChannelFactory(RocketMessageConsumerFactory factory) : IAmAChannelFactory
{
    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not RocketSubscription)
        {
            throw new ConfigurationException("We expect a RocketSubscription or a RocketSubscription<T> as parameter");
        }
        
        return new Channel(
            subscription.ChannelName,
            subscription.RoutingKey,
            factory.Create(subscription),
            subscription.BufferSize);
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        if (subscription is not RocketSubscription)
        {
            throw new ConfigurationException("We expect a RocketSubscription or a RocketSubscription<T> as parameter");
        }
        
        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            factory.CreateAsync(subscription),
            subscription.BufferSize);
    }

    /// <inheritdoc />
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            await factory.CreateConsumerAsync(subscription),
            subscription.BufferSize);
    }
}
