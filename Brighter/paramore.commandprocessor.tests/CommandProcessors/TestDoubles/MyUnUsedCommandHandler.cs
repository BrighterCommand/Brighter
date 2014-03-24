using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyUnusedCommandHandler : RequestHandler<MyCommand>
    {
        private static MyCommand command;

        public MyUnusedCommandHandler()
        {
            command = null;
        }

        [MyAbortingHandlerAttribute(step: 1, timing: HandlerTiming.Before)]
        public override MyCommand  Handle(MyCommand command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        public static bool ShouldRecieve(MyCommand expectedCommand)
        {
            return (command != null) && (expectedCommand.Id == command.Id);
        }

        private void LogCommand(MyCommand request)
        {
            command = request;
        }
    }
}