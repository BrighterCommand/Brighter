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
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    public class When_Reading_A_Message_From_A_Channel_Pump_Out_To_Command_Processor
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyCommandProcessor s_commandProcessor;
        private static MyEvent s_event;

        private Establish _context = () =>
        {
            s_commandProcessor = new SpyCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000 };

            s_event = new MyEvent();

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(s_event)));
            s_channel.Add(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Add(quitMessage);
        };

        private Because _of = () => s_messagePump.Run();

        private It _should_send_the_message_via_the_command_processor = () => s_commandProcessor.Commands[0].ShouldEqual(CommandType.Publish);
        private It _should_convert_the_message_into_an_event = () => (s_commandProcessor.Observe<MyEvent>()).ShouldEqual(s_event);
        private It _should_dispose_the_input_channel = () => s_channel.DisposeHappened.ShouldBeTrue();
    }
}