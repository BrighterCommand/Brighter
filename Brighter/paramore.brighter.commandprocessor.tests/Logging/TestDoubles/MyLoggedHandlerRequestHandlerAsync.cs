using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.logging.Attributes;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.Logging.TestDoubles
{
    class MyLoggedHandlerRequestHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        public MyLoggedHandlerRequestHandlerAsync(ILog logger)
            : base(logger)
        {}

        [RequestLoggingAsync(step:0, timing: HandlerTiming.Before)]
        public override Task<MyCommand> HandleAsync(MyCommand command)
        {
            return base.HandleAsync(command);
        }
    }
}
