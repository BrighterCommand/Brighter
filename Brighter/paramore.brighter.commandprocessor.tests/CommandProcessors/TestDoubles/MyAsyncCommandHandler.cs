using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyAsyncCommandHandler : AsyncRequestHandler<MyCommand>
    {
        private static MyCommand s_command;

        public MyAsyncCommandHandler (ILog logger): base(logger)
        {
            s_command = null;
        }

        public override async Task<MyCommand> HandleAsync(MyCommand command)
        {
            LogCommand(command);
            return await base.HandleAsync(command);
        }

        public static bool ShouldReceive(MyCommand expectedCommand)
        {
            return (s_command != null) && (expectedCommand.Id == s_command.Id);
        }

        private void LogCommand(MyCommand request)
        {
            s_command = request;
        }
        
    }
}
