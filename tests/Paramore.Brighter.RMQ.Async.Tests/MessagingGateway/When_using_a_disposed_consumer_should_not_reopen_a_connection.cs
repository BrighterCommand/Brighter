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
public class RmqDisposedConsumerTests
{
    [Theory]
    [InlineData("nack", false, false)]
    [InlineData("nack", false, true)]
    [InlineData("nack", true, false)]
    [InlineData("nack", true, true)]
    [InlineData("purge", false, false)]
    [InlineData("purge", false, true)]
    [InlineData("purge", true, false)]
    [InlineData("purge", true, true)]
    [InlineData("receive", false, false)]
    [InlineData("receive", false, true)]
    [InlineData("receive", true, false)]
    [InlineData("receive", true, true)]
    [InlineData("requeue", false, false)]
    [InlineData("requeue", false, true)]
    [InlineData("requeue", true, false)]
    [InlineData("requeue", true, true)]
    public async Task When_using_a_disposed_consumer_should_not_reopen_a_connection(
        string operation, bool connectBeforeDisposing, bool useAsync)
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
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        if (connectBeforeDisposing) await consumer.PurgeAsync();
        if (useAsync) await consumer.DisposeAsync();
        else consumer.Dispose();
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after disposal"));

        try
        {
            // Act
            var error = await Record.ExceptionAsync(async () =>
            {
                if (useAsync)
                {
                    switch (operation)
                    {
                        case "purge": await consumer.PurgeAsync(); break;
                        case "receive": await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10)); break;
                        case "requeue": await consumer.RequeueAsync(message); break;
                        case "nack": await consumer.NackAsync(message); break;
                    }
                    return;
                }
                switch (operation)
                {
                    case "purge": consumer.Purge(); break;
                    case "receive": consumer.Receive(TimeSpan.FromMilliseconds(10)); break;
                    case "requeue": consumer.Requeue(message); break;
                    case "nack": consumer.Nack(message); break;
                }
            });

            // Assert
            Assert.IsType<ObjectDisposedException>(error);
        }
        finally
        {
            await pool.RemoveConnectionAsync(factory);
        }
    }
}
