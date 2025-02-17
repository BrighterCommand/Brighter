using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RMQ.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessageDispatch;

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
            .MessageMappers(null, messageMapperRegistry, null, new EmptyMessageTransformerFactoryAsync())
            .ChannelFactory(new ChannelFactory(rmqMessageConsumerFactory))
            .Subscriptions(new []
            {
                new RmqSubscription<MyEvent>(
                    new SubscriptionName("foo"),
                    new ChannelName("mary"),
                    new RoutingKey("bob"),
                    messagePumpType: MessagePumpType.Proactor,
                    timeOut: TimeSpan.FromMilliseconds(200)),
                new RmqSubscription<MyEvent>(
                    new SubscriptionName("bar"),
                    new ChannelName("alice"),
                    new RoutingKey("simon"),
                    messagePumpType: MessagePumpType.Proactor,
                    timeOut: TimeSpan.FromMilliseconds(200))
            })
            .ConfigureInstrumentation(tracer, instrumentationOptions);
    }

    [Fact]
    public async Task When_Building_A_Dispatcher_With_Async()
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
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }

    private Subscription GetConnection(string name)
    {
        return _dispatcher.Subscriptions.SingleOrDefault(conn => conn.Name == name);
    }
}
