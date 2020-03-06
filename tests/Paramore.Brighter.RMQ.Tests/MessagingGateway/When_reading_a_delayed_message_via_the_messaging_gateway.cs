#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway
{
    [Collection("RMQ")]
    [Trait("Category", "RMQ")]
    public class RmqMessageProducerDelayedMessageTests : IDisposable
    {
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly Message _message;

        public RmqMessageProducerDelayedMessageTests()
        {
            var header = new MessageHeader(Guid.NewGuid(), Guid.NewGuid().ToString(), MessageType.MT_COMMAND);
            var originalMessage = new Message(header, new MessageBody("test3 content"));

            var mutatedHeader = new MessageHeader(header.Id, Guid.NewGuid().ToString(), MessageType.MT_COMMAND);
            mutatedHeader.Bag.Add(HeaderNames.DELAY_MILLISECONDS, 1000);
            _message = new Message(mutatedHeader, originalMessage.Body);

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.delay.brighter.exchange", supportDelay: true)
            };

            _messageProducer = new RmqMessageProducer(rmqConnection);
            _messageConsumer = new RmqMessageConsumer(rmqConnection, _message.Header.Topic, _message.Header.Topic, false, false);

            new QueueFactory(rmqConnection, _message.Header.Topic).Create(3000);
        }

        [Fact]
        public void When_reading_a_delayed_message_via_the_messaging_gateway()
        {
            _messageProducer.SendWithDelay(_message, 3000);

            var immediateResult = _messageConsumer.Receive(0).First();
            var deliveredWithoutWait = immediateResult.Header.MessageType == MessageType.MT_NONE;
            immediateResult.Header.HandledCount.Should().Be(0);
            immediateResult.Header.DelayedMilliseconds.Should().Be(0);

            //_should_have_not_been_able_get_message_before_delay
            deliveredWithoutWait.Should().BeTrue();
            
            var delayedResult = _messageConsumer.Receive(10000).First();
             

           //_should_send_a_message_via_rmq_with_the_matching_body
            delayedResult.Body.Value.Should().Be(_message.Body.Value);
            delayedResult.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            delayedResult.Header.HandledCount.Should().Be(0);
            delayedResult.Header.DelayedMilliseconds.Should().Be(3000);

            _messageConsumer.Acknowledge(delayedResult);
        }

        [Fact]
        public void When_requeing_a_failed_message_with_delay()
        {
            //send & receive a message
            _messageProducer.Send(_message);
            var message = _messageConsumer.Receive(1000).Single();
            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            message.Header.HandledCount.Should().Be(0);
            message.Header.DelayedMilliseconds.Should().Be(0);

            _messageConsumer.Acknowledge(message);

            //now requeue with a delay
            _message.UpdateHandledCount();
            _messageConsumer.Requeue(_message, 1000);

            //receive and assert
            var message2 = _messageConsumer.Receive(5000).Single();
            message2.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            message2.Header.HandledCount.Should().Be(1);
            message2.Header.DelayedMilliseconds.Should().Be(1000);

            _messageConsumer.Acknowledge(message2);
        }

        public void Dispose()
        {
            _messageConsumer.Dispose();
            _messageProducer.Dispose();
        }
    }
}
