﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
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
            [_routingKey] = new InMemoryMessageProducer(_internalBus, _timeProvider, InstrumentationOptions.All)
            {
                Publication = { Topic = _routingKey, RequestType = typeof(MyEvent) }
            }
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
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            new FindPublicationByPublicationTopicOrRequestType(),
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

        Assert.True(id.Any());

        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));
        
        var actual = _outbox.Get(message.Id, new RequestContext());

        Assert.Equivalent(message.Body, actual.Body);
        Assert.Equal(message.Id, actual.Id);
        Assert.Equal(message.Persist, actual.Persist);
        Assert.Equal(message.Redelivered, actual.Redelivered);
        Assert.Equal(message.DeliveryTag, actual.DeliveryTag);
        Assert.Equal(message.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(message.Header.Topic, actual.Header.Topic);
        Assert.Equal(message.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(message.Header.CorrelationId, actual.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, actual.Header.ReplyTo);
        Assert.Equal(message.Header.ContentType, actual.Header.ContentType);
        Assert.Equal(message.Header.HandledCount, actual.Header.HandledCount);
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

        Assert.True(id.Any());

        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        var actual = _outbox.Get(req.Id, new RequestContext());

        Assert.Equivalent(message.Body, actual.Body);
        Assert.Equal(message.Id, actual.Id);
        Assert.Equal(message.Persist, actual.Persist);
        Assert.Equal(message.Redelivered, actual.Redelivered);
        Assert.Equal(message.DeliveryTag, actual.DeliveryTag);
        Assert.Equal(message.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(message.Header.Topic, actual.Header.Topic);
        Assert.Equal(message.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(message.Header.CorrelationId, actual.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, actual.Header.ReplyTo);
        Assert.Equal(message.Header.ContentType, actual.Header.ContentType);
        Assert.Equal(message.Header.HandledCount, actual.Header.HandledCount);
    }

    [Fact]
    public async Task When_reschedule_a_message_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        scheduler.ReScheduler(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromHours(1)));

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        _timeProvider.Advance(TimeSpan.FromHours(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));
        
        var actual = _outbox.Get(req.Id, new RequestContext());

        Assert.Equivalent(message.Body, actual.Body);
        Assert.Equal(message.Id, actual.Id);
        Assert.Equal(message.Persist, actual.Persist);
        Assert.Equal(message.Redelivered, actual.Redelivered);
        Assert.Equal(message.DeliveryTag, actual.DeliveryTag);
        Assert.Equal(message.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(message.Header.Topic, actual.Header.Topic);
        Assert.Equal(message.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(message.Header.CorrelationId, actual.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, actual.Header.ReplyTo);
        Assert.Equal(message.Header.ContentType, actual.Header.ContentType);
        Assert.Equal(message.Header.HandledCount, actual.Header.HandledCount);
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

        Assert.True((id)?.Any());
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        scheduler.ReScheduler(id, TimeSpan.FromHours(1));

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        _timeProvider.Advance(TimeSpan.FromHours(2));
        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        var expected = Message.Empty;
        var actual = _outbox.Get(req.Id, new RequestContext());
        
        Assert.NotEqual(expected.Body, actual.Body);
        Assert.NotEqual(expected.Id, actual.Id);
        Assert.NotEqual(expected.Header.MessageType, actual.Header.MessageType);
        Assert.NotEqual(expected.Header.Topic, actual.Header.Topic);
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

        Assert.True(id.Any());

        scheduler.Cancel(id);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        
        
        var expected = Message.Empty;
        var actual = _outbox.Get(req.Id, new RequestContext());
        
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(expected.Header.Topic, actual.Header.Topic);
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

        Assert.True(id.Any());

        scheduler.Cancel(id);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = _outbox.Get(req.Id, new RequestContext());
        
        Assert.Equivalent(expected.Body, actual.Body);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Persist, actual.Persist);
        Assert.Equal(expected.Redelivered, actual.Redelivered);
        Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
        Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(expected.Header.Topic, actual.Header.Topic);
        Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
        Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
        Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
        Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
    }
}
