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
using Paramore.Brighter.MessagingGateway.Postgres;
using Serilog;
using Serilog.Extensions.Logging;

namespace GreetingsSender;

public static class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());

        var connection = new PostgresMessagingGatewayConnection(new RelationalDatabaseConfiguration("Host=localhost;Username=postgres;Password=password;Database=brightertests;"));

        var producerRegistry = new PostgresProducerRegistryFactory(
            connection, 
            [
                new PostgresPublication
                {
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("greeting.event"),
                    RequestType = typeof(GreetingEvent)
                },
                new PostgresPublication
                {
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("farewell.event"),
                    RequestType = typeof(FarewellEvent)
                }
            ]).Create();
            
        serviceCollection
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
        commandProcessor.Post(new FarewellEvent("Ian says: See you later!"));
    }
}
