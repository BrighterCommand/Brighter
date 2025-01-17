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
using System.Transactions;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Serilog;
using Serilog.Extensions.Logging;

namespace GreetingsSender
{
    class Program
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

            var producerRegistry = new RmqProducerRegistryFactory(
                rmqConnection,
                new RmqPublication[]
                {
                    new()
                    {
                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                        MakeChannels =OnMissingChannel.Create,
                        Topic = new RoutingKey("greeting.event"),
                        RequestType = typeof(GreetingEvent)
                    },
                    new()
                    {
                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                        MakeChannels =OnMissingChannel.Create,
                        Topic = new RoutingKey("farewell.event"),
                        RequestType = typeof(FarewellEvent)
                    }
                }).Create();
            
            serviceCollection
                .AddSingleton<IAmAMessageSchedulerFactory>(new InMemoryMessageSchedulerFactory())
                .AddBrighter()
                .UseExternalBus((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                    configure.MaxOutStandingMessages = 5;
                    configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
                })
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();

            commandProcessor.Post(new GreetingEvent("Ian says: Hi there!"));

            // TODO Remove this code:
            while (true)
            {
                Console.WriteLine("Enter a name to greet (Q to quit):");
                var name = Console.ReadLine();
                if (name is "Q" or "q")
                {
                    break;
                }
                
                commandProcessor.Scheduler(TimeSpan.FromSeconds(60), new GreetingEvent($"Ian says: Hi {name}"));
            }
            
            commandProcessor.Post(new FarewellEvent("Ian says: See you later!"));
        }
    }
}
