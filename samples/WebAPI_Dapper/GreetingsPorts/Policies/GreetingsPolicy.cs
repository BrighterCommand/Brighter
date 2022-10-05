using Paramore.Brighter;

namespace GreetingsPorts.Policies
{
    public class GreetingsPolicy : DefaultPolicy 
    {
        public GreetingsPolicy()
        {
            AddGreetingsPolicies();
        }

        private void AddGreetingsPolicies()
        {
            Add(Retry.RETRYPOLICYASYNC, Retry.GetSimpleHandlerRetryPolicy());
            Add(Retry.EXPONENTIAL_RETRYPOLICYASYNC, Retry.GetExponentialHandlerRetryPolicy());
        }
    }
}
