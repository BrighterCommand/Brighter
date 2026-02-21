using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// The Combined channel factory for multi-bus 
/// </summary>
/// <param name="factories"></param>
public class CombinedChannelFactory(IEnumerable<IAmAChannelFactory> factories) : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
{
    /// <summary>
    /// Gets or sets the message scheduler, propagating it to all inner factories
    /// that implement <see cref="IAmAChannelFactoryWithScheduler"/>.
    /// </summary>
    public IAmAMessageScheduler? Scheduler
    {
        get => factories.OfType<IAmAChannelFactoryWithScheduler>().FirstOrDefault()?.Scheduler;
        set
        {
            foreach (var factory in factories.OfType<IAmAChannelFactoryWithScheduler>())
            {
                factory.Scheduler = value;
            }
        }
    }

    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        var factory = factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
        if (factory == null)
        {
            throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
        }

        return factory.CreateSyncChannel(subscription);
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        var factory = factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
        if (factory == null)
        {
            throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
        }

        return factory.CreateAsyncChannel(subscription);
    }

    /// <inheritdoc />
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken ct = default)
    {
        var factory = factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
        if (factory == null)
        {
            throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
        }

        return factory.CreateAsyncChannelAsync(subscription, ct);
    }
}
