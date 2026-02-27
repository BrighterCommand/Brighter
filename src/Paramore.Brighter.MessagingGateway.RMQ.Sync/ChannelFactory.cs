#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync;

/// <summary>
/// Factory class for creating RabbitMQ channels.
/// </summary>
public class ChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
{
    private readonly RmqMessageConsumerFactory _messageConsumerFactory;

    /// <summary>
    /// Gets or sets the message scheduler for delayed requeue support.
    /// Setting this property forwards the scheduler to the underlying consumer factory.
    /// </summary>
    public IAmAMessageScheduler? Scheduler
    {
        get => _messageConsumerFactory.Scheduler;
        set => _messageConsumerFactory.Scheduler = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
    /// </summary>
    /// <param name="messageConsumerFactory">The factory for creating RabbitMQ message consumers.</param>
    public ChannelFactory(RmqMessageConsumerFactory messageConsumerFactory)
    {
        _messageConsumerFactory = messageConsumerFactory;
    }

    /// <summary>
    /// Creates a synchronous RabbitMQ channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <returns>A synchronous RabbitMQ channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an RmqSubscription.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        RmqSubscription? rmqSubscription = subscription as RmqSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("We expect an RmqSubscription or RmqSubscription<T> as a parameter");

        var messageConsumer = _messageConsumerFactory.Create(rmqSubscription);

        return new Channel(
            channelName: subscription.ChannelName,
            routingKey: subscription.RoutingKey,
            messageConsumer: messageConsumer,
            maxQueueLength: subscription.BufferSize
        );
    }

    /// <summary>
    /// Creates an asynchronous RabbitMQ channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <returns>An asynchronous RabbitMQ channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an RmqSubscription.</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        throw new ConfigurationException("Use Paramore.Brighter.MessagingGateway.RMQ.Proactor.ChannelFactory.CreateAsyncChannel instead");
    }

    /// <summary>
    /// Asynchronously creates an asynchronous RabbitMQ channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, with an asynchronous RabbitMQ channel instance as the result.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an RmqSubscription.</exception>
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        RmqSubscription? rmqSubscription = subscription as RmqSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("We expect an RmqSubscription or RmqSubscription<T> as a parameter");

        var messageConsumer = _messageConsumerFactory.CreateAsync(rmqSubscription);

        var channel = new ChannelAsync(
            channelName: subscription.ChannelName,
            routingKey: subscription.RoutingKey,
            messageConsumer: messageConsumer,
            maxQueueLength: subscription.BufferSize
        );

        return Task.FromResult<IAmAChannelAsync>(channel);
    }
}
