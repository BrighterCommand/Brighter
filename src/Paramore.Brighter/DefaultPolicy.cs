#region Licence

/* The MIT License (MIT)
Copyright © 2014 Toby Henderson

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.Policies.Attributes;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    ///     Class DefaultPolicy
    ///     We use <a href="">Polly</a> policies to provide Quality of Service.
    ///     By default we provide them for a
    ///     <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> and require you to
    ///     register policies with
    ///     The is a default policy which will retry once only. If you want better control over retries please use the
    ///     <see cref="PolicyRegistry" />
    ///     to respectively determine retry attempts for putting onto and popping off the queue and for breaking the circuit if
    ///     we cannot
    ///     You can register additional policies (or reuse these) to provide QoS for individual handlers. The
    ///     <see cref="UsePolicyAttribute" /> and <see cref="ExceptionPolicyHandler{TRequest}" />
    ///     provide an easy way to do this using the policies that you add to this registry
    ///     This is a default implementation of <see cref="PolicyRegistry" />
    /// </summary>
    public class DefaultPolicy : PolicyRegistry
    {
        public DefaultPolicy()
        {
            AddDefaultPolicies();
        }

        private void AddDefaultPolicies()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Add(CommandProcessor.CIRCUITBREAKER, Policy.Handle<Exception>().CircuitBreaker(10, new TimeSpan(5000)));
            Add(CommandProcessor.CIRCUITBREAKERASYNC,
                Policy.Handle<Exception>().CircuitBreakerAsync(10, new TimeSpan(5000)));
            Add(CommandProcessor.RETRYPOLICY,
                Policy.Handle<Exception>().WaitAndRetry(new[]
                {
                    TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                }));
            Add(CommandProcessor.RETRYPOLICYASYNC,
                Policy.Handle<Exception>().WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                }));
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
