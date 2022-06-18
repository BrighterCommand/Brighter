using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationAnalytics.Database;
using SalutationPorts.Policies;
using SalutationPorts.Requests;

namespace SalutationAnalytics
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            host.CheckDbIsUp();
            host.MigrateDatabase();
            host.CreateInbox();
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(configurationBuilder =>
                {
                    configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
                    configurationBuilder.AddJsonFile("appsettings.json", optional: true);
                    configurationBuilder.AddJsonFile($"appsettings.{GetEnvironment()}.json", optional: true);
                    configurationBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");  //NOTE: Although not web, we use this to grab the environment
                    configurationBuilder.AddEnvironmentVariables(prefix: "BRIGHTER_");
                    configurationBuilder.AddCommandLine(args);
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                })
                .ConfigureServices((hostContext, services) =>
                {      
                    ConfigureMigration(hostContext, services);
                    ConfigureBrighter(hostContext, services);
                })
                .UseConsoleLifetime();

        private static void ConfigureBrighter(HostBuilderContext hostContext, IServiceCollection services)
        {
            var subscriptions = new Subscription[]
            {
                new RmqSubscription<GreetingMade>(
                    new SubscriptionName("paramore.sample.salutationanalytics"),
                    new ChannelName("SalutationAnalytics"),
                    new RoutingKey("GreetingMade"),
                    runAsync: true,
                    timeoutInMilliseconds: 200,
                    isDurable: true,
                    makeChannels: OnMissingChannel.Create), //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
            };

            var host = hostContext.HostingEnvironment.IsDevelopment() ? "localhost" : "rabbitmq";

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri($"amqp://guest:guest@{host}:5672")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

            services.AddServiceActivator(options =>
                {
                    options.Subscriptions = subscriptions;
                    options.ChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
                    options.UseScoped = true;
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.PolicyRegistry = new SalutationPolicy();
                })
                .UseExternalBus(new RmqProducerRegistryFactory(
                        new RmqMessagingGatewayConnection
                        {
                            AmpqUri = new AmqpUriSpecification(new Uri($"amqp://guest:guest@{host}:5672")),
                            Exchange = new Exchange("paramore.brighter.exchange"),
                        },
                        new RmqPublication[]
                        {
                            new RmqPublication
                            {
                                Topic = new RoutingKey("SalutationReceived"),
                                MaxOutStandingMessages = 5,
                                MaxOutStandingCheckIntervalMilliSeconds = 500,
                                WaitForConfirmsTimeOutInMilliseconds = 1000,
                                MakeChannels = OnMissingChannel.Create
                            }
                        }
                    ).Create()
                )
                .AutoFromAssemblies()
                .UseExternalInbox(
                    ConfigureInbox(hostContext),
                    new InboxConfiguration(
                        scope: InboxScope.All,
                        onceOnly: true,
                        actionOnExists: OnceOnlyAction.Warn
                    )
                );

            services.AddHostedService<ServiceActivatorHostedService>();
        }

        private static void ConfigureMigration(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            
        }

        private static IAmAnInbox ConfigureInbox(HostBuilderContext hostContext)
        {
            //TODO: Set up an Inbox
            //return new DynamoDbInbox();
            return null;
        }
        
        private static string GetEnvironment()
        {
            //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }
    }
}
