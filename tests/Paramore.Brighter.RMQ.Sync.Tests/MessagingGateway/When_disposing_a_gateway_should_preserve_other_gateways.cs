using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Property("Category", "RMQ")]
public class SharedConnectionLifetimeTests
{
    [Test]
    public async Task When_disposing_a_gateway_should_preserve_other_gateways()
    {
        //Arrange
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        using var consumer = new RmqMessageConsumer(connection, new ChannelName(topic.Value), topic, isDurable: false);
        using var producer = new RmqMessageProducer(connection);
        var message = new Message(new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT), new MessageBody("payload"));
        consumer.Receive(TimeSpan.FromMilliseconds(50));
        producer.Send(message);
        var received = consumer.Receive(TimeSpan.FromSeconds(5)).Single();
        await Assert.That(received.Id).IsEqualTo(message.Id);

        var factory = new RabbitMQ.Client.ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        var sharedConnection = pool.GetConnection(factory);

        //Act
        producer.Dispose();
        producer.Dispose();

        //Assert
        await Assert.That(sharedConnection!.IsOpen).IsTrue();
        consumer.Acknowledge(received);
    }
}
