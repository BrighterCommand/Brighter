using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.InMemory.Tests.Scheduler;

[Category("InMemory")]
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
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication{ Topic = _routingKey, RequestType = typeof(MyEvent) })
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var trace = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = trace, 
            //We need to set the outbox entries time to live to be greater than the re-schedule time, otherwise the test will fail
            EntryTimeToLive = TimeSpan.FromHours(3)
        };

        var outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            new FindPublicationByPublicationTopicOrRequestType(),
            _outbox
        );

        _processor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            outboxBus,
            _scheduler
        );
    }

    [Test]
    public async Task When_scheduler_a_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That(id.Length).IsNotEqualTo(0);

        await Assert.That(_internalBus.Stream(_routingKey)).IsEmpty();

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        await Assert.That(_internalBus.Stream(_routingKey)).IsNotEmpty();
        
        var actual = await _outbox.GetAsync(message.Id, new RequestContext());

        await Assert.That(actual.Body).IsEqualTo(message.Body);
        await Assert.That(actual.Id).IsEqualTo(message.Id);
        await Assert.That(actual.Persist).IsEqualTo(message.Persist);
        await Assert.That(actual.Redelivered).IsEqualTo(message.Redelivered);
        await Assert.That(actual.DeliveryTag).IsEqualTo(message.DeliveryTag);
        await Assert.That(actual.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(actual.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(actual.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(actual.Header.ReplyTo).IsEqualTo(message.Header.ReplyTo);
        await Assert.That(actual.Header.ContentType).IsEqualTo(message.Header.ContentType);
        await Assert.That(actual.Header.HandledCount).IsEqualTo(message.Header.HandledCount);
    }

    [Test]
    public async Task When_scheduler_a_message_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        await Assert.That(id.Length).IsNotEqualTo(0);

        await Assert.That(_internalBus.Stream(_routingKey)).IsEmpty();

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        await Assert.That(_internalBus.Stream(_routingKey)).IsNotEmpty();

        var actual = await _outbox.GetAsync(req.Id, new RequestContext());

        await Assert.That(actual.Body).IsEqualTo(message.Body);
        await Assert.That(actual.Id).IsEqualTo(message.Id);
        await Assert.That(actual.Persist).IsEqualTo(message.Persist);
        await Assert.That(actual.Redelivered).IsEqualTo(message.Redelivered);
        await Assert.That(actual.DeliveryTag).IsEqualTo(message.DeliveryTag);
        await Assert.That(actual.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(actual.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(actual.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(actual.Header.ReplyTo).IsEqualTo(message.Header.ReplyTo);
        await Assert.That(actual.Header.ContentType).IsEqualTo(message.Header.ContentType);
        await Assert.That(actual.Header.HandledCount).IsEqualTo(message.Header.HandledCount);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That(id.Length).IsNotEqualTo(0);
        await Assert.That(_internalBus.Stream(_routingKey)).IsEmpty();

        scheduler.ReScheduler(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromHours(1)));

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        await Assert.That(_internalBus.Stream(_routingKey)).IsEmpty();

        _timeProvider.Advance(TimeSpan.FromHours(2));

        await Assert.That(_internalBus.Stream(_routingKey)).IsNotEmpty();
        
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());

        await Assert.That(actual.Body).IsEqualTo(message.Body);
        await Assert.That(actual.Id).IsEqualTo(message.Id);
        await Assert.That(actual.Persist).IsEqualTo(message.Persist);
        await Assert.That(actual.Redelivered).IsEqualTo(message.Redelivered);
        await Assert.That(actual.DeliveryTag).IsEqualTo(message.DeliveryTag);
        await Assert.That(actual.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(actual.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(actual.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(actual.Header.ReplyTo).IsEqualTo(message.Header.ReplyTo);
        await Assert.That(actual.Header.ContentType).IsEqualTo(message.Header.ContentType);
        await Assert.That(actual.Header.HandledCount).IsEqualTo(message.Header.HandledCount);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromHours(1));

        await Assert.That(id.Length).IsNotEqualTo(0);
        await Assert.That(_internalBus.Stream(_routingKey)).IsEmpty();

        scheduler.ReScheduler(id, TimeSpan.FromHours(1));

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        await Assert.That(_internalBus.Stream(_routingKey)).IsEmpty();

        _timeProvider.Advance(TimeSpan.FromHours(2));
        await Assert.That(_internalBus.Stream(_routingKey)).IsNotEmpty();

        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
        await Assert.That(actual.Body).IsNotEqualTo(expected.Body);
        await Assert.That(actual.Id).IsNotEqualTo(expected.Id);
        await Assert.That(actual.Header.MessageType).IsNotEqualTo(expected.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsNotEqualTo(expected.Header.Topic);
    }

    [Test]
    public async Task When_cancel_scheduler_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That(id.Length).IsNotEqualTo(0);

        scheduler.Cancel(id);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        
        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
        await Assert.That(actual.Body).IsEqualTo(expected.Body);
        await Assert.That(actual.Id).IsEqualTo(expected.Id);
        await Assert.That(actual.Header.MessageType).IsEqualTo(expected.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(expected.Header.Topic);
    }


    [Test]
    public async Task When_cancel_scheduler_request_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromHours(1));

        await Assert.That(id.Length).IsNotEqualTo(0);

        scheduler.Cancel(id);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
        await Assert.That(actual.Body).IsEqualTo(expected.Body);
        await Assert.That(actual.Id).IsEqualTo(expected.Id);
        await Assert.That(actual.Persist).IsEqualTo(expected.Persist);
        await Assert.That(actual.Redelivered).IsEqualTo(expected.Redelivered);
        await Assert.That(actual.DeliveryTag).IsEqualTo(expected.DeliveryTag);
        await Assert.That(actual.Header.MessageType).IsEqualTo(expected.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(expected.Header.Topic);
        await Assert.That(actual.Header.TimeStamp).IsEqualTo(expected.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(actual.Header.CorrelationId).IsEqualTo(expected.Header.CorrelationId);
        await Assert.That(actual.Header.ReplyTo).IsEqualTo(expected.Header.ReplyTo);
        await Assert.That(actual.Header.ContentType).IsEqualTo(expected.Header.ContentType);
        await Assert.That(actual.Header.HandledCount).IsEqualTo(expected.Header.HandledCount);
    }
}
