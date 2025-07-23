using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarChannelFactory(PulsarMessageConsumerFactory factory) : IAmAChannelFactory
{
    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        return new Channel(
            subscription.ChannelName,
            subscription.RoutingKey,
            factory.Create(subscription),
            subscription.BufferSize);
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            factory.CreateAsync(subscription),
            subscription.BufferSize);
    }

    /// <inheritdoc />
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        return Task.FromResult(CreateAsyncChannel(subscription));
    }
}
