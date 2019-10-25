using System;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
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
