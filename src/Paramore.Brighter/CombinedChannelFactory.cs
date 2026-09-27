using System;
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
    private readonly IReadOnlyList<IAmAChannelFactory> _factories = factories.ToList();
    private IReadOnlyList<Type>? _factoryTypes;

    /// <summary>
    /// Gets the <see cref="Type"/> of each inner channel factory, in constructor order. This
    /// composite can serve a subscription exactly when that subscription's <c>ChannelFactoryType</c>
    /// is one of them.
    /// </summary>
    /// <remarks>
    /// Not thread-safe: concurrent first reads may each build a list, so every read yields an
    /// <em>equal</em> list, not necessarily the same instance. The only caller is the
    /// single-threaded startup validation path.
    /// </remarks>
    public IReadOnlyList<Type> FactoryTypes => _factoryTypes ??= _factories.Select(f => f.GetType()).ToList();

    /// <summary>
    /// Gets or sets the message scheduler, propagating it to all inner factories
    /// that implement <see cref="IAmAChannelFactoryWithScheduler"/>.
    /// </summary>
    public IAmAMessageScheduler? Scheduler
    {
        get => _factories.OfType<IAmAChannelFactoryWithScheduler>().FirstOrDefault()?.Scheduler;
        set
        {
            foreach (var factory in _factories.OfType<IAmAChannelFactoryWithScheduler>())
            {
                factory.Scheduler = value;
            }
        }
    }

    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
        if (factory == null)
        {
            throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
        }

        return factory.CreateSyncChannel(subscription);
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
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
        var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
        if (factory == null)
        {
            throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
        }

        return factory.CreateAsyncChannelAsync(subscription, ct);
    }
}
