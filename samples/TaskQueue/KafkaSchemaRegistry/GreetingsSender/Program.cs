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
using Confluent.SchemaRegistry;
using Greetings.Ports.Commands;
using GreetingsSender;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;
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
    .ConfigureServices((hostContext, services) =>
    {
        //We take a direct dependency on the schema registry in the message mapper
        var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081" };
        var cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
        services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);

        services.AddBrighter(options =>
            {
                options.PolicyRegistry = RegisterPolicies();
            })
            .AddProducers((configure) =>
            {
                configure.ProducerRegistry = new KafkaProducerRegistryFactory(
                        new KafkaMessagingGatewayConfiguration
                        {
                            Name = "paramore.brighter.greetingsender", BootStrapServers = ["localhost:9092"]
                        },
                        [
                            new KafkaPublication
                            {
                                Topic = new RoutingKey("greeting.event"),
                                RequestType = typeof(GreetingEvent),
                                MessageSendMaxRetries = 3,
                                MessageTimeoutMs = 1000,
                                MaxInFlightRequestsPerConnection = 1
                            }
                        ])
                    .Create();
            })
            .AutoFromAssemblies();

        services.AddHostedService<TimedMessageGenerator>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
return;

static PolicyRegistry? RegisterPolicies()
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
    return policyRegistry;
}
