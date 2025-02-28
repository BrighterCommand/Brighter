using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.RocketMQ.Tests.MessageDispatch;

[Collection("CommandProcessor")]
public class DispatchBuilderWithNamedGateway : IDisposable
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher _dispatcher;

    public DispatchBuilderWithNamedGateway()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null
        );
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        var policyRegistry = new PolicyRegistry
        {
            {
                CommandProcessor.RETRYPOLICY, Policy
                    .Handle<Exception>()
                    .WaitAndRetry(new[] {TimeSpan.FromMilliseconds(50)})
            },
            {
                CommandProcessor.CIRCUITBREAKER, Policy
                    .Handle<Exception>()
                    .CircuitBreaker(1, TimeSpan.FromMilliseconds(500))
            }
        };

        var connection = new RocketMessagingGatewayConnection(new ClientConfig.Builder()
            .SetEndpoints("")
            .Build());

        var consumerFactory = new RocketMessageConsumerFactory(connection);

        var container = new ServiceCollection();
        var tracer = new BrighterTracer(TimeProvider.System);
        var instrumentationOptions = InstrumentationOptions.All;
            
        var commandProcessor = CommandProcessorBuilder.StartNew()
            .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new ServiceProviderHandlerFactory(container.BuildServiceProvider())))
            .Policies(policyRegistry)
            .NoExternalBus()
            .ConfigureInstrumentation(tracer, instrumentationOptions)
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .Build();

        _builder = DispatchBuilder.StartNew()
            .CommandProcessor(commandProcessor,
                new InMemoryRequestContextFactory()
            )
            .MessageMappers(messageMapperRegistry, null, new EmptyMessageTransformerFactory(), null)
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
            .ConfigureInstrumentation(tracer, instrumentationOptions);
    }

    [Fact]
    public void When_building_a_dispatcher_with_named_gateway()
    {
        _dispatcher = _builder.Build();

        _dispatcher.Should().NotBeNull();
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
