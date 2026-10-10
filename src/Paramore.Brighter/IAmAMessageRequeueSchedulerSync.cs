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
/// An optional scheduler capability for retrying a message on a specific physical queue.
/// </summary>
/// <remarks>
/// Implementations must preserve the logical topic and message metadata without modifying the input.
/// A transport may assign a new delivery ID, retaining the first ID in <see cref="Message.OriginalMessageIdHeaderName"/>.
/// Successful completion means the retry has been accepted; the caller may then acknowledge the original.
/// The destination must be a queue in the scheduler's messaging system, not a topic subscription.
/// </remarks>
public interface IAmAMessageRequeueSchedulerSync : IAmAMessageScheduler
{
    /// <summary>Schedule a retry on the destination queue.</summary>
    /// <param name="message">The failed delivery, including its updated handled count.</param>
    /// <param name="destination">The physical queue to receive the retry.</param>
    /// <param name="delay">A non-negative delay before delivery.</param>
    /// <exception cref="ArgumentException">The destination is not a valid queue name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The delay is negative.</exception>
    void Requeue(Message message, ChannelName destination, TimeSpan delay);
}
