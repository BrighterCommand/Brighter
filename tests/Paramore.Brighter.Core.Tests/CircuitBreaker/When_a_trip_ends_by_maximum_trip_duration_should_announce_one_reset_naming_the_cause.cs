using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryResetAnnouncementTests
{
    private const string RESET_TEMPLATE_PREFIX = "Circuit breaker reset for topic {Topic}";

    [Test]
    public async Task When_a_trip_ends_by_maximum_trip_duration_should_announce_one_reset_naming_the_cause()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var exportedActivities = new List<Activity>();
            var meter = new SpyMessagingMeter();
            var tracer = new BrighterTracer();
            using var traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddProcessor(new BrighterMetricsFromTracesProcessor(tracer, new DisabledDbMeter(), meter))
                .AddInMemoryExporter(exportedActivities)
                .Build();

            var orders = new RoutingKey("orders");
            var clock = new FakeTimeProvider();
            var breaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions { CooldownCount = 10, MaximumTripDuration = TimeSpan.FromMinutes(5) },
                tracer,
                timeProvider: clock);
            breaker.TripTopic(orders);
            var spansBeforeReset = CircuitBreakerSpans(exportedActivities, traceProvider).Count;
            var incrementsBeforeReset = meter.AddCircuitBreakerEventCallCount;

            // Act
            clock.Advance(TimeSpan.FromMinutes(5));
            _ = breaker.TrippedTopics.ToArray();

            // Assert
            await Assert.That(ResetLogs()).HasSingleItem();
            await Assert.That(ResetSpans(exportedActivities, traceProvider, orders).Count).IsEqualTo(1);
            await Assert.That(meter.AddCircuitBreakerEventCallCount).IsEqualTo(incrementsBeforeReset + 1);
            await Assert.That(CircuitBreakerSpans(exportedActivities, traceProvider).Count).IsEqualTo(spansBeforeReset + 1);

            _ = breaker.TrippedTopics.ToArray();
            _ = breaker.TrippedTopics.ToArray();
            breaker.CoolDown();

            await Assert.That(ResetLogs()).HasSingleItem();
            await Assert.That(ResetSpans(exportedActivities, traceProvider, orders).Count).IsEqualTo(1);
            await Assert.That(meter.AddCircuitBreakerEventCallCount).IsEqualTo(incrementsBeforeReset + 1);
            await Assert.That(ResetLogs().Single().Properties["ResetCause"].ToString()).IsEqualTo("\"maximum trip duration\"");
        }
    }

    [Test]
    public async Task When_a_trip_ends_by_cooldown_should_announce_one_reset_naming_the_cause()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var exportedActivities = new List<Activity>();
            var tracer = new BrighterTracer();
            using var traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(exportedActivities)
                .Build();

            var orders = new RoutingKey("orders");
            var breaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions { CooldownCount = 1, MaximumTripDuration = TimeSpan.FromMinutes(5) },
                tracer,
                timeProvider: new FakeTimeProvider());
            breaker.TripTopic(orders);

            // Act
            breaker.CoolDown();
            breaker.CoolDown();

            // Assert
            await Assert.That(ResetLogs()).HasSingleItem();
            await Assert.That(ResetSpans(exportedActivities, traceProvider, orders).Count).IsEqualTo(1);
            await Assert.That(ResetLogs().Single().Properties["ResetCause"].ToString()).IsEqualTo("\"cooldown\"");
        }
    }

    private static List<LogEvent> ResetLogs() =>
        TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Level == LogEventLevel.Information && e.MessageTemplate.Text.StartsWith(RESET_TEMPLATE_PREFIX))
            .ToList();

    private static List<Activity> ResetSpans(List<Activity> exported, TracerProvider provider, RoutingKey topic)
    {
        provider.ForceFlush();
        return exported
            .Where(a => a.DisplayName == $"{topic} {CircuitBreakerSpanOperation.Reset.ToSpanName()}")
            .ToList();
    }

    private static List<Activity> CircuitBreakerSpans(List<Activity> exported, TracerProvider provider)
    {
        provider.ForceFlush();
        return exported.Where(a => a.Tags.Any(t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic)).ToList();
    }
}
