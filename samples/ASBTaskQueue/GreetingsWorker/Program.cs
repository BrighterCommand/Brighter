using System;
using System.Threading.Tasks;
using Greetings.Adaptors.Data;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Greetings.Ports.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MsSql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

namespace GreetingsWorker
{
    class Program
    {
        public async static Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>

                {
                    services.AddLogging();

                    services.AddScoped<InstanceCount>();
                    services.AddTransient(typeof(MonitoringAsyncHandler<>));
                    services.AddTransient(typeof(MonitoringAttribute));

                    var subscriptionName = "paramore.example.worker";
                    
                    var subscriptions = new Subscription[]
                    {
                        new AzureServiceBusSubscription<GreetingAsyncEvent>(
                            new SubscriptionName(GreetingEventAsyncMessageMapper.Topic),
                            new ChannelName(subscriptionName),
                            new RoutingKey(GreetingEventAsyncMessageMapper.Topic),
                            timeoutInMilliseconds: 400,
                            makeChannels: OnMissingChannel.Create,
                            requeueCount: 3,
                            isAsync: true),
                        new AzureServiceBusSubscription<GreetingEvent>(
                            new SubscriptionName(GreetingEventMessageMapper.Topic),
                            new ChannelName(subscriptionName),
                            new RoutingKey(GreetingEventMessageMapper.Topic),
                            timeoutInMilliseconds: 400,
                            makeChannels: OnMissingChannel.Create,
                            requeueCount: 3,
                            isAsync: false),
                        new AzureServiceBusSubscription<AddGreetingCommand>(
                            new SubscriptionName(AddGreetingMessageMapper.Topic),
                            new ChannelName(subscriptionName),
                            new RoutingKey(AddGreetingMessageMapper.Topic),
                            timeoutInMilliseconds: 400,
                            makeChannels: OnMissingChannel.Create,
                            requeueCount: 3,
                            isAsync: true)
                    };
                    
                    string dbConnString = "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password1!;Application Name=BrighterTests";
            
                    //EF
                    services.AddDbContext<GreetingsDataContext>(o =>
                    {
                        o.UseSqlServer(dbConnString);
                    });

                    var outboxConfig = new MsSqlConfiguration(dbConnString, "BrighterOutbox");
                    
                    //TODO: add your ASB qualified name here
                    var clientProvider = new ServiceBusVisualStudioCredentialClientProvider("fim-development-bus.servicebus.windows.net");

                    var asbConsumerFactory = new AzureServiceBusConsumerFactory(clientProvider, false);
                    services.AddServiceActivator(options =>
                        {
                            options.Subscriptions = subscriptions;
                            options.ChannelFactory = new AzureServiceBusChannelFactory(asbConsumerFactory);
                            options.UseScoped = false;
                        }).UseMsSqlOutbox(outboxConfig, typeof(MsSqlSqlAuthConnectionProvider))
                        .UseMsSqlTransactionConnectionProvider(typeof(MsSqlEntityFrameworkCoreConnectionProvider<GreetingsDataContext>))
                        .UseExternalBus(AzureServiceBusMessageProducerFactory.Get(clientProvider))
                        .AutoFromAssemblies();

                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddConsole();
                })
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }
    }
}
