using System;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyPostLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public MyPostLoggingHandlerAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyLoggingHandler<>);
        }
    }
}