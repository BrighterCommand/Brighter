using Common.Logging;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyCommandHandler : RequestHandler<MyCommand>
    {
        private static MyCommand command;

        public MyCommandHandler(ILog logger)
            :base(logger)
        {
            command = null;
        }

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