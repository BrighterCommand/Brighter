using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.MsSql;
using Serilog;

namespace CompetingSender
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: MultipleSender <count>");
                Console.WriteLine("eg   : MultipleSender 500");
                return;
            }

            if (!int.TryParse(args[0], out int repeatCount))
            {
                Console.WriteLine($"{args[0]} is not a valid number");
                return;
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    //create the gateway
                    var messagingConfiguration = new MsSqlMessagingGatewayConfiguration(@"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");

                    services.AddBrighter(options =>
                    {
                        var outBox = new InMemoryOutbox();
                        options.BrighterMessaging = new BrighterMessaging(outBox, outBox, new MsSqlMessageProducer(messagingConfiguration), null);
                    }).AutoFromAssemblies();

                    services.AddHostedService<RunCommandProcessor>(provider => new RunCommandProcessor(provider.GetService<IAmACommandProcessor>(),  repeatCount));
                })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();

        }

        internal class RunCommandProcessor : IHostedService
        {
            private readonly IAmACommandProcessor _commandProcessor;
            private readonly int _repeatCount;

            public RunCommandProcessor(IAmACommandProcessor commandProcessor, int repeatCount)
            {
                _commandProcessor = commandProcessor;
                _repeatCount = repeatCount;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                using (new TransactionScope(TransactionScopeOption.RequiresNew,
                    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    Console.WriteLine($"Sending {_repeatCount} command messages");
                    var sequenceNumber = 1;
                    for (int i = 0; i < _repeatCount; i++)
                    {
                        _commandProcessor.Post(new CompetingConsumerCommand(sequenceNumber++));
                    }
                    // We do NOT complete the transaction here to show that a message is
                    // always queued, whether the transaction commits or aborts!
                }

                await Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
    }

