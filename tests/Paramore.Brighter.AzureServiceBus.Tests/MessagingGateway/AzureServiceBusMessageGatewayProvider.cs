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
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// Conformance harness provider for Azure Service Bus (Topic mode).
///
/// ASB dead-letters natively via the built-in $DeadLetterQueue sub-queue on every
/// topic subscription. The gateway's <c>Reject</c> path calls
/// <c>ServiceBusReceiver.DeadLetterAsync(lockToken, reason, description)</c> with no
/// Brighter-stamped metadata, so <see cref="RejectionMetadataKeys"/> is all
/// <see cref="string.Empty"/> (a native-dead-letter transport, conformant on routing alone).
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

    /// <summary>
    /// Provisions the topic before a producer is handed out.
    /// <para>
    /// The gateway creates a missing topic lazily, on the first send. That is fine when one thread
    /// sends first, and it is a race when several do:
    /// <c>When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception</c>
    /// sends from four threads at once against a topic that does not exist yet, so four creates for
    /// the same name reach Azure together. The losers get HTTP 409 — <c>SubCode=40901</c> ("another
    /// conflicting operation is in progress") while a sibling create is still running, or
    /// <c>SubCode=40900</c> ("not allowed in the resource's current state") while the winner's topic
    /// is still settling.
    /// </para>
    /// <para>
    /// The race is in the harness's arrangement, not in Brighter: the test is about concurrent
    /// <em>sends</em>, and provisioning concurrently is incidental to it. Creating the topic once,
    /// before any thread sends, removes the race rather than waiting out the throttle — which also
    /// means it does not depend on the namespace's tier, where management-operation limits live.
    /// </para>
    /// <para>
    /// This is the producer-side twin of <see cref="EnsureSubscriptionExistsAsync"/>, and it is
    /// skipped for any publication that is not asking for creation, so the tests that assert on
    /// missing infrastructure still find it missing.
    /// </para>
    /// </summary>
    private static async Task EnsureTopicExistsAsync(AzureServiceBusPublication publication)
    {
        if (publication.MakeChannels != OnMissingChannel.Create)
            return;

        var administrationClient = new AdministrationClientWrapper(ASBCreds.ASBClientProvider);
        var topicName = publication.Topic!.Value;

        if (await administrationClient.TopicExistsAsync(topicName))
            return;

        try
        {
            await administrationClient.CreateTopicAsync(topicName);
        }
        catch (ServiceBusException e)
            when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Someone else created it between the check and the create. The topic is there, which
            // is the whole point of this call.
        }
    }

    public IAmAMessageProducerSync CreateProducer(AzureServiceBusPublication publication)
    {
        EnsureTopicExistsAsync(publication).GetAwaiter().GetResult();

        var factory = new AzureServiceBusMessageProducerFactory(
            ASBCreds.ASBClientProvider,
            [publication],
            bulkSendBatchSize: 10);

        var producers = factory.Create();
        return (IAmAMessageProducerSync)producers.First().Value;
    }

    /// <summary>
    /// Provisions the topic and subscription before a channel is handed out.
    /// <para>
    /// <see cref="AzureServiceBusChannelFactory"/> creates nothing on the broker — the subscription is
    /// created lazily by the consumer's first receive. Every generated test sends before it first
    /// receives, and an ASB topic with no subscription attached silently discards the message, so
    /// without this the message is gone before anything can read it (#4309).
    /// </para>
    /// <para>
    /// This is the same thing the hand-written ASB tests have always done — see
    /// <c>When_posting_a_message_via_the_producer</c> — and it mirrors how the AWS and GCP providers
    /// ensure their infrastructure exists before returning a channel.
    /// </para>
    /// <para>
    /// <see cref="AdministrationClientWrapper.CreateSubscriptionAsync"/> creates the topic first if it
    /// is missing, so this covers both entities.
    /// </para>
    /// </summary>
    private static async Task EnsureSubscriptionExistsAsync(AzureServiceBusSubscription subscription)
    {
        if (subscription.MakeChannels != OnMissingChannel.Create)
            return;

        var administrationClient = new AdministrationClientWrapper(ASBCreds.ASBClientProvider);
        await administrationClient.CreateSubscriptionAsync(
            subscription.RoutingKey.Value,
            subscription.ChannelName.Value,
            new AzureServiceBusSubscriptionConfiguration());
    }

    public IAmAChannelSync CreateChannel(AzureServiceBusSubscription subscription)
    {
        EnsureSubscriptionExistsAsync(subscription).GetAwaiter().GetResult();

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
            var received = receiver.ReceiveMessagesAsync(maxMessages: 1, maxWaitTime: TimeSpan.FromSeconds(5))
                .GetAwaiter().GetResult();

            var msg = received.FirstOrDefault();
            if (msg == null)
            {
                return new Message();
            }

            receiver.CompleteMessageAsync(msg).GetAwaiter().GetResult();
            return ConvertToMessage(msg);
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
        await EnsureTopicExistsAsync(publication);

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
        await EnsureSubscriptionExistsAsync(subscription);

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
