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
using System.Linq;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.Tests.TestDoubles;
using Paramore.Brighter.Tests.Timeout.Test_Doubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.Timeout
{
    public class TimeoutHandlerFailsCheckTests
    {
        private CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private AggregateException _thrownException;

        public TimeoutHandlerFailsCheckTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsDueToTimeoutHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsDueToTimeoutHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, TimeoutPolicyHandler<MyCommand>>().AsSingleton();

            MyFailsDueToTimeoutHandlerStateTracker.WasCancelled = false;
            MyFailsDueToTimeoutHandlerStateTracker.TaskCompleted = true;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public void When_Sending_A_Command_To_The_Processor_Failing_A_Timeout_Policy_Check()
        {
            _thrownException = (AggregateException)Catch.Exception(() => _commandProcessor.Send(_myCommand));

            //_should_throw_a_timeout_exception
            _thrownException.Flatten().InnerExceptions.First().Should().BeOfType<TimeoutException>();
            //_should_signal_that_a_timeout_occured_and_handler_should_be_cancelled
            MyFailsDueToTimeoutHandlerStateTracker.WasCancelled.Should().BeTrue();
            //_should_not_run_to_completion
            MyFailsDueToTimeoutHandlerStateTracker.TaskCompleted.Should().BeFalse();
        }
    }
}