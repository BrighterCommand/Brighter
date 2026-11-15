using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Property("Category", "RMQ")]
public class SharedConnectionLifetimeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_disposing_a_gateway_should_preserve_other_gateways(bool disposeAsync)
    {
        //Arrange
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        await using var consumer = new RmqMessageConsumer(connection, new ChannelName(topic.Value), topic, isDurable: false);
        await using var producer = new RmqMessageProducer(connection);
        var message = new Message(new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT), new MessageBody("payload"));
        await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(50));
        await producer.SendAsync(message);
        var received = (await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).Single();
        await Assert.That(received.Id).IsEqualTo(message.Id);

        var factory = new RabbitMQ.Client.ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        var sharedConnection = await pool.GetConnectionAsync(factory);

        //Act
        if (disposeAsync)
        {
            await producer.DisposeAsync();
            await producer.DisposeAsync();
        }
        else
        {
            producer.Dispose();
            producer.Dispose();
        }

        //Assert
        await Assert.That(sharedConnection!.IsOpen).IsTrue();
        await consumer.AcknowledgeAsync(received);
    }
}
