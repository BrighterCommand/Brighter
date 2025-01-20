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
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AWSSQS;
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

            if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
            {
                var serviceURL = Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
                var region = string.IsNullOrWhiteSpace(serviceURL) ? RegionEndpoint.EUWest1 : RegionEndpoint.USEast1;
                var awsConnection = new AWSMessagingGatewayConnection(credentials, region, cfg =>
                {
                    if (!string.IsNullOrWhiteSpace(serviceURL))
                    {
                        cfg.ServiceURL = serviceURL;
                    }
                });

                var producerRegistry = new SnsProducerRegistryFactory(
                    awsConnection,
                    new SnsPublication[]
                    {
                        new()
                        {
                            Topic = new RoutingKey(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                            RequestType = typeof(GreetingEvent)
                        }
                    }
                ).Create();

                serviceCollection
                    .AddSingleton<IAmAMessageSchedulerFactory>(new InMemoryMessageSchedulerFactory())
                    .AddBrighter()
                    .UseExternalBus((configure) =>
                    {
                        configure.ProducerRegistry = producerRegistry;
                    })
                    .AutoFromAssemblies(typeof(GreetingEvent).Assembly);

                var serviceProvider = serviceCollection.BuildServiceProvider();

                var commandProcessor = serviceProvider.GetRequiredService<IAmACommandProcessor>();

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

                    commandProcessor.SchedulerPost(TimeSpan.FromSeconds(10), new GreetingEvent($"Ian says: Hi {name}"));
                }

                commandProcessor.Post(new FarewellEvent("Ian says: See you later!"));
            }
        }
    }
}
