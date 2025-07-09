using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class MyCommandHandlerAsync(IDictionary<string, string> receivedMessages) : RequestHandlerAsync<MyCommand>
    {
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private MyCommand? _command = null;

        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
        {
            LogCommand(command);
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
        
        public  bool ShouldReceive(MyCommand expectedCommand)
        {
            return (_command != null) && (expectedCommand.Id == _command.Id);
        }
        
        private void LogCommand(MyCommand request)
        {
            _command = request;
            receivedMessages.Add(nameof(MyCommandHandlerAsync), request.Id);
        }
    }
}
