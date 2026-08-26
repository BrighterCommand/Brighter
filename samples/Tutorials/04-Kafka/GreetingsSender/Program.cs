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
using Paramore.Brighter.MessagingGateway.Kafka;

// Kafka is reached through a bootstrap server list rather than a single connection string.
// The client asks any broker in the list for the cluster's real topology, so one entry is
// enough to learn on; a production cluster names several so that startup survives one being
// down.
var kafkaConfiguration = new KafkaMessagingGatewayConfiguration
{
    Name = "greetings.sender",
    BootStrapServers = ["localhost:9092"]
};

// A publication tells Brighter where a request type goes: which topic, on which broker.
// NumPartitions is the delta from rung 2. A RabbitMQ queue is one ordered stream; a Kafka
// topic is NumPartitions of them, and that is what gives a consumer group something to
// share out. MakeChannels.Create means the sender creates the topic if it is missing, with
// this many partitions.
//
// Naming GreetingEvent here is also what loads the Greetings assembly, which matters below:
// AutoFromAssemblies scans the assemblies loaded so far, so anything it must find has to have
// been touched before the call. Reordering this below the registration is a silent no-op.
var producerRegistry = new KafkaProducerRegistryFactory(
    kafkaConfiguration,
    [
        new KafkaPublication<GreetingEvent>
        {
            Topic = new RoutingKey("greeting.event"),
            NumPartitions = 3,
            MakeChannels = OnMissingChannel.Create
        }
    ]).Create();

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddBrighter()
    .AddProducers(configure => configure.ProducerRegistry = producerRegistry)
    .AutoFromAssemblies();

// Three recipients, three greetings each, sent round-robin so that the three streams are
// interleaved on the wire.
//
// These three names are not arbitrary. Kafka picks the partition by hashing the key —
// crc32(key) % 3 here — so you do not get to choose it, and with three keys over three
// partitions there is only a 2-in-9 chance that they land one apiece. The first three names
// this sample tried, "alice", "bob" and "carol", all hashed to partition 2, which a single
// consumer hides completely: it drains all three partitions and everything still arrives in
// order. These three were checked against the broker.
string[] recipients = ["alice", "grace", "mia"];

// The host is built but never run: Post is synchronous, so all we need from it is the
// container. Rung 3 does run the host, because it hosts the Outbox Sweeper.
using (var host = builder.Build())
{
    var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

    for (var i = 1; i <= 3; i++)
    {
        foreach (var recipient in recipients)
        {
            // The partition key is how you choose a partition, and the partition is the only
            // thing Kafka orders. Keying on the recipient sends every one of alice's greetings
            // to the same partition, so they arrive in the order they were sent — while grace's
            // and mia's are free to be handled on other partitions.
            //
            // No message mapper is needed for this. JsonMessageMapper<T> is Brighter's
            // registered default for both the sync and the async path, and it already reads
            // the key out of the request context onto the message header.
            var context = new RequestContext();
            context.Bag[RequestContextBagNames.PartitionKey] = recipient;

            commandProcessor.Post(new GreetingEvent($"Hello {recipient} #{i}"), context);
        }
    }
}
// Delivery to Kafka is asynchronous: Post hands the message to the producer's send queue and
// returns, and the broker's acknowledgement arrives on a delivery report later. Disposing the
// host flushes that queue and waits for the reports, which is why this line sits after the
// brace rather than inside the loop.
Console.WriteLine($"Published {recipients.Length * 3} greetings to greeting.event");
