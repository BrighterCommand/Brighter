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
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Mappers;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    public class When_mapping_from_a_message_to_a_heartbeat_reply : ContextSpecification
    {
        private static IAmAMessageMapper<HeartbeatReply> s_mapper;
        private static Message s_message;
        private static HeartbeatReply s_request;
        private const string MESSAGE_BODY = "{\r\n  \"HostName\": \"Test.Hostname\",\r\n  \"Consumers\": [\r\n    {\r\n      \"ConnectionName\": \"Test.Connection\",\r\n      \"State\": 1\r\n    },\r\n    {\r\n      \"ConnectionName\": \"More.Consumers\",\r\n      \"State\": 0\r\n    }\r\n  ]\r\n}";
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatReplyCommandMessageMapper();
            var header = new MessageHeader(messageId: Guid.NewGuid(), topic: TOPIC, messageType: MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow, correlationId: s_correlationId);
            var body = new MessageBody(MESSAGE_BODY);
            s_message = new Message(header, body);
        };

        private Because _of = () => s_request = s_mapper.MapToRequest(s_message);

        private It _should_set_the_sender_address_topic = () => s_request.SendersAddress.Topic.ShouldEqual(TOPIC);
        private It _should_set_the_sender_correlation_id = () => s_request.SendersAddress.CorrelationId.ShouldEqual(s_correlationId);
        private It _should_set_the_hostName = () => s_request.HostName.ShouldEqual("Test.Hostname");
        private It _should_contain_the_consumers = () =>
        {
            s_request.Consumers.ShouldContain(rc => rc.ConnectionName == "Test.Connection" && rc.State == ConsumerState.Open);
            s_request.Consumers.ShouldContain(rc => rc.ConnectionName == "More.Consumers" && rc.State == ConsumerState.Shut);
        };
    }
}
