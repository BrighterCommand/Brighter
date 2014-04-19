using System;

namespace paramore.brighter.commandprocessor
{
    public class RequestLoggingAttribute : RequestHandlerAttribute
    {
        public RequestLoggingAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {}

        public override object[] InitializerParams()
        {
            return new object[] {Timing};
        }

        public override Type GetHandlerType()
        {
            return typeof(RequestLoggingHandler<>);
        }
    }}
