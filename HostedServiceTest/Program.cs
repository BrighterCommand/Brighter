using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.HostedService;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using Paramore.Brighter.ServiceActivator;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

namespace HostedServiceTest
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
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
                            timeoutInMilliseconds: 200,
                            isDurable: true,
                            highAvailability: true)
                    };

                    var rmqConnnection = new RmqMessagingGatewayConnection
                    {
                        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                        Exchange = new Exchange("paramore.brighter.exchange")
                    };

                    var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnnection);

                    services.AddServiceActivator(options =>
                        {
                            options.Connections = connections;
                            options.ChannelFactory = new InputChannelFactory(rmqMessageConsumerFactory);
                        })
                        .MapperRegistryFromAssemblies(typeof(GreetingEventHandler).Assembly)
                        .HandlersFromAssemblies(typeof(GreetingEventHandler).Assembly);

                    services.AddSingleton<ILoggerFactory>(x => new SerilogLoggerFactory());
                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}