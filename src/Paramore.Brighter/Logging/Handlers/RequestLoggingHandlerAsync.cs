// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 2016-01-07
//
// Last Modified By : ian
// Last Modified On : 2016-01-07
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2016 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Newtonsoft.Json;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Logging.Handlers
{
    /// <summary>
    /// Class AsyncRequestLoggingHandler.
    /// Logs a request to a <see cref="IHandleRequestsAsync"/> handler using the Common.Logging logger registered with the <see cref="CommandProcessor"/>
    /// The log shows the original <see cref="IRequest"/> properties as well as the timer handling.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public class RequestLoggingHandlerAsync<TRequest> : RequestHandlerAsync<TRequest> where TRequest : class, IRequest
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RequestLoggingHandlerAsync<TRequest>>);

        private HandlerTiming _timing;

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            _timing = (HandlerTiming)initializerList[0];
        }

        /// <summary>
        /// Awaitably handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request. Optional.</param>
        /// <returns>Awaitable <see cref="Task{TRequest}"/>.</returns>
        public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default(CancellationToken))
        {
            LogCommand(command);
            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        /// If a request cannot be completed by <see cref="RequestHandlerAsync{TRequest}.HandleAsync"/>, implementing the <see cref="RequestHandlerAsync{TRequest}.FallbackAsync"/> method provides an alternate code path that can be used
        /// This allows for graceful  degradation. Using the <see cref="AsyncFallbackPolicyAttribute"/> handler you can configure a policy to catch either all <see cref="Exception"/>'s or
        /// just <see cref="BrokenCircuitException"/> that occur later in the pipeline, and then call the <see cref="RequestHandler{TRequest}.Fallback"/> path.
        /// Note that the <see cref="AsyncFallbackPolicyAttribute"/> target handler might be 'beginning of chain' and need to pass through to actual handler that is end of chain.
        /// Because of this we need to call Fallback on the chain. Later step handlers don't know the context of failure so they cannot know if any operations they had, 
        /// that could fail (such as DB access) were the cause of the failure chain being hit.
        /// Steps that don't know how to handle should pass through.
        /// Useful alternatives for Fallback are to try via the cache.
        /// Note that a Fallback handler implementation should not catch exceptions in the <see cref="RequestHandler{TRequest}.Fallback"/> chain to avoid an infinite loop.
        /// Call <see cref="RequestHandlerAsync{TRequest}.Successor"/>.<see cref="RequestHandlerAsync{TRequest}.HandleAsync"/> if having provided a Fallback you want the chain to return to the 'happy' path. Excerise caution here though
        /// as you do not know who generated the exception that caused the fallback chain.
        /// For this reason, the <see cref="AsyncFallbackPolicyHandler{TRequest}"/> puts the exception in the request context.
        /// When the <see cref="FallbackPolicyAttribute"/> is set on the <see cref="RequestHandler{TRequest}.Handle"/> method of a derived class
        /// The <see cref="AsyncFallbackPolicyHandler{TRequest}"/> will catch either all failures (backstop) or <see cref="AsyncBrokenCircuitException"/> depending on configuration
        /// and call the <see cref="RequestHandlerAsync{TRequest}"/>'s <see cref="RequestHandlerAsync{TRequest}.FallbackAsync"/> method
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request. Optional.</param>
        /// <returns>TRequest.</returns>
        public override async Task<TRequest> FallbackAsync(TRequest command, CancellationToken cancellationToken = default(CancellationToken))
        {
            LogFailure(command);
            return await base.FallbackAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        private void LogCommand(TRequest request)
        {
            //TODO: LibLog has no async support, so remains a blocking call for now
            _logger.Value.InfoFormat("Logging handler pipeline call. Pipeline timing {0} target, for {1} with values of {2} at: {3}", _timing.ToString(), typeof(TRequest), JsonConvert.SerializeObject(request), DateTime.UtcNow);
        }

        private void LogFailure(TRequest request)
        {
            //TODO: LibLog has no async support, so remains a blocking call for now
            _logger.Value.InfoFormat("Failure in pipeline call for {0} with values of {1} at: {2}", typeof(TRequest), JsonConvert.SerializeObject(request), DateTime.UtcNow);
        }
    }
}
