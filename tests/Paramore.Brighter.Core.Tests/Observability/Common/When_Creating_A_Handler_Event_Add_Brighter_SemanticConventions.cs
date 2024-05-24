using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterSemanticConventionsEventTests 
{
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Activity _parentActivity;

    public BrighterSemanticConventionsEventTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        BrighterTracer tracer = new();
        _parentActivity = tracer.ActivitySource.StartActivity("BrighterSemanticConventionsEventTests"); 
    }
    [Fact]
    public void When_Creating_A_Handler_Event_Add_Brighter_SemanticConventions()
    {
        //act
        BrighterTracer.CreateHandlerEvent(_parentActivity, "MyCommandHandler", false, true);
        
        _parentActivity.Stop();
        var flushed = _traceProvider.ForceFlush();
        
        //assert
        //check the created activity
        var childActivity = _exportedActivities.First(a => a.DisplayName == "BrighterSemanticConventionsEventTests");
        childActivity.Should().NotBeNull();
        var childEvent = childActivity.Events.First(e => e.Name == "MyCommandHandler");
        childEvent.Tags.Should().ContainKey(BrighterSemanticConventions.HandlerName);
        var eventDictionary = childEvent.Tags.ToDictionary(pair => pair.Key, pair => pair.Value);
        eventDictionary[BrighterSemanticConventions.HandlerName].Should().Be("MyCommandHandler");
        childEvent.Tags.Should().ContainKey(BrighterSemanticConventions.HandlerType);
        eventDictionary[BrighterSemanticConventions.HandlerType].Should().Be("sync");
        childEvent.Tags.Should().ContainKey(BrighterSemanticConventions.IsSink);
        eventDictionary[BrighterSemanticConventions.IsSink].Should().Be(true);

    }
}
