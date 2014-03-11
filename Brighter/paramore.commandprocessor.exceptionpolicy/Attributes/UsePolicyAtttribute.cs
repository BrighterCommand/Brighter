using System;
using paramore.commandprocessor.policy.Handlers;

namespace paramore.commandprocessor.policy.Attributes
{
    public class UsePolicyAttribute : RequestHandlerAttribute
    {
        private readonly string policy;

        public UsePolicyAttribute(string policy, int step) : base(step, HandlerTiming.Before)
        {
            this.policy = policy;
        }

        public override Type GetHandlerType()
        {
            return typeof (PolicyHandler<>);
        }
    }
}
