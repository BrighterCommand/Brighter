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
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using System.Diagnostics;

namespace paramore.commandprocessor.tests.MessagingGateway
{
    public class When_listening_to_messages_on_a_channel
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_gateway;
        private static Message s_receivedMessage;
        private static Message s_sentMessage;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_gateway);

            s_sentMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            A.CallTo(() => s_gateway.Receive(1000)).Returns(s_sentMessage);
        };

        private Because _of = () => s_receivedMessage = s_channel.Receive(1000);

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Receive(1000)).MustHaveHappened();
        private It _should_return_the_next_message_from_the_gateway = () => s_receivedMessage.ShouldEqual(s_sentMessage);
    }

    public class When_a_stop_message_is_added_to_a_channel
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_gateway;
        private static Message s_receivedMessage;
        private static Message s_sentMessage;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_gateway);

            s_sentMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_channel.Stop();

            A.CallTo(() => s_gateway.Receive(1000)).Returns(s_sentMessage);
        };

        private Because _of = () => s_receivedMessage = s_channel.Receive(1000);

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Receive(1000)).MustNotHaveHappened();
    }

    public class When_acknowledge_is_called_on_a_channel
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_gateway;
        private static Message s_receivedMessage;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_gateway);

            s_receivedMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_receivedMessage.SetDeliveryTag(12345UL);
        };

        private Because _of = () => s_channel.Acknowledge(s_receivedMessage);

        private It _should_ackonwledge_the_message = () => A.CallTo(() => s_gateway.Acknowledge(s_receivedMessage)).MustHaveHappened();
    }

    public class When_no_acknowledge_is_called_on_a_channel
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_gateway;
        private static Message s_receivedMessage;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_gateway);

            s_receivedMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_receivedMessage.SetDeliveryTag(12345UL);
        };

        private Because _of = () => s_channel.Reject(s_receivedMessage);

        private It _should_ackonwledge_the_message = () => A.CallTo(() => s_gateway.Reject(s_receivedMessage, true)).MustHaveHappened();
    }

    public class When_disposing_input_channel
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_messageConsumer;

        private Establish _context = () =>
        {
            s_messageConsumer = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_messageConsumer);
        };

        private Because _of = () => s_channel.Dispose();

        private It _should_call_dipose_on_messaging_gateway = () => A.CallTo(() => s_messageConsumer.Dispose()).MustHaveHappened();
    }

    public class When_requeuing_a_message_with_no_delay
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_gateway;
        private static Message s_requeueMessage;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_gateway);

            s_requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));
        };

        private Because _of = () => s_channel.Requeue(s_requeueMessage);

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Requeue(s_requeueMessage)).MustHaveHappened();
    }

    public class When_requeuing_a_message_with_unsupported_delay
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumer s_gateway;
        private static Message s_requeueMessage;
        private static Stopwatch s_stopWatch;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumer>();

            s_channel = new InputChannel("test", s_gateway);

            s_requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_stopWatch = new Stopwatch();
        };

        private Because _of = () =>
        {
            s_stopWatch.Start();
            s_channel.Requeue(s_requeueMessage, 1000);
            s_stopWatch.Stop();
        };

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Requeue(s_requeueMessage)).MustHaveHappened();
        private It _should_have_process_delayed_the_call = () => (s_stopWatch.ElapsedMilliseconds > 900).ShouldBeTrue();
    }

    public class When_requeuing_a_message_with_supported_but_disabled_delay
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumerSupportingDelay s_gateway;
        private static Message s_requeueMessage;
        private static Stopwatch s_stopWatch;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => s_gateway.DelaySupported).Returns(false);

            s_channel = new InputChannel("test", s_gateway);

            s_requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_stopWatch = new Stopwatch();
        };

        private Because _of = () =>
        {
            s_stopWatch.Start();
            s_channel.Requeue(s_requeueMessage, 1000);
            s_stopWatch.Stop();
        };

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Requeue(s_requeueMessage)).MustHaveHappened();
        private It _should_have_process_delayed_the_call = () => (s_stopWatch.ElapsedMilliseconds > 900).ShouldBeTrue();
    }

    public class When_requeuing_a_message_with_supported_and_enabled_delay
    {
        private static IAmAnInputChannel s_channel;
        private static IAmAMessageConsumerSupportingDelay s_gateway;
        private static Message s_requeueMessage;
        private static Stopwatch s_stopWatch;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => s_gateway.DelaySupported).Returns(true);

            s_channel = new InputChannel("test", s_gateway);

            s_requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_stopWatch = new Stopwatch();
        };

        private Because _of = () =>
        {
            s_stopWatch.Start();
            s_channel.Requeue(s_requeueMessage, 1000);
            s_stopWatch.Stop();
        };

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Requeue(s_requeueMessage, 1000)).MustHaveHappened();
        private It _should_have_used_gateway_delay_support = () => (s_stopWatch.ElapsedMilliseconds < 500).ShouldBeTrue();
    }
}
