#region Licence
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
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

namespace GreetingsReceiverConsole;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var host = new HostBuilder()
            .ConfigureServices((_, services) =>

            {
                var subscriptions = new Subscription[]
                {
                    new PostgresSubscription<GreetingEvent>(
                        new SubscriptionName("paramore.example.greeting"),
                        new ChannelName("greeting.event"),
                        new RoutingKey("greeting.event"),
                        timeOut: TimeSpan.FromMilliseconds(2000),
                        messagePumpType: MessagePumpType.Reactor,
                        makeChannels: OnMissingChannel.Create),   //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
                    new PostgresSubscription<FarewellEvent>(
                        new SubscriptionName("paramore.example.farewell"), //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
                        new ChannelName("farewell.event"),
                        new RoutingKey("farewell.event"),
                        timeOut: TimeSpan.FromMilliseconds(200),
                        messagePumpType: MessagePumpType.Reactor,
                        makeChannels: OnMissingChannel.Create)
                };

                var connection = new PostgresMessagingGatewayConnection(new RelationalDatabaseConfiguration("Host=localhost;Username=postgres;Password=password;Database=brightertests;"));

                services.AddServiceActivator(options =>
                    {
                        options.Subscriptions = subscriptions;
                        options.DefaultChannelFactory = new PostgresChannelFactory(connection);
                    })
                    .AutoFromAssemblies();

                    
                services.AddHostedService<ServiceActivatorHostedService>();
            })
            .UseConsoleLifetime()
            .UseSerilog()
            .Build();

        await host.RunAsync();
    }
}
