using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class OutboxCircuitBreakerTripSpanTests
    {
        [Fact]
        public void When_a_healthy_topic_trips_should_export_a_trip_span()
        {
            // Arrange
            var exportedActivities = new List<Activity>();
            using var traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(exportedActivities)
                .Build();

            var tracer = new BrighterTracer();
            var topic = new RoutingKey("healthy.span.topic");
            var circuitBreaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions { CooldownCount = 4 },
                tracer);

            // Act
            circuitBreaker.TripTopic(topic);
            traceProvider.ForceFlush();

            // Assert
            var span = exportedActivities.SingleOrDefault(a =>
                a.DisplayName == $"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}");
            Assert.NotNull(span);
            Assert.Contains(span!.Tags, t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic && t.Value == topic.Value);
            Assert.Contains(span.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 4);
        }
    }
}
