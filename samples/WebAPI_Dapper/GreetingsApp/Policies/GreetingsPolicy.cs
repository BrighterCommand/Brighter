using Paramore.Brighter;
using Paramore.Darker.Policies;

namespace GreetingsPorts.Policies;

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
        Add(Constants.RetryPolicyName, Retry.GetDefaultRetryPolicy());
        Add(Constants.CircuitBreakerPolicyName, Retry.GetDefaultCircuitBreakerPolicy());
    }
}
