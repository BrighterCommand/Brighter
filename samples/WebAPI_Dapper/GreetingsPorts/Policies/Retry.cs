using System;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace GreetingsPorts.Policies
{
    public static class Retry
    {
        public const string RETRYPOLICYASYNC = "GreetingsPorts.Policies.RetryPolicyAsync";
        public const string EXPONENTIAL_RETRYPOLICYASYNC = "GreetingsPorts.Policies.ExponenttialRetryPolicyAsync";

        public static AsyncRetryPolicy GetSimpleHandlerRetryPolicy()
        {
            return Policy.Handle<Exception>().WaitAndRetryAsync(new[]
            {
                TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
            });
        }

        public static AsyncRetryPolicy GetExponentialHandlerRetryPolicy()
        {
            var delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(100), retryCount: 5, fastFirst: true);
            return Policy.Handle<Exception>().WaitAndRetryAsync(delay);
        }
    }
}
