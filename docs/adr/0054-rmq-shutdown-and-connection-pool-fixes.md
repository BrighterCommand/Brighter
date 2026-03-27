# 54. Fix RMQ Shutdown Deadlock and Connection Pool Race Condition

Date: 2026-03-11

## Status

Accepted

## Context

**Parent Requirement**: [specs/0024-rmq_shutdown/requirements.md](../../specs/0024-rmq_shutdown/requirements.md)

**Scope**: This ADR addresses two related bugs in the RabbitMQ messaging gateway: a sync-over-async deadlock that prevents clean shutdown (#3684), and a connection pool race condition that can dispose active connections (#4024).

### Bug 1: Shutdown Deadlock (#3684)

When a ServiceActivator using the Proactor (async message pump) receives a shutdown signal (Ctrl+C), the process hangs indefinitely. The root cause is a sync-over-async deadlock in the dispose chain:

1. The Proactor's `EventLoop` runs inside `BrighterAsyncContext.Run()`, which provides a single-threaded `BrighterTaskScheduler` and a custom `BrighterSynchronizationContext`.
2. On `MT_QUIT`, the Proactor calls `Channel.Dispose()` (line 218 of `Proactor.cs`).
3. `ChannelAsync.Dispose(bool)` calls `_messageConsumer.DisposeAsync()` **fire-and-forget** — the `ValueTask` is never awaited (line 205 of `ChannelAsync.cs`).
4. The fire-and-forget `DisposeAsync()` posts its continuation to the `BrighterSynchronizationContext` via `SynchronizationContext.Post()`, incrementing `_outstandingOperations`.
5. `RmqMessageConsumer.DisposeAsync()` calls `Dispose(true)` on `RmqMessageGateway`.
6. `RmqMessageGateway.Dispose(bool)` performs **blocking sync-over-async**: `Channel?.AbortAsync().Wait()` and `RemoveConnectionAsync().GetAwaiter().GetResult()` (lines 206-212 of `RmqMessageGateway.cs`).
7. `RemoveConnectionAsync` internally `await`s without `ConfigureAwait(false)`, so its continuations are posted back to the `BrighterSynchronizationContext`.
8. The single `BrighterTaskScheduler` thread is blocked in `.GetAwaiter().GetResult()` — the continuations can never run — **deadlock**.
9. `BrighterAsyncContext.Run()` never returns because `_outstandingOperations > 0`, so `Proactor.Run()` never returns, the Performer task never completes, `Dispatcher._controlTask` hangs in `Task.WaitAny()`, and the process is stuck.

The Reactor (sync pump) is **not affected** because it uses `Channel` (sync) which calls `_messageConsumer.Dispose()` synchronously, and the sync `RmqMessageGateway.Dispose(bool)` calls synchronous `Channel.Abort()` and `RemoveConnection()` — no async operations, no deadlock.

### Bug 2: Connection Pool Race (#4024)

`RmqMessageGatewayConnectionPool` uses a static `Dictionary<string, PooledConnection>` keyed by `{user}.{password}.{host}.{port}.{vhost}`. All consumers/producers to the same broker share one entry.

The `ConnectionShutdown` event handler closure captures the `connectionId` string but does not verify the connection instance. A stale handler from a replaced connection can dispose the current connection:

1. Connection A shuts down, `TryRemoveConnection` is called, A is disposed and removed from pool.
2. Connection B is created for the same endpoint and added to the pool.
3. Connection A's shutdown event was already queued before the handler was unsubscribed.
4. The stale handler fires and calls `TryRemoveConnection(connectionId)`, which disposes **Connection B**.

Additionally, the sync variant's `RemoveConnection` checks `ContainsKey` **outside** the lock (TOCTOU issue).

## Decision

### 1. Add `IAsyncDisposable` to `IAmAChannelAsync`

**Role**: Channel — responsible for **knowing** about its consumer and **doing** proper lifecycle management.

`IAmAChannelAsync` currently extends only `IAmAChannel : IDisposable`. We add `IAsyncDisposable` to the interface:

```csharp
public interface IAmAChannelAsync : IAmAChannel, IAsyncDisposable
{
    // existing async methods...
}
```

`ChannelAsync` then implements `DisposeAsync()`:

```csharp
public virtual async ValueTask DisposeAsync()
{
    await _messageConsumer.DisposeAsync();
    GC.SuppressFinalize(this);
}
```

This replaces the current fire-and-forget `Dispose(bool)` path for async consumers. The synchronous `Dispose()` remains for backward compatibility but delegates to `DisposeAsync` safely using `Task.Run` to escape any ambient `SynchronizationContext`.

### 2. Proactor calls `await Channel.DisposeAsync()` on `MT_QUIT`

**Role**: Proactor — responsible for **coordinating** the async message pump lifecycle.

The Proactor's `Channel` property is already typed as `IAmAChannelAsync`. Since `IAmAChannelAsync` now extends `IAsyncDisposable`, the `MT_QUIT` handler changes from:

```csharp
// Before
Channel.Dispose();
```

to:

```csharp
// After
await Channel.DisposeAsync();
```

This ensures the entire dispose chain runs asynchronously within the `BrighterAsyncContext` event loop — no sync-over-async, no deadlock.

### 3. Add `ConfigureAwait(false)` to connection pool async methods

**Role**: Connection Pool — responsible for **knowing** about active connections and **doing** thread-safe lifecycle management.

All `await` calls in `RmqMessageGatewayConnectionPool` (async variant) must use `ConfigureAwait(false)` to prevent capturing ambient `SynchronizationContext`. This is defence-in-depth — even if a caller accidentally uses sync-over-async, the continuations will run on the thread pool rather than deadlocking.

Affected methods:
- `GetOrCreateConnectionAsync` — `await s_lock.WaitAsync().ConfigureAwait(false)`
- `RemoveConnectionAsync` — `await s_lock.WaitAsync().ConfigureAwait(false)`, `await TryRemoveConnectionAsync().ConfigureAwait(false)`
- `TryRemoveConnectionAsync` — `await pooledConnection.Connection.DisposeAsync().ConfigureAwait(false)`
- `CreateConnectionAsync` — `await TryRemoveConnectionAsync().ConfigureAwait(false)`, `await connectionFactory.CreateConnectionAsync().ConfigureAwait(false)`

### 4. Guard `ConnectionShutdown` handler with `ReferenceEquals`

**Role**: Connection Pool — responsible for **deciding** whether a shutdown event applies to the current pooled connection.

The `ShutdownHandler` closure must verify the connection being shut down is the same instance currently in the pool:

```csharp
// Async variant
async Task ShutdownHandler(object sender, ShutdownEventArgs e)
{
    await s_lock.WaitAsync().ConfigureAwait(false);
    try
    {
        if (s_connectionPool.TryGetValue(connectionId, out var pooled)
            && ReferenceEquals(pooled.Connection, sender))
        {
            await TryRemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
    }
    finally
    {
        s_lock.Release();
    }
}
```

```csharp
// Sync variant
void ShutdownHandler(object? sender, ShutdownEventArgs e)
{
    lock (s_lock)
    {
        if (s_connectionPool.TryGetValue(connectionId, out var pooled)
            && ReferenceEquals(pooled.Connection, sender))
        {
            TryRemoveConnection(connectionId);
        }
    }
}
```

The `sender` parameter of the `ConnectionShutdown` event is the `IConnection` instance that is shutting down. By comparing it with `ReferenceEquals` against what's currently in the pool, stale handlers become no-ops.

### 5. Fix TOCTOU in sync `RemoveConnection`

**Role**: Connection Pool — responsible for **doing** thread-safe removal.

Move the `ContainsKey` check inside the lock:

```csharp
// Before (TOCTOU)
public void RemoveConnection(ConnectionFactory connectionFactory)
{
    var connectionId = GetConnectionId(connectionFactory);
    if (s_connectionPool.ContainsKey(connectionId))  // outside lock!
    {
        lock (s_lock)
        {
            TryRemoveConnection(connectionId);
        }
    }
}

// After
public void RemoveConnection(ConnectionFactory connectionFactory)
{
    var connectionId = GetConnectionId(connectionFactory);
    lock (s_lock)
    {
        TryRemoveConnection(connectionId);  // TryGetValue inside is safe if missing
    }
}
```

`TryRemoveConnection` already uses `TryGetValue` and returns early if the key is missing, so the outer check is redundant and racy.

Apply the same simplification to the async variant's `RemoveConnectionAsync`.

### 6. RmqMessageGateway async variant: ensure `DisposeAsync` is the primary path

**Role**: Gateway — responsible for **doing** channel and connection cleanup.

`RmqMessageGateway.DisposeAsync()` already exists and correctly uses `await`:

```csharp
public virtual async ValueTask DisposeAsync()
{
    if (Channel != null)
    {
        await Channel.AbortAsync();
        await Channel.DisposeAsync();
        Channel = null;
    }
    await new RmqMessageGatewayConnectionPool(...)
        .RemoveConnectionAsync(_connectionFactory);
}
```

The sync `Dispose(bool)` remains for callers that genuinely need synchronous disposal (e.g. the Reactor path via `Channel` → sync `RmqMessageConsumer.Dispose()` → sync `RmqMessageGateway.Dispose(bool)` which delegates to the sync connection pool). No changes needed to the sync `Dispose(bool)` — it only deadlocks when called from within `BrighterAsyncContext`, which the Proactor fix (decision 2) eliminates.

### Architecture Overview

```
Ctrl+C → ServiceActivatorHostedService.StopAsync()
       → Dispatcher.End()
       → Consumer.Shut() → ChannelAsync.Stop() [enqueues MT_QUIT]
       → Proactor EventLoop (inside BrighterAsyncContext):
           receives MT_QUIT
           → await Channel.DisposeAsync()              ← Decision 2 (was: Channel.Dispose())
           → await ChannelAsync.DisposeAsync()          ← Decision 1 (new method)
           → await RmqMessageConsumer.DisposeAsync()
           → await RmqMessageGateway.DisposeAsync()     ← Decision 6 (existing, now reachable)
             → await Channel.AbortAsync()
             → await RemoveConnectionAsync()             ← Decision 3 (ConfigureAwait(false))
               → ShutdownHandler validates instance      ← Decision 4 (ReferenceEquals guard)
           → Connection closed, background threads stop
       → BrighterAsyncContext.Run() returns
       → Performer task completes
       → Dispatcher._controlTask completes
       → Process exits cleanly
```

### Components Affected

| Component | Change | Package |
|-----------|--------|---------|
| `IAmAChannelAsync` | Add `: IAsyncDisposable` | Paramore.Brighter |
| `ChannelAsync` | Implement `DisposeAsync()` | Paramore.Brighter |
| `Proactor` | `await Channel.DisposeAsync()` on MT_QUIT | Paramore.Brighter.ServiceActivator |
| `RmqMessageGatewayConnectionPool` (async) | `ConfigureAwait(false)`, `ReferenceEquals` guard, simplify `RemoveConnectionAsync` | Paramore.Brighter.MessagingGateway.RMQ.Async |
| `RmqMessageGatewayConnectionPool` (sync) | `ReferenceEquals` guard, fix TOCTOU in `RemoveConnection` | Paramore.Brighter.MessagingGateway.RMQ.Sync |

### Impact on Other Transports

Adding `IAsyncDisposable` to `IAmAChannelAsync` means all implementations of `IAmAChannelAsync` must implement `DisposeAsync()`. A codebase-wide analysis confirms the blast radius is contained:

1. **No transport implements `IAmAChannelAsync` directly.** Every transport's channel factory (RMQ, Kafka, AWS SQS V3/V4, Azure Service Bus, GCP Pub/Sub, Redis, MsSql, PostgreSQL, MQTT, RocketMQ, InMemory) creates `new ChannelAsync(...)` — the concrete class. The only subclass is a test double (`FailingChannelAsync` in core tests), which will inherit the new `DisposeAsync()` from its base class.

2. **All async message consumers already implement `DisposeAsync()`** because `IAmAMessageConsumerAsync` already extends `IAsyncDisposable`. Verified for all 13 consumer implementations:
   - RMQ Async (`RmqMessageConsumer`), Kafka (`KafkaMessageConsumer`), AWS SQS V3 (`SqsMessageConsumer`), AWS SQS V4 (`SqsMessageConsumer`), Azure Service Bus (`AzureServiceBusConsumer`), GCP Pull (`GcpPullMessageConsumer`), GCP Stream (`GcpPubSubStreamMessageConsumer`), Redis (`RedisMessageConsumer`), MsSql (`MsSqlMessageConsumer`), PostgreSQL (`PostgresMessageConsumer`), MQTT (`MqttMessageConsumer`), RocketMQ (`RocketMessageConsumer`), InMemory (`InMemoryMessageConsumer`).

3. **`ChannelAsync.DisposeAsync()` delegates to `await _messageConsumer.DisposeAsync()`**, which is the existing, already-working method on every transport's consumer. No gateway projects require additional changes.

The interface change is technically breaking for any **external** code that implements `IAmAChannelAsync` directly (they would need to add `DisposeAsync()`), but this is unlikely — `ChannelAsync` is the standard concrete implementation and all known usages go through it.

## Consequences

### Positive

- The shutdown deadlock is eliminated — the Proactor's dispose path is fully async.
- `ConfigureAwait(false)` provides defence-in-depth against future sync-over-async regressions.
- The `ReferenceEquals` guard prevents stale shutdown handlers from disposing active connections, fixing CI test flakiness.
- The TOCTOU fix in `RemoveConnection` eliminates a subtle race condition.
- The fix is minimal and surgical — no architectural redesign of the connection pool or dispatcher.

### Negative

- Adding `IAsyncDisposable` to `IAmAChannelAsync` is a breaking change for any external code that implements the interface directly (unlikely — `ChannelAsync` is the standard implementation).
- The sync `Dispose(bool)` on `RmqMessageGateway` (async variant) still contains sync-over-async patterns. This is acceptable because it's only reachable from sync callers that don't run inside `BrighterAsyncContext`.

### Risks and Mitigations

- **Risk**: Other transports' `IAmAMessageConsumerAsync` implementations may not implement `DisposeAsync()` correctly.
  - **Mitigation**: `IAmAMessageConsumerAsync` already extends `IAsyncDisposable`; all existing implementations already have `DisposeAsync()`.
- **Risk**: The `ReferenceEquals` guard could prevent legitimate shutdown cleanup if the RabbitMQ client reuses connection objects.
  - **Mitigation**: RabbitMQ client creates new `IConnection` instances per `CreateConnection()` call; it does not pool or reuse them internally.

## Alternatives Considered

### 1. Use `Task.Run` to escape `BrighterSynchronizationContext`

Instead of making the Proactor's dispose path async, wrap the synchronous dispose in `Task.Run` to move it off the `BrighterTaskScheduler` thread:

```csharp
Task.Run(() => Channel.Dispose()).GetAwaiter().GetResult();
```

**Rejected** because: This is a workaround, not a fix. It hides the fundamental problem (calling sync dispose on an async consumer) and introduces thread pool dependency during shutdown. The proper fix is to use the async dispose path that already exists.

### 2. Reference-counted connection pool

Track each `IConnection` instance with a reference count, only disposing when all consumers/producers release their reference.

**Rejected** because: Over-engineered for the current problem. The `ReferenceEquals` guard in the shutdown handler is sufficient to prevent stale handlers from disposing active connections. Reference counting could be considered as a future enhancement if connection sharing semantics become more complex.

### 3. Remove static connection pool entirely

Make each consumer/producer own its own connection.

**Rejected** because: This would be a significant behavioral change. RabbitMQ best practice is to share connections and use multiple channels. The static pool serves this purpose correctly in production.

## References

- Requirements: [specs/0024-rmq_shutdown/requirements.md](../../specs/0024-rmq_shutdown/requirements.md)
- Issue #3684: [ServiceActivatorHostedService does not exit with RabbitMQ subscription](https://github.com/BrighterCommand/Brighter/issues/3684)
- Issue #4024: [RMQ connection pool: stale ConnectionShutdown handler can dispose a newer connection](https://github.com/BrighterCommand/Brighter/issues/4024)
- [Proactor Pattern](https://www.dre.vanderbilt.edu/~schmidt/PDF/Proactor.pdf)
- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
