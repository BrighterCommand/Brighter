using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Tests.Logging.TestDoubles
{
    class MyLoggedHandler : RequestHandler<MyCommand>
    {
        [RequestLogging(0, HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand command)
        {
            return base.Handle(command);
        }
    }
}
