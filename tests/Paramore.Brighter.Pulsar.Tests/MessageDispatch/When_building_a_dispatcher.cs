using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.TestDoubles;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessageDispatch;

[Collection("CommandProcessor")]
public class DispatchBuilderTests : IDisposable
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher? _dispatcher;

    public DispatchBuilderTests()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry([
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(150)
            ]);

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

        var connection = GatewayFactory.CreateConnection(); 
        var consumerFactory = new PulsarMessageConsumerFactory(connection);
        var container = new ServiceCollection();

        var tracer = new BrighterTracer(TimeProvider.System);
        const InstrumentationOptions instrumentationOptions = InstrumentationOptions.All;
            
        var commandProcessor = CommandProcessorBuilder.StartNew()
            .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new ServiceProviderHandlerFactory(container.BuildServiceProvider())))
            .DefaultResilience()
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
            .ChannelFactory(new PulsarChannelFactory(consumerFactory))
            .Subscriptions([
                new PulsarSubscription<MyEvent>(
                    new SubscriptionName("foo"),
                    new ChannelName("mary"),
                    new RoutingKey("bt_building_dispatch"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new PulsarSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    new ChannelName("alice"),
                    new RoutingKey("bt_building_dispatch"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200))
            ])
            .ConfigureInstrumentation(tracer);
    }

    [Fact]
    public void When_Building_A_Dispatcher()
    {
        _dispatcher = _builder.Build();

        Assert.NotNull(_dispatcher);
        Assert.NotNull(GetConnection("foo"));
        Assert.NotNull(GetConnection("bar"));
        Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            
        Thread.Sleep(1000);

        _dispatcher.Receive();

        Thread.Sleep(1000);

        Assert.Equal(DispatcherState.DS_RUNNING, _dispatcher.State);

        _dispatcher.End().GetAwaiter().GetResult();
            
        Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }

    private Subscription? GetConnection(string name)
    {
        return _dispatcher!.Subscriptions.SingleOrDefault(conn => conn.Name == name);
    }
}
