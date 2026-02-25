#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using RMQAsyncAPI.Events;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.asyncapi.exchange"),
};

var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

// Build a producer registry with a typed Publication<T> to demonstrate RequestType auto-discovery
var producerRegistry = new RmqProducerRegistryFactory(
    rmqConnection,
    new RmqPublication<OrderCreatedEvent>[]
    {
        new()
        {
            WaitForConfirmsTimeOutInMilliseconds = 1000,
            MakeChannels = OnMissingChannel.Create,
            Topic = new RoutingKey("order.created")
        }
    }).Create();

var host = new HostBuilder()
    .ConfigureServices((_, services) =>
    {
        services.AddConsumers(options =>
            {
                options.Subscriptions = new Subscription[]
                {
                    new RmqSubscription<PaymentReceivedEvent>(
                        new SubscriptionName("paramore.asyncapi.payment"),
                        new ChannelName("payment.received"),
                        new RoutingKey("payment.received"),
                        timeOut: TimeSpan.FromMilliseconds(200),
                        messagePumpType: MessagePumpType.Reactor,
                        makeChannels: OnMissingChannel.Create)
                };
                options.DefaultChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = producerRegistry;
            })
            .UseAsyncApi(opts =>
            {
                opts.Title = "RMQ AsyncAPI Sample";
                opts.Version = "1.0.0";
                opts.Description = "Sample demonstrating AsyncAPI 3.0 generation with RabbitMQ, showcasing subscription, publication, and assembly scanning discovery";
                opts.Servers = new Dictionary<string, AsyncApiServer>
                {
                    ["rabbitmq"] = new AsyncApiServer
                    {
                        Host = "localhost:5672",
                        Protocol = "amqp",
                        Description = "Local RabbitMQ broker"
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
    var document = host.GenerateAsyncApiDocument("asyncapi.json");
    Console.WriteLine($"AsyncAPI document generated: {document.Info.Title} v{document.Info.Version}");
    return;
}

Console.WriteLine("Run with --generate-asyncapi to generate the AsyncAPI document.");
