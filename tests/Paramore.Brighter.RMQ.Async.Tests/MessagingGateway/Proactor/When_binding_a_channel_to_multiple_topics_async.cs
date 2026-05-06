using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Category("RMQ")]
public class AsyncRmqMessageConsumerMultipleTopicTests : IAsyncDisposable
{        
    private IAmAMessageProducerAsync _messageProducer;
    private IAmAMessageConsumerAsync _messageConsumer;
    private Message _messageTopic1, _messageTopic2;
    private RmqMessagingGatewayConnection _rmqConnection;
    private RoutingKeys _topics;
    private ChannelName _queueName;

    public AsyncRmqMessageConsumerMultipleTopicTests()
    {
        var routingKeyOne = new RoutingKey(Guid.NewGuid().ToString());
        var routingKeyTwo = new RoutingKey(Guid.NewGuid().ToString());

        _messageTopic1 = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKeyOne, MessageType.MT_COMMAND),
            new MessageBody("test content for topic test 1"));
        _messageTopic2 = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKeyTwo, MessageType.MT_COMMAND),
            new MessageBody("test content for topic test 2"));

        _rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _topics = new RoutingKeys([
            routingKeyOne,
            routingKeyTwo
        ]);
        _queueName = new ChannelName(Guid.NewGuid().ToString());

        _messageProducer = new RmqMessageProducer(_rmqConnection);
        _messageConsumer = new RmqMessageConsumer(_rmqConnection, _queueName , _topics, false, false);
    }

    [Before(Test)]
    public async Task Setup()
    {
        await new QueueFactory(_rmqConnection, _queueName, _topics).CreateAsync();
    }

    [Test]
    public async Task When_reading_a_message_from_a_channel_with_multiple_topics()
    {
        await _messageProducer.SendAsync(_messageTopic1);
        await _messageProducer.SendAsync(_messageTopic2);

        //allow messages to propogate
        await Task.Delay(3000);

        var topic1Result = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First();
        await _messageConsumer.AcknowledgeAsync(topic1Result);
        var topic2Result = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First();
        await  _messageConsumer.AcknowledgeAsync(topic2Result);

        await Assert.That(topic1Result.Header.Topic).IsEqualTo(_messageTopic1.Header.Topic);
        await Assert.That(topic1Result.Body.Value).IsEquivalentTo(_messageTopic1.Body.Value);

        await Assert.That(topic2Result.Header.Topic).IsEqualTo(_messageTopic2.Header.Topic);
        await Assert.That(topic2Result.Body.Value).IsEquivalentTo(_messageTopic2.Body.Value);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        ((IAmAMessageProducerSync) _messageProducer).Dispose();
        ((IAmAMessageConsumerSync)_messageConsumer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
        await _messageConsumer.DisposeAsync();
    }
}
