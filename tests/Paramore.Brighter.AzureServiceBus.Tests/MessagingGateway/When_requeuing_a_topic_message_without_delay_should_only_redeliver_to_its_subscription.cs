#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "AzureServiceBus")]
[Collection("AzureServiceBus")]
public class AzureServiceBusImmediateRetryIsolationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(null, true)]
    [InlineData(0, true)]
    public async Task When_requeuing_a_topic_message_without_delay_should_only_redeliver_to_its_subscription(
        int? delayMilliseconds, bool synchronous)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var topicName = $"retry-isolation-{Guid.NewGuid():N}";
        await administration.CreateTopicAsync(topicName);

        try
        {
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            var subscription = CreateSubscription(topicName, "a",
                synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor);
            using var syncRetrying = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncRetrying = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            await using var otherB = await factory.CreateAsyncChannelAsync(CreateSubscription(topicName, "b"));
            await using var otherC = await factory.CreateAsyncChannelAsync(CreateSubscription(topicName, "c"));
            await using var sender = client.CreateSender(topicName);
            var original = new ServiceBusMessage("{\"orderId\":42}")
            {
                MessageId = Guid.NewGuid().ToString()
            };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);

            var received = await ReceiveRetryAsync();
            var receivedB = await otherB.ReceiveAsync(TimeSpan.FromSeconds(10));
            var receivedC = await otherC.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(original.MessageId, received.Id.Value);
            Assert.Equal(original.MessageId, receivedB.Id.Value);
            Assert.Equal(original.MessageId, receivedC.Id.Value);
            Assert.Equal(0, received.Header.HandledCount);
            await otherB.AcknowledgeAsync(receivedB);
            await otherC.AcknowledgeAsync(receivedC);

            TimeSpan? delay = delayMilliseconds.HasValue
                ? TimeSpan.FromMilliseconds(delayMilliseconds.Value)
                : null;

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                // Act
                received.Header.UpdateHandledCount();
                Assert.True(synchronous
                    ? syncRetrying!.Requeue(received, delay)
                    : await asyncRetrying!.RequeueAsync(received, delay));
                received = await ReceiveRetryAsync();

                // Assert
                Assert.Equal(original.MessageId, received.Id.Value);
                Assert.Equal(original.Body.ToString(), received.Body.Value);
                Assert.Equal(attempt, received.Header.HandledCount);
                var unexpectedB = await otherB.ReceiveAsync(TimeSpan.FromSeconds(1));
                var unexpectedC = await otherC.ReceiveAsync(TimeSpan.FromSeconds(1));
                Assert.Equal(MessageType.MT_NONE, unexpectedB.Header.MessageType);
                Assert.Equal(MessageType.MT_NONE, unexpectedC.Header.MessageType);
            }

            if (synchronous)
                syncRetrying!.Acknowledge(received);
            else
                await asyncRetrying!.AcknowledgeAsync(received);

            Task<Message> ReceiveRetryAsync() => synchronous
                ? Task.FromResult(syncRetrying!.Receive(TimeSpan.FromSeconds(10)))
                : asyncRetrying!.ReceiveAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await administration.DeleteTopicAsync(topicName);
        }
    }

    private static AzureServiceBusSubscription CreateSubscription(string topicName, string subscriptionName,
        MessagePumpType messagePumpType = MessagePumpType.Proactor)
        => new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(subscriptionName),
            routingKey: new RoutingKey(topicName),
            messagePumpType: messagePumpType,
            makeChannels: OnMissingChannel.Create,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            });
}
