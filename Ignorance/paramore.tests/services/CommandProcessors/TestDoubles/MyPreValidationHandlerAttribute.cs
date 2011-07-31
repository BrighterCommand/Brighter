using System;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandProcessors;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyPreValidationHandlerAttribute : RequestHandlerAttribute
    {
        public MyPreValidationHandlerAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandler<>);
        }
    }
}