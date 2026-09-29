using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;


namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class OutboxCircuitBreakerResetTests
    {
        [Test]
        public async System.Threading.Tasks.Task When_a_tripped_topic_cools_down_should_log_information_and_export_a_reset_span()
        {
            using (TestCorrelator.CreateContext())
            {
                // Arrange
                var exportedActivities = new List<Activity>();
                using var traceProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource("Paramore.Brighter")
                    .ConfigureResource(r => r.AddService("in-memory-tracer"))
                    .AddInMemoryExporter(exportedActivities)
                    .Build();

                var tracer = new BrighterTracer();
                var topic = new RoutingKey("cooling.down.topic");
                var circuitBreaker = new InMemoryOutboxCircuitBreaker(
                    new OutboxCircuitBreakerOptions { CooldownCount = 1 },
                    tracer);
                circuitBreaker.TripTopic(topic);

                // Act: a topic tripped with CooldownCount = 1 needs two cooldown cycles to evict
                // (the first decrements 1 -> 0, the second decrements 0 -> -1, which triggers eviction)
                circuitBreaker.CoolDown();
                circuitBreaker.CoolDown();
                traceProvider.ForceFlush();

                // Assert: the topic is healthy again
                await Assert.That(circuitBreaker.TrippedTopics).DoesNotContain(topic);

                // Assert: Information log for the reset
                var logEvents = TestCorrelator.GetLogEventsFromCurrentContext();
                await Assert.That(logEvents).Contains(e =>
                    e.Level == LogEventLevel.Information &&
                    e.MessageTemplate.Text == "Circuit breaker reset for topic {Topic}; publish suppression lifted" &&
                    e.Properties["Topic"].ToString() == "\"cooling.down.topic\"");

                // Assert: Reset span exported
                var resetSpan = exportedActivities.SingleOrDefault(a =>
                    a.DisplayName == $"{topic} {CircuitBreakerSpanOperation.Reset.ToSpanName()}");
                await Assert.That(resetSpan).IsNotNull();
                await Assert.That(resetSpan!.Tags).Contains(t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic && t.Value == topic.Value);
            }
        }
    }
}
