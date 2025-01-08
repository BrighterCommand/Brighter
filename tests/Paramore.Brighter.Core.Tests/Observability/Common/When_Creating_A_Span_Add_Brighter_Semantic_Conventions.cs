using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterTracerSpanTests : IDisposable
{
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer;
    private readonly Activity _parentActivity;

    public BrighterTracerSpanTests()
    {
       var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        _tracer = new BrighterTracer();
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
     }

    [Fact]
    public void When_Creating_A_Span_Add_Brighter_Semantic_Conventions()
    {
        //arrange
        var command = new MyCommand { Value = "My Test String" };

        //act
        var childActivity = _tracer.CreateSpan(
            CommandProcessorSpanOperation.Send, 
            command, 
            _parentActivity,
            options: InstrumentationOptions.All
        );
        
        childActivity.Stop();
        _parentActivity.Stop();
        
       var flushed = _traceProvider.ForceFlush();

        //assert
        flushed.Should().BeTrue();

        //check the created activity
        childActivity.ParentId.Should().Be(_parentActivity.Id);
        childActivity.DisplayName.Should().Be($"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}");
        childActivity.Source.Name.Should().Be(BrighterSemanticConventions.SourceName);
        var tagDictionary = childActivity.Tags.ToDictionary(pair => pair.Key, pair => pair.Value);
        tagDictionary.Should().ContainKey(BrighterSemanticConventions.RequestId);
        tagDictionary[BrighterSemanticConventions.RequestId].Should().Be(command.Id.ToString());
        tagDictionary.Should().ContainKey(BrighterSemanticConventions.RequestType);
        tagDictionary[BrighterSemanticConventions.RequestType].Should().Be(command.GetType().Name);
        tagDictionary.Should().ContainKey(BrighterSemanticConventions.RequestBody);
        tagDictionary[BrighterSemanticConventions.RequestBody].Should().Be(System.Text.Json.JsonSerializer.Serialize(command, JsonSerialisationOptions.Options));
        tagDictionary.Should().ContainKey(BrighterSemanticConventions.Operation);
        tagDictionary[BrighterSemanticConventions.Operation].Should().Be(CommandProcessorSpanOperation.Send.ToSpanName());


        //check via the exporter as well
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == BrighterSemanticConventions.SourceName).Should().BeTrue();
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}"); 
        childSpan.Should().NotBeNull();
        childSpan.ParentId.Should().Be(_parentActivity.Id);
        childSpan.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && (string)t.Value == command.Id.ToString()).Should().BeTrue();
        childSpan.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestType && (string)t.Value == command.GetType().Name).Should().BeTrue();
        childSpan.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && (string)t.Value == System.Text.Json.JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        childSpan.Tags.Any(t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == CommandProcessorSpanOperation.Send.ToSpanName()).Should().BeTrue();
    }

    public void Dispose()
    {
        _parentActivity.Dispose();
        _traceProvider.Dispose();
    }
}
