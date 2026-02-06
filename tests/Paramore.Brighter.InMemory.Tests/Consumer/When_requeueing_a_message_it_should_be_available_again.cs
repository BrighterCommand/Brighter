using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryConsumerRequeueTests
{
    private const string MY_TOPIC = "my topic";
    private readonly RoutingKey _routingKey;
    private readonly CommandProcessor _processor;
    private readonly InternalBus _internalBus = new();
    private readonly IAmAMessageScheduler _scheduler;
    private readonly FakeTimeProvider _timeProvider;

    public InMemoryConsumerRequeueTests()
    {
        _routingKey = new RoutingKey(MY_TOPIC);
        
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyEventHandler(new Dictionary<string, string>()),
            _ => new FireSchedulerMessageHandler(_processor!));

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();
        subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

        var policyRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication{ Topic = _routingKey, RequestType = typeof(MyEvent) })
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var trace = new BrighterTracer(_timeProvider);
        var outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = trace, EntryTimeToLive
                //We need to set the outbox entries time to live to be greater than the re-schedule time, otherwise the test will fail
                = TimeSpan.FromHours(3)
        };

        var outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox
        );

        var schedulerFactory = new InMemorySchedulerFactory { TimeProvider = _timeProvider };
        
        _processor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            policyRegistry,
            outboxBus,
            schedulerFactory
        );

        _scheduler = schedulerFactory.Create(_processor);
    }
    
    [Fact]
    public void When_requeueing_a_message_it_should_be_available_again()
    {
        //arrange
        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        _internalBus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(_routingKey, _internalBus, _timeProvider, 
            ackTimeout: TimeSpan.FromMilliseconds(1000), scheduler: _scheduler);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        consumer.Requeue(receivedMessage, TimeSpan.Zero);
        
        //assert
        Assert.Single(_internalBus.Stream(_routingKey));
        
    }
    
    [Fact]
    public void When_requeueing_a_message_with_a_delay_it_should_not_be_available_immediately()
    {
        //arrange

        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        _internalBus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(_routingKey, _internalBus, _timeProvider, 
            ackTimeout: TimeSpan.FromMilliseconds(1000), scheduler: _scheduler);
        
        //act
        var receivedMessage = consumer.Receive().Single();
        Assert.Empty(_internalBus.Stream(_routingKey));
        
        consumer.Requeue(receivedMessage, TimeSpan.FromMilliseconds(1000));
        
        //assert
        Assert.Empty(_internalBus.Stream(_routingKey));
        
        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        Assert.Single(_internalBus.Stream(_routingKey));
    }
}
