#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAReceiveMessageGatewayAsync
    /// </summary>
    public interface IAmAMessageConsumerAsync : IAsyncDisposable
    {
        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">Cancel the acknowledgment</param>
        Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// When a message is rejected, another consumer should not process it. If there is a dead letter, or invalid
        /// message channel, the message should be forwardedn to it
        /// <param name="message">The <see cref="Message"/> to reject</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explaines why we rejected the message</param>
        /// <param name="cancellationToken">Cancels the rejection</param>
        /// <returns>True if the message has been removed from the channel, false otherwise</returns>
        Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeOut">The <see cref="TimeSpan"/> timeout. If null default to 1000</param>
        /// <param name="cancellationToken">Cancel the recieve</param>
        /// <returns>An array of Messages from middleware</returns>
        Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Nacks the specified message, releasing it back to the transport for redelivery.
        /// </summary>
        /// <remarks>
        /// For queue-based transports, this explicitly releases the transport's lock so the message
        /// is immediately available to any consumer. For stream-based transports, this is a no-op
        /// because not committing the offset is sufficient.
        /// </remarks>
        /// <param name="message">The <see cref="Message"/> to nack</param>
        /// <param name="cancellationToken">Cancel the nack operation</param>
        Task NackAsync(Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">Time to delay delivery of the message, default to 0ms or no delay</param>
        /// <param name="cancellationToken">Cancel the requeue</param>
        /// <returns>True if the message has been acked, false otherwise</returns>
        Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken));
    }
}

