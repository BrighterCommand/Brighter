using System;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    public class MyAbortingHandlerAttribute : RequestHandlerAttribute
    {
        public MyAbortingHandlerAttribute(int step, HandlerTiming timing) 
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof (MyAbortingHandler<>);
        }
    }
}
