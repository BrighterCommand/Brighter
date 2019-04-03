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

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Message Store to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// </summary>
    public class InMemoryOutbox : IAmAnOutbox<Message>, IAmAnOutboxAsync<Message>, IAmAnOutboxViewer<Message>
    {
        private readonly List<OutboxEntry> _post = new List<OutboxEntry>();

        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext
        /// </summary>
        /// <value><c>true</c> if [continue on captured context]; otherwise, <c>false</c>.</value>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Adds the specified message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="outBoxTimeout"></param>
        public void Add(Message message, int outBoxTimeout = -1)
        {
            if (!_post.Exists((entry)=> entry.Message.Id == message.Id))
            {
                _post.Add(new OutboxEntry{Message = message, TimeDeposited = DateTime.UtcNow});
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
       /// <param name="millisecondsDispatchedAgo">How long ago would the message have been dispatched in milliseconds</param>
       /// <param name="pageSize">How many messages in a page</param>
       /// <param name="pageNumber">Which page of messages to get</param>
       /// <returns>A list of dispatched messages</returns>
         public IEnumerable<Message> DispatchedMessages(double millisecondsDispatchedAgo, int pageSize = 100, int pageNumber = 1)
        {
            DateTime dispatchedSince = DateTime.UtcNow.AddMilliseconds( -1 * millisecondsDispatchedAgo);
            return _post.Where(oe =>  oe.TimeDeposited > dispatchedSince)
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
            if (!_post.Exists((entry) => entry.Message.Id == messageId))
                return null;

            return _post.Find((entry) => entry.Message.Id == messageId).Message;
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
        /// <param name="messageId">The message to mark as dispatched</param>
        public Task MarkDispatchedAsync(Guid messageId, DateTime? dispatchedAt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();
            
            MarkDispatched(messageId, dispatchedAt);
            
            tcs.SetResult(new object());

            return tcs.Task;
        }

        /// <summary>
        /// Mark the message as dispatched
        /// </summary>
        /// <param name="messageId">The message to mark as dispatched</param>
         public void MarkDispatched(Guid messageId, DateTime? dispatchedAt = null)
        {
             if (!_post.Exists((oe) => oe.Message.Id == messageId))
               return;
             
           var post = _post.Find((entry) => entry.Message.Id == messageId);
           post.TimeFlushed = dispatchedAt ?? DateTime.UtcNow;

        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <returns>Outstanding Messages</returns>
        public IEnumerable<Message> OutstandingMessages(double millSecondsSinceSent, int pageSize = 100, int pageNumber = 1)
        {
            DateTime sentAfter = DateTime.UtcNow.AddMilliseconds( -1 * millSecondsSinceSent);
            return _post.Where(oe =>  oe.TimeDeposited > sentAfter)
                .Take(pageSize)
                .Select(oe => oe.Message).ToArray();
        }
    
        class OutboxEntry
        {
            public DateTime TimeDeposited { get; set; }
            public DateTime TimeFlushed { get; set; }
            public Message Message { get; set; }
        }


        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            return _post.Select(oe => oe.Message).Take(pageSize).ToList();
        }

    }
}
