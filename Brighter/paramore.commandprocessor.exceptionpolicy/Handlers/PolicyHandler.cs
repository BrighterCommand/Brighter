using System;
using Polly;

namespace paramore.commandprocessor.exceptionpolicy.Handlers
{
    class PolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private Policy policy;

        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            //we expect the first and only parameter to be a string
            var policyName = (string) initializerList[0];
            policy = Context.Container.GetInstance<Policy>(policyName);
            if (policy == null)
                throw new ArgumentException("Could not find the policy for this attribute, did you register it with the command processor's container", "initializerList");
        }

        public override TRequest Handle(TRequest command)
        {
            return policy.Execute(() => base.Handle(command));
        }
    }
}
