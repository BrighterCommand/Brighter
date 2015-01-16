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
    public class When_reading_a_message_from_a_channel_pump_out_to_command_processor
    {
        static IAmAMessagePump messagePump;
        private static FakeChannel channel;
        static SpyCommandProcessor commandProcessor;
        static MyEvent @event;

        Establish context = () =>
        {
            commandProcessor = new SpyCommandProcessor();
            channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            messagePump = new MessagePump<MyEvent>(commandProcessor, mapper) {Channel = channel, TimeoutInMilliseconds = 5000};

            @event = new MyEvent();

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(@event)));
            channel.Send(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Send(quitMessage);
        };

        Because of = () => messagePump.Run();

        It should_send_the_message_via_the_command_processor = () => commandProcessor.PublishHappened.ShouldBeTrue();
        It should_convert_the_message_into_an_event = () => ((MyEvent) commandProcessor.Request).ShouldEqual(@event);
    }

    public class When_a_requeue_exception_is_throwen
    {
        static IAmAMessagePump messagePump;
        private static FakeChannel channel;
        static SpyCommandProcessor commandProcessor;
        static MyEvent @event;

        Establish context = () =>
        {
            commandProcessor = new SpyRequeueCommandProcessor();
            channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            messagePump = new MessagePump<MyEvent>(commandProcessor, mapper) { Channel = channel, TimeoutInMilliseconds = 5000 };

            @event = new MyEvent();

            var message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_COMMAND), new MessageBody(JsonConvert.SerializeObject(@event)));
            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(@event)));
            channel.Send(message1);
            channel.Send(message2);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Send(quitMessage);
        };

        Because of = () => messagePump.Run();

        It should_send_the_message_via_the_command_processor = () => commandProcessor.SendHappened.ShouldBeTrue();
        It should_publish_the_message_via_the_command_processor = () => commandProcessor.PublishHappened.ShouldBeTrue();
        It should_requeue_the_messages = () => channel.Length.ShouldEqual(2);
    }


}
