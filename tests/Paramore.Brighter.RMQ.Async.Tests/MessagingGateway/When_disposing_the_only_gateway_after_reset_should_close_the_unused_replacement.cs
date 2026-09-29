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

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Category("RMQ")]
[NotInParallel]
public class RmqUnusedReplacementConnectionTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        using var producer = new RmqMessageProducer(connection);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true);
        if (disposeConsumer) await consumer.PurgeAsync();
        else await producer.SendAsync(new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND), new MessageBody("before reset")));
        var original = await pool.GetConnectionAsync(factory);
        await pool.ResetConnectionAsync(factory);
        var replacement = await pool.GetConnectionAsync(factory);
        await Assert.That(original).IsNotNull();
        await Assert.That(replacement).IsNotNull();
        await Assert.That(replacement).IsNotSameReferenceAs(original);
        await Assert.That(original.IsOpen).IsFalse();
        await Assert.That(replacement.IsOpen).IsTrue();

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
            await Assert.That(replacement.IsOpen).IsFalse();
        }
        finally
        {
            await pool.RemoveConnectionAsync(factory);
        }
    }
}
