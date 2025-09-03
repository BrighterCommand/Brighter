#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Serilog;
using Serilog.Extensions.Logging;

namespace GreetingsSender
{
    static class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                Exchange = new Exchange("paramore.brighter.exchange"),
            };

            serviceCollection
                .AddBrighter()
                .AddProducers((configure) =>
                {
                    configure.ProducerRegistry = new RmqProducerRegistryFactory(
                        rmqConnection,
                        [
                            new RmqPublication
                            {
                                Topic = new RoutingKey("Greeting.Request"),
                                RequestType = typeof(GreetingRequest)
                            }
                        ]).Create();
                    configure.UseRpc = true;
                    configure.ReplyQueueSubscriptions =
                    [
                        new RmqSubscription(
                            new SubscriptionName("ReplySubscription"), 
                            new ChannelName("ReplyChannel"), 
                            new RoutingKey("Reply"), 
                            typeof(GreetingReply),
                            messagePumpType: MessagePumpType.Reactor
                        )
                    ];
                    configure.ResponseChannelFactory = new ChannelFactory(new RmqMessageConsumerFactory(rmqConnection));
                })
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            Console.WriteLine("Requesting Salutation...");

            //blocking call
            commandProcessor.Call<GreetingRequest, GreetingReply>(
                new GreetingRequest
                {
                    Name = "Ian", Language = "en-gb"
                }, 
                timeOut: TimeSpan.FromMilliseconds(2000)
            );

            Console.WriteLine("Done...");
            Console.ReadLine();
        }
    }
}
