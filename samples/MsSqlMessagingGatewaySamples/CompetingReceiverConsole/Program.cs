using System;
using Events;
using Events.Adapters.ServiceHost;
using Events.Ports.CommandHandlers;
using Events.Ports.Commands;
using Events.Ports.Mappers;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Serilog;

namespace CompetingReceiverConsole
{
    internal class Program
    {
        private static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();

            var container = new TinyIoCContainer();

            var handlerFactory = new TinyIocHandlerFactory(container);
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            container.Register<IAmACommandCounter, CommandCounter>();
            container.Register<IHandleRequests<CompetingConsumerCommand>, CompetingConsumerCommandHandler>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<CompetingConsumerCommand, CompetingConsumerCommandHandler>();

            //create policies
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

            var policyRegistry = new PolicyRegistry
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };

            //create message mappers
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(CompetingConsumerCommand), typeof(CompetingConsumerCommandMessageMapper)}
            };

            //create the gateway
            var messagingConfiguration =
                new MsSqlMessagingGatewayConfiguration(
                    @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
            var messageConsumerFactory = new MsSqlMessageConsumerFactory(messagingConfiguration);

            var dispatcher = DispatchBuilder.With()
                .CommandProcessor(CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                    .Policies(policyRegistry)
                    .NoTaskQueues()
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build())
                .MessageMappers(messageMapperRegistry)
                .DefaultChannelFactory(new MsSqlInputChannelFactory(messageConsumerFactory))
                .Connections(new Connection[]
                {
                    new Connection<CompetingConsumerCommand>(
                        new ConnectionName("paramore.example.multipleconsumer.command"),
                        new ChannelName("multipleconsumer.command"),
                        new RoutingKey("multipleconsumer.command"),
                        timeoutInMilliseconds: 200)
                }).Build();

            dispatcher.Receive();

            Console.WriteLine("Press Enter to stop receiving ...");
            Console.ReadLine();

            dispatcher.End().Wait();

            var count = container.Resolve<IAmACommandCounter>().Counter;


            Console.WriteLine($"There were {count} commands handled by this consumer");
            Console.WriteLine("Press Enter to exit ...");
            Console.ReadLine();
        }
    }
}
