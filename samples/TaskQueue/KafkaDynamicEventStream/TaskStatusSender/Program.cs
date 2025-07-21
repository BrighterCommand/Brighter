using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;
using Polly;
using Polly.Registry;
using TaskStatus.Driving_Ports;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(configurationBuilder =>
    {
        configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
        configurationBuilder.AddJsonFile("appsettings.json", optional: true);
        configurationBuilder.AddCommandLine(args);
    })
    .ConfigureLogging((context, builder) =>
    {
        builder.ClearProviders();
        builder.AddConsole();
        builder.AddDebug();
    })
    .ConfigureServices((hostContext, services) =>
    {
        var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[]
        {
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
        });

        var circuitBreakerPolicy =
            Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

        var retryPolicyAsync = Policy.Handle<Exception>().WaitAndRetryAsync(new[]
        {
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
        });

        var circuitBreakerPolicyAsync = Policy.Handle<Exception>()
            .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));

        var policyRegistry = new PolicyRegistry
        {
            { CommandProcessor.RETRYPOLICY, retryPolicy },
            { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
            { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
            { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
        };

        var producerRegistry = new KafkaProducerRegistryFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
                },
                [
                    new KafkaPublication<TaskCreated>()
                    {
                        //same topic for both TaskCreated and TaskUpdated, but different cloud events types
                        Topic = new RoutingKey("task.update"),
                        Type = "io.goparamore.task.created",
                        NumPartitions = 3,
                        MessageSendMaxRetries = 3,
                        MessageTimeoutMs = 1000,
                        MaxInFlightRequestsPerConnection = 1
                    },
                    new KafkaPublication<TaskUpdated>()
                    {
                        Topic = new RoutingKey("task.update"),
                        Type = "io.goparamore.task.updated",
                        NumPartitions = 3,
                        MessageSendMaxRetries = 3,
                        MessageTimeoutMs = 1000,
                        MaxInFlightRequestsPerConnection = 1
                    }
                ])
            .Create();

        services.AddBrighter(options =>
            {
                options.PolicyRegistry = policyRegistry;
            })
            .AddProducers((configure) =>
            {
                configure.ProducerRegistry = producerRegistry;
            })
            .MapperRegistryFromAssemblies([typeof(TaskCreated).Assembly]);

        services.AddHostedService<TimedMessageGenerator>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();

