using System;
using System.Collections.Generic;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace GreetingsApp.Policies;

public static class Retry
{
    public const string RETRYPOLICYASYNC = "GreetingsPorts.Policies.RetryPolicyAsync";
    public const string EXPONENTIAL_RETRYPOLICYASYNC = "GreetingsPorts.Policies.ExponenttialRetryPolicyAsync";

    public static AsyncRetryPolicy GetSimpleHandlerRetryPolicy()
    {
        return Policy.Handle<Exception>().WaitAndRetryAsync(
        [
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
        ]);
    }

    public static AsyncRetryPolicy GetExponentialHandlerRetryPolicy()
    {
        IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(100), 5, fastFirst: true);
        return Policy.Handle<Exception>().WaitAndRetryAsync(delay);
    }

    public static AsyncRetryPolicy GetDefaultRetryPolicy()
    {
        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(
            [
                TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
            ]);
    }

    public static AsyncCircuitBreakerPolicy GetDefaultCircuitBreakerPolicy()
    {
        return Policy.Handle<Exception>().CircuitBreakerAsync(
            1, TimeSpan.FromMilliseconds(500)
        );
    }
}
