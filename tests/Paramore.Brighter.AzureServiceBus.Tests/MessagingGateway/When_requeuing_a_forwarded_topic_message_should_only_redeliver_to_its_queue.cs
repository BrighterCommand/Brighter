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
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "AzureServiceBus")]
[Collection("AzureServiceBus")]
public class AzureServiceBusForwardedRetryIsolationTests
{
    [Theory]
    [InlineData(OnMissingChannel.Create, false, false)]
    [InlineData(OnMissingChannel.Create, true, false)]
    [InlineData(OnMissingChannel.Validate, false, false)]
    [InlineData(OnMissingChannel.Validate, true, false)]
    [InlineData(OnMissingChannel.Assume, false, false)]
    [InlineData(OnMissingChannel.Assume, true, false)]
    [InlineData(OnMissingChannel.Create, false, true)]
    [InlineData(OnMissingChannel.Create, true, true)]
    public async Task When_requeuing_a_forwarded_topic_message_should_only_redeliver_to_its_queue(
        OnMissingChannel makeChannels, bool synchronous, bool requireSession)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var topicName = $"forwarded-retry-{Guid.NewGuid():N}";
        var queueName = $"retry-queue-{Guid.NewGuid():N}";
        await administration.CreateTopicAsync(topicName);

        try
        {
            foreach (var name in new[] { "b", "c" })
            {
                await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, name)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1)
                });
            }

            if (makeChannels != OnMissingChannel.Create)
            {
                await administration.CreateQueueAsync(new CreateQueueOptions(queueName)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                    RequiresSession = requireSession
                });
                await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, "a")
                {
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                    ForwardTo = queueName
                });
            }

            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                subscriptionName: new SubscriptionName("a"),
                channelName: new ChannelName("a"),
                routingKey: new RoutingKey(topicName),
                messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
                makeChannels: makeChannels,
                requeueCount: 3,
                requeueDelay: TimeSpan.FromSeconds(2),
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
                {
                    ForwardTo = queueName,
                    RequireSession = requireSession,
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1)
                });
            using var syncRetrying = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncRetrying = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            Assert.True((await administration.QueueExistsAsync(queueName)).Value);
            var forwardingSubscription = await administration.GetSubscriptionAsync(topicName, "a");
            Assert.EndsWith(queueName, forwardingSubscription.Value.ForwardTo.TrimEnd('/'));
            Assert.False(forwardingSubscription.Value.RequiresSession);
            Assert.Equal(requireSession, (await administration.GetQueueAsync(queueName)).Value.RequiresSession);

            await using var otherB = client.CreateReceiver(topicName, "b");
            await using var otherC = client.CreateReceiver(topicName, "c");
            await using var sender = client.CreateSender(topicName);
            var original = new ServiceBusMessage("{\"orderId\":42}")
            {
                MessageId = Guid.NewGuid().ToString(),
                SessionId = requireSession ? "orders" : null
            };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);

            var received = await ReceiveRetryAsync(TimeSpan.FromSeconds(10));
            var receivedB = await otherB.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            var receivedC = await otherC.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(original.MessageId, received.Id.Value);
            Assert.NotNull(receivedB);
            Assert.NotNull(receivedC);
            Assert.Equal(original.MessageId, receivedB.MessageId);
            Assert.Equal(original.MessageId, receivedC.MessageId);
            await otherB.CompleteMessageAsync(receivedB);
            await otherC.CompleteMessageAsync(receivedC);

            // Act
            received.Header.UpdateHandledCount();
            var elapsed = Stopwatch.StartNew();
            Assert.True(synchronous
                ? syncRetrying!.Requeue(received, TimeSpan.FromSeconds(5))
                : await asyncRetrying!.RequeueAsync(received, TimeSpan.FromSeconds(5)));

            // Assert
            if (!requireSession)
            {
                var beforeDelay = await ReceiveRetryAsync(TimeSpan.FromSeconds(1));
                Assert.Equal(MessageType.MT_NONE, beforeDelay.Header.MessageType);
            }

            var redelivered = await ReceiveRetryAsync(TimeSpan.FromSeconds(10));
            Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(4.5), "The retry must respect its scheduled delay.");
            Assert.NotEqual(original.MessageId, redelivered.Id.Value);
            Assert.Equal(original.MessageId, redelivered.Header.Bag[Message.OriginalMessageIdHeaderName]);
            Assert.Equal(original.MessageId, received.Id.Value);
            Assert.Equal(original.Body.ToString(), redelivered.Body.Value);
            Assert.Equal(1, redelivered.Header.HandledCount);
            Assert.Equal(new RoutingKey(topicName), redelivered.Header.Topic);
            Assert.Equal(new RoutingKey(topicName), received.Header.Topic);
            Assert.Null(await otherB.ReceiveMessageAsync(TimeSpan.FromSeconds(1)));
            Assert.Null(await otherC.ReceiveMessageAsync(TimeSpan.FromSeconds(1)));
            if (synchronous)
                syncRetrying!.Acknowledge(redelivered);
            else
                await asyncRetrying!.AcknowledgeAsync(redelivered);

            Task<Message> ReceiveRetryAsync(TimeSpan timeout) => synchronous
                ? Task.FromResult(syncRetrying!.Receive(timeout))
                : asyncRetrying!.ReceiveAsync(timeout);
        }
        finally
        {
            await administration.DeleteTopicAsync(topicName);
            if ((await administration.QueueExistsAsync(queueName)).Value)
                await administration.DeleteQueueAsync(queueName);
        }
    }
}
