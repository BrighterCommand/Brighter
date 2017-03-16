using System;

namespace Paramore.Brighter.Tests.TestDoubles
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
