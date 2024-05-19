using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Send;

public class CommandProcessorSendObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private BrighterTracer _tracer;
    private Activity _parentActivity;

    public CommandProcessorSendObservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        _tracer = new BrighterTracer();
       
        Brighter.CommandProcessor.ClearServiceBus();
        
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        
        var handlerFactory = new TestHandlerFactorySync<MyCommand, MyCommandHandler>(() => new MyCommandHandler());
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};

        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            tracer: _tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public void When_Sending_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported()
    {
        //arrange
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
         var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        context.Span = _parentActivity;
        
        //act
        _commandProcessor.Send(command, context);
        _parentActivity.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpan.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().Be(_parentActivity.Id);
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && (string)t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestType && (string)t.Value == nameof(MyCommand)).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && (string)t.Value == JsonSerializer.Serialize(command)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == "send").Should().BeTrue();
        
        _exportedActivities.First().Events.Count().Should().Be(1);
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandler));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value == true).Should().BeTrue();
    }
    
    [Fact]
    public void When_Sending_A_Request_With_Span_In_ActivityCurrent_A_Child_Span_Is_Exported()
    {
        //arrange
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        Activity.Current = _parentActivity;
        
        //act
        _commandProcessor.Send(command, context);
        _parentActivity.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpan.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().Be(_parentActivity.Id);
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && (string)t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestType && (string)t.Value == nameof(MyCommand)).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && (string)t.Value == JsonSerializer.Serialize(command)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == "send").Should().BeTrue();
        
        _exportedActivities.First().Events.Count().Should().Be(1);
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandler));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value == true).Should().BeTrue();
    }
    
    [Fact]
    public void When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        
        //act
        _commandProcessor.Send(command, context);
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(1);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpan.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().BeNull();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && (string)t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestType && (string)t.Value == nameof(MyCommand)).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && (string)t.Value == JsonSerializer.Serialize(command)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == "send").Should().BeTrue();
        
        _exportedActivities.First().Events.Count().Should().Be(1);
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandler));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value == true).Should().BeTrue();
    }
    
}
