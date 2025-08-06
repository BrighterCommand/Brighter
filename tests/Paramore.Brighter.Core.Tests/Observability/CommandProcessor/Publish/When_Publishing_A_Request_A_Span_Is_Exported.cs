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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Publish;

[Collection("Observability")]
public class CommandProcessorPublishObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;

    public CommandProcessorPublishObservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        BrighterTracer tracer = new();
       
        Brighter.CommandProcessor.ClearServiceBus();
        
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

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};
        
        Brighter.CommandProcessor.ClearServiceBus();

        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory(),
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public void When_Publishing_A_Request_With_Span_In_Context_Child_Spans_Are_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        _commandProcessor.Publish(@event, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        Assert.Equal(4, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Equal(parentActivity?.Id, createActivity.ParentId);
        
        //parent span and child spans for each publish operation
        Assert.Equal(2, _exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}"));
        
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").ToList();

        //--first publish
        var first = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyEventHandler)));
        Assert.Equal(createActivity.Id, first.ParentId);
        Assert.True(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id));
        Assert.True(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })); 
        Assert.True(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        Assert.True(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
        
        var activityEvent = first.Events.Single(e => e.Name == nameof(MyEventHandler) || e.Name == nameof(MyOtherEventHandler));
        Assert.True(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == activityEvent.Name));
        Assert.True(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(activityEvent.Tags.Any(t => t.Value != null && t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));       
        
        //--second publish
        var second = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyOtherEventHandler)));
        Assert.Equal(createActivity.Id, second.ParentId);
        Assert.True(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id));
        Assert.True(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }));
        Assert.True(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        Assert.True(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
        
        activityEvent = second.Events.Single(e => e.Name == nameof(MyEventHandler) || e.Name == nameof(MyOtherEventHandler));
        Assert.True(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == activityEvent.Name));
        Assert.True(activityEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(activityEvent.Tags.Any(t => t.Value != null && t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));       
         
         //TODO: Needs adding when https://github.com/dotnet/runtime/pull/101381 is released
         /*
         //--check the links 
         first.Links.Count().Should().Be(1);
         first.Links.Single().Context.Should().Be(second.Context);
         second.Links.Count().Should().Be(1);
         second.Links.Single().Context.Should().Be(first.Context);
         */
    }

    [Fact]
    public void When_Publishing_A_Request_With_Span_In_ActivityCurrent_Child_Spans_Are_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext();
        Activity.Current = parentActivity;
        
        //act
        _commandProcessor.Publish(@event, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        Assert.Equal(4, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Equal(parentActivity?.Id, createActivity.ParentId);
        
        //parent span and child spans for each publish operation
        Assert.Equal(2, _exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}"));
        
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").ToList();

        //--first publish
        var first = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyEventHandler)));
        Assert.Equal(createActivity.Id, first.ParentId);
        Assert.True(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id));
        Assert.True(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })); 
        Assert.True(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        Assert.True(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
        
        Assert.Equal(1, first.Events.Count());
        Assert.Equal(nameof(MyEventHandler), first.Events.First().Name);
        Assert.True(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandler)));
        Assert.True(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));       
        
        //--second publish
        var second = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyOtherEventHandler)));
        Assert.Equal(createActivity.Id, second.ParentId);
        Assert.True(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id));
        Assert.True(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }));
        Assert.True(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        Assert.True(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
        
         Assert.Equal(1, second.Events.Count());
         Assert.Equal(nameof(MyOtherEventHandler), second.Events.First().Name);
         Assert.True(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandler)));
         Assert.True(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
         Assert.True(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
         
         //TODO: Needs adding when https://github.com/dotnet/runtime/pull/101381 is released
         /*
         //--check the links 
         first.Links.Count().Should().Be(1);
         first.Links.Single().Context.Should().Be(second.Context);
         second.Links.Count().Should().Be(1);
         second.Links.Single().Context.Should().Be(first.Context);
         */
    }

    [Fact]
    public void When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        var @event = new MyEvent();
        var context = new RequestContext();
        
        //act
        _commandProcessor.Publish(@event, context);
        
        _traceProvider.ForceFlush();
        
        //assert
        Assert.Equal(3, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Null(createActivity.ParentId);
        
        //parent span and child spans for each publish operation
        Assert.Equal(2, _exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}"));
        
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").ToList();

        //--first publish
        var first = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyEventHandler)));
        Assert.Equal(createActivity.Id, first.ParentId);
        Assert.True(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id));
        Assert.True(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })); 
        Assert.True(first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        Assert.True(first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
        
        Assert.Equal(1, first.Events.Count());
        Assert.Equal(nameof(MyEventHandler), first.Events.First().Name);
        Assert.True(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandler)));
        Assert.True(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));       
        
        //--second publish
        var second = publishActivities.First(activity => activity.Events.Any(e => e.Name == nameof(MyOtherEventHandler)));
        Assert.Equal(createActivity.Id, second.ParentId);
        Assert.True(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id));
        Assert.True(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }));
        Assert.True(second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        Assert.True(second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
        
         Assert.Equal(1, second.Events.Count());
         Assert.Equal(nameof(MyOtherEventHandler), second.Events.First().Name);
         Assert.True(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandler)));
         Assert.True(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
         Assert.True(second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
         
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
