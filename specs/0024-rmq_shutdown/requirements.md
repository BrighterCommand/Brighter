# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issues**: #3684, #4024

## Problem Statement

As a developer using Brighter's ServiceActivator with RabbitMQ, I would like the service to shut down cleanly when I press Ctrl+C, so that the process exits without requiring a force-kill.

There are two related bugs:

### Bug 1: Shutdown hang — sync-over-async deadlock (#3684)

After Ctrl+C the message pump logs "Finished running message loop" but the process never terminates. The root cause is a **sync-over-async deadlock** inside `BrighterAsyncContext`'s single-threaded scheduler:

1. The Proactor's `EventLoop` runs inside `BrighterAsyncContext.Run()` on a single-threaded `BrighterTaskScheduler`.
2. When `MT_QUIT` is received, the Proactor calls `Channel.Dispose()`.
3. `ChannelAsync.Dispose(bool)` calls `_messageConsumer.DisposeAsync()` **fire-and-forget** — the `ValueTask` is not awaited (`ChannelAsync.cs:205`).
4. `RmqMessageConsumer.DisposeAsync()` eventually calls `Dispose(true)` on `RmqMessageGateway`.
5. `RmqMessageGateway.Dispose(bool)` does blocking sync-over-async: `Channel?.AbortAsync().Wait()` and `RemoveConnectionAsync().GetAwaiter().GetResult()` (`RmqMessageGateway.cs:206-212`).
6. `RemoveConnectionAsync` internally `await`s without `ConfigureAwait(false)`, so continuations are posted back to the `BrighterSynchronizationContext`.
7. But the **only thread** serving that context is blocked in `.GetAwaiter().GetResult()` — **classic deadlock**.
8. `BrighterAsyncContext.Run()` never returns because `_outstandingOperations > 0`, so `Proactor.Run()` never returns, the Performer task never completes, `Dispatcher._controlTask` hangs in `Task.WaitAny()`, and the process is stuck.

Confirmed still present in v10.3.0 (reported 2026-03-11).

### Bug 2: Connection pool race condition (#4024)

The static `RmqMessageGatewayConnectionPool` has a race where a stale `ConnectionShutdown` event handler can dispose a *newer* connection sharing the same pool key (`{user}.{password}.{host}.{port}.{vhost}`), causing `OperationInterruptedException` ("Connection close forced").

The `ShutdownHandler` closure captures the `connectionId` string but does not verify the connection instance. If:
1. Connection A shuts down and is replaced by Connection B in the pool (same key)
2. Connection A's shutdown event was already queued before the handler was unsubscribed
3. The stale handler fires and calls `TryRemoveConnection(connectionId)`, which disposes **Connection B**

Additionally, `RemoveConnection` (sync variant) checks `ContainsKey` **outside** the lock (TOCTOU issue).

## Proposed Solution

### For #3684 (shutdown deadlock)
- Eliminate the sync-over-async chain in the Proactor's dispose path. The Proactor should `await` channel disposal asynchronously rather than calling synchronous `Dispose()` which triggers blocking `.Wait()` / `.GetAwaiter().GetResult()` calls inside the `BrighterAsyncContext`.
- Ensure `ChannelAsync` properly awaits `DisposeAsync()` instead of fire-and-forget.
- Add `ConfigureAwait(false)` to connection pool async methods so continuations don't capture `BrighterSynchronizationContext`.

### For #4024 (pool race condition)
- Guard the `ShutdownHandler` with a `ReferenceEquals` check: only remove/dispose the connection if the instance being shut down is the same one currently in the pool.
- Move the `ContainsKey` check inside the lock in the sync variant's `RemoveConnection`.

## Requirements

### Functional Requirements

1. **Clean shutdown**: When `ServiceActivatorHostedService.StopAsync()` completes, the RMQ connection must be closed and the process must exit without requiring a force-kill.
2. **No sync-over-async in async pipeline**: The Proactor dispose path must not call blocking `.Wait()`, `.GetAwaiter().GetResult()`, or `BrighterAsyncContext.Run()` on async RMQ operations. The async channel and consumer must be disposed via `await DisposeAsync()`.
3. **ChannelAsync must properly dispose**: `ChannelAsync` must not fire-and-forget `DisposeAsync()`. It must either await it (in an async dispose path) or properly synchronize it.
4. **ConfigureAwait(false) on library internals**: `RmqMessageGatewayConnectionPool` async methods must use `ConfigureAwait(false)` to avoid capturing ambient `SynchronizationContext`.
5. **Connection pool instance safety**: The `ConnectionShutdown` event handler must verify via `ReferenceEquals` that the connection being shut down is the *same instance* currently in the pool before removing/disposing it.
6. **Thread-safe pool removal**: The sync variant's `RemoveConnection` must move the `ContainsKey` check inside the lock (fix TOCTOU).
7. **Both Sync and Async variants**: Fixes must be applied to both `Paramore.Brighter.MessagingGateway.RMQ.Sync` and `Paramore.Brighter.MessagingGateway.RMQ.Async` packages.

