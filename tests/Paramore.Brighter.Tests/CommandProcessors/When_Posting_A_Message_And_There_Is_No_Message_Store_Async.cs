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
using Nito.AsyncEx;
using Xunit;
using Paramore.Brighter.Tests.TestDoubles;
using Polly;

namespace Paramore.Brighter.Tests
{
    public class CommandProcessorNoMessageStoreAsyncTests : IDisposable

    {
        private CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private FakeMessageProducer _fakeMessageProducer;
        private Exception _exception;

        public CommandProcessorNoMessageStoreAsyncTests()
        {
            _myCommand.Value = "Hello World";

            _fakeMessageProducer = new FakeMessageProducer();

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                null,
                (IAmAMessageProducer)_fakeMessageProducer);
        }

        [Fact]
        public void When_Posting_A_Message_And_There_Is_No_Message_Store_Async()
        {
            _exception = Catch.Exception(() => AsyncContext.Run(async () => await _commandProcessor.PostAsync(_myCommand)));
        }

        public void Dispose()
        {
            _commandProcessor.Dispose();

            //_should_throw_an_exception
            Assert.IsInstanceOf<InvalidOperationException>(_exception);
        }
    }
}
