using System;
using paramore.commandprocessor.exceptionpolicy.Handlers;

namespace paramore.commandprocessor.exceptionpolicy.Attributes
{
    public class UsePolicyAttribute : RequestHandlerAttribute
    {
        private readonly string policy;

        public UsePolicyAttribute(string policy, int step) : base(step, HandlerTiming.Before)
        {
            this.policy = policy;
        }

        public override object[] InitializerParams()
        {
            return new object[] {policy};
        }

        public override Type GetHandlerType()
        {
            return typeof (PolicyHandler<>);
        }
    }
}
