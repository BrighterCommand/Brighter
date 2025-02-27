using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Scheduler;

[Trait("Category", "InMemory")]
[Collection("CommandProcess")]
public class InMemorySchedulerMessageTests
{
    private readonly InMemorySchedulerFactory _scheduler;
    private readonly IAmACommandProcessor _processor;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    private readonly RoutingKey _routingKey;
    private readonly FakeTimeProvider _timeProvider;

    public InMemorySchedulerMessageTests()
    {
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        _scheduler = new InMemorySchedulerFactory { TimeProvider = _timeProvider };

        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyEventHandler(new Dictionary<string, string>()),
            _ => new FireSchedulerMessageHandler(_processor!));

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();
        subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

        var policyRegistry = new PolicyRegistry
        {
            [CommandProcessor.RETRYPOLICY] = Policy.Handle<Exception>().Retry(),
            [CommandProcessor.CIRCUITBREAKER] =
                Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryProducer(_internalBus, _timeProvider)
            {
                Publication = { Topic = _routingKey, RequestType = typeof(MyEvent) }
            }
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var trace = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = trace };

        var outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            _outbox
        );

        CommandProcessor.ClearServiceBus();
        _processor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            outboxBus,
            _scheduler
        );
    }

    [Fact]
    public void When_scheduler_a_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        id.Should().NotBeNullOrEmpty();

        _internalBus.Stream(_routingKey).Should().BeEmpty();

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        _outbox.Get(message.Id, new RequestContext())
            .Should().BeEquivalentTo(message);

        _internalBus.Stream(_routingKey).Should().NotBeEmpty();
    }

    [Fact]
    public void When_scheduler_a_message_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        id.Should().NotBeNullOrEmpty();

        _internalBus.Stream(_routingKey).Should().BeEmpty();

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        _internalBus.Stream(_routingKey).Should().NotBeEmpty();

        _outbox.Get(req.Id, new RequestContext())
            .Should().BeEquivalentTo(message);
    }

    [Fact]
    public void When_reschedule_a_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        id.Should().NotBeNullOrEmpty();
        _internalBus.Stream(_routingKey).Should().BeEmpty();

        scheduler.ReScheduler(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromHours(1)));

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        _internalBus.Stream(_routingKey).Should().BeEmpty();

        _timeProvider.Advance(TimeSpan.FromHours(2));

        _internalBus.Stream(_routingKey).Should().NotBeEmpty();
        _outbox.Get(req.Id, new RequestContext())
            .Should().BeEquivalentTo(message);
    }

    [Fact]
    public void When_reschedule_a_message_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromHours(1));

        id.Should().NotBeNullOrEmpty();
        _internalBus.Stream(_routingKey).Should().BeEmpty();

        scheduler.ReScheduler(id, TimeSpan.FromHours(1));

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        _internalBus.Stream(_routingKey).Should().BeEmpty();

        _timeProvider.Advance(TimeSpan.FromHours(2));
        _internalBus.Stream(_routingKey).Should().NotBeEmpty();

        _outbox.Get(req.Id, new RequestContext())
            .Should().NotBeEquivalentTo(new Message());
    }

    [Fact]
    public void When_cancel_scheduler_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        id.Should().NotBeNullOrEmpty();

        scheduler.Cancel(id);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        _outbox.Get(req.Id, new RequestContext())
            .Should().BeEquivalentTo(new Message());
    }


    [Fact]
    public void When_cancel_scheduler_request_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromHours(1));

        id.Should().NotBeNullOrEmpty();

        scheduler.Cancel(id);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        _outbox.Get(req.Id, new RequestContext())
            .Should().BeEquivalentTo(new Message());
    }
}
