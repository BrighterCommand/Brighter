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

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAChannelSync 
    /// An <see cref="IAmAChannelSync"/> for reading messages from a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    /// and acknowledging receipt of those messages
    /// </summary>
    public interface IAmAChannelSync : IAmAChannel
    {
        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <remarks>
        /// When a message is acknowledged, another consumer should not process it
        /// </remarks>
        /// <param name="message">The<see cref="Message"/> to reject</param>
        void Acknowledge(Message message);

        /// <summary>
        /// Clears the queue
        /// </summary>
        void Purge();
        
        /// <summary>
        /// The timeout for the channel to receive a message.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan"/> timeout; if null default to 1 second</param>
        /// <returns>Message.</returns>
        Message Receive(TimeSpan? timeout);

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// When a message is rejected, another consumer should not process it. If there is a dead letter, or invalid
        /// message channel, the message should be forwardedn to it
        /// <param name="message">The <see cref="Message"/> to reject</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explaines why we rejected the message </param>
        bool Reject(Message message, MessageRejectionReason? reason = null);

        /// <summary>
        /// Nacks the specified message, releasing it back to the transport for redelivery.
        /// </summary>
        /// <remarks>
        /// For queue-based transports, this explicitly releases the transport's lock so the message
        /// is immediately available to any consumer. For stream-based transports, this is a no-op
        /// because not committing the offset is sufficient.
        /// </remarks>
        /// <param name="message">The <see cref="Message"/> to nack</param>
        void Nack(Message message);

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeOut">The delay to the delivery of the message.</param>
        /// <returns>True if the message should be Acked, false otherwise</returns>
        bool Requeue(Message message, TimeSpan? timeOut = null);

   }
}