### Non-functional Requirements

- **No behavioral regression**: Existing RMQ tests must continue to pass. The connection pool must still share connections correctly in production.
- **Thread safety**: All connection pool mutations must remain properly synchronized.
- **Idempotent disposal**: Calling Dispose/DisposeAsync multiple times on a consumer must not throw.

### Constraints and Assumptions

- The connection pool is static and shared across the application lifetime — this design is intentional for production use (connection reuse).
- The pool key is `{user}.{password}.{host}.{port}.{vhost}`, so all consumers/producers to the same broker share one entry.
- The `ConnectionShutdown` event fires asynchronously from the RabbitMQ client library and may fire after a replacement connection has been added to the pool.
- The Proactor runs its event loop inside `BrighterAsyncContext.Run()` on a single-threaded scheduler — any blocking call on that thread will deadlock if the blocked operation's continuations are posted back to the same context.
- `RmqMessageGateway.Dispose(bool)` is called from multiple paths (consumer dispose, producer dispose) — changes must not break the sync variant's disposal.

### Out of Scope

- Changing the connection pool from static/shared to per-consumer connections.
- Adding connection pool reference counting.
- Changing the Dispatcher's control task architecture.
- Fixing pre-existing flaky `[Trait("Fragile", "CI")]` tests beyond what these fixes naturally resolve.
- Other transport implementations (Kafka, SQS, Redis, etc.).

## Acceptance Criteria

1. **Process exits after Ctrl+C**: A ServiceActivator with RMQ subscriptions (using `MessagePumpType.Proactor`) shuts down and the process exits within a reasonable timeout after Ctrl+C (reproducer from issue #3684).
2. **No deadlock in async dispose path**: The Proactor dispose chain does not call blocking `.Wait()` or `.GetAwaiter().GetResult()` on code that captures `BrighterSynchronizationContext`.
3. **No stale handler disposal**: When a connection is replaced in the pool, the old connection's shutdown handler does not dispose the new connection.
4. **Existing RMQ tests pass**: All non-fragile RMQ tests pass.
5. **Idempotent consumer dispose**: Disposing an already-disposed consumer does not throw.

## Additional Context

### Shutdown chain — current (broken) behavior
```
Ctrl+C → ServiceActivatorHostedService.StopAsync()
       → Dispatcher.End()
       → Consumer.Shut() → Channel.Stop() [enqueues MT_QUIT]
       → Proactor EventLoop receives MT_QUIT
       → Channel.Dispose()                                    [inside BrighterAsyncContext]
       → ChannelAsync.Dispose() fire-and-forgets DisposeAsync()
       → RmqMessageConsumer.DisposeAsync() → RmqMessageGateway.Dispose(true)
       → AbortAsync().Wait() / RemoveConnectionAsync().GetResult()  [BLOCKS single thread]
       → Continuations posted to BrighterSynchronizationContext     [DEADLOCK — thread blocked]
       → BrighterAsyncContext.Run() never returns
       → Performer task never completes → Dispatcher._controlTask hangs
       ✗ Process stuck forever
```

### Expected chain after fix
```
Ctrl+C → ServiceActivatorHostedService.StopAsync()
       → Dispatcher.End()
       → Consumer.Shut() → Channel.Stop() [enqueues MT_QUIT]
       → Proactor EventLoop receives MT_QUIT
       → await Channel.DisposeAsync()                         [properly async]
       → await RmqMessageConsumer.DisposeAsync()              [no sync-over-async]
       → await AbortAsync(); await RemoveConnectionAsync()    [with ConfigureAwait(false)]
       → Connection closed → background threads terminate
       → BrighterAsyncContext.Run() returns
       → Performer task completes → Dispatcher control task completes
       → Process exits cleanly
```

### Key source files
- `src/Paramore.Brighter/ChannelAsync.cs` — fire-and-forget DisposeAsync (line 205)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs` — blocking sync-over-async in Dispose(bool) (lines 206-212)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGatewayConnectionPool.cs` — missing ConfigureAwait(false), ShutdownHandler race
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageGatewayConnectionPool.cs` — ShutdownHandler race, TOCTOU in RemoveConnection
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs` — EventLoop calls Channel.Dispose() (line 218)
