using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Logging.Attributes;

namespace Paramore.Brighter.Core.Tests.Logging.TestDoubles
{
    internal class MyLoggedHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            return base.HandleAsync(command, cancellationToken);
        }
    }
}
