#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// A message gateway provider for GCP Pub/Sub Stream subscription with message ordering enabled.
/// Configures topics and subscriptions with <c>EnableMessageOrdering = true</c> so that
/// messages published with an ordering key are delivered in order.
/// </summary>
public class GcpStreamOrderingMessageGatewayProvider
    : StreamOrdering.Proactor.IAmAMessageGatewayProactorProvider,
        StreamOrdering.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly GcpPubSubChannelFactory _channelFactory;
    private GcpPubSubSubscription? _lastSubscription;

    public GcpStreamOrderingMessageGatewayProvider()
    {
        _channelFactory = GatewayFactory.CreateChannelFactory();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen-stream-ord-{Guid.NewGuid():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"gen-stream-ord-{Guid.NewGuid():N}");
    }

    public GcpPublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new GcpPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
            EnableMessageOrdering = true,
        };
    }

    public GcpPubSubSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false)
    {
        if (setupDeadLetterQueue)
        {
            var dlqTopic = $"dlq-stream-ord-{Guid.NewGuid():N}"[..45];
            var dlqSub = $"dlq-stream-ord-{Guid.NewGuid():N}"[..45];

            return new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                ackDeadlineSeconds: 60,
                requeueCount: 5,
                deadLetter: new DeadLetterPolicy(new RoutingKey(dlqTopic), new ChannelName(dlqSub))
                {
                    AckDeadlineSeconds = 60,
                    MaxDeliveryAttempts = 5
                },
                makeChannels: makeChannel,
                subscriptionMode: SubscriptionMode.Stream,
                enableMessageOrdering: true
            );
        }

        return new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            subscriptionMode: SubscriptionMode.Stream,
            enableMessageOrdering: true
        );
    }

    public IAmAMessageProducerSync CreateProducer(GcpPublication publication)
    {
        return GatewayFactory.CreateProducer(publication);
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        GcpPublication publication,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return GatewayFactory.CreateProducer(publication);
    }

    public IAmAChannelSync CreateChannel(GcpPubSubSubscription subscription)
    {
        _lastSubscription = subscription;
        return _channelFactory.CreateSyncChannel(subscription);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        _lastSubscription = subscription;
        return await _channelFactory.CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        channel?.Dispose();
        producer?.Dispose();

        if (_lastSubscription != null)
        {
            _channelFactory.DeleteTopic(_lastSubscription);
            _channelFactory.DeleteSubscription(_lastSubscription);
        }
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        channel?.Dispose();

        if (producer != null)
        {
            await producer.DisposeAsync();
        }

        if (_lastSubscription != null)
        {
            await _channelFactory.DeleteTopicAsync(_lastSubscription);
            await _channelFactory.DeleteSubscriptionAsync(_lastSubscription);
        }
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var dlqSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscription.DeadLetter!.Subscription!.Value),
            channelName: subscription.DeadLetter.Subscription,
            routingKey: subscription.DeadLetter.TopicName,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var dlqChannel = await _channelFactory.CreateAsyncChannelAsync(dlqSubscription, cancellationToken);
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var message = await dlqChannel.ReceiveAsync(TimeSpan.FromSeconds(5), cancellationToken);
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await dlqChannel.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }

    public Message GetMessageFromDeadLetterQueue(GcpPubSubSubscription subscription)
    {
        var dlqSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscription.DeadLetter!.Subscription!.Value),
            channelName: subscription.DeadLetter.Subscription,
            routingKey: subscription.DeadLetter.TopicName,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var dlqChannel = _channelFactory.CreateSyncChannel(dlqSubscription);
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var message = dlqChannel.Receive(TimeSpan.FromSeconds(5));
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    dlqChannel.Acknowledge(message);
                    return message;
                }

                Thread.Sleep(1000);
            }

            return new Message();
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }
}
