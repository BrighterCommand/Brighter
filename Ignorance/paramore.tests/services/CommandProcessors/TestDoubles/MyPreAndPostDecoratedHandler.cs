using Paramore.Services.CommandHandlers;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyPreAndPostDecoratedHandler : RequestHandler<MyCommand>
    {
        [MyPreValidationHandlerAttribute(step: 2, timing: HandlerTiming.Before)]
        [MyPostLoggingHandlerAttribute(step: 1, timing: HandlerTiming.After)]
        public override MyCommand Handle(MyCommand command)
        {
            return base.Handle(command);
        }
    }
}