using System;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace GreetingsSender
{
    static class Program
    {
        static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging();

            //TODO: add your ASB qualified name here
            var asbClientProvider = new ServiceBusVisualStudioCredentialClientProvider("fim-development-bus.servicebus.windows.net");

            var producerRegistry = new AzureServiceBusProducerRegistryFactory(
                asbClientProvider,
                [
                    new AzureServiceBusPublication<GreetingEvent>
                    {
                        Topic = new RoutingKey("greeting.event"),
                    },
                    new AzureServiceBusPublication<AddGreetingCommand>
                    {
                        Topic = new RoutingKey("greeting.addGreetingCommand"),
                    },
                    new AzureServiceBusPublication<GreetingAsyncEvent>
                    {
                        Topic = new RoutingKey("greeting.Asyncevent"),
                    }
                ]
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
