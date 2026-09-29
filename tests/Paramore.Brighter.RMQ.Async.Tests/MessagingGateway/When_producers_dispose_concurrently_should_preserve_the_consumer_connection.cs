#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqConcurrentGatewayDisposalTests
{
    [Fact]
    public async Task When_producers_dispose_concurrently_should_preserve_the_consumer_connection()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@127.0.0.1:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        using var consumer = new RmqMessageConsumer(connection, new ChannelName(Guid.NewGuid().ToString()),
            routingKey, isDurable: true);
        await consumer.PurgeAsync();
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        var factory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var sharedConnection = await pool.GetConnectionAsync(factory);
        var messages = Enumerable.Range(0, 8)
            .Select(i => new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
                new MessageBody($"message {i}")))
            .ToArray();

        // Act
        await Task.WhenAll(messages.Select(message => Task.Run(async () =>
        {
            await using var producer = new RmqMessageProducer(connection);
            await producer.SendAsync(message);
        }))).WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(sharedConnection.IsOpen);
        var receivedIds = new HashSet<Id>();
        for (var i = 0; i < messages.Length; i++)
        {
            var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains(received.Id, messages.Select(sent => sent.Id));
            Assert.True(receivedIds.Add(received.Id));
            await consumer.AcknowledgeAsync(received);
        }

        consumer.Dispose();
        Assert.False(sharedConnection.IsOpen);
    }
}
