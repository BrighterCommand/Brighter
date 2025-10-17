using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Factory for creating Pulsar message channels (synchronous and asynchronous) using a Pulsar consumer factory.
/// </summary>
/// <remarks>
/// This factory creates channels that integrate with Apache Pulsar messaging systems.
/// Channels are configured using subscription details including channel names, routing keys, and buffer sizes.
/// </remarks>
/// <param name="factory">The factory responsible for creating Pulsar message consumers</param>
public class PulsarChannelFactory(PulsarMessageConsumerFactory factory) : IAmAChannelFactory
{
    /// <summary>
    /// Creates a synchronous Pulsar message channel
    /// </summary>
    /// <param name="subscription">Subscription configuration containing channel parameters</param>
    /// <returns>Synchronous channel instance bound to Pulsar</returns>
    /// <remarks>
    /// Uses the following subscription properties:
    /// <list type="bullet">
    ///   <item><description>ChannelName: Logical name for the channel</description></item>
    ///   <item><description>RoutingKey: Pulsar topic routing information</description></item>
    ///   <item><description>BufferSize: Internal message buffer capacity</description></item>
    /// </list>
    /// </remarks>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        return new Channel(
            subscription.ChannelName,
            subscription.RoutingKey,
            factory.Create(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Creates an asynchronous Pulsar message channel
    /// </summary>
    /// <param name="subscription">Subscription configuration containing channel parameters</param>
    /// <returns>Asynchronous channel instance bound to Pulsar</returns>
    /// <remarks>
    /// Uses the same subscription configuration parameters as <see cref="CreateSyncChannel"/>.
    /// </remarks>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            factory.CreateAsync(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Creates an asynchronous Pulsar channel wrapped in a completed Task
    /// </summary>
    /// <param name="subscription">Subscription configuration containing channel parameters</param>
    /// <param name="ct">Cancellation token (not used in this implementation)</param>
    /// <returns>Completed task containing an asynchronous channel instance</returns>
    /// <remarks>
    /// This implementation is synchronous and does not perform asynchronous operations.
    /// The cancellation token parameter is provided for interface compliance but is not utilized.
    /// </remarks>
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        return Task.FromResult(CreateAsyncChannel(subscription));
    }
}
