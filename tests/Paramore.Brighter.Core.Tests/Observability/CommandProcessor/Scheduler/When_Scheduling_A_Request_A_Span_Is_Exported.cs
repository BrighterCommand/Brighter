using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Scheduler;

[Collection("Observability")]
public class CommandProcessorSchedulerObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly FakeTimeProvider _timeProvider;

    public CommandProcessorSchedulerObservabilityTests()
    {
        _timeProvider = new FakeTimeProvider(DateTimeOffset.Now);
        
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
        registry.Register<MyCommand, MyCommandHandler>();
        registry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        
        var handlerFactory = new SimpleHandlerFactory(_ => new MyCommandHandler(new Dictionary<string, string>()), _ => new FireSchedulerRequestHandler(_commandProcessor!));
 
        var policyRegistry = new PolicyRegistry 
        {
            {Brighter.CommandProcessor.RETRYPOLICY, Policy.Handle<Exception>().Retry()},
            {Brighter.CommandProcessor.RETRYPOLICYASYNC, Policy.Handle<Exception>().RetryAsync()},
        
        };
        
        Brighter.CommandProcessor.ClearServiceBus();

        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory{TimeProvider = _timeProvider},
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public void When_Scheduling_A_Sending_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{ Value = "My Test String" };
        var context = new RequestContext { Span = parentActivity };

        //act
        _commandProcessor.Send(TimeSpan.FromSeconds(1), command, context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        Assert.Equal(2, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Scheduler.ToSpanName()}"));
        Assert.Equal(parentActivity?.Id, _exportedActivities.First().ParentId);
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })); 
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "scheduler" }));
        Assert.Equal(0, _exportedActivities.First().Events.Count());
        
        _exportedActivities.Clear();

        parentActivity?.Start();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        
        Assert.Equal(2, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}"));
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })); 
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }));
                    
        Assert.Equal(nameof(MyCommandHandler), _exportedActivities.First().Events.First().Name);
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
        
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(FireSchedulerRequest)} {CommandProcessorSpanOperation.Send.ToSpanName()}"));
        Assert.True(_exportedActivities[1].Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(FireSchedulerRequest) })); 
        Assert.True(_exportedActivities[1].Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && !string.IsNullOrEmpty(t.Value)));
        Assert.True(_exportedActivities[1].Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }));
                    
        Assert.Equal(nameof(FireSchedulerRequestHandler), _exportedActivities[1].Events.First().Name);
        Assert.True(_exportedActivities[1].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(FireSchedulerRequestHandler)));
        Assert.True(_exportedActivities[1].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async"));
        Assert.True(_exportedActivities[1].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
    }
    
    [Fact]
    public void When_Scheduling_A_Publish_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{ Value = "My Test String" };
        var context = new RequestContext { Span = parentActivity };

        //act
        _commandProcessor.Publish(TimeSpan.FromSeconds(1), command, context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        Assert.Equal(2, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Scheduler.ToSpanName()}"));
        Assert.Equal(parentActivity?.Id, _exportedActivities.First().ParentId);
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })); 
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "scheduler" }));
        Assert.Equal(0, _exportedActivities.First().Events.Count());
        
        _exportedActivities.Clear();

        parentActivity?.Start();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        
        Assert.Equal(3, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Publish.ToSpanName()}"));
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })); 
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }));
                    
        Assert.Equal(nameof(MyCommandHandler), _exportedActivities.First().Events.First().Name);
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
        
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(FireSchedulerRequest)} {CommandProcessorSpanOperation.Send.ToSpanName()}"));
        Assert.True(_exportedActivities[2].Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(FireSchedulerRequest) })); 
        Assert.True(_exportedActivities[2].Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && !string.IsNullOrEmpty(t.Value)));
        Assert.True(_exportedActivities[2].Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }));
                    
        Assert.Equal(nameof(FireSchedulerRequestHandler), _exportedActivities[2].Events.First().Name);
        Assert.True(_exportedActivities[2].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(FireSchedulerRequestHandler)));
        Assert.True(_exportedActivities[2].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async"));
        Assert.True(_exportedActivities[2].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
    }
}
