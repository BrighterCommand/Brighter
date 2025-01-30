using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway.Proactor;

public class RmqBrokerNotPreCreatedTestsAsync : IDisposable
{
    private Message _message;
    private RmqMessageProducer _messageProducer;

    public RmqBrokerNotPreCreatedTestsAsync()
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
    public async Task When_posting_a_message_but_no_broker_created()
    {
        bool exceptionHappened = false;
        try
        {
            await _messageProducer.SendAsync(_message);
        }
        catch (ChannelFailureException)
        {
            exceptionHappened = true;
        }
            
        exceptionHappened.Should().BeTrue();
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
