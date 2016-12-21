using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.logging.Attributes;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.Logging.TestDoubles
{
    class MyLoggedHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        [RequestLoggingAsync(step:0, timing: HandlerTiming.Before)]
        public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken? ct = null)
        {
            return base.HandleAsync(command, ct);
        }
    }
}
