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

#nullable enable

using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSharedConnectionCleanupFailureTests
{
    [Theory]
    [InlineData(false, false, "WaitForConfirms")]
    [InlineData(false, true, "WaitForConfirms")]
    [InlineData(true, false, "BasicCancel")]
    public async Task When_gateway_cleanup_throws_should_release_its_connection(
        bool disposeConsumer, bool disposeAsync, string failingOperation)
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
        var pool = new RmqMessageGatewayConnectionPool(connection.Name!, connection.Heartbeat, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        string? failure = failingOperation;
        using var producer = new CleanupFailureRmqProducer(connection, () => failure);
        using var consumer = new CleanupFailureRmqConsumer(connection, queueName, routingKey, () => failure);
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("before cleanup failure"));
        consumer.Purge();
        producer.Send(message);
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.Equal(message.Id, received.Id);
        consumer.Acknowledge(received);
        var sharedConnection = pool.GetConnection(factory);
        Assert.NotNull(sharedConnection);

        try
        {
            // Act
            var error = await Record.ExceptionAsync(async () =>
            {
                if (disposeConsumer)
                {
                    consumer.Dispose();
                }
                else if (disposeAsync) await producer.DisposeAsync();
                else producer.Dispose();
            });

            // Assert
            Assert.IsType<InvalidOperationException>(error);
            Assert.Equal("Injected channel cleanup failure.", error.Message);
            Assert.True(sharedConnection.IsOpen);
            failure = null;
            if (disposeConsumer) producer.Dispose();
            else consumer.Dispose();
            Assert.False(sharedConnection.IsOpen);
        }
        finally
        {
            failure = null;
            pool.RemoveConnection(factory);
        }
    }
}
