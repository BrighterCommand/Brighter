using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql;

/// <summary>
/// Factory class for creating MS SQL channels.
/// </summary>
public partial class ChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ChannelFactory>();
    private readonly MsSqlMessageConsumerFactory _msSqlMessageConsumerFactory;

    /// <summary>
    /// Gets or sets the message scheduler for delayed requeue support.
    /// Setting this property forwards the scheduler to the underlying consumer factory.
    /// </summary>
    public IAmAMessageScheduler? Scheduler
    {
        get => _msSqlMessageConsumerFactory.Scheduler;
        set => _msSqlMessageConsumerFactory.Scheduler = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
    /// </summary>
    /// <param name="msSqlMessageConsumerFactory">The factory for creating MS SQL message consumers.</param>
    /// <exception cref="ArgumentNullException">Thrown when the msSqlMessageConsumerFactory is null.</exception>
    public ChannelFactory(MsSqlMessageConsumerFactory msSqlMessageConsumerFactory)
    {
        _msSqlMessageConsumerFactory = msSqlMessageConsumerFactory ??
                                       throw new ArgumentNullException(nameof(msSqlMessageConsumerFactory));
    }

    /// <summary>
    /// Creates a synchronous MS SQL channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <returns>A synchronous MS SQL channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an MsSqlSubscription.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        MsSqlSubscription? rmqSubscription = subscription as MsSqlSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("MS SQL ChannelFactory We expect an MsSqlSubscription or MsSqlSubscription<T> as a parameter");

        Log.MsSqlInputChannelFactoryCreateInputChannel(s_logger, subscription.ChannelName, subscription.RoutingKey);
        return new Channel(
            subscription.ChannelName,
            subscription.RoutingKey,
            _msSqlMessageConsumerFactory.Create(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Creates an asynchronous MS SQL channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <returns>An asynchronous MS SQL channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an MsSqlSubscription.</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        MsSqlSubscription? rmqSubscription = subscription as MsSqlSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("MS SQL ChannelFactory We expect an MsSqlSubscription or MsSqlSubscription<T> as a parameter");

        Log.MsSqlInputChannelFactoryCreateInputChannel(s_logger, subscription.ChannelName, subscription.RoutingKey);
        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            _msSqlMessageConsumerFactory.CreateAsync(subscription),
            subscription.BufferSize);

    }

    /// <summary>
    /// Asynchronously creates an asynchronous MS SQL channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, with an asynchronous MS SQL channel instance as the result.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an MsSqlSubscription.</exception>
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        MsSqlSubscription? rmqSubscription = subscription as MsSqlSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("MS SQL ChannelFactory We expect an MsSqlSubscription or MsSqlSubscription<T> as a parameter");

        Log.MsSqlInputChannelFactoryCreateInputChannel(s_logger, subscription.ChannelName, subscription.RoutingKey);
        var channel = new ChannelAsync(
            subscription.ChannelName, 
            subscription.RoutingKey,
            _msSqlMessageConsumerFactory.CreateAsync(subscription),
            subscription.BufferSize);

        return await Task.FromResult(channel);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "MsSqlInputChannelFactory: create input channel {ChannelName} for topic {Topic}")]
        public static partial void MsSqlInputChannelFactoryCreateInputChannel(ILogger logger, string? channelName, string topic);
    }
}

