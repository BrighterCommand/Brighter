using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.eventsourcing.Attributes;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.EventSourcing.TestDoubles
{
    internal class MyStoredCommandHandlerAsync : RequestHandlerAsync<MyCommand> 
    {
        private readonly ILog _logger;

        public MyStoredCommandHandlerAsync(ILog logger)
            : base(logger)
        {
            _logger = logger;
        }

        [UseAsyncCommandSourcing(step: 1, timing: HandlerTiming.Before)]
        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken? ct = null)
        {
            return await base.HandleAsync(command, ct);
        }

    }
}