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
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Clear;

[Collection("Observability")]
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
            .RetryAsync();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICYASYNC, retryPolicy}};

        var timeProvider  = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        InMemoryOutbox outbox = new(timeProvider){Tracer = tracer};
        
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var type = new CloudEventsType("io.goparamore.brighter.myevent");
        _messageProducer = new InMemoryMessageProducer(_internalBus, timeProvider,
            new Publication
            {
                Source = new Uri("http://localhost"),
                RequestType = typeof(MyEvent),
                Topic = _topic,
                Type = type,
            });

        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer>
        {
            {new ProducerKey(_topic, type), _messageProducer}
        });
        
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(), 
            messageMapperRegistry, 
            new EmptyMessageTransformerFactory(), 
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            maxOutStandingMessages: -1
        );
        
        _commandProcessor = new Brighter.CommandProcessor(
            registry, 
            handlerFactory, 
            new InMemoryRequestContextFactory(),
            policyRegistry, 
            new ResiliencePipelineRegistry<string>(),
            bus,
            new InMemorySchedulerFactory(),
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }
    
    [Fact]
    public async Task When_Clearing_A_Message_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        var messageId = await _commandProcessor.DepositPostAsync(@event, context);
        
        //reset the parent span as deposit and clear are siblings
        
        context.Span = parentActivity;
        await _commandProcessor.ClearOutboxAsync([messageId], context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert 
        Assert.Equal(8, _exportedActivities.Count);
        Assert.Contains(_exportedActivities, a => a.Source.Name == "Paramore.Brighter");
        
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        Assert.Equal(parentActivity?.Id, createActivity.ParentId);
        Assert.Contains(createActivity.Tags, t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" });

        
        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        Assert.NotNull(clearActivity);
        Assert.Contains(clearActivity.Tags, t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" });
        Assert.Contains(clearActivity.Tags, t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId.Value);

        var events = clearActivity.Events.ToList();
        
        //retrieving the message should be an event
        var message = _internalBus.Stream(new RoutingKey(_topic)).Single();
        var depositEvent = events.Single(e => e.Name == BoxDbOperation.Get.ToSpanName());
        Assert.Contains(depositEvent.Tags, a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false);
        Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.OutboxType && a.Value as string == "async");
        Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageId && a.Value as string == message.Id.Value);
        Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value?.ToString() == message.Header.Topic.ToString());
        Assert.Contains(depositEvent.Tags, a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length);
        Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageBody && a.Value as string == message.Body.Value);
        Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageType && a.Value as string == message.Header.MessageType.ToString());
        Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value as string == message.Header.PartitionKey.Value);
        
        //there should be a span in the Db for retrieving the message
        var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.Get.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Get.ToSpanName());
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities.Single(a => a.DisplayName == $"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        Assert.Equal(clearActivity.Id, producerActivity.ParentId);
        Assert.Equal(ActivityKind.Producer, producerActivity.Kind);
        
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName());
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id.Value);
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString());
        Assert.Contains(producerActivity.TagObjects, t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == _topic.Value); 
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey.Value);
        Assert.Contains(producerActivity.TagObjects, t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value);
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId.Value);
        
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeMessageId && t.Value as string == message.Id.Value);
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeSource && t.Value as Uri == _messageProducer.Publication.Source);
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeVersion && t.Value as string == "1.0");
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeSubject && t.Value as string == _messageProducer.Publication.Subject);
        Assert.Contains(producerActivity.TagObjects, t => t.Key == BrighterSemanticConventions.CeType && t.Value as string == _messageProducer.Publication.Type);
        
        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name ==$"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName());
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessagingSystem && t.Value as string == MessagingSystem.InternalBus.ToMessagingSystemName());          
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value?.ToString() == _topic.Value);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey.Value);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id.Value);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString());
        Assert.Contains(produceEvent.Tags, t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId.Value);
        
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeMessageId && t.Value as string == message.Id.Value);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeSource && t.Value as Uri == _messageProducer.Publication.Source);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeVersion && t.Value as string == "1.0");
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeSubject && t.Value as string == _messageProducer.Publication.Subject);
        Assert.Contains(produceEvent.Tags, t => t.Key == BrighterSemanticConventions.CeType && t.Value as string == _messageProducer.Publication.Type);
        
        //There should be  a span event to mark as dispatched
        var markAsDispatchedActivity = _exportedActivities.Single(a => a.DisplayName == $"{BoxDbOperation.MarkDispatched.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.MarkDispatched.ToSpanName());
        Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
        Assert.Contains(markAsDispatchedActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);

    }
}
