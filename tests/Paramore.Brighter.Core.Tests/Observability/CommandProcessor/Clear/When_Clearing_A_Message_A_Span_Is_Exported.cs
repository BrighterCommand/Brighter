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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Clear;

public class CommandProcessorClearObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly InMemoryOutbox _outbox;
    private readonly string _topic;

    public CommandProcessorClearObservabilityTests()
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
        registry.Register<MyCommand, MyCommandHandler>();
        
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler());
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};
        
        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        _outbox = new InMemoryOutbox(timeProvider){Tracer = tracer};
        
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            {_topic, new InMemoryProducer(new InternalBus(), new FakeTimeProvider())
            {
                Publication = { Topic = new RoutingKey(_topic), RequestType = typeof(MyEvent)}
            }}
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
    public void When_Clearing_A_Message_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        var messageId = _commandProcessor.DepositPost(@event, context);
        _commandProcessor.ClearOutbox([messageId], context);
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert 
        _exportedActivities.Count.Should().Be(3);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        
        //there should be a create span for the batch
        var createActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Create.ToSpanName()}");
        createActivity.Should().NotBeNull();
        createActivity.ParentId.Should().Be(parentActivity?.Id);
        
        //there should be a clear span for each message id
        var clearActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Clear.ToSpanName()}");
        clearActivity.Should().NotBeNull();
        clearActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "clear" }).Should().BeTrue();
        clearActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId).Should().BeTrue();

        var events = clearActivity.Events.ToList();
        
        //retrieving the message should be an event
        var message = _outbox.OutstandingMessages(0, context).Single();
        var depositEvent = events.Single(e => e.Name == BrighterSemanticConventions.GetFromOutbox);
        depositEvent.Tags.Any(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value == false).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.OutboxType && (string)a.Value == "sync" ).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && (string)a.Value == message.Id ).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && (string)a.Value == message.Header.Topic).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBodySize && (string)a.Value == Convert.ToString(message.Body.Bytes.Length)).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && (string)a.Value == message.Body.Value.ToString()).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageType && (string)a.Value == message.Header.MessageType.ToString()).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)a.Value == message.Header.PartitionKey).Should().BeTrue();
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && (string)a.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
        
        //we also support the cloud events identifiers here
        depositEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.CeMessageId && (string)a.Value == message.Id ).Should().BeTrue();        
        
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

        //there should be a span in the producer for producing the message


    }
}
