using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal class MyCommandHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        private readonly IDictionary<string, Guid> _receivedMessages;

        public MyCommandHandlerAsync(IDictionary<string, Guid> receivedMessages)
        {
            _receivedMessages = receivedMessages;
        }

        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            _receivedMessages.Add(nameof(MyCommandHandlerAsync), command.Id);
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
