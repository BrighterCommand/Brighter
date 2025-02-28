using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

[Collection("Observability")]
public class AsyncCommandProcessorSendObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;

    public AsyncCommandProcessorSendObservabilityTests()
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
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        var receivedMessages = new Dictionary<string, string>();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(receivedMessages));
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .RetryAsync();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICYASYNC, retryPolicy}};
        
        Brighter.CommandProcessor.ClearServiceBus();

        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new InMemorySchedulerFactory(),
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public async Task When_Sending_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext { Span = parentActivity };

        //act
        await _commandProcessor.SendAsync(command, context, true);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().Be(parentActivity.Id);
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }).Should().BeTrue();
        
        _exportedActivities.First().Events.Count().Should().Be(1);
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandlerAsync));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandlerAsync)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
    }
    
    [Fact]
    public async Task When_Sending_A_Request_With_Span_In_ActivityCurrent_A_Child_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        Activity.Current = parentActivity;
        
        //act
        await _commandProcessor.SendAsync(command, context, true, new CancellationToken());
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().Be(parentActivity.Id);
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }).Should().BeTrue();                   
        
        _exportedActivities.First().Events.Count().Should().Be(1);
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandlerAsync));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandlerAsync)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
    }
    
    [Fact]
    public async Task When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        var command = new MyCommand{Value = "My Test String"};
        var context = new RequestContext();
        
        //act
        await _commandProcessor.SendAsync(command, context, true, new CancellationToken());
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(1);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().BeNull();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }).Should().BeTrue();
        
        _exportedActivities.First().Events.Count().Should().Be(1);
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandlerAsync));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandlerAsync)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
    }
    
}
