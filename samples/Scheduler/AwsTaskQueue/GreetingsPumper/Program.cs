using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessageScheduler.AWS.V4;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Serilog;

namespace GreetingsPumper;

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
                        // var serviceURL = "http://localhost:4566/"; // Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
                        // var region = string.IsNullOrWhiteSpace(serviceURL)
                        //     ? RegionEndpoint.EUWest1
                        //     : RegionEndpoint.USEast1;
                        var serviceURL = string.Empty;
                        var region = RegionEndpoint.USEast1;
                        var awsConnection = new AWSMessagingGatewayConnection(credentials, region,
                            cfg =>
                            {
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
                                        .ToValidSNSTopicName()),
                                    RequestType = typeof(GreetingEvent)
                                },
                                new SnsPublication
                                {
                                    Topic = new RoutingKey(
                                        typeof(FarewellEvent).FullName.ToValidSNSTopicName(true)),
                                    TopicAttributes = new SnsAttributes { Type = SqsType.Fifo },
                                    RequestType = typeof(FarewellEvent)
                                },
                                new SnsPublication
                                {
                                    Topic = new RoutingKey("message-scheduler-topic"),
                                    RequestType = typeof(FireAwsScheduler)
                                }
                            ]
                        ).Create();

                        services.AddBrighter()
                            .AddProducers(configure =>
                            {
                                configure.ProducerRegistry = producerRegistry;
                            })
                            .UseScheduler(new AwsSchedulerFactory(awsConnection, "brighter-scheduler")
                            {
                                SchedulerTopicOrQueue = new RoutingKey("message-scheduler-topic"),
                                OnConflict = OnSchedulerConflict.Overwrite
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

    internal sealed class RunCommandProcessor(IAmACommandProcessor commandProcessor, ILogger<RunCommandProcessor> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            long loop = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                loop++;

                logger.LogInformation("Scheduling message #{Loop}", loop);
                commandProcessor.Post(TimeSpan.FromMinutes(1), new GreetingEvent($"Ian #{loop}"));

                if (loop % 100 != 0)
                {
                    continue;
                }
                
                logger.LogInformation("Pausing for breath...");
                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
