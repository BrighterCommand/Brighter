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
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using RabbitMQ.Client;


//private static ActivitySource source = new ActivitySource("GreetingsSender", "1.0.0");


HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRabbitMQ("messaging");

RmqMessagingGatewayConnection rmqConnection = new()
{
    AmpqUri = new AmqpUriSpecification(builder.Services.BuildServiceProvider().GetService<IConnectionFactory>().Uri),
    Exchange = new Exchange("paramore.brighter.exchange")
};

IAmAProducerRegistry producerRegistry = new RmqProducerRegistryFactory(
    rmqConnection,
    new RmqPublication[]
    {
        new()
        {
            MaxOutStandingMessages = 5,
            MaxOutStandingCheckIntervalMilliSeconds = 500,
            WaitForConfirmsTimeOutInMilliseconds = 1000,
            MakeChannels = OnMissingChannel.Create,
            Topic = new RoutingKey("greeting.event")
        },
        new()
        {
            MaxOutStandingMessages = 5,
            MaxOutStandingCheckIntervalMilliSeconds = 500,
            WaitForConfirmsTimeOutInMilliseconds = 1000,
            MakeChannels = OnMissingChannel.Create,
            Topic = new RoutingKey("farewell.event")
        }
    }).Create();

builder.Services.AddBrighter()
    .UseExternalBus(configure =>
    {
        configure.ProducerRegistry = producerRegistry;
    })
    .AutoFromAssemblies();

IHost host = builder.Build();

IAmACommandProcessor commandProcessor = host.Services.GetService<IAmACommandProcessor>();

Console.ReadKey();
// using (var activity = source.StartActivity("Post"))
//{
commandProcessor.Post(new GreetingEvent("Ian says: Hi there!"));
//}

//using (var activity = source.StartActivity("Post"))
//{
commandProcessor.Post(new FarewellEvent("Ian says: See you later!"));
//}

host.Run();
