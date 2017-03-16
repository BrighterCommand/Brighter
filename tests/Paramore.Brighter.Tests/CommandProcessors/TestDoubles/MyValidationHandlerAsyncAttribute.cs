using System;

namespace Paramore.Brighter.Tests.TestDoubles
{
    class MyValidationHandlerAsyncAttribute : RequestHandlerAttribute
    {
        public MyValidationHandlerAsyncAttribute (int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandlerAsync<>);
        }
    }
}
