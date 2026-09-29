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
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqUnusedReplacementConnectionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_disposing_the_only_gateway_after_reset_should_close_the_unused_replacement(
        bool disposeConsumer, bool disposeAsync)
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
        using var producer = new RmqMessageProducer(connection, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        if (disposeConsumer) await consumer.PurgeAsync();
        else await producer.SendAsync(new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND), new MessageBody("before reset")));
        var original = await pool.GetConnectionAsync(factory);
        await pool.ResetConnectionAsync(factory);
        var replacement = await pool.GetConnectionAsync(factory);
        Assert.NotNull(original);
        Assert.NotNull(replacement);
        Assert.NotSame(original, replacement);
        Assert.False(original.IsOpen);
        Assert.True(replacement.IsOpen);

        try
        {
            // Act
            if (disposeConsumer)
            {
                if (disposeAsync) await consumer.DisposeAsync();
                else consumer.Dispose();
            }
            else if (disposeAsync) await producer.DisposeAsync();
            else producer.Dispose();

            // Assert
            Assert.False(replacement.IsOpen);
        }
        finally
        {
            await pool.RemoveConnectionAsync(factory);
        }
    }
}
