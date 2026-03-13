# Tasks: Fix RMQ Shutdown Deadlock and Connection Pool Race Condition

**Spec**: `specs/0024-rmq_shutdown/`
**ADR**: `docs/adr/0054-rmq-shutdown-and-connection-pool-fixes.md`
**Issues**: #3684, #4024
**Branch**: `shutdown_rmq`

## Phase 1: Tidy — Connection Pool Race Condition (Sync)

These tasks fix the TOCTOU and stale-handler bugs in the sync connection pool (#4024).
No behavioral changes to the async dispose path yet.

- [x] **1.1 TEST + IMPLEMENT: Sync pool stale shutdown handler does not dispose replacement connection** (afaa64df6)
  - **USE COMMAND**: `/test-first when sync pool shutdown handler fires for replaced connection then it should not dispose current connection`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessagingGateway/RMQ/`
  - Test file: `When_sync_pool_shutdown_handler_fires_for_replaced_connection_should_not_dispose_current.cs`
  - Test should verify:
    - A connection is added to the pool, then replaced by a new connection with the same key
    - When the old connection's shutdown handler fires, the new connection remains open/undisposed
    - The pool still contains the replacement connection
  - **STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RmqMessageGatewayConnectionPool` (Sync), add `ReferenceEquals(pooled.Connection, sender)` guard to `ShutdownHandler` closure (`RmqMessageGatewayConnectionPool.cs:111-119`)
    - Only call `TryRemoveConnection` if the sender is the current pooled instance

- [x] **1.2 TEST + IMPLEMENT: Sync pool RemoveConnection is thread-safe without TOCTOU** (afaa64df6)
  - **USE COMMAND**: `/test-first when sync pool RemoveConnection is called then ContainsKey check is inside lock`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessagingGateway/RMQ/`
  - Test file: `When_sync_pool_remove_connection_should_be_thread_safe.cs`
  - Test should verify:
    - `RemoveConnection` called for a connection that exists removes it
    - `RemoveConnection` called for a connection that does not exist does not throw
    - Concurrent `RemoveConnection` calls do not throw or corrupt pool state
  - **STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RemoveConnection` (`RmqMessageGatewayConnectionPool.cs:157-168`), move the `ContainsKey` check inside the lock (or remove it entirely since `TryRemoveConnection` uses `TryGetValue`)

## Phase 2: Tidy — Connection Pool Race Condition (Async)

Mirror the sync fixes in the async connection pool.

- [x] **2.1 TEST + IMPLEMENT: Async pool stale shutdown handler does not dispose replacement connection** (a7810257e)
  - **USE COMMAND**: `/test-first when async pool shutdown handler fires for replaced connection then it should not dispose current connection`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessagingGateway/RMQ/`
  - Test file: `When_async_pool_shutdown_handler_fires_for_replaced_connection_should_not_dispose_current.cs`
  - Test should verify:
    - Same as 1.1 but for the async pool variant
    - Old connection's async shutdown handler fires, new connection remains undisposed
  - **STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RmqMessageGatewayConnectionPool` (Async), add `ReferenceEquals` guard to async `ShutdownHandler` closure (`RmqMessageGatewayConnectionPool.cs:151-165`)
    - Only call `TryRemoveConnectionAsync` if `sender` is the current pooled instance

- [x] **2.2 TEST + IMPLEMENT: Async pool RemoveConnectionAsync is thread-safe without TOCTOU** (a7810257e)
  - **USE COMMAND**: `/test-first when async pool RemoveConnectionAsync is called then ContainsKey check is inside semaphore`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessagingGateway/RMQ/`
  - Test file: `When_async_pool_remove_connection_should_be_thread_safe.cs`
  - Test should verify:
    - `RemoveConnectionAsync` called for a connection that exists removes it
    - `RemoveConnectionAsync` called for a connection that does not exist does not throw
  - **STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RemoveConnectionAsync` (`RmqMessageGatewayConnectionPool.cs:115-131`), move the `ContainsKey` check inside the semaphore (or remove it entirely)

- [x] **2.3 IMPLEMENT: Add ConfigureAwait(false) to async connection pool methods** (a7810257e)
  - This is a structural tidy — no behavioral test needed (defence-in-depth)
  - Implementation should add `ConfigureAwait(false)` to every `await` in `RmqMessageGatewayConnectionPool` (Async):
    - `GetConnectionAsync`: `await s_lock.WaitAsync(cancellationToken).ConfigureAwait(false)`
    - `ResetConnectionAsync`: `await s_lock.WaitAsync(cancellationToken).ConfigureAwait(false)`, `await DelayReconnectingAsync().ConfigureAwait(false)`, `await CreateConnectionAsync(...)ConfigureAwait(false)`
    - `RemoveConnectionAsync`: `await s_lock.WaitAsync(cancellationToken).ConfigureAwait(false)`, `await TryRemoveConnectionAsync(...).ConfigureAwait(false)`
    - `CreateConnectionAsync`: `await TryRemoveConnectionAsync(...).ConfigureAwait(false)`, `await connectionFactory.CreateConnectionAsync(...).ConfigureAwait(false)`, `await s_lock.WaitAsync(...).ConfigureAwait(false)` in ShutdownHandler, `await TryRemoveConnectionAsync(...).ConfigureAwait(false)` in ShutdownHandler
    - `TryRemoveConnectionAsync`: `await pooledConnection.Connection.DisposeAsync().ConfigureAwait(false)`
    - `DelayReconnectingAsync`: `await Task.Delay(...).ConfigureAwait(false)`

## Phase 3: ChannelAsync supports IAsyncDisposable

- [x] **3.1 TEST + IMPLEMENT: ChannelAsync DisposeAsync awaits consumer DisposeAsync** (2c213e258)
  - **USE COMMAND**: `/test-first when ChannelAsync is disposed async then consumer DisposeAsync is awaited`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/`
  - Test file: `When_channel_async_dispose_async_should_await_consumer_dispose.cs`
  - Test should verify:
    - Create a `ChannelAsync` with an `InMemoryMessageConsumer`
    - Call `await channel.DisposeAsync()`
    - The consumer is properly disposed (subsequent operations on the consumer reflect disposal)
    - Calling `DisposeAsync` a second time does not throw (idempotent)
  - **STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `IAsyncDisposable` to `IAmAChannelAsync` interface (`IAmAChannelAsync.cs:36`)
    - Implement `DisposeAsync()` on `ChannelAsync` (`ChannelAsync.cs`): `await _messageConsumer.DisposeAsync()` then `GC.SuppressFinalize(this)`

## Phase 4: Proactor uses async dispose on shutdown

- [x] **4.1 TEST + IMPLEMENT: Proactor disposes channel asynchronously on MT_QUIT** (7a66a16a0)
  - **USE COMMAND**: `/test-first when proactor receives quit message then channel is disposed asynchronously and pump stops`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/`
  - Test file: `When_proactor_receives_quit_should_dispose_channel_async.cs`
  - Test should verify:
    - A Proactor with a `ChannelAsync` (wrapping `InMemoryMessageConsumer`) receives MT_QUIT
    - The pump stops (performer task completes, `IsCompleted == true`)
    - The channel's consumer is properly disposed after shutdown
    - The performer task is not faulted
  - **STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `Proactor.EventLoop()`, change all 5 `Channel.Dispose()` calls to `await Channel.DisposeAsync()`:
      - Line 144 (unacceptable message limit reached)
      - Line 183 (null message error)
      - Line 218 (MT_QUIT)
      - Line 322 (AggregateException stop)
      - Line 333 (ConfigurationException)

## Phase 5: Regression

- [x] **5.1 VERIFY: All existing core tests pass**
  - Run `dotnet test tests/Paramore.Brighter.Core.Tests/`
  - All non-fragile tests must pass
  - Focus on MessageDispatch/Proactor tests — these exercise the dispose path
  - **Result**: 542 passed, 0 failed, 7 skipped (both net9.0 and net10.0)

- [x] **5.2 VERIFY: Existing RMQ async tests pass (if Docker available)**
  - Run `dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/`
  - If Docker/RabbitMQ not available, skip and note
  - **Result**: 73 passed, 4 skipped, 13 failed (both net9.0 and net10.0)
  - All 13 failures are pre-existing infrastructure issues (mTLS certs not generated, delayed-message plugin not installed) — no regressions from our changes

## Dependencies

```
Phase 1 (sync pool fixes) ──┐
                             ├── Phase 5 (regression)
Phase 2 (async pool fixes) ─┤
                             │
Phase 3 (ChannelAsync) ─────┤
                             │
Phase 4 (Proactor) ──────────┘
         │
         └── depends on Phase 3 (IAsyncDisposable on IAmAChannelAsync)
```

- Phases 1, 2, and 3 are independent and can be done in any order
- Phase 4 depends on Phase 3 (needs `IAsyncDisposable` on `IAmAChannelAsync`)
- Phase 5 runs after all implementation phases
