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

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;
using Polly.CircuitBreaker;

namespace paramore.brighter.commandprocessor.policy.Handlers
{
    public class FallbackPolicyHandlerRequestHandlerAsync<TRequest> : RequestHandlerAsync<TRequest> where TRequest : class, IRequest
    {
        /// <summary>
        /// The Key to the <see cref="IHandleRequests{TRequest}.Context"/> bag item that contains the exception initiating the fallback
        /// </summary>
        public const string CAUSE_OF_FALLBACK_EXCEPTION = "Fallback_Exception_Cause";

        private Func<TRequest, CancellationToken?, Task<TRequest>> _exceptionHandlerFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        public FallbackPolicyHandlerRequestHandlerAsync() 
            : this(LogProvider.GetCurrentClassLogger()) 
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// Use this if you need to inject a logger, for testing
        /// </summary>
        /// <param name="logger">The logger.</param>
        public FallbackPolicyHandlerRequestHandlerAsync(ILog logger) : base(logger) {}

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            bool catchAll = Convert.ToBoolean(initializerList[0]);
            bool catchBrokenCircuit = Convert.ToBoolean(initializerList[1]);

            if (catchBrokenCircuit == true)
            {
                _exceptionHandlerFunc = CatchBrokenCircuit;
            }
            else if (catchAll == true)
            {
                _exceptionHandlerFunc = CatchAll;
            }
            else
            {
                _exceptionHandlerFunc = CatchNone;
            }
        }

        /// <summary>
        /// Catches any <see cref="Exception"/>s occurring in the pipeline and calls <see cref="IHandleRequests{TRequest}.Fallback"/> to allow handlers in the chain a chance to provide a fallback on failure
        /// The original exception is stored in the <see cref="IHandleRequests{TRequest}.Context"/> under the key <see cref="FallbackPolicyHandler{TRequest}.CAUSE_OF_FALLBACK_EXCEPTION"/> for probing
        /// by handlers in the pipeline called on fallback
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="ct">Allow the sender to cancel the request</param>
        /// <returns>TRequest.</returns>
        public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken? ct = null)
        {
            return await _exceptionHandlerFunc(command, ct);
        }

        private async Task<TRequest> CatchAll(TRequest command, CancellationToken? ct = null)
        {
            try
            {
                return await base.HandleAsync(command, ct).ConfigureAwait(base.ContinueOnCapturedContext);
            }
            catch (Exception exception)
            {
                Context.Bag.Add(CAUSE_OF_FALLBACK_EXCEPTION, exception);
            }
            return await base.FallbackAsync(command, ct).ConfigureAwait(base.ContinueOnCapturedContext);
        }

        private async Task<TRequest> CatchBrokenCircuit(TRequest command, CancellationToken? ct = null)
        {
            try
            {
                return await base.HandleAsync(command, ct).ConfigureAwait(base.ContinueOnCapturedContext);
            }
            catch (BrokenCircuitException brokenCircuitExceptionexception)
            {
                Context.Bag.Add(CAUSE_OF_FALLBACK_EXCEPTION, brokenCircuitExceptionexception);
            }
            return await base.FallbackAsync(command, ct).ConfigureAwait(base.ContinueOnCapturedContext);
        }

        private async Task<TRequest> CatchNone(TRequest command, CancellationToken? ct = null)
        {
            var tcs = new TaskCompletionSource<TRequest>();
            if (ct.HasValue && ct.Value.IsCancellationRequested)
            {
                tcs.SetCanceled();
            }
            else
            {
                tcs.SetResult(command);
                
            }
            return await tcs.Task.ConfigureAwait(base.ContinueOnCapturedContext);
        }
    }

}
