using System;
using System.Linq;
using Machine.Specifications;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;
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
}
