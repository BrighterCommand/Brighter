#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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

namespace Paramore.Brighter.MessagingGateway.Kafka;

/// <summary>
/// Factory class for creating Kafka channels.
/// </summary>
public class ChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
{
    private readonly KafkaMessageConsumerFactory _kafkaMessageConsumerFactory;

    /// <summary>
    /// Gets or sets the message scheduler for delayed requeue support.
    /// Setting this property forwards the scheduler to the underlying consumer factory.
    /// </summary>
    public IAmAMessageScheduler? Scheduler
    {
        get => _kafkaMessageConsumerFactory.Scheduler;
        set => _kafkaMessageConsumerFactory.Scheduler = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
    /// </summary>
    /// <param name="kafkaMessageConsumerFactory">The factory for creating Kafka message consumers.</param>
    public ChannelFactory(KafkaMessageConsumerFactory kafkaMessageConsumerFactory)
    {
        _kafkaMessageConsumerFactory = kafkaMessageConsumerFactory;
    }

    /// <summary>
    /// Creates a synchronous Kafka channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <returns>A synchronous Kafka channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not a KafkaSubscription.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        KafkaSubscription? rmqSubscription = subscription as KafkaSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("We expect a KafkaSubscription or KafkaSubscription<T> as a parameter");

        return new Channel(
            subscription.ChannelName,
            subscription.RoutingKey,
            _kafkaMessageConsumerFactory.Create(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Creates an asynchronous Kafka channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <returns>An asynchronous Kafka channel instance.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not a KafkaSubscription.</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        KafkaSubscription? rmqSubscription = subscription as KafkaSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("We expect a KafkaSubscription or KafkaSubscription<T> as a parameter");

        return new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            _kafkaMessageConsumerFactory.CreateAsync(subscription),
            subscription.BufferSize);
    }

    /// <summary>
    /// Asynchronously creates an asynchronous Kafka channel.
    /// </summary>
    /// <param name="subscription">The subscription details for the channel.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, with an asynchronous Kafka channel instance as the result.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not a KafkaSubscription.</exception>
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        KafkaSubscription? rmqSubscription = subscription as KafkaSubscription;
        if (rmqSubscription == null)
            throw new ConfigurationException("We expect a KafkaSubscription or KafkaSubscription<T> as a parameter");

        IAmAChannelAsync channel = new ChannelAsync(
            subscription.ChannelName,
            subscription.RoutingKey,
            _kafkaMessageConsumerFactory.CreateAsync(subscription),
            subscription.BufferSize);

        return Task.FromResult(channel);
    }
}
