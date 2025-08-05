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
        InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider,
            new Publication
            {
                Source = new Uri("http://localhost"),
                RequestType = typeof(MyEvent),
                Topic = routingKey,
                Type = new CloudEventsType("io.goparamore.brighter.myevent")
            }
        );

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            {routingKey, messageProducer}
        });
        
        _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
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
            _mediator,
            new InMemorySchedulerFactory(),
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
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter"));
        
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        Assert.NotNull(createActivity);
        
        //there should be a clear span for the batch of messages
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{BrighterSemanticConventions.ClearMessages} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        
        //retrieving the messages should be an event
        var events = clearActivity.Events.ToList();
        var messages = _internalBus.Stream(new RoutingKey(_topic)).ToArray();
        
        var depositEvents = events.Where(e => e.Name == BoxDbOperation.OutStandingMessages.ToSpanName()).ToArray();
        Assert.Equal(messages.Length, depositEvents.Length);
        
        foreach (var message in messages)
        {
            var depositEvent = depositEvents.Single(e => e.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id));
            Assert.Contains(depositEvent.Tags, a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false);
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.OutboxType && (string)a.Value == "async");
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id);
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessagingDestination && (string)a.Value == message.Header.Topic);
            Assert.Contains(depositEvent.Tags, a => a is { Value: not null, Key: BrighterSemanticConventions.MessageBodySize } && (int)a.Value == message.Body.Bytes.Length);
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageBody && (string)a.Value == message.Body.Value);
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageType && (string)a.Value == message.Header.MessageType.ToString());
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)a.Value == message.Header.PartitionKey);
            Assert.Contains(depositEvent.Tags, a => a.Key == BrighterSemanticConventions.MessageHeaders && (string)a.Value == JsonSerializer.Serialize(message.Header));
        }
        //there should be a span in the Db for retrieving the message
        var outBoxActivity = _exportedActivities
            .Single(a =>
                a.DisplayName == $"{BoxDbOperation.OutStandingMessages.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}"
            );
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.OutStandingMessages.ToSpanName());
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.Brighter.ToDbName());
        Assert.Contains(outBoxActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);

        //there should be a span for publishing the message via the producer
        var producerActivity = _exportedActivities
            .Single(a => a.DisplayName == $"{_topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
        
        var producerEvents = producerActivity.Events.ToArray();
        Assert.Equal(3, producerEvents.Length);     
    }
}
