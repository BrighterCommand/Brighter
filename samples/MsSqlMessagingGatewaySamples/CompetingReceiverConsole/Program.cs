using System;
using System.Threading;
using System.Threading.Tasks;
using Events;
using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

namespace CompetingReceiverConsole
{
    internal class Program
    {
        private static async Task Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    var connections = new Connection[]
                    {
                    new Connection<CompetingConsumerCommand>(
                        new ConnectionName("paramore.example.multipleconsumer.command"),
                        new ChannelName("multipleconsumer.command"),
                        new RoutingKey("multipleconsumer.command"),
                        timeoutInMilliseconds: 200)
                    };

                    var messagingConfiguration = new MsSqlMessagingGatewayConfiguration(@"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
                    var messageConsumerFactory = new MsSqlMessageConsumerFactory(messagingConfiguration);

                    services.AddServiceActivator(options =>
                    {
                        options.Connections = connections;
                        options.ChannelFactory = new ChannelFactory(messageConsumerFactory);
                        var outBox = new InMemoryOutbox();
                        options.BrighterMessaging = new BrighterMessaging(outBox, outBox, new MsSqlMessageProducer(messagingConfiguration), null);
                    }).AutoFromAssemblies();


                    services.AddHostedService<ServiceActivatorHostedService>();
                    services.AddHostedService<RunStuff>();

                    services.AddSingleton<IAmACommandCounter, CommandCounter>();
                })
                
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();
        }
    }

    internal class RunStuff : IHostedService
    {
        private readonly IAmACommandCounter _commandCounter;

        public RunStuff(IAmACommandCounter commandCounter)
        {
            _commandCounter = commandCounter;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"There were {_commandCounter.Counter} commands handled by this consumer");

            await Task.CompletedTask;
        }
    }
}
