using Microsoft.Extensions.DependencyInjection;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.RocketMQ.Tests.MessageDispatch;

public class DispatchBuilderTests
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher? _dispatcher;

    public DispatchBuilderTests()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var connection = GatewayFactory.CreateConnection(); 
        var consumerFactory = new RocketMessageConsumerFactory(connection);
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
            .ChannelFactory(new RocketMqChannelFactory(consumerFactory))
            .Subscriptions([
                new RocketMqSubscription<MyEvent>(
                    new SubscriptionName("foo"),
                    new ChannelName("mary"),
                    new RoutingKey("bt_building_dispatch"),
                    consumerGroup: Guid.NewGuid().ToString(),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new RocketMqSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    new ChannelName("alice"),
                    new RoutingKey("bt_building_dispatch"),
                    consumerGroup: Guid.NewGuid().ToString(),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200))
            ])
            .ConfigureInstrumentation(tracer);
    }

    [Test]
    public async Task When_Building_A_Dispatcher()
    {
        _dispatcher = _builder.Build();

        await Assert.That(_dispatcher).IsNotNull();
        await Assert.That(GetConnection("foo")).IsNotNull();
        await Assert.That(GetConnection("bar")).IsNotNull();
        await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            
        Thread.Sleep(1000);

        _dispatcher.Receive();

        Thread.Sleep(1000);

        await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_RUNNING);

        await _dispatcher.End();
            
        await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
    }
    private Subscription? GetConnection(string name)
    {
        return _dispatcher!.Subscriptions.SingleOrDefault(conn => conn.Name == name);
    }
}
