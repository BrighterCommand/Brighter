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
using Greetings.Ports.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Redis;
using Serilog;

namespace GreetingsSender
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            
            try
            {
                Log.Information("Starting host");
                var host = CreateHostBuilder(args)
                    .UseSerilog()
                    .UseConsoleLifetime()
                    .Build();
                
                
                DoWork(host);

                host.WaitForShutdown();
                
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
 
        }

        private static void DoWork(IHost host)
        {
            var commandProcessor = host.Services.GetService<IAmACommandProcessor>();

            commandProcessor?.Post(new GreetingEvent("Ian"));
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            new HostBuilder()
                .ConfigureServices((context, collection) =>
                {
                    var redisConnection = new RedisMessagingGatewayConfiguration
                    {
                        RedisConnectionString = "localhost:6379?connectTimeout=1&sendTimeout=1000&",
                        MaxPoolSize = 10,
                        MessageTimeToLive = TimeSpan.FromMinutes(10)
                    };

                    var producerRegistry = new RedisProducerRegistryFactory(
                        redisConnection,
                        [
                            new()
                            {
                                Topic = new RoutingKey("greeting.event"),
                                RequestType = typeof(GreetingEvent)
                            }
                        ]
                    ).Create();
                    
                    collection.AddBrighter()
                        // InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
                        // Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
                        .UseScheduler(new InMemorySchedulerFactory())
                        .AddProducers((configure) =>
                        {
                            configure.ProducerRegistry = producerRegistry;
                        })
                        .AutoFromAssemblies();
                });
    }
}
