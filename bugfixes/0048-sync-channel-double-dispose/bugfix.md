# Bugfix: Sync Channel.Dispose is not idempotent, so the Reactor disposes every consumer twice

**Linked Issue**: #4511
**Status**: Verified

## Symptom

**Observed:** In a normal Reactor shutdown, the wrapped `IAmAMessageConsumerSync.Dispose()` runs twice for each
performer. `Channel.Dispose()` has no disposed check, so each call goes straight through to the consumer.

**Expected:** `Channel.Dispose()` is idempotent and disposes the wrapped consumer exactly once. `ChannelAsync`
already works this way.

**Call path (normal quit, sequential, two threads):**
1. Pump thread: the Reactor receives `MT_QUIT` and calls `Channel.Dispose()`
   (`src/Paramore.Brighter.ServiceActivator/Reactor.cs:190`). It then breaks out of the loop and the performer task
   completes.
2. Dispatcher control-loop thread: `WaitForPerformersToStop` calls `HandleNextStoppedPerformer` (`Dispatcher.cs:553`).
   `Task.WaitAny` returns, then `RemoveConsumerForTask` (`Dispatcher.cs:570`) calls `consumer.Dispose()`
   (`Dispatcher.cs:580`).
3. `Consumer.Dispose(bool)` calls `Performer.Dispose()` (`Consumer.cs:146`).
4. `Performer.Dispose(bool)` calls `_channel.Dispose()` (`Performer.cs:99`). This is the **second** dispose, and it
   reaches `_messageConsumer.Dispose()` again (`Channel.cs:205`).

**Reactor exit paths that dispose the channel.** All of them are later followed by steps 2 to 4, because any
completed or faulted performer task goes through `HandleNextStoppedPerformer`:
- `Reactor.cs:106`: unacceptable-message limit reached (`MP_LIMIT_EXCEEDED`), then break.
- `Reactor.cs:155`: `Receive` returned null. It disposes, then throws at `:158`, so the task faults. The Dispatcher
  still disposes the consumer.
- `Reactor.cs:190`: `MT_QUIT`. This is the normal shutdown path.
- `Reactor.cs:316`: an `AggregateException` containing a `ConfigurationException` (`stop`), then break.
- `Reactor.cs:332`: a direct `ConfigurationException`, then break.
- The `finally` at `Reactor.cs:400-403` only ends the trace span. It does not dispose.

**Other things that dispose a channel:**
- `ConsumerFactory.cs` has no `Dispose` calls.
- `Dispatcher.Dispose()` (`Dispatcher.cs:208`) goes through `End()` and `Consumer.Shut`, which posts a quit. So it
  reaches the same Reactor `:190` path, then `Performer.Dispose`.
- The `Channel?.Dispose()` calls in `RmqMessageGateway` (`RMQ.Sync/RmqMessageGateway.cs:205`,
  `RMQ.Async/RmqMessageGateway.cs:249`) dispose the RabbitMQ `IModel`, not a Brighter `Channel`. Not relevant.

**The Proactor is not affected.** It uses `ChannelAsync`, and `Performer.Dispose` calls its `[Obsolete]` sync
`Dispose()`, which is already guarded (`ChannelAsync.cs:227-228`).

## Suspected Location

**Core:**
- `src/Paramore.Brighter/Channel.cs:195-212`. `Dispose()` calls `Dispose(true)`. The private `Dispose(bool)` calls
  `_messageConsumer.Dispose()` at `:205` with no guard.
  - The finalizer `~Channel()` (`:209-212`) calls `Dispose(false)`, which does nothing. That is harmless.
  - `Dispose()` is `virtual` (`:195`). `Dispose(bool)` is `private`, not `protected virtual`.
- Reference implementation: `src/Paramore.Brighter/ChannelAsync.cs:48` declares `private bool _disposed`. It is
  checked and set in `DisposeAsync` (`:204-205`) and in `Dispose(bool)` (`:227-228`). It is a plain `bool`, with no
  `Interlocked`.
- Double-dispose callers: `Reactor.cs:106, 155, 190, 316, 332` and `Performer.cs:95-101` (reached from
  `Consumer.cs:142-147` and `Dispatcher.cs:570-582`).

