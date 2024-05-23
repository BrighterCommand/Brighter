#region Licence

/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

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
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class FakeOutbox : IAmAnOutboxSync<Message, CommittableTransaction>, IAmAnOutboxAsync<Message, CommittableTransaction>
    {
        private readonly List<OutboxEntry> _posts = new List<OutboxEntry>();

        public bool ContinueOnCapturedContext { get; set; }
        
        public IAmABrighterTracer Tracer { private get; set; } 

        public void Add(
            Message message, 
            RequestContext requestContext,
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null
        )
        {
            _posts.Add(new OutboxEntry {Message = message, TimeDeposited = DateTime.UtcNow});
        }

        public Task AddAsync(
            Message message, 
            RequestContext requestContext,
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null,
            CancellationToken cancellationToken = default 
            )
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Add(message, requestContext, outBoxTimeout);

            return Task.FromResult(0);
        }

        public async Task AddAsync(
            IEnumerable<Message> messages, 
            RequestContext requestContext,
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null,
            CancellationToken cancellationToken = default
            )
        {
            foreach (var message in messages)
            {
                await AddAsync(message, requestContext, outBoxTimeout, transactionProvider, cancellationToken);
            }    
        }

        public void Add(
            IEnumerable<Message> messages, 
            RequestContext requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null)
        {
            foreach (Message message in messages)
            {
                Add(message, requestContext, outBoxTimeout, transactionProvider);
            }
        }

        public async Task AddAsync(
            IEnumerable<Message> messages, 
            RequestContext requestContext,
            int outBoxTimeout = -1,
            CancellationToken cancellationToken = default,
            IAmABoxTransactionProvider<CommittableTransaction> transactionProvider = null)
        {
            foreach (var message in messages)
            {
                await AddAsync(message, requestContext, outBoxTimeout, transactionProvider, cancellationToken);
            }
        }

        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object> args = null)
        {
            var ago = millisecondsDispatchedSince * -1;
            var now = DateTime.UtcNow;
            var messagesSince = now.AddMilliseconds(ago);
            return _posts.Where(oe => oe.TimeFlushed >= messagesSince).Select(oe => oe.Message).Take(pageSize).ToArray();
        }

        public Message Get(string messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object> args = null)
        {
            foreach (var outboxEntry in _posts)
            {
                if (outboxEntry.Message.Id == messageId)
                {
                    return outboxEntry.Message;
                }
            }

            return null;
        }

        public IList<Message> Get(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null)
        {
            return _posts.Select(outboxEntry => outboxEntry.Message).Take(pageSize).ToList();
        }

        public Task<Message> GetAsync(
            string messageId, 
            RequestContext requestContext,
            int outBoxTimeout = -1, 
            Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<Message>(cancellationToken);

            return Task.FromResult(Get(messageId, requestContext, outBoxTimeout));
        }

        public Task<IList<Message>> GetAsync(
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Get(pageSize, pageNumber, args));
        }

        public Task<IEnumerable<Message>> GetAsync(
            IEnumerable<string> messageIds, 
            RequestContext requestContext,
            int outBoxTimeout = -1,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<IEnumerable<Message>>();
            tcs.SetResult(_posts.Where(oe => messageIds.Contains(oe.Message.Id))
                .Select(outboxEntry => outboxEntry.Message).ToList());

            return tcs.Task;
        }
        
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

        public async Task MarkDispatchedAsync(
            IEnumerable<string> ids, 
            RequestContext requestContext,
            DateTime? dispatchedAt = null, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default
            )
        {
            foreach (var id in ids)
            {
                await MarkDispatchedAsync(id, requestContext, dispatchedAt, args, cancellationToken);
            }
        }

        public Task<IEnumerable<Message>> DispatchedMessagesAsync(
            double millisecondsDispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100, 
            int pageNumber = 1,
            int outboxTimeout = -1, Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DispatchedMessages(millisecondsDispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout,
                args));
        }

        public Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent, 
            RequestContext requestContext,
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OutstandingMessages(millSecondsSinceSent, requestContext, pageSize, pageNumber, args));
        }

        public Task DeleteAsync(string[] messageIds, RequestContext requestContext,  Dictionary<string, object> args, CancellationToken cancellationToken = default)
        {
            Delete(messageIds, requestContext);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Message>> DispatchedMessagesAsync(
            int millisecondsDispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100,
            Dictionary<string, object> args = null ,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DispatchedMessages(millisecondsDispatchedSince, requestContext, pageSize));
        }

        public IEnumerable<Message> DispatchedMessages(
            int millisecondsDispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            var ago = millisecondsDispatchedSince * -1;
            var now = DateTime.UtcNow;
            var messagesSince = now.AddMilliseconds(ago);
            return _posts.Where(oe => oe.TimeFlushed >= messagesSince).Select(oe => oe.Message).Take(pageSize).ToArray();
        }

        public void MarkDispatched(string id, RequestContext requestContext, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
           var entry = _posts.Single(oe => oe.Message.Id == id);
           entry.TimeFlushed = dispatchedAt ?? DateTime.UtcNow;

        }

       public IEnumerable<Message> OutstandingMessages(
           double millSecondsSinceSent, 
           RequestContext requestContext,
           int pageSize = 100, 
           int pageNumber = 1,
           Dictionary<string, object> args = null)
        {
            var sentAfter = DateTime.UtcNow.AddMilliseconds(-1 * millSecondsSinceSent);
            return _posts
                .Where(oe => oe.TimeDeposited.HasValue && oe.TimeDeposited.Value < sentAfter && oe.TimeFlushed == null)
                .Select(oe => oe.Message)
                .Take(pageSize)
                .ToArray();
        }

       public void Delete(string[] messageIds, RequestContext requestContext, Dictionary<string, object> args = null)
       {
           foreach (string messageId in messageIds)
           {
               var message = _posts.First(e => e.Message.Id == messageId);
               _posts.Remove(message);
           }
       }

       class OutboxEntry
        {
            public DateTime? TimeDeposited { get; set; }
            public DateTime? TimeFlushed { get; set; }
            public Message Message { get; set; }
        }

 
    }
}
