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
    public class OutboxCircuitBreakerReTripSpanTests
    {
        [Fact]
        public void When_an_already_tripped_topic_fails_again_should_export_a_re_trip_span()
        {
            // Arrange
            var exportedActivities = new List<Activity>();
            using var traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(exportedActivities)
                .Build();

            var tracer = new BrighterTracer();
            var topic = new RoutingKey("already.tripped.span.topic");
            var circuitBreaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions { CooldownCount = 4 },
                tracer);
            circuitBreaker.TripTopic(topic); // fresh trip

            // Act
            circuitBreaker.TripTopic(topic); // re-trip
            traceProvider.ForceFlush();

            // Assert
            var reTripSpan = exportedActivities.SingleOrDefault(a =>
                a.DisplayName == $"{topic} {CircuitBreakerSpanOperation.ReTrip.ToSpanName()}");
            Assert.NotNull(reTripSpan);
            Assert.Contains(reTripSpan!.Tags, t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic && t.Value == topic.Value);
            Assert.Contains(reTripSpan.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 4);
        }
    }
}
