using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.eventsourcing.Attributes;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.EventSourcing.TestDoubles
{
    internal class MyStoredCommandHandlerRequestHandlerAsync : RequestHandlerAsync<MyCommand> 
    {
        private readonly ILog _logger;

        public MyStoredCommandHandlerRequestHandlerAsync(ILog logger)
            : base(logger)
        {
            _logger = logger;
        }

        [UseAsyncCommandSourcing(step: 1, timing: HandlerTiming.Before)]
        public override async Task<MyCommand> HandleAsync(MyCommand command)
        {
            return await base.HandleAsync(command);
        }

    }
}