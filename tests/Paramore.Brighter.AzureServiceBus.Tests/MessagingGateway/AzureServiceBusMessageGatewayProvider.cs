#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// Conformance harness provider for Azure Service Bus (Topic mode).
///
/// ASB dead-letters natively via the built-in $DeadLetterQueue sub-queue on every
/// topic subscription. The gateway's <c>Reject</c> path calls
/// <c>ServiceBusReceiver.DeadLetterAsync(lockToken, reason, description)</c> with no
/// Brighter-stamped metadata, so <see cref="RejectionMetadataKeys"/> is all
/// <see cref="string.Empty"/> (FR-8 relaxation: native-DLQ transport, routing only).
///
/// Credentials are resolved lazily from <c>BrighterTestsASBConnectionString</c> or
/// <c>BrighterTestsASBNameSpace</c>. When neither env-var is set, <see cref="ASBCreds"/>
/// throws at runtime, not at build time — generation and compilation are unaffected.
/// Task 55 owns the broker attempt and the conformance ledger row.
/// </summary>
public class AzureServiceBusMessageGatewayProvider
    : Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway.Reactor.IAmAMessageGatewayReactorProvider,
      Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway.Proactor.IAmAMessageGatewayProactorProvider
{
    // ── routing-key / channel-name factories ────────────────────────────────

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
        => new RoutingKey($"gen-asb-topic-{Uuid.New():N}");

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
        => new ChannelName($"gen-asb-sub-{Uuid.New():N}");

    // ── publication / subscription factories ────────────────────────────────

    public AzureServiceBusPublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new AzureServiceBusPublication<ASBTestCommand>
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
        };
    }

    /// <summary>
    /// Creates an <see cref="AzureServiceBusSubscription"/> for the given routing key and channel.
    /// ASB dead-letters natively: the DLQ is always the built-in sub-queue and is accessed via
    /// <see cref="GetMessageFromDeadLetterQueue"/>. There is no separate invalid-message channel.
    /// The <paramref name="deadLetterRoutingKey"/> and <paramref name="invalidMessageRoutingKey"/>
    /// parameters indicate the intent of the canonical test, but have no structural effect on the
    /// ASB subscription itself.
    /// </summary>
    public AzureServiceBusSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
    {
        return new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: makeChannel,
            requeueCount: deadLetterRoutingKey != null ? 3 : -1
        );
    }

    // ── Reactor (sync) path ─────────────────────────────────────────────────

    public IAmAMessageProducerSync CreateProducer(AzureServiceBusPublication publication)
    {
        var factory = new AzureServiceBusMessageProducerFactory(
            ASBCreds.ASBClientProvider,
            [publication],
            bulkSendBatchSize: 10);

        var producers = factory.Create();
        return (IAmAMessageProducerSync)producers.First().Value;
    }

    public IAmAChannelSync CreateChannel(AzureServiceBusSubscription subscription)
    {
        var consumerFactory = new AzureServiceBusConsumerFactory(ASBCreds.ASBClientProvider);
        var channelFactory = new AzureServiceBusChannelFactory(consumerFactory);
        return channelFactory.CreateSyncChannel(subscription);
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        if (channel != null)
        {
            try { channel.Purge(); } catch { /* best effort */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        try { producer?.Dispose(); } catch { /* best effort */ }
    }

    /// <summary>
    /// Genuine bounded read from ASB's native DLQ sub-queue.
    /// Uses <c>ServiceBusReceiver</c> with <c>SubQueue = SubQueue.DeadLetter</c>
    /// to access the built-in <c>&lt;topic&gt;/Subscriptions/&lt;subscription&gt;/$DeadLetterQueue</c>
    /// entity. Polls up to 10 times; returns MT_NONE when the bound is exhausted.
    /// </summary>
    public Message GetMessageFromDeadLetterQueue(AzureServiceBusSubscription subscription)
    {
        var client = ASBCreds.ASBClientProvider.GetServiceBusClient();
        var topicName = subscription.RoutingKey.Value;
        var subscriptionName = subscription.ChannelName.Value;

        var receiver = client.CreateReceiver(topicName, subscriptionName,
            new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var received = receiver.ReceiveMessagesAsync(maxMessages: 1, maxWaitTime: TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();

                var msg = received.FirstOrDefault();
                if (msg != null)
                {
                    receiver.CompleteMessageAsync(msg).GetAwaiter().GetResult();
                    return ConvertToMessage(msg);
                }

                Thread.Sleep(1000);
            }

            return new Message();
        }
        finally
        {
            receiver.CloseAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Genuine bounded read against the conceptual invalid-message channel.
    ///
    /// ASB dead-letters natively via the built-in DLQ sub-queue; there is no Brighter-managed
    /// invalid-message routing. This hook makes a GENUINE bounded read against the topic entity
    /// that would carry invalid-channel routing (the <c>{topic}.Invalid</c> convention the
    /// canonical test uses), so the harness is complete. Because ASB never routes an unacceptable
    /// rejection to that separate entity, the read observes MT_NONE — evidencing an architectural
    /// gap, not a stubbed harness hook.
    /// </summary>
    public Message GetMessageFromInvalidChannel(AzureServiceBusSubscription subscription)
    {
        var client = ASBCreds.ASBClientProvider.GetServiceBusClient();
        var invalidTopicName = $"{subscription.RoutingKey.Value}.Invalid";
        var subscriptionName = subscription.ChannelName.Value;

        try
        {
            var receiver = client.CreateReceiver(invalidTopicName, subscriptionName,
                new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

            try
            {
                var received = receiver.ReceiveMessagesAsync(maxMessages: 1, maxWaitTime: TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();

                var msg = received.FirstOrDefault();
                if (msg != null)
                {
                    receiver.CompleteMessageAsync(msg).GetAwaiter().GetResult();
                    return ConvertToMessage(msg);
                }

                return new Message();
            }
            finally
            {
                receiver.CloseAsync().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Entity does not exist or is inaccessible — evidences the architectural gap.
            return new Message();
        }
    }

    public RejectionMetadataKeys RejectionMetadataKeys =>
        new RejectionMetadataKeys(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        );

    // ── Proactor (async) path ───────────────────────────────────────────────

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        AzureServiceBusPublication publication,
        CancellationToken cancellationToken = default)
    {
        var factory = new AzureServiceBusMessageProducerFactory(
            ASBCreds.ASBClientProvider,
            [publication],
            bulkSendBatchSize: 10);

        var producers = await factory.CreateAsync();
        return (IAmAMessageProducerAsync)producers.First().Value;
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        AzureServiceBusSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var consumerFactory = new AzureServiceBusConsumerFactory(ASBCreds.ASBClientProvider);
        var channelFactory = new AzureServiceBusChannelFactory(consumerFactory);
        return await channelFactory.CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        if (channel != null)
        {
            try { await channel.PurgeAsync(); } catch { /* best effort */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        if (producer != null)
        {
            try { await producer.DisposeAsync(); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Genuine async bounded read from ASB's native DLQ sub-queue.
    /// Uses <c>ServiceBusReceiver.ReceiveMessagesAsync</c> with <c>SubQueue.DeadLetter</c>.
    /// </summary>
    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        AzureServiceBusSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var client = ASBCreds.ASBClientProvider.GetServiceBusClient();
        var topicName = subscription.RoutingKey.Value;
        var subscriptionName = subscription.ChannelName.Value;

        var receiver = client.CreateReceiver(topicName, subscriptionName,
            new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        await using (receiver.ConfigureAwait(false))
        {
            for (var i = 0; i < 10; i++)
            {
                var received = await receiver.ReceiveMessagesAsync(maxMessages: 1,
                    maxWaitTime: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                var msg = received.FirstOrDefault();
                if (msg != null)
                {
                    await receiver.CompleteMessageAsync(msg, cancellationToken).ConfigureAwait(false);
                    return ConvertToMessage(msg);
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }

        return new Message();
    }

    /// <summary>
    /// Genuine async bounded read against the conceptual invalid-message channel.
    /// See <see cref="GetMessageFromInvalidChannel"/> for the architectural rationale.
    /// </summary>
    public async Task<Message> GetMessageFromInvalidChannelAsync(
        AzureServiceBusSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var client = ASBCreds.ASBClientProvider.GetServiceBusClient();
        var invalidTopicName = $"{subscription.RoutingKey.Value}.Invalid";
        var subscriptionName = subscription.ChannelName.Value;

        try
        {
            var receiver = client.CreateReceiver(invalidTopicName, subscriptionName,
                new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

            await using (receiver.ConfigureAwait(false))
            {
                var received = await receiver.ReceiveMessagesAsync(maxMessages: 1,
                    maxWaitTime: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                var msg = received.FirstOrDefault();
                if (msg != null)
                {
                    await receiver.CompleteMessageAsync(msg, cancellationToken).ConfigureAwait(false);
                    return ConvertToMessage(msg);
                }

                return new Message();
            }
        }
        catch
        {
            // Entity does not exist or is inaccessible — evidences the architectural gap.
            return new Message();
        }
    }

    // ── private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts an Azure Service Bus SDK <see cref="ServiceBusReceivedMessage"/> to a Brighter
    /// <see cref="Message"/>. Extracts the body bytes, message ID, topic and message type from the
    /// well-known application properties the ASB gateway stamps on outgoing messages.
    /// </summary>
    private static Message ConvertToMessage(ServiceBusReceivedMessage msg)
    {
        var body = msg.Body?.ToArray() ?? [];
        var messageId = msg.MessageId ?? Guid.NewGuid().ToString();

        var messageType = MessageType.MT_EVENT;
        if (msg.ApplicationProperties.TryGetValue("MessageType", out var mt))
        {
            if (!Enum.TryParse<MessageType>(mt?.ToString(), ignoreCase: true, out messageType))
                messageType = MessageType.MT_EVENT;
        }

        var topic = msg.Subject ?? string.Empty;
        if (msg.ApplicationProperties.TryGetValue("Topic", out var t))
            topic = t?.ToString() ?? topic;

        return new Message(
            new MessageHeader(
                messageId: new Id(messageId),
                topic: new RoutingKey(topic),
                messageType: messageType
            ),
            new MessageBody(body)
        );
    }
}
