using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;


namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class OutboxCircuitBreakerReTripSpanTests
    {
        [Test]
        public async System.Threading.Tasks.Task When_an_already_tripped_topic_fails_again_should_export_a_re_trip_span()
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
            await Assert.That(reTripSpan).IsNotNull();
            await Assert.That(reTripSpan!.Tags).Contains(t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic && t.Value == topic.Value);
            await Assert.That(reTripSpan.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 4);
        }
    }
}
