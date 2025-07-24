#region Licence
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
using System.Collections.Generic;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAnOutbox
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into an OutBox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// We provide implementations of <see cref="IAmAnOutboxSync{T}"/> for various databases. Users using other databases should consider a Pull Request
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <typeparam name="TTransaction">The transaction type of the underlying Db</typeparam>
    public interface IAmAnOutboxSync<T, TTransaction> : IAmAnOutbox where T : Message
    {
        /// <summary>
        /// Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="transactionProvider">The Connection Provider to use for this call</param>
        void Add(T message, RequestContext requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<TTransaction>? transactionProvider = null);

        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="transactionProvider">The Connection Provider to use for this call</param>
        void Add(IEnumerable<T> messages, RequestContext? requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<TTransaction>? transactionProvider = null);

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="messageIds">The id of the message to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null);

        /// <summary>
        /// Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="dispatchedSince"></param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">The number of messages to fetch.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="outBoxTimeout">Timeout of sql call.</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>List of messages that need to be dispatched.</returns>
        IEnumerable<Message> DispatchedMessages(
            TimeSpan dispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100, 
            int pageNumber = 1, 
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null);

        /// <summary>
        /// Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null);

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Dictionary to allow platform specific parameters to be passed to the interface</param>
        void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null);

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="dispatchedSince">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">Number of items on the page, default is 100</param>
        /// <param name="pageNumber">Page number of results to return, default is first</param>
        /// <param name="trippedTopics">Collection of tripped topics</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Outstanding Messages</returns>
        IEnumerable<Message> OutstandingMessages(
            TimeSpan dispatchedSince, 
            RequestContext? requestContext,
            int pageSize = 100, 
            int pageNumber = 1,
            IEnumerable<RoutingKey>? trippedTopics = null,
            Dictionary<string, object>? args = null);
    }
}
