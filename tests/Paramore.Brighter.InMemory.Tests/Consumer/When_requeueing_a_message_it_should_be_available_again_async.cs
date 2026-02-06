using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class AsyncInMemoryConsumerRequeueTests
{
    private const string MY_TOPIC = "my topic";
    private readonly RoutingKey _routingKey;
    private readonly CommandProcessor _processor;
    private readonly InternalBus _internalBus = new();
    private readonly IAmAMessageScheduler _scheduler;
    private readonly FakeTimeProvider _timeProvider;

    public AsyncInMemoryConsumerRequeueTests()
    {
        _routingKey = new RoutingKey(MY_TOPIC);
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);


        var handlerFactory = new SimpleHandlerFactory(
            _ => throw new ConfigurationException(),
            t =>
            {
                if (t == typeof(FireSchedulerMessageHandler))
                    return new FireSchedulerMessageHandler(_processor!);    

                if (t == typeof(MyEventHandlerAsync))
                    return new MyEventHandlerAsync(new Dictionary<string, string>());

                throw new ConfigurationException("Unknown handler type");

            });

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

        var policyRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication{ Topic = _routingKey, RequestType = typeof(MyEvent) })
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

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
            new ResiliencePipelineRegistry<string>(),
            outboxBus,
            schedulerFactory
        );
        
        _scheduler = schedulerFactory.Create(_processor);
    }
    
    
    [Fact]
    public async Task When_requeueing_a_message_it_should_be_available_again()
    {
        //arrange

        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        _internalBus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(_routingKey, _internalBus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));
        
        //act
        var receivedMessage = await consumer.ReceiveAsync();
        await consumer.RequeueAsync(receivedMessage.Single(), TimeSpan.Zero);
        
        //assert
        Assert.Single(_internalBus.Stream(_routingKey));
        
    }
    
    [Fact]
    public async Task When_requeueing_a_message_with_a_delay_it_should_not_be_available_immediately()
    {
        //arrange
        var expectedMessage = new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("a test body"));
        
        var bus = new InternalBus();
        bus.Enqueue(expectedMessage);

        var consumer = new InMemoryMessageConsumer(_routingKey, bus, _timeProvider, 
            ackTimeout: TimeSpan.FromMilliseconds(1000), scheduler:_scheduler);
        
        //act
        var receivedMessage = await consumer.ReceiveAsync();
        await consumer.RequeueAsync(receivedMessage.Single(), TimeSpan.FromMilliseconds(1000));
        
        //assert
        Assert.Empty(bus.Stream(_routingKey));
        
        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        Assert.Single(bus.Stream(_routingKey));
    }
}
