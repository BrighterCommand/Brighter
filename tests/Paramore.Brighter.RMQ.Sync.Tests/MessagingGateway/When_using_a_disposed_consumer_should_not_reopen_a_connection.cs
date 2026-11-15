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
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Category("RMQ")]
[NotInParallel]
public class RmqDisposedConsumerTests
{
    [Test]
    [Arguments("nack", false)]
    [Arguments("nack", true)]
    [Arguments("purge", false)]
    [Arguments("purge", true)]
    [Arguments("receive", false)]
    [Arguments("receive", true)]
    [Arguments("requeue", false)]
    [Arguments("requeue", true)]
    public async Task When_using_a_disposed_consumer_should_not_reopen_a_connection(
        string operation, bool connectBeforeDisposing)
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
        var pool = new RmqMessageGatewayConnectionPool(connection.Name!, connection.Heartbeat);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true);
        if (connectBeforeDisposing) consumer.Purge();
        consumer.Dispose();
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after disposal"));

        try
        {
            // Act
            var error = TestExceptionRecorder.Capture(() =>
            {
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
            pool.RemoveConnection(factory);
        }
    }
}
