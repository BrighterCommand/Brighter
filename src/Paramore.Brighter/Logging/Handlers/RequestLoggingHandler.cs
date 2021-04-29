﻿#region Licence
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
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Logging.Handlers
{
    /// <summary>
    /// Class RequestLoggingHandler.
    /// Logs a request to a <see cref="IHandleRequests"/> handler using the Common.Logging logger registered with the <see cref="CommandProcessor"/>
    /// The log shows the original <see cref="IRequest"/> properties as well as the timer handling.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public class RequestLoggingHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<RequestLoggingHandler<TRequest>>();

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
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override TRequest Handle(TRequest command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        /// <summary>
        /// If a request cannot be completed by <see cref="RequestHandler{TRequest}.Handle"/>, implementing the <see cref="RequestHandler{TRequest}.Fallback"/> method provides an alternate code path that can be used
        /// This allows for graceful  degradation. Using the <see cref="FallbackPolicyAttribute"/> handler you can configure a policy to catch either all <see cref="Exception"/>'s or
        /// just <see cref="BrokenCircuitException"/> that occur later in the pipeline, and then call the <see cref="RequestHandler{TRequest}.Fallback"/> path.
        /// Note that the <see cref="FallbackPolicyAttribute"/> target handler might be 'beginning of chain' and need to pass through to actual handler that is end of chain.
        /// Because of this we need to call Fallback on the chain. Later step handlers don't know the context of failure so they cannot know if any operations they had, 
        /// that could fail (such as DB access) were the cause of the failure chain being hit.
        /// Steps that don't know how to handle should pass through.
        /// Useful alternatives for Fallback are to try via the cache.
        /// Note that a Fallback handler implementation should not catch exceptions in the <see cref="RequestHandler{TRequest}.Fallback"/> chain to avoid an infinite loop.
        /// Call <see cref="RequestHandler{TRequest}.Successor"/>.<see cref="RequestHandler{TRequest}.Handle"/> if having provided a Fallback you want the chain to return to the 'happy' path. Excerise caution here though
        /// as you do not know who generated the exception that caused the fallback chain.
        /// For this reason, the <see cref="FallbackPolicyHandler{TRequest}"/> puts the exception in the request context.
        /// When the <see cref="FallbackPolicyAttribute"/> is set on the <see cref="RequestHandler{TRequest}.Handle"/> method of a derived class
        /// The <see cref="FallbackPolicyHandler{TRequest}"/> will catch either all failures (backstop) or <see cref="BrokenCircuitException"/> depending on configuration
        /// and call the <see cref="RequestHandler{TRequest}"/>'s <see cref="RequestHandler{TRequest}.Fallback"/> method
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override TRequest Fallback(TRequest command)
        {
            LogFailure(command);
            return base.Fallback(command);
        }

        private void LogCommand(TRequest request)
        {
            s_logger.LogInformation("Logging handler pipeline call. Pipeline timing {HandlerTiming} target, for {RequestType} with values of {Request} at: {Time}", _timing.ToString(), typeof(TRequest), JsonSerializer.Serialize(request), DateTime.UtcNow);
        }

        private void LogFailure(TRequest request)
        {
            s_logger.LogInformation("Failure in pipeline call for {RequestType} with values of {Request} at: {Time}", typeof(TRequest), JsonSerializer.Serialize(request), DateTime.UtcNow);
        }
    }
}
