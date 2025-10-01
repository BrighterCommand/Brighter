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
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

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
            new SqsSubscription<GreetingEvent>(
                subscriptionName: new SubscriptionName("paramore.example.greeting"),
                channelName: new ChannelName(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                routingKey: new RoutingKey(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                channelType: ChannelType.PubSub,
                bufferSize: 10,
                timeOut: TimeSpan.FromMilliseconds(20),
                messagePumpType: MessagePumpType.Reactor,
                queueAttributes: new SqsAttributes(lockTimeout: TimeSpan.FromSeconds(30)))
        };

        //create the gateway
        var serviceURL = "http://localhost:4566/"; 
        var region = RegionEndpoint.USEast1;
        var awsConnection = new AWSMessagingGatewayConnection(new BasicAWSCredentials("test", "test"), region,
            cfg => { cfg.ServiceURL = serviceURL; });

        services.AddConsumers(options =>
            {
                options.Subscriptions = subscriptions;
                options.DefaultChannelFactory = new ChannelFactory(awsConnection);
            })
            .AutoFromAssemblies();

        services.AddHostedService<ServiceActivatorHostedService>();
    })
    .UseConsoleLifetime()
    .UseSerilog()
    .Build();

Console.CancelKeyPress += (_, _) => host.StopAsync().Wait();

await host.RunAsync();
