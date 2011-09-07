using Paramore.Services.CommandHandlers;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyCommandHandler : RequestHandler<MyCommand>
    {
        private static MyCommand command;

        public MyCommandHandler()
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