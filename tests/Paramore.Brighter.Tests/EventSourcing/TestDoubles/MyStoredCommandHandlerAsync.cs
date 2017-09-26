using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Eventsourcing.Attributes;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Tests.EventSourcing.TestDoubles
{
    internal class MyStoredCommandHandlerAsync : RequestHandlerAsync<MyCommand> 
    {
        [UseCommandSourcingAsync(1, onceOnly: true, timing:HandlerTiming.Before)]
        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}