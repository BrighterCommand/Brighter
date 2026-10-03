using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using Xunit;

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

    [Theory]
    [InlineData(InstrumentationOptions.All)]
    [InlineData(InstrumentationOptions.RequestInformation)]
    [InlineData(InstrumentationOptions.CircuitBreaker)]
    [InlineData(InstrumentationOptions.None)]
    public void When_Creating_A_Circuit_Breaker_Span_Add_Brighter_Semantic_Conventions(InstrumentationOptions options)
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
        Assert.Equal($"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}", activity.DisplayName);
        if (options == InstrumentationOptions.None)
        {
            Assert.Empty(activity.Tags);
        }
        else
        {
            Assert.Contains(activity.Tags, t =>
                t.Key == BrighterSemanticConventions.InstrumentationDomain &&
                t.Value == BrighterSemanticConventions.CircuitBreakerInstrumentationDomain);
        }

        // Assert: the generic Operation tag is gated by RequestInformation, matching every other Create*Span
        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            Assert.Contains(activity.Tags, t => t.Key == BrighterSemanticConventions.Operation && t.Value == "trip");
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, t => t.Key == BrighterSemanticConventions.Operation);
        }

        // Assert: topic/cooldown detail is gated by the dedicated CircuitBreaker flag
        if (options.HasFlag(InstrumentationOptions.CircuitBreaker))
        {
            Assert.Contains(activity.Tags, t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic && t.Value == topic.Value);
            Assert.Contains(activity.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount && (int)t.Value! == 5);
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, t => t.Key == BrighterSemanticConventions.CircuitBreakerTopic);
            Assert.DoesNotContain(activity.TagObjects, t => t.Key == BrighterSemanticConventions.CircuitBreakerCooldownCount);
        }

        // Assert: also visible via the exporter
        Assert.Contains(_exportedActivities, a => a.Source.Name == BrighterSemanticConventions.SourceName);
        Assert.Contains(_exportedActivities, a => a.DisplayName == $"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}");
    }
}
