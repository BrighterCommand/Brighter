using System.Collections.Specialized;
using System.Text.Json;
using System.Transactions;
using Paramore.Brighter;
using Paramore.Brighter.MessageScheduler.Quartz;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using ParamoreBrighter.Quartz.Tests.TestDoubles;
using Polly;
using Polly.Registry;
using Quartz;

namespace ParamoreBrighter.Quartz.Tests;

[Collection("Scheduler")]
public class QuartzSchedulerMessageAsyncTests
{
    private readonly QuartzSchedulerFactory _scheduler;
    private readonly IAmACommandProcessor _processor;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    private readonly RoutingKey _routingKey;
    private readonly TimeProvider _timeProvider;

    public QuartzSchedulerMessageAsyncTests()
    {
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = TimeProvider.System;

        var handlerFactory = new SimpleHandlerFactoryAsync(
            type =>
            {
                if (type == typeof(MyEventHandlerAsync))
                {
                    return new MyEventHandlerAsync(new Dictionary<string, string>());
                }

                return new FireSchedulerMessageHandler(_processor!);
            });

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

        var policyRegistry = new PolicyRegistry
        {
            [CommandProcessor.RETRYPOLICYASYNC] = Policy.Handle<Exception>().RetryAsync(),
            [CommandProcessor.CIRCUITBREAKERASYNC] =
                Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1))
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, _timeProvider, new Publication { Topic = _routingKey, RequestType = typeof(MyEvent) } )
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var trace = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = trace };

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

        var schedulerFactory = SchedulerBuilder.Create(new NameValueCollection())
            .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
            .UseJobFactory<BrighterResolver>()
            .Build();

        var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        scheduler.Start().GetAwaiter().GetResult();

        _scheduler = new QuartzSchedulerFactory(scheduler);

        CommandProcessor.ClearServiceBus();
        _processor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            outboxBus,
            _scheduler
        );

        BrighterResolver.Processor = _processor;
    }

    [Fact]
    public async Task When_scheduler_a_message_with_a_datetimeoffset_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equivalent(message, await _outbox.GetAsync(message.Id, new RequestContext()));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));
    }

    [Fact]
    public async Task When_scheduler_a_message_with_a_timespan_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());

        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        Assert.Equivalent(message, await _outbox.GetAsync(req.Id, new RequestContext()));
    }

    [Fact]
    public async Task When_reschedule_a_message_with_a_datetimeoffset_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        await scheduler.ReSchedulerAsync(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        await Task.Delay(TimeSpan.FromSeconds(4));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));
        Assert.Equivalent(message, await _outbox.GetAsync(req.Id, new RequestContext()));
    }

    [Fact]
    public async Task When_reschedule_a_message_with_a_timespan_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        Assert.True((id)?.Any());
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        await scheduler.ReSchedulerAsync(id, TimeSpan.FromSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        Assert.NotEqual(Message.Empty, await _outbox.GetAsync(req.Id, new RequestContext()));
    }

    [Fact]
    public async Task When_cancel_scheduler_message_with_a_datetimeoffset_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
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


    [Fact]
    public async Task When_cancel_scheduler_request_with_a_timespan_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        Assert.True((id)?.Any());

        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
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
