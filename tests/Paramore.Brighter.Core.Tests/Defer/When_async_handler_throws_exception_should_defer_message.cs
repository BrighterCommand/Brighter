#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Defer.TestDoubles;
using Paramore.Brighter.Defer.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Defer
{
    public class When_async_handler_throws_exception_should_defer_message
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _command = new();

        public When_async_handler_throws_exception_should_defer_message()
        {
            //Arrange
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyFailingDeferHandlerAsync>();

            var handlerFactory = new SimpleHandlerFactoryAsync(type =>
            {
                if (type == typeof(MyFailingDeferHandlerAsync))
                    return new MyFailingDeferHandlerAsync();
                if (type == typeof(DeferMessageOnErrorHandlerAsync<MyCommand>))
                    return new DeferMessageOnErrorHandlerAsync<MyCommand>();
                throw new ArgumentOutOfRangeException(nameof(type), type.Name, null);
            });

            MyFailingDeferHandlerAsync.HandlerCalled = false;

            _commandProcessor = new CommandProcessor(
                registry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory()
            );
        }

        [Fact]
        public async Task It_should_throw_DeferMessageAction_with_original_exception_and_delay()
        {
            //Act
            var exception = await Assert.ThrowsAsync<DeferMessageAction>(() => _commandProcessor.SendAsync(_command));

            //Assert
            Assert.True(MyFailingDeferHandlerAsync.HandlerCalled); // Handler was invoked
            Assert.Equal(MyFailingDeferHandlerAsync.EXCEPTION_MESSAGE, exception.Message); // Preserves original message
            Assert.IsType<InvalidOperationException>(exception.InnerException); // Preserves original exception type
            Assert.Equal(MyFailingDeferHandlerAsync.EXCEPTION_MESSAGE, exception.InnerException.Message); // Inner has same message
            Assert.Equal(TimeSpan.FromMilliseconds(5000), exception.Delay); // Delay from attribute flows through
        }
    }
}
