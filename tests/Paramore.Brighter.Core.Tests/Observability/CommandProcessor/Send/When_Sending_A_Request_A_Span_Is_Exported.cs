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

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Send;

[Collection("Observability")]
public class CommandProcessorSendObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;

    public CommandProcessorSendObservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    
    [Theory]
    [InlineData(InstrumentationOptions.All)]
    [InlineData(InstrumentationOptions.None)]
    [InlineData(InstrumentationOptions.RequestBody)]
    [InlineData(InstrumentationOptions.RequestContext)]
    [InlineData(InstrumentationOptions.RequestInformation)]
    public void When_Sending_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported(InstrumentationOptions instrumentationOptions)
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext { Span = parentActivity };

        //act
        CreateCommandProcessor(instrumentationOptions).Send(command, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        Assert.Equal(2, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}"));

        var firstActivity = _exportedActivities.First();
        
        Assert.Equal(parentActivity?.Id, firstActivity.ParentId);
        if(instrumentationOptions == InstrumentationOptions.None)
            Assert.Empty(firstActivity.Tags);
        if (instrumentationOptions.HasFlag(InstrumentationOptions.RequestInformation))
        {
            Assert.Contains(firstActivity.Tags, t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id);
            Assert.Contains(firstActivity.Tags, t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) });
            Assert.Contains(firstActivity.Tags, t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" });
        }
        else
        {
            Assert.DoesNotContain(firstActivity.Tags, t => t.Key == BrighterSemanticConventions.RequestId);
            Assert.DoesNotContain(firstActivity.Tags, t => t.Key == BrighterSemanticConventions.RequestType);
            Assert.DoesNotContain(firstActivity.Tags, t => t.Key == BrighterSemanticConventions.Operation);
        }
        
        if(instrumentationOptions.HasFlag(InstrumentationOptions.RequestBody))
            Assert.Contains(firstActivity.Tags, t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options));
        else
            Assert.DoesNotContain(firstActivity.Tags, t => t.Key == BrighterSemanticConventions.RequestBody);
        
        
        Assert.Equal(1, firstActivity.Events.Count());
        Assert.Equal(nameof(MyCommandHandler), firstActivity.Events.First().Name);
        Assert.True(firstActivity.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)));
        Assert.True(firstActivity.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(firstActivity.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
    }
    
    [Fact]
    public void When_Sending_A_Request_With_Span_In_ActivityCurrent_A_Child_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        Activity.Current = parentActivity;
        
        //act
        CreateCommandProcessor(InstrumentationOptions.All).Send(command, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        Assert.Equal(2, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}"));
        Assert.Equal(parentActivity?.Id, _exportedActivities.First().ParentId);
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })); 
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }));
        
        Assert.Equal(1, _exportedActivities.First().Events.Count());
        Assert.Equal(nameof(MyCommandHandler), _exportedActivities.First().Events.First().Name);
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
    }
    
    [Fact]
    public void When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        
        //act
        CreateCommandProcessor(InstrumentationOptions.All).Send(command, context);
        
        _traceProvider.ForceFlush();
        
        //assert
        Assert.Equal(1, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        Assert.True(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}"));
        Assert.Null(_exportedActivities.First().ParentId);
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })); 
        Assert.True(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)));
        Assert.True(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }));
        
        Assert.Equal(1, _exportedActivities.First().Events.Count());
        Assert.Equal(nameof(MyCommandHandler), _exportedActivities.First().Events.First().Name);
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync"));
        Assert.True(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value));
    }

    private IAmACommandProcessor CreateCommandProcessor(InstrumentationOptions instrumentationOptions)
    {
        BrighterTracer tracer = new();
       
        Brighter.CommandProcessor.ClearServiceBus();
        
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(new Dictionary<string, string>()));
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};
        
        Brighter.CommandProcessor.ClearServiceBus();

        return new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory(),
            tracer: tracer, 
            instrumentationOptions: instrumentationOptions
        );
    }
}
