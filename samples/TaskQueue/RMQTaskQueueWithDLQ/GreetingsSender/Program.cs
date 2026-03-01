#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using Greetings.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Extensions.DependencyInjection;
using GreetingsSender;

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
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var producerRegistry = new RmqProducerRegistryFactory(
            rmqConnection,
            [
                new()
                {
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("greeting.event"),
                    RequestType = typeof(GreetingEvent)
                }
            ]).Create();

        services
            .AddBrighter()
            // InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
            // Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
            .UseScheduler(new InMemorySchedulerFactory())
            .AddProducers((configure) =>
            {
                configure.ProducerRegistry = producerRegistry;
                configure.MaxOutStandingMessages = 5;
                configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
            })
            .AutoFromAssemblies();

        services.AddHostedService<TimedMessageGenerator>();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
