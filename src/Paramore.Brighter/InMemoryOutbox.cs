#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

/*
 * NOTE:
 * Design inspired by MS System.Extensions.Caching.Memory.MemoryCache
 */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
   /// <summary>
    /// An outbox entry - a message that we want to send
    /// </summary>
    public class OutboxEntry : IHaveABoxWriteTime
    {
        /// <summary>
        /// When was the message added to the outbox
        /// </summary>
        public DateTime WriteTime { get; set; }
        
        /// <summary>
        /// When was the message sent to the middleware
        /// </summary>
        public DateTime TimeFlushed { get; set; }
        
        /// <summary>
        /// The message to be dispatched
        /// </summary>
        public Message Message { get; set; }
    }


    /// <summary>
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Outbox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// This class is intended to be thread-safe, so you can use one InMemoryOutbox across multiple performers. However, the state is not global i.e. static
    /// so you can use multiple instances safely as well
    /// </summary>
#pragma warning disable CS0618
    public class InMemoryOutbox(
        TimeProvider timeProvider, 
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All
        ) : InMemoryBox<OutboxEntry>(timeProvider), 
        IAmAnOutboxSync<Message, CommittableTransaction>, IAmAnOutboxAsync<Message, CommittableTransaction>
#pragma warning restore CS0618
    {
        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        /// or access the Result or otherwise block. You may need the originating synchronization context if you need to access thread specific storage
        /// such as HTTPContext
        /// </summary>
        /// <value><c>true</c> if [continue on captured context]; otherwise, <c>false</c>.</value>
        public bool ContinueOnCapturedContext { get; set; }
        
        /// <summary>
        /// The Tracer that we want to use to capture telemetry
        /// We inject this so that we can use the same tracer as the calling application
        /// You do not need to set this property as we will set it when setting up the External Service Bus
        /// </summary>
        public IAmABrighterTracer Tracer { private get; set; } 

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="message">The message to add to the Outbox</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">How long in ms to wait; -1 forever (default -1)</param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null)
        {
            var span = Tracer?.CreateDbSpan(
                new OutboxSpanInfo(DbSystem.Brighter, InMemoryAttributes.DbName, OutboxDbOperation.Add, InMemoryAttributes.DbTable),  
                requestContext?.Span, 
                options: instrumentationOptions
                );

            try
            {
                ClearExpiredMessages();
                EnforceCapacityLimit();

                if (!Requests.ContainsKey(message.Id))
                {
                    if (!Requests.TryAdd(message.Id,
                            new OutboxEntry { Message = message, WriteTime = timeProvider.GetUtcNow().DateTime }))
                    {
                        throw new Exception($"Could not add message with Id: {message.Id} to outbox");
                    }
                }
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Adds the specified messages
        /// </summary>
        /// <param name="messages">The messages to add to the Outbox</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">How long to wait in ms; -1 = forever (default -1)</param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        public void Add(
            IEnumerable<Message> messages, 
            RequestContext requestContext,
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null
            )
        {
            ClearExpiredMessages();
            EnforceCapacityLimit();

            foreach (Message message in messages)
            {
                Add(message, requestContext, outBoxTimeout, transactionProvider);
            }
        }

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="message">The messages to add to the Outbox</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">How long to wait in ms; -1 = forever (default -1)</param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task AddAsync(
            Message message,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            Add(message, requestContext);
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Adds the specified messages
        /// </summary>
        /// <param name="messages">The messages to add to the Outbox</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="outBoxTimeout">How long to wait in ms; -1 = forever (default -1)</param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task AddAsync(
            IEnumerable<Message> messages,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            foreach (Message message in messages)
            {
                Add(message, requestContext, outBoxTimeout);
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }
        
        /// <summary>
        /// Delete the specified messages from the Outbox
        /// </summary>
        /// <param name="messageIds">The messages to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>           
        /// <param name="args"></param>
        public void Delete(string[] messageIds, RequestContext requestContext, Dictionary<string, object> args = null)
        {
            foreach (string messageId in messageIds)
            {
                Requests.TryRemove(messageId, out _);
            }
        }

        /// <summary>
        /// Deletes the messages from the Outbox
        /// </summary>
        /// <param name="messageIds">The ids of the messages to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>           
        /// <param name="args">Additional arguments needed to find a message, if any</param>
        /// <param name="cancellationToken">A cancellation token for the ongoing asynchronous operation</param>
        /// <returns></returns>
        public Task DeleteAsync(
            string[] messageIds, 
            RequestContext requestContext,
            Dictionary<string, object> args,
            CancellationToken cancellationToken = default)
        {
            Delete(messageIds, requestContext, args);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get the messages that have been marked as flushed in the store
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100, 
            int pageNumber = 1,
            int outboxTimeout = -1, 
            Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            DateTime dispatchedSince = timeProvider.GetUtcNow().DateTime.AddMilliseconds( -1 * millisecondsDispatchedSince);
            return Requests.Values.Where(oe =>  (oe.TimeFlushed != DateTime.MinValue) && (oe.TimeFlushed >= dispatchedSince))
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
        }

        /// <summary>
        /// Get the messages that have been marked as flushed in the store
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">A cancellation token for the async operation</param>
        public Task<IEnumerable<Message>> DispatchedMessagesAsync(double millisecondsDispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DispatchedMessages(millisecondsDispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout,
                args));
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId">The id of the message to get</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The message</returns>
        public Message Get(string messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            return Requests.TryGetValue(messageId, out OutboxEntry entry) ? entry.Message : null;
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="outBoxTimeout"></param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">A cancellation token for the async operation</param>
        /// <returns></returns>
        public Task<Message> GetAsync(
            string messageId,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Get(messageId, requestContext, outBoxTimeout);

            tcs.SetResult(command);
            return tcs.Task;
        }

        /// <summary>
        /// Mark the message as dispatched
        /// </summary>
        /// <param name="id">The message to mark as dispatched</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="dispatchedAt">The time to mark as the dispatch time</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">A cancellation token for the async operation</param>
        public Task MarkDispatchedAsync(
            string id,
            RequestContext requestContext,
            DateTime? dispatchedAt = null,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            MarkDispatched(id, requestContext, dispatchedAt);
            
            tcs.SetResult(new object());

            return tcs.Task;
        }

        /// <summary>
        /// Mark the messages as dispatched
        /// </summary>
        /// <param name="ids">The messages to mark as dispatched</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="dispatchedAt">The time to mark as the dispatch time</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">A cancellation token for the async operation</param>
        public Task MarkDispatchedAsync(
            IEnumerable<string> ids,
            RequestContext requestContext,
            DateTime? dispatchedAt = null,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
       {
           var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            
           ids.Each((id) => MarkDispatched(id, requestContext, dispatchedAt));
            
           tcs.SetResult(new object());

           return tcs.Task;
       }


        /// <summary>
        /// Mark the message as dispatched
        /// </summary>
        /// <param name="id">The message to mark as dispatched</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="dispatchedAt">The time that the message was dispatched</param>
        /// <param name="args">Allows passing arbitrary arguments for searching for a message - not used</param>
        public void MarkDispatched(string id, RequestContext requestContext, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            if (Requests.TryGetValue(id, out OutboxEntry entry))
            {
                entry.TimeFlushed = dispatchedAt ?? timeProvider.GetUtcNow().DateTime;
            }
        }

       /// <summary>
       /// Messages still outstanding in the Outbox because their timestamp
       /// </summary>
       /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
       /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
       /// <param name="pageSize">The number of messages to return on a page</param>
       /// <param name="pageNumber">The page number to return</param>
       /// <param name="args">Additional parameters required for search, if any</param>
       /// <returns>Outstanding Messages</returns>
       public IEnumerable<Message> OutstandingMessages(
           double millSecondsSinceSent, 
           RequestContext requestContext,
           int pageSize = 100, 
           int pageNumber = 1,
            Dictionary<string, object> args = null
           )
        {
            ClearExpiredMessages();

            var now = timeProvider.GetUtcNow();
            var sentBefore = now.AddMilliseconds( -1 * millSecondsSinceSent);
            var outstandingMessages = Requests.Values.Where(oe =>  (oe.TimeFlushed == DateTime.MinValue) && (oe.WriteTime <= sentBefore))
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
            return outstandingMessages;
        }

        /// <summary>
        /// A list of outstanding messages
        /// </summary>
        /// <param name="millSecondsSinceSent">The age of the message in milliseconds</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>       
        /// <param name="pageSize">The number of messages to return on a page</param>
        /// <param name="pageNumber">The page to return</param>
        /// <param name="args">Additional arguments needed to find a message, if any</param>
        /// <param name="cancellationToken">A cancellation token for the ongoing asynchronous operation</param>
        /// <returns></returns>
        public Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<IEnumerable<Message>>(TaskCreationOptions.RunContinuationsAsynchronously);

            tcs.SetResult(OutstandingMessages(millSecondsSinceSent, requestContext, pageSize, pageNumber, args));

            return tcs.Task;
        }
        
        private void EndSpan(Activity span)
        {
            if (span?.Status == ActivityStatusCode.Unset)
                span.SetStatus(ActivityStatusCode.Ok);
            span?.Dispose();
        }
        
        private RequestContext InitRequestContext(Activity span, RequestContext requestContext)
        {
            //We don't take a dependency on a IAmARequestContextFactory, because the call will always be in the context
            // of the external service bus which does have a factory and should have created a context for us if there was not one,
            // so this is a null object pattern to support tests etc.
            var context = requestContext ?? new RequestContext();
            context.Span = span;
            return context;
        }
    }
}
