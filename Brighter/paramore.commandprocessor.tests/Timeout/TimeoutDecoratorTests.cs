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
using Machine.Specifications;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.Timeout.TestDoubles;

namespace paramore.commandprocessor.tests.Timeout
{
    [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_failing_a_timeout_policy_check
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static AggregateException thrownException;

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //Handler is decorated with UsePolicy and fails with divide by zero error
            container.Register<IHandleRequests<MyCommand>, MyFailsDueToTimeoutHandler>().AsMultiInstance();
            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

            MyFailsDueToTimeoutHandler.WasCancelled = false;
            MyFailsDueToTimeoutHandler.TaskCompleted = false;
        };

        //We have to catch the final exception that bubbles out after retry
        Because of = () => thrownException = (AggregateException)Catch.Exception(() => commandProcessor.Send(myCommand));

        It should_throw_a_timeout_exception = () => thrownException.Flatten().InnerExceptions.First().ShouldBeOfExactType<TimeoutException>() ;
        It should_signal_that_a_timeout_occured_and_handler_should_be_cancelled = () => MyFailsDueToTimeoutHandler.WasCancelled.ShouldBeTrue();
        It should_not_run_to_completion = () => MyFailsDueToTimeoutHandler.TaskCompleted.ShouldBeFalse();
    }

    [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_passing_a_timeout_policy_check
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //Handler is decorated with UsePolicy and fails with divide by zero error
            container.Register<IHandleRequests<MyCommand>, MyPassesTimeoutHandler>().AsMultiInstance();
            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

        };

        //We have to catch the final exception that bubbles out after retry
        Because of = () =>  commandProcessor.Send(myCommand);

        It should_complete_the_command_before_an_exception = () => MyPassesTimeoutHandler.ShouldRecieve(myCommand);
    }


    //TODO: Combine Timeout and Exception Policy in one pipeline
}
