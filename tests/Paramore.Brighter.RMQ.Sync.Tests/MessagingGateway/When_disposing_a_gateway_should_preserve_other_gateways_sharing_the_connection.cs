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
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSharedConnectionDisposalTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public async Task When_disposing_a_gateway_should_preserve_other_gateways_sharing_the_connection(
        bool disposeConsumer, bool connectBeforeDisposing, bool disposeAsync)
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@127.0.0.1:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());
        using var producer = new RmqMessageProducer(connection, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var siblingProducer = new RmqMessageProducer(connection, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var siblingConsumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        consumer.Purge();

        var warmup = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("before disposal"));
        producer.Send(warmup);
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.Equal(warmup.Id, received.Id);
        consumer.Acknowledge(received);

        if (connectBeforeDisposing)
        {
            if (disposeConsumer)
                siblingConsumer.Purge();
            else
            {
                siblingProducer.Send(warmup);
                received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
                Assert.Equal(warmup.Id, received.Id);
                consumer.Acknowledge(received);
            }
        }

        var factory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var sharedConnection = pool.GetConnection(factory);
        Assert.NotNull(sharedConnection);
        Assert.True(sharedConnection.IsOpen);

        // Act
        if (disposeConsumer)
            siblingConsumer.Dispose();
        else if (disposeAsync)
            await siblingProducer.DisposeAsync();
        else
            siblingProducer.Dispose();

        // Assert
        siblingConsumer.Dispose();
        siblingProducer.Dispose();
        Assert.True(sharedConnection.IsOpen);
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after disposal"));
        producer.Send(message);
        received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.Equal(message.Id, received.Id);
        Assert.Equal(message.Body.Value, received.Body.Value);
        consumer.Acknowledge(received);

        consumer.Dispose();
        Assert.True(sharedConnection.IsOpen);
        producer.Dispose();
        Assert.False(sharedConnection.IsOpen);
    }
}
