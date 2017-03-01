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
using Newtonsoft.Json;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    [TestFixture]
    public class MessagePumpEventRequeueTests
    {
        private IAmAMessagePump _messagePump;
        private FakeChannel _channel;
        private SpyCommandProcessor _commandProcessor;
        private MyEvent _event;

        [SetUp]
        public void Establish()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            _channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            _messagePump = new MessagePump<MyEvent>(_commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = -1 };

            _event = new MyEvent();

            var message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(_event)));
            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(_event)));
            _channel.Add(message1);
            _channel.Add(message2);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Add(quitMessage);
        }

        [Test]
        public void When_A_Requeue_Of_Event_Exception_Is_Thrown()
        {
            _messagePump.Run();

            //_should_publish_the_message_via_the_command_processor
            _commandProcessor.Commands[0].ShouldEqual(CommandType.Publish);
            //_should_requeue_the_messages
            _channel.Length.ShouldEqual(2);
            //_should_dispose_the_input_channel
            _channel.DisposeHappened.ShouldBeTrue();
        }
    }
}