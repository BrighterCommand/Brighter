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
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Scheduler.Events;
using Serilog;

namespace GreetingsPumper;

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
                        var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.USEast1,
                            cfg =>
                            {
                                var serviceURL =
                                    "http://localhost:4566/"; // Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
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
                                    SnsAttributes = new SnsAttributes { Type = SnsSqsType.Fifo },
                                    RequestType = typeof(FarewellEvent)
                                },
                                new SnsPublication
                                {
                                    Topic = new RoutingKey("message-scheduler-topic"),
                                    RequestType = typeof(FireSchedulerMessage)
                                },
                                new SnsPublication
                                {
                                    Topic = new RoutingKey("request-scheduler-topic"),
                                    RequestType = typeof(FireSchedulerRequest)
                                }
                            ]
                        ).Create();

                        services.AddBrighter()
                            .UseExternalBus(configure =>
                            {
                                configure.ProducerRegistry = producerRegistry;
                            })
                            .UseScheduler(new AwsMessageSchedulerFactory(awsConnection, "brighter-scheduler")
                            {
                                MessageSchedulerTopicOrQueue = new RoutingKey("message-scheduler-topic"),
                                RequestSchedulerTopicOrQueue = new RoutingKey("request-scheduler-topic"),
                                OnConflict = OnSchedulerConflict.Overwrite,
                                GetOrCreateMessageSchedulerId = message => message.Id,
                                GetOrCreateRequestSchedulerId = request => request.Id
                            })
                            .AutoFromAssemblies(typeof(GreetingEvent).Assembly);
                    }

                    services.AddHostedService<RunCommandProcessor>();
                }
            )
            .UseConsoleLifetime()
            .UseSerilog()
            .Build();

        await host.RunAsync();
    }

    internal class RunCommandProcessor(IAmACommandProcessor commandProcessor, ILogger<RunCommandProcessor> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            long loop = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                loop++;

                logger.LogInformation("Scheduling message #{Loop}", loop);
                commandProcessor.Post(new GreetingEvent($"Ian #{loop}"));

                if (loop % 100 != 0)
                    continue;

                logger.LogInformation("Pausing for breath...");
                await Task.Delay(4000, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
