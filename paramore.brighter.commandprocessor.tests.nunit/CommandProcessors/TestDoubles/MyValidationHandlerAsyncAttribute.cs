using System;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles
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
