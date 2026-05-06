# 57. Make InMemoryBox Abstract with Subclass-Specific Expiry Strategies

Date: 2026-05-05

## Status

Accepted

## Context

**Parent Requirement**: [specs/0027-Outbox-Limits/requirements.md](../../specs/0027-Outbox-Limits/requirements.md)

**Scope**: This ADR addresses the structural change to `InMemoryBox<T>` and its subclasses to support different expiry strategies, the configuration of the default `InMemoryOutbox` via `ProducersConfiguration`, and the error handling fix in `OutboxProducerMediator`.

`InMemoryBox<T>` is a concrete base class shared by `InMemoryOutbox` and `InMemoryInbox`. It provides a single `RemoveExpiredMessages` implementation that evicts entries based on `WriteTime` age — entries older than `EntryTimeToLive` are removed.

This is appropriate for the inbox (idempotency deduplication), but wrong for the outbox. The outbox stores messages that must be dispatched to a broker. Evicting an undispatched message means it is silently lost. The outbox already tracks dispatch state via `OutboxEntry.TimeFlushed` (`DateTimeOffset.MinValue` means "not yet dispatched"), but the base class expiry ignores this.

The forces at play:

1. **Inbox and outbox have fundamentally different eviction semantics.** The inbox can safely remove any entry older than its TTL — the entry has served its idempotency purpose. The outbox must never remove undispatched messages; only dispatched messages (where `TimeFlushed != DateTimeOffset.MinValue`) are safe to evict.
2. **The default `InMemoryOutbox` is created with hardcoded settings** in `ServiceCollectionExtensions.AddProducers` (lines ~296 and ~430). Users who don't provide an explicit outbox get `EntryLimit = 2048` with no way to change it.
3. **`OutboxProducerMediator.ClearOutbox` throws `NullReferenceException`** when a message has been compacted or evicted before dispatch, which is both the wrong exception type and unhelpful for diagnosis.

## Decision

### 1. Make `InMemoryBox<T>` Abstract

`InMemoryBox<T>` becomes an `abstract class`. The `RemoveExpiredMessages` method moves from a `private` concrete implementation to a `protected abstract` method. Each subclass must define its own eviction strategy.

The base class retains:
- The `ConcurrentDictionary<string, T> Requests` store
- The `ClearExpiredMessages()` method (the timer-gated trigger that calls `RemoveExpiredMessages` on a background thread)
- The `EnforceCapacityLimit()` method (triggers compaction on a background thread when `EntryLimit` is reached)
- All configuration properties: `EntryTimeToLive`, `ExpirationScanInterval`, `EntryLimit`, `CompactionPercentage`

Both `RemoveExpiredMessages` and `Compact` become `protected abstract` methods. This gives each subclass full control over both eviction strategies:
- **Expiry**: which entries to remove based on time
- **Compaction**: which entries to remove under capacity pressure

#### Locking Contract

The base class owns all locking. The existing `_cleanupRunningLockObject` and `Monitor.TryEnter` pattern stays in the base class. The abstract methods are called *inside* the lock:

- `ClearExpiredMessages()` checks the scan interval, then spawns a background thread that acquires the lock and calls `RemoveExpiredMessages(now)` inside it.
- `EnforceCapacityLimit()` checks `EntryLimit`, then spawns a background thread that acquires the lock and calls `Compact(entriesToRemove)` inside it.

Subclass overrides of `RemoveExpiredMessages` and `Compact` are guaranteed to be called under the lock and must not block or attempt to re-acquire it. They are pure eviction logic — no threading or synchronisation concerns.

```csharp
// Base class pattern (pseudocode):
private void RunRemoveExpiredMessages(DateTimeOffset now)
{
    if (Monitor.TryEnter(_cleanupRunningLockObject))
    {
        try { RemoveExpiredMessages(now); }
        finally { Monitor.Exit(_cleanupRunningLockObject); }
    }
}

private void RunCompact(int entriesToRemove)
{
    if (Monitor.TryEnter(_cleanupRunningLockObject))
    {
        try
        {
            _lastCompactionAttemptAt = _timeProvider.GetUtcNow();
            Compact(entriesToRemove);
        }
        finally { Monitor.Exit(_cleanupRunningLockObject); }
    }
}

protected abstract void RemoveExpiredMessages(DateTimeOffset now);
protected abstract void Compact(int entriesToRemove);
```

