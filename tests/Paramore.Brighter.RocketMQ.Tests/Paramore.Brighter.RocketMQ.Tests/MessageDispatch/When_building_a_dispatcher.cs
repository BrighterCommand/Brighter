using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.RocketMQ.Tests.MessageDispatch;

[Collection("CommandProcessor")]
public class DispatchBuilderTests : IDisposable
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher? _dispatcher;

    public DispatchBuilderTests()
    {
        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "false");
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(new[]
            {
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(150)
            });

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

        var connection = new RocketMessagingGatewayConnection(new ClientConfig.Builder()
            .SetEndpoints("localhost:9876")
            .Build());

        var consumerFactory = new RocketMessageConsumerFactory(connection);
        var container = new ServiceCollection();

        var tracer = new BrighterTracer(TimeProvider.System);
        const InstrumentationOptions instrumentationOptions = InstrumentationOptions.All;
            
        var commandProcessor = CommandProcessorBuilder.StartNew()
            .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new ServiceProviderHandlerFactory(container.BuildServiceProvider())))
            .Policies(new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            })
            .NoExternalBus()
            .ConfigureInstrumentation(tracer, instrumentationOptions)
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .RequestSchedulerFactory(new InMemorySchedulerFactory())
            .Build();

        _builder = DispatchBuilder.StartNew()
            .CommandProcessor(commandProcessor,
                new InMemoryRequestContextFactory()
            )
            .MessageMappers(messageMapperRegistry, null, null, null)
            .ChannelFactory(new ChannelFactory(consumerFactory))
            .Subscriptions(new []
            {
                new RocketSubscription<MyEvent>(
                    new SubscriptionName("foo"),
                    new ChannelName("mary"),
                    new RoutingKey("bob"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new RocketSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    new ChannelName("alice"),
                    new RoutingKey("simon"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200))
            })
            .ConfigureInstrumentation(tracer);

       var producerFactory = new RocketMessageProducerFactory(connection,
            new[]
            {
                new RocketPublication { Topic = new RoutingKey("bob") },
                new RocketPublication { Topic = new RoutingKey("simon") }
            });

       var producers = producerFactory.Create();
       producers.Each(x =>
       {
           ((IAmAMessageProducerSync)x.Value).Send(new Message(
               new MessageHeader(Guid.NewGuid().ToString("N"), x.Key, MessageType.MT_COMMAND),
               new MessageBody("{}")));
       });
    }

    [Fact]
    public async Task When_Building_A_Dispatcher()
    {
        _dispatcher = _builder.Build();

        _dispatcher.Should().NotBeNull();
        GetConnection("foo").Should().NotBeNull();
        GetConnection("bar").Should().NotBeNull();
        _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            
        await Task.Delay(1000);

        _dispatcher.Receive();

        await Task.Delay(1000);

        _dispatcher.State.Should().Be(DispatcherState.DS_RUNNING);

        await _dispatcher.End();
            
        _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }

    private Subscription? GetConnection(string name)
    {
        return _dispatcher.Subscriptions.SingleOrDefault(conn => conn.Name == name);
    }
}
