#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

internal sealed class InMemoryCancellationOutbox(TimeProvider timeProvider)
    : InMemoryOutbox(timeProvider), IAmAnOutboxAsync<Message, CommittableTransaction>
{
    public List<CancellationToken> AddTokens { get; } = [];
    public List<CancellationToken> MarkDispatchedTokens { get; } = [];
    public Action? OnAdding { get; set; }
    public Action? OnMarkingDispatched { get; set; }

    Task IAmAnOutboxAsync<Message, CommittableTransaction>.AddAsync(
        IEnumerable<Message> messages,
        RequestContext? requestContext,
        int outBoxTimeout,
        IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider,
        CancellationToken cancellationToken)
    {
        AddTokens.Add(cancellationToken);
        OnAdding?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return base.AddAsync(messages, requestContext, outBoxTimeout, transactionProvider, cancellationToken);
    }

    Task IAmAnOutboxAsync<Message, CommittableTransaction>.MarkDispatchedAsync(
        Id id,
        RequestContext requestContext,
        DateTimeOffset? dispatchedAt,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        MarkDispatchedTokens.Add(cancellationToken);
        OnMarkingDispatched?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return base.MarkDispatchedAsync(id, requestContext, dispatchedAt, args, cancellationToken);
    }
}
