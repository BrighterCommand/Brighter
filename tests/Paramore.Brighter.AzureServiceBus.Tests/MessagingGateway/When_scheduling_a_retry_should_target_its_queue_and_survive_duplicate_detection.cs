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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Azure;
using Paramore.Brighter.Observability;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Polly.Registry;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "AzureServiceBus")]
[NotInParallel("AzureServiceBus")]
public class AzureServiceBusScheduledRetryIsolationTests
{
    [Test]
    [Arguments(false, false, 1, false, 5, false)]
    [Arguments(false, true, 1, false, 5, false)]
    [Arguments(true, false, 1, false, 5, false)]
    [Arguments(true, true, 1, false, 5, false)]
    [Arguments(false, false, 0, false, 0, false)]
    [Arguments(true, true, 0, false, 5, false)]
    [Arguments(false, true, 0, true, 5, false)]
    [Arguments(true, false, 0, true, 0, false)]
    [Arguments(false, false, 2, false, 5, false)]
    [Arguments(true, true, 2, true, 5, false)]
    [Arguments(false, true, 1, true, 0, false)]
    [Arguments(true, false, 1, true, 5, false)]
    [Arguments(false, false, 1, false, 5, true)]
    [Arguments(true, true, 1, false, 5, true)]
    [Arguments(false, true, 1, true, 0, true)]
    [Arguments(true, false, 1, true, 5, true)]
    public async Task When_scheduling_a_retry_should_target_its_queue_and_survive_duplicate_detection(
        bool synchronous, bool requireSession, int schedulerMode, bool useQueue, int delaySeconds, bool useGlobalScheduler)
    {
        // Arrange
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var suffix = Guid.NewGuid().ToString("N");
        var topicName = $"retry-events-{suffix}";
        var retryQueue = $"retry-consumer-{suffix}";
        var schedulerQueue = $"retry-scheduler-{suffix}";
        var delay = TimeSpan.FromSeconds(delaySeconds);

        await administration.CreateTopicAsync(topicName);
        try
        {
            await administration.CreateQueueAsync(new CreateQueueOptions(retryQueue)
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(5),
                RequiresSession = requireSession
            });
            await administration.CreateQueueAsync(new CreateQueueOptions(schedulerQueue)
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            });
            await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, "a")
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                ForwardTo = retryQueue
            });
            await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, "b")
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            });

            var processor = new CommandProcessor(new SubscriberRegistry(),
                new SimpleHandlerFactoryAsync(_ => throw new InvalidOperationException("No handler is needed for broker scheduling.")),
                new InMemoryRequestContextFactory(), new DefaultPolicy(),
                new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            var schedulerFactory = new AzureServiceBusSchedulerFactory(provider, new RoutingKey(schedulerQueue));
            var observedScheduler = new RecordingMessageRequeueScheduler(schedulerFactory.Create(processor));
            var retryPublication = new AzureServiceBusPublication
            {
                RequeueScheduler = useGlobalScheduler ? RequeueSchedulerMode.Global : RequeueSchedulerMode.Native
            };
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider, retryPublication));
            await Assert.That(factory).IsAssignableTo<IAmAChannelFactoryWithScheduler>();
            var logicalTopic = useQueue ? retryQueue : topicName;

            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                subscriptionName: new SubscriptionName("a"), channelName: new ChannelName("a"),
                routingKey: new RoutingKey(logicalTopic),
                messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Assume, requeueCount: 3, requeueDelay: delay,
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
                {
                    ForwardTo = useQueue ? null : retryQueue,
                    UseServiceBusQueue = useQueue,
                    RequireSession = requireSession,
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1)
                });
            using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            await using var sibling = client.CreateReceiver(topicName, "b");
            await using var schedulerReceiver = client.CreateReceiver(schedulerQueue);
            await using var sender = client.CreateSender(topicName);
            var original = new ServiceBusMessage("{\"orderId\":42}")
            {
                MessageId = Guid.NewGuid().ToString(),
                CorrelationId = Guid.NewGuid().ToString(),
                SessionId = requireSession ? "orders" : null,
                TimeToLive = TimeSpan.FromHours(1)
            };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            original.ApplicationProperties["cloudEvents:id"] = "business-event-" + suffix;
            original.ApplicationProperties["cloudEvents:source"] = "https://example.org/orders";
            await sender.SendMessageAsync(original);
            var received = await ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(received.Id.Value).IsEqualTo(original.MessageId);
            var siblingOriginal = await sibling.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            await Assert.That(siblingOriginal).IsNotNull();
            await sibling.CompleteMessageAsync(siblingOriginal);

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                // Act
                received.Header.UpdateHandledCount();
                var previousId = received.Id.Value;
                var previousTraceParent = received.Header.TraceParent;
                var callingThread = Environment.CurrentManagedThreadId;
                using var activity = new Activity("ASB retry").SetIdFormat(ActivityIdFormat.W3C).Start();
                var elapsed = Stopwatch.StartNew();
                await Assert.That(synchronous
                    ? syncChannel!.Requeue(received, delay)
                    : await asyncChannel!.RequeueAsync(received, delay)).IsTrue();
                if (useGlobalScheduler && synchronous)
                    await Assert.That(observedScheduler.LastSyncThread).IsEqualTo(callingThread);
                await Assert.That(System.Linq.Enumerable.Any(activity.Events, e =>
                    e.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingSystem
                        && Equals(t.Value, "azure_service_bus")))).IsTrue();
                var redelivered = await ReceiveAsync(TimeSpan.FromSeconds(15));

                // Assert
                await Assert.That(redelivered.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
                await Assert.That(elapsed.Elapsed >= delay - TimeSpan.FromMilliseconds(500)).IsTrue().Because("The retry arrived before its delay.");
                await Assert.That(redelivered.Id.Value).IsNotEqualTo(previousId);
                await Assert.That(redelivered.Id.Value).IsNotEqualTo(original.MessageId);
                await Assert.That(redelivered.Header.Bag[Message.OriginalMessageIdHeaderName]).IsEqualTo(original.MessageId);
                await Assert.That(redelivered.Header.HandledCount).IsEqualTo(attempt);
                await Assert.That(redelivered.Header.Bag["cloudEvents:id"]).IsEqualTo(original.ApplicationProperties["cloudEvents:id"]);
                await Assert.That(redelivered.Header.Source.ToString()).IsEqualTo(original.ApplicationProperties["cloudEvents:source"]);
                await Assert.That(received.Header.TraceParent).IsEqualTo(previousTraceParent);
                await Assert.That(redelivered.Header.TraceParent?.Value).IsEqualTo(activity.Id);
                await Assert.That(redelivered.Body.Value).IsEqualTo(original.Body.ToString());
                await Assert.That(redelivered.Header.CorrelationId.Value).IsEqualTo(original.CorrelationId);
                await Assert.That(redelivered.Header.Topic).IsEqualTo(new RoutingKey(logicalTopic));
                await Assert.That(received.Id.Value).IsEqualTo(previousId);
                await Assert.That(received.Header.Topic).IsEqualTo(new RoutingKey(logicalTopic));
                if (requireSession)
                    await Assert.That(redelivered.Header.Bag["SessionId"]).IsEqualTo(original.SessionId);
                received = redelivered;
            }

            await Assert.That(observedScheduler.SyncCalls).IsEqualTo(useGlobalScheduler && synchronous ? 2 : 0);
            await Assert.That(observedScheduler.AsyncCalls).IsEqualTo(useGlobalScheduler && !synchronous ? 2 : 0);
            if (synchronous)
                syncChannel!.Acknowledge(received);
            else
                await asyncChannel!.AcknowledgeAsync(received);
            await Assert.That(await sibling.ReceiveMessageAsync(TimeSpan.FromSeconds(1))).IsNull();
            await Assert.That(await schedulerReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1))).IsNull();

            Task<Message> ReceiveAsync(TimeSpan timeout) => synchronous
                ? Task.FromResult(syncChannel!.Receive(timeout))
                : asyncChannel!.ReceiveAsync(timeout);
        }
        finally
        {
            await administration.DeleteTopicAsync(topicName);
            foreach (var queue in new[] { retryQueue, schedulerQueue })
            {
                if ((await administration.QueueExistsAsync(queue)).Value)
                    await administration.DeleteQueueAsync(queue);
            }
        }
    }
}
