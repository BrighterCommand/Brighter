#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// A producer that can raise its publish confirmation through an awaitable callback. Where
    /// <see cref="ISupportPublishConfirmation.OnMessagePublished"/> is fire-and-forget (an async
    /// subscriber becomes async void, so the producer cannot observe when the handler finishes),
    /// <see cref="OnMessagePublishedAsync"/> returns a <see cref="Task"/> the producer awaits — or
    /// tracks to completion — before it considers the confirmation handled. This lets a producer's
    /// dispose path drain the full confirmation pipeline (for example the Outbox mark-dispatched
    /// write) rather than only the receipt of the broker's ack.
    /// </summary>
    public interface ISupportPublishConfirmationAsync
    {
        /// <summary>
        /// When <see langword="true"/>, the mediator subscribes <see cref="OnMessagePublishedAsync"/>
        /// (awaited) instead of <see cref="ISupportPublishConfirmation.OnMessagePublished"/>
        /// (fire-and-forget) for its confirmation callback. Broker-backed producers should return
        /// <see langword="true"/>; the <see cref="InMemoryMessageProducer"/> exposes it as an opt-in
        /// because its awaited path also defers the send to a background pump.
        /// </summary>
        bool UseAsyncPublishConfirmation { get; }

        /// <summary>
        /// Fired when a confirmation is received that a message has been published. Unlike
        /// <see cref="ISupportPublishConfirmation.OnMessagePublished"/>, the producer awaits (or
        /// tracks) the returned <see cref="Task"/>, so disposal does not complete while a handler
        /// is still running. Handlers are expected to be non-throwing; producers isolate handler
        /// faults so one bad subscriber cannot halt confirmation processing.
        /// </summary>
        event Func<PublishConfirmationResult, Task> OnMessagePublishedAsync;
    }
}
