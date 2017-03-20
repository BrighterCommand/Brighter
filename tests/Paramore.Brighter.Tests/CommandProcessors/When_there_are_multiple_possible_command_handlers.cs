#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Xunit;
using Paramore.Brighter.Tests.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests
{
    public class CommandProcessorSendWithMultipleMatchesTests
    {
        private CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _exception;

        public CommandProcessorSendWithMultipleMatchesTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyCommand, MyImplicitHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>("DefaultHandler");
            container.Register<IHandleRequests<MyCommand>, MyImplicitHandler>("ImplicitHandler");
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public void When_There_Are_Multiple_Possible_Command_Handlers()
        {
            _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));

            //_should_fail_because_multiple_receivers_found
            Assert.IsAssignableFrom(typeof(ArgumentException), _exception);
            //_should_have_an_error_message_that_tells_you_why
            Assert.NotNull(_exception);
            StringAssert.Contains("More than one handler was found for the typeof command Paramore.Brighter.Tests.TestDoubles.MyCommand - a command should only have one handler.", _exception.Message);
        }
    }
}