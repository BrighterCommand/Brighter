#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using Polly;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof (CommandProcessorBuilder))]
    public class When_Building_With_A_Default_Policy_Sufficient_To_Post
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Message s_message;
        private static FakeMessageStore s_fakeMessageStore;
        private static FakeMessageProducer s_fakeMessageProducer;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_myCommand.Value = "Hello World";

            s_fakeMessageStore = new FakeMessageStore();
            s_fakeMessageProducer = new FakeMessageProducer();

            s_message = new Message(
                header:
                    new MessageHeader(messageId: s_myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(s_myCommand))
                );

            var messageMapperRegistry =
                new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            s_commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactory()))
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration((IAmAMessageStore<Message>)s_fakeMessageStore, (IAmAMessageProducer) s_fakeMessageProducer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        };

        private Because _of = () => s_commandProcessor.Post(s_myCommand);

        private It _should_store_the_message_in_the_sent_command_message_repository = () => s_fakeMessageStore.MessageWasAdded.ShouldBeTrue();
        private It _should_send_a_message_via_the_messaging_gateway = () => s_fakeMessageProducer.MessageWasSent.ShouldBeTrue();
        private It _should_convert_the_command_into_a_message =() => s_fakeMessageStore.Get().First().ShouldEqual(s_message);

        internal class EmptyHandlerFactory : IAmAHandlerFactory
        {
            public IHandleRequests Create(Type handlerType)
            {
                return null;
            }

            public void Release(IHandleRequests handler) {}
        }
    }
}
