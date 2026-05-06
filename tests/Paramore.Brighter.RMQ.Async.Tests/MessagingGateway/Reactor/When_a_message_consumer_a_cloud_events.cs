using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Category("RMQ")]
public class RMQBufferedConsumerCloudEventsTests : IDisposable
{
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly IAmAMessageConsumerSync _messageConsumer;
    private readonly ChannelName _channelName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private const int BatchSize = 3;

    public RMQBufferedConsumerCloudEventsTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        _messageConsumer = new RmqMessageConsumer(connection: rmqConnection, queueName: _channelName,
            routingKey: _routingKey, isDurable: false, highAvailability: false, batchSize: BatchSize);

        //create the queue, so that we can receive messages posted to it
        new QueueFactory(rmqConnection, _channelName, new RoutingKeys(_routingKey)).CreateAsync().GetAwaiter()
            .GetResult();
    }

    [Test]
    public async Task When_uses_cloud_events()
    {
        //Post one more than batch size messages
        var messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND)
            {
                Type = new CloudEventsType($"Type{Guid.NewGuid():N}"),
                Subject = $"Subject{Guid.NewGuid():N}",
                Source = new Uri($"/component/{Guid.NewGuid()}", UriKind.RelativeOrAbsolute),
                DataSchema = new Uri("https://example.com/storage/tenant/container", UriKind.RelativeOrAbsolute)
            }, new MessageBody("test content One"));
        _messageProducer.Send(messageOne);

        //let them arrive
        Task.Delay(5000);

        //Now retrieve messages from the consumer
        var messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000));

        //We should only have three messages
        await Assert.That(messages).HasSingleItem();

        await Assert.That(messages[0].Header.MessageId).IsEqualTo(messageOne.Header.MessageId);
        await Assert.That(messages[0].Header.Subject).IsEqualTo(messageOne.Header.Subject);
        await Assert.That(messages[0].Header.Type).IsEqualTo(messageOne.Header.Type);
        await Assert.That(messages[0].Header.Source).IsEqualTo(messageOne.Header.Source);
        await Assert.That(messages[0].Header.DataSchema).IsEqualTo(messageOne.Header.DataSchema);
    }

    public void Dispose()
    {
        _messageConsumer.Purge();
        _messageConsumer.Dispose();
        _messageProducer.Dispose();
    }
}

