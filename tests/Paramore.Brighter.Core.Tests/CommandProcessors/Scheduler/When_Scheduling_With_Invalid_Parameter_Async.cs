using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
public class CommandProcessorSchedulerCommandWithInvalidParamsAsyncTests
{
    private const string Topic = "MyCommand";
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    public CommandProcessorSchedulerCommandWithInvalidParamsAsyncTests()
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

        var policyRegistry = new PolicyRegistry
        {
            {
                CommandProcessor.RETRYPOLICY, Policy
                    .Handle<Exception>()
                    .Retry()
            },
            {
                CommandProcessor.CIRCUITBREAKER, Policy
                    .Handle<Exception>()
                    .CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
            },
            {
                CommandProcessor.RETRYPOLICYASYNC, Policy
                    .Handle<Exception>()
                    .RetryAsync()
            },
            {
                CommandProcessor.CIRCUITBREAKERASYNC, Policy
                    .Handle<Exception>()
                    .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1))
            }
        };

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });

        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
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
            policyRegistry,
            bus,
            new InMemorySchedulerFactory { TimeProvider = _timeProvider });
        PipelineBuilder<MyCommand>.ClearPipelineCache();
        PipelineBuilder<FireSchedulerRequest>.ClearPipelineCache();
    }

    [Fact]
    public async Task When_Scheduling_Send_With_Invalid_Parameter_Async()
    {
        var exception = await Catch.ExceptionAsync(() =>
            _commandProcessor.SendAsync(_timeProvider.GetUtcNow().AddMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);

        exception = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(TimeSpan.FromMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);
    }

    [Fact]
    public async Task When_Scheduling_Publish_With_Invalid_Parameter()
    {
        var exception = await Catch.ExceptionAsync(() =>
            _commandProcessor.PublishAsync(_timeProvider.GetUtcNow().AddMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);

        exception = await Catch.ExceptionAsync(() => _commandProcessor.PublishAsync(TimeSpan.FromMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);
    }
    
    [Fact]
    public async Task When_Scheduling_Post_With_Invalid_Parameter()
    {
        var exception = await Catch.ExceptionAsync(() => _commandProcessor.PostAsync(_timeProvider.GetUtcNow().AddMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);

        exception = await Catch.ExceptionAsync(() => _commandProcessor.PostAsync(TimeSpan.FromMilliseconds(-1), _myCommand));
        Assert.NotNull(exception);
        Assert.True((exception) is ArgumentOutOfRangeException);
    }
}
