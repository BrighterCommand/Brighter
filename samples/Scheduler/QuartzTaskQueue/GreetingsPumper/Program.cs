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
using Paramore.Brighter.MessageScheduler.Quartz;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Quartz;
using Serilog;

namespace GreetingsPumper;

class Program
{
    private static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var host = new HostBuilder()
            .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddSingleton<QuartzBrighterJob>()
                        .AddQuartz(opt =>
                        {
                            opt.SchedulerId = "QuartzBrighter";
                            opt.SchedulerName = "QuartzBrighter";
                            opt.UseSimpleTypeLoader();
                            opt.UseInMemoryStore();
                        })
                        .AddQuartzHostedService(opt =>
                        {
                            opt.WaitForJobsToComplete = true;
                        });

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
                                    Topic = new RoutingKey(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                                    RequestType = typeof(GreetingEvent)
                                },
                                new SnsPublication
                                {
                                    Topic =
                                        new RoutingKey(typeof(FarewellEvent).FullName.ToValidSNSTopicName(true)),
                                    SnsAttributes = new SnsAttributes { Type = SnsSqsType.Fifo }
                                }
                            ]
                        ).Create();

                        services.AddBrighter()
                            .UseExternalBus((configure) =>
                            {
                                configure.ProducerRegistry = producerRegistry;
                            })
                            .UseMessageScheduler(provider =>
                            {
                                var factory = provider.GetRequiredService<ISchedulerFactory>();
                                return new QuartzMessageSchedulerFactory(
                                    factory.GetScheduler().GetAwaiter().GetResult());
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

    internal class RunCommandProcessor(IAmACommandProcessor commandProcessor, ILogger<RunCommandProcessor> logger)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            long loop = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                loop++;

                logger.LogInformation("Scheduling message #{Loop}", loop);
                commandProcessor.SchedulerPost(new GreetingEvent($"Scheduler message Ian #{loop}"),
                    TimeSpan.FromMinutes(1));
                
                if (loop % 100 != 0)
                {
                    continue;
                }

                logger.LogInformation("Pausing for breath...");
                await Task.Delay(4000, stoppingToken);
            }
        }
    }
}
