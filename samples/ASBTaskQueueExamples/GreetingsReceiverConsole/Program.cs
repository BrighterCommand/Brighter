using System;
using System.Threading.Tasks;
using Greetings.Ports.Events;
using Greetings.Ports.Mappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

namespace GreetingsReceiverConsole
{
    class Program
    {
        public async static Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>

                {
                    services.AddLogging();

                    var subscriptions = new Subscription[]
                    {
                        new AzureServiceBusSubscription<GreetingAsyncEvent>(
                            new SubscriptionName(GreetingEventAsyncMessageMapper.Topic),
                            new ChannelName("paramore.example.greeting"),
                            new RoutingKey(GreetingEventAsyncMessageMapper.Topic),
                            timeoutInMs: 400,
                            makeChannels: OnMissingChannel.Validate,
                            requeueCount: 3,
                            isAsync: true),
                        new AzureServiceBusSubscription<GreetingEvent>(
                            new SubscriptionName(GreetingEventMessageMapper.Topic),
                            new ChannelName("paramore.example.greeting"),
                            new RoutingKey(GreetingEventMessageMapper.Topic),
                            timeoutInMs: 400,
                            makeChannels: OnMissingChannel.Validate,
                            requeueCount: 3,
                            isAsync: false)
                    };

                    //create the gateway
                    var asbConfig = new AzureServiceBusConfiguration("Endpoint=sb://fim-development-bus.servicebus.windows.net/;Authentication=Managed Identity", true);

                    var asbConsumerFactory = new AzureServiceBusConsumerFactory(asbConfig);
                    services.AddServiceActivator(options =>
                    {
                        options.Subscriptions = subscriptions;
                        options.ChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
                    }).UseInMemoryOutbox()
                        .UseExternalBus(AzureServiceBusMessageProducerFactory.Get(asbConfig))
                        .AutoFromAssemblies();


                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .ConfigureLogging((hostingContext, logging) => {
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddConsole();
                })
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }
    }
}
