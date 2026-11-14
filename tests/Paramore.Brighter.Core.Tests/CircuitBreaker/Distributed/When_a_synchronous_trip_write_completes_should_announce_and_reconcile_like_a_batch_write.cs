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
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Observability;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class SynchronousTripWriteCompletionTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown };
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(150);
    private readonly RoutingKey _orders = new("orders");

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

    private static async Task WaitForSpansAsync(ActivityRecorder recorder, int count)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while (recorder.SpanNames().Length < count && DateTime.UtcNow < deadline)
            await Task.Delay(5);
    }

    private string Span(CircuitBreakerSpanOperation operation) => $"{_orders} {operation.ToSpanName()}";

    [Fact]
    public async Task When_a_synchronous_trip_write_completes_should_announce_and_reconcile_like_a_batch_write()
    {
        // Arrange
        using var recorder = new ActivityRecorder("sync-trip-announce-tracer");
        var store = new SignallingTrippedTopicStore(new InMemoryTrippedTopicStore(), WriteHold.AllWrites);
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: new FakeTimeProvider(s_start));

        // Act
        breaker.TripTopic(_orders);
        await store.WriteStarted;
        var beforeCompletion = recorder.SpanNames();
        store.ReleaseWrites();
        await WaitForSpansAsync(recorder, 1);

        // Assert
        Assert.Empty(beforeCompletion);
        Assert.Equal([Span(CircuitBreakerSpanOperation.Trip)], recorder.SpanNames());
        Assert.Equal([_orders], breaker.TrippedTopics);
    }

    [Fact]
    public async Task When_a_synchronous_trips_earlier_stamp_lands_after_a_later_stamp_should_announce_a_re_trip_and_take_the_stored_stamp()
    {
        // Arrange
        using var recorder = new ActivityRecorder("sync-trip-re-trip-tracer");
        var inner = new InMemoryTrippedTopicStore();
        await inner.WriteAsync([new TopicTrip(_orders, s_start + TimeSpan.FromSeconds(30))]);
        var store = new SignallingTrippedTopicStore(inner);
        var clock = new FakeTimeProvider(s_start + TimeSpan.FromSeconds(10));
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);

        // Act
        breaker.TripTopic(_orders);
        await WaitForSpansAsync(recorder, 1);

        // Assert
        Assert.Equal([Span(CircuitBreakerSpanOperation.ReTrip)], recorder.SpanNames());
        clock.SetUtcNow(s_start + TimeSpan.FromSeconds(79.9));
        Assert.Equal([_orders], breaker.TrippedTopics);
        clock.SetUtcNow(s_start + TimeSpan.FromSeconds(80));
        Assert.Empty(breaker.TrippedTopics);
    }

    [Fact]
    public async Task When_a_pending_synchronous_write_has_run_out_before_it_is_issued_should_not_write_and_announce_nothing()
    {
        // Arrange
        using var recorder = new ActivityRecorder("sync-trip-run-out-tracer");
        var store = new SignallingTrippedTopicStore(hold: WriteHold.FirstWriteOnly);
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);
        breaker.TripTopic(_orders);
        await store.WriteStarted;
        breaker.TripTopic(_orders);
        clock.Advance(s_cooldown);

        // Act
        store.ReleaseWrites();
        var secondWriteIssued = await Task.WhenAny(store.SecondWriteStarted, Task.Delay(s_settle)) == store.SecondWriteStarted;

        // Assert
        Assert.False(secondWriteIssued);
        Assert.Single(store.WrittenTrips);
        Assert.Empty(recorder.SpanNames());
        Assert.Empty(breaker.TrippedTopics);
    }

    [Fact]
    public async Task When_a_synchronous_write_lands_over_an_ended_unmarked_row_should_announce_reset_then_trip()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            using var recorder = new ActivityRecorder("sync-trip-reset-then-trip-tracer");
            var inner = new InMemoryTrippedTopicStore();
            await inner.WriteAsync([new TopicTrip(_orders, s_start)]);
            var store = new SignallingTrippedTopicStore(inner);
            var clock = new FakeTimeProvider(s_start + TimeSpan.FromSeconds(60));
            var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);

            // Act
            breaker.TripTopic(_orders);
            await WaitForSpansAsync(recorder, 2);

            // Assert
            Assert.Equal([Span(CircuitBreakerSpanOperation.Reset), Span(CircuitBreakerSpanOperation.Trip)], recorder.SpanNames());
            var messages = TestCorrelator.GetLogEventsFromCurrentContext().Select(e => e.MessageTemplate.Text).ToList();
            Assert.Equal(2, messages.Count);
            Assert.StartsWith("Circuit breaker reset for topic {Topic}", messages[0]);
            Assert.StartsWith("Circuit breaker tripped for topic {Topic}", messages[1]);
            Assert.Equal([_orders], breaker.TrippedTopics);
        }
    }

    [Fact]
    public async Task When_a_synchronous_write_leaves_the_row_ended_should_announce_nothing_and_drop_the_topic_from_the_view()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            using var recorder = new ActivityRecorder("sync-trip-left-ended-tracer");
            var inner = new InMemoryTrippedTopicStore();
            await inner.WriteAsync([new TopicTrip(_orders, s_start + TimeSpan.FromSeconds(30))]);
            await inner.EndExpiredAsync(new TripWindow(s_start + TimeSpan.FromSeconds(80), s_cooldown));
            var store = new SignallingTrippedTopicStore(inner);
            var clock = new FakeTimeProvider(s_start + TimeSpan.FromSeconds(10));
            var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);

            // Act
            breaker.TripTopic(_orders);
            await store.WriteCompleted;
            await Task.Delay(s_settle);

            // Assert
            Assert.Empty(recorder.SpanNames());
            Assert.Empty(TestCorrelator.GetLogEventsFromCurrentContext());
            Assert.Empty(breaker.TrippedTopics);
        }
    }
}
