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
using Baggage = OpenTelemetry.Baggage;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Clear;

public class MessageDispatchPropogateContextTests  
{
    private readonly List<Activity> _exportedActivities = [];
    private readonly TracerProvider _traceProvider;
    private readonly InternalBus _internalBus = new();
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly RoutingKey _routingKey;
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;

    public MessageDispatchPropogateContextTests()
    {
        _routingKey = new RoutingKey("MyEvent");
        
        var builder = Sdk.CreateTracerProviderBuilder();

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

        var cloudEventsType = new CloudEventsType("io.goparamore.brighter.myevent");
        InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider,
            new Publication
            {
                Source = new Uri("http://localhost"),
                RequestType = typeof(MyEvent),
                Topic = _routingKey,
                Type = cloudEventsType,
            }
        );

        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer>
        {
            {new ProducerKey(_routingKey, cloudEventsType), messageProducer}
        });
        
         _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry, 
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
            _mediator,
            new InMemorySchedulerFactory(),
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );

    }

    [Fact]
    public async Task When_Producing_A_Message_Should_Propagate_Context()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("MessageDispatchPropogateContextTests");

        var @event = new MyEvent();
        var context = new RequestContext { Span = parentActivity };

        //act
        var messageId = _commandProcessor.DepositPost(@event, context);

        //reset the parent span as deposit and clear are siblings
        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("key2", "value2");
        
        context.Span = parentActivity;
        _commandProcessor.ClearOutbox([messageId], context);

        await Task.Delay(3000);     //allow bulk clear to run -- can make test fragile
        
        parentActivity?.Stop();
        
        _traceProvider.ForceFlush();

        //assert 
        var messages = _internalBus.Stream(_routingKey);
        var message = messages.FirstOrDefault(m => m.Id == messageId);
        Assert.NotNull(message);
        Assert.NotNull(message.Header.TraceParent);
        
        //? What is tracestate 
        Assert.Equal("key=value,key2=value2", message.Header.Baggage.ToString());
    }
}
