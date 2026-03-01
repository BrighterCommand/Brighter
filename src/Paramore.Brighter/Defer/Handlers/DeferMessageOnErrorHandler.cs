#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Defer.Handlers;

/// <summary>
/// Handler that catches unhandled exceptions and converts them to <see cref="DeferMessageAction"/>.
/// When used with a message pump (Reactor or Proactor), this causes the message to be requeued with a delay.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <remarks>
/// This handler should be positioned at the outermost layer of the pipeline (lowest step number)
/// to act as a backstop for any exceptions that escape inner handlers.
/// </remarks>
public partial class DeferMessageOnErrorHandler<TRequest> : RequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<DeferMessageOnErrorHandler<TRequest>>();
    private int _delayMilliseconds;

    /// <summary>
    /// Initializes from attribute parameters.
    /// </summary>
    /// <param name="initializerList">The initializer list. First element is the delay in milliseconds.</param>
    public override void InitializeFromAttributeParams(params object?[] initializerList)
    {
        _delayMilliseconds = (int?)initializerList[0] ?? 0;
    }

    /// <summary>
    /// Handles the request by passing it to the next handler in the pipeline.
    /// If any exception occurs in the pipeline, it is caught and converted to a <see cref="DeferMessageAction"/>.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <returns>The request after processing.</returns>
    /// <exception cref="DeferMessageAction">
    /// Thrown when any exception occurs in the pipeline. The original exception is preserved as <see cref="Exception.InnerException"/>.
    /// </exception>
    public override TRequest Handle(TRequest request)
    {
        try
        {
            return base.Handle(request);
        }
        catch (Exception ex)
        {
            Log.UnhandledExceptionDeferringMessage(s_logger, ex, typeof(TRequest).Name, ex.Message);
            throw new DeferMessageAction(ex.Message, ex, _delayMilliseconds);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Unhandled exception caught by backstop error handler, deferring message for request {RequestType}: {ExceptionMessage}")]
        public static partial void UnhandledExceptionDeferringMessage(ILogger logger, Exception ex, string requestType, string exceptionMessage);
    }
}
