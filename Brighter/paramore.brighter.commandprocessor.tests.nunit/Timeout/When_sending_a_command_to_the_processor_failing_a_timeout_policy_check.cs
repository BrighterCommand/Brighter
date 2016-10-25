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
using FakeItEasy;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.policy.Handlers;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.Timeout.Test_Doubles;
using TinyIoC;
using NUnit.Specifications;
using nUnitShouldAdapter;

namespace paramore.brighter.commandprocessor.tests.nunit.Timeout
{
    [Subject(typeof(TimeoutPolicyHandler<>))]
//    [Tags("Requires", new[] { "Fails on AppVeyor" })]
    public class When_Sending_A_Command_To_The_Processor_Failing_A_Timeout_Policy_Check : ContextSpecification
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static AggregateException s_thrownException;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsDueToTimeoutHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<ILog>(logger);
            container.Register<IHandleRequests<MyCommand>, MyFailsDueToTimeoutHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, TimeoutPolicyHandler<MyCommand>>().AsSingleton();

            MyFailsDueToTimeoutHandlerStateTracker.WasCancelled = false;
            MyFailsDueToTimeoutHandlerStateTracker.TaskCompleted = true;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        //We have to catch the final exception that bubbles out after retry
        private Because _of = () =>
        {
            s_thrownException = (AggregateException)Catch.Exception(() => s_commandProcessor.Send(s_myCommand));
        };

        private It _should_throw_a_timeout_exception = () => s_thrownException.Flatten().InnerExceptions.First().ShouldBeOfExactType<TimeoutException>();
        private It _should_signal_that_a_timeout_occured_and_handler_should_be_cancelled = () => MyFailsDueToTimeoutHandlerStateTracker.WasCancelled.ShouldBeTrue();
        private It _should_not_run_to_completion = () => MyFailsDueToTimeoutHandlerStateTracker.TaskCompleted.ShouldBeFalse();
    }
}