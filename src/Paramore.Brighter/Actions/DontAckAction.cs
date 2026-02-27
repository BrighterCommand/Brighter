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

namespace Paramore.Brighter.Actions;

/// <summary>
/// Thrown to indicate that a message should not be acknowledged. The message remains on the channel and will be
/// presented again on the next pump iteration. Call <c>throw new DontAckAction()</c> from within a
/// <see cref="RequestHandler{TRequest}"/> or <see cref="RequestHandlerAsync{TRequest}"/> to prevent acknowledgment.
/// </summary>
/// <remarks>
/// Unlike <see cref="DeferMessageAction"/> which requeues the message, and <see cref="RejectMessageAction"/> which
/// moves the message to a dead letter queue, <see cref="DontAckAction"/> leaves the message completely unacknowledged
/// on the channel. The message will be re-delivered by the transport after its visibility timeout expires.
/// A configurable delay on the pump prevents tight-loop CPU burn. The unacceptable message count is incremented,
/// allowing the pump to eventually stop if a limit is configured.
/// </remarks>
public class DontAckAction : Exception
{
    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should not be acknowledged.
    /// </summary>
    public DontAckAction() {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should not be acknowledged.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should not be acknowledged</param>
    public DontAckAction(string? reason) : base(reason) {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should not be acknowledged.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should not be acknowledged</param>
    /// <param name="innerException">The exception that led to not acknowledging the <see cref="Message"/></param>
    public DontAckAction(string? reason, Exception? innerException) : base(reason, innerException) {}
}
