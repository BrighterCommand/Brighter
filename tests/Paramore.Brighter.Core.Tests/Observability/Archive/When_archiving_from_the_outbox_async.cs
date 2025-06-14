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
    private readonly OutboxArchiver<Message,CommittableTransaction> _archiver;
    private const double TOLERANCE = 0.000000001;

    public AsyncExternalServiceBusArchiveObservabilityTests()
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

        var producer = new InMemoryMessageProducer(internalBus, _timeProvider)
        {
            Publication = _publication
        };

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, producer } });

        var retryPolicy = Policy
            .Handle<Exception>()
            .RetryAsync();

        var policyRegistry = new PolicyRegistry { { Brighter.CommandProcessor.RETRYPOLICYASYNC, retryPolicy } };

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };
        var archiveProvider = new InMemoryArchiveProvider();

        _archiver = new OutboxArchiver<Message, CommittableTransaction>(_outbox, archiveProvider, tracer: tracer);

        _bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            _outbox,
            timeProvider:_timeProvider);
    }

    [Fact]
    public async Task When_archiving_from_the_outbox()
    {
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var context = new RequestContext();
        context.Span = parentActivity;
        
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
        await _archiver.ArchiveAsync(dispatchedSince, context);
        
         //should be no messages in the outbox
        Assert.Equal(0, _outbox.EntryCount);

        parentActivity?.Stop();

        _traceProvider.ForceFlush();

        //We should have exported matching activities
        Assert.Equal(9, _exportedActivities.Count);

        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));

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
        Assert.Contains(createActivity.TagObjects, t => t.Key == BrighterSemanticConventions.ArchiveAge && Math.Abs(Convert.ToDouble(t.Value) - dispatchedSince.TotalMilliseconds) < TOLERANCE);

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