#### Compaction Cooldown

`EnforceCapacityLimit()` gains a `_lastCompactionAttemptAt` timestamp with a cooldown, mirroring the `_lastScanAt` pattern used by `ClearExpiredMessages()`. When the outbox cannot compact (because all entries are undispatched), this prevents spawning a background thread on every operation. The cooldown interval reuses `ExpirationScanInterval` — if compaction found nothing to remove, wait before trying again.

```csharp
protected void EnforceCapacityLimit()
{
    if (EntryLimit == -1) return;

    var now = _timeProvider.GetUtcNow();
    if (EntryCount >= EntryLimit)
    {
        if ((now - _lastCompactionAttemptAt) < ExpirationScanInterval) return;

        int newSize = (int)(EntryLimit * CompactionPercentage);
        int entriesToRemove = EntryCount - newSize;
        Task.Factory.StartNew(
            state => RunCompact((int)state!), entriesToRemove,
            CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
    }
}
```

#### Architecture Diagram

```
┌──────────────────────────────────────────┐
│   InMemoryBox<T> (abstract)              │
│                                          │
│   Knowing:                               │
│   - Requests dictionary                  │
│   - EntryLimit, EntryTimeToLive          │
│   - ExpirationScanInterval               │
│   - CompactionPercentage                 │
│   - _cleanupRunningLockObject (private)  │
│   - _lastScanAt, _lastCompactionAt       │
│                                          │
│   Doing:                                 │
│   - ClearExpiredMessages() (trigger)     │
│   - EnforceCapacityLimit() (trigger)     │
│   - RunRemoveExpiredMessages() (lock)    │
│   - RunCompact() (lock)                  │
│                                          │
│   Deciding (delegated to subclasses):    │
│   # RemoveExpiredMessages(now)           │
│   # Compact(entriesToRemove)             │
│                                          │
│   Support for EntryLimit = -1:           │
│   - EnforceCapacityLimit() skips         │
│     compaction when EntryLimit is -1     │
└──────────┬───────────────┬───────────────┘
           │               │
    ┌──────┴───────┐ ┌─────┴──────┐
    │InMemoryOutbox │ │InMemoryInbox│
    │              │ │            │
    │Expiry:       │ │Expiry:     │
    │ Only flushed │ │ Any entry  │
    │ entries      │ │ older than │
    │              │ │ TTL        │
    │Compaction:   │ │            │
    │ Only flushed │ │Compaction: │
    │ entries;     │ │ Oldest by  │
    │ oldest by    │ │ WriteTime  │
    │ TimeFlushed  │ │            │
    └──────────────┘ └────────────┘
```

### 2. `InMemoryOutbox` — Dispatched-Only Eviction and Compaction

**`RemoveExpiredMessages` override** removes entries where:
- `TimeFlushed != DateTimeOffset.MinValue` (the message has been dispatched), **and**
- `(now - TimeFlushed) >= EntryTimeToLive` (enough time has passed since dispatch)

Note: `EntryTimeToLive` changes meaning for the outbox — it measures time since *dispatch* (`TimeFlushed`), not time since *write* (`WriteTime`). This is intentional: a message should remain available for a reasonable period after dispatch for diagnostics and replay. The default 5-minute TTL applies from dispatch time.

**`Compact` override** only compacts dispatched messages. When the entry limit is reached:
- Filter to entries where `TimeFlushed != DateTimeOffset.MinValue` (dispatched only)
- Order by `TimeFlushed` (oldest dispatched first)
- Remove entries until the target size is reached

The `Compact(int entriesToRemove)` signature passes the number of entries the base class would like removed. The outbox implementation removes *up to* that many dispatched entries — it is best-effort. If there are not enough dispatched messages, the outbox compacts as many as it can. Undispatched messages are **never** removed by either expiry or compaction — they are protected until they are dispatched and aged out. This means the outbox can temporarily exceed `EntryLimit` if all entries are undispatched; this is the correct trade-off, as losing an undispatched message is a data loss bug.

