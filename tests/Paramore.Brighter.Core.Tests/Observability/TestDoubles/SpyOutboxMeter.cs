using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

/// <summary>
/// Spy that records every Outbox metric call for assertion in tests.
/// Thread-safe: all list access is behind a lock (for AC-25 concurrent add tests).
/// </summary>
public class SpyOutboxMeter : IAmABrighterOutboxMeter, IDisposable
{
    private readonly object _lock = new();
    private readonly List<RoutingKey> _added = new();
    private readonly List<(RoutingKey Destination, OutboxClearSource Source)> _cleared = new();
    private readonly List<(TimeSpan Wait, RoutingKey Destination, OutboxClearSource Source)> _durations = new();

    public bool Enabled { get; set; } = true;
    public bool Disposed { get; private set; }

    public IReadOnlyList<RoutingKey> Added { get { lock (_lock) return _added.ToList(); } }
    public IReadOnlyList<(RoutingKey Destination, OutboxClearSource Source)> Cleared { get { lock (_lock) return _cleared.ToList(); } }
    public IReadOnlyList<(TimeSpan Wait, RoutingKey Destination, OutboxClearSource Source)> Durations { get { lock (_lock) return _durations.ToList(); } }

    public void AddMessageAdded(RoutingKey destination) { lock (_lock) _added.Add(destination); }
    public void AddMessageCleared(RoutingKey destination, OutboxClearSource clearSource) { lock (_lock) _cleared.Add((destination, clearSource)); }
    public void RecordPublishDuration(TimeSpan wait, RoutingKey destination, OutboxClearSource clearSource) { lock (_lock) _durations.Add((wait, destination, clearSource)); }

    public void Dispose() => Disposed = true;
}
