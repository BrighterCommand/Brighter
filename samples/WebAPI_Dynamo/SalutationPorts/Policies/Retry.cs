using System;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace SalutationPorts.Policies
{
    public static class Retry
    {
        public const string RETRYPOLICY = "SalutationPorts.Policies.RetryPolicy";
        public const string EXPONENTIAL_RETRYPOLICY = "SalutationPorts.Policies.ExponenttialRetryPolicy";

        public static RetryPolicy GetSimpleHandlerRetryPolicy()
        {
            return Policy.Handle<Exception>().WaitAndRetry(new[]
            {
                TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
            });
        }

        public static RetryPolicy GetExponentialHandlerRetryPolicy()
        {
            var delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(100), retryCount: 5, fastFirst: true);
            return Policy.Handle<Exception>().WaitAndRetry(delay);
        }
    }
}
