using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway.Proactor;

public class RmqValidateExistingInfrastructureTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly Message _message;
        
    public RmqValidateExistingInfrastructureTestsAsync() 
    {
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND), 
            new MessageBody("test content")
        );

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection, new RmqPublication{MakeChannels = OnMissingChannel.Validate});
        _messageConsumer = new RmqMessageConsumer(
            connection: rmqConnection, 
            queueName: queueName, 
            routingKey: routingKey, 
            isDurable: false, 
            highAvailability: false, 
            makeChannels: OnMissingChannel.Validate);

        //This creates the infrastructure we want
        new QueueFactory(rmqConnection, queueName, new RoutingKeys(routingKey))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }
        
    [Fact]
    public async Task When_infrastructure_exists_can_validate_producer()
    {
        var exceptionThrown = false;
        try
        {
            //As we validate and don't create, this would throw due to lack of infrastructure if not already created
            await _messageProducer.SendAsync(_message);
            await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
        }
        catch (ChannelFailureException cfe)
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
        await _messageConsumer.DisposeAsync();
    }
}
