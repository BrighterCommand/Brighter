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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Azure;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[NotInParallel("AzureServiceBus")]
public class AzureServiceBusRetryInstrumentationOptionsTests
{
    public static List<(bool, bool, InstrumentationOptions)> Options => new()
    {
(false,false,InstrumentationOptions.None ),(true,false,InstrumentationOptions.None ),(false,true,InstrumentationOptions.None ),(true,true,InstrumentationOptions.None ),(false,false,InstrumentationOptions.RequestInformation ),(true,true,InstrumentationOptions.RequestInformation ),(false,false,InstrumentationOptions.Messaging ),(true,true,InstrumentationOptions.Messaging ),(false,false,InstrumentationOptions.All ),(true,true,InstrumentationOptions.All )    };

    [Test]
    [MethodDataSource(nameof(Options))]
    public async Task When_retrying_should_respect_instrumentation_options(
        bool synchronous, bool global, InstrumentationOptions options)
    {
        // Arrange
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
        var delivery = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData("order"), messageId: "original", lockTokenGuid: Guid.NewGuid(),
            lockedUntil: DateTimeOffset.UtcNow.AddMinutes(1),
            properties: new Dictionary<string, object>
            {
                ["MessageType"] = "MT_COMMAND",
                ["cloudEvents:id"] = "business-event",
                ["private-header"] = "sensitive-value"
            });
        await using var client = new InMemoryRetryServiceBusClient(delivery);
        var publication = new AzureServiceBusPublication
        {
            RequeueScheduler = global ? RequeueSchedulerMode.Global : RequeueSchedulerMode.Native
        };
        var factory = new AzureServiceBusChannelFactory(
            new AzureServiceBusConsumerFactory(client, publication, options));
        if (global)
        {
            var processor = new CommandProcessor(new SubscriberRegistry(),
                new SimpleHandlerFactoryAsync(_ => throw new InvalidOperationException()),
                new InMemoryRequestContextFactory(), new DefaultPolicy(),
                new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            factory.Scheduler = new AzureServiceBusSchedulerFactory(client, new RoutingKey("scheduler"))
            {
                InstrumentationOptions = options
            }.Create(processor);
        }
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("retry"),
            new ChannelName("retry-queue"), new RoutingKey("retry-queue"),
            requestType: typeof(Command), timeOut: TimeSpan.FromSeconds(1),
            messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
        using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
        await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
        var message = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(1))
            : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(1));
        message.Header.UpdateHandledCount();
        var previousTraceParent = message.Header.TraceParent;
        using var activity = new Activity("retry options").SetIdFormat(ActivityIdFormat.W3C).Start();

        // Act
        await Assert.That(synchronous ? syncChannel!.Requeue(message, TimeSpan.Zero)
            : await asyncChannel!.RequeueAsync(message, TimeSpan.Zero)).IsTrue();

        // Assert
        var retry = (await Assert.That(client.Scheduled).HasSingleItem()).Message;
        var tags = activity.Events.SelectMany(e => e.Tags).ToArray();
        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            await Assert.That(System.Linq.Enumerable.Any(tags, tag => tag.Key == BrighterSemanticConventions.MessageHeaders)).IsTrue();
            await Assert.That(System.Linq.Enumerable.Any(tags, tag => tag.Key == BrighterSemanticConventions.MessageId
                && Equals(tag.Value, retry.MessageId))).IsTrue();
        }
        else
        {
            await Assert.That(System.Linq.Enumerable.Any(tags, tag => tag.Key == BrighterSemanticConventions.MessageHeaders)).IsFalse();
            await Assert.That(System.Linq.Enumerable.Any(tags, tag => tag.Value?.ToString()?.Contains("sensitive-value") == true)).IsFalse();
        }
        await Assert.That(tags.Any(tag => tag.Key == BrighterSemanticConventions.MessageBody)).IsEqualTo(options.HasFlag(InstrumentationOptions.RequestBody));
        if (options.HasFlag(InstrumentationOptions.RequestInformation))
            await Assert.That(System.Linq.Enumerable.Any(tags, tag => tag.Key == BrighterSemanticConventions.CeMessageId
                && Equals(tag.Value, "business-event"))).IsTrue();
        await Assert.That(retry.ApplicationProperties["cloudEvents:id"]).IsEqualTo("business-event");
        await Assert.That(retry.ApplicationProperties["cloudevents:traceparent"]).IsEqualTo(activity.Id);
        await Assert.That(message.Id.Value).IsEqualTo("original");
        await Assert.That(message.Header.TraceParent).IsEqualTo(previousTraceParent);
        await Assert.That(client.Receiver.CompletionAttempts).IsEqualTo(1);
    }
}
