using System;
using paramore.commandprocessor.timeoutpolicy.Handlers;

namespace paramore.commandprocessor.timeoutpolicy.Attributes
{
    public class TimeoutPolicyAttribute : RequestHandlerAttribute
    {
        public TimeoutPolicyAttribute(int milliseconds, int step, HandlerTiming timing = HandlerTiming.Before) : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof (TimeoutPolicyHandler<>);
        }
    }
}