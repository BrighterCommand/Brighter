using System;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageProducerSendPersistentMessageTests : IDisposable
{
    private IAmAMessageProducerSync _messageProducer;
    private IAmAMessageConsumerSync _messageConsumer;
    private Message _message;

    public RmqMessageProducerSendPersistentMessageTests()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), 
                MessageType.MT_COMMAND),
            new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
            PersistMessages = true
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName, _message.Header.Topic, false);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys( _message.Header.Topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public void When_posting_a_message_to_persist_via_the_messaging_gateway()
    {
        // arrange
        _messageProducer.Send(_message);

        // act
        var result = _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000)).First();

        // assert
        Assert.Equal(true, result.Persist);
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
