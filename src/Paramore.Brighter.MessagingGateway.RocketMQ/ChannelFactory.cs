using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The channel factory for RocketMQ
/// </summary>
public class ChannelFactory : IAmAChannelFactory
{
    private readonly RocketMessageConsumerFactory _factory;

    /// <summary>
    /// Initialize new instance of the <see cref="ChannelFactory"/>
    /// </summary>
    /// <param name="factory">The consumer factory.</param>
    public ChannelFactory(RocketMessageConsumerFactory factory)
    {
        _factory = factory;
    }

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
            _factory.Create(subscription),
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
            _factory.CreateAsync(subscription),
            subscription.BufferSize);
    }

    /// <inheritdoc />
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            await _factory.CreateConsumerAsync(subscription),
            subscription.BufferSize);
    }
}
