using System;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
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