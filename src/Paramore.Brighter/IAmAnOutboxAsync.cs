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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAnOutboxAsync
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into an OutBox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// We provide implementations of <see cref="IAmAnOutboxAsync{T}"/> for various databases. Users using unsupported databases should consider a Pull
    /// request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAmAnOutboxAsync<in T> : IAmAnOutbox<T> where T : Message
    {
        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext
        /// </summary>
        bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <param name="transactionConnectionProvider">The Connection Provider to use for this call</param>
        /// <returns><see cref="Task"/>.</returns>
        Task AddAsync(T message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken), IAmABoxTransactionConnectionProvider transactionConnectionProvider = null);

        /// <summary>
        /// Awaitable Get the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}"/>.</returns>
        Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        ///  Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Cancellation Token, if any</param>
        /// <returns></returns>
        [Obsolete("Removed in v10, Please use OutstandingMessagesAsync instead.")]
        Task<IList<Message>> GetAsync(
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Awaitable Get the messages.
        /// </summary>
        /// <param name="messageIds">The message identifiers.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}"/>.</returns>
        Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Update messages to show it is dispatched
        /// </summary>
        /// <param name="ids">The ids of the messages to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="millisecondsDispatchedSince"></param>
        /// <param name="pageSize">The number of messages to fetch.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="outboxTimeout">Timeout of sql call.</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>List of messages that need to be dispatched.</returns>
        Task<IEnumerable<Message>> DispatchedMessagesAsync(
            double millisecondsDispatchedSince,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="pageSize">The number of messages to fetch.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>Outstanding Messages</returns>
        Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent,
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <param name="messageIds">The id of the message to delete</param>
        Task DeleteAsync(CancellationToken cancellationToken, params Guid[] messageIds);
    }
}
