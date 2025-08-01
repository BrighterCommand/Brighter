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
using System.Threading;
using System.Threading.Tasks;
using Polly.CircuitBreaker;

namespace Paramore.Brighter.Policies.Handlers
{
    public class FallbackPolicyHandlerRequestHandlerAsync<TRequest> : RequestHandlerAsync<TRequest> where TRequest : class, IRequest
    {
        /// <summary>
        /// The Key to the <see cref="IHandleRequests{TRequest}.Context"/> bag item that contains the exception initiating the fallback
        /// </summary>
        public const string CAUSE_OF_FALLBACK_EXCEPTION = "Fallback_Exception_Cause";

        private Func<TRequest, CancellationToken, Task<TRequest>>? _exceptionHandlerFunc;

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object?[] initializerList)
        {
            var catchAll = Convert.ToBoolean(initializerList[0]);
            var catchBrokenCircuit = Convert.ToBoolean(initializerList[1]);

            if (catchBrokenCircuit)
            {
                _exceptionHandlerFunc = CatchBrokenCircuit;
            }
            else if (catchAll)
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
        /// <param name="cancellationToken">Allow the sender to cancel the request</param>
        /// <returns>TRequest.</returns>
        public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
        {
            if (_exceptionHandlerFunc is null)
                throw new ArgumentException("ExceptionHandler must be set before handling.");
            return await _exceptionHandlerFunc(command, cancellationToken);
        }

        private async Task<TRequest> CatchAll(TRequest command, CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (Exception exception)
            {
                Context?.Bag.AddOrUpdate(CAUSE_OF_FALLBACK_EXCEPTION, exception, (s, o) => exception);
            }
            return await FallbackAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        private async Task<TRequest> CatchBrokenCircuit(TRequest command, CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (BrokenCircuitException brokenCircuitException)
            {
                Context?.Bag.AddOrUpdate(CAUSE_OF_FALLBACK_EXCEPTION, brokenCircuitException, (s, o) => brokenCircuitException);
            }
            return await FallbackAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        private async Task<TRequest> CatchNone(TRequest command, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<TRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
            }
            else
            {
                tcs.SetResult(command);
            }

            return await tcs.Task.ConfigureAwait(ContinueOnCapturedContext);
        }
    }

}
