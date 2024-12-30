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

[Collection("Observability")]
public class AsyncCommandProcessorBulkClearOutstandingObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly string _topic;
    private readonly InternalBus _internalBus = new();
    private readonly IAmAnOutboxProducerMediator _mediator;

    public AsyncCommandProcessorBulkClearOutstandingObservabilityTests()
    {
        _topic = "MyEvent";
        
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

        var routingKey = new RoutingKey(_topic);
        InMemoryProducer producer = new(_internalBus, timeProvider)
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
            {routingKey, producer}
        });
        
        _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
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
            _mediator,
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
    }
    
    [Fact(Skip = "This test is fragile due to background processing")]
    //[Fact]
    public async Task When_Clearing_Outstanding_Messages_Spans_Are_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var eventOne = new MyEvent();
        var eventTwo = new MyEvent();
        var eventThree = new MyEvent();

        var context = new RequestContext { Span = parentActivity };

        //act
        await _commandProcessor.DepositPostAsync(new[]{eventOne, eventTwo, eventThree}, context);
        
        //reset the parent span as deposit and clear are siblings
        
        context.Span = parentActivity;
        await _mediator.ClearOutstandingFromOutboxAsync(3, TimeSpan.Zero, useBulk: true, requestContext: context);

        await Task.Delay(3000);     //allow bulk clear to run -- can make test fragile
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert 
        //_exportedActivities.Count.Should().Be(18);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        createActivity.Should().NotBeNull();
        
        //there should be a clear span for the batch of messages
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        
        //retrieving the messages should be an event
        var events = clearActivity.Events.ToList();
        var messages = _internalBus.Stream(new RoutingKey(_topic)).ToArray();
        
        var depositEvents = events.Where(e => e.Name == OutboxDbOperation.OutStandingMessages.ToSpanName()).ToArray();
        depositEvents.Length.Should().Be(messages.Length);
        
        foreach (var message in messages)
        {
            var depositEvent = depositEvents.Single(e => e.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id));
            depositEvent.Tags.Any(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.OutboxType && (string)a.Value == "async" ).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id ).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && (string)a.Value == message.Header.Topic).Should().BeTrue();
            depositEvent.Tags.Any(a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && (string)a.Value == message.Body.Value).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageType && (string)a.Value == message.Header.MessageType.ToString()).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)a.Value == message.Header.PartitionKey).Should().BeTrue();
            depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && (string)a.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        }
        //there should be a span in the Db for retrieving the message
        var outBoxActivity = _exportedActivities
            .Single(a =>
                a.DisplayName == $"{OutboxDbOperation.OutStandingMessages.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}"
            );
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.OutStandingMessages.ToSpanName()).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName()).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.DbName).Should().BeTrue();

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities
            .Single(a => a.DisplayName == $"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        
        var producerEvents = producerActivity.Events.ToArray();
        producerEvents.Length.Should().Be(3);     
    }
}
