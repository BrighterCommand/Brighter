using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationAnalytics.Database;
using SalutationPorts.EntityGateway;
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
            host.CreateOutbox();
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
                    ConfigureEFCore(hostContext, services);
                    
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
                        })
                        .UseExternalBus(new RmqProducerRegistryFactory(
                                new RmqMessagingGatewayConnection
                                {
                                    AmpqUri = new AmqpUriSpecification(new Uri($"amqp://guest:guest@{host}:5672")),
                                    Exchange = new Exchange("paramore.brighter.exchange"),
                                },
                                new RmqPublication[] {
                                    new RmqPublication
                                    {
                                        Topic = new RoutingKey("SalutationReceived"),
                                        MaxOutStandingMessages = 5,
                                        MaxOutStandingCheckIntervalMilliSeconds = 500,
                                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                                        MakeChannels = OnMissingChannel.Create
                                    }}
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
                })
                .UseConsoleLifetime();

        private static string GetEnvironment()
        {
            //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }
        
        private static void ConfigureEFCore(HostBuilderContext hostContext, IServiceCollection services)
        {
            string connectionString = DbConnectionString(hostContext);

            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                services.AddDbContext<SalutationsEntityGateway>(
                    builder =>
                    {
                        builder.UseSqlite(connectionString, 
                            optionsBuilder =>
                            {
                                optionsBuilder.MigrationsAssembly("Salutations_SqliteMigrations");
                            });
                    });
            }
            else
            {
               services.AddDbContextPool<SalutationsEntityGateway>(builder =>
               {
                   builder
                       .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), optionsBuilder =>
                       {
                           optionsBuilder.MigrationsAssembly("Salutations_MySqlMigrations");
                       })
                       .EnableDetailedErrors()
                       .EnableSensitiveDataLogging();
               });
            }
        }

        private static IAmAnInbox ConfigureInbox(HostBuilderContext hostContext)
        {
            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                return new SqliteInbox(new SqliteInboxConfiguration(DbConnectionString(hostContext), SchemaCreation.INBOX_TABLE_NAME));
            }

            return new MySqlInbox(new MySqlInboxConfiguration(DbConnectionString(hostContext), SchemaCreation.INBOX_TABLE_NAME));
        }
        
        private static string DbConnectionString(HostBuilderContext hostContext)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return hostContext.HostingEnvironment.IsDevelopment() ? "Filename=Salutations.db;Cache=Shared" : hostContext.Configuration.GetConnectionString("Salutations");
        }

    }
}
