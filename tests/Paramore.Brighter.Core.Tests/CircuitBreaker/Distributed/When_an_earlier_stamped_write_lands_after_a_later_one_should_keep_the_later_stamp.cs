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
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class OutOfOrderStampedWriteTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private readonly RoutingKey _orders = new("orders");

    private sealed class Scenario
    {
        public InMemoryTrippedTopicStore Store { get; } = new();
        public FakeTimeProvider ClockA { get; } = new(s_start);
        public FakeTimeProvider ClockB { get; } = new(s_start);
        public DistributedOutboxCircuitBreaker BreakerA { get; }
        public DistributedOutboxCircuitBreaker BreakerB { get; }
        public IAmATripBatch BatchA { get; }
        public IAmATripBatch BatchB { get; }

        public Scenario(RoutingKey topic, IAmABrighterTracer? tracer = null)
        {
            var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown };
            BreakerA = new DistributedOutboxCircuitBreaker(Store, options, tracer, timeProvider: ClockA);
            BreakerB = new DistributedOutboxCircuitBreaker(Store, options, tracer, timeProvider: ClockB);
            BatchA = BreakerA.BeginTripBatch();
            BatchB = BreakerB.BeginTripBatch();
            ClockA.Advance(TimeSpan.FromSeconds(10));
            BatchA.TripTopic(topic);
            ClockB.Advance(TimeSpan.FromSeconds(30));
            BatchB.TripTopic(topic);
        }
    }

    [Fact]
    public async Task When_an_earlier_stamped_write_lands_after_a_later_one_should_keep_the_later_stamp_and_move_the_writers_view()
    {
        // Arrange
        var scenario = new Scenario(_orders);
        await scenario.BatchB.WriteAsync();

        // Act
        await scenario.BatchA.WriteAsync();

        // Assert
        scenario.ClockA.SetUtcNow(s_start + TimeSpan.FromSeconds(79.9));
        scenario.ClockB.SetUtcNow(s_start + TimeSpan.FromSeconds(79.9));
        var stored = await scenario.Store.ReadLiveAsync(new TripWindow(s_start + TimeSpan.FromSeconds(79.9), s_cooldown));
        Assert.Equal([new TopicTrip(_orders, s_start + TimeSpan.FromSeconds(30))], stored);
        Assert.Equal([_orders], scenario.BreakerA.TrippedTopics);
        Assert.Equal([_orders], scenario.BreakerB.TrippedTopics);

        scenario.ClockA.SetUtcNow(s_start + TimeSpan.FromSeconds(80));
        scenario.ClockB.SetUtcNow(s_start + TimeSpan.FromSeconds(80));
        Assert.Empty(scenario.BreakerA.TrippedTopics);
        Assert.Empty(scenario.BreakerB.TrippedTopics);
    }

    [Fact]
    public async Task When_an_earlier_stamped_write_lands_after_a_later_one_should_keep_the_later_stamp_and_export_a_re_trip_span_for_it_and_a_trip_span_for_the_later()
    {
        // Arrange
        var exportedActivities = new List<Activity>();
        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("out-of-order-write-tracer"))
            .AddInMemoryExporter(exportedActivities)
            .Build();
        var scenario = new Scenario(_orders, new BrighterTracer());

        // Act
        await scenario.BatchB.WriteAsync();
        traceProvider.ForceFlush();
        var afterLaterWrite = exportedActivities.Select(a => a.DisplayName).ToArray();
        exportedActivities.Clear();
        await scenario.BatchA.WriteAsync();
        traceProvider.ForceFlush();
        var afterEarlierWrite = exportedActivities.Select(a => a.DisplayName).ToArray();

        // Assert
        Assert.Equal([$"{_orders} {CircuitBreakerSpanOperation.Trip.ToSpanName()}"], afterLaterWrite);
        Assert.Equal([$"{_orders} {CircuitBreakerSpanOperation.ReTrip.ToSpanName()}"], afterEarlierWrite);
    }

    [Fact]
    public async Task When_an_earlier_stamped_write_lands_after_a_later_one_should_keep_the_later_stamp_and_log_a_trip_then_a_re_trip()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var scenario = new Scenario(_orders);

            // Act
            await scenario.BatchB.WriteAsync();
            await scenario.BatchA.WriteAsync();

            // Assert
            var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
            Assert.Single(logEvents, e => e.Level == LogEventLevel.Warning &&
                e.MessageTemplate.Text.StartsWith("Circuit breaker tripped for topic {Topic}") &&
                e.Properties["Topic"].ToString() == "\"orders\"");
            Assert.Single(logEvents, e => e.Level == LogEventLevel.Debug &&
                e.MessageTemplate.Text.StartsWith("Circuit breaker re-tripped for topic {Topic}") &&
                e.Properties["Topic"].ToString() == "\"orders\"");
        }
    }
}
