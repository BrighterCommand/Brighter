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
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Serilog;

namespace GreetingsPumper
{
    static class Program
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
                            var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.USEast1,
                                cfg =>
                                {
                                    var serviceURL = Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
                                    if (!string.IsNullOrWhiteSpace(serviceURL))
                                    {
                                        cfg.ServiceURL = serviceURL;
                                    }
                                });

                            var producerRegistry = new SnsProducerRegistryFactory(
                                awsConnection,
                                [
                                    new SnsPublication
                                    {
                                        Topic = new RoutingKey(typeof(GreetingEvent).FullName
                                            .ToValidSNSTopicName())
                                    },
                                    new SnsPublication
                                    {
                                        Topic = new RoutingKey(
                                            typeof(FarewellEvent).FullName.ToValidSNSTopicName(true)),
                                        TopicAttributes = new SnsAttributes { Type = SqsType.Fifo }
                                    }
                                ]
                            ).Create();

                            services.AddBrighter()
                                .AddProducers((configure) =>
                                {
                                    configure.ProducerRegistry = producerRegistry;
                                })
                                .AutoFromAssemblies([typeof(GreetingEvent).Assembly]);
                        }

                        services.AddHostedService<RunCommandProcessor>();
                    }
                )
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();
        }

        internal sealed class RunCommandProcessor : IHostedService
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
