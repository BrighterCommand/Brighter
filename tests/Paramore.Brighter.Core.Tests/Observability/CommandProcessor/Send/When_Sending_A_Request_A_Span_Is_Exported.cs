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

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Send;
[NotInParallel]
public class CommandProcessorSendObservabilityTests
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    public CommandProcessorSendObservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
    }

    [Test]
    [Arguments(InstrumentationOptions.All)]
    [Arguments(InstrumentationOptions.None)]
    [Arguments(InstrumentationOptions.RequestBody)]
    [Arguments(InstrumentationOptions.RequestContext)]
    [Arguments(InstrumentationOptions.RequestInformation)]
    public async Task When_Sending_A_Request_With_Span_In_Context_A_Child_Span_Is_Exported(InstrumentationOptions instrumentationOptions)
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var command = new MyCommand
        {
            Value = "My Test String"
        };
        var context = new RequestContext
        {
            Span = parentActivity
        };
        //act
        CreateCommandProcessor(instrumentationOptions).Send(command, context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(2);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        await Assert.That(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}")).IsTrue();
        var firstActivity = _exportedActivities.First();
        await Assert.That(firstActivity.ParentId).IsEqualTo(parentActivity?.Id);
        if (instrumentationOptions == InstrumentationOptions.None)
            await Assert.That(firstActivity.Tags).IsEmpty();
        if (instrumentationOptions.HasFlag(InstrumentationOptions.RequestInformation))
        {
            await Assert.That((firstActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id)).IsTrue();
            await Assert.That((firstActivity.Tags).Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })).IsTrue();
            await Assert.That((firstActivity.Tags).Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" })).IsTrue();
        }
        else
        {
            await Assert.That((firstActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestId)).IsFalse();
            await Assert.That((firstActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestType)).IsFalse();
            await Assert.That((firstActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.Operation)).IsFalse();
        }

        if (instrumentationOptions.HasFlag(InstrumentationOptions.RequestBody))
            await Assert.That((firstActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))).IsTrue();
        else
            await Assert.That((firstActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.RequestBody)).IsFalse();
        await Assert.That(firstActivity.Events.Count()).IsEqualTo(1);
        await Assert.That(firstActivity.Events.First().Name).IsEqualTo(nameof(MyCommandHandler));
        await Assert.That(firstActivity.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler))).IsTrue();
        await Assert.That(firstActivity.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(firstActivity.Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
    }

    [Test]
    public async Task When_Sending_A_Request_With_Span_In_ActivityCurrent_A_Child_Span_Is_Exported()
    {
        //arrange
        Activity.Current = null;
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var command = new MyCommand
        {
            Value = "My Test String"
        };
        var context = new RequestContext();
        Activity.Current = parentActivity;
        //act
        CreateCommandProcessor(InstrumentationOptions.All).Send(command, context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(2);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        await Assert.That(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}")).IsTrue();
        await Assert.That(_exportedActivities.First().ParentId).IsEqualTo(parentActivity?.Id);
        await Assert.That(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id)).IsTrue();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })).IsTrue();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" })).IsTrue();
        await Assert.That(_exportedActivities.First().Events.Count()).IsEqualTo(1);
        await Assert.That(_exportedActivities.First().Events.First().Name).IsEqualTo(nameof(MyCommandHandler));
        await Assert.That(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler))).IsTrue();
        await Assert.That(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
    }

    [Test]
    public async Task When_Sending_A_Request_With_No_Context_Or_Span_In_ActivityCurrent_A_Root_Span_Is_Exported()
    {
        //arrange
        Activity.Current = null;
        var command = new MyCommand
        {
            Value = "My Test String"
        };
        var context = new RequestContext();
        //act
        CreateCommandProcessor(InstrumentationOptions.All).Send(command, context);
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(1);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        await Assert.That(_exportedActivities.Any(a => a.DisplayName == $"{nameof(MyCommand)} {CommandProcessorSpanOperation.Send.ToSpanName()}")).IsTrue();
        await Assert.That(_exportedActivities.First().ParentId).IsNull();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == command.Id)).IsTrue();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyCommand) })).IsTrue();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(_exportedActivities.First().Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "send" })).IsTrue();
        await Assert.That(_exportedActivities.First().Events.Count()).IsEqualTo(1);
        await Assert.That(_exportedActivities.First().Events.First().Name).IsEqualTo(nameof(MyCommandHandler));
        await Assert.That(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerName && (string)t.Value == nameof(MyCommandHandler))).IsTrue();
        await Assert.That(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.HandlerType && (string)t.Value == "sync")).IsTrue();
        await Assert.That(_exportedActivities.First().Events.First().Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value)).IsTrue();
    }

    private IAmACommandProcessor CreateCommandProcessor(InstrumentationOptions instrumentationOptions)
    {
        BrighterTracer tracer = new();
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(new Dictionary<string, string>()));
        var retryPolicy = Policy.Handle<Exception>().Retry();
        var policyRegistry = new PolicyRegistry
        {
            {
                Brighter.CommandProcessor.RETRYPOLICY,
                retryPolicy
            }
        };
        return new Brighter.CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: instrumentationOptions);
    }
}
