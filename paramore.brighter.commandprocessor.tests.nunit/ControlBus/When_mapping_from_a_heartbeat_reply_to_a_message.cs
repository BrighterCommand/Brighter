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
using NUnit.Framework;
using paramore.brighter.serviceactivator;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    [TestFixture]
    public class HeartbeatReplyToMessageMapperTests
    {
        private IAmAMessageMapper<HeartbeatReply> _mapper;
        private Message _message;
        private HeartbeatReply _request;
        private const string TOPIC = "test.topic";
        private readonly Guid _correlationId = Guid.NewGuid();
        private RunningConsumer _firstConsumer;
        private RunningConsumer _secondConsumer;

        [SetUp]
        public void Establish()
        {
            _mapper = new HeartbeatReplyCommandMessageMapper();
            _request = new HeartbeatReply("Test.Hostname", new ReplyAddress(TOPIC, _correlationId));
            _firstConsumer = new RunningConsumer(new ConnectionName("Test.Connection"), ConsumerState.Open);
            _request.Consumers.Add(_firstConsumer);
            _secondConsumer = new RunningConsumer(new ConnectionName("More.Consumers"),ConsumerState.Shut );
            _request.Consumers.Add(_secondConsumer);
        }

        [Test]
        public void When_mapping_from_a_heartbeat_reply_to_a_message()
        {
            _message = _mapper.MapToMessage(_request);

            //_should_put_the_reply_to_as_the_topic
            Assert.AreEqual(TOPIC, _message.Header.Topic);
            //_should_put_the_correlation_id_in_the_header
            Assert.AreEqual(_correlationId, _message.Header.CorrelationId);
            //_should_put_the_connections_into_the_body
            Assert.True(((Func<MessageBody, bool>) (body => body.Value.Contains("\"ConnectionName\": \"Test.Connection\""))).Invoke(_message.Body));
            Assert.True(((Func<MessageBody, bool>) (body => body.Value.Contains("\"State\": 1"))).Invoke(_message.Body));
            Assert.True(((Func<MessageBody, bool>) (body => body.Value.Contains("\"ConnectionName\": \"More.Consumers\""))).Invoke(_message.Body));
            Assert.True(((Func<MessageBody, bool>) (body => body.Value.Contains("\"State\": 0"))).Invoke(_message.Body));
            //_should_put_the_hostname_in_the_message_body
            Assert.True(((Func<MessageBody, bool>) (body => body.Value.Contains("\"HostName\": \"Test.Hostname\""))).Invoke(_message.Body));
        }
    }
}