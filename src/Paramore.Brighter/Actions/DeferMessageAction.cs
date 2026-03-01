#region Licence
/* The MIT License (MIT)
Copyright © 2015 Toby Henderson

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

namespace Paramore.Brighter.Actions;

/// <summary>
/// Thrown to indicate that a message should be deferred.  Its purpose is to allow messages that cannot be processed
/// immediately to be delayed. Call `throw new DeferMessageAction()` from within a <see cref="RequestHandler{TRequest}"/>
/// or a <see cref="RequestHandlerAsync{TRequest}"/> to requeue the message with a delay.
/// </summary>
/// <remarks>How the delay works depends on whether the transport natively implements delay. If not, we rely on the
/// configuration of an <see cref="IAmARequestScheduler"/> or <see cref="IAmARequestSchedulerAsync"/>.
/// </remarks>
public class DeferMessageAction : Exception
{
    /// <summary>
    /// The delay before the message should be requeued. If <c>null</c>, the subscription's default
    /// <see cref="Subscription.RequeueDelay"/> is used.
    /// </summary>
    public TimeSpan? Delay { get; }

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be deferred.
    /// </summary>
    public DeferMessageAction() {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be deferred.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should be deferred</param>
    public DeferMessageAction(string? reason) : base(reason) {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be deferred.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should be deferred</param>
    /// <param name="innerException">The exception that led to deferral of the <see cref="Message"/></param>
    public DeferMessageAction(string? reason, Exception? innerException) : base(reason, innerException) {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be deferred with a specific delay.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should be deferred</param>
    /// <param name="innerException">The exception that led to deferral of the <see cref="Message"/></param>
    /// <param name="delayMilliseconds">The delay in milliseconds before requeuing. Zero means use subscription default.</param>
    public DeferMessageAction(string? reason, Exception? innerException, int delayMilliseconds)
        : base(reason, innerException)
    {
        Delay = delayMilliseconds > 0 ? TimeSpan.FromMilliseconds(delayMilliseconds) : null;
    }
}
