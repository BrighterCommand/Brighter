using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationAnalytics.Database;
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
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddJsonFile("hostsettings.json", optional: true);
                    configHost.AddEnvironmentVariables(prefix: "BRIGHTER_");
                    configHost.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var subscriptions = new Subscription[]
                    {
                        new RmqSubscription<GreetingMade>(
                            new SubscriptionName("paramore.sample.salutationanalytics"),
                            new ChannelName("SalutationAnalytics"),
                            new RoutingKey("GreetingMade"),
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
                        })
                        .AutoFromAssemblies();

                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .UseConsoleLifetime();
    }
}
