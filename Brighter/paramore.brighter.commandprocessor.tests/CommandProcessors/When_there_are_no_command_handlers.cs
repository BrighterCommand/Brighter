using System;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject("Commands should have at least one handler")]
    public class When_there_are_no_command_handlers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_commandProcessor = new CommandProcessor(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer()), new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private It _should_fail_because_multiple_receivers_found = () => s_exception.ShouldBeAssignableTo(typeof(ArgumentException));
        private It _should_have_an_error_message_that_tells_you_why = () => s_exception.ShouldContainErrorMessage("No command handler was found for the typeof command paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyCommand - a command should have exactly one handler.");
    }
}