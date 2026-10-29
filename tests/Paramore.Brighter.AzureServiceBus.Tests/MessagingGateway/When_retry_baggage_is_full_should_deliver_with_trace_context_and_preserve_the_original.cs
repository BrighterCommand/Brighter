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
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "AzureServiceBus")]
[Collection("AzureServiceBus")]
public class AzureServiceBusRetryBaggageTests
{
    [Theory]
    [InlineData(false, "key0", false)]
    [InlineData(true, "key0", false)]
    [InlineData(false, "extra", false)]
    [InlineData(true, "extra", false)]
    [InlineData(false, "tenant.name", false)]
    [InlineData(true, "tenant.name", false)]
    [InlineData(false, "key0", true)]
    [InlineData(true, "key0", true)]
    [InlineData(false, "extra", true)]
    [InlineData(true, "extra", true)]
    [InlineData(false, "tenant.name", true)]
    [InlineData(true, "tenant.name", true)]
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
            Assert.Equal(original.MessageId, message.Id.Value);
            message.Header.UpdateHandledCount();
            Assert.Equal(32, message.Header.Baggage.Count());
            OpenTelemetry.Baggage.Current = default;
            OpenTelemetry.Baggage.SetBaggage(ambientKey, "updated");
            if (nonFiniteHeader)
                message.Header.Bag["measurement"] = double.NaN;
            var previousTraceParent = message.Header.TraceParent;
            using var activity = new Activity("retry baggage").SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.TraceStateString = "vendor=state";

            // Act
            Assert.True(synchronous ? syncChannel!.Requeue(message, TimeSpan.Zero)
                : await asyncChannel!.RequeueAsync(message, TimeSpan.Zero));
            await using var receiver = client.CreateReceiver(queue);
            var retry = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(retry);
            Assert.NotEqual(original.MessageId, retry.MessageId);
            Assert.Equal(original.MessageId, retry.ApplicationProperties[Message.OriginalMessageIdHeaderName]);
            Assert.Equal(original.Body.ToArray(), retry.Body.ToArray());
            Assert.Equal(originalBaggage.ToString(), message.Header.Baggage.ToString());
            var retryBaggage = Paramore.Brighter.Observability.Baggage.FromString(
                Assert.IsType<string>(retry.ApplicationProperties["cloudevents:baggage"]));
            Assert.Equal(32, retryBaggage.Count());
            Assert.All(retryBaggage, pair => Assert.Equal(
                ambientKey == "key0" && pair.Key == "key0" ? "updated" : "original", pair.Value));
            Assert.Equal(previousTraceParent, message.Header.TraceParent);
            Assert.Equal(original.MessageId, message.Id.Value);
            Assert.Equal(activity.Id, retry.ApplicationProperties["cloudevents:traceparent"]);
            Assert.Equal("vendor=state", retry.ApplicationProperties["cloudevents:tracestate"]);
            if (nonFiniteHeader)
                Assert.True(double.IsNaN(Assert.IsType<double>(retry.ApplicationProperties["measurement"])));
            else
                Assert.Contains(activity.Events, e => e.Tags.Any(t =>
                    t.Key == BrighterSemanticConventions.MessagingSystem && Equals(t.Value, "azure_service_bus")));
            await receiver.CompleteMessageAsync(retry);
        }
        finally
        {
            OpenTelemetry.Baggage.Current = previousBaggage;
            await administration.DeleteQueueAsync(queue);
        }
    }
}
