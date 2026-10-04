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

#nullable enable

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Characterises R-6 / AC-5 on the GCP stream consumer: with <c>requeueCount: -1</c>, the budget
/// is disabled, <c>DiscardRequeuedMessagesEnabled()</c> (<c>MessagePump.cs:171</c>) returns
/// <c>false</c>, the message is requeued indefinitely, and no <c>DeliveryError</c> rejection is
/// ever issued. After a 60-second run the dispatch count exceeds 3 and the Brighter DLQ is empty.
/// This is the "-1" clause of AC-35 and is unguarded — it holds regardless of R-13's branch,
/// because the guard lives in the pump (<c>MessagePump.cs:171</c>), not in the GCP consumers.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamBudgetMinusOneNeverRejectsReactorTests
{
    /// <summary>
    /// With <c>requeueCount: -1</c>, no native <c>DeadLetterPolicy</c>, and a Brighter DLQ routing
    /// key, the pump runs for 60 s. The dispatch count exceeds 3, no <c>DeliveryError</c> rejection
    /// is issued, and the Brighter DLQ is empty (R-6, AC-5).
    /// </summary>
    /// <remarks>
    /// The stream consumer's <c>Requeue</c> Nacks the message (<c>GcpStreamMessage.Reject()</c>),
    /// which completes the local reply and frees the client's flow-control slot immediately — it
    /// does not wait on the ack deadline — so, like the pull consumer, redelivery is prompt and the
    /// dispatch count will far exceed 3 within 60 s.
    /// </remarks>
    [Fact]
    public async Task When_gcp_budget_is_minus_one_should_never_reject()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new GcpStreamMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        // Subscription: budget = -1 (disabled), no native DeadLetterPolicy, with a Brighter DLQ
        // routing key. DiscardRequeuedMessagesEnabled() returns false, so HandledCountReached is
        // never called and the message is requeued indefinitely.
        var subscription = new GcpPubSubSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            requeueCount: -1,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            deadLetterRoutingKey: dlqRoutingKey);

        var publication = provider.CreatePublication(routingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            // CreateChannel pre-provisions the Brighter DLQ topic/subscription because
            // DeadLetterRoutingKey is set and MakeChannels == Create (ADR 0078 step 5), so there is
            // somewhere to dead-letter to — and somewhere to read from below — even under the RED
            // mutation.
            channel = provider.CreateChannel(subscription);

            var cmd = new ConformanceDeferredCommand { Value = "budget minus one never rejects test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the pump with budget -1; pump for 60 s, then quit and await it (the quit
            // must happen before the channel is disposed, so the pump settles the message it holds
            // rather than leaving Dispose to nack it).
            // Under the RED mutation (DiscardRequeuedMessagesEnabled returns true),
            // HandledCountReached(-1) is true on the first deferral (HandledCount 0 >= -1), so
            // dispatch count stays at 1 and the DLQ receives the message — the test then fails on
            // the "dispatch count > 3" assertion.
            var pump = ConformanceDeferredPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            await Task.Delay(TimeSpan.FromSeconds(60));

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            var key = ConformanceDeferredPump.KeyOf(message);
            var dispatchCount = ConformanceDeferredPump.GetDispatchCount(key);

            // Assert — R-6 / AC-5: handler invoked more than 3 times with budget -1
            Assert.True(dispatchCount > 3,
                $"Expected dispatch count > 3 after 60 s with requeueCount: -1, but was {dispatchCount}");

            // No DeliveryError rejection was issued — the Brighter DLQ must be empty
            var dlqMessage = provider.GetMessageFromDeadLetterQueue(subscription);
            Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}
