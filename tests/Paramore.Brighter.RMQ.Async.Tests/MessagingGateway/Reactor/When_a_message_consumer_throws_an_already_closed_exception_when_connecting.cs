using System;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageConsumerConnectionClosedTests : IDisposable
{
    private readonly IAmAMessageProducerSync _sender;
    private readonly IAmAMessageConsumerSync _receiver;
    private readonly IAmAMessageConsumerSync _badReceiver;
    private readonly Message _sentMessage;
    private Exception _firstException;

    public RmqMessageConsumerConnectionClosedTests()
    {
        var messageHeader = new MessageHeader(Guid.NewGuid().ToString(),  
            new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND);

        messageHeader.UpdateHandledCount();
        _sentMessage = new Message(messageHeader, new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _sender = new RmqMessageProducer(rmqConnection);
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _receiver = new RmqMessageConsumer(rmqConnection, queueName, _sentMessage.Header.Topic, false, false);
        _badReceiver = new AlreadyClosedRmqMessageConsumer(rmqConnection, queueName, _sentMessage.Header.Topic, false, 1, false);

    }

    [Fact]
    public void When_a_message_consumer_throws_an_already_closed_exception_when_connecting()
    {
        _sender.Send(_sentMessage);
            
        bool exceptionHappened = false;
        try
        {
            _badReceiver.Receive(TimeSpan.FromMilliseconds(2000));
        }
        catch (ChannelFailureException cfe)
        {
            exceptionHappened = true;
            Assert.True((cfe.InnerException) is AlreadyClosedException);
        }
           
        Assert.True(exceptionHappened);
    }

    public void Dispose()
    {
        _sender.Dispose();
        _receiver.Dispose();
    }
}
