﻿using System.Transactions;
using Hangfire;
using Hangfire.InMemory;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Hangfire.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Hangfire;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Hangfire.Tests;

[Collection("Scheduler")]
public class HangfireSchedulerRequestTests : IDisposable
{
    private readonly HangfireMessageSchedulerFactory _scheduler;
    private readonly BackgroundJobServer _server;
    private readonly IAmACommandProcessor _processor;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    private readonly Dictionary<string, string> _receivedMessages;
    private readonly RoutingKey _routingKey;
    private readonly TimeProvider _timeProvider;

    public HangfireSchedulerRequestTests()
    {
        _receivedMessages = new Dictionary<string, string>();
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = TimeProvider.System;

        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyEventHandler(_receivedMessages),
            _ => new FireSchedulerRequestHandler(_processor!));

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();
        subscriberRegistry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();

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
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = trace };

        var outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            new FindPublicationByPublicationTopicOrRequestType(),
            circuitBreaker: new InMemoryCircuitBreaker(),
            _outbox
        );

        GlobalConfiguration.Configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage(new InMemoryStorageOptions { IdType = InMemoryStorageIdType.Guid })
            .UseActivator(new BrighterActivator());

        _server = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            WorkerCount = 1, SchedulePollingInterval = TimeSpan.FromSeconds(1), Activator = new BrighterActivator(),
        });
        _scheduler = new HangfireMessageSchedulerFactory();

        CommandProcessor.ClearServiceBus();
        _processor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            outboxBus,
            _scheduler
        );

        BrighterActivator.Processor = _processor;
    }

    #region Scheduler

    [Fact]
    public void When_scheduler_send_request_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Send,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Contains(nameof(MyEventHandler), _receivedMessages);

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

    [Fact]
    public void When_scheduler_send_request_with_a_timespan()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Send, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());

        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Contains(nameof(MyEventHandler), _receivedMessages);

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

    [Fact]
    public void When_scheduler_publish_request_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Publish,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Contains(nameof(MyEventHandler), _receivedMessages);

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

    [Fact]
    public void When_scheduler_publish_request_with_a_timespan()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Publish, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());

        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Contains(nameof(MyEventHandler), _receivedMessages);

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

    [Fact]
    public void When_scheduler_post_request_with_a_datetimeoffset()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Post,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        Assert.NotEqual(Message.Empty, _outbox.Get(req.Id, new RequestContext()));
    }

    [Fact]
    public void When_scheduler_post_request_with_a_timespan()
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, RequestSchedulerType.Post, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());

        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        Assert.NotEqual(Message.Empty, _outbox.Get(req.Id, new RequestContext()));
    }

    #endregion

    #region Rescheduler

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Publish)]
    public void When_reschedule_request_with_a_datetimeoffset(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        Assert.True(scheduler.ReScheduler(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5))));

        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(4));
        Assert.Contains(nameof(MyEventHandler), _receivedMessages);

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

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Publish)]
    public void When_reschedule_send_request_with_a_timespan(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());
        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Assert.True(scheduler.ReScheduler(id, TimeSpan.FromSeconds(5)));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        Thread.Sleep(TimeSpan.FromSeconds(4));
        Assert.Contains(nameof(MyEventHandler), _receivedMessages);

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

    #endregion

    #region Cancel

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public void When_cancel_scheduler_request_with_a_datetimeoffset(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type,
            _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());

        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);


        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

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

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public void When_cancel_scheduler_request_with_a_timespan(RequestSchedulerType type)
    {
        var req = new MyEvent();
        var scheduler = _scheduler.CreateSync(_processor);
        var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());
        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(nameof(MyEventHandler), _receivedMessages);

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

    #endregion

    public void Dispose()
    {
        _server.Dispose();
    }
}
