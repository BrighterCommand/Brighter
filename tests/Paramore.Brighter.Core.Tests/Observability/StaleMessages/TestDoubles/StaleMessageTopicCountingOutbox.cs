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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// A <see cref="StaleMessageCountingOutbox"/> that can also count its stale messages per topic, and records each call
/// of its per-topic count.
/// </summary>
public class StaleMessageTopicCountingOutbox : StaleMessageCountingOutbox, IAmATopicCountingOutbox
{
    private readonly ConcurrentQueue<StaleTopicCountCall> _topicCountCalls = new();
    private readonly TimeProvider? _clock;

    /// <summary>Creates the double.</summary>
    /// <param name="clock">When given, each recorded call carries the time on this clock when it was made.</param>
    public StaleMessageTopicCountingOutbox(TimeProvider? clock = null) : base(clock)
    {
        _clock = clock;
    }

    /// <summary>Each call of the per-topic count, in the order the calls were made.</summary>
    public IReadOnlyCollection<StaleTopicCountCall> TopicCountCalls => _topicCountCalls;

    public Task<IReadOnlyDictionary<RoutingKey, int>> GetOutstandingMessageCountsByTopicAsync(
        IReadOnlyDictionary<RoutingKey, TimeSpan> dispatchedSinceByTopic,
        RequestContext? requestContext,
        int maxCountPerTopic,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        _topicCountCalls.Enqueue(new StaleTopicCountCall(dispatchedSinceByTopic, requestContext, maxCountPerTopic, args, cancellationToken, _clock?.GetUtcNow()));
        return Task.FromResult<IReadOnlyDictionary<RoutingKey, int>>(
            dispatchedSinceByTopic.Keys.ToDictionary(topic => topic, _ => 0));
    }
}
