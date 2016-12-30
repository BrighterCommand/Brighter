using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles
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
