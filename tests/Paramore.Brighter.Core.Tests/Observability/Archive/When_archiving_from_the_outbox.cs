using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Archive;

public class ExternalServiceBusArchiveObservabilityTests
{
    private readonly List<Activity> _exportedActivities = new();
    private readonly ExternalBusService<Message,CommittableTransaction> _bus;
    private readonly Publication _publication;
    private readonly FakeTimeProvider _timeProvider;
    private RoutingKey _routingKey = new("MyEvent");
    private readonly InMemoryOutbox _outbox;
    private readonly TracerProvider _traceProvider;
    private const double TOLERANCE = 0.000000001;

    public ExternalServiceBusArchiveObservabilityTests()
    {
        IAmABus internalBus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(_timeProvider);

        var builder = Sdk.CreateTracerProviderBuilder();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        Brighter.CommandProcessor.ClearServiceBus();

        _publication = new Publication
        {
            Source = new Uri("http://localhost"),
            RequestType = typeof(MyEvent),
            Topic = _routingKey,
            Type = nameof(MyEvent),
        };

        var producer = new InMemoryProducer(internalBus, _timeProvider)
        {
            Publication = _publication
        };

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, producer } });

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var policyRegistry = new PolicyRegistry { { Brighter.CommandProcessor.RETRYPOLICY, retryPolicy } };

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };
        var archiveProvider = new InMemoryArchiveProvider();

        _bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            _outbox,
            archiveProvider,
            timeProvider:_timeProvider);
    }

    [Fact]
    public void When_archiving_from_the_outbox()
    {
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var context = new RequestContext();
        context.Span = parentActivity;
        
        //add and clear message
        var myEvent = new MyEvent();
        var myMessage = new MyEventMessageMapper().MapToMessage(myEvent, _publication);
        _bus.AddToOutbox(myMessage, context); 
        _bus.ClearOutbox([myMessage.Id], context);
        
        //se should have an entry in the outbox
        _outbox.EntryCount.Should().Be(1);
        
        //allow time to pass
        _timeProvider.Advance(TimeSpan.FromSeconds(300)); 
        
        //archive
        var dispatchedSince = TimeSpan.FromSeconds(100);
        _bus.Archive(dispatchedSince, context);
        
        //should be no messages in the outbox
        _outbox.EntryCount.Should().Be(0);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //We should have exported matching activities
        _exportedActivities.Count.Should().Be(9);
        
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        
        //there should be a n archive create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ArchiveMessages} {CommandProcessorSpanOperation.Archive.ToSpanName()}");
        createActivity.Should().NotBeNull();
        createActivity.ParentId.Should().Be(parentActivity?.Id);
        
        //check for outstanding messages span
        var osCheckActivity = _exportedActivities.SingleOrDefault(a =>
            a.DisplayName == $"{OutboxDbOperation.DispatchedMessages.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        osCheckActivity.Should().NotBeNull();
        osCheckActivity.ParentId.Should().Be(createActivity.Id);

        //check for delete messages span
        var deleteActivity = _exportedActivities.SingleOrDefault(a =>
            a.DisplayName == $"{OutboxDbOperation.Delete.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        deleteActivity.Should().NotBeNull();
        deleteActivity.ParentId.Should().Be(createActivity.Id);
        
        //check the tags for the create span
        createActivity.TagObjects.Should().Contain(t => t.Key == BrighterSemanticConventions.ArchiveAge && Math.Abs(Convert.ToDouble(t.Value) - dispatchedSince.TotalMilliseconds) < TOLERANCE);
    }
}
