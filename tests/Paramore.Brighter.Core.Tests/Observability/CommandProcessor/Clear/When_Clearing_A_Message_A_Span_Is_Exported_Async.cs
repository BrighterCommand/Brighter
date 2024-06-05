using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
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

public class AsyncCommandProcessorClearObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly InMemoryOutbox _outbox;
    private readonly string _topic;
    private readonly InMemoryProducer _producer;
    private InternalBus _internalBus = new InternalBus();
    private Dictionary<string, string> _receivedMessages = new Dictionary<string, string>();

    public AsyncCommandProcessorClearObservabilityTests()
    {
        _topic = "MyCommand";
        
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
        _outbox = new InMemoryOutbox(timeProvider){Tracer = tracer};
        
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        _producer = new InMemoryProducer(_internalBus, timeProvider)
        {
            Publication =
            {
                Source = new Uri("http://localhost"),
                RequestType = typeof(MyEvent),
                Topic = new RoutingKey(_topic),
                Type = nameof(MyCommand),
            }
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            {_topic, _producer}
        });
        
        IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry, 
            messageMapperRegistry, 
            new EmptyMessageTransformerFactory(), 
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            _outbox,
            maxOutStandingMessages: -1
        );
        
        _commandProcessor = new Brighter.CommandProcessor(
            registry, 
            handlerFactory, 
            new InMemoryRequestContextFactory(),
            policyRegistry, 
            bus,
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
        _exportedActivities.Count.Should().Be(7);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        createActivity.Should().NotBeNull();
        createActivity.ParentId.Should().Be(parentActivity?.Id);
        createActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" }).Should().BeTrue();

        
        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        clearActivity.Should().NotBeNull();
        clearActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" }).Should().BeTrue();
        clearActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId).Should().BeTrue();

        var events = clearActivity.Events.ToList();
        
        //retrieving the message should be an event
        var message = _internalBus.Stream(new RoutingKey(_topic)).Single();
        var depositEvent = events.Single(e => e.Name == OutboxDbOperation.Get.ToSpanName());
        depositEvent.Tags.Any(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.OutboxType && (string)a.Value == "async" ).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id ).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && (string)a.Value == message.Header.Topic).Should().BeTrue();
        depositEvent.Tags.Any(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && (string)a.Value == message.Body.Value.ToString()).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageType && (string)a.Value == message.Header.MessageType.ToString()).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)a.Value == message.Header.PartitionKey).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && (string)a.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        
        //there should be a span in the Db for retrieving the message
        var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{OutboxDbOperation.Get.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.Get.ToSpanName()).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.DbName).Should().BeTrue();

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities.Single(a => a.DisplayName == $"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        producerActivity.ParentId.Should().Be(clearActivity.Id);
        producerActivity.Kind.Should().Be(ActivityKind.Producer);
        
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && (string)t.Value == CommandProcessorSpanOperation.Publish.ToSpanName()).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageType && (string)t.Value == message.Header.MessageType.ToString()).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == _topic).Should().BeTrue(); 
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == message.Header.PartitionKey).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && (string)t.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.ConversationId && (string)t.Value == message.Header.CorrelationId).Should().BeTrue();
        
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == _producer.Publication.Source).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0").Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == _producer.Publication.Subject).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _producer.Publication.Type).Should().BeTrue();
        
        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name ==$"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && (string)t.Value == CommandProcessorSpanOperation.Publish.ToSpanName()).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingSystem && (string)t.Value == MessagingSystem.InternalBus.ToMessagingSystemName()).Should().BeTrue();          
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestination && (string)t.Value == _topic).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == message.Header.PartitionKey).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageType && (string)t.Value == message.Header.MessageType.ToString()).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && (string)t.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        produceEvent.Tags.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value.ToString()).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.ConversationId && (string)t.Value == message.Header.CorrelationId).Should().BeTrue();
        
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeMessageId && (string)t.Value == message.Id).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeSource && (Uri)t.Value == _producer.Publication.Source).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeVersion && (string)t.Value == "1.0").Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeSubject && (string)t.Value == _producer.Publication.Subject).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeType && (string)t.Value == _producer.Publication.Type).Should().BeTrue();
    }
}
