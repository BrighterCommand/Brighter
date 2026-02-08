#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Reject.TestDoubles;
using Paramore.Brighter.Reject.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Reject
{
    public class When_handler_throws_exception_should_reject_message : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _command = new();

        public When_handler_throws_exception_should_reject_message()
        {
            //Arrange
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailingRejectHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(type =>
            {
                if (type == typeof(MyFailingRejectHandler))
                    return new MyFailingRejectHandler();
                if (type == typeof(RejectMessageOnErrorHandler<MyCommand>))
                    return new RejectMessageOnErrorHandler<MyCommand>();
                throw new ArgumentOutOfRangeException(nameof(type), type.Name, null);
            });

            MyFailingRejectHandler.HandlerCalled = false;

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
        public void It_should_throw_RejectMessageAction_with_original_exception()
        {
            //Act
            var exception = Assert.Throws<RejectMessageAction>(() => _commandProcessor.Send(_command));

            //Assert
            Assert.True(MyFailingRejectHandler.HandlerCalled); // Handler was invoked
            Assert.Equal(MyFailingRejectHandler.EXCEPTION_MESSAGE, exception.Message); // Preserves original message
            Assert.IsType<InvalidOperationException>(exception.InnerException); // Preserves original exception type
            Assert.Equal(MyFailingRejectHandler.EXCEPTION_MESSAGE, exception.InnerException.Message); // Inner has same message
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
