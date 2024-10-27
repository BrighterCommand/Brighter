﻿using System;
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

[Collection("Observability")]
public class AsyncCommandProcessorClearObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly RoutingKey _topic = new("MyCommand");
    private readonly InMemoryProducer _producer;
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

        _producer = new InMemoryProducer(_internalBus, timeProvider)
        {
            Publication =
            {
                Source = new Uri("http://localhost"),
                RequestType = typeof(MyEvent),
                Topic = _topic,
                Type = nameof(MyEvent),
            }
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
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
            outbox,
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
        _exportedActivities.Count.Should().Be(8);
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
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.OutboxType && a.Value as string == "async" ).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && a.Value as string == message.Id ).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value?.ToString() == message.Header.Topic.ToString()).Should().BeTrue();
        depositEvent.Tags.Any(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && a.Value as string == message.Body.Value).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageType && a.Value as string == message.Header.MessageType.ToString()).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value as string == message.Header.PartitionKey).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && a.Value as string == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        
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
        
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName()).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString()).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessagingDestination } && t.Value.ToString() == _topic.Value.ToString()).Should().BeTrue(); 
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && t.Value as string == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId).Should().BeTrue();
        
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeMessageId && t.Value as string == message.Id).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeSource && t.Value as Uri == _producer.Publication.Source).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeVersion && t.Value as string == "1.0").Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeSubject && t.Value as string == _producer.Publication.Subject).Should().BeTrue();
        producerActivity.TagObjects.Any(t => t.Key == BrighterSemanticConventions.CeType && t.Value as string == _producer.Publication.Type).Should().BeTrue();
        
        //there should be an event in the producer for producing the message
        var produceEvent = producerActivity.Events.Single(e => e.Name ==$"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value as string == CommandProcessorSpanOperation.Publish.ToSpanName()).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingSystem && t.Value as string == MessagingSystem.InternalBus.ToMessagingSystemName()).Should().BeTrue();          
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value?.ToString() == _topic.Value.ToString()).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && t.Value as string == message.Header.PartitionKey).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value as string == message.Id).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageType && t.Value as string == message.Header.MessageType.ToString()).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && t.Value as string == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        produceEvent.Tags.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)t.Value == message.Body.Bytes.Length).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBody && t.Value as string == message.Body.Value).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.ConversationId && t.Value as string == message.Header.CorrelationId).Should().BeTrue();
        
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeMessageId && t.Value as string == message.Id).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeSource && t.Value as Uri == _producer.Publication.Source).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeVersion && t.Value as string == "1.0").Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeSubject && t.Value as string == _producer.Publication.Subject).Should().BeTrue();
        produceEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.CeType && t.Value as string == _producer.Publication.Type).Should().BeTrue();
        
        //There should be  a span event to mark as dispatched
        var markAsDispatchedActivity = _exportedActivities.Single(a => a.DisplayName == $"{OutboxDbOperation.MarkDispatched.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.MarkDispatched.ToSpanName()).Should().BeTrue();
        markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable).Should().BeTrue();
        markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()).Should().BeTrue();
        markAsDispatchedActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.DbName).Should().BeTrue();

    }
}
