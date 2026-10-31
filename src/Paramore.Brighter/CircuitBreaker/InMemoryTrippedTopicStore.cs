#region License

/* The MIT License (MIT)
Copyright © 2025 Michael Freeman <mike.c.freeman@gmail.com>

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

# endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// A trip store that holds its trips in memory, for one process.
/// </summary>
public class InMemoryTrippedTopicStore : IAmATrippedTopicStore
{
    private readonly object _lock = new();
    private readonly Dictionary<RoutingKey, Row> _trips = new();

    /// <inheritdoc />
    public Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var ended = MarkEnded(now, now - tripLength);
            var live = _trips.Where(trip => trip.Value.EndedAt is null)
                .Select(trip => new TopicTrip(trip.Key, trip.Value.TripTime))
                .ToList();
            return Task.FromResult(new TrippedTopicStoreRead(live, ended));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<TripWriteResult> results = trips.Select(Write).ToList();
            return Task.FromResult(results);
        }
    }

    private List<TopicTrip> MarkEnded(DateTimeOffset now, DateTimeOffset cutoff)
    {
        var expired = _trips.Where(trip => trip.Value.EndedAt is null && trip.Value.TripTime <= cutoff).ToList();
        foreach (var trip in expired)
            _trips[trip.Key] = trip.Value with { EndedAt = now };

        return expired.Select(trip => new TopicTrip(trip.Key, trip.Value.TripTime)).ToList();
    }

    private TripWriteResult Write(TopicTrip trip)
    {
        DateTimeOffset? prior = _trips.TryGetValue(trip.Topic, out var stored) ? stored.TripTime : null;
        var storedTripTime = prior.HasValue && prior.Value > trip.TripTime ? prior.Value : trip.TripTime;
        _trips[trip.Topic] = new Row(storedTripTime, null);
        return new TripWriteResult(trip.Topic, prior, storedTripTime, false);
    }

    private sealed record Row(DateTimeOffset TripTime, DateTimeOffset? EndedAt);
}
