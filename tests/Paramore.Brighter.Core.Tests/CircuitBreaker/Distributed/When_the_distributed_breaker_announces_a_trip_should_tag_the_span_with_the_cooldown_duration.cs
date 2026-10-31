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
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerCooldownDurationSpanTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);
    private readonly RoutingKey _orders = new("orders");

    private static async Task<Activity> WaitForSpanAsync(TracerProvider provider, List<Activity> activities)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while (DateTime.UtcNow < deadline)
        {
            provider.ForceFlush();
            lock (activities)
            {
                if (activities.Count >= 1)
                    return activities[0];
            }
            await Task.Delay(5);
        }

        throw new TimeoutException("No circuit breaker span was exported");
    }

    [Fact]
    public async Task When_the_distributed_breaker_announces_a_trip_should_tag_the_span_with_the_cooldown_duration()
    {
        // Arrange
        var activities = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("cooldown-duration-tracer"))
            .AddInMemoryExporter(activities)
            .Build();
        var breaker = new DistributedOutboxCircuitBreaker(
            new InMemoryTrippedTopicStore(),
            new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown },
            new BrighterTracer(),
            timeProvider: new FakeTimeProvider(s_start));

        // Act
        breaker.TripTopic(_orders);
        var span = await WaitForSpanAsync(provider, activities);

        // Assert
        Assert.Equal($"{_orders} {CircuitBreakerSpanOperation.Trip.ToSpanName()}", span.DisplayName);
        Assert.Contains(span.TagObjects, t =>
            t.Key == BrighterSemanticConventions.CircuitBreakerCooldownDuration && (double)t.Value! == s_cooldown.TotalSeconds);
        Assert.Contains(span.TagObjects, t =>
            t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 0);
    }

    [Fact]
    public void When_an_in_memory_breaker_trips_should_not_tag_the_span_with_a_cooldown_duration()
    {
        // Arrange
        var activities = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-cooldown-duration-tracer"))
            .AddInMemoryExporter(activities)
            .Build();
        var breaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 4 }, new BrighterTracer());

        // Act
        breaker.TripTopic(_orders);
        provider.ForceFlush();

        // Assert
        var span = Assert.Single(activities);
        Assert.DoesNotContain(span.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownDuration);
        Assert.Contains(span.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 4);
    }

    [Fact]
    public async Task When_the_distributed_breaker_announces_a_trip_should_keep_the_trips_counter_attributes_unchanged()
    {
        // Arrange
        var activities = new List<Activity>();
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = services.GetRequiredService<IMeterFactory>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("trips-counter-meter"))
            .Build();
        var messagingMeter = new MessagingMeter(meterFactory, meterProvider);
        var tracer = new BrighterTracer();
        var recordedTags = new List<string[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, meterFactory) && instrument.Name == "paramore.brighter.circuit_breaker.trips")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((_, _, tags, _) =>
        {
            var keys = tags.ToArray().Select(t => t.Key).ToArray();
            lock (recordedTags)
                recordedTags.Add(keys);
        });
        listener.Start();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("trips-counter-tracer"))
            .AddProcessor(new BrighterMetricsFromTracesProcessor(tracer, new DisabledDbMeter(), messagingMeter))
            .AddInMemoryExporter(activities)
            .Build();
        var breaker = new DistributedOutboxCircuitBreaker(
            new InMemoryTrippedTopicStore(),
            new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown },
            tracer,
            timeProvider: new FakeTimeProvider(s_start));

        // Act
        breaker.TripTopic(_orders);
        await WaitForSpanAsync(provider, activities);

        // Assert
        string[] keys;
        lock (recordedTags)
            keys = Assert.Single(recordedTags);
        Assert.Contains(BrighterSemanticConventions.Operation, keys);
        Assert.Contains(BrighterSemanticConventions.CircuitBreakerTopic, keys);
        Assert.DoesNotContain(BrighterSemanticConventions.CircuitBreakerCooldownDuration, keys);
        Assert.DoesNotContain(BrighterSemanticConventions.CircuitBreakerCooldownCount, keys);
    }
}
