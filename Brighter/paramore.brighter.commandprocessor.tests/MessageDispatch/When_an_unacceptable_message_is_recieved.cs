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
using System.Threading.Tasks;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    [Subject(typeof(MessagePump<>))]
    public class When_An_Unacceptable_Message_Is_Recieved
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyRequeueCommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = 3 };

            var unacceptableMessage = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_UNACCEPTABLE), new MessageBody(""));

            s_channel.Add(unacceptableMessage);
        };
        private Because of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Add(quitMessage);

            Task.WaitAll(new[] { task });
        };

        private It should_acknowledge_the_message = () => s_channel.AcknowledgeHappened.ShouldBeTrue();
    }
}