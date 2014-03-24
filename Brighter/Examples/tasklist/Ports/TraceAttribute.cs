using System;
using Tasklist.Ports.Handlers;
using paramore.brighter.commandprocessor;

namespace Tasklist.Ports
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