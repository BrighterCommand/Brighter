using System;
using System.Data.Common;
using System.Transactions;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Polly.Caching;

namespace GreetingsSender
{
    class Program
    {
        static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging();

            //TODO: add your ASB qualified name here
            var asbClientProvider = new ServiceBusConnectionStringClientProvider("Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

            var producerRegistry = new AzureServiceBusProducerRegistryFactory(
                asbClientProvider,
                new AzureServiceBusPublication[] 
                {
                    new AzureServiceBusPublication
                    {
                        Topic = new RoutingKey("greeting.event"),
                        RequestType = typeof(GreetingEvent),
                        MakeChannels = OnMissingChannel.Assume
                    },
                    new AzureServiceBusPublication
                    {
                        Topic = new RoutingKey("greeting.addGreetingCommand"),
                        RequestType = typeof(AddGreetingCommand),
                        MakeChannels = OnMissingChannel.Assume
                        
                    },
                    new AzureServiceBusPublication
                    {
                        Topic = new RoutingKey("greeting.Asyncevent"),
                        RequestType = typeof(GreetingAsyncEvent),
                        MakeChannels = OnMissingChannel.Assume
                    }
                }
            ).Create();
            
            serviceCollection.AddBrighter()
                .UseExternalBus((config) =>
                {
                    config.ProducerRegistry = producerRegistry;
                })
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            bool run = true;

            while (run)
            {
                Console.WriteLine("Sending....");
                var distroGreeting = new GreetingEvent("Paul - Distributed");
                commandProcessor.DepositPost(distroGreeting);
                
                commandProcessor.Post(new GreetingEvent("Paul"));
                commandProcessor.Post(new GreetingAsyncEvent("Paul - Async"));

                commandProcessor.ClearOutbox(new []{distroGreeting.Id});
                
                Console.WriteLine("Press q to Quit or any other key to continue");

                var keyPress = Console.ReadKey();
                if (keyPress.KeyChar == 'q')
                    run = false;
            }
        }
    }
}
