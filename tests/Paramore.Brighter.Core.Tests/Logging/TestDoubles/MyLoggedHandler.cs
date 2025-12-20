using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Logging.Attributes;

namespace Paramore.Brighter.Core.Tests.Logging.TestDoubles
{
    sealed class MyLoggedHandler : RequestHandler<MyCommand>
    {
        [RequestLogging(0, HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand myCommand)
        {
            return base.Handle(myCommand);
        }
    }
}
