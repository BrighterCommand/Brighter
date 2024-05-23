using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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
using MyEvent = Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyEvent;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Deposit;

[Collection("Observability")]
public class CommandProcessorDepositObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly InMemoryOutbox _outbox;

    public CommandProcessorDepositObservabilityTests()
    {
        const string topic = "MyEvent";
        
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        BrighterTracer tracer = new();
       
        Brighter.CommandProcessor.ClearServiceBus();
        
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler());
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};
        
        TimeProvider timeProvider = new FakeTimeProvider();
        _outbox = new InMemoryOutbox(new BrighterTracer(), timeProvider);
        
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            {topic, new FakeMessageProducer{Publication = { Topic = new RoutingKey(topic), RequestType = typeof(MyEvent)}}}
        });
        
        IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry, 
            messageMapperRegistry, 
            new EmptyMessageTransformerFactory(), 
            new EmptyMessageTransformerFactoryAsync(),
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
    public void When_Depositing_A_Request_A_Span_Is_Exported()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterTracerSpanTests");
        
        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        _commandProcessor.DepositPost(@event, context);
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();
        
        //assert
        _exportedActivities.Count.Should().Be(3);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        var depositActivity = _exportedActivities.Single(a => a.DisplayName == $"{nameof(MyEvent)} {CommandProcessorSpanOperation.Deposit.ToSpanName()}");
        depositActivity.Should().NotBeNull();
        depositActivity.ParentId.Should().Be(parentActivity?.Id);
        
        depositActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestId && t.Value == @event.Id).Should().BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.RequestType, Value: nameof(MyEvent) }).Should().BeTrue(); 
        depositActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.RequestBody && t.Value == JsonSerializer.Serialize(@event)).Should().BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.Operation, Value: "deposit" }).Should().BeTrue();

        var events = depositActivity.Events.ToList();
        events.Count.Should().Be(2);
        
        //mapping a message should be an event
        var mapperEvent = events.Single(e => e.Name == $"{nameof(MyEventMessageMapper)}");
        mapperEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MapperName && (string)a.Value == nameof(MyEventMessageMapper)).Should().BeTrue();
        mapperEvent.Tags.Any(a => a.Key == BrighterSemanticConventions.MapperType && (string)a.Value == "sync").Should().BeTrue();
        
        //depositing a message should be an event
        var message = _outbox.OutstandingMessages(0, context).Single();
        var depositEvent = events.Single(e => e.Name == BrighterSemanticConventions.AddToOutbox);
        depositEvent.Tags.Any(a => a.Value != null && a.Key == BrighterSemanticConventions.OutboxSharedTransaction && (bool)a.Value).Should().BeTrue();
        depositActivity.Tags.Any(t => t is { Key: BrighterSemanticConventions.OutboxType, Value: "sync" }).Should().BeTrue();
        depositActivity.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageId && a.Value == message.Id ).Should().BeTrue();
        depositActivity.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestination && a.Value == message.Header.Topic).Should().BeTrue();
        depositActivity.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBodySize && a.Value == Convert.ToString(message.Body.Bytes.Length)).Should().BeTrue();
        depositActivity.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageBody && a.Value == message.Body.Value).Should().BeTrue();
        depositActivity.Tags.Any(a => a.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && a.Value == message.Header.PartitionKey).Should().BeTrue();
        depositActivity.Tags.Any(a => a.Key == BrighterSemanticConventions.MessageHeaders && a.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();

        //-- there should be a span for the outbox itself to use for its call; even in-memory here; should use <db.operation> <db.name> for the span name
        var dbOperation = "Add";
        var dbName = "outbox";
        var outBoxActivity = _exportedActivities.Single(a => a.DisplayName == $"{dbOperation} {dbName}");
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == "in_memory").Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == dbName).Should().BeTrue();
        outBoxActivity.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == dbOperation).Should().BeTrue();

    }
}
