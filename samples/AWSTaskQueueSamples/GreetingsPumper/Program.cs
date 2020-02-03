using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Serilog;

namespace GreetingsPumper
{
    class Program
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
                    if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
                    {
                        var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);
                        var producer = new SqsMessageProducer(awsConnection);

                        services.AddBrighter(options =>
                        {
                            var outBox = new InMemoryOutbox();
                            options.BrighterMessaging = new BrighterMessaging(outBox, outBox, producer, null);
                        }).AutoFromAssemblies(typeof(GreetingEvent).Assembly);
                    }

                    services.AddHostedService<RunCommandProcessor>();
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
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    loop++;

                    Console.WriteLine($"Sending message #{loop}");
                    _commandProcessor.Post(new GreetingEvent($"Ian #{loop}"));

                    if (loop % 100 != 0)
                        continue;

                    Console.WriteLine("Pausing for breath...");
                    await Task.Delay(4000, cancellationToken);
                }
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
