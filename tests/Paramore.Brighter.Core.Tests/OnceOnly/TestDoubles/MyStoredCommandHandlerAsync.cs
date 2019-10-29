using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox.Attributes;

namespace Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles
{
    internal class MyStoredCommandHandlerAsync : RequestHandlerAsync<MyCommand> 
    {
        [UseInboxAsync(1, onceOnly: true, contextKey: typeof(MyStoredCommandHandlerAsync), timing:HandlerTiming.Before)]
        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }

    internal class MyStoredCommandToFailHandlerAsync : RequestHandlerAsync<MyCommandToFail> 
    {
        [UseInboxAsync(1, onceOnly: true, contextKey: typeof(MyStoredCommandToFailHandlerAsync), timing:HandlerTiming.Before)]
        public override async Task<MyCommandToFail> HandleAsync(MyCommandToFail command, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Delay(0, cancellationToken);
            throw new NotImplementedException();
        }
    }
}
