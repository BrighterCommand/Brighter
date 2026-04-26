using System.Collections.Specialized;
using System.Text.Json;
using System.Transactions;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessageScheduler.Quartz;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using ParamoreBrighter.Quartz.Tests.TestDoubles;
using Polly;
using Polly.Registry;
using Quartz;

namespace ParamoreBrighter.Quartz.Tests;

// Scheduler tests rely on timing; serialize to avoid CI CPU starvation (equivalent to xUnit [Collection("Scheduler")])
[NotInParallel("QuartzScheduler")]
public class QuartzSchedulerMessageAsyncTests
{
    private QuartzSchedulerFactory _scheduler;
    private IScheduler _quartzScheduler;
    private IAmACommandProcessor _processor;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    private readonly RoutingKey _routingKey;
    private readonly TimeProvider _timeProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly SubscriberRegistry _subscriberRegistry;
    private readonly SimpleHandlerFactoryAsync _handlerFactory;
    private readonly PolicyRegistry _policyRegistry;
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _outboxBus;

    public QuartzSchedulerMessageAsyncTests()
    {
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = TimeProvider.System;

        _handlerFactory = new SimpleHandlerFactoryAsync(
            type =>
            {
                if (type == typeof(MyEventHandlerAsync))
                {
                    return new MyEventHandlerAsync(new Dictionary<string, string>());
                }

                return new FireSchedulerMessageHandler(_processor!);
            });

        _subscriberRegistry = new SubscriberRegistry();
        _subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        _subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

        _policyRegistry = new PolicyRegistry
        {
            [CommandProcessor.RETRYPOLICYASYNC] = Policy.Handle<Exception>().RetryAsync(),
            [CommandProcessor.CIRCUITBREAKERASYNC] =
                Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1))
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyEvent) } )
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var trace = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = trace };

        _outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            new FindPublicationByPublicationTopicOrRequestType(),
            _outbox
        );

        _schedulerFactory = SchedulerBuilder.Create(new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = $"QuartzScheduler-{Guid.NewGuid():N}",
                ["quartz.scheduler.instanceId"] = Guid.NewGuid().ToString("N"),
            })
            .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
            .UseJobFactory<BrighterResolver>()
            .Build();
    }

    [Before(Test)]
    public async Task Setup()
    {
        _quartzScheduler = await _schedulerFactory.GetScheduler();
        await _quartzScheduler.Start();

        _scheduler = new QuartzSchedulerFactory(_quartzScheduler);

        _processor = new CommandProcessor(
            _subscriberRegistry,
            _handlerFactory,
            new InMemoryRequestContextFactory(),
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            _outboxBus,
            _scheduler
        );

        _quartzScheduler.Context.Put(BrighterResolver.ProcessorContextKey, _processor);
    }

    [Test]
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

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await Task.Delay(TimeSpan.FromSeconds(2));

        await Assert.That(await _outbox.GetAsync(message.Id, new RequestContext())).IsEquivalentTo(message);

        await Assert.That(_internalBus.Stream(_routingKey)).IsNotEmpty();
    }

    [Test]
    public async Task When_scheduler_a_message_with_a_timespan_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromSeconds(1));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await Assert.That(() => _internalBus.Stream(_routingKey).Any())
            .Eventually(s => s.IsTrue(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));

        await Assert.That(await _outbox.GetAsync(req.Id, new RequestContext())).IsEquivalentTo(message);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_datetimeoffset_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await scheduler.ReSchedulerAsync(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        await Task.Delay(TimeSpan.FromSeconds(2));
        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await Assert.That(() => _internalBus.Stream(_routingKey).Any())
            .Eventually(s => s.IsTrue(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
        await Assert.That(await _outbox.GetAsync(req.Id, new RequestContext())).IsEquivalentTo(message);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_timespan_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await scheduler.ReSchedulerAsync(id, TimeSpan.FromSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(2));
        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await Assert.That(() => _internalBus.Stream(_routingKey).Any())
            .Eventually(s => s.IsTrue(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));

        await Assert.That(await _outbox.GetAsync(req.Id, new RequestContext())).IsNotEqualTo(Message.Empty);
    }

    [Test]
    public async Task When_cancel_scheduler_message_with_a_datetimeoffset_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();

        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
        await Assert.That(actual.Body).IsEquivalentTo(expected.Body);
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


    [Test]
    public async Task When_cancel_scheduler_request_with_a_timespan_async()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerAsync)_scheduler.Create(_processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        await Assert.That((id)?.Any()).IsTrue();

        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _outbox.GetAsync(req.Id, new RequestContext());
        
        await Assert.That(actual.Body).IsEquivalentTo(expected.Body);
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

    [After(Test)]
    public async Task Cleanup()
    {
        await _quartzScheduler.Shutdown(waitForJobsToComplete: true);
    }
}
