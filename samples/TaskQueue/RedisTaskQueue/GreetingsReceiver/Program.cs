using System;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Greetings.Ports.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

namespace GreetingsReceiver
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    var subscriptions = new Subscription[]
                    {
                        new RedisSubscription<GreetingEvent>(
                            new SubscriptionName("paramore.example.greeting"),
                            new ChannelName("greeting.event"),
                            new RoutingKey("greeting.event"),
                            timeOut: TimeSpan.FromSeconds(1))
                    };

                    //create the gateway
                    var redisConnection = new RedisMessagingGatewayConfiguration
                    {
                        RedisConnectionString = "localhost:6379?connectTimeout=1&sendTImeout=1000&",
                        MaxPoolSize = 10,
                        MessageTimeToLive = TimeSpan.FromMinutes(10)
                    };

                    var redisConsumerFactory = new RedisMessageConsumerFactory(redisConnection);
                    services.AddConsumers(options =>
                    {
                        options.Subscriptions = subscriptions;
                        options.DefaultChannelFactory = new ChannelFactory(redisConsumerFactory);
                    })
                    .AutoFromAssemblies();


                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            Console.CancelKeyPress += (_, _) => host.StopAsync().Wait();
            await host.RunAsync();
        }
    }
}
