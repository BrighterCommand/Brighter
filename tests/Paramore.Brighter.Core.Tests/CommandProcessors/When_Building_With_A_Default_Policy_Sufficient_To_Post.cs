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
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PostCommandTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducer _fakeMessageProducer;

        public PostCommandTests()
        {
            _myCommand.Value = "Hello World";

            _fakeOutbox = new FakeOutbox();
            _fakeMessageProducer = new FakeMessageProducer();

            _message = new Message(
                new MessageHeader(_myCommand.Id, "MyCommand", MessageType.MT_COMMAND),
                new MessageBody(JsonConvert.SerializeObject(_myCommand))
                );

            var messageMapperRegistry =
                new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactory()))
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration((IAmAnOutbox<Message>)_fakeOutbox, (IAmAMessageProducer) _fakeMessageProducer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        }

        [Fact]
        public void When_Building_With_A_Default_Policy_Sufficient_To_Post()
        {
            _commandProcessor.Post(_myCommand);

            //_should_store_the_message_in_the_sent_command_message_repository
            _fakeOutbox
                .Get()
                .SingleOrDefault(msg => msg.Id == _message.Id)
                .Should().NotBe(null);
            //_should_send_a_message_via_the_messaging_gateway
            _fakeMessageProducer.MessageWasSent.Should().BeTrue();
            //_should_convert_the_command_into_a_message
            _fakeOutbox.Get().First().Should().Be(_message);
        }

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
