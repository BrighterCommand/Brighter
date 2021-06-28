using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using GreetingsReceiverConsole;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Polly.CircuitBreaker;
using Serilog;

namespace GreetingsPumper
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>

                    {
                        var outbox = new InMemoryOutbox();
                        var gatewayConnection = new RmqMessagingGatewayConnection
                        {
                            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                            Exchange = new Exchange("paramore.brighter.exchange")

                        };
                        var producer = new RmqMessageProducer(
                            connection:gatewayConnection, 
                            new RmqPublication
                            {
                                MakeChannels =OnMissingChannel.Create
                            });

                        services.AddBrighter(options =>
                        {
                            options.BrighterMessaging = new BrighterMessaging(producer);
                        })
                            .UseInMemoryOutbox()
                            .AutoFromAssemblies(typeof(GreetingEvent).Assembly);

                        services.AddSingleton<IAmAnOutboxViewer<Message>>(outbox);

                        services.AddHostedService<RunCommandProcessor>();
                        services.AddHostedService<TimedOutboxSweeper>();
                    }
                )
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();
        }

        internal class RunCommandProcessor : IHostedService
        {
            private readonly IAmACommandProcessor _commandProcessor;

            public RunCommandProcessor(IAmACommandProcessor commandProcessor)
            {
                _commandProcessor = commandProcessor;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                long loop = 0;
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        loop++;
                 
                        Console.WriteLine($"Sending message #{loop}");
                        _commandProcessor.Post(new GreetingEvent($"Ian #{loop}"));

                        if (loop % 100 != 0) continue;

                        Console.WriteLine("Pausing for breath...");
                        await Task.Delay(4000, cancellationToken);
                    }
                    catch (BrokenCircuitException)
                    {
                        Console.WriteLine("Can't send to producer, pausing...will retry in 5 seconds");
                        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                    }
                }
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
