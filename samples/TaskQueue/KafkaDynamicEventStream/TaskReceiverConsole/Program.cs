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

using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Paramore.Brighter;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using TaskStatus.Driving_Ports;
using TaskStatus.Ports;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(configurationBuilder =>
    {
        configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
        configurationBuilder.AddJsonFile("appsettings.json", optional: true);
        configurationBuilder.AddCommandLine(args);
    })
    .ConfigureLogging((_, builder) =>
    {
        builder.AddConsole();
        builder.AddDebug();
    })
    .ConfigureServices((_, services) =>
    {
        var subscriptions = new[]
        {
            new KafkaSubscription(
                new SubscriptionName("paramore.example.taskstate"),
                channelName: new ChannelName("task.state"),
                routingKey:new RoutingKey("task.update"),
                getRequestType: message => message switch
                {
                    var m when m.Header.Type == new CloudEventsType("io.goparamore.task.created") => typeof(TaskCreated),
                     var m when m.Header.Type == new CloudEventsType("io.goparamore.task.updated") => typeof(TaskUpdated),
                    _ => throw new ArgumentException($"No type mapping found for message with type {message.Header.Type}", nameof(message)),
                },
                groupId: "kafka-TaskReceiverConsole-Sample",
                timeOut: TimeSpan.FromMilliseconds(100),
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                sweepUncommittedOffsetsInterval: TimeSpan.FromMilliseconds(10000),
                messagePumpType: MessagePumpType.Reactor)
        };

        services.AddConsumers(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = new ChannelFactory(
                new KafkaMessageConsumerFactory(
                    new KafkaMessagingGatewayConfiguration
                    {
                        Name = "paramore.brighter", BootStrapServers = new[] { "localhost:9092" }
                    }
                ));
        })
        //This is the default mapper type, but we are  explicit  for the sample anyway
        .AutoFromAssemblies([typeof(TaskCreated).Assembly], defaultMessageMapper: typeof(JsonMessageMapper<>), asyncDefaultMessageMapper: typeof(JsonMessageMapper<>));


        services.AddHostedService<ServiceActivatorHostedService>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
