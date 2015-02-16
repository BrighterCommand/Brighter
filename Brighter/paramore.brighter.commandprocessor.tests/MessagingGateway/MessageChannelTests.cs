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

namespace paramore.commandprocessor.tests.MessagingGateway
{
    public class When_listening_to_messages_on_a_channel
    {
        private static IAmAnInputChannel channel;
        private static IAmAMessageConsumer gateway;
        private static Message receivedMessage;
        private static Message sentMessage;

        Establish context = () =>
        {
            gateway = A.Fake<IAmAMessageConsumer>();

            channel = new InputChannel("test", "key", gateway);

            sentMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));
                
            A.CallTo(() => gateway.Receive(1000)).Returns(sentMessage);
        };

        Because of = () => receivedMessage = channel.Receive(1000);

        It should_call_the_messaging_gateway = () => A.CallTo(() => gateway.Receive(1000)).MustHaveHappened();
        It should_return_the_next_message_from_the_gateway = () => receivedMessage.ShouldEqual(sentMessage);
    }

    public class When_a_stop_message_is_added_to_a_channel
    {
        private static IAmAnInputChannel channel;
        private static IAmAMessageConsumer gateway;
        private static Message receivedMessage;
        private static Message sentMessage;

        Establish context = () =>
        {
            gateway = A.Fake<IAmAMessageConsumer>();

            channel = new InputChannel("test", "key", gateway);

            sentMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            channel.Stop();
                
            A.CallTo(() => gateway.Receive(1000)).Returns(sentMessage);
        };

        Because of = () => receivedMessage = channel.Receive(1000);

        It should_call_the_messaging_gateway = () => A.CallTo(() => gateway.Receive(1000)).MustNotHaveHappened();
    }

    public class When_acknowledge_is_called_on_a_channel
    {
        private static IAmAnInputChannel channel;
        private static IAmAMessageConsumer gateway;
        private static Message receivedMessage;

        Establish context = () =>
        {
            gateway = A.Fake<IAmAMessageConsumer>();

            channel = new InputChannel("test", "key", gateway);

            receivedMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            receivedMessage.SetDeliveryTag(12345UL);

        };

        Because of = () => channel.Acknowledge(receivedMessage);

        It should_ackonwledge_the_message = () => A.CallTo(() => gateway.Acknowledge(receivedMessage)).MustHaveHappened();

    }

    public class When_no_acknowledge_is_called_on_a_channel
    {
        private static IAmAnInputChannel channel;
        private static IAmAMessageConsumer gateway;
        private static Message receivedMessage;

        Establish context = () =>
        {
            gateway = A.Fake<IAmAMessageConsumer>();

            channel = new InputChannel("test", "key", gateway);

            receivedMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            receivedMessage.SetDeliveryTag(12345UL);

        };

        Because of = () => channel.Reject(receivedMessage);

        It should_ackonwledge_the_message = () => A.CallTo(() => gateway.Reject(receivedMessage, false)).MustHaveHappened();

    }

    public class When_disposing_input_channel
    {
        private static IAmAnInputChannel channel;
        private static IAmAMessageConsumer messageConsumer;

        Establish context = () =>
        {
            messageConsumer = A.Fake<IAmAMessageConsumer>();

            channel = new InputChannel("test", "key", messageConsumer);
        };

        Because of = () => channel.Dispose();

        It should_call_dipose_on_messaging_gateway = () => A.CallTo(() => messageConsumer.Dispose()).MustHaveHappened();
    }
}
