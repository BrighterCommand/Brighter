using Paramore.Services.CommandHandlers;

namespace Paramore.Tests.CommandProcessors.TestDoubles
{
    internal class MyDoubleDecoratedHandler : RequestHandler<MyCommand>
    {
        [MyValidationHandler(step:2)]
        [MyLoggingHandler(step:1)]
        public override MyCommand Handle(MyCommand request)
        {
            return base.Handle(request);
        }
    }
}