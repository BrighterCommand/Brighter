using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Observability.Archive;
[NotInParallel]
public class ExternalServiceBusArchiveObservabilityTests
{
    private readonly List<Activity> _exportedActivities = new();
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _bus;
    private readonly Publication _publication;
    private readonly FakeTimeProvider _timeProvider;
    private RoutingKey _routingKey = new("MyEvent");
    private readonly InMemoryOutbox _outbox;
    private readonly TracerProvider _traceProvider;
    private const double TOLERANCE = 0.000000001;
    private readonly BrighterTracer _tracer;
    public ExternalServiceBusArchiveObservabilityTests()
    {
        IAmABus internalBus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _tracer = new BrighterTracer(_timeProvider);
        var builder = Sdk.CreateTracerProviderBuilder();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        var type = new CloudEventsType("io.goparamore.brighter.myevent");
        _publication = new Publication
        {
            Source = new Uri("http://localhost"),
            RequestType = typeof(MyEvent),
            Topic = _routingKey,
            Type = type,
        };
        var producer = new InMemoryMessageProducer(internalBus, _publication);
        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> { { new ProducerKey(_routingKey, type), producer } });
        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()), null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        _outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = _tracer
        };
        _bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, new ResiliencePipelineRegistry<string>().AddBrighterDefault(), messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), _tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox, timeProvider: _timeProvider);
    }

    [Test]
    [Arguments(InstrumentationOptions.RequestInformation)]
    [Arguments(InstrumentationOptions.None)]
    [Arguments(InstrumentationOptions.All)]
    public async Task When_archiving_from_the_outbox(InstrumentationOptions instrumentationOptions)
    {
        Activity.Current = null;
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var context = new RequestContext
        {
            Span = parentActivity
        };
        //add and clear message
        var myEvent = new MyEvent();
        var myMessage = new MyEventMessageMapper().MapToMessage(myEvent, _publication);
        _bus.AddToOutbox(myMessage, context);
        _bus.ClearOutbox([myMessage.Id], context);
        //we should have an entry in the outbox
        await Assert.That(_outbox.EntryCount).IsEqualTo(1);
        //allow time to pass
        _timeProvider.Advance(TimeSpan.FromSeconds(300));
        //archive
        var dispatchedSince = TimeSpan.FromSeconds(100);
        var archiveProvider = new InMemoryArchiveProvider();
        var archiver = new OutboxArchiver<Message, CommittableTransaction>(_outbox, archiveProvider, tracer: _tracer, instrumentationOptions: instrumentationOptions);
        await archiver.ArchiveAsync(dispatchedSince, context);
        //should be no messages in the outbox
        await Assert.That(_outbox.EntryCount).IsEqualTo(0);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //We should have exported matching activities
        //+1 confirmation (settle) span emitted per confirmed message (FR-2)
        await Assert.That(_exportedActivities.Count).IsEqualTo(10);

        await Assert.That((_exportedActivities).Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();

        //there should be an archive create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ArchiveMessages} {CommandProcessorSpanOperation.Archive.ToSpanName()}");
        await Assert.That(createActivity).IsNotNull();
        await Assert.That(createActivity.ParentId).IsEqualTo(parentActivity?.Id);
        //check for outstanding messages span
        var osCheckActivity = _exportedActivities.SingleOrDefault(a => a.DisplayName == $"{BoxDbOperation.DispatchedMessages.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(osCheckActivity).IsNotNull();
        await Assert.That(osCheckActivity?.ParentId).IsEqualTo(createActivity.Id);
        //check for delete messages span
        var deleteActivity = _exportedActivities.SingleOrDefault(a => a.DisplayName == $"{BoxDbOperation.Delete.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(deleteActivity).IsNotNull();
        await Assert.That(deleteActivity?.ParentId).IsEqualTo(createActivity.Id);
        //check the tags for the create span
        if (instrumentationOptions == InstrumentationOptions.None)
            await Assert.That(createActivity.Tags).IsEmpty();
        if (instrumentationOptions.HasFlag(InstrumentationOptions.RequestInformation))
        {
            await Assert.That((createActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.ArchiveAge && Math.Abs(Convert.ToDouble(t.Value) - dispatchedSince.TotalMilliseconds) < TOLERANCE)).IsTrue();
            await Assert.That((createActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.Operation && (string)t.Value == CommandProcessorSpanOperation.Archive.ToSpanName())).IsTrue();
            await Assert.That((createActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && (string)t.Value == CommandProcessorSpanOperation.Archive.ToSpanName())).IsTrue();
        }
        else
        {
            await Assert.That((createActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.ArchiveAge)).IsFalse();
            await Assert.That((createActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.Operation)).IsFalse();
            await Assert.That((createActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.MessagingOperationName)).IsFalse();
        }

        //check the tags for the outstanding messages span
        await Assert.That(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.DispatchedMessages.ToSpanName())).IsTrue();
        await Assert.That(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable)).IsTrue();
        await Assert.That(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName())).IsTrue();
        await Assert.That(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName)).IsTrue();
        //check the tags for the delete messages span
        await Assert.That(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Delete.ToSpanName())).IsTrue();
        await Assert.That(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable)).IsTrue();
        await Assert.That(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName())).IsTrue();
        await Assert.That(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName)).IsTrue();
    }
}
