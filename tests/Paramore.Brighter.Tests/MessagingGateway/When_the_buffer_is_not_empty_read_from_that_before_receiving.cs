using System;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway
{
    public class BufferedChannelTests
    {
        private readonly IAmAChannel _channel;
        private readonly IAmAMessageConsumer _gateway;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;
        private Message _messageThree;
        private const int BufferLimit = 3;

        public BufferedChannelTests()
        {
            _gateway = A.Fake<IAmAMessageConsumer>();

            _channel = new Channel("test", _gateway, BufferLimit);

            _messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("FirstMessage"));
           
            _messageTwo = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("SecondMessage"));

        }
        
        [Fact]
        public void When_the_buffer_is_not_empty_read_from_that_before_receiving()
        {
            _channel.Enqueue(_messageOne, _messageTwo);
             
            _messageThree = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("ThirdMessage"));
            
            A.CallTo(() => _gateway.Receive(10)).Returns(new Message[] {_messageThree});
            
            //pull the first message enqueued from the buffer
            _channel.Receive(10).Id.Should().Be(_messageOne.Id);
            //pull the second message enqueued from the buffer
            _channel.Receive(10).Id.Should().Be(_messageTwo.Id);
            //now we pull from the queue as the buffer is empty
            _channel.Receive(10).Id.Should().Be(_messageThree.Id);
            A.CallTo(() => _gateway.Receive(10)).MustHaveHappened();
         }

        [Fact]
        public void When_the_buffer_is_replenished_allow_up_to_the_maximum_number_of_new_elements_to_enqueue()
        {
            //put BufferLimit messages on the queue first
            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("FirstMessage"));
            
            var messageTwo = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("SecondMessage"));
            
            var messageThree = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("ThirdMessage"));
            
            var messageFour = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("FourthMessage"));
            

            // This should be fine
             _channel.Enqueue(messageOne, messageTwo, messageThree);
            
             //This should throw an exception
             Assert.Throws<InvalidOperationException>(() => _channel.Enqueue(messageOne, messageTwo, messageThree, messageFour));
            
        }

        [Fact]
        public void When_we_try_to_create_with_too_small_a_buffer()
        {
              Assert.Throws<ConfigurationException>(() => new Channel("test", _gateway, 0));
        }

        [Fact]
        public void When_we_try_to_create_with_too_large_a_buffer()
        {
              Assert.Throws<ConfigurationException>(() => new Channel("test", _gateway, 11));
        }
        
        [Fact]
        public void When_the_gateway_returns_an_array_of_messages_enqueue_them_into_the_buffer_then_retrieve_from_there()
        {
               A.CallTo(() => _gateway.Receive(10)).Returns(new Message[] {_messageOne, _messageTwo, _messageThree});
  
        }
    }
}
