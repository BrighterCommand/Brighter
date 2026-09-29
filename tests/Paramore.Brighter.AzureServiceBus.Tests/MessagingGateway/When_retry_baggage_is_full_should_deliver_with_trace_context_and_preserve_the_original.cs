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
public class AzureServiceBusRetryBaggageTests
{
    [Test]
    [Arguments(false, "key0", false)]
    [Arguments(true, "key0", false)]
    [Arguments(false, "extra", false)]
    [Arguments(true, "extra", false)]
    [Arguments(false, "tenant.name", false)]
    [Arguments(true, "tenant.name", false)]
    [Arguments(false, "key0", true)]
    [Arguments(true, "key0", true)]
    [Arguments(false, "extra", true)]
    [Arguments(true, "extra", true)]
    [Arguments(false, "tenant.name", true)]
    [Arguments(true, "tenant.name", true)]
    public async Task When_retry_baggage_is_full_should_deliver_with_trace_context_and_preserve_the_original(
        bool synchronous, string ambientKey, bool nonFiniteHeader)
    {
        // Arrange
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
        var previousBaggage = OpenTelemetry.Baggage.Current;
        var queue = "retry-baggage-" + Guid.NewGuid().ToString("N");
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
            var originalBaggage = new Paramore.Brighter.Observability.Baggage();
            for (var i = 0; i < 32; i++) originalBaggage.Add("key" + i, "original");
            original.ApplicationProperties["cloudevents:baggage"] = originalBaggage.ToString();
            await sender.SendMessageAsync(original);
            var message = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(10))
                : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(10));
            await Assert.That(message.Id.Value).IsEqualTo(original.MessageId);
            message.Header.UpdateHandledCount();
            await Assert.That(message.Header.Baggage.Count()).IsEqualTo(32);
            OpenTelemetry.Baggage.Current = default;
            OpenTelemetry.Baggage.SetBaggage(ambientKey, "updated");
            if (nonFiniteHeader)
                message.Header.Bag["measurement"] = double.NaN;
            var previousTraceParent = message.Header.TraceParent;
            using var activity = new Activity("retry baggage").SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.TraceStateString = "vendor=state";

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
            await Assert.That(message.Header.Baggage.ToString()).IsEqualTo(originalBaggage.ToString());
            var retryBaggage = Paramore.Brighter.Observability.Baggage.FromString(
                (await Assert.That(retry.ApplicationProperties["cloudevents:baggage"]).IsTypeOf<string>()));
            await Assert.That(retryBaggage.Count()).IsEqualTo(32);
            foreach (var pair in retryBaggage)
            {
                await Assert.That(pair.Value).IsEqualTo(ambientKey == "key0" && pair.Key == "key0" ? "updated" : "original");
            }
            await Assert.That(message.Header.TraceParent).IsEqualTo(previousTraceParent);
            await Assert.That(message.Id.Value).IsEqualTo(original.MessageId);
            await Assert.That(retry.ApplicationProperties["cloudevents:traceparent"]).IsEqualTo(activity.Id);
            await Assert.That(retry.ApplicationProperties["cloudevents:tracestate"]).IsEqualTo("vendor=state");
            if (nonFiniteHeader)
                await Assert.That(double.IsNaN((await Assert.That(retry.ApplicationProperties["measurement"]).IsTypeOf<double>()))).IsTrue();
            else
                await Assert.That(System.Linq.Enumerable.Any(activity.Events, e => e.Tags.Any(t =>
                    t.Key == BrighterSemanticConventions.MessagingSystem && Equals(t.Value, "azure_service_bus")))).IsTrue();
            await receiver.CompleteMessageAsync(retry);
        }
        finally
        {
            OpenTelemetry.Baggage.Current = previousBaggage;
            await administration.DeleteQueueAsync(queue);
        }
    }
}
