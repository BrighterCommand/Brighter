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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PostFailureLimitCommandTests
    {
        private readonly CommandProcessor _commandProcessor;
        private IAmAMessageProducer _fakeMessageProducer;
        private InMemoryOutbox _outbox;

        public PostFailureLimitCommandTests()
        {
            _outbox = new InMemoryOutbox();
            _fakeMessageProducer = new FakeErroringMessageProducer();

            var messageMapperRegistry =
                new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactory()))
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration((IAmAnOutbox<Message>)_outbox, (IAmAMessageProducer) _fakeMessageProducer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        }

        [Fact]
        public void When_Posting_Fails_Limit_Total_Writes_To_OutBox_In_Window()
        {
            //We are only going to allow 50 erroring messages
            _fakeMessageProducer.MaxOutStandingMessages = 5;
            _fakeMessageProducer.MaxOutStandingCheckIntervalMilliSeconds = 1000;

            var sentList = new List<Guid>(); 
            bool shouldThrowException = false;
            try
            {
                do
                {
                    var command = new MyCommand{Value = $"Hello World: {sentList.Count() + 1}"};
                    _commandProcessor.Post(command);
                    sentList.Add(command.Id);

                    //We need to wait for the sweeper thread to check the outstanding in the outbox
                    Task.Delay(50).Wait();

                } while (sentList.Count < 10);
            }
            catch (OutboxLimitReachedException)
            {
                shouldThrowException = true;
            }
            
            //We should error before the end
            shouldThrowException.Should().BeTrue();
            
            //should store the message in the sent outbox
            foreach (var id in sentList)
            {
                _outbox.Get(id).Should().NotBeNull();
            }
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
