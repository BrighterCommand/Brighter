using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;
// ReSharper disable ExplicitCallerInfoArgument

namespace Paramore.Brighter.Core.Tests.Observability.Archive;

public class AsyncExternalServiceBusArchiveObservabilityTests
{
    private readonly List<Activity> _exportedActivities = new();
    private readonly OutboxProducerMediator<Message,CommittableTransaction> _bus;
    private readonly Publication _publication;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RoutingKey _routingKey = new("MyEvent");
    private readonly InMemoryOutbox _outbox;
    private readonly TracerProvider _traceProvider;
    private const double TOLERANCE = 0.000000001;
    private readonly BrighterTracer _tracer;

    public AsyncExternalServiceBusArchiveObservabilityTests()
    {
        IAmABus internalBus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        _tracer = new BrighterTracer(_timeProvider);

        var builder = Sdk.CreateTracerProviderBuilder();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        Brighter.CommandProcessor.ClearServiceBus();

        var type = new CloudEventsType("io.goparamore.brighter.myevent");
        _publication = new Publication
        {
            Source = new Uri("http://localhost"),
            RequestType = typeof(MyEvent),
            Topic = _routingKey,
            Type = type,
        };

        var producer = new InMemoryMessageProducer(internalBus, _timeProvider, _publication);

        var producerRegistry =
            new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> { { new ProducerKey(_routingKey, type), producer } });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = _tracer };

        _bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            _tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            _outbox,
            timeProvider:_timeProvider);
    }

    [Theory]
    [InlineData(InstrumentationOptions.RequestInformation)]
    [InlineData(InstrumentationOptions.None)]
    [InlineData(InstrumentationOptions.All)]
    public async Task When_archiving_from_the_outbox(InstrumentationOptions instrumentationOptions)
    {
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var context = new RequestContext { Span = parentActivity };

        //add and clear message
        var myEvent = new MyEvent();
        var myMessage = new MyEventMessageMapper().MapToMessage(myEvent, _publication);
        await _bus.AddToOutboxAsync(myMessage, context); 
        await  _bus.ClearOutboxAsync([myMessage.Id], context);
        
        //we should have an entry in the outbox
        Assert.Equal(1, _outbox.EntryCount);
        
        //allow time to pass
        _timeProvider.Advance(TimeSpan.FromSeconds(300)); 
        
        //archive
        var dispatchedSince = TimeSpan.FromSeconds(100);
        var archiveProvider = new InMemoryArchiveProvider();

        var archiver = new OutboxArchiver<Message, CommittableTransaction>(_outbox, archiveProvider, tracer: _tracer,
            instrumentationOptions: instrumentationOptions);
        await archiver.ArchiveAsync(dispatchedSince, context);
        
         //should be no messages in the outbox
        Assert.Equal(0, _outbox.EntryCount);

        parentActivity?.Stop();

        _traceProvider.ForceFlush();

        //We should have exported matching activities
        Assert.Equal(9, _exportedActivities.Count);

        Assert.Contains(_exportedActivities, a => a.Source.Name == "Paramore.Brighter");

        //there should be an archive create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ArchiveMessages} {CommandProcessorSpanOperation.Archive.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Equal(parentActivity?.Id, createActivity.ParentId);

        //check for outstanding messages span
        var osCheckActivity = _exportedActivities.SingleOrDefault(a =>
            a.DisplayName == $"{BoxDbOperation.DispatchedMessages.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.NotNull(osCheckActivity);
        Assert.Equal(createActivity.Id, osCheckActivity?.ParentId);

        //check for delete messages span
        var deleteActivity = _exportedActivities.SingleOrDefault(a =>
            a.DisplayName == $"{BoxDbOperation.Delete.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.NotNull(deleteActivity);
        Assert.Equal(createActivity.Id, deleteActivity?.ParentId);
        
        //check the tags for the create span
        if(instrumentationOptions == InstrumentationOptions.None)
            Assert.Empty(createActivity.Tags);
        
        if (instrumentationOptions.HasFlag(InstrumentationOptions.RequestInformation))
        {
            Assert.Contains(createActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.ArchiveAge &&
                     Math.Abs(Convert.ToDouble(t.Value) - dispatchedSince.TotalMilliseconds) < TOLERANCE);
            Assert.Contains(createActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.Operation &&
                     (string)t.Value == CommandProcessorSpanOperation.Archive.ToSpanName());
            Assert.Contains(createActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.MessagingOperationType &&
                     (string)t.Value == CommandProcessorSpanOperation.Archive.ToSpanName());
        }
        else
        {
            Assert.DoesNotContain(createActivity.TagObjects, t => t.Key == BrighterSemanticConventions.ArchiveAge);
            Assert.DoesNotContain(createActivity.TagObjects, t => t.Key == BrighterSemanticConventions.Operation);
            Assert.DoesNotContain(createActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessagingOperationName);
        }

        //check the tags for the outstanding messages span
        Assert.True(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.DispatchedMessages.ToSpanName()));
        Assert.True(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable));
        Assert.True(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()));
        Assert.True(osCheckActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName));

        //check the tags for the delete messages span
        Assert.True(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Delete.ToSpanName()));
        Assert.True(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable));
        Assert.True(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()));
        Assert.True(deleteActivity?.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName));
        
    }
}
