using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class SpyOutbox : 
        IAmAnOutboxSync<Message, SpyTransaction>,
        IAmAnOutboxAsync<Message, SpyTransaction>
    {
        public required IAmABrighterTracer Tracer { private get; set; }

        public List<SpyOutboxEntry> Messages { get; set; } = new List<SpyOutboxEntry>();
        public bool ContinueOnCapturedContext { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<SpyTransaction>? transactionProvider = null)
        {
            if (transactionProvider != null)
            {
                var transaction = transactionProvider.GetTransaction();
                transaction.Add(message);
            }
            else
            {
                Messages.Add(new SpyOutboxEntry(message));
            }
        }

        public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<SpyTransaction>? transactionProvider = null)
        {
            foreach (var message in messages)
            {
                Add(message, requestContext ?? new RequestContext(), outBoxTimeout, transactionProvider);
            }
        }

        public Task AddAsync(Message message, RequestContext requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<SpyTransaction>? transactionProvider = null, CancellationToken cancellationToken = default)
        {
            Add(message, requestContext, outBoxTimeout, transactionProvider);
            return Task.CompletedTask;
        }

        public Task AddAsync(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<SpyTransaction>? transactionProvider = null, CancellationToken cancellationToken = default)
        {
            Add(messages, requestContext, outBoxTimeout, transactionProvider);
            return Task.CompletedTask;
        }

        public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        {
            Messages = Messages.Where(m => !messageIds.Any(id => m.Message.Id == id)).ToList();
        }

        public Task DeleteAsync(Id[] messageIds, RequestContext requestContext, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        {
            Delete(messageIds, requestContext, args);
            return Task.CompletedTask;
        }

        public IEnumerable<Message> DispatchedMessages(TimeSpan dispatchedSince, RequestContext requestContext, int pageSize = 100, int pageNumber = 1, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        {
            return Messages.Where(m => m.Dispatched).Select(m => m.Message);
        }

        public Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext, int pageSize = 100, int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DispatchedMessages(dispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout, args));
        }

        public Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        {
            return Messages.First(m => m.Message.Id == messageId).Message;
        }

        public Task<Message> GetAsync(Id messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Get(messageId, requestContext, outBoxTimeout, args));
        }

        public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null)
        {
            var entry = Messages.First(m => m.Message.Id == id);
            entry.Dispatched = true;
        }

        public Task MarkDispatchedAsync(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        {
            MarkDispatched(id, requestContext, dispatchedAt, args);
            return Task.CompletedTask;
        }

        public Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        {
            foreach (var id in ids)
            {
                MarkDispatched(id, requestContext, dispatchedAt, args);
            }
            
            return Task.CompletedTask;
        }

        public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext, int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null)
        {
            return Messages.Where(m => !m.Dispatched).Select(m => m.Message);
        }

        public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext, 
            int pageSize = 100, int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OutstandingMessages(dispatchedSince, requestContext, pageSize, pageNumber));
        }
    }

    public class SpyOutboxEntry
    {
        public SpyOutboxEntry(Message message)
        {
            Message = message;
        }

        public Message Message { get; }

        public bool Dispatched { get; set; } = false;
    }
}
