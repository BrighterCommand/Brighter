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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSharedConnectionResetTests
{
    [Fact]
    public async Task When_disposing_a_gateway_after_reset_should_preserve_the_replacement_connection()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@127.0.0.1:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var factory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var oldProducer = new RmqMessageProducer(connection, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var producer = new RmqMessageProducer(connection, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        await consumer.PurgeAsync();
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("before reset"));
        await oldProducer.SendAsync(message);
        var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(message.Id, received.Id);
        await consumer.AcknowledgeAsync(received);
        var originalConnection = await pool.GetConnectionAsync(factory);
        await pool.ResetConnectionAsync(factory);
        Assert.False(originalConnection.IsOpen);
        await consumer.PurgeAsync();
        await producer.SendAsync(message);
        received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(message.Id, received.Id);
        await consumer.AcknowledgeAsync(received);
        var replacementConnection = await pool.GetConnectionAsync(factory);
        Assert.NotSame(originalConnection, replacementConnection);

        // Act
        await oldProducer.DisposeAsync();

        // Assert
        Assert.True(replacementConnection.IsOpen);
        var afterReset = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after reset"));
        await producer.SendAsync(afterReset);
        received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(afterReset.Id, received.Id);
        await consumer.AcknowledgeAsync(received);
        consumer.Dispose();
        Assert.True(replacementConnection.IsOpen);
        producer.Dispose();
        Assert.False(replacementConnection.IsOpen);
    }
}
