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

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Category("RMQ")]
[NotInParallel]
public class RmqSharedConnectionDisposalTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
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
        using var producer = new RmqMessageProducer(connection);
        using var consumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true);
        using var siblingProducer = new RmqMessageProducer(connection);
        using var siblingConsumer = new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true);
        consumer.Purge();

        var warmup = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("before disposal"));
        producer.Send(warmup);
        var received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.Id).IsEqualTo(warmup.Id);
        consumer.Acknowledge(received);

        if (connectBeforeDisposing)
        {
            if (disposeConsumer)
                siblingConsumer.Purge();
            else
            {
                siblingProducer.Send(warmup);
                received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
                await Assert.That(received.Id).IsEqualTo(warmup.Id);
                consumer.Acknowledge(received);
            }
        }

        var factory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        var sharedConnection = pool.GetConnection(factory);
        await Assert.That(sharedConnection).IsNotNull();
        await Assert.That(sharedConnection.IsOpen).IsTrue();

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
        await Assert.That(sharedConnection.IsOpen).IsTrue();
        var message = new Message(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("after disposal"));
        producer.Send(message);
        received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.Id).IsEqualTo(message.Id);
        await Assert.That(received.Body.Value).IsEqualTo(message.Body.Value);
        consumer.Acknowledge(received);

        consumer.Dispose();
        await Assert.That(sharedConnection.IsOpen).IsTrue();
        producer.Dispose();
        await Assert.That(sharedConnection.IsOpen).IsFalse();
    }
}