When the outbox cannot compact enough entries, the compaction cooldown (see section 1) prevents `EnforceCapacityLimit()` from spawning a background thread on every subsequent operation. The cooldown reuses `ExpirationScanInterval` — if compaction found nothing to remove, it waits before trying again.

This fully addresses FR-4: "Undispatched messages must never be removed by expiry, regardless of age."

### 3. `InMemoryInbox` — Existing Algorithms

**`RemoveExpiredMessages` override** uses the current algorithm unchanged: remove entries where `(now - WriteTime) >= EntryTimeToLive`. This preserves existing behaviour.

**`Compact` override** uses the current algorithm unchanged: order by `WriteTime` (oldest first), remove entries until the target size is reached. The inbox has no concept of dispatch state, so all entries are eligible for compaction.

### 4. Support `EntryLimit = -1` to Disable Compaction

`EnforceCapacityLimit()` in the base class gains a guard: if `EntryLimit == -1`, return immediately without compacting. Expiry-based cleanup still runs (controlled by `ExpirationScanInterval` and delegated to the subclass override).

### 5. Configurable Default Outbox via `ProducersConfiguration`

#### Motivation: Pit of Success

Brighter always has an outbox — even when users call `Post` followed by `ClearOutbox`/`Dispatch` (rather than using a persistent outbox), the framework creates a default `InMemoryOutbox`. Users in high-throughput environments (particularly with RMQ or Kafka, where asynchronous confirmations mean messages linger in the outbox) are stumbling into the 2048-entry default limit without realising it exists. The configuration must be discoverable and self-documenting so that users fall into the pit of success.

#### `InMemoryBoxConfiguration` Record

Collate the related box properties into a single record type, making it clear they belong together and what their defaults mean:

```csharp
/// <summary>
/// Configuration for the default in-memory box used when no explicit outbox is provided.
/// Brighter always requires an outbox — even when using Post with ClearOutbox/Dispatch
/// rather than a persistent outbox. In those scenarios, an InMemoryOutbox is created
/// automatically with these settings. For high-throughput environments (especially with
/// brokers like RMQ or Kafka where asynchronous confirmations keep messages in the outbox
/// longer), you may need to increase the EntryLimit or set it to -1 to disable compaction.
/// </summary>
/// <param name="EntryLimit">
/// Maximum number of entries before compaction runs. Default is 2048.
/// Set to -1 to disable compaction entirely (expiry still runs).
/// </param>
/// <param name="EntryTimeToLive">
/// How long after dispatch (for outbox) or after write (for inbox) before an entry
/// can be evicted by the expiry scan. Default is 5 minutes.
/// </param>
/// <param name="ExpirationScanInterval">
/// Minimum interval between expiry scans. Also used as the cooldown between
/// compaction attempts. Default is 10 minutes.
/// </param>
/// <param name="CompactionPercentage">
/// Target size as a fraction of EntryLimit after compaction.
/// 0.0 = remove as many eligible entries as possible.
/// 0.5 = compact down to 50% of the limit (default).
/// </param>
public record InMemoryBoxConfiguration(
    int EntryLimit = 2048,
    TimeSpan? EntryTimeToLive = null,
    TimeSpan? ExpirationScanInterval = null,
    double CompactionPercentage = 0.5
)
{
    /// <summary>How long before an entry can be evicted. Defaults to 5 minutes.</summary>
    public TimeSpan EntryTimeToLive { get; init; } = EntryTimeToLive ?? TimeSpan.FromMinutes(5);

    /// <summary>Minimum interval between scans. Defaults to 10 minutes.</summary>
    public TimeSpan ExpirationScanInterval { get; init; } = ExpirationScanInterval ?? TimeSpan.FromMinutes(10);
}
```

Using a record gives us immutability after construction, `with` expression support for overriding individual values, and a compact declaration.

#### Property on `ProducersConfiguration`

Add a single property to `ProducersConfiguration` (not the `IAmProducersConfiguration` interface — this is only relevant when no explicit outbox is provided):

