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
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Clear;

[Collection("Observability")]
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

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        Brighter.CommandProcessor.ClearServiceBus();

        _timeProvider = new FakeTimeProvider();
    }

    [Theory]
    [InlineData(InstrumentationOptions.All)]
    [InlineData(InstrumentationOptions.None)]
    public void When_Clearing_A_Message_A_Span_Is_Exported(InstrumentationOptions options)
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");

        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };
        var commandProcessor = CreateCommandProcessor(options);

        //act
        var messageId = commandProcessor.DepositPost(@event, context);

        //reset the parent span as deposit and clear are siblings

        context.Span = parentActivity;
        commandProcessor.ClearOutbox([messageId], context);

        parentActivity?.Stop();

        _traceProvider.ForceFlush();

        //assert 
        Assert.Equal(8, _exportedActivities.Count);
        Assert.Contains(_exportedActivities, a => a.Source.Name == "Paramore.Brighter");

        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a =>
            a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Equal(parentActivity?.Id, createActivity.ParentId);

        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a =>
            a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        Assert.NotNull(clearActivity);
        if (options == InstrumentationOptions.None)
            Assert.Empty(createActivity.Tags);
        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            Assert.Contains(clearActivity.Tags, t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" });
            Assert.Contains(clearActivity.Tags, t => t is { Key: BrighterSemanticConventions.MessagingOperationType, Value: "clear" });
            Assert.Contains(clearActivity.Tags, t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId);
        }
        else
        {
            Assert.DoesNotContain(clearActivity.Tags, t => t.Key == BrighterSemanticConventions.Operation);
            Assert.DoesNotContain(clearActivity.Tags, t => t.Key == BrighterSemanticConventions.MessagingOperationType);
            Assert.DoesNotContain(clearActivity.Tags, t => t.Key == BrighterSemanticConventions.MessageId);
        }

        var events = clearActivity.Events.ToList();

        //retrieving the message should be an event
        var message = _internalBus.Stream(new RoutingKey("MyEvent")).Single();
        var depositEvent = events.Single(e => e.Name == BoxDbOperation.Get.ToSpanName());

        if (options == InstrumentationOptions.None)
            Assert.Empty(depositEvent.Tags);

        if (options.HasFlag(InstrumentationOptions.RequestBody))
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageBody && a.Value as string == message.Body.Value);
        if (options.HasFlag(InstrumentationOptions.Brighter))
        {
            Assert.Contains(depositEvent.Tags, a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false);
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.OutboxType && a.Value as string == "sync");
        }

        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageId && a.Value as string == message.Id.Value);
            Assert.Contains(depositEvent.Tags,
                a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value?.ToString() == message.Header.Topic.Value);
            Assert.Contains(depositEvent.Tags,
                a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length);
            Assert.Contains(depositEvent.Tags,
                a => a.Key == BrighterSemanticConventions.MessageType && a.Value as string == message.Header.MessageType.ToString());
            Assert.Contains(depositEvent.Tags,
                a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value as string == message.Header.PartitionKey.Value);
        }

        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            //there should be a span in the Db for retrieving the message
            var outBoxActivity = _exportedActivities.Single(a =>
                a.DisplayName == $"{BoxDbOperation.Get.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
            Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Get.ToSpanName());
            Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
            Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
        }

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities.Single(a => a.DisplayName == $"{"MyEvent"} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        Assert.Equal(clearActivity.Id, producerActivity.ParentId);
        Assert.Equal(ActivityKind.Producer, producerActivity.Kind);

        if (options == InstrumentationOptions.None)
            Assert.Empty(producerActivity.Tags);

        if (options.HasFlag(InstrumentationOptions.RequestBody))
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value);

        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id.Value);
            Assert.Contains(producerActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString());
            Assert.Contains(producerActivity.TagObjects,
                t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == "MyEvent");
            Assert.Contains(producerActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey.Value);
            Assert.Contains(producerActivity.TagObjects,
                t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
            Assert.Contains(producerActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId.Value);
        }

        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            Assert.Contains(producerActivity.TagObjects,
                t => t.Key == BrighterSemanticConventions.MessagingOperationType &&
                     t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName());
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id.Value);
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == publicationSource);
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0");
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == null);
            Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _publicationType);
        }

        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name == $"{"MyEvent"} {CommandProcessorSpanOperation.Publish.ToSpanName()}");

        if (options == InstrumentationOptions.None)
            Assert.Empty(produceEvent.Tags);

        if (options.HasFlag(InstrumentationOptions.RequestBody))
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value);

        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            Assert.Contains(produceEvent.Tags,
                t => t.Key == BrighterSemanticConventions.MessagingOperationType && (string)t.Value == CommandProcessorSpanOperation.Publish.ToSpanName());
            Assert.Contains(produceEvent.Tags,
                t => t.Key == BrighterSemanticConventions.MessagingSystem && (string)t.Value == MessagingSystem.InternalBus.ToMessagingSystemName());
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessagingDestination && (RoutingKey)t.Value == "MyEvent");
            Assert.Contains(produceEvent.Tags,
                t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == message.Header.PartitionKey.Value);
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id.Value);
            Assert.Contains(produceEvent.Tags,
                t => t.Key == BrighterSemanticConventions.MessageType && (string)t.Value == message.Header.MessageType.ToString());
            Assert.Contains(produceEvent.Tags,
                t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
            Assert.Contains(produceEvent.Tags,
                t => t.Key == BrighterSemanticConventions.ConversationId && (string)t.Value == message.Header.CorrelationId.Value);
        }

        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id.Value);
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == publicationSource);
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0");
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == null);
            Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _publicationType);
        }

        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            //There should be  a span event to mark as dispatched
            var markAsDispatchedActivity = _exportedActivities.Single(a =>
                a.DisplayName == $"{BoxDbOperation.MarkDispatched.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
            Assert.Contains(markAsDispatchedActivity.Tags,
                t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.MarkDispatched.ToSpanName());
            Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
            Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
        }
    }

    private Brighter.CommandProcessor CreateCommandProcessor(InstrumentationOptions instrumentationOptions)
    {
        _publicationType = new CloudEventsType("io.goparamore.brighter.myevent");
        
        var messageProducer = new InMemoryMessageProducer(
            _internalBus,
            _timeProvider,
            new Publication
            {
                Source = publicationSource, RequestType = typeof(MyEvent), Topic = _routingKey, Type = _publicationType,
            },
            instrumentationOptions
        );

        Brighter.CommandProcessor.ClearServiceBus();

        var registry = new SubscriberRegistry();

        var handlerFactory = new PostCommandTests.EmptyHandlerFactorySync();

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var policyRegistry = new PolicyRegistry { { Brighter.CommandProcessor.RETRYPOLICY, retryPolicy } };

        var tracer = new BrighterTracer(_timeProvider);
        InMemoryOutbox outbox = new(_timeProvider);

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> { { new ProducerKey(_routingKey, _publicationType), messageProducer } });

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            maxOutStandingMessages: -1,
            instrumentationOptions: instrumentationOptions
        );

        return new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            bus,
            new InMemorySchedulerFactory(),
            tracer: tracer,
            instrumentationOptions: instrumentationOptions
        );
    }
}
