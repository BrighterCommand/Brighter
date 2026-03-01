using System;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Collection("RMQ")]
public class RmqAssumeExistingInfrastructureTests : IDisposable
{
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly IAmAMessageConsumerSync _messageConsumer;
    private readonly Message _message;
        
    public RmqAssumeExistingInfrastructureTests() 
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

        _messageProducer = new RmqMessageProducer(rmqConnection, new RmqPublication{MakeChannels = OnMissingChannel.Assume});
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _messageConsumer = new RmqMessageConsumer(
            connection:rmqConnection, 
            queueName: queueName, 
            routingKey:_message.Header.Topic, 
            isDurable: false, 
            highAvailability:false, 
            makeChannels: OnMissingChannel.Assume);

        //This creates the infrastructure we want
        new QueueFactory(rmqConnection, queueName, new RoutingKeys( _message.Header.Topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult() ;
    }
        
    [Fact]
    public void When_infrastructure_exists_can_assume_producer()
    {
        var exceptionThrown = false;
        try
        {
            //As we validate and don't create, this would throw due to lack of infrastructure if not already created
            _messageProducer.Send(_message);
            _messageConsumer.Receive(new TimeSpan(10000));
        }
        catch (ChannelFailureException)
        {
            exceptionThrown = true;
        }

        Assert.False(exceptionThrown);
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
        _messageConsumer.Dispose();
    } 
}
