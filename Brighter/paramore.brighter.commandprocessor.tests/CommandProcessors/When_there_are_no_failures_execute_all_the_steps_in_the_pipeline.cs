using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    public class When_there_are_no_failures_execute_all_the_steps_in_the_pipeline
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);
            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_call_the_pre_validation_handler = () => MyValidationHandler<MyCommand>.Shouldreceive(s_myCommand).ShouldBeTrue();
        private It _should_send_the_command_to_the_command_handler = () => MyPreAndPostDecoratedHandler.Shouldreceive(s_myCommand).ShouldBeTrue();
        private It _should_call_the_post_validation_handler = () => MyLoggingHandler<MyCommand>.Shouldreceive(s_myCommand).ShouldBeTrue();
    }
}