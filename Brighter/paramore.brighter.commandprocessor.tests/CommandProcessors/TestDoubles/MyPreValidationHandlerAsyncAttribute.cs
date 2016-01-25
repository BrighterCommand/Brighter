using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    class MyPreValidationHandlerAsyncAttribute : RequestHandlerAttribute
    {
        public MyPreValidationHandlerAsyncAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandlerAsync<>);
        }
    }
}
