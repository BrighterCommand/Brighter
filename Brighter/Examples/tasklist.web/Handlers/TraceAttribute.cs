using System;
using paramore.commandprocessor;

namespace tasklist.web.Handlers
{
    public class TraceAttribute : RequestHandlerAttribute
    {
        public TraceAttribute(int step, HandlerTiming timing) 
            : base(step, timing)
        { }

        public override Type GetHandlerType()
        {
            return typeof (TraceHandler<>);
        }
    }
}