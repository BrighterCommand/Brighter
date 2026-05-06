using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Publish;
[NotInParallel]
public class CommandProcessorPublishObservabilityTests
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    public CommandProcessorPublishObservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        BrighterTracer tracer = new();
        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyEventHandler>();
        registry.Register<MyEvent, MyOtherEventHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(type =>
        {
            switch (type.Name)
            {
                case nameof(MyEventHandler):
                    return new MyEventHandler(new Dictionary<string, string>());
                case nameof(MyOtherEventHandler):
                    return new MyOtherEventHandler(new Dictionary<string, string>());
                default:
                    throw new ArgumentOutOfRangeException(nameof(type.Name), type.Name, null);
            }
        });
        var retryPolicy = Policy.Handle<Exception>().Retry();
        var policyRegistry = new PolicyRegistry
        {
            {
                Brighter.CommandProcessor.RETRYPOLICY,
                retryPolicy
            }
        };
        _commandProcessor = new Brighter.CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Publishing_A_Request_With_Span_In_Context_Child_Spans_Are_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var @event = new MyEvent();
        var context = new RequestContext
        {
            Span = parentActivity
        };
        //act
        _commandProcessor.Publish(@event, context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(4);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        await Assert.That(createActivity).IsNotNull();
        await Assert.That(createActivity.ParentId).IsEqualTo(parentActivity?.Id);
        //parent span and child spans for each publish operation
        await Assert.That(_exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}")).IsEqualTo(2);
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").ToList();
        //--first publish
        var first = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyEventHandler)));
        await Assert.That(first.ParentId).IsEqualTo(createActivity.Id);
        await Assert.That(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" })).IsTrue();
        var activityEvent = first.Events.Single(e => e.Name == nameof(MyEventHandler) || e.Name == nameof(MyOtherEventHandler));
        await Assert.That(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == activityEvent.Name)).IsTrue();
        await Assert.That(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(activityEvent.Tags.Any(t => t.Value != null && t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
        //--second publish
        var second = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyOtherEventHandler)));
        await Assert.That(second.ParentId).IsEqualTo(createActivity.Id);
        await Assert.That(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" })).IsTrue();
        activityEvent = second.Events.Single(e => e.Name == nameof(MyEventHandler) || e.Name == nameof(MyOtherEventHandler));
        await Assert.That(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == activityEvent.Name)).IsTrue();
        await Assert.That(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(activityEvent.Tags.Any(t => t.Value != null && t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
    //TODO: Needs adding when https://github.com/dotnet/runtime/pull/101381 is released
    /*
         //--check the links 
         first.Links.Count().Should().Be(1);
         first.Links.Single().Context.Should().Be(second.Context);
         second.Links.Count().Should().Be(1);
         second.Links.Single().Context.Should().Be(first.Context);
         */
    }

    [Test]
    public async Task When_Publishing_A_Request_With_Span_In_ActivityCurrent_Child_Spans_Are_Exported()
    {
        //arrange
        Activity.Current = null;
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var @event = new MyEvent();
        var context = new RequestContext();
        Activity.Current = parentActivity;
        //act
        _commandProcessor.Publish(@event, context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(4);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        await Assert.That(createActivity).IsNotNull();
        await Assert.That(createActivity.ParentId).IsEqualTo(parentActivity?.Id);
        //parent span and child spans for each publish operation
        await Assert.That(_exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}")).IsEqualTo(2);
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").ToList();
        //--first publish
        var first = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyEventHandler)));
        await Assert.That(first.ParentId).IsEqualTo(createActivity.Id);
        await Assert.That(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" })).IsTrue();
        await Assert.That(first.Events.Count()).IsEqualTo(1);
        await Assert.That(first.Events.First().Name).IsEqualTo(nameof(MyEventHandler));
        await Assert.That(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandler))).IsTrue();
        await Assert.That(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
        //--second publish
        var second = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyOtherEventHandler)));
        await Assert.That(second.ParentId).IsEqualTo(createActivity.Id);
        await Assert.That(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" })).IsTrue();
        await Assert.That(second.Events.Count()).IsEqualTo(1);
        await Assert.That(second.Events.First().Name).IsEqualTo(nameof(MyOtherEventHandler));
        await Assert.That(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandler))).IsTrue();
        await Assert.That(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
    //TODO: Needs adding when https://github.com/dotnet/runtime/pull/101381 is released
    /*
         //--check the links 
         first.Links.Count().Should().Be(1);
         first.Links.Single().Context.Should().Be(second.Context);
         second.Links.Count().Should().Be(1);
         second.Links.Single().Context.Should().Be(first.Context);
         */
    }

    [Test]
    public async Task When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        Activity.Current = null;
        var @event = new MyEvent();
        var context = new RequestContext();
        //act
        _commandProcessor.Publish(@event, context);
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(3);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        await Assert.That(createActivity).IsNotNull();
        await Assert.That(createActivity.ParentId).IsNull();
        //parent span and child spans for each publish operation
        await Assert.That(_exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}")).IsEqualTo(2);
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").ToList();
        //--first publish
        var first = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyEventHandler)));
        await Assert.That(first.ParentId).IsEqualTo(createActivity.Id);
        await Assert.That(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" })).IsTrue();
        await Assert.That(first.Events.Count()).IsEqualTo(1);
        await Assert.That(first.Events.First().Name).IsEqualTo(nameof(MyEventHandler));
        await Assert.That(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandler))).IsTrue();
        await Assert.That(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
        //--second publish
        var second = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyOtherEventHandler)));
        await Assert.That(second.ParentId).IsEqualTo(createActivity.Id);
        await Assert.That(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" })).IsTrue();
        await Assert.That(second.Events.Count()).IsEqualTo(1);
        await Assert.That(second.Events.First().Name).IsEqualTo(nameof(MyOtherEventHandler));
        await Assert.That(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandler))).IsTrue();
        await Assert.That(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
    //TODO: Needs adding when https://github.com/dotnet/runtime/pull/101381 is released
    /*
         //--check the links 
         first.Links.Count().Should().Be(1);
         first.Links.Single().Context.Should().Be(second.Context);
         second.Links.Count().Should().Be(1);
         second.Links.Single().Context.Should().Be(first.Context);
         */
    }
}