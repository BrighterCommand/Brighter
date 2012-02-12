using System;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public MyLoggingHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyLoggingHandler<>);
        }
    }
}