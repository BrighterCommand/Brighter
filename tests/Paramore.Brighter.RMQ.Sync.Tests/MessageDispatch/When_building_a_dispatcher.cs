using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessageDispatch;

public class DispatchBuilderTests
{
    private static readonly Uri s_brokerUri = new("amqp://guest:guest@localhost:5672/%2f");
    private readonly ChannelName _firstQueue = new($"rmq-sync-dispatcher-{Guid.NewGuid():N}");
    private readonly ChannelName _secondQueue = new($"rmq-sync-dispatcher-{Guid.NewGuid():N}");
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
            AmpqUri = new AmqpUriSpecification(s_brokerUri),
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
                    _firstQueue,
                    new RoutingKey("bob"),
                    messagePumpType: MessagePumpType.Reactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new RmqSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    _secondQueue,
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

    [Before(HookType.Test)] public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]
    public async Task DisposeAsync()
    {
        if (_dispatcher is null)
            return;

        try
        {
            await _dispatcher.DisposeAsync();
        }
        finally
        {
            var factory = new ConnectionFactory { Uri = s_brokerUri };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDelete(_firstQueue.Value);
            channel.QueueDelete(_secondQueue.Value);
        }
    }

    private Subscription GetConnection(string name)
    {
        return Enumerable.SingleOrDefault<Subscription>(_dispatcher.Subscriptions, conn => conn.Name == name);
    }
}
