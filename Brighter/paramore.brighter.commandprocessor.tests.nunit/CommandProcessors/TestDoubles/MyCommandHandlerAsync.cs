using System.Threading;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles
{
    internal class MyCommandHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        private static MyCommand s_command;

        public MyCommandHandlerAsync()
        {
            s_command = null;
        }

        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken? ct = null)
        {
            LogCommand(command);
            return await base.HandleAsync(command, ct).ConfigureAwait(base.ContinueOnCapturedContext);
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
