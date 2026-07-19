// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Creates Brighter message consumers for NATS subscriptions.
/// </summary>
/// <remarks>
/// A <see cref="NatsSubscription"/> yields a <see cref="NatsMessageConsumer"/> over a core NATS subject;
/// a <see cref="NatsStreamSubscription"/> yields a <see cref="NatsStreamMessageConsumer"/> over a JetStream
/// durable consumer. For stream subscriptions the JetStream stream and consumer are created, validated, or
/// assumed to exist according to <see cref="Subscription.MakeChannels"/>: the channel name is used as the
/// stream name and the routing key as the stream subject unless
/// <see cref="NatsStreamSubscription.StreamConfiguration"/> provides an explicit stream configuration.
/// </remarks>
/// <param name="natsClient">The <see cref="INatsClient"/> used for core NATS subscriptions.</param>
/// <param name="natsJsContext">The <see cref="INatsJSContext"/> used for JetStream subscriptions.</param>
public partial class NatsMessageConsumerFactory(INatsClient natsClient, INatsJSContext natsJsContext) : IAmAMessageConsumerFactory
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<NatsMessageConsumerFactory>();

    /// <summary>
    /// Gets or sets the message scheduler used for delayed requeue on core NATS subscriptions.
    /// Can be set after construction to allow channel factories to forward the scheduler from DI.
    /// JetStream subscriptions use native NAK delays and do not need a scheduler.
    /// </summary>
    /// <value>The <see cref="IAmAMessageScheduler"/>, or <see langword="null"/> when delayed requeue is not configured.</value>
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Creates a synchronous consumer for the given subscription.
    /// </summary>
    /// <param name="subscription">A <see cref="NatsSubscription"/> or <see cref="NatsStreamSubscription"/>.</param>
    /// <returns>An <see cref="IAmAMessageConsumerSync"/> for the subscription.</returns>
    /// <exception cref="ConfigurationException">Thrown when <paramref name="subscription"/> is not a NATS subscription.</exception>
    /// <exception cref="ChannelFailureException">Thrown when the channel is validated or created but the JetStream stream or consumer cannot be found.</exception>
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        if (subscription is NatsSubscription natsSubscription)
        {
            return Create(natsSubscription);
        }

        if (subscription is NatsStreamSubscription natsStreamSubscription)
        {
            return BrighterAsyncContext.Run(async () => await CreateAsync(natsStreamSubscription));
        }

        throw new ConfigurationException("We expect a NatsSubscription or NatsStreamSubscription as a parameter");
    }

    /// <summary>
    /// Creates an asynchronous consumer for the given subscription.
    /// </summary>
    /// <param name="subscription">A <see cref="NatsSubscription"/> or <see cref="NatsStreamSubscription"/>.</param>
    /// <returns>An <see cref="IAmAMessageConsumerAsync"/> for the subscription.</returns>
    /// <exception cref="ConfigurationException">Thrown when <paramref name="subscription"/> is not a NATS subscription.</exception>
    /// <exception cref="ChannelFailureException">Thrown when the channel is validated or created but the JetStream stream or consumer cannot be found.</exception>
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is NatsSubscription natsSubscription)
        {
            return Create(natsSubscription);
        }

        if (subscription is NatsStreamSubscription natsStreamSubscription)
        {
            return BrighterAsyncContext.Run(async () => await CreateAsync(natsStreamSubscription));
        }

        throw new ConfigurationException("We expect a NatsSubscription or NatsStreamSubscription as a parameter");
    }

    private NatsMessageConsumer Create(NatsSubscription subscription)
    {
        Log.CreatingCoreConsumer(s_logger, subscription.ChannelName.Value, subscription.QueueGroup ?? "none");
        var sub = BrighterAsyncContext.Run(async () => await natsClient.Connection.SubscribeCoreAsync<byte[]>(
            subscription.ChannelName.Value,
            subscription.QueueGroup,
            opts: subscription.NatsSubOpts));

        return new NatsMessageConsumer(sub, natsClient.Connection, Scheduler);
    }

    private async Task<NatsStreamMessageConsumer> CreateAsync(NatsStreamSubscription subscription)
    {
        await EnsureExistsAsync(subscription);
        Log.CreatingStreamConsumer(s_logger, subscription.ChannelName.Value, subscription.Consumer);
        var stream = await natsJsContext.GetStreamAsync(subscription.ChannelName.Value);
        var consumer = await stream.GetConsumerAsync(subscription.Consumer);
        var stopTokenSource = new CancellationTokenSource();
        var buffer = consumer.ConsumeAsync<byte[]>(opts: new NatsJSConsumeOpts
        {
            MaxMsgs = subscription.BufferSize,
            IdleHeartbeat = subscription.IdleHeartbeat,
            PriorityGroup = subscription.PriorityGroup,
        }, cancellationToken: stopTokenSource.Token);

        return new NatsStreamMessageConsumer(stream, buffer, stopTokenSource);
    }

    private async Task EnsureExistsAsync(NatsStreamSubscription subscription)
    {
        if (subscription.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        if (subscription.MakeChannels == OnMissingChannel.Validate)
        {
            try
            {
                var s = await natsJsContext.GetStreamAsync(subscription.ChannelName.Value);
                _ = await s.GetConsumerAsync(subscription.Consumer);
            }
            catch (NatsJSApiException e) when (e.Error.Code == 404)
            {
                Log.StreamOrConsumerMissing(s_logger, subscription.ChannelName.Value, subscription.Consumer);
                throw new ChannelFailureException(
                    $"Stream {subscription.ChannelName.Value} or consumer {subscription.Consumer} does not exist", e);
            }

            return;
        }

        var config = subscription.StreamConfiguration ?? new StreamConfig
        {
            Name = subscription.ChannelName.Value,
            Subjects = [subscription.RoutingKey.Value]
        };
        Log.CreatingOrUpdatingStream(s_logger, config.Name ?? subscription.ChannelName.Value);
        var stream = await natsJsContext.CreateOrUpdateStreamAsync(config);

        if (subscription.Ordered)
        {
            await stream.CreateOrderedConsumerAsync(subscription.OrderedConsumerOption ?? new NatsJSOrderedConsumerOpts());
        }
        else
        {
            await stream.CreateOrUpdateConsumerAsync(subscription.ConsumerOption);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Creating core NATS consumer for subject {Subject} with queue group {QueueGroup}")]
        public static partial void CreatingCoreConsumer(ILogger logger, string subject, string queueGroup);

        [LoggerMessage(LogLevel.Debug, "Creating JetStream consumer for stream {StreamName} and consumer {ConsumerName}")]
        public static partial void CreatingStreamConsumer(ILogger logger, string streamName, string consumerName);

        [LoggerMessage(LogLevel.Warning, "JetStream stream {StreamName} or consumer {ConsumerName} does not exist")]
        public static partial void StreamOrConsumerMissing(ILogger logger, string streamName, string consumerName);

        [LoggerMessage(LogLevel.Debug, "Creating or updating JetStream stream {StreamName}")]
        public static partial void CreatingOrUpdatingStream(ILogger logger, string streamName);
    }
}
