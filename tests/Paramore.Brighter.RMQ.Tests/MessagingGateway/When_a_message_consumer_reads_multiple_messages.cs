using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ._Tests.MessagingGateway
{
    [Collection("RMQ")]
    [Trait("Category", "RMQ")]
    public class RMQBufferedConsumerTests : IDisposable
    {
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private string _topic = Guid.NewGuid().ToString();
        private const int BatchSize = 3;

        public RMQBufferedConsumerTests()
        {
            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            _messageProducer = new RmqMessageProducer(rmqConnection);
            _messageConsumer = new RmqMessageConsumer(rmqConnection, _topic, _topic, false, false, BatchSize);
            
            //create the queue, so that we can receive messages posted to it
            new QueueFactory(rmqConnection, _topic).Create(3000);
        }

        [Fact]
        public void When_a_message_consumer_reads_multiple_messages()
        {
            //Post one more than batch size messages
             var messageOne = new Message(new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND), new MessageBody("test content One"));
            _messageProducer.Send(messageOne);
             var messageTwo= new Message(new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND), new MessageBody("test content Two"));
            _messageProducer.Send(messageTwo);
             var messageThree= new Message(new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND), new MessageBody("test content Three"));
            _messageProducer.Send(messageThree);
             var messageFour= new Message(new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND), new MessageBody("test content Four"));
            _messageProducer.Send(messageFour);
            
            //let them arrive
            Task.Delay(5000);
            
            //Now retrieve messages from the consumer
            var messages = _messageConsumer.Receive(1000);
            
            //We should only have three messages
            messages.Length.Should().Be(3);
            
            //ack those to remove from the queue
            foreach (var message in messages)
            {
                _messageConsumer.Acknowledge(message);
            }

            //Allow ack to register
            Task.Delay(1000);
            
            //Now retrieve again
            messages = _messageConsumer.Receive(500);

            //This time, just the one message
            messages.Length.Should().Be(1);

        }

        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageConsumer.Dispose();
        }
    }
}
