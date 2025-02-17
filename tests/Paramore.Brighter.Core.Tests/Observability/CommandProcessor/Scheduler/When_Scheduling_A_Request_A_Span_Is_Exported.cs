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
using Polly;
using Polly.Registry;
using Xunit;
using MyEvent = Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyEvent;

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
        var routingKey = new RoutingKey("MyEvent");

        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        Brighter.CommandProcessor.ClearServiceBus();

        var registry = new SubscriberRegistry();

        var handlerFactory = new PostCommandTests.EmptyHandlerFactorySync();

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var policyRegistry = new PolicyRegistry { { Brighter.CommandProcessor.RETRYPOLICY, retryPolicy } };
        
        var tracer = new BrighterTracer(_timeProvider);
        InMemoryOutbox outbox = new(_timeProvider) { Tracer = tracer };

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            {
                routingKey,
                new InMemoryProducer(new InternalBus(), new FakeTimeProvider())
                {
                    Publication = { Topic = routingKey, RequestType = typeof(MyEvent) }
                }
            }
        });

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            outbox,
            maxOutStandingMessages: -1
        );

        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            bus,
            new InMemorySchedulerFactory{ TimeProvider = _timeProvider },
            tracer: tracer,
            instrumentationOptions: InstrumentationOptions.All
        );
    }

    [Fact]
    public void When_Scheduling_A_Request_With_A_Delay_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        _commandProcessor.Post(TimeSpan.FromSeconds(10), @event, context);
        parentActivity?.Stop();

        _traceProvider.ForceFlush();

        //assert
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        var depositActivity = _exportedActivities.Single(a =>
            a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Scheduler.ToSpanName()}");
        depositActivity.Should().NotBeNull();
        depositActivity.ParentId.Should().Be(parentActivity?.Id);

        depositActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should()
            .BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })
            .Should().BeTrue();
        depositActivity.Tags
            .Any(t => t.Key == BrighterSemanticConventions.RequestBody &&
                      t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)).Should().BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "scheduler" }).Should()
            .BeTrue();

        var events = depositActivity.Events.ToList();
        events.Count.Should().Be(1);

        //mapping a message should be an event
        var mapperEvent = events.Single(e => e.Name == $"{nameof(MyEventMessageMapper)}");
        mapperEvent.Tags
            .Any(a => a.Key == BrighterSemanticConventions.MapperName &&
                      (string)a.Value == nameof(MyEventMessageMapper)).Should().BeTrue();
        mapperEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MapperType && (string)a.Value == "sync").Should()
            .BeTrue();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void When_Scheduling_A_Request_With_A_DateTime_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        _commandProcessor.Post(_timeProvider.GetUtcNow().AddSeconds(10), @event, context);
        parentActivity?.Stop();

        _traceProvider.ForceFlush();

        //assert
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        var depositActivity = _exportedActivities.Single(a =>
            a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Scheduler.ToSpanName()}");
        depositActivity.Should().NotBeNull();
        depositActivity.ParentId.Should().Be(parentActivity?.Id);

        depositActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should()
            .BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })
            .Should().BeTrue();
        depositActivity.Tags
            .Any(t => t.Key == BrighterSemanticConventions.RequestBody &&
                      t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)).Should().BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "scheduler" }).Should()
            .BeTrue();

        var events = depositActivity.Events.ToList();
        events.Count.Should().Be(1);

        //mapping a message should be an event
        var mapperEvent = events.Single(e => e.Name == $"{nameof(MyEventMessageMapper)}");
        mapperEvent.Tags
            .Any(a => a.Key == BrighterSemanticConventions.MapperName &&
                      (string)a.Value == nameof(MyEventMessageMapper)).Should().BeTrue();
        mapperEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MapperType && (string)a.Value == "sync").Should()
            .BeTrue();
        
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
    }
}
