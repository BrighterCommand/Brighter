#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.AsyncAPI;
using Paramore.Brighter.AsyncAPI.Model;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using KafkaAsyncAPI.Events;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var kafkaConfig = new KafkaMessagingGatewayConfiguration
{
    Name = "paramore.brighter.kafkaasyncapi",
    BootStrapServers = new[] { "localhost:9092" }
};

var kafkaMessageConsumerFactory = new KafkaMessageConsumerFactory(kafkaConfig);

var producerRegistry = new KafkaProducerRegistryFactory(
    kafkaConfig,
    new KafkaPublication<OrderCreatedEvent>[]
    {
        new()
        {
            Topic = new RoutingKey("order.created"),
            NumPartitions = 3,
            MessageSendMaxRetries = 3,
            MessageTimeoutMs = 1000,
            MaxInFlightRequestsPerConnection = 1
        }
    }).Create();

var host = new HostBuilder()
    .ConfigureServices((_, services) =>
    {
        services.AddConsumers(options =>
            {
                options.Subscriptions = new Subscription[]
                {
                    new KafkaSubscription<PaymentReceivedEvent>(
                        new SubscriptionName("paramore.asyncapi.payment"),
                        new ChannelName("payment.received"),
                        new RoutingKey("payment.received"),
                        groupId: "kafka-asyncapi-sample",
                        timeOut: TimeSpan.FromMilliseconds(200),
                        messagePumpType: MessagePumpType.Reactor,
                        makeChannels: OnMissingChannel.Create)
                };
                options.DefaultChannelFactory = new ChannelFactory(kafkaMessageConsumerFactory);
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = producerRegistry;
            })
            .UseAsyncApi(opts =>
            {
                opts.Title = "Kafka AsyncAPI Sample";
                opts.Version = "1.0.0";
                opts.Description = "Sample demonstrating AsyncAPI 3.0 generation with Kafka, showcasing subscription, publication, and assembly scanning discovery";
                opts.Servers = new Dictionary<string, AsyncApiServer>
                {
                    ["kafka"] = new AsyncApiServer
                    {
                        Host = "localhost:9092",
                        Protocol = "kafka",
                        Description = "Local Kafka broker"
                    }
                };
            })
            .AutoFromAssemblies();
    })
    .UseSerilog()
    .Build();

// Generate AsyncAPI document if --generate-asyncapi argument is provided
if (args.Length > 0 && args[0] == "--generate-asyncapi")
{
    var document = await host.GenerateAsyncApiDocumentAsync("asyncapi.json");
    Console.WriteLine($"AsyncAPI document generated: {document.Info.Title} v{document.Info.Version}");
    return;
}

Console.WriteLine("Run with --generate-asyncapi to generate the AsyncAPI document.");
