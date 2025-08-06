#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Policies.Attributes;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Policies.Handlers
{
    /// <summary>
    /// Class ExceptionPolicyHandlerAsync.
    /// The <see cref="UsePolicyAttribute" /> supports the use of <a href="https://github.com/App-vNext/Polly">Polly</a> to provide quality of service around exceptions
    /// thrown from subsequent steps in the handler pipeline. A Polly Policy can be used to support a Retry or Circuit Breaker approach to exception handling
    /// Policies used by the attribute are identified by a string based key, which is used as a lookup into an <see cref="PolicyRegistry" /> and it is
    /// assumed that you have registered required policies with a Policy Registry such as <see cref="PolicyRegistry" /> and configured that as a
    /// dependency of the <see cref="CommandProcessor" /> using the <see cref="CommandProcessorBuilder" />
    /// The ExceptionPolicyHandler is instantiated by the pipeline when the <see cref="UsePolicyAttribute" /> is added to the <see cref="IHandleRequests{T}.Handle" /> method
    /// of the target handler implemented by the client.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public class ExceptionPolicyHandlerAsync<TRequest> : RequestHandlerAsync<TRequest> where TRequest : class, IRequest
    {
        private bool _initialized = false;
        private readonly List<AsyncPolicy> _policies = new();
        
        /// <summary>
        /// Initializes from attribute parameters. This will get the <see cref="PolicyRegistry" /> from the <see cref="IRequestContext" /> and query it for the
        /// policy identified in <see cref="UsePolicyAttribute" />
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        /// <exception cref="System.ArgumentException">Could not find the policy for this attribute, did you register it with the command processor's container;initializerList</exception>
        public override void InitializeFromAttributeParams(params object?[] initializerList)
        {
            if (_initialized) return;

            var policies = (List<string>?)initializerList[0] ?? [];
#pragma warning disable CS0618 // Type or member is obsolete
            policies.Each(p => _policies.Add(Context!.Policies!.Get<AsyncPolicy>(p)));
#pragma warning restore CS0618 // Type or member is obsolete
            _initialized = true;
        }

        /// <summary>
        /// Handles the specified command asynchronously.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Allow the sender to cancel the reques (optional) </param>
        /// <returns>AA Task<TRequest> that wraps the asynchronous call to the policy, which itself wraps the handler chain</TRequest></returns>
        public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
        {
            if (_policies.Count == 1)
            {
                return await _policies[0].ExecuteAsync(async () => await base.HandleAsync(command, cancellationToken))
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            else
            {
                var policyWrap = _policies[0].WrapAsync(_policies[1]);
                if (_policies.Count <= 2) return await policyWrap.ExecuteAsync(async () => await base.HandleAsync(command, cancellationToken));
                
                //we have more than two policies, so we need to wrap them
                for (int i = 2; i < _policies.Count; i++)
                {
                    policyWrap = policyWrap.WrapAsync(_policies[i]);
                }
                return await policyWrap.ExecuteAsync(async () => await base.HandleAsync(command,cancellationToken));
            }
        }
    }
}
