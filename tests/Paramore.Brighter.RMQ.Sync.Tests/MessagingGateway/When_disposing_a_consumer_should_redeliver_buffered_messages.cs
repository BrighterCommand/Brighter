using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Property("Category", "RMQ")]
public class BufferedConsumerDisposalTests
{
    [Test]
    public async Task When_disposing_a_consumer_should_redeliver_buffered_messages()
    {
        //Arrange
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var queue = new ChannelName(topic.Value);
        var configuration = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        using var producer = new RmqMessageProducer(configuration);
        using var consumer = new RmqMessageConsumer(configuration, queue, topic, false, batchSize: 5);
        var factory = new ConnectionFactory { Uri = configuration.AmpqUri.Uri };
        using var administrationConnection = factory.CreateConnection();
        using var administration = administrationConnection.CreateModel();
        try
        {
            consumer.Receive(TimeSpan.FromMilliseconds(50));
            var sent = Enumerable.Range(0, 5).Select(_ => new Message(
                new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT), new MessageBody("buffered"))).ToArray();
            foreach (var message in sent) producer.Send(message);
            var deadline = Stopwatch.StartNew();
            while (administration.QueueDeclarePassive(queue.Value).MessageCount != 0 && deadline.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(10);
            await Assert.That(administration.QueueDeclarePassive(queue.Value).MessageCount).IsEqualTo(0u);
            var shared = new RmqMessageGatewayConnectionPool(configuration.Name, configuration.Heartbeat).GetConnection(factory);

            //Act
            consumer.Dispose();

            //Assert
            await Assert.That(shared!.IsOpen).IsTrue();
            using var replacement = new RmqMessageConsumer(configuration, queue, topic, false, batchSize: 5);
            var received = replacement.Receive(TimeSpan.FromSeconds(5));
            await Assert.That(received.Select(message => message.Id)).IsEquivalentTo(sent.Select(message => message.Id));
            foreach (var message in received) replacement.Acknowledge(message);
        }
        finally
        {
            administration.QueueDelete(queue.Value, false, false);
        }
    }
}
