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
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "AzureServiceBus")]
[Collection("AzureServiceBus")]
public class AzureServiceBusScheduledRetryIsolationTests
{
    [Theory]
    [InlineData(false, false, 1, false, 5, false)]
    [InlineData(false, true, 1, false, 5, false)]
    [InlineData(true, false, 1, false, 5, false)]
    [InlineData(true, true, 1, false, 5, false)]
    [InlineData(false, false, 0, false, 0, false)]
    [InlineData(true, true, 0, false, 5, false)]
    [InlineData(false, true, 0, true, 5, false)]
    [InlineData(true, false, 0, true, 0, false)]
    [InlineData(false, false, 2, false, 5, false)]
    [InlineData(true, true, 2, true, 5, false)]
    [InlineData(false, true, 1, true, 0, false)]
    [InlineData(true, false, 1, true, 5, false)]
    [InlineData(false, false, 1, false, 5, true)]
    [InlineData(true, true, 1, false, 5, true)]
    [InlineData(false, true, 1, true, 0, true)]
    [InlineData(true, false, 1, true, 5, true)]
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
            Assert.IsAssignableFrom<IAmAChannelFactoryWithScheduler>(factory).Scheduler = schedulerMode switch
            {
                1 => observedScheduler,
                2 => new InMemorySchedulerFactory().Create(processor),
                _ => null
            };
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
            Assert.Equal(original.MessageId, received.Id.Value);
            var siblingOriginal = await sibling.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(siblingOriginal);
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
                Assert.True(synchronous
                    ? syncChannel!.Requeue(received, delay)
                    : await asyncChannel!.RequeueAsync(received, delay));
                if (useGlobalScheduler && synchronous)
                    Assert.Equal(callingThread, observedScheduler.LastSyncThread);
                Assert.Contains(activity.Events, e =>
                    e.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingSystem
                        && Equals(t.Value, "azure_service_bus")));
                var redelivered = await ReceiveAsync(TimeSpan.FromSeconds(15));

                // Assert
                Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
                Assert.True(elapsed.Elapsed >= delay - TimeSpan.FromMilliseconds(500), "The retry arrived before its delay.");
                Assert.NotEqual(previousId, redelivered.Id.Value);
                Assert.NotEqual(original.MessageId, redelivered.Id.Value);
                Assert.Equal(original.MessageId, redelivered.Header.Bag[Message.OriginalMessageIdHeaderName]);
                Assert.Equal(attempt, redelivered.Header.HandledCount);
                Assert.Equal(original.ApplicationProperties["cloudEvents:id"],
                    redelivered.Header.Bag["cloudEvents:id"]);
                Assert.Equal(original.ApplicationProperties["cloudEvents:source"],
                    redelivered.Header.Source.ToString());
                Assert.Equal(previousTraceParent, received.Header.TraceParent);
                Assert.Equal(activity.Id, redelivered.Header.TraceParent?.Value);
                Assert.Equal(original.Body.ToString(), redelivered.Body.Value);
                Assert.Equal(original.CorrelationId, redelivered.Header.CorrelationId.Value);
                Assert.Equal(new RoutingKey(logicalTopic), redelivered.Header.Topic);
                Assert.Equal(previousId, received.Id.Value);
                Assert.Equal(new RoutingKey(logicalTopic), received.Header.Topic);
                if (requireSession)
                    Assert.Equal(original.SessionId, redelivered.Header.Bag["SessionId"]);
                received = redelivered;
            }

            Assert.Equal(useGlobalScheduler && synchronous ? 2 : 0, observedScheduler.SyncCalls);
            Assert.Equal(useGlobalScheduler && !synchronous ? 2 : 0, observedScheduler.AsyncCalls);
            if (synchronous)
                syncChannel!.Acknowledge(received);
            else
                await asyncChannel!.AcknowledgeAsync(received);
            Assert.Null(await sibling.ReceiveMessageAsync(TimeSpan.FromSeconds(1)));
            Assert.Null(await schedulerReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1)));

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
