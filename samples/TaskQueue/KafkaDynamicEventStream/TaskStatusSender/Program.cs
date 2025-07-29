#region Licence

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.MessagingGateway.Kafka;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using TaskStatus.Driving_Ports;
using TaskStatus.Ports;
using TaskStatusSender;

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
        services.AddBrighter(options =>
            {
                options.PolicyRegistry = new DefaultPolicy();
            })
            .AddProducers((configure) =>
            {
                configure.ProducerRegistry = new KafkaProducerRegistryFactory(
                        new KafkaMessagingGatewayConfiguration { Name = "paramore.brighter.greetingsender", BootStrapServers = ["localhost:9092"] },
                        [
                            new KafkaPublication<TaskCreated>()
                            {
                                //the same topic for both TaskCreated and TaskUpdated, but different cloud events types
                                Topic = new RoutingKey("task.update"),
                                Type = new CloudEventsType("io.goparamore.task.created"),
                                NumPartitions = 3,
                                MessageSendMaxRetries = 3,
                                MessageTimeoutMs = 1000,
                                MaxInFlightRequestsPerConnection = 1
                            },
                            new KafkaPublication<TaskUpdated>()
                            {
                                Topic = new RoutingKey("task.update"),
                                Type = new CloudEventsType("io.goparamore.task.updated"),
                                NumPartitions = 3,
                                MessageSendMaxRetries = 3,
                                MessageTimeoutMs = 1000,
                                MaxInFlightRequestsPerConnection = 1
                            }
                        ])
                    .Create();
            })
            //This is the default mapper type, but we are  explicit  for the sample anyway
            .AutoFromAssemblies([typeof(TaskCreated).Assembly], defaultMessageMapper: typeof(JsonMessageMapper<>), asyncDefaultMessageMapper: typeof(JsonMessageMapper<>));


        services.AddHostedService<TimedStatusSender>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
