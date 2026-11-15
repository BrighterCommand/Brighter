using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;


namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterSemanticConventionsCircuitBreakerSpanTests
{
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer;

    public BrighterSemanticConventionsCircuitBreakerSpanTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        _tracer = new BrighterTracer();
    }

    [Test]
    [Arguments(InstrumentationOptions.All)]
    [Arguments(InstrumentationOptions.RequestInformation)]
    [Arguments(InstrumentationOptions.CircuitBreaker)]
    [Arguments(InstrumentationOptions.None)]
    public async System.Threading.Tasks.Task When_Creating_A_Circuit_Breaker_Span_Add_Brighter_Semantic_Conventions(InstrumentationOptions options)
    {
        // Arrange
        var topic = new RoutingKey("payment.events");

        // Act
        var activity = _tracer.CreateCircuitBreakerSpan(
            new CircuitBreakerSpanInfo(CircuitBreakerSpanOperation.Trip, topic, CooldownCount: 5),
            options: options);

        activity!.Stop();
        _traceProvider.ForceFlush();

        // Assert: span identity and domain tag always present unless None
        await Assert.That(activity.DisplayName).IsEqualTo($"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}");
        if (options == InstrumentationOptions.None)
        {
            await Assert.That(activity.Tags).IsEmpty();
        }
        else
        {
            await Assert.That(System.Linq.Enumerable.Any(activity.Tags, t =>
                t.Key == BrighterSemanticConventions.InstrumentationDomain &&
                t.Value == BrighterSemanticConventions.CircuitBreakerInstrumentationDomain)).IsTrue();
        }

        // Assert: the generic Operation tag is gated by RequestInformation, matching every other Create*Span
        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            await Assert.That(System.Linq.Enumerable.Any(activity.Tags, t => t.Key == BrighterSemanticConventions.Operation && t.Value == "trip")).IsTrue();
        }
        else
        {
            await Assert.That(System.Linq.Enumerable.Any(activity.Tags, t => t.Key == BrighterSemanticConventions.Operation)).IsFalse();
        }

        // Assert: topic/cooldown detail is gated by the dedicated CircuitBreaker flag
        if (options.HasFlag(InstrumentationOptions.CircuitBreaker))
        {
            await Assert.That(System.Linq.Enumerable.Any(activity.Tags, t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic && t.Value == topic.Value)).IsTrue();
            await Assert.That(System.Linq.Enumerable.Any(activity.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 5)).IsTrue();
        }
        else
        {
            await Assert.That(System.Linq.Enumerable.Any(activity.Tags, t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic)).IsFalse();
            await Assert.That(System.Linq.Enumerable.Any(activity.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount)).IsFalse();
        }

        // Assert: also visible via the exporter
        await Assert.That(System.Linq.Enumerable.Any(_exportedActivities, a => a.Source.Name == BrighterSemanticConventions.SourceName)).IsTrue();
        await Assert.That(System.Linq.Enumerable.Any(_exportedActivities, a => a.DisplayName == $"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}")).IsTrue();
    }
}
