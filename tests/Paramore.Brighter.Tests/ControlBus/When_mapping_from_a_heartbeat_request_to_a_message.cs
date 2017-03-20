#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Xunit;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Tests.ControlBus
{
    public class HearbeatRequestToMessageMapperTests
    {
        private IAmAMessageMapper<HeartbeatRequest> _mapper;
        private Message _message;
        private HeartbeatRequest _request;
        private const string TOPIC = "test.topic";
        private readonly Guid _correlationId = Guid.NewGuid();

        public HearbeatRequestToMessageMapperTests()
        {
            _mapper = new HeartbeatRequestCommandMessageMapper();

            _request = new HeartbeatRequest(new ReplyAddress(TOPIC, _correlationId));
        }

        [Fact]
        public void When_mapping_from_a_heartbeat_request_to_a_message()
        {
            _message = _mapper.MapToMessage(_request);

            //_should_serialize_the_message_type_to_the_header
            Assert.AreEqual(MessageType.MT_COMMAND, _message.Header.MessageType);
            //_should_serialize_the_message_id_to_the_message_body
            Assert.True(_message.Body.Value.Contains(string.Format("\"Id\": \"{0}\"", _request.Id)));
            //_should_serialize_the_topic_to_the_message_body
            Assert.AreEqual(TOPIC, _message.Header.ReplyTo);
            //_should_serialize_the_correlation_id_to_the_message_body
            Assert.AreEqual(_correlationId, _message.Header.CorrelationId);
        }
    }
}