using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway
{
    public class RmqValidateExistingInfrastructureTests : IDisposable
    {
        private readonly IAmAMessageProducerSync _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly Message _message;
        
        public RmqValidateExistingInfrastructureTests() 
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
            _messageConsumer = new RmqMessageConsumer(
                connection:rmqConnection, 
                queueName: new ChannelName(Guid.NewGuid().ToString()), 
                routingKey:_message.Header.Topic, 
                isDurable: false, 
                highAvailability:false, 
                makeChannels: OnMissingChannel.Validate);

            //This creates the infrastructure we want
            new QueueFactory(rmqConnection,new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys( _message.Header.Topic))
                .Create(TimeSpan.FromMilliseconds(3000));
        }
        
        [Fact]
        public void When_infrastructure_exists_can_validate_producer()
        {
            var exceptionThrown = false;
            try
            {
                //As we validate and don't create, this would throw due to lack of infrastructure if not already created
                _messageProducer.Send(_message);
                _messageConsumer.Receive(TimeSpan.FromMilliseconds(10000));
            }
            catch (ChannelFailureException)
            {
                exceptionThrown = true;
            }

            exceptionThrown.Should().BeFalse();
        }

        public void Dispose()
        {
            _messageProducer.Dispose();
            _messageConsumer.Dispose();
        }
    }
}