```csharp
/// <summary>
/// Configuration for the default InMemoryOutbox that Brighter creates when no explicit
/// outbox is provided. Brighter always uses an outbox — even for Post with
/// ClearOutbox/Dispatch workflows — so this controls the in-memory box that backs
/// those operations.
///
/// Defaults: EntryLimit = 2048, EntryTimeToLive = 5 min,
/// ExpirationScanInterval = 10 min, CompactionPercentage = 0.5.
///
/// For high-throughput scenarios (RMQ/Kafka with async confirmations), increase
/// EntryLimit or set it to -1 to disable compaction.
/// </summary>
public InMemoryBoxConfiguration DefaultBoxConfiguration { get; set; } = new();
```

Users configure it naturally:

```csharp
// Increase the limit
services.AddBrighter()
    .AddProducers(config =>
    {
        config.DefaultBoxConfiguration = new InMemoryBoxConfiguration(EntryLimit: 8192);
        config.ProducerRegistry = ...;
    });

// Disable compaction entirely
services.AddBrighter()
    .AddProducers(config =>
    {
        config.DefaultBoxConfiguration = new InMemoryBoxConfiguration(EntryLimit: -1);
        config.ProducerRegistry = ...;
    });
```

#### Application in `ServiceCollectionExtensions.AddProducers`

There are two `AddProducers` overloads that both create a default outbox:

**First overload** (`Action<ProducersConfiguration>`, line ~247): constructs a local `ProducersConfiguration` directly, so the concrete type and `DefaultBoxConfiguration` are available.

**Second overload** (`Func<IServiceProvider, ProducersConfiguration>`, line ~376): the `configure(sp)` call returns a `ProducersConfiguration`, but it is registered as `IAmProducersConfiguration` in DI. The outbox factory lambda at line ~423 resolves `IAmProducersConfiguration`, losing access to the concrete type. To fix this, the outbox creation in the second overload casts back to `ProducersConfiguration` (which is safe — the factory at line ~389 always returns one). If the cast fails (defensive), fall back to the built-in defaults.

Both overloads use the same `CreateDefaultOutbox` helper:

```csharp
var outbox = busConfiguration.Outbox ?? CreateDefaultOutbox(busConfiguration);

// where:
static InMemoryOutbox CreateDefaultOutbox(IAmProducersConfiguration config)
{
    var outbox = new InMemoryOutbox(TimeProvider.System);
    if (config is ProducersConfiguration { DefaultBoxConfiguration: var boxConfig })
    {
        outbox.EntryLimit = boxConfig.EntryLimit;
        outbox.EntryTimeToLive = boxConfig.EntryTimeToLive;
        outbox.ExpirationScanInterval = boxConfig.ExpirationScanInterval;
        outbox.CompactionPercentage = boxConfig.CompactionPercentage;
    }
    return outbox;
}
```

When the cast succeeds (the normal case), the user's configuration is applied. When it fails (a custom `IAmProducersConfiguration` implementation), the `InMemoryOutbox` uses its built-in defaults — the same behaviour as today.

### 6. Fix `NullReferenceException` in `OutboxProducerMediator`

Replace the `throw new NullReferenceException(...)` in both `ClearOutbox` and `ClearOutboxAsync` with error logging. When messages are missing from the outbox:

1. Log an error with the missing message IDs.
2. Continue dispatching the messages that **were** found.

This is the correct behaviour because the messages may have been dispatched by another mechanism (e.g. the sweeper), or compacted under memory pressure. Throwing halts dispatch of all remaining messages in the batch, making a partial failure into a total failure.

### Requirement Coverage

| Decision | Requirement |
|----------|-------------|
| 1. Make `InMemoryBox<T>` abstract | FR-3 |
| 2. Outbox dispatched-only eviction and compaction | FR-4 |
| 3. Inbox existing algorithms | FR-5 |
| 4. `EntryLimit = -1` | FR-2 |
| 5. Configurable via `ProducersConfiguration` | FR-1 |
| 6. Fix `NullReferenceException` | FR-6 |

## Consequences

### Positive

