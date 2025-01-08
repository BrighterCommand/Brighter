using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway;

public class RmqAssumeExistingInfrastructureTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly Message _message;
        
    public RmqAssumeExistingInfrastructureTestsAsync() 
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
    public async Task When_infrastructure_exists_can_assume_producer()
    {
        var exceptionThrown = false;
        try
        {
            //As we validate and don't create, this would throw due to lack of infrastructure if not already created
            await _messageProducer.SendAsync(_message);
            await _messageConsumer.ReceiveAsync(new TimeSpan(10000));
        }
        catch (ChannelFailureException)
        {
            exceptionThrown = true;
        }

        exceptionThrown.Should().BeFalse();
    }

    public void Dispose()
    { 
        ((IAmAMessageProducerSync)_messageProducer).Dispose(); 
        ((IAmAMessageConsumerSync)_messageConsumer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
        await  _messageConsumer.DisposeAsync(); 
    }
}
