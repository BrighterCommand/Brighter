using System;
using System.Collections.Generic;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace SalutationApp.Policies;

public static class Retry
{
    public const string RETRYPOLICYASYNC = "SalutationPorts.Policies.RetryPolicy";
    public const string EXPONENTIAL_RETRYPOLICYASYNC = "SalutationPorts.Policies.ExponenttialRetryPolicy";

    public static AsyncRetryPolicy GetSimpleHandlerRetryPolicyAsync()
    {
        return Policy.Handle<Exception>().WaitAndRetryAsync(
        [
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
        ]);
    }

    public static AsyncRetryPolicy GetExponentialHandlerRetryPolicyAsync()
    {
        IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(100), 5, fastFirst: true);
        return Policy.Handle<Exception>().WaitAndRetryAsync(delay);
    }
}
