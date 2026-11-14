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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class WriteOverEndedRowTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown };
    private readonly RoutingKey _orders = new("orders");

    private static ActivityRecorder StartRecording(string service) => new(service);

    private sealed class ActivityRecorder : IDisposable
    {
        private readonly TracerProvider _provider;
        public List<Activity> Activities { get; } = new();

        public ActivityRecorder(string service)
        {
            _provider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService(service))
                .AddInMemoryExporter(Activities)
                .Build();
        }

        public string[] SpanNames()
        {
            _provider.ForceFlush();
            return Activities.Select(a => a.DisplayName).ToArray();
        }

        public void Dispose() => _provider.Dispose();
    }

    [Fact]
    public async Task When_a_later_write_lands_over_an_ended_unmarked_row_should_announce_reset_then_trip()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            using var recorder = StartRecording("reset-then-trip-tracer");
            var store = new InMemoryTrippedTopicStore();
            await store.WriteAsync([new TopicTrip(_orders, s_start)]);
            var clock = new FakeTimeProvider(s_start + TimeSpan.FromSeconds(60));
            var writer = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);
            var bystander = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);
            var batch = writer.BeginTripBatch();
            batch.TripTopic(_orders);

            // Act
            await batch.WriteAsync();

            // Assert
            Assert.Equal(
                [
                    $"{_orders} {CircuitBreakerSpanOperation.Reset.ToSpanName()}",
                    $"{_orders} {CircuitBreakerSpanOperation.Trip.ToSpanName()}"
                ],
                recorder.SpanNames());
            var messages = TestCorrelator.GetLogEventsFromCurrentContext().Select(e => e.MessageTemplate.Text).ToList();
            Assert.Equal(2, messages.Count);
            Assert.StartsWith("Circuit breaker reset for topic {Topic}", messages[0]);
            Assert.StartsWith("Circuit breaker tripped for topic {Topic}", messages[1]);
            Assert.Equal([_orders], writer.TrippedTopics);
            Assert.Empty(bystander.TrippedTopics);
        }
    }

    [Fact]
    public async Task When_a_write_leaves_the_row_ended_should_announce_nothing_and_drop_the_topic_from_the_view()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            using var recorder = StartRecording("left-ended-tracer");
            var store = new InMemoryTrippedTopicStore();
            await store.WriteAsync([new TopicTrip(_orders, s_start + TimeSpan.FromSeconds(30))]);
            await store.EndExpiredAsync(new TripWindow(s_start + TimeSpan.FromSeconds(80), s_cooldown));
            var clock = new FakeTimeProvider(s_start + TimeSpan.FromSeconds(10));
            var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);
            var batch = breaker.BeginTripBatch();
            batch.TripTopic(_orders);

            // Act
            await batch.WriteAsync();

            // Assert
            Assert.Empty(recorder.SpanNames());
            Assert.Empty(TestCorrelator.GetLogEventsFromCurrentContext());
            Assert.Empty(breaker.TrippedTopics);
        }
    }

    [Fact]
    public async Task When_the_stored_stamp_has_already_run_out_should_drop_the_topic_and_announce_nothing()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            using var recorder = StartRecording("stored-stamp-run-out-tracer");
            var clock = new FakeTimeProvider(s_start + TimeSpan.FromSeconds(60));
            var inner = new InMemoryTrippedTopicStore();
            await inner.WriteAsync([new TopicTrip(_orders, s_start)]);
            var store = new ClockAdvancingTrippedTopicStore(inner, clock, TimeSpan.FromSeconds(50));
            var breaker = new DistributedOutboxCircuitBreaker(store, s_options, new BrighterTracer(), timeProvider: clock);
            var batch = breaker.BeginTripBatch();
            batch.TripTopic(_orders);

            // Act
            await batch.WriteAsync();

            // Assert
            Assert.Empty(recorder.SpanNames());
            Assert.Empty(TestCorrelator.GetLogEventsFromCurrentContext());
            Assert.Empty(breaker.TrippedTopics);
        }
    }
}
