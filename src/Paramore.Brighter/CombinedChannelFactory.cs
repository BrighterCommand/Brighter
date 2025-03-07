using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// The Combined channel factory for multi-bus 
/// </summary>
/// <param name="factories"></param>
public class CombinedChannelFactory(IEnumerable<IAmAChannelFactory> factories) : IAmAChannelFactory
{
    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        foreach (var factory in factories)
        {
            try
            {
                return factory.CreateSyncChannel(subscription);
            }
            catch (ConfigurationException)
            {
            }
        }

        throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        foreach (var factory in factories)
        {
            try
            {
                return factory.CreateAsyncChannel(subscription);
            }
            catch (ConfigurationException)
            {
            }
        }

        throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
    }

    /// <inheritdoc />
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        foreach (var factory in factories)
        {
            try
            {
                return await factory.CreateAsyncChannelAsync(subscription, ct);
            }
            catch (ConfigurationException)
            {
            }
        }

        throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
    }
}
