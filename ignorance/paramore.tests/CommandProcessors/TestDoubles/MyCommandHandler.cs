using Paramore.Services.CommandHandlers;

namespace Paramore.Tests.CommandProcessors.TestDoubles
{
    internal class MyCommandHandler : RequestHandler<MyCommand>
    {
        private static MyCommand _command;

        public override MyCommand  Handle(MyCommand request)
        {
            LogCommand(request);
            return base.Handle(request);
        }

        public static void SetUp()
        {
            _command = null;
        }

        public static bool ShouldRecieve(MyCommand expectedCommand)
        {
            return (_command != null) && (expectedCommand.Id == _command.Id);
        }

        private void LogCommand(MyCommand request)
        {
            _command = request;
        }
    }
}