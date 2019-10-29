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

namespace Paramore.Brighter.RMQ._Tests.MessagingGateway
{
    [Collection("RMQ")]
    [Trait("Category", "RMQ")]
    public class RmqMessageProducerSendMessageTests : IDisposable
    {
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly Message _message;

        public RmqMessageProducerSendMessageTests()
        {
            _message = new Message(
                new MessageHeader(Guid.NewGuid(), Guid.NewGuid().ToString(), MessageType.MT_COMMAND), 
                new MessageBody("test content"));

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            _messageProducer = new RmqMessageProducer(rmqConnection);
            _messageConsumer = new RmqMessageConsumer(rmqConnection, _message.Header.Topic, _message.Header.Topic, false, false);

            new QueueFactory(rmqConnection, _message.Header.Topic).Create(3000);
        }

        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _messageProducer.Send(_message);

            var result = _messageConsumer.Receive(10000).First(); 

            //_should_send_a_message_via_rmq_with_the_matching_body
            result.Body.Value.Should().Be(_message.Body.Value);
       }

        public void Dispose()
        {
            _messageProducer.Dispose();
        }
    }
}
