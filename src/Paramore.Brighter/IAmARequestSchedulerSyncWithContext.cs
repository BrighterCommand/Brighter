#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

namespace Paramore.Brighter;

/// <summary>
/// Schedules requests with a snapshot of their supported message metadata and trace context.
/// </summary>
/// <remarks>
/// Implement this optional interface to preserve context supplied to scheduled command processor calls.
/// Unrelated bag entries and runtime services are excluded; see <see cref="Scheduler.ScheduledRequestContext"/>.
/// </remarks>
public interface IAmARequestSchedulerSyncWithContext : IAmARequestSchedulerSync
{
    /// <summary>
    /// Schedules a request with its supported message metadata and trace context.
    /// </summary>
    /// <typeparam name="TRequest">The reference type implementing <see cref="IRequest"/> to schedule.</typeparam>
    /// <param name="request">The request to schedule.</param>
    /// <param name="type">The operation to execute.</param>
    /// <param name="at">When to execute the request.</param>
    /// <param name="requestContext">The context whose supported metadata is captured when scheduling.</param>
    /// <returns>The scheduler identifier.</returns>
    string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        IRequestContext? requestContext)
        where TRequest : class, IRequest;

    /// <summary>
    /// Schedules a request with its supported message metadata and trace context.
    /// </summary>
    /// <typeparam name="TRequest">The reference type implementing <see cref="IRequest"/> to schedule.</typeparam>
    /// <param name="request">The request to schedule.</param>
    /// <param name="type">The operation to execute.</param>
    /// <param name="delay">When to execute the request.</param>
    /// <param name="requestContext">The context whose supported metadata is captured when scheduling.</param>
    /// <returns>The scheduler identifier.</returns>
    string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        IRequestContext? requestContext)
        where TRequest : class, IRequest;
}
