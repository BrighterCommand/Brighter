using Events.Adapters.ServiceHost;
using Events.Ports.Commands;
using Events.Ports.Mappers;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.MsSql;
using TinyIoC;

namespace GreetingsSender
{
    class Program
    {
        static void Main()
        {
            var container = new TinyIoCContainer();


            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);

            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(GreetingEvent), typeof(GreetingEventMessageMapper)}
            };

            var outbox = new InMemoryOutbox();

            var messagingConfiguration = new MsSqlMessagingGatewayConfiguration(@"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
            var producer = new MsSqlMessageProducer(messagingConfiguration);

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration())
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration((IAmAnOutbox<Message>) outbox, producer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            commandProcessor.Post(new GreetingEvent("Ian"));
        }
    }
}
