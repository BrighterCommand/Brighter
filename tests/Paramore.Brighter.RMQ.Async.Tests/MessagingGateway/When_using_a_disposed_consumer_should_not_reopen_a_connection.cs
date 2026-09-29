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
public class RmqDisposedConsumerTests
{
    [Test]
    [Arguments("nack", false, false)]
    [Arguments("nack", false, true)]
    [Arguments("nack", true, false)]
    [Arguments("nack", true, true)]
    [Arguments("purge", false, false)]
    [Arguments("purge", false, true)]
    [Arguments("purge", true, false)]
    [Arguments("purge", true, true)]
    [Arguments("receive", false, false)]
    [Arguments("receive", false, true)]
    [Arguments("receive", true, false)]
    [Arguments("receive", true, true)]
    [Arguments("requeue", false, false)]
    [Arguments("requeue", false, true)]
    [Arguments("requeue", true, false)]
    [Arguments("requeue", true, true)]
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
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true);
        if (connectBeforeDisposing) await consumer.PurgeAsync();
        if (useAsync) await consumer.DisposeAsync();
        else consumer.Dispose();
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after disposal"));

        try
        {
            // Act
            var error = await TestExceptionRecorder.CaptureAsync(async () =>
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
            await Assert.That(error).IsTypeOf<ObjectDisposedException>();
        }
        finally
        {
            await pool.RemoveConnectionAsync(factory);
        }
    }
}
