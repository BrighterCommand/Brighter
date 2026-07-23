#region Licence

/* The MIT License (MIT)
Copyright © 2026 Rafael Andrade

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

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Factory class for creating NATS channels from <see cref="NatsSubscription"/> or
/// <see cref="NatsStreamSubscription"/> subscriptions.
/// </summary>
public class NatsChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
{
    private readonly NatsMessageConsumerFactory _consumerFactory;

    /// <summary>
    /// Gets or sets the message scheduler for delayed requeue support.
    /// Setting this property forwards the scheduler to the underlying consumer factory.
    /// </summary>
    /// <value>The <see cref="IAmAMessageScheduler"/>, or <see langword="null"/> when delayed requeue is not configured.</value>
    public IAmAMessageScheduler? Scheduler
    {
        get => _consumerFactory.Scheduler;
        set => _consumerFactory.Scheduler = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsChannelFactory"/> class.
    /// </summary>
    /// <param name="consumerFactory">The <see cref="NatsMessageConsumerFactory"/> used to create the channel's consumer.</param>
    public NatsChannelFactory(NatsMessageConsumerFactory consumerFactory)
    {
        _consumerFactory = consumerFactory;
    }

    /// <summary>
    /// Creates a synchronous NATS channel.
    /// </summary>
    /// <param name="subscription">A <see cref="NatsSubscription"/> or <see cref="NatsStreamSubscription"/>.</param>
    /// <returns>A synchronous NATS channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not a NATS subscription.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not (NatsSubscription or NatsStreamSubscription))
        {
            throw new ConfigurationException("We expect a NatsSubscription or NatsStreamSubscription as a parameter");
        }

        return new Channel(
            subscription.ChannelName,
            subscription.RoutingKey,
            _consumerFactory.Create(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Creates an asynchronous NATS channel.
    /// </summary>
    /// <param name="subscription">A <see cref="NatsSubscription"/> or <see cref="NatsStreamSubscription"/>.</param>
    /// <returns>An asynchronous NATS channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not a NATS subscription.</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        if (subscription is not (NatsSubscription or NatsStreamSubscription))
        {
            throw new ConfigurationException("We expect a NatsSubscription or NatsStreamSubscription as a parameter");
        }

        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            _consumerFactory.CreateAsync(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Asynchronously creates an asynchronous NATS channel.
    /// </summary>
    /// <param name="subscription">A <see cref="NatsSubscription"/> or <see cref="NatsStreamSubscription"/>.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, with an asynchronous NATS channel instance as the result.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not a NATS subscription.</exception>
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        if (subscription is not (NatsSubscription or NatsStreamSubscription))
        {
            throw new ConfigurationException("We expect a NatsSubscription or NatsStreamSubscription as a parameter");
        }

        IAmAChannelAsync channel = new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            _consumerFactory.CreateAsync(subscription),
            subscription.BufferSize);

        return Task.FromResult(channel);
    }
}
