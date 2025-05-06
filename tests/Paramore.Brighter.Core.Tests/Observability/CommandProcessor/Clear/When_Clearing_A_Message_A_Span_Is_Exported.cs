using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly InMemoryProducer _producer;
    private readonly InternalBus _internalBus = new();

    public CommandProcessorClearObservabilityTests()
    {
        var routingKey = new RoutingKey("MyEvent");
        
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
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};

        var timeProvider  = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        InMemoryOutbox outbox = new(timeProvider){Tracer = tracer};
        
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _producer = new InMemoryProducer(_internalBus, timeProvider)
        {
            Publication =
            {
                Source = new Uri("http://localhost"),
                RequestType = typeof(MyEvent),
                Topic = routingKey,
                Type = nameof(MyEvent),
            }
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            {routingKey, _producer}
        });
        
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry, 
            messageMapperRegistry, 
            new EmptyMessageTransformerFactory(), 
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByRequestType(),
            outbox,
            maxOutStandingMessages: -1
        );
        
        _commandProcessor = new Brighter.CommandProcessor(
            registry, 
            handlerFactory, 
            new InMemoryRequestContextFactory(),
            policyRegistry, 
            bus,
            new InMemorySchedulerFactory(),
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }
    
    [Fact]
    public void When_Clearing_A_Message_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        var messageId = _commandProcessor.DepositPost(@event, context);
        
        //reset the parent span as deposit and clear are siblings
        
        context.Span = parentActivity;
        _commandProcessor.ClearOutbox([messageId], context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert 
        Assert.Equal(8, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Equal(parentActivity?.Id, createActivity.ParentId);
        Assert.True(createActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" }));
        
        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        Assert.NotNull(clearActivity);
        Assert.True(clearActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" }));
        Assert.True(clearActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId));

        var events = clearActivity.Events.ToList();
        
        //retrieving the message should be an event
        var message = _internalBus.Stream(new RoutingKey("MyEvent")).Single();
        var depositEvent = events.Single(e => e.Name == BoxDbOperation.Get.ToSpanName());
        Assert.True(depositEvent.Tags.Any(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.OutboxType && a.Value as string == "sync" ));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && a.Value as string == message.Id ));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value?.ToString() == message.Header.Topic.Value));
        Assert.True(depositEvent.Tags.Any(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && a.Value as string == message.Body.Value));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageType && a.Value as string == message.Header.MessageType.ToString()));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value as string == message.Header.PartitionKey));
        Assert.True(depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && a.Value as string == JsonSerializer.Serialize(message.Header)));
        
        //there should be a span in the Db for retrieving the message
        var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.Get.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.True(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Get.ToSpanName()));
        Assert.True(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable));
        Assert.True(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()));
        Assert.True(outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName));

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities.Single(a => a.DisplayName == $"{"MyEvent"} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        Assert.Equal(clearActivity.Id, producerActivity.ParentId);
        Assert.Equal(ActivityKind.Producer, producerActivity.Kind);
        
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName()));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString()));
        Assert.True(producerActivity.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == "MyEvent")); 
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && (string)t.Value == JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options)));
        Assert.True(producerActivity.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId));
        
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == _producer.Publication.Source));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0"));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == _producer.Publication.Subject));
        Assert.True(producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _producer.Publication.Type));
        
        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name ==$"{"MyEvent"} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && (string)t.Value == CommandProcessorSpanOperation.Publish.ToSpanName()));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingSystem && (string)t.Value == MessagingSystem.InternalBus.ToMessagingSystemName()));          
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestination && (RoutingKey)t.Value == "MyEvent"));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == message.Header.PartitionKey));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageType && (string)t.Value == message.Header.MessageType.ToString()));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && (string)t.Value == JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options)));
        Assert.True(produceEvent.Tags.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.ConversationId && (string)t.Value == message.Header.CorrelationId));
        
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == _producer.Publication.Source));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0"));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == _producer.Publication.Subject));
        Assert.True(produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _producer.Publication.Type));
        
        //There should be  a span event to mark as dispatched
        var markAsDispatchedActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.MarkDispatched.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.True(markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.MarkDispatched.ToSpanName()));
        Assert.True(markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable));
        Assert.True(markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()));
        Assert.True(markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName));
    }
}
