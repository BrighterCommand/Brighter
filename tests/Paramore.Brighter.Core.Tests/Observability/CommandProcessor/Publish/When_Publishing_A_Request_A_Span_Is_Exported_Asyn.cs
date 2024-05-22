﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Publish;

public class AsyncCommandProcessorPublishObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;

    public AsyncCommandProcessorPublishObservabilityTests()
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
        registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        registry.RegisterAsync<MyEvent, MyOtherEventHandlerAsync>();
        
        var handlerFactory = new SimpleHandlerFactoryAsync(type =>
        {
            switch (type.Name)
            {
                case nameof(MyEventHandlerAsync):
                    return new MyEventHandlerAsync(new Dictionary<string, string>());
                case nameof(MyOtherEventHandlerAsync):
                    return new MyOtherEventHandlerAsync(new Dictionary<string, string>());
                default:
                    throw new ArgumentOutOfRangeException(nameof(type.Name), type.Name, null);
            }
        });

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
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public async Task When_Publishing_A_Request_With_Span_In_Context_Child_Spans_Are_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        await _commandProcessor.PublishAsync(@event, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(4);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Create.ToSpanName()}");
        createActivity.Should().NotBeNull();
        createActivity.ParentId.Should().Be(parentActivity?.Id);
        
        //parent span and child spans for each publish operation
        _exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Publish.ToSpanName()}").Should().Be(2);
        
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Publish.ToSpanName()}").ToList();

        //--first publish
        var first = publishActivities.First();
        first.ParentId.Should().Be(createActivity.Id);
        first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue(); 
        first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
        
        first.Events.Count().Should().Be(1);
        first.Events.First().Name.Should().Be(nameof(MyEventHandlerAsync));
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandlerAsync)).Should().BeTrue();
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();       
        
        //--second publish
        var second = publishActivities.Last();
        second.ParentId.Should().Be(createActivity.Id);
        second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue();
        second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
        
         second.Events.Count().Should().Be(1);
         second.Events.First().Name.Should().Be(nameof(MyOtherEventHandlerAsync));
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandlerAsync)).Should().BeTrue();
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
         
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
    public async Task When_Publishing_A_Request_With_Span_In_ActivityCurrent_Child_Spans_Are_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext();
        Activity.Current = parentActivity;
        
        //act
        await _commandProcessor.PublishAsync(@event, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(4);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Create.ToSpanName()}");
        createActivity.Should().NotBeNull();
        createActivity.ParentId.Should().Be(parentActivity?.Id);
        
        //parent span and child spans for each publish operation
        _exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Publish.ToSpanName()}").Should().Be(2);
        
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Publish.ToSpanName()}").ToList();

        //--first publish
        var first = publishActivities.First();
        first.ParentId.Should().Be(createActivity.Id);
        first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue(); 
        first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
        
        first.Events.Count().Should().Be(1);
        first.Events.First().Name.Should().Be(nameof(MyEventHandlerAsync));
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandlerAsync)).Should().BeTrue();
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();       
        
        //--second publish
        var second = publishActivities.Last();
        second.ParentId.Should().Be(createActivity.Id);
        second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue();
        second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
        
         second.Events.Count().Should().Be(1);
         second.Events.First().Name.Should().Be(nameof(MyOtherEventHandlerAsync));
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandlerAsync)).Should().BeTrue();
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
         
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
    public async Task When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        var @event = new MyEvent();
        var context = new RequestContext();
        
        //act
        await _commandProcessor.PublishAsync(@event, context);
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(3);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Create.ToSpanName()}");
        createActivity.Should().NotBeNull();
        createActivity.ParentId.Should().BeNull();
        
        //parent span and child spans for each publish operation
        _exportedActivities.Count(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Publish.ToSpanName()}").Should().Be(2);
        
        var publishActivities = _exportedActivities.Where(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpan.Publish.ToSpanName()}").ToList();

        //--first publish
        var first = publishActivities.First();
        first.ParentId.Should().Be(createActivity.Id);
        first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        first.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue(); 
        first.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        first.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
        
        first.Events.Count().Should().Be(1);
        first.Events.First().Name.Should().Be(nameof(MyEventHandlerAsync));
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyEventHandlerAsync)).Should().BeTrue();
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
        first.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();       
        
        //--second publish
        var second = publishActivities.Last();
        second.ParentId.Should().Be(createActivity.Id);
        second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        second.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue();
        second.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        second.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "publish" }).Should().BeTrue();
        
         second.Events.Count().Should().Be(1);
         second.Events.First().Name.Should().Be(nameof(MyOtherEventHandlerAsync));
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyOtherEventHandlerAsync)).Should().BeTrue();
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "async").Should().BeTrue();
         second.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value).Should().BeTrue();
         
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
