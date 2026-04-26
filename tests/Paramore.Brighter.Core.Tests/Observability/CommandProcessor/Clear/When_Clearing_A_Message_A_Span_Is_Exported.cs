using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
public class CommandProcessorClearObservabilityTests
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly InternalBus _internalBus = new();
    private readonly FakeTimeProvider _timeProvider;
    private readonly RoutingKey _routingKey;
    private readonly Uri publicationSource = new Uri("http://localhost");
    private CloudEventsType? _publicationType;
    public CommandProcessorClearObservabilityTests()
    {
        _routingKey = new RoutingKey("MyEvent");
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        _timeProvider = new FakeTimeProvider();
    }

    [Test]
    [Arguments(InstrumentationOptions.All)]
    [Arguments(InstrumentationOptions.None)]
    public async Task When_Clearing_A_Message_A_Span_Is_Exported(InstrumentationOptions options)
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        var @event = new MyEvent();
        var context = new RequestContext
        {
            Span = parentActivity
        };
        var commandProcessor = CreateCommandProcessor(options);
        //act
        var messageId = commandProcessor.DepositPost(@event, context);
        //reset the parent span as deposit and clear are siblings
        context.Span = parentActivity;
        commandProcessor.ClearOutbox([messageId], context);
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert 
        await Assert.That(_exportedActivities.Count).IsEqualTo(8);
        await Assert.That(_exportedActivities).Contains(a => a.Source.Name == "Paramore.Brighter");
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        await Assert.That(createActivity).IsNotNull();
        await Assert.That(createActivity.ParentId).IsEqualTo(parentActivity?.Id);
        var events = createActivity.Events.ToList();
        //retrieving the message batch should be an event
        var message = _internalBus.Stream(new RoutingKey("MyEvent")).Single();
        var getEvent = events.Single(e => e.Name == BoxDbOperation.Get.ToSpanName());
        if (options == InstrumentationOptions.None)
            await Assert.That(getEvent.Tags).IsEmpty();
        if (options.HasFlag(InstrumentationOptions.RequestBody))
            await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessageBody && a.Value as string == message.Body.Value);
        if (options.HasFlag(InstrumentationOptions.Brighter))
        {
            await Assert.That(getEvent.Tags).Contains(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false);
            await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.OutboxType && a.Value as string == "sync");
        }

        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessageId && a.Value as string == message.Id.Value);
            await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value?.ToString() == message.Header.Topic.Value);
            await Assert.That(getEvent.Tags).Contains(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length);
            await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessageType && a.Value as string == message.Header.MessageType.ToString());
            await Assert.That(getEvent.Tags).Contains(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value as string == message.Header.PartitionKey.Value);
        }

        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        await Assert.That(clearActivity).IsNotNull();
        if (options == InstrumentationOptions.None)
            await Assert.That(createActivity.Tags).IsEmpty();
        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            await Assert.That(clearActivity.Tags).Contains(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" });
            await Assert.That(clearActivity.Tags).Contains(t => t is { Key: BrighterSemanticConventions.MessagingOperationType, Value: "clear" });
            await Assert.That(clearActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId);
        }
        else
        {
            await Assert.That(clearActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.Operation);
            await Assert.That(clearActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.MessagingOperationType);
            await Assert.That(clearActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.MessageId);
        }

        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            //there should be a span in the Db for retrieving the message
            var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.Get.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
            await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Get.ToSpanName());
            await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
            await Assert.That(outBoxActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
        }

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities.Single(a => a.DisplayName == $"{"MyEvent"} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        await Assert.That(producerActivity.ParentId).IsEqualTo(clearActivity.Id);
        await Assert.That(producerActivity.Kind).IsEqualTo(ActivityKind.Producer);
        if (options == InstrumentationOptions.None)
            await Assert.That(producerActivity.Tags).IsEmpty();
        if (options.HasFlag(InstrumentationOptions.RequestBody))
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value);
        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id.Value);
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString());
            await Assert.That(producerActivity.TagObjects).Contains(t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == "MyEvent");
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey.Value);
            await Assert.That(producerActivity.TagObjects).Contains(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId.Value);
        }

        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName());
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id.Value);
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == publicationSource);
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0");
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == null);
            await Assert.That(producerActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _publicationType);
        }

        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name == $"{"MyEvent"} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        if (options == InstrumentationOptions.None)
            await Assert.That(produceEvent.Tags).IsEmpty();
        if (options.HasFlag(InstrumentationOptions.RequestBody))
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value);
        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingOperationType && (string)t.Value == CommandProcessorSpanOperation.Publish.ToSpanName());
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingSystem && (string)t.Value == MessagingSystem.InternalBus.ToMessagingSystemName());
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingDestination && (RoutingKey)t.Value == "MyEvent");
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == message.Header.PartitionKey.Value);
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id.Value);
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.MessageType && (string)t.Value == message.Header.MessageType.ToString());
            await Assert.That(produceEvent.Tags).Contains(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.ConversationId && (string)t.Value == message.Header.CorrelationId.Value);
        }

        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id.Value);
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == publicationSource);
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0");
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == null);
            await Assert.That(produceEvent.Tags).Contains(t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _publicationType);
        }

        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            //There should be  a span event to mark as dispatched
            var markAsDispatchedActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.MarkDispatched.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
            await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.MarkDispatched.ToSpanName());
            await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
            await Assert.That(markAsDispatchedActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
        }
    }

    private Brighter.CommandProcessor CreateCommandProcessor(InstrumentationOptions instrumentationOptions)
    {
        _publicationType = new CloudEventsType("io.goparamore.brighter.myevent");
        var messageProducer = new InMemoryMessageProducer(_internalBus, new Publication { Source = publicationSource, RequestType = typeof(MyEvent), Topic = _routingKey, Type = _publicationType, }, instrumentationOptions);
        var registry = new SubscriberRegistry();
        var handlerFactory = new PostCommandTests.EmptyHandlerFactorySync();
        var retryPolicy = Policy.Handle<Exception>().Retry();
        var policyRegistry = new PolicyRegistry
        {
            {
                Brighter.CommandProcessor.RETRYPOLICY,
                retryPolicy
            }
        };
        var tracer = new BrighterTracer(_timeProvider);
        InMemoryOutbox outbox = new(_timeProvider);
        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()), null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> { { new ProducerKey(_routingKey, _publicationType), messageProducer } });
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, new ResiliencePipelineRegistry<string>().AddBrighterDefault(), messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), outbox, maxOutStandingMessages: -1, instrumentationOptions: instrumentationOptions);
        return new Brighter.CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: instrumentationOptions);
    }
}