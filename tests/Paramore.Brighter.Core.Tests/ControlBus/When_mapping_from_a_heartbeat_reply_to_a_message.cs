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
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatReplyToMessageMapperTests
    {
        private readonly IAmAMessageMapper<HeartbeatReply> _mapper;
        private Message _message;
        private readonly HeartbeatReply _request;
        private const string TOPIC = "test.topic";
        private readonly Guid _correlationId = Guid.NewGuid();

        public HeartbeatReplyToMessageMapperTests()
        {
            _mapper = new HeartbeatReplyCommandMessageMapper();
            _request = new HeartbeatReply("Test.Hostname", new ReplyAddress(TOPIC, _correlationId));
            var firstConsumer = new RunningConsumer(new ConsumerName("Test.Consumer1"), ConsumerState.Open);
            _request.Consumers.Add(firstConsumer);
            var secondConsumer = new RunningConsumer(new ConsumerName("More.Consumers2"),ConsumerState.Shut );
            _request.Consumers.Add(secondConsumer);
        }

        [Fact]
        public void When_mapping_from_a_heartbeat_reply_to_a_message()
        {
            _message = _mapper.MapToMessage(_request);

            //_should_put_the_reply_to_as_the_topic
            _message.Header.Topic.Should().Be(TOPIC);
            //_should_put_the_correlation_id_in_the_header
            _message.Header.CorrelationId.Should().Be(_correlationId);
            //_should_put_the_connections_into_the_body
            _message.Body.Value.Should().Contain("\"ConsumerName\": \"Test.Consumer1\"");
            _message.Body.Value.Should().Contain("\"State\": 1");
            _message.Body.Value.Should().Contain("\"ConsumerName\": \"More.Consumers2\"");
            _message.Body.Value.Should().Contain("\"State\": 0");

            //_should_put_the_hostname_in_the_message_body
            _message.Body.Value.Should().Contain("\"HostName\": \"Test.Hostname\"");
        }
    }
}
