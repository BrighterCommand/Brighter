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
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var kafkaConfiguration = new KafkaMessagingGatewayConfiguration
{
    Name = "greetings.receiver",
    BootStrapServers = ["localhost:9092"]
};

// groupId is the delta from rung 2, and it is what makes a second copy of this process
// interesting rather than redundant. Every instance sharing a group id is one member of one
// consumer group, and Kafka gives each member a disjoint set of partitions: run one instance
// and it holds all three, start a second and Kafka rebalances the group so they hold roughly
// half each. Two instances with *different* group ids would each get all three and both would
// see every greeting.
//
// messagePumpType is the default and is written out because it is the point of this rung.
// A Reactor pump is a single thread per performer, and noOfPerformers defaults to 1 — so one
// instance is one member with one thread, draining its partitions in turn. That single thread
// is why per-key ordering holds; it is not that Brighter runs a pump per partition.
//
// numOfPartitions matches the publication so that whichever process reaches an empty broker
// first creates the same three-partition topic.
var subscriptions = new Subscription[]
{
    new KafkaSubscription<GreetingEvent>(
        new SubscriptionName("greeting.subscription"),
        new ChannelName("greeting.event"),
        new RoutingKey("greeting.event"),
        groupId: "greeting.readers",
        numOfPartitions: 3,
        messagePumpType: MessagePumpType.Reactor,
        makeChannels: OnMissingChannel.Create)
};

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddConsumers(options =>
    {
        options.Subscriptions = subscriptions;
        options.DefaultChannelFactory = new ChannelFactory(new KafkaMessageConsumerFactory(kafkaConfiguration));
    })
    .AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var host = builder.Build();

await host.RunAsync();
