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
            Add(Retry.RETRYPOLICY, Retry.GetSimpleHandlerRetryPolicy());
            Add(Retry.EXPONENTIAL_RETRYPOLICY, Retry.GetExponentialHandlerRetryPolicy());
        }
    }
}
