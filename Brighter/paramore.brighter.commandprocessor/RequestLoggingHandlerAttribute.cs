using System;

namespace paramore.brighter.commandprocessor
{
    public class RequestLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public RequestLoggingHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(RequestLoggingHandler<>);
        }
    }}