**Subclasses of `Channel`.** None of them overrides `Dispose`, so they all inherit the unguarded path:
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannel.cs:30-31` overrides only `Receive`.
- The test doubles `tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/FailingChannel.cs:30-31` and
  `NullReturningChannel.cs:34-35` are `sealed` and have no `Dispose` override.
- Every other transport creates a plain `new Channel(...)`: InMemory, RocketMQ, AWSSQS, AWSSQS.V4, GcpPubSub, Redis,
  RMQ.Sync, RMQ.Async, Postgres, Kafka, MsSql and MQTT.

**Thread-safety:** In the normal path the two disposes are sequential. The second runs on the Dispatcher control
thread only after `Task.WaitAny` sees the performer task finish. No code path was found where the Dispatcher
disposes a consumer while its pump is still running. `ChannelAsync`'s guard is a plain `bool`;
`GcpPubSubStreamMessageConsumer` and the RMQ consumers use `Interlocked.Exchange`.

**Sync consumer survey (what a second `Dispose()` does):**

| Consumer | Dispose | Second call | Notes |
|---|---|---|---|
| InMemoryMessageConsumer | `src/Paramore.Brighter/InMemoryMessageConsumer.cs:350-354`, `:376-380` | Harmless | Disposes the `ITimer` (idempotent) and `InMemoryMessageProducer.Dispose`, which is a no-op unless its pump started (`InMemoryMessageProducer.cs:147-182`). |
| GcpPubSubStreamMessageConsumer | `GcpPubSub/GcpPubSubStreamMessageConsumer.cs:327-336` | Guarded | `Interlocked.Exchange(ref _disposed, 1)`, added by #4502. This consumer is where the bug was first seen: the shared-client handler count went to −1. |
| GcpPullMessageConsumer | `GcpPubSub/GcpPullMessageConsumer.cs:483-486` | Harmless | `GcpRejectionRouter.Dispose` removes each producer before disposing it (`GcpRejectionRouter.cs:283-292`, `:319-323`). |
| PostgresMessageConsumer | `Postgres/PostgresMessageConsumer.cs:488` | Harmless | `PostgreSqlConnectionProvider.Dispose(bool)` disposes the data source, then sets it to null (`PostgreSql/PostgreSqlConnectionProvider.cs:75-80`). |
| MsSqlMessageConsumer | `MsSql/MsSqlMessageConsumer.cs:331-335` | Harmless | `MsSqlMessageProducer.Dispose` is empty (`MsSql/MsSqlMessageProducer.cs:187`). |
| RmqMessageConsumer (Sync) | `RMQ.Sync/RmqMessageConsumer.cs:617-636` | Guarded | `Interlocked.Exchange`. The base gateway also has `_disposed` (`RMQ.Sync/RmqMessageGateway.cs:199-200`). |
| RmqMessageConsumer (Async) | `RMQ.Async/RmqMessageConsumer.cs:731-750` | Guarded | `Interlocked.Exchange`. The base has `_disposed` (`RMQ.Async/RmqMessageGateway.cs:243-244`). |
| AzureServiceBusConsumer | `AzureServiceBus/AzureServiceBusConsumer.cs:83-87` | Harmless | `ServiceBusReceiverWrapper.CloseAsync` returns the cached `_closing` task on a repeat call (`AzureServiceBusWrappers/ServiceBusReceiverWrapper.cs:90-92`). |
| SqsMessageConsumer (V3) | `AWSSQS/SqsMessageConsumer.cs:433-442` | Harmless | Disposes only the DLQ and invalid-message producers, and `SqsMessageProducer.Dispose` is empty (`AWSSQS/SqsMessageProducer.cs:84-86`). |
| SqsMessageConsumer (V4) | `AWSSQS.V4/SqsMessageConsumer.cs:426-435` | Harmless | Same pattern. `AWSSQS.V4/SqsMessageProducer.cs:84-86` is empty. |
| RedisMessageConsumer | `Redis/RedisMessageConsumer.cs:163-173` | Probably harmless (third-party, not verified) | The pool is per instance (`Redis/RedisMessageGateway.cs:37-38`). `DisposePool` (`:69-73`) has no guard, so ServiceStack's `RedisManagerPool.Dispose()` runs a second time. |
| MqttMessageConsumer | `MQTT/MQTTMessageConsumer.cs:201-223` | Probably harmless (third-party, not verified) | `TryComplete` is idempotent. It disposes `IMqttClient` (MQTTnet) twice, relying on MQTTnet's own dispose guard. |
| RocketMessageConsumer | `RocketMQ/RocketMessageConsumer.cs:599` | Unknown (third-party) | `SimpleConsumer.Dispose()` (RocketMQ.Client 5.2.1) runs twice with no Brighter guard. The `SimpleConsumer` belongs to one consumer and is not shared (`RocketMQ/RocketMessageConsumerFactory.cs:29-43`). |
| KafkaMessageConsumer | `Kafka/KafkaMessageConsumer.cs:1228-1245`, `:1250-1254` | **At risk** | No guard. `Close()` is guarded by `_isClosed` (`:416`). `_consumer?.Dispose()` (Confluent) runs twice. If a requeue or rejection producer was created, `KafkaMessageProducer.Dispose(bool)` (`Kafka/KafkaMessageProducer.cs:393-402`) calls `Flush()` → `_producer?.Flush()` (`:186-189`) on an already-disposed Confluent producer. That may throw `ObjectDisposedException` (not verified). |

**Why a throw on the second dispose would make things worse (not verified):** `WaitForPerformersToStop` catches
only `AggregateException` (`Dispatcher.cs:542`). In `HandleNextStoppedPerformer`, `RemoveConsumerForTask` (`:560`)
runs before `_tasks.TryRemove` (`:562`). If the second `consumer.Dispose()` (`:580`) throws any other exception, it
escapes the control loop. `State` would then never reach `DS_STOPPED`, and the task would stay in `_tasks`.

## Root-Cause Hypothesis

**Hypothesis:** `Channel.Dispose()` (`src/Paramore.Brighter/Channel.cs:195-207`) forwards every call to
`_messageConsumer.Dispose()`, because it has no disposed flag. The Reactor disposes the channel on each loop exit
(`Reactor.cs:106/155/190/316/332`). The Dispatcher then disposes it again through `Consumer.Dispose`,
`Performer.Dispose` and `_channel.Dispose()` (`Dispatcher.cs:580`, `Consumer.cs:146`, `Performer.cs:99`). So every
sync consumer is disposed exactly twice per performer shutdown.

**This is falsifiable.** A spy `IAmAMessageConsumerSync` that counts `Dispose()` calls, driven through a real
`Reactor` + `Performer` + `Consumer`/`Dispatcher` quit, should end with a count of **2**. If it ends at 1, the
hypothesis is wrong.

**Implied fix (from the issue): UNVERIFIED — to be proven or refuted in /bugfix:confirm.** Add a `_disposed` guard
to `Channel` like the one in `ChannelAsync` (`ChannelAsync.cs:48`, `:227-228`), so the consumer is disposed once.

Open questions for confirm:
- Should the guard be a plain `bool` (as in `ChannelAsync`) or use `Interlocked`? No concurrent path was found.
- Should the guard sit in the public `virtual Dispose()` or in the private `Dispose(bool)`? `Dispose()` is virtual,
  and `AzureServiceBusChannel` and the test doubles subclass `Channel`, though none overrides `Dispose` today.

**Suggested confirm probes:**
1. Unit: construct a `Channel` with a spy sync consumer. Call `Dispose()` twice. Expect a count of 2 today.
2. Pump level: build a `Reactor` over `Channel(spy)`, wrap it in a `Performer`, and enqueue a quit with
   `channel.Stop(routingKey)`. Run the pump, then call `performer.Dispose()`. Expect a count of 2 today.
3. Dispatcher level (optional): one subscription with an InMemory or spy consumer factory, `Receive()` then
   `End().Wait()`. Assert the count per consumer.
4. Kafka impact probe (optional): does a second `KafkaMessageProducer.Dispose()` throw from `Flush()` on a disposed
   Confluent producer? This decides whether the survey's "may throw" is real. It needs a broker or a check of the
   Confluent source.

**Stale references:** the earlier notes in `bugfixes/0026-gcp-stream-stopped-client-cache/bugfix.md` cite
`Channel.cs:190-202`, `Reactor.cs:183` and `Performer.cs:93`. The current lines are `Channel.cs:195-212`,
`Reactor.cs:190` and `Performer.cs:99`.

## Confirmed Root Cause

**Verdict: CONFIRMED.** Every normal Reactor shutdown disposes the wrapped sync consumer twice. Some edge paths
dispose it fewer times. No path disposes it more than twice.

`Channel.Dispose()` has no disposed flag. `src/Paramore.Brighter/Channel.cs:195-199` calls the private
`Dispose(bool)` at `:201`, which calls `_messageConsumer.Dispose()` at `:205` every time. Two owners call it, one
after the other:

1. **The pump thread.** The Reactor disposes the channel on each exit: `Reactor.cs:106` (limit), `:155` (null
   receive, then throw at `:158`), `:190` (MT_QUIT), `:316` (stop flag) and `:332` (ConfigurationException).
2. **The Dispatcher control thread.** After `Task.WaitAny` (`Dispatcher.cs:556`), `RemoveConsumerForTask`
   (`:560`, `:570-582`) calls `consumer.Dispose()` (`:580`). The chain is then `Consumer.Dispose(bool)` →
   `Performer.Dispose()` (`Consumer.cs:146`), and `Performer.Dispose(bool)` → `_channel.Dispose()` (`Performer.cs:99`).

The Dispatcher path really uses the sync `Channel`:
- `ConsumerFactory.Create` sends Reactor subscriptions to `CreateReactor` (`ConsumerFactory.cs:84-90`).
- `CreateReactor` builds `CreateSyncChannel(...)` (`:100`), a `Reactor` over it (`:101`) and
  `new Consumer(..., channel, messagePump)` (`:113`).
- `Consumer` wraps the same channel in `new Performer(channel, messagePump)` (`Consumer.cs:98`).

`Consumer.cs:136-148` and `Performer.cs:80-101` have no guard either, so a flag in `Channel` is the one place that
covers every caller.

## Evidence
- [x] Code-trace (Plan/opus sub-agent, spot-checked by the main agent):
  - **Never three times.** Only four places in `src` reach these disposes: `Dispatcher.cs:567` (the Task),
    `Dispatcher.cs:580`, `Consumer.cs:146` and `Performer.cs:99`.
    - `Dispatcher.Dispose()` (`:208-252`) does not dispose consumers. It only calls `End()` → `Consumer.Shut` →
      `Performer.Stop`, which posts a quit (`Dispatcher.cs:341-349`, `Consumer.cs:121-131`, `Performer.cs:53-56`).
    - `ServiceActivatorHostedService.cs:87` only calls `End()`.
    - The finalizers call `Dispose(false)`, which does nothing.
  - **The `RemoveConsumerForTask` early return (`Dispatcher.cs:573-574`) does not happen in practice.** Consumer
    names are unique (`{subscription}-{Uuid}`, `ConsumerFactory.cs:61/81`), and every `_tasks.TryAdd` is paired with
    a `_consumers.TryAdd`.
  - **Paths with fewer than two disposes.** These don't refute the bug:
    - Count 1: the Reactor dies from an unexpected exception that skips its dispose sites.
    - Count 0: `Shut` before `Open` (`Consumer.cs:109-110`).
    - Count 1: a bare `Performer` with no `Dispose`.
  - **Restarts follow the same 2-dispose path.** `Open` while running, `SetActivePerformers` growth and `Open` after
    DS_STOPPED all build fresh consumers and channels.
  - **The two disposes are sequential, never concurrent.** The Dispatcher disposes only after `Task.WaitAny` sees
    the pump task finish.
- [x] Red repro (pure logic, no infrastructure; the durable tests are for `/bugfix:test`):
  - **No existing test double fits.** `InMemoryMessageConsumer` is `sealed`. A new spy is needed:
    `SpyDisposeCountingConsumer : IAmAMessageConsumerSync`, which counts `Dispose()` calls.
  - (1) Unit: call `new Channel(name, rk, spy).Dispose()` twice, then `Assert.Equal(1, spy.DisposeCount)`. Fails
    today with 2.
  - (2) Pump level: copy the arrangement from
    `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/When_running_a_message_pump_on_a_thread_should_be_able_to_stop.cs`
    over `Channel(spy)`. Run `performer.Run()`, `performer.Stop(rk)`, `task.Wait`, then `performer.Dispose()`, and
    assert a count of 1. Fails today with 2.
- [x] **Kafka impact probe** (scratchpad `kprobe/`, Confluent.Kafka 2.15.0, no broker):
  - `Flush` after `Dispose` throws **`ObjectDisposedException: handle is destroyed`**.
  - A second `Dispose` of a producer or consumer does not throw.
  - `KafkaMessageProducer.Dispose(bool)` (`KafkaMessageProducer.cs:393-403`) calls `Flush()` first and never nulls
    `_producer`. So a **second dispose of any Kafka producer the consumer created** (requeue, DLQ, invalid-message;
    `KafkaMessageConsumer.cs:1238-1244`) **throws**.

**Suggested-fix assessment: CONFIRMED.** Add `private bool _disposed;` to `Channel`. Put
`if (_disposed) return; _disposed = true;` at the top of the private `Dispose(bool)`, above `if (disposing)`. This
mirrors `ChannelAsync.cs:48`, `:225-228`.
- **Where it goes:** the guard in `Dispose(bool)` covers the public path and any subclass that calls `base`. No
  subclass overrides `Dispose` today.
- **Plain `bool` or `Interlocked`:** a plain `bool` is enough, because no concurrent path exists. A race would need
  someone to dispose by hand while the pump is still running.
- **The flag is set before the consumer is called,** so a throw on the first dispose is not retried.

## Scope Notes
1. **Real-world impact: Kafka.** A Kafka Reactor consumer that has requeued or rejected a message owns a producer.
   At shutdown, the second dispose throws `ObjectDisposedException` from `Flush`.
   - On the Dispatcher control thread, `WaitForPerformersToStop` catches only `AggregateException`
     (`Dispatcher.cs:542`). So the exception escapes `RunControlLoop`: State never reaches DS_STOPPED and the
     remaining performers are not waited on.
   - That is inferred from the code plus the probe. It has not been run end to end.
   - The `Channel` guard removes this trigger.
2. **Optional defence in depth, kept separate:** idempotent guards on `KafkaMessageConsumer.Dispose` and
   `KafkaMessageProducer.Dispose`. Once the `Channel` guard is in, Kafka has no double-dispose path of its own.
3. **Separate, out of scope:** the Dispatcher's exception escape (`Dispatcher.cs:542`, `:560-562`) is still possible
   when the Dispatcher's dispose is the *first* one (the count-1 path) and it throws.
4. **Separate, out of scope:** a consumer that is Shut before it is Opened (`Consumer.cs:109-110`) is never
   disposed, which is a potential leak.
5. **Out of scope:** making other `Channel` members throw `ObjectDisposedException` after dispose. `ChannelAsync`
   doesn't do this either.
6. **Parity:** the Proactor/`ChannelAsync` path is already guarded (`Proactor.cs:145/194/229/351/362` →
   `ChannelAsync.cs:204-205`, then `:227`). No gap.
7. **Other owners:** nothing else disposes a sync consumer twice. The only static consumer cache is GCP
   `s_consumers`, which #4502 already guarded.

## Regression Test
Both tests were RED before the fix, failing with `Expected: 1, Actual: 2`. The user approved them on 2026-10-04.
- `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/When_a_channel_is_disposed_twice_should_dispose_its_consumer_once.cs`
  (`ChannelDoubleDisposalTests`): a bare `Channel` disposed twice.
- `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/When_a_reactor_quits_and_its_performer_is_disposed_should_dispose_the_consumer_once.cs`
  (`ReactorQuitConsumerDisposalTests`): Reactor `MT_QUIT`, then `Performer.Dispose`. This is the path the Dispatcher
  takes.
- New test double: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/SpyDisposeCountingConsumer.cs`.

## Fix
- `src/Paramore.Brighter/Channel.cs`: a new `private bool _disposed`, checked and set at the top of the private
  `Dispose(bool)`, mirroring `ChannelAsync`. The flag is set before the consumer is disposed. The public API is
  unchanged.
- Both regression tests are green (net10.0). The full-suite run is for `/bugfix:verify`.
- Kafka's own dispose guards and the Dispatcher's exception escape are not changed (Scope Notes 2 and 3).

**Verified (2026-10-04)**, compared with the session-9 baselines by test name:
- Core, Release: 1604 passed / 0 failed / 7 skipped on both net9.0 and net10.0. That is the baseline 1602 plus the
  2 new tests, with no other changes.
- Extensions, Release: 649 (net9.0) and 646 (net10.0), both unchanged.
- GCP Stream suite on the emulator: 126 / 1 / 25. The 1 failure is the known StreamOrdering Proactor two-message
  Nack flake, which passed 3/3 when re-run. Everything else matches the 0047 baseline by name.
