using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessageDispatch;

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

        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(connection);

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
            .MessageMappers(messageMapperRegistry, null, new EmptyMessageTransformerFactory(), null)
            .ChannelFactory(new ChannelFactory(rmqMessageConsumerFactory))
            .Subscriptions(new []
            {
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
            })
            .ConfigureInstrumentation(tracer, instrumentationOptions);
    }

    [Fact]
    public void When_building_a_dispatcher_with_named_gateway()
    {
        _dispatcher = _builder.Build();

        Assert.NotNull(_dispatcher);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
