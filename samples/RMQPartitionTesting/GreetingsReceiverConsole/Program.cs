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

using System;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

namespace GreetingsReceiverConsole
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    var subscriptions = new Subscription[]
                    {
                        new RmqSubscription<GreetingEvent>(
                            new SubscriptionName("paramore.example.greeting"),
                            channelName:new ChannelName("greeting.event"),
                            routingKey:new RoutingKey("greeting.event"),
                            bufferSize: 10,
                            timeoutInMilliseconds: 200,
                            isDurable: true,
                            highAvailability: true,
                            makeChannels: OnMissingChannel.Create)
                    };

                    var rmqConnection = new RmqMessagingGatewayConnection
                    {
                        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                        Exchange = new Exchange("paramore.brighter.exchange")
                    };

                    var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

                    var outBox = new InMemoryOutbox();
                    services.AddServiceActivator(options =>
                    {
                        options.Subscriptions = subscriptions;
                        options.ChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
                    }).AutoFromAssemblies();

                    services.AddSingleton<IAmAnOutboxViewer<Message>>(outBox);

                    services.AddHostedService<ServiceActivatorHostedService>();
             

                })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();
            

            await host.RunAsync();
        }
    }
}
