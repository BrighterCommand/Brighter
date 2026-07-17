using System.Collections.Specialized;
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

// Scheduler tests rely on timing; serialize to avoid CI CPU starvation (equivalent to xUnit [NotInParallel("Scheduler")])
[NotInParallel("QuartzScheduler")]
public class QuartzSchedulerRequestTests
{
    private QuartzSchedulerFactory _scheduler;
    private IScheduler _quartzScheduler;
    private IAmACommandProcessor _processor;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    private readonly Dictionary<string, string> _receivedMessages;
    private readonly RoutingKey _routingKey;
    private readonly TimeProvider _timeProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly SubscriberRegistry _subscriberRegistry;
    private readonly SimpleHandlerFactory _handlerFactory;
    private readonly PolicyRegistry _policyRegistry;
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _outboxBus;

    public QuartzSchedulerRequestTests()
    {
        _receivedMessages = new Dictionary<string, string>();
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = TimeProvider.System;

        _handlerFactory = new SimpleHandlerFactory(
            _ => new MyEventHandler(_receivedMessages),
            _ => new FireSchedulerRequestHandler(_processor!));

        _subscriberRegistry = new SubscriberRegistry();
        _subscriberRegistry.Register<MyEvent, MyEventHandler>();
        _subscriberRegistry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();

        _policyRegistry = new PolicyRegistry
        {
            [CommandProcessor.RETRYPOLICY] = Policy.Handle<Exception>().Retry(),
            [CommandProcessor.CIRCUITBREAKER] =
                Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyEvent) } )
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

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

    #region Scheduler

    [Test]
    public async Task When_scheduler_send_request_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Send,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        await Assert.That(_receivedMessages).ContainsKey(nameof(MyEventHandler));

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

    [Test]
    public async Task When_scheduler_send_request_with_a_timespan()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Send, TimeSpan.FromSeconds(1));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        await Assert.That(_receivedMessages).ContainsKey(nameof(MyEventHandler));

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

    [Test]
    public async Task When_scheduler_publish_request_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Publish,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        await Assert.That(_receivedMessages).ContainsKey(nameof(MyEventHandler));

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

    [Test]
    public async Task When_scheduler_publish_request_with_a_timespan()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Publish, TimeSpan.FromSeconds(1));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        await Assert.That(_receivedMessages).ContainsKey(nameof(MyEventHandler));

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

    [Test]
    public async Task When_scheduler_post_request_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Post,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        Thread.Sleep(TimeSpan.FromSeconds(2));

        await Assert.That(await _outbox.GetAsync(req.Id, new RequestContext())).IsNotEqualTo(Message.Empty);

        await Assert.That(_internalBus.Stream(_routingKey)).IsNotEmpty();
    }

    [Test]
    public async Task When_scheduler_post_request_with_a_timespan()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Post, TimeSpan.FromSeconds(1));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_internalBus.Stream(_routingKey) ?? []).IsEmpty();

        await Assert.That(() => _internalBus.Stream(_routingKey).Any())
            .Eventually(s => s.IsTrue(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));

        await Assert.That(await _outbox.GetAsync(req.Id, new RequestContext())).IsNotEqualTo(Message.Empty);
    }

    #endregion

    #region Rescheduler

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_reschedule_request_with_a_datetimeoffset(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();

        scheduler.ReScheduler(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(4));
        await Assert.That(_receivedMessages).ContainsKey(nameof(MyEventHandler));

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

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_reschedule_send_request_with_a_timespan(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(1));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        scheduler.ReScheduler(id, TimeSpan.FromSeconds(5));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        Thread.Sleep(TimeSpan.FromSeconds(4));
        await Assert.That(_receivedMessages).ContainsKey(nameof(MyEventHandler));

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

    #endregion

    #region Cancel

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_cancel_scheduler_request_with_a_datetimeoffset(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That((id)?.Any()).IsTrue();

        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));


        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));
        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

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

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_cancel_scheduler_request_with_a_timespan(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(1));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));
        await Assert.That(_receivedMessages).DoesNotContainKey(nameof(MyEventHandler));

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

    #endregion

    [After(Test)]
    public async Task Cleanup()
    {
        await _quartzScheduler.Shutdown(waitForJobsToComplete: true);
    }
}
