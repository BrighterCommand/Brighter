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
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSharedConnectionResetTests
{
    [Fact]
    public void When_disposing_a_gateway_after_reset_should_preserve_the_replacement_connection()
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
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        using var oldProducer = new RmqMessageProducer(connection);
        using var producer = new RmqMessageProducer(connection);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true);
        consumer.Purge();
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("before reset"));
        oldProducer.Send(message);
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.Equal(message.Id, received.Id);
        consumer.Acknowledge(received);
        var originalConnection = pool.GetConnection(factory);
        pool.ResetConnection(factory);
        Assert.False(originalConnection.IsOpen);
        consumer.Purge();
        producer.Send(message);
        received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.Equal(message.Id, received.Id);
        consumer.Acknowledge(received);
        var replacementConnection = pool.GetConnection(factory);
        Assert.NotSame(originalConnection, replacementConnection);

        // Act
        oldProducer.Dispose();

        // Assert
        Assert.True(replacementConnection.IsOpen);
        var afterReset = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after reset"));
        producer.Send(afterReset);
        received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.Equal(afterReset.Id, received.Id);
        consumer.Acknowledge(received);
        consumer.Dispose();
        Assert.True(replacementConnection.IsOpen);
        producer.Dispose();
        Assert.False(replacementConnection.IsOpen);
    }
}
