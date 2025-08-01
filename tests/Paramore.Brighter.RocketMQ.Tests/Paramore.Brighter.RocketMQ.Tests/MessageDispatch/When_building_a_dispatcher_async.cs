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
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessageDispatch;

[Collection("CommandProcessor")]
public class DispatchBuilderTestsAsync : IDisposable
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher? _dispatcher;

    public DispatchBuilderTestsAsync()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var connection = GatewayFactory.CreateConnection(); 
        
        var consumerFactory = new RocketMessageConsumerFactory(connection);
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
            .MessageMappers(null!, messageMapperRegistry, null, new EmptyMessageTransformerFactoryAsync())
            .ChannelFactory(new RocketMqChannelFactory(consumerFactory))
            .Subscriptions([
                new RocketMqSubscription<MyEvent>(
                    new SubscriptionName("foo"),
                    new ChannelName("mary"),
                    new RoutingKey("bt_building_dispatch_async"),
                    consumerGroup: Guid.NewGuid().ToString(),
                    messagePumpType: MessagePumpType.Proactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new RocketMqSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    new ChannelName("alice"),
                    new RoutingKey("bt_building_dispatch_async"),
                    consumerGroup: Guid.NewGuid().ToString(),
                    messagePumpType: MessagePumpType.Proactor,
                    timeOut: TimeSpan.FromMilliseconds(200))
            ])
            .ConfigureInstrumentation(tracer, instrumentationOptions);
    }
                
    [Fact]
    public async Task When_Building_A_Dispatcher_With_Async()
    {
        _dispatcher = _builder.Build();

        Assert.NotNull(_dispatcher);
        Assert.NotNull(GetConnection("foo"));
        Assert.NotNull(GetConnection("bar"));
        
        Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);

        await Task.Delay(1000);

        _dispatcher.Receive();

        await Task.Delay(1000);

        Assert.Equal(DispatcherState.DS_RUNNING, _dispatcher.State);

        await _dispatcher.End();
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
