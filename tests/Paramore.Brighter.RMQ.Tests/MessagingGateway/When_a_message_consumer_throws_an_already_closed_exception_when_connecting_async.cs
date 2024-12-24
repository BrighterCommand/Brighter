using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.RMQ.Tests.TestDoubles;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway 
{
    [Trait("Category", "RMQ")]
    public class RmqMessageConsumerConnectionClosedTestsAsync : IDisposable, IAsyncDisposable
    {
        private readonly IAmAMessageProducerAsync _sender;
        private readonly IAmAMessageConsumerAsync _receiver;
        private readonly IAmAMessageConsumerAsync _badReceiver;
        private readonly Message _sentMessage;
        private Exception _firstException;

        public RmqMessageConsumerConnectionClosedTestsAsync()
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
        public async Task When_a_message_consumer_throws_an_already_closed_exception_when_connecting()
        {
            await _sender.SendAsync(_sentMessage);
            
            bool exceptionHappened = false;
            try
            {
                await _badReceiver.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
            }
            catch (ChannelFailureException cfe)
            {
                exceptionHappened = true;
                cfe.InnerException.Should().BeOfType<AlreadyClosedException>();
            }
            
            exceptionHappened.Should().BeTrue();
        }

        public void Dispose()
        {
            ((IAmAMessageProducerSync)_sender).Dispose();
            ((IAmAMessageConsumerSync)_receiver).Dispose();
            ((IAmAMessageConsumerSync)_badReceiver).Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await _receiver.DisposeAsync(); 
            await _badReceiver.DisposeAsync();
            await _sender.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
