using System.Collections.Specialized;
using System.Text.Json;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessageScheduler.Quartz;
using Paramore.Brighter.Observability;
using Quartz;
using Quartz.Impl;
using Quartz.Simpl;
using Quartz.Spi;

namespace ParamoreBrighter.Quartz.Tests;

public class QuartzMessageSchedulerTest
{
    private readonly RoutingKey _routingKey = new("MyCommand");
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly Message _message;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();
    private readonly StdSchedulerFactory _schedulerFactory;

    public QuartzMessageSchedulerTest()
    {
        _myCommand = new() { Value = $"Hello World {Guid.NewGuid():N}" };

        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        _outbox = new InMemoryOutbox(timeProvider) { Tracer = tracer };
        InMemoryProducer producer = new(_internalBus, timeProvider)
        {
            Publication = { Topic = _routingKey, RequestType = typeof(MyCommand) }
        };

        _message = new Message(
            new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        );

        var messageMapperRegistry =
            new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, producer }, });

        var externalBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry: producerRegistry,
            policyRegistry: new DefaultPolicy(),
            mapperRegistry: messageMapperRegistry,
            messageTransformerFactory: new EmptyMessageTransformerFactory(),
            messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),
            tracer: tracer,
            outbox: _outbox
        );

        _schedulerFactory = SchedulerBuilder.Create(new NameValueCollection())
            .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
            .UseJobFactory<BrighterResolver>()
            .Build();

        var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        scheduler.Start().GetAwaiter().GetResult();

        _commandProcessor = CommandProcessorBuilder.StartNew()
            .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactorySync()))
            .DefaultPolicy()
            .ExternalBus(ExternalBusType.FireAndForget, externalBus)
            .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .RequestSchedulerFactory(new QuartzSchedulerFactory(scheduler))
            .Build();

        BrighterResolver.Processor = _commandProcessor;
    }

    [Fact]
    public void Quartz_Scheduling_A_Message()
    {
        _commandProcessor.Post(TimeSpan.FromSeconds(1), _myCommand);

        Task.Delay(TimeSpan.FromSeconds(2)).Wait();

        _outbox.Get(_myCommand.Id, new RequestContext()).Should().NotBeNull();
    }

    [Fact]
    public async Task Scheduling_A_Message_Async()
    {
        _commandProcessor.Post(DateTimeOffset.UtcNow.AddSeconds(1), _myCommand);

        await Task.Delay(TimeSpan.FromSeconds(10));

        _outbox.Get(_myCommand.Id, new RequestContext()).Should().NotBeNull();
    }
}

public class MyCommand : Command
{
    public MyCommand()
        : base(Guid.NewGuid())

    {
    }

    public string Value { get; set; }
    public bool WasCancelled { get; set; }
    public bool TaskCompleted { get; set; }
}

internal class EmptyHandlerFactorySync : IAmAHandlerFactorySync
{
    public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
    {
        return null;
    }

    public void Release(IHandleRequests handler, IAmALifetime lifetime) { }
}

internal class MyCommandMessageMapper : IAmAMessageMapper<MyCommand>
{
    public IRequestContext Context { get; set; }

    public Message MapToMessage(MyCommand request, Publication publication)
    {
        var header = new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public MyCommand MapToRequest(Message message)
    {
        var command = JsonSerializer.Deserialize<MyCommand>(message.Body.Value, JsonSerialisationOptions.Options);
        return command;
    }
}

public class BrighterResolver : PropertySettingJobFactory
{
    public static IAmACommandProcessor Processor { get; set; }

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return new QuartzBrighterJob(Processor);
    }
}
