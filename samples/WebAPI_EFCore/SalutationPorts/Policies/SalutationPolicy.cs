using Paramore.Brighter;

namespace SalutationPorts.Policies
{
    public class SalutationPolicy : DefaultPolicy 
    {
        public SalutationPolicy()
        {
            AddSalutationPolicies();
        }

        private void AddSalutationPolicies()
        {
            Add(Retry.RETRYPOLICYASYNC, Retry.GetSimpleHandlerRetryPolicyAsync());
            Add(Retry.EXPONENTIAL_RETRYPOLICY_ASYNC, Retry.GetExponentialHandlerRetryPolicyAsync());
        }
    }
}
