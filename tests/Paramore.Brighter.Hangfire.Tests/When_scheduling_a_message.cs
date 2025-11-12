using System.Text.Json;
using System.Transactions;
using Hangfire;
using Hangfire.InMemory;
using Paramore.Brighter.Hangfire.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Hangfire;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Hangfire.Tests;

[Collection("Scheduler")]
public class HangfireSchedulerMessageTests : IDisposable
{
    private readonly HangfireMessageSchedulerFactory _scheduler;
    private readonly BackgroundJobServer _server;
    private readonly IAmACommandProcessor _processor;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    private readonly RoutingKey _routingKey;
    private readonly TimeProvider _timeProvider;

    public HangfireSchedulerMessageTests()
    {
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = TimeProvider.System;

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
            [_routingKey] = new InMemoryMessageProducer(_internalBus, _timeProvider, new Publication{ Topic = _routingKey, RequestType = typeof(MyEvent) })
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var trace = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = trace };

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
            new ResiliencePipelineRegistry<string>(),
            outboxBus,
            _scheduler
        );

        BrighterActivator.Processor = _processor;
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

        Assert.NotEqual(0, id.Length);

        Assert.Empty(_internalBus.Stream(_routingKey));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Equivalent(message, _outbox.Get(message.Id, new RequestContext()));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));
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

        Assert.NotEqual(0, id.Length);

        Assert.Empty(_internalBus.Stream(_routingKey));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        Assert.Equivalent(message, _outbox.Get(req.Id, new RequestContext()));
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

        Assert.NotEqual(0, id.Length);
        Assert.Empty(_internalBus.Stream(_routingKey));

        Assert.True(scheduler
            .ReScheduler(id, _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5))));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.Empty(_internalBus.Stream(_routingKey));

        Thread.Sleep(TimeSpan.FromSeconds(6));

        Assert.NotEmpty(_internalBus.Stream(_routingKey));
        Assert.Equivalent(message, _outbox.Get(req.Id, new RequestContext()));
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

        Assert.NotEqual(0, id.Length);
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        Assert.True(scheduler.ReScheduler(id, TimeSpan.FromSeconds(5)));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.Empty(_internalBus.Stream(_routingKey) ?? []);

        Thread.Sleep(TimeSpan.FromSeconds(4));
        Assert.NotEmpty(_internalBus.Stream(_routingKey));

        Assert.NotEqual(Message.Empty, _outbox.Get(req.Id, new RequestContext()));
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

        Assert.NotEqual(0, id.Length);

        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));

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
    public void When_cancel_scheduler_request_with_a_timespan()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _routingKey },
                new MessageBody(JsonSerializer.Serialize(req)));

        var scheduler = (IAmAMessageSchedulerSync)_scheduler.Create(_processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        Assert.NotEqual(0, id.Length);

        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));

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

    public void Dispose()
    {
        _server.Dispose();
    }
}
