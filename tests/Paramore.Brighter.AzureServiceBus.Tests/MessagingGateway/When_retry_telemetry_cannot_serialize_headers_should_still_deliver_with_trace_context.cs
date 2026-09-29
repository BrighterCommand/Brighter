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
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "AzureServiceBus")]
[NotInParallel("AzureServiceBus")]
public class AzureServiceBusRetryTelemetryTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task When_retry_telemetry_cannot_serialize_headers_should_still_deliver_with_trace_context(
        bool synchronous, bool nonFiniteHeader)
    {
        // Arrange
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
        const string localHeader = "retry-telemetry-local-type";
        MessageHeader.RegisterLocalHeader(localHeader);
        var queue = "retry-telemetry-" + Guid.NewGuid().ToString("N");
        const bool useQueue = true;
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        await administration.CreateQueueAsync(new CreateQueueOptions(queue)
        {
            DefaultMessageTimeToLive = TimeSpan.FromHours(1)
        });
        try
        {
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                new SubscriptionName("consumer"), new ChannelName("consumer"),
                new RoutingKey(useQueue ? queue : "orders"),
                messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Assume,
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
                {
                    UseServiceBusQueue = useQueue,
                    ForwardTo = useQueue ? null : queue
                });
            using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            await using var sender = client.CreateSender(queue);
            var original = new ServiceBusMessage("order") { MessageId = Guid.NewGuid().ToString() };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);
            var message = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(10))
                : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(message.Id.Value).IsEqualTo(original.MessageId);
            message.Header.UpdateHandledCount();
            message.Header.Bag[localHeader] = typeof(AzureServiceBusRetryTelemetryTests);
            if (nonFiniteHeader)
                message.Header.Bag["measurement"] = double.NaN;
            var previousTraceParent = message.Header.TraceParent;
            using var activity = new Activity("retry telemetry").SetIdFormat(ActivityIdFormat.W3C).Start();
            // Act
            await Assert.That(synchronous ? syncChannel!.Requeue(message, TimeSpan.Zero)
                : await asyncChannel!.RequeueAsync(message, TimeSpan.Zero)).IsTrue();
            await using var receiver = client.CreateReceiver(queue);
            var retry = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));

            // Assert
            await Assert.That(retry).IsNotNull();
            await Assert.That(retry.MessageId).IsNotEqualTo(original.MessageId);
            await Assert.That(retry.ApplicationProperties[Message.OriginalMessageIdHeaderName]).IsEqualTo(original.MessageId);
            await Assert.That(retry.Body.ToArray()).IsEqualTo(original.Body.ToArray());
            await Assert.That(retry.ApplicationProperties.ContainsKey(localHeader)).IsFalse();
            await Assert.That(message.Header.Bag[localHeader]).IsEqualTo(typeof(AzureServiceBusRetryTelemetryTests));
            await Assert.That(message.Header.TraceParent).IsEqualTo(previousTraceParent);
            await Assert.That(message.Id.Value).IsEqualTo(original.MessageId);
            await Assert.That(retry.ApplicationProperties["cloudevents:traceparent"]).IsEqualTo(activity.Id);
            if (nonFiniteHeader)
                await Assert.That(double.IsNaN((await Assert.That(retry.ApplicationProperties["measurement"]).IsTypeOf<double>()))).IsTrue();
            else
                await Assert.That(System.Linq.Enumerable.Any(activity.Events, e => e.Tags.Any(t =>
                    t.Key == BrighterSemanticConventions.MessagingSystem && Equals(t.Value, "azure_service_bus")))).IsTrue();
            await Assert.That(System.Linq.Enumerable.Any(activity.Events.SelectMany(e => e.Tags), t =>
                t.Value?.ToString()?.Contains(localHeader, StringComparison.Ordinal) == true)).IsFalse();
            await receiver.CompleteMessageAsync(retry);
        }
        finally
        {
            await administration.DeleteQueueAsync(queue);
        }
    }
}
