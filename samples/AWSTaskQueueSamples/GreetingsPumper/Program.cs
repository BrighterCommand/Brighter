using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Greetings.Adapters.ServiceHost;
using Greetings.Ports.Commands;
using Greetings.Ports.Mappers;
using Greetings.TinyIoc;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AWSSQS;

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

            var messageStore = new InMemoryOutbox();
            if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
            {
                var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);
                var producer = new SqsMessageProducer(awsConnection);

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
}
