using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;

[Collection("CommandProcessor")]
public class CommandProcessorSchedulerCommandWithInvalidParamsTests
{
    private const string Topic = "MyCommand";
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    public CommandProcessorSchedulerCommandWithInvalidParamsTests()
    {
        _myCommand = new() { Value = $"Hello World {Guid.NewGuid():N}" };
        var routingKey = new RoutingKey("MyCommand");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var registry = new SubscriberRegistry();
        registry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        registry.Register<MyCommand, MyCommandHandler>();
        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyCommandHandler(_receivedMessages),
            _ => new FireSchedulerRequestHandler(_commandProcessor!));

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            null);

        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var producer = new InMemoryMessageProducer(_internalBus, _timeProvider, new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
            .AddBrighterDefault();

        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            _outbox
        );

        CommandProcessor.ClearServiceBus();
        _commandProcessor = new CommandProcessor(registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory { TimeProvider = _timeProvider });
        PipelineBuilder<MyCommand>.ClearPipelineCache();
        PipelineBuilder<FireSchedulerRequest>.ClearPipelineCache();
    }

    [Fact]
    public void When_Scheduling_Send_With_Invalid_Parameter()
    {
        var exception = Catch.Exception(() =>
            _commandProcessor.Send(_timeProvider.GetUtcNow().AddMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);

        exception = Catch.Exception(() => _commandProcessor.Send(TimeSpan.FromMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);
    }

    [Fact]
    public void When_Scheduling_Publish_With_Invalid_Parameter()
    {
        var exception = Catch.Exception(() =>
            _commandProcessor.Publish(_timeProvider.GetUtcNow().AddMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);

        exception = Catch.Exception(() => _commandProcessor.Publish(TimeSpan.FromMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);
    }
    
    [Fact]
    public void When_Scheduling_Post_With_Invalid_Parameter()
    {
        var exception = Catch.Exception(() => _commandProcessor.Post(_timeProvider.GetUtcNow().AddMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);

        exception = Catch.Exception(() => _commandProcessor.Post(TimeSpan.FromMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);
    }
}
