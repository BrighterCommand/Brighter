#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_the_store_throws_on_write_during_a_batch_should_keep_dispatching_and_keep_the_trip_unshared
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _invoices = new("invoices");

    private sealed class ActivityRecorder : IDisposable
    {
        private readonly TracerProvider _provider;
        private readonly List<Activity> _activities = new();

        public ActivityRecorder(string service)
        {
            _provider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService(service))
                .AddInMemoryExporter(_activities)
                .Build();
        }

        public string[] SpanNames()
        {
            _provider.ForceFlush();
            lock (_activities)
                return _activities.Select(a => a.DisplayName).ToArray();
        }

        public void Dispose() => _provider.Dispose();
    }

    private static async Task<bool> BecomesTrueAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        return condition();
    }

    private string Span(CircuitBreakerSpanOperation operation) => $"{_orders} {operation.ToSpanName()}";

    private static bool NamesTheFailure(LogEvent e)
        => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("InvalidOperationException");

    private Message NewMessage(RoutingKey topic)
        => new(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("{}"));

    [Fact]
    public async Task When_a_trip_batch_write_throws_should_complete_warn_and_announce_one_trip_from_the_view()
    {
        using var context = TestCorrelator.CreateContext();
        // Arrange
        using var recorder = new ActivityRecorder("batch-write-fails-trip-tracer");
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(new ThrowingOnWriteTrippedTopicStore(), s_options, new BrighterTracer(), timeProvider: clock);
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);

        // Act
        await batch.WriteAsync();
        clock.Advance(TimeSpan.FromSeconds(20));
        var listedUntilCooldown = await breaker.ReadTrippedTopicsAsync();
        var statusUntilCooldown = breaker.StoreStatus;
        clock.Advance(s_cooldown);
        var listedAfterCooldown = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal([Span(CircuitBreakerSpanOperation.Trip), Span(CircuitBreakerSpanOperation.Reset)], recorder.SpanNames());
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), NamesTheFailure);
        Assert.True(statusUntilCooldown.IsUsingFallback);
        Assert.Equal("InvalidOperationException", statusUntilCooldown.LastFailure);
        Assert.Equal([_orders], listedUntilCooldown.ToArray());
        Assert.Empty(listedAfterCooldown);
    }

    [Fact]
    public async Task When_a_trip_batch_write_throws_over_a_live_trip_should_announce_one_re_trip()
    {
        // Arrange
        using var recorder = new ActivityRecorder("batch-write-fails-re-trip-tracer");
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(new ThrowingOnWriteTrippedTopicStore(), s_options, new BrighterTracer(), timeProvider: clock);
        var first = breaker.BeginTripBatch();
        first.TripTopic(_orders);
        await first.WriteAsync();
        clock.Advance(TimeSpan.FromSeconds(10));
        var second = breaker.BeginTripBatch();
        second.TripTopic(_orders);

        // Act
        await second.WriteAsync();

        // Assert
        Assert.Equal([Span(CircuitBreakerSpanOperation.Trip), Span(CircuitBreakerSpanOperation.ReTrip)], recorder.SpanNames());
    }

    [Fact]
    public async Task When_a_synchronous_trip_write_throws_a_later_trip_should_start_a_new_write()
    {
        // Arrange
        var store = new ThrowingOnWriteTrippedTopicStore();
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        breaker.TripTopic(_orders);
        await BecomesTrueAsync(() => store.WriteCount == 1 && breaker.StoreStatus.IsUsingFallback);

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        breaker.TripTopic(_orders);
        var writtenAgain = await BecomesTrueAsync(() => store.WriteCount == 2);

        // Assert
        Assert.True(writtenAgain);
        Assert.Equal("InvalidOperationException", breaker.StoreStatus.LastFailure);
    }

    [Fact]
    public async Task When_the_orders_send_fails_and_the_store_throws_on_write_should_send_both_invoices_and_announce_one_trip()
    {
        using var context = TestCorrelator.CreateContext();
        // Arrange
        using var recorder = new ActivityRecorder("mediator-write-fails-tracer");
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(new ThrowingOnWriteTrippedTopicStore(), s_options, new BrighterTracer(), timeProvider: clock);
        var outbox = new InMemoryOutbox(clock);
        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { [_orders] = new TopicFailingProducerAsync(_orders, fails: true), [_invoices] = new TopicFailingProducerAsync(_invoices, fails: false) }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            timeProvider: clock,
            outboxCircuitBreaker: breaker);
        var context1 = new RequestContext();
        var ordersMessage = NewMessage(_orders);
        var firstInvoice = NewMessage(_invoices);
        var secondInvoice = NewMessage(_invoices);
        foreach (var message in new[] { ordersMessage, firstInvoice, secondInvoice })
            await mediator.AddToOutboxAsync(message, context1);

        // Act
        await mediator.ClearOutboxAsync([ordersMessage.Id, firstInvoice.Id, secondInvoice.Id], context1);
        var announced = await BecomesTrueAsync(() => recorder.SpanNames().Length > 0);
        var spans = recorder.SpanNames();
        var listed = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.True(announced);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), NamesTheFailure);
        Assert.Equal([Span(CircuitBreakerSpanOperation.Trip)], spans);
        Assert.Equal([ordersMessage.Id], outbox.OutstandingMessages(TimeSpan.Zero, context1).Select(m => m.Id).ToArray());
        Assert.Equal(
            new[] { firstInvoice.Id, secondInvoice.Id }.OrderBy(id => id.Value),
            outbox.DispatchedMessages(TimeSpan.Zero, context1).Select(m => m.Id).OrderBy(id => id.Value));
        Assert.Equal([_orders], listed.ToArray());
    }
}