- **Undispatched outbox messages are safe from both expiry and compaction.** The outbox only evicts messages that have been confirmed as dispatched, fully eliminating the root cause of #4115.
- **Each subclass owns its eviction strategy.** Making `InMemoryBox<T>` abstract forces future subclasses to make an explicit decision about eviction — there is no inherited default to silently get wrong.
- **Users can tune the default outbox.** The new `ProducersConfiguration` properties mean users no longer need to construct an `InMemoryOutbox` themselves to change limits.
- **Missing messages are handled gracefully.** Logging instead of throwing means partial outbox misses don't cascade into total dispatch failure.

### Negative

- **Breaking change for any external subclass of `InMemoryBox<T>`.** Any third-party code that extends `InMemoryBox<T>` will need to implement both `RemoveExpiredMessages` and `Compact`. This is unlikely to affect anyone in practice, as the class is not designed for external extension, but it is a binary-breaking change.
- **`InMemoryInbox.Add` now calls `EnforceCapacityLimit()`.** Previously the inbox only performed TTL-based eviction; compaction was never triggered. After this change, an inbox that exceeds `EntryLimit` (default 2048) will compact on the next `Add`, removing the oldest entries by `WriteTime`. Users storing more than 2048 idempotency tokens should either increase `EntryLimit` or set it to `-1` to disable compaction.
- **More configuration surface.** `DefaultBoxConfiguration` on `ProducersConfiguration` adds API surface. Mitigated by sensible defaults matching current behaviour.

### Risks and Mitigations

- **Risk**: With `EntryLimit = -1` and long-running processes, the outbox could grow unbounded if messages are never dispatched.
  - **Mitigation**: Expiry still runs and removes dispatched messages. The `-1` option is documented as appropriate only when users have a reliable dispatch mechanism. The outbox is primarily for dev/test; production users should use a persistent outbox.
- **Risk**: When the outbox is at capacity with mostly undispatched messages, compaction cannot free space.
  - **Mitigation**: The compaction cooldown (reusing `ExpirationScanInterval`) prevents repeated no-op compaction attempts. Users in high-throughput scenarios should set `EntryLimit = -1` to disable compaction entirely, relying on expiry of dispatched messages for cleanup.
- **Risk**: Changing `InMemoryBox<T>` from concrete to abstract could break deserialization or reflection-based usage.
  - **Mitigation**: `InMemoryBox<T>` is never directly instantiated in the codebase — only through `InMemoryOutbox` and `InMemoryInbox`. A search for `new InMemoryBox` confirms no direct construction.

## Alternatives Considered

### 1. Make `RemoveExpiredMessages` `protected virtual` Instead of `abstract`

Keep `InMemoryBox<T>` concrete with a virtual method and default implementation. This would be non-breaking for external subclasses.

**Rejected because**: A virtual method with a default implementation means future subclasses silently inherit inbox-style eviction, which is wrong for outbox-like use cases. Making it abstract forces an explicit choice, following the principle of "reveal intention; be explicit to support future readers."

### 2. Use a Strategy/Delegate Pattern

Pass a `Func<ConcurrentDictionary<string, T>, DateTimeOffset, TimeSpan, IEnumerable<string>>` eviction strategy to the constructor.

**Rejected because**: This adds a layer of indirection without benefit. The two subclasses have clear, stable, different strategies. A delegate would obscure the intent and make the code harder to understand. "If the implementation is hard to explain, it's a bad idea."

### 3. Do Nothing — Document That Users Should Provide Their Own InMemoryOutbox

Let users construct and configure `InMemoryOutbox` themselves.

**Rejected because**: This is the current workaround and it fails in practice — users don't realise the default outbox has a 2048 limit until messages are silently lost. The pit of success should be the default configuration.

## References

- Requirements: [specs/0027-Outbox-Limits/requirements.md](../../specs/0027-Outbox-Limits/requirements.md)
- Issue: [#4115](https://github.com/BrighterCommand/Brighter/issues/4115)
- Design Inspiration: [MS System.Extensions.Caching.Memory.MemoryCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycache) (cited in InMemoryOutbox source)
