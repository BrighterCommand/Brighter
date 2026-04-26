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
using Paramore.Brighter.Core.Tests.CommandProcessors.Post;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Clear;
[NotInParallel]
public class AsyncCommandProcessorClearObservabilityTests
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly RoutingKey _topic = new("MyCommand");
    private readonly InMemoryMessageProducer _messageProducer;
    private readonly InternalBus _internalBus = new();
    public AsyncCommandProcessorClearObservabilityTests()
    {
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
        InMemoryOutbox outbox = new(timeProvider)
        {
            Tracer = tracer
        };
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var type = new CloudEventsType("io.goparamore.brighter.myevent");
        _messageProducer = new InMemoryMessageProducer(_internalBus, new Publication { Source = new Uri("http://localhost"), RequestType = typeof(MyEvent), Topic = _topic, Type = type, });
        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> { { new ProducerKey(_topic, type), _messageProducer } });
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, new ResiliencePipelineRegistry<string>().AddBrighterDefault(), messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), outbox, maxOutStandingMessages: -1);
        _commandProcessor = new Brighter.CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Clearing_A_Message_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var @event = new MyEvent();
        var context = new RequestContext
        {
            Span = parentActivity
        };
        //act
        var messageId = await _commandProcessor.DepositPostAsync(@event, context);
        //reset the parent span as deposit and clear are siblings
        context.Span = parentActivity;
        await _commandProcessor.ClearOutboxAsync([messageId], context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert 
        await Assert.That(_exportedActivities.Count).IsEqualTo(8);
        await Assert.That(_exportedActivities).Contains(a => a.Source.Name == "Paramore.Brighter");
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        await Assert.That(createActivity).IsNotNull();
        await Assert.That(createActivity.ParentId).IsEqualTo(parentActivity?.Id);
        await Assert.That(createActivity.Tags).Contains(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" });
        var events = createActivity.Events.ToList();
        //retrieving the message should be an event on the batch
        var message = _internalBus.Stream(new RoutingKey(_topic)).Single();
        var getEvent = events.Single(e => e.Name == BoxDbOperation.Get.ToSpanName());
        await Assert.That(getEvent.Tags).Contains(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false);
        await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.OutboxType && a.Value as string == "async");
        await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessageId && a.Value as string == message.Id.Value);
        await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value?.ToString() == message.Header.Topic.ToString());
        await Assert.That(getEvent.Tags).Contains(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length);
        await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessageBody && a.Value as string == message.Body.Value);
        await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessageType && a.Value as string == message.Header.MessageType.ToString());
        await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value as string == message.Header.PartitionKey.Value);
        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        await Assert.That(clearActivity).IsNotNull();
        await Assert.That(clearActivity.Tags).Contains(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" });
        await Assert.That(clearActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId.Value);
        //there should be a span in the Db for retrieving the message
        var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.Get.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Get.ToSpanName());
        await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
        await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities.Single(a => a.DisplayName == $"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        await Assert.That(producerActivity.ParentId).IsEqualTo(clearActivity.Id);
        await Assert.That(producerActivity.Kind).IsEqualTo(ActivityKind.Producer);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName());
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id.Value);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString());
        await Assert.That(producerActivity.TagObjects).Contains(t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == _topic.Value);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey.Value);
        await Assert.That(producerActivity.TagObjects).Contains(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId.Value);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeMessageId && t.Value as string == message.Id.Value);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeSource && t.Value as Uri == _messageProducer.Publication.Source);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeVersion && t.Value as string == "1.0");
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeSubject && t.Value as string == _messageProducer.Publication.Subject);
        await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeType && t.Value as string == _messageProducer.Publication.Type);
        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name == $"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName());
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingSystem && t.Value as string == MessagingSystem.InternalBus.ToMessagingSystemName());
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value?.ToString() == _topic.Value);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey.Value);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id.Value);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString());
        await Assert.That(produceEvent.Tags).Contains(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId.Value);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeMessageId && t.Value as string == message.Id.Value);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeSource && t.Value as Uri == _messageProducer.Publication.Source);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeVersion && t.Value as string == "1.0");
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeSubject && t.Value as string == _messageProducer.Publication.Subject);
        await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeType && t.Value as string == _messageProducer.Publication.Type);
        //There should be  a span event to mark as dispatched
        var markAsDispatchedActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.MarkDispatched.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.MarkDispatched.ToSpanName());
        await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
        await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
    }
}