using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.Observability;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessageDispatch;

[Collection("CommandProcessor")]
public class DispatchBuilderTests : IDisposable
{
    private readonly IAmADispatchBuilder _builder;
    private Dispatcher? _dispatcher;

    public DispatchBuilderTests()
    {
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
            .Build();

        _builder = DispatchBuilder.StartNew()
            .CommandProcessor(commandProcessor,
                new InMemoryRequestContextFactory()
            )
            .MessageMappers(messageMapperRegistry, null, null, null)
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

    private Subscription GetConnection(string name)
    {
        return Enumerable.SingleOrDefault<Subscription>(_dispatcher.Subscriptions, conn => conn.Name == name);
    }
}
