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
using Amazon.DynamoDBv2.DataModel;
using Newtonsoft.Json;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    public class FakeOutbox : IAmAnOutbox<Message>, IAmAnOutboxAsync<Message>, IAmAnOutboxViewer<Message>
    {
        private readonly List<OutboxEntry> _posts = new List<OutboxEntry>();

        public bool ContinueOnCapturedContext { get; set; }

        public void Add(Message message, int outBoxTimeout = -1)
        {
            _posts.Add(new OutboxEntry {Message = message, TimeDeposited = DateTime.UtcNow});
        }

        public Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Add(message, outBoxTimeout);

            return Task.FromResult(0);
        }

        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince,
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

        public Message Get(Guid messageId, int outBoxTimeout = -1)
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

        public Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<Message>(cancellationToken);

            return Task.FromResult(Get(messageId, outBoxTimeout));
        }

        public Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();
            
            MarkDispatched(id, dispatchedAt);
            
            tcs.SetResult(new object());

            return tcs.Task;
        }

        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null)
        {
           var entry = _posts.SingleOrDefault(oe => oe.Message.Id == id);
           entry.TimeFlushed = dispatchedAt ?? DateTime.UtcNow;

        }

       public IEnumerable<Message> OutstandingMessages(
           double millSecondsSinceSent, 
           int pageSize = 100, 
           int pageNumber = 1,
           Dictionary<string, object> args = null)
        {
            var sentAfter = DateTime.UtcNow.AddMilliseconds(-1 * millSecondsSinceSent);
            return _posts
                .Where(oe => oe.TimeDeposited.Value > sentAfter && oe.TimeFlushed == null)
                .Select(oe => oe.Message)
                .Take(pageSize)
                .ToArray();
        }

        class OutboxEntry
        {
            public DateTime? TimeDeposited { get; set; }
            public DateTime? TimeFlushed { get; set; }
            public Message Message { get; set; }
        }
    }
}
