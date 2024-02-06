﻿using System;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace SalutationPorts.Policies
{
    public static class Retry
    {
        public const string RETRYPOLICYASYNC = "SalutationPorts.Policies.RetryPolicyAsync";
        public const string EXPONENTIAL_RETRYPOLICY_ASYNC = "SalutationPorts.Policies.ExponenttialRetryPolicyAsync";

        public static AsyncRetryPolicy GetSimpleHandlerRetryPolicyAsync()
        {
            return Policy.Handle<Exception>().WaitAndRetryAsync(new[]
            {
                TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)
            });
        }

        public static AsyncRetryPolicy GetExponentialHandlerRetryPolicyAsync()
        {
            var delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(100), retryCount: 5, fastFirst: true);
            return Policy.Handle<Exception>().WaitAndRetryAsync(delay);
        }
    }
}
