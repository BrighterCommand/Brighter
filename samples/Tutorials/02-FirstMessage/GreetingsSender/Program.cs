#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Greetings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.exchange")
};

// A publication tells Brighter where a request type goes: which routing key, on which broker.
var producerRegistry = new RmqProducerRegistryFactory(
    rmqConnection,
    [
        new RmqPublication<GreetingEvent>
        {
            Topic = new RoutingKey("greeting.event"),
            MakeChannels = OnMissingChannel.Create
        }
    ]).Create();

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddBrighter()
    .AddProducers(configure => configure.ProducerRegistry = producerRegistry)
    .AutoFromAssemblies();

// The host is built but never run: Post is synchronous, so all we need from it is the
// container. Rung 3 does run the host, because it hosts the Outbox Sweeper.
using (var host = builder.Build())
{
    var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

    commandProcessor.Post(new GreetingEvent("Hello from the sender"));
}
// Publisher confirms are asynchronous: Post returns once the message is on its way, and the
// broker's acknowledgement arrives later. Disposing the host waits for it, which is why the
// success message belongs after this brace and not before it.

Console.WriteLine("Published greeting.event");
