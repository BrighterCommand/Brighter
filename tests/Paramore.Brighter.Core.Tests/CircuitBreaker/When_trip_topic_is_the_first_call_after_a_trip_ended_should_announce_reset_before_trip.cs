using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryTripTopicObservesEndedTripTests
{
    [Fact]
    public void When_trip_topic_is_the_first_call_after_a_trip_ended_should_announce_reset_before_trip()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var (breaker, exported, provider) = BuildBreakerWithEndedTripFor(orders);
        using var _ = provider;

        // Act
        breaker.TripTopic(orders);

        // Assert
        provider.ForceFlush();
        Assert.Equal(
            [SpanName(orders, CircuitBreakerSpanOperation.Reset), SpanName(orders, CircuitBreakerSpanOperation.Trip)],
            SpansAfterTheFirstTrip(exported));
        Assert.Contains(orders, breaker.TrippedTopics);
    }

    [Fact]
    public void When_trip_topic_for_another_topic_is_the_first_call_after_a_trip_ended_should_announce_the_reset_before_the_trip()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var invoices = new RoutingKey("invoices");
        var (breaker, exported, provider) = BuildBreakerWithEndedTripFor(orders);
        using var _ = provider;

        // Act
        breaker.TripTopic(invoices);

        // Assert
        provider.ForceFlush();
        Assert.Equal(
            [SpanName(orders, CircuitBreakerSpanOperation.Reset), SpanName(invoices, CircuitBreakerSpanOperation.Trip)],
            SpansAfterTheFirstTrip(exported));
        Assert.Equal([invoices], breaker.TrippedTopics);
    }

    private static (InMemoryOutboxCircuitBreaker Breaker, List<Activity> Exported, TracerProvider Provider)
        BuildBreakerWithEndedTripFor(RoutingKey topic)
    {
        var exported = new List<Activity>();
        var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(exported)
            .Build();
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { CooldownCount = 10, MaximumTripDuration = TimeSpan.FromMinutes(5) },
            new BrighterTracer(),
            timeProvider: clock);
        breaker.TripTopic(topic);
        clock.Advance(TimeSpan.FromMinutes(5));
        return (breaker, exported, provider);
    }

    private static string SpanName(RoutingKey topic, CircuitBreakerSpanOperation operation) =>
        $"{topic} {operation.ToSpanName()}";

    private static List<string> SpansAfterTheFirstTrip(List<Activity> exported) =>
        exported.Skip(1).Select(a => a.DisplayName).ToList();
}
