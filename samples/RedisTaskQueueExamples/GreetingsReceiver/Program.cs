using System;
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
    internal class Program
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
                    var connections = new Connection[]
                    {
                        new Connection<GreetingEvent>(
                            new ConnectionName("paramore.example.greeting"),
                            new ChannelName("greeting.event"),
                            new RoutingKey("greeting.event"),
                            timeoutInMilliseconds: 200)
                    };

                    //create the gateway
                    var redisConnection = new RedisMessagingGatewayConfiguration
                    {
                        RedisConnectionString = "localhost:6379?connectTimeout=1&sendTImeout=1000&",
                        MaxPoolSize = 10,
                        MessageTimeToLive = TimeSpan.FromMinutes(10)
                    };

                    var redisConsumerFactory = new RedisMessageConsumerFactory(redisConnection);
                    services.AddServiceActivator(options =>
                    {
                        options.Connections = connections;
                        options.ChannelFactory = new ChannelFactory(redisConsumerFactory);
                        var outBox = new InMemoryOutbox();
                        options.BrighterMessaging = new BrighterMessaging(outBox, outBox, new RedisMessageProducer(redisConnection), null);
                    }).AutoFromAssemblies();


                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();
        }
    }
}
