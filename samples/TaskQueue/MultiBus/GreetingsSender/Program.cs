#region Licence

/* The MIT License (MIT)
Copyright © 2017 Wayne Hunsley <whunsley@gmail.com>
Copyright © 2021 Ian Cooper Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.IO;
using Greetings.Ports.Commands;
using GreetingsSender;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Polly;
using Polly.Registry;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(configurationBuilder =>
    {
        configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
        configurationBuilder.AddJsonFile("appsettings.json", optional: true);
        configurationBuilder.AddCommandLine(args);
    })
    .ConfigureLogging((_, builder) =>
    {
        builder.ClearProviders();
        builder.AddConsole();
        builder.AddDebug();
    })
    .ConfigureServices((_, services) =>
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

        var kafkaMessageProducerFactory = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
                },
                [
                    new KafkaPublication
                    {
                        Topic = new RoutingKey("greeting.event"),
                        RequestType = typeof(GreetingEvent),
                        NumPartitions = 3,
                        MessageSendMaxRetries = 3,
                        MessageTimeoutMs = 1000,
                        MaxInFlightRequestsPerConnection = 1
                    }
                ]);

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange"),
        };
        
        var rmqMessageProducerFactory = new RmqMessageProducerFactory(
            rmqConnection,
            [
                new RmqPublication
                {
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("another.greeting.event"),
                    RequestType = typeof(AnotherGreetingEvent)
                }
            ]);

        services.AddBrighter(options =>
            {
                options.PolicyRegistry = policyRegistry;
            })
            .AddProducers((configure) =>
            {
                configure.ProducerRegistry = new CombinedProducerRegistryFactory(
                    rmqMessageProducerFactory, 
                    kafkaMessageProducerFactory)
                    .Create();
            })
            .AutoFromAssemblies();

        services.AddHostedService<TimedMessageGenerator>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
