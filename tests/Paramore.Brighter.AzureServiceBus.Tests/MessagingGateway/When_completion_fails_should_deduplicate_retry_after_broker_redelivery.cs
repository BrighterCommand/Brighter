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

#nullable enable

using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "AzureServiceBus")]
[NotInParallel("AzureServiceBus")]
public class AzureServiceBusRetryRedeliveryDeduplicationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_completion_fails_should_deduplicate_retry_after_broker_redelivery(bool synchronous)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var queue = "retry-redelivery-" + Guid.NewGuid().ToString("N");
        await administration.CreateQueueAsync(new CreateQueueOptions(queue)
        {
            DefaultMessageTimeToLive = TimeSpan.FromHours(1),
            RequiresDuplicateDetection = true,
            DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(5)
        });
        try
        {
            await using var faultClient = new CompletionFaultServiceBusClient(client, administration);
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(faultClient));
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                new SubscriptionName("retry"), new ChannelName(queue), new RoutingKey(queue),
                messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Assume,
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
            using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            await using var sender = client.CreateSender(queue);
            var original = new ServiceBusMessage("order") { MessageId = Guid.NewGuid().ToString() };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);
            var first = await ReceiveAsync();
            await Assert.That(first.Id.Value).IsEqualTo(original.MessageId);
            await Assert.That(first.Header.HandledCount).IsEqualTo(0);
            first.Header.UpdateHandledCount();

            // Act
            var error = await TestExceptionRecorder.CaptureAsync(() => RequeueAsync(first));
            await Assert.That((await Assert.That(error).IsTypeOf<ServiceBusException>()).Reason).IsEqualTo(ServiceBusFailureReason.ServiceBusy);
            if (synchronous) syncChannel!.Nack(first);
            else await asyncChannel!.NackAsync(first);
            var redelivery = await ReceiveAsync();
            await Assert.That(redelivery).IsNotSameReferenceAs(first);
            await Assert.That(redelivery.Id.Value).IsEqualTo(original.MessageId);
            await Assert.That(redelivery.Header.HandledCount).IsEqualTo(0);
            redelivery.Header.UpdateHandledCount();
            await Assert.That(await RequeueAsync(redelivery)).IsTrue();
            var retry = await ReceiveAsync();

            // Assert
            await Assert.That(retry.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
            await Assert.That(retry.Id.Value).IsNotEqualTo(original.MessageId);
            await Assert.That(retry.Header.Bag[Message.OriginalMessageIdHeaderName]).IsEqualTo(original.MessageId);
            await Assert.That(retry.Header.HandledCount).IsEqualTo(1);
            await Assert.That(retry.Body.Value).IsEqualTo("order");
            await Assert.That(faultClient.Receiver!.CompletionAttempts).IsEqualTo(2);
            if (synchronous) syncChannel!.Acknowledge(retry);
            else await asyncChannel!.AcknowledgeAsync(retry);
            var extra = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(12))
                : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(12));
            await Assert.That(extra.Header.MessageType).IsEqualTo(MessageType.MT_NONE);

            Task<Message> ReceiveAsync() => synchronous
                ? Task.FromResult(syncChannel!.Receive(TimeSpan.FromSeconds(20)))
                : asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(20));

            Task<bool> RequeueAsync(Message message) => synchronous
                ? Task.FromResult(syncChannel!.Requeue(message, TimeSpan.FromSeconds(10)))
                : asyncChannel!.RequeueAsync(message, TimeSpan.FromSeconds(10));
        }
        finally
        {
            await administration.DeleteQueueAsync(queue);
        }
    }
}
