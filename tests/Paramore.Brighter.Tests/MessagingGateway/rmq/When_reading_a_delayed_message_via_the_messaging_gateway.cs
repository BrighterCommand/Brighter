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
using System.Collections.Generic;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.RMQ
{
    [Trait("Category", "RMQ")]
    public class RmqMessageProducerDelayedMessageTests : IDisposable
    {
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly Message _message;
        private readonly TestRMQListener _client;
        private string _messageBody;
        private bool _immediateReadIsNull;
        private IDictionary<string, object> _messageHeaders;

        public RmqMessageProducerDelayedMessageTests()
        {
            var header = new MessageHeader(Guid.NewGuid(), "test3", MessageType.MT_COMMAND);
            var originalMessage = new Message(header, new MessageBody("test3 content"));

            var mutatedHeader = new MessageHeader(header.Id, "test3", MessageType.MT_COMMAND);
            mutatedHeader.Bag.Add(HeaderNames.DELAY_MILLISECONDS, 1000);
            _message = new Message(mutatedHeader, originalMessage.Body);

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange", supportDelay: true)
            };

            _messageProducer = new RmqMessageProducer(rmqConnection);
            _messageConsumer = new RmqMessageConsumer(rmqConnection, _message.Header.Topic, _message.Header.Topic, false, 1, false);
            _messageConsumer.Purge();

            _client = new TestRMQListener(rmqConnection, _message.Header.Topic);
        }

        [Fact]
        public void When_reading_a_delayed_message_via_the_messaging_gateway()
        {
            _messageProducer.SendWithDelay(_message, 1000);

            var immediateResult = _client.Listen(0, true);
            _immediateReadIsNull = immediateResult == null;

            var delayedResult = _client.Listen(2000);
            _messageBody = delayedResult.GetBody();
            _messageHeaders = delayedResult.GetHeaders();

            //_should_have_not_been_able_get_message_before_delay
            _immediateReadIsNull.Should().BeTrue();
            //_should_send_a_message_via_rmq_with_the_matching_body
            _messageBody.Should().Be(_message.Body.Value);
            //_should_send_a_message_via_rmq_with_delay_header
            _messageHeaders.Keys.Should().Contain(HeaderNames.DELAY_MILLISECONDS);
            //_should_received_a_message_via_rmq_with_delayed_header
            _messageHeaders.Keys.Should().Contain(HeaderNames.DELAYED_MILLISECONDS);
        }

        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageProducer.Dispose();
        }
    }
}