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
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Mappers;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    public class When_mapping_from_a_heartbeat_reply_to_a_message : ContextSpecification
    {
        private static IAmAMessageMapper<HeartbeatReply> s_mapper;
        private static Message s_message;
        private static HeartbeatReply s_request;
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();
        private static RunningConsumer s_firstConsumer;
        private static RunningConsumer s_secondConsumer;

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatReplyCommandMessageMapper();
            s_request = new HeartbeatReply("Test.Hostname", new ReplyAddress(TOPIC, s_correlationId));
            s_firstConsumer = new RunningConsumer(new ConnectionName("Test.Connection"), ConsumerState.Open);
            s_request.Consumers.Add(s_firstConsumer);
            s_secondConsumer = new RunningConsumer(new ConnectionName("More.Consumers"),ConsumerState.Shut );
            s_request.Consumers.Add(s_secondConsumer);
        };

        private Because _of = () => s_message = s_mapper.MapToMessage(s_request);

        private It _should_put_the_reply_to_as_the_topic = () => s_message.Header.Topic.ShouldEqual(TOPIC);
        private It _should_put_the_correlation_id_in_the_header = () => s_message.Header.CorrelationId.ShouldEqual(s_correlationId);
        private It _should_put_the_connections_into_the_body = () =>
        {
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"ConnectionName\": \"Test.Connection\""));
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"State\": 1"));
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"ConnectionName\": \"More.Consumers\""));
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"State\": 0"));
        };

        private It _should_put_the_hostname_in_the_message_body = () => s_message.Body.ShouldMatch(body => body.Value.Contains("\"HostName\": \"Test.Hostname\""));
    }
}