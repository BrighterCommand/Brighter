﻿#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.IO;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Greetings.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(configurationBuilder =>
    {
        configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
        configurationBuilder.AddJsonFile("appsettings.json", optional: true);
        configurationBuilder.AddCommandLine(args);
    })
    .ConfigureLogging((context, builder) =>
    {
        builder.AddConsole();
        builder.AddDebug();
    })
    .ConfigureServices((hostContext, services) =>
    {
        var subscriptions = new KafkaSubscription[]
        {
            new KafkaSubscription<GreetingEvent>(
                new SubscriptionName("paramore.example.greeting"),
                channelName: new ChannelName("greeting.event"),
                routingKey: new RoutingKey("greeting.event"),
                groupId: "kafka-GreetingsReceiverConsole-Sample",
                timeoutInMilliseconds: 100,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                sweepUncommittedOffsetsIntervalMs: 10000,
                runAsync: true)
        };

        //We take a direct dependency on the schema registry in the message mapper
        var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081" };
        var cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
        services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);

        services.AddServiceActivator(options =>
        {
            options.Subscriptions = subscriptions;
            options.ChannelFactory = new ChannelFactory(
                new KafkaMessageConsumerFactory(
                    new KafkaMessagingGatewayConfiguration
                    {
                        Name = "paramore.brighter", BootStrapServers = new[] { "localhost:9092" }
                    }
                ));
        }).AutoFromAssemblies();


        services.AddHostedService<ServiceActivatorHostedService>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
