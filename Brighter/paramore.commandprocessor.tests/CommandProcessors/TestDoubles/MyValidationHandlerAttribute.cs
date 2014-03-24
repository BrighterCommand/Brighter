using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyValidationHandlerAttribute : RequestHandlerAttribute
    {
        public MyValidationHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandler<>);
        }
    }
}