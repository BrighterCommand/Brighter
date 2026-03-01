using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageConsumerMultipleTopicTests : IDisposable
{        
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly IAmAMessageConsumerSync _messageConsumer;
    private readonly Message _messageTopic1, _messageTopic2;

    public RmqMessageConsumerMultipleTopicTests()
    {
        var routingKeyOne = new RoutingKey(Guid.NewGuid().ToString());
        var routingKeyTwo = new RoutingKey(Guid.NewGuid().ToString());
            
        _messageTopic1 = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKeyOne, MessageType.MT_COMMAND), 
            new MessageBody("test content for topic test 1"));
        _messageTopic2 = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKeyTwo, MessageType.MT_COMMAND), 
            new MessageBody("test content for topic test 2"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var topics = new RoutingKeys([
            routingKeyOne, 
            routingKeyTwo
        ]);
        var queueName = new ChannelName(Guid.NewGuid().ToString());

        _messageProducer = new RmqMessageProducer(rmqConnection);
        _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName , topics, false, false);

        new QueueFactory(rmqConnection, queueName, topics).Create(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public void When_reading_a_message_from_a_channel_with_multiple_topics()
    {
        _messageProducer.Send(_messageTopic1);
        _messageProducer.Send(_messageTopic2);

        //allow messages to propogate
        Task.Delay(3000);

        var topic1Result = _messageConsumer.Receive(TimeSpan.FromMilliseconds(10000)).First();
        _messageConsumer.Acknowledge(topic1Result);
        
        var topic2Result = _messageConsumer.Receive(TimeSpan.FromMilliseconds(10000)).First();
        _messageConsumer.Acknowledge(topic2Result);

        // should_received_a_message_from_test1_with_same_topic_and_body
        Assert.Equal(_messageTopic1.Header.Topic, topic1Result.Header.Topic);
        Assert.Equivalent(_messageTopic1.Body.Value, topic1Result.Body.Value);

        // should_received_a_message_from_test2_with_same_topic_and_body
        Assert.Equal(_messageTopic2.Header.Topic, topic2Result.Header.Topic);
        Assert.Equivalent(_messageTopic2.Body.Value, topic2Result.Body.Value);            
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
