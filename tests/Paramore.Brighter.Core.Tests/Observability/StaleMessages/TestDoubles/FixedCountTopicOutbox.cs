#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// An async outbox that can count per topic and answers every count with the same results, whatever it is asked for,
/// so a test can give the monitor a result that breaks the outbox contract.
/// </summary>
public class FixedCountTopicOutbox : IAmAnOutboxAsync<Message, CommittableTransaction>, IAmATopicCountingOutbox
{
    private readonly int _overallCount;
    private readonly IReadOnlyDictionary<RoutingKey, int> _countsByTopic;

    /// <summary>Creates the double.</summary>
    /// <param name="overallCount">What every call of the overall count returns.</param>
    /// <param name="countsByTopic">What every call of the per-topic count returns.</param>
    public FixedCountTopicOutbox(int overallCount, IReadOnlyDictionary<RoutingKey, int> countsByTopic)
    {
        _overallCount = overallCount;
        _countsByTopic = countsByTopic;
    }

    public bool ContinueOnCapturedContext { get; set; }

    public IAmABrighterTracer? Tracer
    {
        set { }
    }

    public Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince, RequestContext? requestContext,
        int maxCount = 100, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_overallCount);

    public Task<IReadOnlyDictionary<RoutingKey, int>> GetOutstandingMessageCountsByTopicAsync(
        IReadOnlyDictionary<RoutingKey, TimeSpan> dispatchedSinceByTopic,
        RequestContext? requestContext,
        int maxCountPerTopic,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_countsByTopic);

    public Task AddAsync(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task AddAsync(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(Id[] messageIds, RequestContext requestContext,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100, int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Message> GetAsync(Id messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<Message>> GetAsync(IEnumerable<Id> messageIds, RequestContext requestContext,
        int outBoxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task MarkDispatchedAsync(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince,
        RequestContext requestContext, int pageSize = 100, int pageNumber = 1,
        IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
