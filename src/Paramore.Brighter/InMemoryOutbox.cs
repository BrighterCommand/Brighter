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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Extensions;

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

        /// <summary>
        /// The Id of the message as a string key
        /// </summary>
        public string Key => Message.Id.ToString();

        /// <summary>
        /// Turn a Guid into an inbox key - convenience wrapper
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string ConvertKey(Guid id)
        {
            return $"{id}";
        }
    }


    /// <summary>
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Outbox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// This class is intended to be thread-safe, so you can use one InMemoryOutbox across multiple performers. However, the state is not global i.e. static
    /// so you can use multiple instances safely as well
    /// </summary>
#pragma warning disable CS0618
    public class InMemoryOutbox : InMemoryBox<OutboxEntry>, IAmAnOutboxSync<Message, CommittableTransaction>, IAmAnOutboxAsync<Message, CommittableTransaction>
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
        /// Adds the specified message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null)
        {
            ClearExpiredMessages();
            EnforceCapacityLimit();

            var key = OutboxEntry.ConvertKey(message.Id);
            if (!_requests.ContainsKey(key))
            {
                if (!_requests.TryAdd(key, new OutboxEntry {Message = message, WriteTime = DateTime.UtcNow}))
                {
                    throw new Exception($"Could not add message with Id: {message.Id} to outbox");
                }
            }
        }
        
        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        public void Add(
            IEnumerable<Message> messages, 
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null
            )
        {
            ClearExpiredMessages();
            EnforceCapacityLimit();

            foreach (Message message in messages)
            {
                Add(message, outBoxTimeout, transactionProvider);
            }
        }

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task AddAsync(Message message,
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

            Add(message, outBoxTimeout);
            
            tcs.SetResult(new object());
            return tcs.Task;
        }

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionProvider">This is not used for the In Memory Outbox.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task AddAsync(IEnumerable<Message> messages,
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
                Add(message, outBoxTimeout);
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }

        /// <summary>
        /// Get the messages that have been marked as flushed in the store
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1,
            int outboxTimeout = -1, 
            Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            DateTime dispatchedSince = DateTime.UtcNow.AddMilliseconds( -1 * millisecondsDispatchedSince);
            return _requests.Values.Where(oe =>  (oe.TimeFlushed != DateTime.MinValue) && (oe.TimeFlushed >= dispatchedSince))
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
        }
         
        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId">The id of the message to get</param>
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <returns>The message</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            ClearExpiredMessages();
            
            return _requests.TryGetValue(OutboxEntry.ConvertKey(messageId), out OutboxEntry entry) ? entry.Message : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();

            if (pageNumber == 1)
            {
                return _requests.Values.Select(oe => oe.Message).Take(pageSize).ToList();
            }
            else
            {
                var skipMessageCount = (pageNumber-1) * pageSize;
                return _requests.Values.Select(oe => oe.Message).Skip(skipMessageCount).Take(pageSize).ToList();
            }
        }
         
       /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Get(messageId, outBoxTimeout);

            tcs.SetResult(command);
            return tcs.Task;
        }

       public Task<IEnumerable<Message>> GetAsync(
           IEnumerable<Guid> messageIds, 
           int outBoxTimeout = -1,
           CancellationToken cancellationToken = default
           )
       {
           var tcs = new TaskCompletionSource<IEnumerable<Message>>(TaskCreationOptions.RunContinuationsAsynchronously);
            ClearExpiredMessages();

            var ids = messageIds.Select(m => m.ToString()).ToList();

            tcs.SetResult(_requests.Values.Where(oe => ids.Contains(oe.Key)).Select(oe => oe.Message).ToList());

           return tcs.Task;
       }

       /// <summary>
       /// Mark the message as dispatched
       /// </summary>
       /// <param name="id">The message to mark as dispatched</param>
       /// <param name="dispatchedAt">The time to mark as the dispatch time</param>
       /// <param name="cancellationToken">A cancellation token for the async operation</param>
       public Task MarkDispatchedAsync(
           Guid id, 
           DateTime? dispatchedAt = null, 
           Dictionary<string, object> args = null, 
           CancellationToken cancellationToken = default
           )
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            MarkDispatched(id, dispatchedAt);
            
            tcs.SetResult(new object());

            return tcs.Task;
        }

       public Task MarkDispatchedAsync(
           IEnumerable<Guid> ids, 
           DateTime? dispatchedAt = null, 
           Dictionary<string, object> args = null,
           CancellationToken cancellationToken = default
           )
       {
           var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            
           ids.Each((id) => MarkDispatched(id, dispatchedAt));
            
           tcs.SetResult(new object());

           return tcs.Task;
       }

       public Task<IEnumerable<Message>> DispatchedMessagesAsync(
           double millisecondsDispatchedSince, 
           int pageSize = 100, 
           int pageNumber = 1,
           int outboxTimeout = -1, 
           Dictionary<string, object> args = null, 
           CancellationToken cancellationToken = default
           )
       {
           return Task.FromResult(DispatchedMessages(millisecondsDispatchedSince, pageSize, pageNumber, outboxTimeout,
               args));
       }

       /// <summary>
       /// Mark the message as dispatched
       /// </summary>
       /// <param name="id">The message to mark as dispatched</param>
       /// <param name="dispatchedAt">The time that the message was dispatched</param>
       /// <param name="args">Allows passing arbitrary arguments for searching for a message - not used</param>
       public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            if (_requests.TryGetValue(OutboxEntry.ConvertKey(id), out OutboxEntry entry))
            {
                entry.TimeFlushed = dispatchedAt ?? DateTime.UtcNow;
            }
        }

       /// <summary>
       /// Messages still outstanding in the Outbox because their timestamp
       /// </summary>
       /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
       /// <param name="pageSize">The number of messages to return on a page</param>
       /// <param name="pageNumber">The page number to return</param>
       /// <param name="args">Additional parameters required for search, if any</param>
       /// <returns>Outstanding Messages</returns>
       public IEnumerable<Message> OutstandingMessages(double millSecondsSinceSent, int pageSize = 100, int pageNumber = 1,
            Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            DateTime sentBefore = DateTime.UtcNow.AddMilliseconds( -1 * millSecondsSinceSent);
            var outstandingMessages = _requests.Values.Where(oe =>  (oe.TimeFlushed == DateTime.MinValue) && (oe.WriteTime <= sentBefore))
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
            return outstandingMessages;
        }

       /// <summary>
       /// Delete the specified messages from the Outbox
       /// </summary>
       /// <param name="messageIds">The messages to delete</param>
        public void Delete(params Guid[] messageIds)
        {
            foreach (Guid messageId in messageIds)
            {
                _requests.TryRemove(messageId.ToString(), out _);
            }
        }

       /// <summary>
       /// Get messages from the Outbox
       /// </summary>
       /// <param name="pageSize">The number of messages to return on each page</param>
       /// <param name="pageNumber">The page to return</param>
       /// <param name="args">Additional parameters used to find messages, if any</param>
       /// <param name="cancellationToken">A cancellation token for the ongoing asynchronous process</param>
       /// <returns></returns>
        public Task<IList<Message>> GetAsync(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<IList<Message>>(TaskCreationOptions.RunContinuationsAsynchronously);

            tcs.SetResult(Get(pageSize, pageNumber, args));

            return tcs.Task;
        }

       /// <summary>
       /// A list of outstanding messages
       /// </summary>
       /// <param name="millSecondsSinceSent">The age of the message in milliseconds</param>
       /// <param name="pageSize">The number of messages to return on a page</param>
       /// <param name="pageNumber">The page to return</param>
       /// <param name="args">Additional arguments needed to find a message, if any</param>
       /// <param name="cancellationToken">A cancellation token for the ongoing asynchronous operation</param>
       /// <returns></returns>
        public Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<IEnumerable<Message>>(TaskCreationOptions.RunContinuationsAsynchronously);

            tcs.SetResult(OutstandingMessages(millSecondsSinceSent, pageSize, pageNumber, args));

            return tcs.Task;
        }

       /// <summary>
       /// Deletes the messages from the Outbox
       /// </summary>
       /// <param name="cancellationToken">A cancellation token for the ongoing asynchronous operation</param>
       /// <param name="messageIds">The ids of the messages to delete</param>
       /// <returns></returns>
        public Task DeleteAsync(CancellationToken cancellationToken, params Guid[] messageIds)
        {
            Delete(messageIds);
            return Task.CompletedTask;
        }

        public IEnumerable<Message> DispatchedMessages(int hoursDispatchedSince, int pageSize = 100)
        {
            ClearExpiredMessages();
            
            DateTime dispatchedSince = DateTime.UtcNow.AddHours( -1 * hoursDispatchedSince);
            return _requests.Values.Where(oe =>  (oe.TimeFlushed != DateTime.MinValue) && (oe.TimeFlushed >= dispatchedSince))
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
        }
        
        public Task<IEnumerable<Message>> DispatchedMessagesAsync(int hoursDispatchedSince, int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DispatchedMessages(hoursDispatchedSince, pageSize));
        }

        public Task<int> GetNumberOfOutstandingMessagesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_requests.Count(r => r.Value.TimeFlushed == DateTime.MinValue));
        }
    }
}
