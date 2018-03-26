using System;
using System.Threading.Tasks;
using Greetings.Adapters.ServiceHost;
using Greetings.Ports.Commands;
using Greetings.Ports.Mappers;
using Greetings.TinyIoc;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;

namespace GreetingsPumper
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new TinyIoCContainer();


            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);

            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(GreetingEvent), typeof(GreetingEventMessageMapper)}
            };

            var messageStore = new InMemoryMessageStore();
            var rmqConnnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://myuser:mypass@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange"),
            };
            var producer = new RmqMessageProducer(rmqConnnection);

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration())
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration(messageStore, producer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            Console.WriteLine("Press <ENTER> to stop sending messages");
            long loop = 0;
            while (true)
            {
                loop++;
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Enter)
                        break;
                }
                Console.WriteLine($"Sending message #{loop}");
                commandProcessor.Post(new GreetingEvent($"Ian #{loop}"));

                if (loop % 100 == 0)
                {
                    Console.WriteLine("Pausing for breath...");
                    Task.Delay(4000).Wait();
                }
            }
        }
    }
}