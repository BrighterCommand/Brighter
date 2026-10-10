using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class ProactorOutboxClearContextTests
{
    private readonly RoutingKey _incoming = new("MyCommand");
    private readonly RoutingKey _outgoing = new("Outgoing");

    [Fact]
    public async Task When_a_handler_clears_the_outbox_should_run_the_resilience_pipeline_on_the_pump_thread()
    {
        //Arrange
        var retryThread = 0;
        var resiliencePipelines = new ResiliencePipelineRegistry<string>();
        resiliencePipelines.TryAddBuilder(CommandProcessor.OutboxProducer, (builder, _) => builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 1,
            Delay = TimeSpan.Zero,
            OnRetry = _ =>
            {
                retryThread = Environment.CurrentManagedThreadId; // where Polly resumed after the failed send
                return default;
            }
        }));
        resiliencePipelines.AddBrighterDefault();

        var outgoingBus = new InternalBus();
        var producer = new FailsOnceOffPoolProducer(outgoingBus, new Publication { Topic = _outgoing });
        var outbox = new InMemoryOutbox(TimeProvider.System);
        var outstanding = new Message(new MessageHeader(Id.Random(), _outgoing, MessageType.MT_EVENT), new MessageBody("posted by the handler"));
        outbox.Add(outstanding, new RequestContext());
        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _outgoing, producer } }),
            resiliencePipelines,
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            new BrighterTracer(TimeProvider.System),
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox);

        var commandProcessor = new OutboxClearingCommandProcessor(mediator, outstanding.Id);
        var (pump, channel) = CreatePump(commandProcessor);

        //Act
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);
        var handled = commandProcessor.WaitForHandler(TimeSpan.FromSeconds(5));
        channel.Enqueue(new Message(new MessageHeader(string.Empty, RoutingKey.Empty, MessageType.MT_QUIT), new MessageBody("")));
        await pumping;
        await producer.DisposeAsync();

        //Assert
        Assert.True(handled, "The handler did not finish clearing the outbox");
        Assert.Single(outgoingBus.Stream(_outgoing)); // sent on the retry
        Assert.Equal(commandProcessor.HandlerThread, retryThread); // Polly kept the pump's context
        Assert.Equal(commandProcessor.HandlerThread, commandProcessor.ThreadAfterClear);
    }

    private (IAmAMessagePump Pump, ChannelAsync Channel) CreatePump(IAmACommandProcessor commandProcessor)
    {
        var incomingBus = new InternalBus();
        var channel = new ChannelAsync(new("myChannel"), _incoming,
            new InMemoryMessageConsumer(_incoming, incomingBus, new FakeTimeProvider(), ackTimeout: TimeSpan.FromMilliseconds(1000)));

        var mappers = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        mappers.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var pump = new ServiceActivator.Proactor(commandProcessor, _ => typeof(MyCommand),
            mappers, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel)
        {
            Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 1
        };

        var command = new TransformPipelineBuilderAsync(mappers, null, InstrumentationOptions.All)
            .BuildWrapPipeline<MyCommand>()
            .WrapAsync(new MyCommand(), new RequestContext(), new Publication { Topic = _incoming })
            .Result;
        incomingBus.Enqueue(command);

        return (pump, channel);
    }
}
