using System;
using System.Threading;
using System.Threading.Tasks;
using Events;
using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

namespace CompetingReceiverConsole
{
    internal static class Program
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
                    var subscriptions = new Subscription[]
                    {
                    new Subscription<CompetingConsumerCommand>(
                        new SubscriptionName("paramore.example.multipleconsumer.command"),
                        new ChannelName("multipleconsumer.command"),
                        new RoutingKey("multipleconsumer.command"),
                        timeOut: TimeSpan.FromMilliseconds(200))
                    };

                    var messagingConfiguration = new RelationalDatabaseConfiguration(
                        @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", 
                        databaseName: "BrighterSqlQueue", 
                        queueStoreTable: "QueueData");
                    var messageConsumerFactory = new MsSqlMessageConsumerFactory(messagingConfiguration);

                    services.AddConsumers(options =>
                    {
                        options.Subscriptions = subscriptions;
                        options.DefaultChannelFactory = new ChannelFactory(messageConsumerFactory);
                    })
                    // InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
                    // Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
                    .UseScheduler(new InMemorySchedulerFactory())
                    .AutoFromAssemblies();

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

    internal sealed class RunStuff : IHostedService
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
