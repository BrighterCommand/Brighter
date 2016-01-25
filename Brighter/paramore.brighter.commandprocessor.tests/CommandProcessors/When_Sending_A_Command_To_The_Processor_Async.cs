using FakeItEasy;
using Machine.Specifications;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(CommandProcessor))]
    public class When_Sending_A_Command_To_The_Processor_Async
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            var handlerFactory = new TestHandlerFactoryAsync<MyCommand, MyCommandHandlerAsync>(() => new MyCommandHandlerAsync(logger));

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        //Ignore any errors about adding System.Runtime from the IDE. See https://social.msdn.microsoft.com/Forums/en-US/af4dc0db-046c-4728-bfe0-60ceb93f7b9f/vs2012net-45-rc-compiler-error-when-using-actionblock-missing-reference-to?forum=tpldataflow
        private Because _of = () => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_myCommand));

        private It _should_send_the_command_to_the_command_handler = () => MyCommandHandlerAsync.ShouldReceive(s_myCommand).ShouldBeTrue();
    }
}
