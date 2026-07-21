using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.Post;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using MyEvent = Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyEvent;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Deposit;
[NotInParallel]
public class AsyncCommandProcessorDepositObservabilityTests
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly InMemoryOutbox _outbox;
    public AsyncCommandProcessorDepositObservabilityTests()
    {
        var routingKey = new RoutingKey("MyEvent");
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        var registry = new SubscriberRegistry();
        var handlerFactory = new PostCommandTests.EmptyHandlerFactorySync();
        var retryPolicy = Policy.Handle<Exception>().RetryAsync();
        var policyRegistry = new PolicyRegistry
        {
            {
                Brighter.CommandProcessor.RETRYPOLICYASYNC,
                retryPolicy
            }
        };
        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        _outbox = new InMemoryOutbox(timeProvider)
        {
            Tracer = tracer
        };
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(MyEvent) }) } });
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, new ResiliencePipelineRegistry<string>().AddBrighterDefault(), messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox, maxOutStandingMessages: -1);
        _commandProcessor = new Brighter.CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Depositing_A_Request_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var @event = new MyEvent();
        var context = new RequestContext
        {
            Span = parentActivity
        };
        //act
        await _commandProcessor.DepositPostAsync(@event, context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert
        await Assert.That(_exportedActivities.Count).IsEqualTo(3);
        await Assert.That(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        var depositActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Deposit.ToSpanName()}");
        await Assert.That(depositActivity).IsNotNull();
        await Assert.That(depositActivity.ParentId).IsEqualTo(parentActivity?.Id);
        await Assert.That(depositActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id)).IsTrue();
        await Assert.That(depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) })).IsTrue();
        await Assert.That(depositActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))).IsTrue();
        await Assert.That(depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "deposit" })).IsTrue();
        var events = depositActivity.Events.ToList();
        await Assert.That(events.Count).IsEqualTo(2);
        //mapping a message should be an event
        var mapperEvent = events.Single(e => e.Name == $"{nameof(MyEventMessageMapperAsync)}");
        await Assert.That(mapperEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MapperName && (string)a.Value == nameof(MyEventMessageMapperAsync))).IsTrue();
        await Assert.That(mapperEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MapperType && (string)a.Value == "async")).IsTrue();
        //depositing a message should be an event
        var message = (await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).Single();
        var depositEvent = events.Single(e => e.Name == BoxDbOperation.Add.ToSpanName());
        await Assert.That(depositEvent.Tags.Any(a => a is { Value: not null, Key: BrighterSemanticConventions.OutboxSharedTransaction } && (bool)a.Value == false)).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.OutboxType && (string)a.Value == "async")).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id)).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && (RoutingKey)a.Value == message.Header.Topic)).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length)).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && (string)a.Value == message.Body.Value)).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageType && (string)a.Value == message.Header.MessageType.ToString())).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)a.Value == message.Header.PartitionKey)).IsTrue();
        await Assert.That(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && (string)a.Value == JsonSerializer.Serialize(message.Header))).IsTrue();
        //-- there should be a span for the outbox itself to use for its call; even in-memory here; should use <db.operation> <db.name> for the span name
        var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.Add.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName())).IsTrue();
        await Assert.That(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable)).IsTrue();
        await Assert.That(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName())).IsTrue();
        await Assert.That(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName)).IsTrue();
    }
}