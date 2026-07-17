using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.RMQ.Async.Tests.MessageDispatch;

public class DispatchBuilderTests
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher? _dispatcher;

    public DispatchBuilderTests()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);
        var container = new ServiceCollection();

        var tracer = new BrighterTracer(TimeProvider.System);
        var instrumentationOptions = InstrumentationOptions.All;
            
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
            .ChannelFactory(new ChannelFactory(rmqMessageConsumerFactory))
            .Subscriptions(
            [
                new RmqSubscription<MyEvent>(
                    new SubscriptionName("foo"),
                    new ChannelName("mary"),
                    new RoutingKey("bob"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new RmqSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    new ChannelName("alice"),
                    new RoutingKey("simon"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200))
            ])
            .ConfigureInstrumentation(tracer, instrumentationOptions);
    }

    [Test]
    public async Task When_Building_A_Dispatcher()
    {
        _dispatcher = _builder.Build();

        await Assert.That(_dispatcher).IsNotNull();
        await Assert.That(GetConnection("foo")).IsNotNull();
        await Assert.That(GetConnection("bar")).IsNotNull();
        await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            
        await Task.Delay(1000);

        _dispatcher.Receive();

        await Task.Delay(1000);

        await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_RUNNING);

        await _dispatcher.End();
            
        await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
    }
    private Subscription GetConnection(string name)
    {
        return Enumerable.SingleOrDefault<Subscription>(_dispatcher.Subscriptions, conn => conn.Name == name);
    }
}
