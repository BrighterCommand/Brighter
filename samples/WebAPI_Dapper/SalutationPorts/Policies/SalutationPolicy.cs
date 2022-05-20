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
            Add(Retry.RETRYPOLICYASYNC, Retry.GetSimpleHandlerRetryPolicy());
            Add(Retry.EXPONENTIAL_RETRYPOLICYASYNC, Retry.GetExponentialHandlerRetryPolicy());
        }
    }
}
