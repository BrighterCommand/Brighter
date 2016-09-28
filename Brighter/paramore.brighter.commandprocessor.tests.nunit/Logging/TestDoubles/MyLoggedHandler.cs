using paramore.brighter.commandprocessor.logging.Attributes;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.Logging.TestDoubles
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
