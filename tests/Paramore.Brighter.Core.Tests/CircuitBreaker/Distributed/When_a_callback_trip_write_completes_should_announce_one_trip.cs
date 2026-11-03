#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class ConfirmationCallbackTripWriteCompletionTests
{
    [Fact]
    public async Task When_a_confirmation_callback_trip_write_completes_should_announce_one_trip()
    {
        // Arrange
        var topic = new RoutingKey("orders");
        var activities = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("callback-trip-tracer"))
            .AddInMemoryExporter(activities)
            .Build();
        var store = new SignallingTrippedTopicStore(new InMemoryTrippedTopicStore(), WriteHold.AllWrites);
        var breaker = new DistributedOutboxCircuitBreaker(
            store,
            new OutboxCircuitBreakerOptions { CooldownDuration = TimeSpan.FromSeconds(50) },
            new BrighterTracer(),
            timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = topic })
        {
            UseAsyncPublishConfirmation = true,
            PublishFailurePredicate = _ => true
        };
        _ = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { [topic] = producer }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outboxCircuitBreaker: breaker);
        var message = new Message(
            new MessageHeader(new Id(Guid.NewGuid().ToString()), topic, MessageType.MT_EVENT),
            new MessageBody("test"));

        // Act
        await producer.SendAsync(message);
        await producer.DisposeAsync();
        await store.WriteStarted;
        provider.ForceFlush();
        var beforeCompletion = activities.Select(a => a.DisplayName).ToArray();
        store.ReleaseWrites();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            provider.ForceFlush();
            if (activities.Count >= 1)
                break;
            await Task.Delay(5);
        }

        // Assert
        Assert.Empty(beforeCompletion);
        Assert.Equal([$"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}"], activities.Select(a => a.DisplayName).ToArray());
    }
}
