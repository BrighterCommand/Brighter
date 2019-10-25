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
using Xunit;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatMessageToRequestTests
    {
        private readonly IAmAMessageMapper<HeartbeatRequest> _mapper;
        private readonly Message _message;
        private HeartbeatRequest _request;
        private const string TOPIC = "test.topic";
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly Guid _commandId = Guid.NewGuid();

        public HeartbeatMessageToRequestTests()
        {
            _mapper = new HeartbeatRequestCommandMessageMapper();
            var messageHeader = new MessageHeader(
                Guid.NewGuid(),
                "Heartbeat",
                MessageType.MT_COMMAND,
                DateTime.UtcNow,
                _correlationId, TOPIC);

            var body = String.Format("\"Id\": \"{0}\"", _commandId);
            var messageBody = new MessageBody("{" + body + "}");
            _message = new Message(messageHeader, messageBody);
        }

        [Fact]
        public void When_mapping_from_a_message_to_a_heartbeat_request()
        {
            _request = _mapper.MapToRequest(_message);

            //_should_put_the_message_reply_topic_into_the_address
            _request.ReplyAddress.Topic.Should().Be(TOPIC);
            //_should_put_the_message_correlation_id_into_the_address
            _request.ReplyAddress.CorrelationId.Should().Be(_correlationId);
            //_should_set_the_id_of_the_request
            _request.Id.Should().Be(_commandId);
        }
    }
}
