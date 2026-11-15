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

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "AzureServiceBus")]
[NotInParallel("AzureServiceBus")]
public class AzureServiceBusForwardedRetryIsolationTests
{
    [Test]
    [Arguments(OnMissingChannel.Create, false, false)]
    [Arguments(OnMissingChannel.Create, true, false)]
    [Arguments(OnMissingChannel.Validate, false, false)]
    [Arguments(OnMissingChannel.Validate, true, false)]
    [Arguments(OnMissingChannel.Assume, false, false)]
    [Arguments(OnMissingChannel.Assume, true, false)]
    [Arguments(OnMissingChannel.Create, false, true)]
    [Arguments(OnMissingChannel.Create, true, true)]
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
            await Assert.That((await administration.QueueExistsAsync(queueName)).Value).IsTrue();
            var forwardingSubscription = await administration.GetSubscriptionAsync(topicName, "a");
            await Assert.That(forwardingSubscription.Value.ForwardTo.TrimEnd('/')).EndsWith(queueName);
            await Assert.That(forwardingSubscription.Value.RequiresSession).IsFalse();
            await Assert.That((await administration.GetQueueAsync(queueName)).Value.RequiresSession).IsEqualTo(requireSession);

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
            await Assert.That(received.Id.Value).IsEqualTo(original.MessageId);
            await Assert.That(receivedB).IsNotNull();
            await Assert.That(receivedC).IsNotNull();
            await Assert.That(receivedB.MessageId).IsEqualTo(original.MessageId);
            await Assert.That(receivedC.MessageId).IsEqualTo(original.MessageId);
            await otherB.CompleteMessageAsync(receivedB);
            await otherC.CompleteMessageAsync(receivedC);

            // Act
            received.Header.UpdateHandledCount();
            var elapsed = Stopwatch.StartNew();
            await Assert.That(synchronous
                ? syncRetrying!.Requeue(received, TimeSpan.FromSeconds(5))
                : await asyncRetrying!.RequeueAsync(received, TimeSpan.FromSeconds(5))).IsTrue();

            // Assert
            if (!requireSession)
            {
                var beforeDelay = await ReceiveRetryAsync(TimeSpan.FromSeconds(1));
                await Assert.That(beforeDelay.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
            }

            var redelivered = await ReceiveRetryAsync(TimeSpan.FromSeconds(10));
            await Assert.That(elapsed.Elapsed >= TimeSpan.FromSeconds(4.5)).IsTrue().Because("The retry must respect its scheduled delay.");
            await Assert.That(redelivered.Id.Value).IsNotEqualTo(original.MessageId);
            await Assert.That(redelivered.Header.Bag[Message.OriginalMessageIdHeaderName]).IsEqualTo(original.MessageId);
            await Assert.That(received.Id.Value).IsEqualTo(original.MessageId);
            await Assert.That(redelivered.Body.Value).IsEqualTo(original.Body.ToString());
            await Assert.That(redelivered.Header.HandledCount).IsEqualTo(1);
            await Assert.That(redelivered.Header.Topic).IsEqualTo(new RoutingKey(topicName));
            await Assert.That(received.Header.Topic).IsEqualTo(new RoutingKey(topicName));
            await Assert.That(await otherB.ReceiveMessageAsync(TimeSpan.FromSeconds(1))).IsNull();
            await Assert.That(await otherC.ReceiveMessageAsync(TimeSpan.FromSeconds(1))).IsNull();
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
