using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.logging.Attributes;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.Logging.TestDoubles
{
    class MyLoggedHandler : RequestHandler<MyCommand>
    {
        public MyLoggedHandler(ILog logger)
            : base(logger)
        {}

        [RequestLogging(step:0, timing: HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand command)
        {
            return base.Handle(command);
        }
    }
}
