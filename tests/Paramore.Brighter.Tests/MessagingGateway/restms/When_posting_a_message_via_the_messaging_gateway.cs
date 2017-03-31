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
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RESTMS;
using Paramore.Brighter.MessagingGateway.RESTMS.MessagingGatewayConfiguration;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.RESTMS
{
    [Trait("Category", "RESTMS")]
    public class RestMsMessageProducerSendTests : IDisposable
    {
        private const string Topic = "test";
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly Message _message;
        private Message _sentMessage;
        private string _messageBody;
        private const string QueueName = "test";

        public RestMsMessageProducerSendTests()
        {
            var configuration = new RestMSMessagingGatewayConfiguration
            {
                Feed = new Feed { Name = "test", Type = "Default"},
                RestMS = new RestMsSpecification { Uri = new Uri("http://localhost:3416/restms/domain/default"),  Id = "dh37fgj492je", User ="Guest", Key ="wBgvhp1lZTr4Tb6K6+5OQa1bL9fxK7j8wBsepjqVNiQ=", Timeout=2000}
            };
            _messageProducer = new RestMsMessageProducer(configuration);
            _messageConsumer = new RestMsMessageConsumer(configuration, QueueName, Topic);
            _message = new Message(new MessageHeader(Guid.NewGuid(), Topic, MessageType.MT_COMMAND), new MessageBody("test content"));
        }

        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _messageConsumer.Receive(30000); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _messageProducer.Send(_message);
            _sentMessage = _messageConsumer.Receive(30000);
            _messageBody = _sentMessage.Body.Value;
            _messageConsumer.Acknowledge(_sentMessage);

            //_should_send_a_message_via_restms_with_the_matching_body
            _messageBody.Should().Be(_message.Body.Value);
            //_should_have_an_empty_pipe_after_acknowledging_the_message
            ((RestMsMessageConsumer)_messageConsumer).NoOfOutstandingMessages(30000).Should().Be(0);
        }

        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageProducer.Dispose();
        }
    }
}