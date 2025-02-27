using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.Post;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;
using MyEvent = Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyEvent;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Scheduler;

[Collection("Observability")]
public class CommandProcessorSchedulerObservabilityAsyncTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly FakeTimeProvider _timeProvider;

    public CommandProcessorSchedulerObservabilityAsyncTests()
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
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        
        var handlerFactory = new SimpleHandlerFactory(_ => null!, type =>
        {
            if (type == typeof(FireSchedulerRequestHandler))
            {
                return new FireSchedulerRequestHandler(_commandProcessor!);
            }

            return new MyCommandHandlerAsync(new Dictionary<string, string>());
        });
 
        var policyRegistry = new PolicyRegistry 
        {
            {Brighter.CommandProcessor.RETRYPOLICYASYNC, Policy.Handle<Exception>().RetryAsync()},
        
        };
        
        Brighter.CommandProcessor.ClearServiceBus();

        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new InMemorySchedulerFactory{TimeProvider = _timeProvider},
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public async Task When_Scheduling_A_Sending_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported_Async()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{ Value = "My Test String" };
        var context = new RequestContext { Span = parentActivity };

        //act
        await _commandProcessor.SendAsync(TimeSpan.FromSeconds(1), command, context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Scheduler.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().Be(parentActivity?.Id);
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "scheduler" }).Should().BeTrue();
        _exportedActivities.First().Events.Count().Should().Be(0);
        
        _exportedActivities.Clear();

        parentActivity?.Start();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }).Should().BeTrue();
                    
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandlerAsync));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandlerAsync)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
        
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(FireSchedulerRequest)} {CommandProcessorSpanOperation.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities[1].Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(FireSchedulerRequest) }).Should().BeTrue(); 
        _exportedActivities[1].Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && !string.IsNullOrEmpty(t.Value)).Should().BeTrue();
        _exportedActivities[1].Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }).Should().BeTrue();
                    
        _exportedActivities[1].Events.First().Name.Should().Be(nameof(FireSchedulerRequestHandler));
        _exportedActivities[1].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(FireSchedulerRequestHandler)).Should().BeTrue();
        _exportedActivities[1].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities[1].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
    }
    
    [Fact]
    public async Task When_Scheduling_A_Publish_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported_Async()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var command = new MyCommand{ Value = "My Test String" };
        var context = new RequestContext { Span = parentActivity };

        //act
        await _commandProcessor.PublishAsync(TimeSpan.FromSeconds(1), command, context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Scheduler.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().ParentId.Should().Be(parentActivity?.Id);
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "scheduler" }).Should().BeTrue();
        _exportedActivities.First().Events.Count().Should().Be(0);
        
        _exportedActivities.Clear();

        parentActivity?.Start();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        
        _exportedActivities.Count.Should().Be(3);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Publish.ToSpanName()}").Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) }).Should().BeTrue(); 
        _exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)).Should().BeTrue();
        _exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
                    
        _exportedActivities.First().Events.First().Name.Should().Be(nameof(MyCommandHandlerAsync));
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandlerAsync)).Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
        
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        _exportedActivities.Any(a => a.DisplayName == $"{nameof(FireSchedulerRequest)} {CommandProcessorSpanOperation.Send.ToSpanName()}").Should().BeTrue();
        _exportedActivities[2].Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(FireSchedulerRequest) }).Should().BeTrue(); 
        _exportedActivities[2].Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && !string.IsNullOrEmpty(t.Value)).Should().BeTrue();
        _exportedActivities[2].Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" }).Should().BeTrue();
                    
        _exportedActivities[2].Events.First().Name.Should().Be(nameof(FireSchedulerRequestHandler));
        _exportedActivities[2].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(FireSchedulerRequestHandler)).Should().BeTrue();
        _exportedActivities[2].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        _exportedActivities[2].Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
    }
}

