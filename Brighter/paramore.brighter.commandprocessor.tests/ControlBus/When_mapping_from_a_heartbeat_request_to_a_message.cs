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
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Mappers;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_mapping_from_a_heartbeat_request_to_a_message
    {
        private static IAmAMessageMapper<HeartbeatRequest> s_mapper;
        private static Message s_message;
        private static HeartbeatRequest s_request;
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatRequestCommandMessageMapper();

            s_request = new HeartbeatRequest(new ReplyAddress(TOPIC, s_correlationId));
        };

        private Because _of = () => s_message = s_mapper.MapToMessage(s_request);

        private It _should_serialize_the_message_type_to_the_header = () => s_message.Header.MessageType.ShouldEqual(MessageType.MT_COMMAND); 
        private It _should_serialize_the_message_id_to_the_message_body = () => s_message.Body.Value.Contains(string.Format("\"Id\": \"{0}\"", s_request.Id)).ShouldBeTrue();
        private It _should_serialize_the_topic_to_the_message_body = () => s_message.Header.ReplyTo.ShouldEqual(TOPIC);
        private It _should_serialize_the_correlation_id_to_the_message_body = () => s_message.Header.CorrelationId.ShouldEqual(s_correlationId);

    }
}