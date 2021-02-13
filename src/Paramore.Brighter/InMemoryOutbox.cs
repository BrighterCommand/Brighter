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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Outbox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// This class is intended to be thread-safe, so you can use one InMemoryOutbox across multiple performers. However, the state is not global i.e. static
    /// so you can use multiple instances safely as well
    /// </summary>
    public class InMemoryOutbox : IAmAnOutbox<Message>, IAmAnOutboxAsync<Message>, IAmAnOutboxViewer<Message>
    {
        private readonly ConcurrentDictionary<Guid, OutboxEntry> _posts = new ConcurrentDictionary<Guid, OutboxEntry>();

        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext
        /// </summary>
        /// <value><c>true</c> if [continue on captured context]; otherwise, <c>false</c>.</value>
        public bool ContinueOnCapturedContext { get; set; }
        
        /// <summary>
        /// How long does an entry last in the Outbox before we delete it (defaults to 5 min)
        /// Think about your typical data volumes over a window of time, they all use memory to store
        /// But contrast with how long you want to be able to resend due to broker failure for.
        /// Memory is not reclaimed until an expiration scan
        /// </summary>
        public TimeSpan PostTimeToLive { get; set; } = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// if it has been this long since the last scan, any operation can trigger a scan of the
        /// cache to delete existing entries (defaults to 5 mins)
        /// Your expiration interval should greater than your time to live, and represents the frequency at which we will reclaim memory
        /// Note that scan check is triggered by an operation on the outbox, but it runs on a background thread to avoid latency with basic operation
        /// </summary>
        public TimeSpan ExpirationScanInterval { get; set; } = TimeSpan.FromMinutes(10);

        public int MessageLimit { get; set; }

        /// <summary>
        /// For diagnostics 
        /// </summary>
        public int MessageCount => _posts.Count;

        /// <summary>
        /// At what percentage of our size limit should we return, once we hit that limit
        /// </summary>
        public double CompactionPercentage{ get; set; }


        private DateTime _lastScanAt = DateTime.UtcNow;
        private readonly object _cleanupRunningLockObject = new object();

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="outBoxTimeout"></param>
        public void Add(Message message, int outBoxTimeout = -1)
        {
            ClearExpiredMessages();
            EnforceCapacityLimit();
            
            if (!_posts.ContainsKey(message.Id))
            {
                if (!_posts.TryAdd(message.Id, new OutboxEntry {Message = message, TimeDeposited = DateTime.UtcNow}))
                {
                    throw new Exception($"Could not add message with Id: {message.Id} to outbox");
                }
            }
        }

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();

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
            return _posts.Values.Where(oe =>  (oe.TimeFlushed != DateTime.MinValue) && (oe.TimeFlushed >= dispatchedSince))
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
            
            return _posts.TryGetValue(messageId, out OutboxEntry entry) ? entry.Message : null;
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
            
            return _posts.Values.Select(oe => oe.Message).Take(pageSize).ToList();
        }
         
       /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<Message>();

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Get(messageId, outBoxTimeout);

            tcs.SetResult(command);
            return tcs.Task;
        }

        /// <summary>
        /// Mark the message as dispatched
        /// </summary>
        /// <param name="id">The message to mark as dispatched</param>
        public Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();
            
            MarkDispatched(id, dispatchedAt);
            
            tcs.SetResult(new object());

            return tcs.Task;
        }

        /// <summary>
        /// Mark the message as dispatched
        /// </summary>
        /// <param name="id">The message to mark as dispatched</param>
         public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            if (_posts.TryGetValue(id, out OutboxEntry entry))
            {
                entry.TimeFlushed = dispatchedAt ?? DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="args">Additional parameters required for search, if any</param>
         /// <returns>Outstanding Messages</returns>
       public IEnumerable<Message> OutstandingMessages(double millSecondsSinceSent, int pageSize = 100, int pageNumber = 1,
            Dictionary<string, object> args = null)
        {
            ClearExpiredMessages();
            
            DateTime sentBefore = DateTime.UtcNow.AddMilliseconds( -1 * millSecondsSinceSent);
            return _posts.Values.Where(oe =>  (oe.TimeFlushed == DateTime.MinValue) && (oe.TimeDeposited <= sentBefore))
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
        }

        private void ClearExpiredMessages()
        {
            var now = DateTime.Now;
            
            if (now - _lastScanAt < ExpirationScanInterval)
                return;

            if (Monitor.TryEnter(_cleanupRunningLockObject))
            {
                try
                {
                    //This is expensive, so use a background thread
                    Task.Factory.StartNew(
                        action: state => RemoveExpiredMessages((DateTime)state),
                        state: now,
                        cancellationToken: CancellationToken.None,
                        creationOptions: TaskCreationOptions.DenyChildAttach,
                        scheduler: TaskScheduler.Default);

                    _lastScanAt = now;

                }
                finally
                {
                    Monitor.Exit(_cleanupRunningLockObject);
                }
            }
        }

        private void RemoveExpiredMessages(DateTime now)
        {
           var expiredPosts = 
               _posts
                   .Where(entry => now - entry.Value.TimeDeposited >= PostTimeToLive)
                   .Select(entry => entry.Key);
           
            foreach (var post in expiredPosts)
            {
                //if this fails ignore, killed by something else like compaction
                _posts.TryRemove(post, out _);
            }
        }

        private void EnforceCapacityLimit()
        {
            //Take a copy as it may change whilst we are doing the calculation, we ignore that
            var count = MessageCount;
            var upperSize = MessageLimit;

            if (count >= upperSize)
            {
                if (Monitor.TryEnter(_cleanupRunningLockObject))
                {
                    try
                    {
                        int newSize = (int)(count * CompactionPercentage);
                        int entriesToRemove = upperSize - newSize;

                        Task.Factory.StartNew(
                            action: state => Compact((int)state),
                            state: entriesToRemove,
                            CancellationToken.None,
                            TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default);

                    }
                    finally
                    {
                        Monitor.Exit(_cleanupRunningLockObject);
                    }
                }
            }
        }

        // Compaction algorithm is to sort into date deposited order, with oldest first
        // Then remove entries until newsize is reached
        private void Compact(int entriesToRemove)
        {
            var removalList = 
                _posts
                    .Values
                    .OrderBy(entry => entry.TimeDeposited)
                    .Take(entriesToRemove)
                    .Select(entry => entry.Message.Id);

            foreach (var messageId in removalList)
            {
                //ignore errors, likely just something else has cleared it such as TTL eviction
                _posts.TryRemove(messageId, out _);
            }
        }

        class OutboxEntry
        {
            public DateTime TimeDeposited { get; set; }
            public DateTime TimeFlushed { get; set; }
            public Message Message { get; set; }
        }
   }
}
