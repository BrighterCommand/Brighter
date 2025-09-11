using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
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

            var awsConnection = new AWSMessagingGatewayConnection(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1,
                cfg =>
                {
                    var serviceURL = "http://localhost:4566/";
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
                    }
                ]
            ).Create();

            services.AddBrighter()
                .AddProducers((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                })
                .UseScheduler(provider =>
                {
                    var factory = provider.GetRequiredService<ISchedulerFactory>();
                    return new QuartzSchedulerFactory(
                        factory.GetScheduler().GetAwaiter().GetResult());
                })
                .AutoFromAssemblies([typeof(GreetingEvent).Assembly]);

            services.AddHostedService<RunCommandProcessor>();
        }
    )
    .UseConsoleLifetime()
    .UseSerilog()
    .Build();


Console.CancelKeyPress += (_, _) => host.StopAsync().Wait();

await host.RunAsync();

internal sealed class RunCommandProcessor(IAmACommandProcessor commandProcessor, ILogger<RunCommandProcessor> logger)
        : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long loop = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            loop++;

            logger.LogInformation("Scheduling message #{Loop}", loop);
            commandProcessor.Post(TimeSpan.FromSeconds(10), new GreetingEvent($"Scheduler message Ian #{loop}"));
            
            if (loop % 100 != 0)
            {
                continue;
            }

            logger.LogInformation("Pausing for breath...");
            await Task.Delay(4000, stoppingToken);
        }
    }
}
