using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal class MyObsoleteCommandHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        private static MyCommand s_command;

        public MyObsoleteCommandHandlerAsync()
        {
            s_command = null;
        }

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        [MyPreValidationHandlerAsync(2, HandlerTiming.Before)]
        [MyPostLoggingHandlerAsync(1, HandlerTiming.After)]
        [Obsolete] // even with attributes non inheriting from MessageHandlerDecoratorAttribute it should not fail
        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return command;
            }

            LogCommand(command);
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

        public static bool Shouldreceive(MyCommand expectedCommand)
        {
            return s_command != null && expectedCommand.Id == s_command.Id;
        }

        private void LogCommand(MyCommand request)
        {
            s_command = request;
        }
    }
}
