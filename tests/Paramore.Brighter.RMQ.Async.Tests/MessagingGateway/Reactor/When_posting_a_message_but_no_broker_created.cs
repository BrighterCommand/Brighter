using System;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Collection("RMQ")]
public class RmqBrokerNotPreCreatedTests : IDisposable
{
    private Message _message;
    private RmqMessageProducer _messageProducer;

    public RmqBrokerNotPreCreatedTests()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), 
                MessageType.MT_COMMAND), 
            new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange(Guid.NewGuid().ToString())
        };

        _messageProducer = new RmqMessageProducer(rmqConnection, new RmqPublication{MakeChannels = OnMissingChannel.Validate});

    }
        
    [Fact]
    public void When_posting_a_message_but_no_broker_created()
    {
        Assert.Throws<ChannelFailureException>(() => _messageProducer.Send(_message));
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
