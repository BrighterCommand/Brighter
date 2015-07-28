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
            s_channel.Send(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Send(quitMessage);
        };

        private Because _of = () => s_messagePump.Run();

        private It _should_send_the_message_via_the_command_processor = () => s_commandProcessor.PublishHappened.ShouldBeTrue();
        private It _should_convert_the_message_into_an_event = () => (s_commandProcessor.Observe<MyEvent>()).ShouldEqual(s_event);
        private It _should_dispose_the_input_channel = () => s_channel.DisposeHappened.ShouldBeTrue();
    }

    public class When_a_requeue_exception_is_thrown
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyCommandProcessor s_commandProcessor;
        private static MyEvent s_event;

        private Establish _context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = -1 };

            s_event = new MyEvent();

            var message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_COMMAND), new MessageBody(JsonConvert.SerializeObject(s_event)));
            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(s_event)));
            s_channel.Send(message1);
            s_channel.Send(message2);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Send(quitMessage);
        };

        private Because _of = () => s_messagePump.Run();

        private It _should_send_the_message_via_the_command_processor = () => s_commandProcessor.SendHappened.ShouldBeTrue();
        private It _should_publish_the_message_via_the_command_processor = () => s_commandProcessor.PublishHappened.ShouldBeTrue();
        private It _should_requeue_the_messages = () => s_channel.Length.ShouldEqual(2);
        private It _should_dispose_the_input_channel = () => s_channel.DisposeHappened.ShouldBeTrue();
    }

    public class When_a_channel_failure_exception_is_thrown_should_retry_until_connection_re_established
    {
        private static IAmAMessagePump s_messagePump;
        private static FailingChannel s_channel;
        private static SpyCommandProcessor s_commandProcessor;
        private static MyEvent s_event;

        private Establish _context = () =>
        {
            s_commandProcessor = new SpyCommandProcessor();
            s_channel = new FailingChannel { NumberOfRetries = 4 };
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = -1 };

            s_event = new MyEvent();

            var message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_COMMAND), new MessageBody(JsonConvert.SerializeObject(s_event)));
            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(s_event)));
            s_channel.Send(message1);
            s_channel.Send(message2);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Send(quitMessage);
        };

        private Because _of = () => s_messagePump.Run();

        private It _should_send_the_message_via_the_command_processor = () => s_commandProcessor.SendHappened.ShouldBeTrue();
        private It _should_publish_the_message_via_the_command_processor = () => s_commandProcessor.PublishHappened.ShouldBeTrue();
    }

    public class When_a_requeue_count_threshold_has_been_reached
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyRequeueCommandProcessor s_commandProcessor;
        private static MyEvent s_event;

        private Establish _context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = 3 };

            s_event = new MyEvent();

            var message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_COMMAND), new MessageBody(JsonConvert.SerializeObject(s_event)));
            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(s_event)));
            s_channel.Send(message1);
            s_channel.Send(message2);
        };

        private Because _of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Send(quitMessage);

            Task.WaitAll(new[] { task });
        };

        private It _should_send_the_message_via_the_command_processor = () => s_commandProcessor.SendHappened.ShouldBeTrue();
        private It _should_have_been_handled_3_times_via_send = () => s_commandProcessor.SendCount.ShouldEqual(3);
        private It _should_publish_the_message_via_the_command_processor = () => s_commandProcessor.PublishHappened.ShouldBeTrue();
        private It _should_have_been_handled_3_times_via_publish = () => s_commandProcessor.PublishCount.ShouldEqual(3);
        private It _should_requeue_the_messages = () => s_channel.Length.ShouldEqual(0);
        private It _should_dispose_the_input_channel = () => s_channel.DisposeHappened.ShouldBeTrue();
    }


    public class When_an_unacceptable_message_is_recieved
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

            s_channel.Send(unacceptableMessage);
        };
        private Because of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Send(quitMessage);

            Task.WaitAll(new[] { task });
        };

        private It should_acknowledge_the_message = () => s_channel.AcknowledgeHappened.ShouldBeTrue();
    }


    public class When_an_unacceptable_message_limit_is_reached
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyRequeueCommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3};

            var unacceptableMessage1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_UNACCEPTABLE), new MessageBody(""));
            var unacceptableMessage2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_UNACCEPTABLE), new MessageBody(""));
            var unacceptableMessage3 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_UNACCEPTABLE), new MessageBody(""));

            s_channel.Send(unacceptableMessage1);
            s_channel.Send(unacceptableMessage2);
            s_channel.Send(unacceptableMessage3);
        };

        private Because of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            Task.WaitAll(new[] { task });
        };

        private It should_have_acknowledge_the_3_messages = () => s_channel.AcknowledgeCount.ShouldEqual(3);
        private It should_dispose_the_input_channel = () => s_channel.DisposeHappened.ShouldBeTrue();
    }


    public class When_a_message_fails_to_be_mapped_to_a_request
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyRequeueCommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new FailingEventMessageMapper();
            s_messagePump = new MessagePump<MyFailingMapperEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3 };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            s_channel.Send(unmappableMessage);
        };

        private Because of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            s_channel.Stop();

            Task.WaitAll(new[] { task });
        };

        private It should_have_acknowledge_the_message = () => s_channel.AcknowledgeHappened.ShouldBeTrue();
    }

    public class When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyRequeueCommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new FailingEventMessageMapper();
            s_messagePump = new MessagePump<MyFailingMapperEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3 };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            s_channel.Send(unmappableMessage);
            s_channel.Send(unmappableMessage);
            s_channel.Send(unmappableMessage);
        };

        private Because of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            Task.WaitAll(new[] { task });
        };

        private It should_have_acknowledge_the_3_messages = () => s_channel.AcknowledgeCount.ShouldEqual(3);
        private It should_dispose_the_input_channel = () => s_channel.DisposeHappened.ShouldBeTrue();
    }



}
