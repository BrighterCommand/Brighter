using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        Assert.True(flushed);

        //check the created activity
        Assert.Equal(_parentActivity.Id, childActivity.ParentId);
        Assert.Equal($"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}", childActivity.DisplayName);
        Assert.Equal(BrighterSemanticConventions.SourceName, childActivity.Source.Name);
        var tagDictionary = childActivity.Tags.ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.Contains(BrighterSemanticConventions.RequestId, tagDictionary.Keys);
        Assert.Equal(command.Id.ToString(), tagDictionary[BrighterSemanticConventions.RequestId]);
        Assert.Contains(BrighterSemanticConventions.RequestType, tagDictionary.Keys);
        Assert.Equal(command.GetType().Name, tagDictionary[BrighterSemanticConventions.RequestType]);
        Assert.Contains(BrighterSemanticConventions.RequestBody, tagDictionary.Keys);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(command, JsonSerialisationOptions.Options), tagDictionary[BrighterSemanticConventions.RequestBody]);
        Assert.Contains(BrighterSemanticConventions.Operation, tagDictionary.Keys);
        Assert.Equal(CommandProcessorSpanOperation.Send.ToSpanName(), tagDictionary[BrighterSemanticConventions.Operation]);


        //check via the exporter as well
        Assert.Equal(2, _exportedActivities.Count);
        Assert.Contains(_exportedActivities, a => a.Source.Name == BrighterSemanticConventions.SourceName);
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}"); 
        Assert.NotNull(childSpan);
        Assert.Equal(_parentActivity.Id, childSpan.ParentId);
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.RequestId && (string)t.Value == command.Id.ToString());
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.RequestType && (string)t.Value == command.GetType().Name);
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.RequestBody && (string)t.Value == System.Text.Json.JsonSerializer.Serialize(command, JsonSerialisationOptions.Options));
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == CommandProcessorSpanOperation.Send.ToSpanName());
    }

    public void Dispose()
    {
        _parentActivity.Dispose();
        _traceProvider.Dispose();
    }
}
