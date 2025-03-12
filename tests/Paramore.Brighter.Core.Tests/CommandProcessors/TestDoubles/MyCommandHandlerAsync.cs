using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class MyCommandHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        private readonly IDictionary<string, string> _receivedMessages;

        public MyCommandHandlerAsync(IDictionary<string, string> receivedMessages)
        {
            _receivedMessages = receivedMessages;
        }

        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
        {
            _receivedMessages.Add(nameof(MyCommandHandlerAsync), command.Id);
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
