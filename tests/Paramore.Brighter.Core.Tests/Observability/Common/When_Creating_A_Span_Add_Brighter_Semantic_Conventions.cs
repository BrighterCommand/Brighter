using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.Common;
[NotInParallel]
public class BrighterTracerSpanTests
{
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer;
    private readonly Activity _parentActivity;
    public BrighterTracerSpanTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        _tracer = new BrighterTracer();
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
    }

    [Test]
    public async Task When_Creating_A_Span_Add_Brighter_Semantic_Conventions()
    {
        //arrange
        var command = new MyCommand
        {
            Value = "My Test String"
        };
        //act
        var childActivity = _tracer.CreateSpan(CommandProcessorSpanOperation.Send, command, _parentActivity, options: InstrumentationOptions.All);
        childActivity.Stop();
        _parentActivity.Stop();
        var flushed = _traceProvider.ForceFlush();
        //assert
        await Assert.That(flushed).IsTrue();
        //check the created activity
        await Assert.That(childActivity.ParentId).IsEqualTo(_parentActivity.Id);
        await Assert.That(childActivity.DisplayName).IsEqualTo($"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}");
        await Assert.That(childActivity.Source.Name).IsEqualTo(BrighterSemanticConventions.SourceName);
        var tagDictionary = childActivity.Tags.ToDictionary(pair => pair.Key, pair => pair.Value);
        await Assert.That(tagDictionary.Keys).Contains(BrighterSemanticConventions.RequestId);
        await Assert.That(tagDictionary[BrighterSemanticConventions.RequestId]).IsEqualTo(command.Id.ToString());
        await Assert.That(tagDictionary.Keys).Contains(BrighterSemanticConventions.RequestType);
        await Assert.That(tagDictionary[BrighterSemanticConventions.RequestType]).IsEqualTo(command.GetType().Name);
        await Assert.That(tagDictionary.Keys).Contains(BrighterSemanticConventions.RequestBody);
        await Assert.That(tagDictionary[BrighterSemanticConventions.RequestBody]).IsEqualTo(System.Text.Json.JsonSerializer.Serialize(command, JsonSerialisationOptions.Options));
        await Assert.That(tagDictionary.Keys).Contains(BrighterSemanticConventions.Operation);
        await Assert.That(tagDictionary[BrighterSemanticConventions.Operation]).IsEqualTo(CommandProcessorSpanOperation.Send.ToSpanName());
        //check via the exporter as well
        await Assert.That(_exportedActivities.Count).IsEqualTo(2);
        await Assert.That((_exportedActivities).Any(a => a.Source.Name == BrighterSemanticConventions.SourceName)).IsTrue();
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}");
        await Assert.That(childSpan).IsNotNull();
        await Assert.That(childSpan.ParentId).IsEqualTo(_parentActivity.Id);
        await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestId && (string)t.Value == command.Id.ToString())).IsTrue();
        await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestType && (string)t.Value == command.GetType().Name)).IsTrue();
        await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestBody && (string)t.Value == System.Text.Json.JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == CommandProcessorSpanOperation.Send.ToSpanName())).IsTrue();
    }

    [After(Test)]
    public void Dispose()
    {
        _parentActivity.Dispose();
        _traceProvider.Dispose();
    }
}
