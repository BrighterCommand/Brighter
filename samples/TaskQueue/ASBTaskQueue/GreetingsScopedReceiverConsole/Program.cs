﻿using System;
using System.Threading.Tasks;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

namespace GreetingsScopedReceiverConsole
{
    static class Program
    {
        // If you run this receiver with the other receiver, and send you'll see different behaviours. 
        // This scoped receiver will refresh the scoped dependency for each pipeline (Event/Command dispatch)
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((_, services) =>

                {
                    services.AddLogging();

                    services.AddScoped<InstanceCount>();
                    services.AddTransient(typeof(MonitoringAsyncHandler<>));
                    services.AddTransient(typeof(MonitoringAttribute));

                    var subscriptions = new Subscription[]
                    {
                        new AzureServiceBusSubscription<GreetingAsyncEvent>(
                            new SubscriptionName("Async Event"),
                            new ChannelName("paramore.example.greeting"),
                            new RoutingKey("greeting.Asyncevent"),
                            timeOut: TimeSpan.FromMilliseconds(400),
                            makeChannels: OnMissingChannel.Assume,
                            requeueCount: 3,
                            messagePumpType: MessagePumpType.Proactor),

                        new AzureServiceBusSubscription<GreetingEvent>(
                            new SubscriptionName("Event"),
                            new ChannelName("paramore.example.greeting"),
                            new RoutingKey("greeting.event"),
                            timeOut: TimeSpan.FromMilliseconds(400),
                            makeChannels: OnMissingChannel.Assume,
                            requeueCount: 3,
                            messagePumpType: MessagePumpType.Proactor)
                    };

                    //TODO: add your ASB qualified name here
                    var asbClientProvider = new ServiceBusConnectionStringClientProvider("Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
                    var asbConsumerFactory = new AzureServiceBusConsumerFactory(asbClientProvider);
                    services
                        .AddConsumers(options =>
                        {
                            options.Subscriptions = subscriptions;
                            options.DefaultChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
                            options.UseScoped = true;
                        })
                        .AutoFromAssemblies();

                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .ConfigureLogging((_, logging) => {
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddConsole();
                })
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }
    }
}
