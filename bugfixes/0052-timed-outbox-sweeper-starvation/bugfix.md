# Bugfix: TimedOutboxSweeper ticks are starved or delayed under load (InMemoryOutbox + Kafka async confirms, EKS)

**Linked Issue**: #4560
**Related**: #4554 (same outstanding-check defect as Confirmed Root Cause item 3, claimed by
@guilhermeappel; no PR yet), PR #4552 (same contributor; adds `GetAwaiter().GetResult()` inside the
same `Task.Run` check for async-only outboxes, i.e. more pool blocking on that path), draft PR #4484
(outbox metrics; overlaps part of the telemetry gap)
**Status**: Verified

## Symptom

**Setup reported by the maintainer.** For speed, some teams combine three things:
- `CommandProcessor.Post` / `PostAsync`
- `InMemoryOutbox`
- a producer whose publisher confirms arrive out of band, most commonly `KafkaMessageProducer`

`Post` writes to the in-memory outbox and dispatches. A message is only marked dispatched when the
broker's delivery report (the confirm) arrives. Anything never confirmed must be re-sent by
`TimedOutboxSweeper`, a hosted service in the same process.

**Observed.** Under load on Kubernetes (specifically EKS), the sweeper's background work runs rarely
or not at all. Unconfirmed messages pile up in the `InMemoryOutbox` and are not re-sent in good
time. Because the outbox is in memory, a pod restart loses them for good.

**Expected.** One sweep starts roughly every `TimerInterval` seconds (default 5 s,
`TimedOutboxSweeperOptions.cs:38`). Each sweep re-dispatches up to `BatchSize` (default 100, `:47`)
undispatched messages older than `MinimumMessageAge` (default 5 s, `:42`). This should keep
happening whatever the Post rate.

**Reproduction.** There is no reliable reproduction; maintainers have not reproduced it. The reports
share these conditions:
- high Post rate
- Kafka with delivery reports
- container CPU limits, so `Environment.ProcessorCount` is small and CFS throttling is possible
- the sweeper in the same process as the publishers

## Suspected Location

All references below were checked against the working tree at `2890f8610`.

**Sweeper host (timer and tick)**
- `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs:77`:
  `new Timer(Sweep, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval))`. This is a
  `System.Threading.Timer`, so its callbacks are queued to the shared thread pool. The time source is
  hard-wired: there is no `TimeProvider` or `ITimer` injection.
- `TimedOutboxSweeper.cs:104`: `private async void Sweep(object? state)`. Because it is async void,
  there is no handle to await, and exceptions escape to the thread pool.
- `TimedOutboxSweeper.cs:106`: `ObtainLockAsync` is the first thing each tick does, which makes it
  the best seam for timestamping when a tick actually runs.
- `TimedOutboxSweeper.cs:136`: the "Outbox Sweeper is still running - abandoning attempt" log (the
  H2 signal).
- `TimedOutboxSweeper.cs:114`: resolves `IAmAnOutboxProducerMediator` from a new scope.
- `TimedOutboxSweeper.cs:124`: awaits `OutboxSweeper.SweepAsync()`. No cancellation token or timeout
  is passed.
- `TimedOutboxSweeper.cs:47`: static logger from `ApplicationLogging.CreateLogger`
  (`src/Paramore.Brighter/Logging/ApplicationLogging.cs:7-8`, a settable static `LoggerFactory`).
- `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeperOptions.cs:38,42,47`: defaults
  `TimerInterval = 5` (int seconds), `MinimumMessageAge = 5s`, `BatchSize = 100`.
- `src/Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs:41-50`:
  `UseOutboxSweeper` registration via `AddHostedService<TimedOutboxSweeper>()`.
- `src/Paramore.Brighter/OutboxSweeper.cs:71-80`: `SweepAsync` calls
  `ClearOutstandingFromOutboxAsync` at `:79`, with no cancellation token.

**Sweeper lock**
- `src/Paramore.Brighter/InMemoryLock.cs:44-49`: `SemaphoreSlim(1,1)` with
  `WaitAsync(TimeSpan.Zero)`, so it never waits; it returns null if the lock is held. `:58-63`
  releases it.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:410-411` and
  `:541-545`: `InMemoryLock` is the default singleton `IDistributedLock`.

**Mediator: singleton, shared by Post and the sweeper**
- `ServiceCollectionExtensions.cs:419-423` and `:557-574`: `IAmAnOutboxProducerMediator` is
  registered `ServiceLifetime.Singleton`. The scoped resolution at `TimedOutboxSweeper.cs:114`
  therefore returns the same instance that `Post` uses.
- `src/Paramore.Brighter/OutboxProducerMediator.cs:190`: `ConfigureCallbacks` is called once, in the
  constructor.
  - At `:819-834`, the `switch` tests `IAmAMessageProducerAsync` first (`:826-827`).
    `KafkaMessageProducer` implements both sync and async, so the **async** callback path is the one
    wired up.
  - `:843-858` `ConfigureAsyncPublisherCallbackMaybe` → `:860-906` `HandleAsyncPublishConfirmation`,
    which runs `_asyncOutbox.MarkDispatchedAsync` inside `ExecuteWithResiliencePipelineAsync`
    (`:879-884`).
  - For Kafka, the sync `ConfigurePublisherCallbackMaybe` / `_outBox.MarkDispatched` delegate
    (`:952-1009`, `:977`) is not the subscriber.
- `OutboxProducerMediator.cs:715-777` `BackgroundDispatchUsingAsync`:
  - `:724` calls `_outboxCircuitBreaker?.CoolDown()`.
  - `:726` does `_backgroundClearSemaphore.WaitAsync(TimeSpan.Zero, …)`; the semaphore is a
    per-instance field at `:70`.
  - `:737-738` queries outstanding messages, excluding `TrippedTopics`.
  - `:753` calls `DispatchAsync`.
  - `:762` rethrows; `:775` logs `SkippingDispatchOfMessages`.
- `OutboxProducerMediator.cs:495-506`: `ClearOutstandingFromOutboxAsync` is the only public entry
  into `BackgroundDispatchUsingAsync`. Its only caller in `src/` is `OutboxSweeper.cs:79`.
- `OutboxProducerMediator.cs:1175-1234` `DispatchAsync`: a sequential `foreach`. Each message awaits
  `producerAsync.SendAsync` (`:1203`) through the resilience pipeline. For a confirming producer it
  does not mark dispatched (`:1211`); on failure it trips the topic (`:1223`).

**Outstanding-count check: runs after every Post and blocks pool threads**
- `OutboxProducerMediator.cs:411` (`ClearOutbox`), `:480` (`ClearOutboxAsync`), `:770`, `:1400` and
  `:1432` all call `CheckOutstandingMessages`.
- `:795-814` `CheckOutstandingMessages`: gated on
  `now - _lastOutStandingMessageCheckAt >= _maxOutStandingCheckInterval` (default 1 s, `:184`;
  initialised at `:162`). It then does `Task.Run(() => OutstandingMessagesCheck(...))` at `:811`.
- `:1332-1378` `OutstandingMessagesCheck`: `s_checkOutstandingSemaphoreToken.Wait()` at `:1334` is a
  **blocking** wait on a pool thread. The semaphore is **static**: `:74`, shared by every mediator in
  the process. `_lastOutStandingMessageCheckAt` is only updated at `:1336`, *after* the queued item
  has run and taken the semaphore. Until then, every `Post` that passes the gate at `:803` queues
  *another* `Task.Run`.
- `:1344-1349`: `GetOutstandingMessageCount` on the sync outbox.

**Post entry points**
- `src/Paramore.Brighter/CommandProcessor.cs:714-721`: `Post` → `ClearOutbox` → mediator
  `ClearOutbox` → `Dispatch` (`OutboxProducerMediator.cs:1035-1090`) → `producerSync.Send`
  (`:1060-1062`). For confirming producers it does not mark dispatched.
- `src/Paramore.Brighter/CommandProcessor.cs:776-787`: `PostAsync` → `ClearOutboxAsync` →
  `DispatchAsync`.

**Kafka producer**
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageProducer.cs:230-233,258-323`: sync
  `Send` → `SendWithDelay` → `_publisher.PublishMessage(...)` at `:295`.
  - `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagePublisher.cs:40-45` calls
    `producer.Produce(topic, msg, deliveryReport)`. That is fire-and-enqueue: no `Flush`, no wait.
  - The delivery-report callback is invoked on Confluent's own poll/delivery thread, not a pool
    thread. This is Confluent.Kafka behaviour and is not in this repo, so it is **unverified here**.
  - `Flush` is only called from `Dispose` (`KafkaMessageProducer.cs:393-403`, `:399`).
  - The only sync-over-async in the send path is the scheduler branch (`:274`,
    `BrighterAsyncContext.Run`), which only applies when there is a delay.
- `KafkaMessageProducer.cs:245-248,334-391`: `SendAsync` → `PublishMessageAsync`
  (`KafkaMessagePublisher.cs:47-70`), which **awaits `producer.ProduceAsync`** (`:53`). It then calls
  the delivery-report action inline on the continuation, so each awaited send waits for the broker
  ack.
- `KafkaMessageProducer.cs:405-446` `PublishResults`, the Brighter work done on the delivery-report
  thread: it parses the `MESSAGE_ID` header, may log, then calls `RaisePublishConfirmation`.
- `KafkaMessageProducer.cs:448-472` `RaisePublishConfirmation`:
  - `_confirmationCallbacks.Begin()` (`:455`) takes a short lock
    (`src/Paramore.Brighter/Tasks/InFlightCallbackTracker.cs:55-57`).
  - It then does `Task.Run(async () => { OnMessagePublished?.Invoke; await
    _onMessagePublishedAsync.InvokeAllAsync(result); })` at `:456-471`, one pool work item per
    delivery report.
  - On the sync `Produce` path, the `Task.Run` is issued from a non-pool thread (Confluent's), so it
    goes to the pool **global** queue. On the `ProduceAsync` path it is issued from a pool thread, so
    it goes to that thread's **local** queue. This is .NET runtime behaviour, **unverified here**.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaPublication.cs:75,81,88,94,130`: defaults
  `LingerMs = 5`, `MessageSendMaxRetries = 3`, `MessageTimeoutMs = 5000`,
  `MaxInFlightRequestsPerConnection = 1`, `RequestTimeoutMs = 500`.

**InMemoryOutbox**
- `src/Paramore.Brighter/InMemoryOutbox.cs:548-585` `OutstandingMessages`:
  `Requests.Values.OrderBy(TimeStamp)` over **all** retained entries, including dispatched ones
  (`:571-572`), *before* `.Where(undispatched && old enough && topic not tripped)`, then
  `.Take(pageSize)` (`:573-578`).
- `InMemoryOutbox.cs:617-644` `GetOutstandingMessageCount`: the same full `OrderBy` (`:631-632`). It
  runs about once a second per mediator via the outstanding check.
- `InMemoryOutbox.cs:462-478` and `:598-614`: the async methods are synchronous work wrapped in a
  `TaskCompletionSource` with `RunContinuationsAsynchronously`, which schedules continuations on the
  pool.
- `InMemoryOutbox.cs:514-536` `MarkDispatched`: a dictionary lookup that sets `TimeFlushed`.
- `src/Paramore.Brighter/InMemoryBox.cs:59,68,76`: `EntryLimit = 2048`, `EntryTimeToLive = 5 min`,
  `ExpirationScanInterval = 10 min`. Only *dispatched* entries are expired or compacted
  (`InMemoryOutbox.cs:76-80`, `:658-665`).

**Other pool blocker in the same host**
- `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs:73`: the timer callback does
  `Archive(cancellationToken).GetAwaiter().GetResult()`, which blocks a pool thread for each archive
  run if the archiver is also registered.

**Existing tests**
- There are **no `TimedOutboxSweeper` tests**: no `.cs` file under `tests/` references it.
- `OutboxSweeper` is covered by:
  - `tests/Paramore.Brighter.InMemory.Tests/Sweeper/When_sweeping_the_outbox.cs`,
    `When_sweeping_the_outbox_with_circuit_breaker.cs` and
    `When_clearing_outbox_with_missing_messages.cs`. This project does **not** reference
    `Paramore.Brighter.Outbox.Hosting`;
    `tests/Paramore.Brighter.InMemory.Tests/Paramore.Brighter.InMemory.Tests.csproj:28-29` lists only
    `Paramore.Brighter` and `Base.Test`.
  - `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Clear/When_Implicit_Clearing_The_PostBox_On_The_Command_Processor*.cs`,
    `When_Bulk_Clearing_The_PostBox_On_The_Command_Processor_Async.cs` and
    `When_independent_mediators_sweep_concurrently_should_clear_both_outboxes.cs`. Core.Tests
    **does** reference Outbox.Hosting
    (`tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj:12`) and has
    `TimedOutboxArchiver` tests under `tests/Paramore.Brighter.Core.Tests/Archiving/`, which are the
    closest pattern to copy.
- Precedent for pool starvation in tests:
  `tests/Paramore.Brighter.InMemory.Tests/ThreadPoolSaturatingCollection.cs:29-38`
  (`DisableParallelization = true`). Its comment cites #4475, where saturating `Task.Run` items
  starved `InMemoryBox` background work "for tens of seconds" on small CI runners.
- In-process stand-in for Kafka's out-of-band confirm:
  `src/Paramore.Brighter/InMemoryMessageProducer.cs:112` (`UseAsyncPublishConfirmation { get; init;
  }`). With it, `:237-248` sends become fire-and-forget onto a background worker, and `:264-281`
  confirms asynchronously, so a test does not need a broker.

## Root-Cause Hypothesis

> All hypotheses below are **UNVERIFIED — to be proven or refuted in /bugfix:confirm**.

### H1 (leading): thread-pool starvation delays the sweeper's timer tick

**The chain**
1. `TimedOutboxSweeper.cs:77` schedules sweeps with a `System.Threading.Timer`. The runtime timer
   thread does not run `Sweep`; it queues the callback to the shared pool's global FIFO queue (.NET
   runtime behaviour, unverified here).
2. At a high Post rate with Kafka, every delivery report adds one `Task.Run` work item
   (`KafkaMessageProducer.cs:456`). That item runs `HandleAsyncPublishConfirmation`
   (`OutboxProducerMediator.cs:860-906`): span creation, a `RequestContext` copy, the resilience
   pipeline, then `MarkDispatchedAsync`, whose continuation is posted back to the pool
   (`InMemoryOutbox.cs:471`). So each confirm costs at least two pool work items.
3. Two amplifiers turn a busy pool into a **blocked** pool:
   - **(a) Outstanding-check pile-up.** `CheckOutstandingMessages`
     (`OutboxProducerMediator.cs:795-814`) runs on every `ClearOutbox` / `ClearOutboxAsync` (`:411`,
     `:480`), i.e. on every Post. The timestamp that gates it is only refreshed by the queued work
     item itself (`:1336`). When the pool is slow, every Post in the gap passes the gate at `:803`
     and queues another `Task.Run` (`:811`). Each of those, once running, **blocks a pool thread** on
     the static `s_checkOutstandingSemaphoreToken.Wait()` (`:1334`) while one at a time runs an
     O(n log n) `OrderBy` over the whole outbox (`InMemoryOutbox.cs:631-632`). That is positive
     feedback: starvation → more queued checks → more blocked threads → more starvation.
   - **(b) Other blocking.** Synchronous `Post` pipelines on request threads, and
     `TimedOutboxArchiver.cs:73` `.GetAwaiter().GetResult()` if the archiver is used.
4. Under a Kubernetes CPU limit, `Environment.ProcessorCount` (and hence the pool's minimum worker
   count) is small. The pool then adds threads only gradually, through hill-climbing and starvation
   detection, so blocked threads are not quickly replaced (runtime behaviour, unverified here). CFS
   throttling also freezes every thread in the cgroup for the rest of each 100 ms period once its
   quota is used up.
5. Result: the sweeper's tick sits behind a long queue, and its own awaits
   (`TimedOutboxSweeper.cs:106,124`; every `SendAsync` in `DispatchAsync` at
   `OutboxProducerMediator.cs:1203`) are posted back to the same starved pool.

**Feedback loop through re-sends.** Starvation also delays `MarkDispatched` itself. If
confirm-to-mark latency exceeds `MinimumMessageAge` (5 s), a sweep that does run picks up messages
that were in fact delivered and re-sends them. That creates more delivery reports and more
`Task.Run` items. The defaults (`TimerInterval` 5 s = `MinimumMessageAge` 5 s) leave no headroom.

**Pool-queue ordering (unverified, .NET runtime behaviour).** The timer callback goes to the global
queue. Pool workers drain their **local** queues first, before taking from the global queue. Work
spawned from pool threads (the `ProduceAsync` continuation path; `Task.Run` at `:811` when called
from a pool thread) therefore jumps ahead of the sweeper tick.

#### Falsifiable predictions for H1

**P1-a: tick latency under saturation.** In an isolated process:
- Constrain the pool: `DOTNET_PROCESSOR_COUNT=1` or `2`, plus `ThreadPool.SetMinThreads(1–2, …)`
  and, where allowed, `SetMaxThreads`. Note that `SetMaxThreads` returns false below the processor
  count, so the env var or runtimeconfig is what actually lowers the ceiling.
- Before calling `StartAsync`, put N `Task.Run` items on the global queue that each block for X ms
  (e.g. N = 200, X = 50 ms, simulating confirm callbacks that hit a sync outbox or the semaphore at
  `:1334`).
- Prediction: the first `ObtainLockAsync` call on a **recording `IDistributedLock`** comes later than
  `StartAsync` by roughly ≥ N·X / workers, i.e. much more than `TimerInterval` (the first tick is due
  at `TimeSpan.Zero`, `:77`).
- Prediction: under *sustained* saturation, gaps between ticks are much larger than `TimerInterval`
  and irregular.
- Control: a loop on a **dedicated thread** (`new Thread` or `TaskCreationOptions.LongRunning` with a
  synchronous wait) under the same load keeps its ticks within ~`TimerInterval` + ε.
- **Refuted if** the `System.Threading.Timer` tick latency stays below `TimerInterval` under the same
  saturation.

**P1-b: outstanding-check amplifier.** With `InMemoryMessageProducer { UseAsyncPublishConfirmation =
true }` and a constrained pool, drive M Posts per second for T seconds.
- Prediction: the number of `OutstandingMessagesCheck` executions (or `RunningOutstandingMessageCheck`
  log lines at `:809`) is much greater than T / `maxOutStandingCheckInterval`.
- Prediction: the peak number of pool threads blocked at `:1334` is greater than 1.
- **Refuted if** the count stays ≤ ~T / 1 s and there is never more than one waiter.

**P1-c: re-sends from late confirms.** Under the same load, count messages the sweeper sends that
already had a success confirmation queued. Prediction: greater than 0 once p99
confirm-to-`MarkDispatched` latency exceeds `MinimumMessageAge`, and 0 without saturation.

**P1-d: production signals that separate H1 from H2.**
- Gaps between consecutive "Outbox Sweeper looking for unsent messages" logs are much larger than
  `TimerInterval`, with **no** "still running - abandoning attempt" warnings in between.
- `dotnet-counters` `threadpool-queue-length` keeps rising and `threadpool-thread-count` creeps up
  slowly.
- Kubernetes `container_cpu_cfs_throttled_periods_total` is non-zero.

#### Testability obstacles for a deterministic regression test
- `TimedOutboxSweeper` constructs its `Timer` directly (`:77`). There is no `TimeProvider` /
  `ITimer` seam, so the test cannot fake time and must use real wall-clock bounds. Assertions must
  be "delay > k·TimerInterval" with generous margins, not exact values.
- `Sweep` is private `async void` (`:104`), so there is no task to await and no completion event.
  - Usable seams: the injected `IDistributedLock` (its `ObtainLockAsync` is the first call per tick,
    `:106`) and the injected `IServiceScopeFactory` (`:111`, `:114`, which can return a fake mediator
    that records `ClearOutstandingFromOutboxAsync`).
  - `TimerInterval` is an `int` in seconds (`TimedOutboxSweeperOptions.cs:38`), so the smallest
    period is 1 s and tests will take seconds.
- The logger is static (`TimedOutboxSweeper.cs:47` via the settable static
  `ApplicationLogging.LoggerFactory`). Capturing logs means swapping a process-wide static: serialise
  such tests (compare `tests/Paramore.Brighter.Extensions.Tests/LoggerCaptureCollection.cs`) or rely
  on the lock/scope seams instead.
- Pool limits are process-wide:
  - `ThreadPool.SetMinThreads` / `SetMaxThreads` and `DOTNET_PROCESSOR_COUNT` affect the whole test
    host, and `DOTNET_PROCESSOR_COUNT` must be set before the runtime starts.
  - A trustworthy test needs a **separate child process** (no `RemoteExecutor`-style helper exists
    in `tests/`; a grep found none), or a dedicated test project whose runtimeconfig / env sets the
    limits.
  - At minimum it needs an xUnit collection with `DisableParallelization = true`, following
    `ThreadPoolSaturatingCollection.cs`. Even then, other test assemblies running in parallel under
    `dotnet test` share neither the process nor its pool, but they do compete for CPU.
- `InMemory.Tests` does not reference `Paramore.Brighter.Outbox.Hosting`. A test of
  `TimedOutboxSweeper` belongs in Core.Tests (which already references it), in a new isolated
  project, or InMemory.Tests needs a new project reference.
- `s_checkOutstandingSemaphoreToken` is `static` (`OutboxProducerMediator.cs:74`). Mediators in other
  tests in the same process share it, so P1-b must run in isolation.

### H2: a sweep hangs inside `DispatchAsync` and holds the lock
`DispatchAsync` sends one message at a time, awaiting each `SendAsync`
(`OutboxProducerMediator.cs:1187-1203`). For Kafka that awaits `ProduceAsync` until the broker acks
or fails (`KafkaMessagePublisher.cs:53`), which can take up to `MessageTimeoutMs` (5000 ms default,
`KafkaPublication.cs:88`), with `MessageSendMaxRetries = 3` inside librdkafka, plus any Brighter
resilience-pipeline retries. With `BatchSize = 100`, a degraded broker can make one sweep take
minutes. No cancellation or timeout is passed (`OutboxSweeper.cs:79`, `TimedOutboxSweeper.cs:124`).
`InMemoryLock` stays held (`InMemoryLock.cs:48`) until `ReleaseLockAsync` at
`TimedOutboxSweeper.cs:129`.
- **Distinguishing signal:** every tick in that window logs "Outbox Sweeper is still running -
  abandoning attempt" (`:136`) at the expected 5 s cadence, i.e. ticks *are* being scheduled.
  Per-message `SendAsync` durations approach `MessageTimeoutMs`.
- **Falsified if** no "abandoning attempt" warnings appear during the stall.

### H3: `_backgroundClearSemaphore` contention → `SkippingDispatchOfMessages`
The semaphore is per mediator instance (`OutboxProducerMediator.cs:70`). The mediator is a singleton
(`ServiceCollectionExtensions.cs:419-423`, `:557-574`). The only path into
`BackgroundDispatchUsingAsync` is `ClearOutstandingFromOutboxAsync` (`:495-506`), and in `src/` its
only caller is `OutboxSweeper.cs:79`. `Post` / `PostAsync` take the explicit-id `ClearOutbox` /
`ClearOutboxAsync` path (`CommandProcessor.cs:720`, `:786`), which does **not** touch this
semaphore. Within one `TimedOutboxSweeper`, the `InMemoryLock` already serialises sweeps, so
contention needs a second caller: a second sweeper registration, or user code calling
`ClearOutstandingFromOutboxAsync`.
- **Low prior.**
- **Distinguishing signal:** the `SkippingDispatchOfMessages` log (`:775`) shows up alongside
  "looking for unsent messages" logs.

### H4: sweeps run but are slow or filtered
- `OutstandingMessages` sorts **all** retained entries, including dispatched ones, before filtering
  (`InMemoryOutbox.cs:571-578`). Dispatched entries are kept for 5 min (`InMemoryBox.cs:68`), and the
  expiry scan runs every 10 min (`:76`). So at high throughput, every sweep (and every 1 s
  outstanding check, `:631-632`) sorts a large collection, which adds CPU pressure and feeds H1.
- Topics whose circuit breaker has tripped are excluded (`:576`, fed from
  `OutboxProducerMediator.cs:738`). Failed confirms trip the wire topic (`:893`) and failed sends do
  too (`:1223`). Cool-down happens only once per sweep (`:724`), so if confirms keep failing, a topic
  can stay excluded.
- **Distinguishing signal:** `FoundMessagesToClear` (`:745`) reports 0 or far fewer messages than are
  outstanding, while ticks are on schedule; or sweep duration grows with `EntryCount`.

### H5: unhandled exception from `async void Sweep` (side observation)
`BackgroundDispatchUsingAsync` rethrows (`OutboxProducerMediator.cs:762`) after logging, for example
when `OutstandingMessagesAsync` or producer lookup fails. `OutboxSweeper.SweepAsync` does not catch.
`TimedOutboxSweeper.Sweep` is `async void` with only `try/finally` (`:104-133`), so the exception
escapes to the thread pool and, by default .NET behaviour, kills the process. On Kubernetes that
appears as pod restarts, and every unsent message in the in-memory outbox is lost.
`TimedOutboxArchiver` does catch everything (`TimedOutboxArchiver.cs:129-132`); the sweeper does not.
- **Distinguishing signal:** `ErrorWhileDispatchingFromOutbox` followed by an unhandled-exception
  crash or container restart count above 0.

### H6: configuration check
`TimeSpan.FromSeconds(_options.TimerInterval)` is used as the timer **period**
(`TimedOutboxSweeper.cs:77`). Per `System.Threading.Timer` semantics, a period of zero or `Infinite`
turns off repeating, so `TimerInterval = 0` would sweep exactly once ("never scheduled" afterwards).
- **Distinguishing signal:** exactly one "looking for unsent messages" log after start-up. Check the
  affected teams' `UseOutboxSweeper` options.

### Checked and ruled out as causes on their own
- The sweeper's scoped resolution of `IAmAnOutboxProducerMediator` returns the same singleton `Post`
  uses, so there is no split-brain outbox (`ServiceCollectionExtensions.cs:419-423`).
- The Kafka sync `Send` path does not `Flush` or block on a pool thread. `Produce` enqueues to
  librdkafka (`KafkaMessagePublisher.cs:44`), and `Flush` is only on dispose
  (`KafkaMessageProducer.cs:399`).
- On Confluent's delivery thread, Brighter does only header parsing and a short lock before handing
  off with `Task.Run` (`KafkaMessageProducer.cs:405-471`).

## Maintainer Guidance (recorded after triage)

These loosen the testability obstacles listed under H1:

- **`TimedOutboxSweeper` tests may be added.** There are none today.
- **A time seam is acceptable.** Add an optional `TimeProvider` parameter that defaults to the real
  timer. Brighter guarantees **source** compatibility only, not binary, so an optional constructor
  parameter is fine.
- **`Sweep` may be made public for testing.** That gives a test something to call and await
  directly, instead of inferring ticks from a recording `IDistributedLock`.
- **A harness under `samples/` is acceptable.** It is preferred to a test that tries to control
  process-wide thread-pool limits. Use it to reproduce starvation under constrained CPU
  (`DOTNET_PROCESSOR_COUNT`, `docker --cpus`) with Post + InMemoryOutbox + async confirms
  (`InMemoryMessageProducer { UseAsyncPublishConfirmation = true }` and/or Kafka).
- **OpenTelemetry blind spots are in scope.** If the sweeper's health cannot be seen through
  Brighter's telemetry, fixing that is part of this defect. Gaps found so far (to be confirmed):
  - **No tick-lag signal.** Nothing records the delay between when a tick was due and when `Sweep`
    actually ran, which is the H1 signal.
  - **Lock-abandon and mediator-skip are logs only.** The H2 "still running - abandoning attempt" and
    H3 `SkippingDispatchOfMessages` events have no metric.
  - **Sweep duration and messages found per sweep are visible only in sampled traces.** The
    "Implicit Clear Outbox" span is created via the obsolete `ApplicationTelemetry`
    (`OutboxSweeper.cs`). It shares the `Paramore.Brighter` source name, so it is exported, but it
    is not a metric.
  - **No gauge for the outbox's outstanding count.** `_outStandingCount` is computed in
    `OutboxProducerMediator` but not exported.
  - **No confirm-to-dispatched latency.** The time from `Post` to `MarkDispatched` is the P1-c
    signal.
  - **Thread-pool health is a runtime metric, not Brighter's.** Queue length and thread count come
    from the .NET `System.Runtime` meter. The sample and docs should turn it on rather than Brighter
    re-exporting it.

## Confirmed Root Cause

**Verdict: H1 PARTIALLY CONFIRMED.** The mechanism is proven, but the driver named in triage is
refuted as the cause on its own. A separate, more severe defect was proven along the way.

1. **Proven: the sweeper's tick is only as reliable as the shared thread pool.**
   `TimedOutboxSweeper` runs `async void Sweep` from a `System.Threading.Timer`
   (`TimedOutboxSweeper.cs:77`, `:104`). When pool workers are **blocked or monopolised**, ticks are
   delayed 3–4.5× the interval. A dedicated thread under the same load ticks exactly on time
   (Experiments A and C). The comment at `:128`, "on a timer thread, so blocking is OK", is wrong:
   the callback runs on a pool thread.

2. **Refuted as a sole cause: the per-confirm `Task.Run` flood.** On .NET 9 a timer firing is
   **not** queued behind ordinary pool work. 100k+ pending short `Task.Run` items did not delay a
   single tick (Experiment A, flood mode). The likely reason is that the runtime queues timer
   firings at high priority; that is runtime behaviour, observed rather than read in source. Under
   fair load, where callers yield as real request traffic does, the Kafka-style per-confirm
   `Task.Run` kept up and the sweeper ticked at 1.00 s (Experiment D). Ticks are delayed only when
   workers are **busy or blocked**, not when the queue is merely long.

3. **Proven, separate and severe: the outstanding-message check runs on every Post under the
   default DI wiring.** It blocks pool threads and collapses throughput.
   - **The interval defaults to zero under DI.** `ProducersConfiguration.MaxOutStandingCheckInterval`
     defaults to `TimeSpan.Zero` (`ProducersConfiguration.cs:207`), and DI passes it straight
     through (`ServiceCollectionExtensions.cs:849`). The mediator's 1 s fallback,
     `?? TimeSpan.FromMilliseconds(1000)` at `OutboxProducerMediator.cs:184`, applies only to
     `null`, so it never kicks in.
   - **So every Post queues a check.** With a Zero interval, the gate at `:803` never holds, and
     every `ClearOutbox` / `ClearOutboxAsync` (`:411`, `:480`) calls `Task.Run(OutstandingMessagesCheck)`
     (`:811`).
   - **Each check blocks a pool thread and sorts the whole outbox.** It parks the thread on a
     **static** semaphore with a synchronous `Wait()` (`:1334`), then runs an `OrderBy` over every
     retained outbox entry (`InMemoryOutbox.cs:631-632`).
   - **The count is computed even when there is no limit.** It runs when `MaxOutStandingMessages`
     is -1, the default (`ProducersConfiguration.cs:200`), even though the result is then never used
     (`:788`). The docs at `ProducersConfiguration.cs:205` say the opposite.
   - **Even with a 1 s interval, nothing stops the checks piling up.** The timestamp is refreshed
     only *inside* the queued item, after `Wait()` (`:1336`), and there is no in-flight guard.
     Under overload this queued 235k–365k checks in 12 s (Experiment C).
   - **Measured cost under fair load:** about 1,270 checks/s, and Post throughput fell **~30×**
     (11.7k vs 354k posts in 9 s, Experiment D). With a DB outbox the same path is a blocking COUNT
     query on every Post.

4. **Proven consequence under overload: delivered messages are re-sent.** Late confirms leave
   delivered messages looking undispatched past `MinimumMessageAge`, so the sweeper re-sends them.
   Experiment C saw 100 re-sends per sweep.

**Not established:** what blocks or monopolises pool threads in the reporters' EKS pods. Candidates:
- the outstanding check (3)
- `KafkaMessageConsumer.ReceiveAsync`, which runs a blocking `Consume(timeout)` inside `Task.Run`
  (`KafkaMessageConsumer.cs:577-591`), pinning a pool thread for every receive when consumers share
  the process
- `TimedOutboxArchiver.cs:73` `GetAwaiter().GetResult()`
- `InMemoryScheduler.cs:312,321` `BrighterAsyncContext.Run`
- sync-over-async in application code
- CPU (CFS) throttling

A `dotnet-dump` of an affected pod (`threadpool`, `clrstack -all`) would settle this.

## Evidence

- [x] **Code trace** (Plan sub-agent, opus; all triage line references re-verified at `2890f8610`):
  1. `TimedOutboxSweeper.cs:77` → pool `Timer` → `async void Sweep` (`:104`). There is no seam and
     no telemetry.
  2. DI: `ProducersConfiguration.cs:200,207` → `ServiceCollectionExtensions.cs:848-849` →
     `OutboxProducerMediator.cs:184`, so the interval stays Zero.
  3. `Post`: `CommandProcessor.cs:720` → `OutboxProducerMediator.cs:411`. `PostAsync`:
     `CommandProcessor.cs:786` → `:480`. Both reach `CheckOutstandingMessages` (`:795-814`) →
     `Task.Run` (`:811`) with no dedup.
  4. `:1334` blocks on the static `SemaphoreSlim` (`:74`); `:1336` refreshes the timestamp only
     after that; `:1340-1360` counts even at limit -1.
  5. Kafka confirms are wired to the **async** callback (`:826`, `:845`;
     `KafkaMessageProducer.cs:51`). That costs one `Task.Run` per report (`:456`) and **never
     blocks**: `MarkDispatchedAsync` completes synchronously (`InMemoryOutbox.cs:471-477`).
     **Correction to triage:** one pool item per confirm, not two.
  6. **Correction to triage:** the sweep's own lock and outstanding-query awaits complete
     synchronously. Only each `SendAsync` (`:1203`) hops to the pool.

- [x] **Executable experiments.** These are scratch console apps in the session scratchpad that
  reference the repo projects; they are not committed. All ran on net9.0 with
  `DOTNET_PROCESSOR_COUNT` constrained, measuring real `TimedOutboxSweeper` ticks through a
  recording `IDistributedLock` at `TimerInterval = 1`.

  **Experiment A** (`starve/`): which kinds of pool load delay the timer?

  | Load | procs | Real sweeper (pool `Timer`) | Dedicated thread | `PeriodicTimer` async loop |
  |---|---|---|---|---|
  | none | 1 / 2 / 10 | 10 ticks, max gap 1.00 s | 10, 1.00 s | 10, 1.00 s |
  | 200 blocked pool items | 1 | **7 ticks, first tick at 1.01 s, max gap 3.03 s** | 10, 1.00 s | **4, 2.00 s** |
  | 200 blocked pool items | 2 | **6 ticks, max gap 3.01 s** | 10, 1.02 s | **5, 3.02 s** |
  | flood: 20k/s `Task.Run` from a non-pool thread, 100k pending | 1 / 2 | 10, 1.00 s | 10, 1.00 s | 10, 1.00 s |

  **Experiments C and D** (`pileup/`): real `CommandProcessor` + `OutboxProducerMediator` +
  `InMemoryOutbox` + real `TimedOutboxSweeper` with `InMemoryLock`, and a fake producer that mimics
  Kafka (a non-pool "delivery thread" doing one `Task.Run` per confirm). 64 concurrent posters,
  12 s, procs = 2.
  - **C, saturating load** (posters never yield):
    - The sweeper's max gap was 3.5–4.5 s in every configuration, including with the check disabled.
    - Outstanding-check backlog: 235k at the 1 s interval, 365k at the Zero (DI) interval.
    - About 100 delivered messages were re-sent per sweep.
  - **D, fair load** (posters `await Task.Yield()`):
    - Check disabled: 350k posts, sweeper max gap 1.00 s, 0 re-sends.
    - 1 s interval: 354k posts, 12 checks, max gap 1.01 s.
    - Zero (DI default): **11.7k posts (~30× fewer)**, 11.7k checks, max gap 1.00 s.

- [ ] **Red repro for the durable test.** This is deterministic and needs no pool limits.
  - **Setup:** build the mediator as DI does (`maxOutStandingMessages: -1`,
    `maxOutStandingCheckInterval: TimeSpan.Zero`), with an outbox decorator whose
    `GetOutstandingMessageCount` counts calls and blocks on a gate. Post K times, then open the gate.
  - **Today:** K checks are queued and run, and K−1 pool threads park at `:1334`.
  - **Assertion:** with limit -1, zero checks run. With a limit set, at most one check is in flight
    or queued at any time.
  - The sweeper-tick test (a tick runs while N pool items are blocked) needs the new seam: either a
    dedicated-thread loop or the `TimeProvider` option.

## Suggested-Fix Assessment

- **Outstanding check (not in the original fix list; should come first): CONFIRMED needed.**
  - Skip it when `MaxOutStandingMessages == -1`.
  - ~~Treat a Zero interval as the 1 s default~~ — **rejected on #4554**: the interval is also the
    minimum age passed to `GetOutstandingMessageCount`, so the maintainer agreed to keep Zero.
    Skipping at -1 removes the per-Post check for default users anyway. Fix the docs.
  - Add an `Interlocked` in-flight flag and set the timestamp before queuing.
  - Use a non-blocking acquire instead of `Wait()`.
  - Make the semaphore per instance.
- **(i) Dedicated sweep thread + optional `TimeProvider` + public `Sweep`: CONFIRMED for the
  tick, PARTIAL for the sweep body.**
  - Experiment A shows a dedicated thread is immune and a `PeriodicTimer` async loop is **not**. A
    plain `BackgroundService` with `await PeriodicTimer` therefore does not fix this.
  - After the first real `SendAsync`, the sweep body still continues on the pool. Bound it with a
    cancellation token or timeout.
  - Replace `async void` with a catch-all, as `TimedOutboxArchiver.cs:129-132` does.
- **(ii) Stop one `Task.Run` per Kafka delivery report: PARTIAL.** It is not what starves the tick,
  but under overload it produces an unbounded queue (~1 pending item per Post in Experiment C),
  delayed `MarkDispatched`, and re-sends of delivered messages. Per your note, it is worth fixing on
  its own merits: a single-reader `Channel` drained by a long-running worker (the pattern in
  `InMemoryMessageProducer.cs:264-281`), or bounded parallelism.
- **(iii) OpenTelemetry: CONFIRMED gap.** Nothing today measures:
  - tick lag
  - sweep duration and messages found
  - abandon and skip counts (`:136`, `:775`)
  - outstanding-check queued and in-flight counts
  - the outstanding gauge
  - confirm-to-dispatched latency

## Scope Notes

- **Outstanding-check defect affects every outbox, not just InMemory.** With a DB outbox it is a
  blocking COUNT query on every Post. Docs disagree: interface `:89` vs `ProducersConfiguration.cs:205`.
- **H2 refuted as "never completes"; it is "slow but bounded".**
  - Each `ProduceAsync` is bounded by `MessageTimeoutMs` (5 s), and Polly makes 3 attempts.
  - The worst case is about 8 minutes per 100-message sweep, during which every tick logs "abandoning".
  - No cancellation token is passed anywhere.
- **H5 confirmed (conditional).** Two failure modes:
  - Exceptions rethrown at `OutboxProducerMediator.cs:762` escape `async void Sweep` and crash the
    process; one example is a `ConfigurationException` from `LookupBy` at `:1194`.
  - **New:** `CreateScope()` at `TimedOutboxSweeper.cs:111` is **outside** the `try`. If it throws
    (for example during shutdown), `InMemoryLock` is never released and every later tick logs
    "abandoning attempt" forever.
- **H6 confirmed as possible.** `TimerInterval` is not validated: 0 sweeps once, and a negative value
  makes `StartAsync` throw.
- **Same timer/blocking pattern elsewhere:**
  - `TimedOutboxArchiver.cs:73` (`GetAwaiter().GetResult()` in the timer) and `:120`
  - `HeartbeatHostedService.cs:25`
  - `InMemoryScheduler.cs:312,321`
  - `TimeoutPolicyHandler.cs:120-122,152`
  - `KafkaMessageConsumer.cs:577-591` (blocking `Consume` inside `Task.Run`), `:231`, `:421`, `:934`
- **Per-confirm `Task.Run` parity:** `RMQ.Sync/RmqMessageProducer.cs:282-307` has the same pattern.
  `RMQ.Async` does not.
- **Test stand-in fidelity:** `InMemoryMessageProducer { UseAsyncPublishConfirmation = true }` runs
  confirms serially. It also adds every raise task to `_raiseTasks` (`:50`, `:286`), which grows
  without bound in long runs, so it is a poor stand-in for Kafka in the sample harness.
- **Minor:**
  - `KafkaMessageProducer.cs:297` catches `ProduceException<string,string>`, but the producer is
    `<string, byte[]>`, so that catch never matches.
  - `InMemoryBox.cs:72` comment says 5 min; the value at `:76` is 10 min.
- **Agreed scope (#4560):** the sweeper (dedicated tick thread, optional `TimeProvider`, public
  awaitable `Sweep`, catch-all, lock released on every path, `TimerInterval` validation, cancellation),
  a bounded-channel confirm drain for Kafka and RMQ.Sync, sweeper OpenTelemetry metrics, and a
  `samples/` harness. **The harness was later dropped** (maintainer, 2026-10-08): the starvation
  regression tests and the live-broker confirm-ordering tests already prove the fix, and
  `diagnostic/` stays as the end-to-end tool. **Out of scope:** the outstanding check (owned by #4554; evidence posted
  there) and #4552 (review note posted).
- **Superseded recommendation: split the work.** Fix the outstanding check and make the sweeper robust (dedicated
  tick thread, catch-all, lock leak, interval validation, telemetry) in this bugfix. Track the Kafka
  and RMQ.Sync per-confirm `Task.Run` and the other timer/blocking sites as follow-up issues, or
  widen this fix if you prefer.

## Regression Test

1. `tests/Paramore.Brighter.Core.Tests/Sweeping/When_thread_pool_workers_are_all_blocked_should_still_sweep_the_outbox.cs`
   (approved). The pool is capped at `ProcessorCount` and every worker is parked on a gate. The
   assertion is that an undispatched message reaches the `InternalBus` within 3 s.
   - **RED:** failed 3/3 with "did not dispatch the outstanding message within 00:00:03". With
     starvation removed it passes in 90 ms.
   - **Collection:** runs in `ThreadPoolStarvationCollection` (`DisableParallelization`).
   - **Second defect, seen live while RED:** the starved tick fired *after* `StopAsync` and after
     the provider had been disposed. `CreateScope()` (`TimedOutboxSweeper.cs:111`, outside the
     `try`) threw `ObjectDisposedException`, the exception escaped `async void Sweep`, and it crashed
     the test host. The test leaves the provider undisposed until the fix lands.
   - **Refactored** onto the `Sweeping/TestDoubles/StarvedThreadPool` helper. It proves starvation
     with a probe `Timer` that can't fire, and releases the pool if it fails.
2. `.../Sweeping/When_stopping_during_a_sweep_should_wait_for_the_sweep_to_finish.cs`.
   - A `GatedDistributedLock` holds a sweep in flight.
   - **RED (5/5):** "StopAsync returned while a sweep was still running".
   - **Revised from the first idea, "don't sweep after stop", which was not a defect:** a timer firing
     that has not started yet is cancelled by `Change(Infinite)`. The real defect is that `StopAsync`
     does not wait for a sweep that is already running.
3. `.../Sweeping/When_a_sweep_throws_should_sweep_again_on_the_next_tick.cs`.
   - **RED:** the exception escapes `async void Sweep` and **crashes the test host**.
4. `.../Sweeping/When_creating_the_sweep_scope_throws_should_release_the_lock_and_sweep_again.cs`
   (uses `TestDoubles/FailsFirstScopeFactory`).
   - **RED:** crashes the test host. The lock leak would keep it red once the crash is fixed.
5. `.../Sweeping/When_the_timer_interval_is_not_positive_should_reject_the_options.cs` (Theory: 0, -1).
   - **RED:** no `ConfigurationException` is thrown.
6. `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_confirming_many_messages_should_raise_confirmations_one_at_a_time_in_order.cs`
   (Kafka category, live broker).
   - **Decision:** one worker drains confirms in order.
   - **RED (3/3):** the most handlers running at once is **50 of 50** (expected 1).
7. `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_confirming_many_messages_should_raise_confirmations_one_at_a_time_in_order.cs`
   (RMQ category, live broker).
   - **RED (3/3):** the most handlers running at once is 15–23 (expected 1).
8. **Sweeper telemetry, per ADR 0081 (Accepted, commit `94527b8c2`).** All these files are in
   `tests/Paramore.Brighter.Core.Tests/Sweeping/`:
   - `When_a_sweep_completes_should_record_a_completed_sweep_and_its_duration.cs`
   - `When_the_sweep_lock_is_held_should_record_a_lock_unavailable_sweep.cs`
   - `When_a_sweep_throws_should_record_a_failed_sweep.cs`
   - `When_a_sweep_starts_late_should_record_how_late_it_started.cs`. This uses a `FakeTimeProvider`:
     the first lag is 0 s. Advancing 7 s past a sweep due at 5 s makes the second lag 2 s.
   - `When_the_sweeper_meter_throws_should_still_sweep_the_outbox.cs`
   - **Test doubles:** `RecordedSweeperMeasurements` (a `MeterListener` scoped to its own
     `IMeterFactory`), `SweeperMeasurement`, `HeldDistributedLock`, `ThrowingSweeperMeter` and
     `SweepableOutbox`.
   - **RED:** compilation fails because `IAmABrighterSweeperMeter`, `SweepOutcome`, `SweeperMeter`
     and the `meter:` and `timeProvider:` constructor parameters do not exist yet.

9. **Reopened after review of #4562 (2026-10-09), finding #1/#2.**
   `.../Sweeping/When_thread_pool_workers_are_all_blocked_and_sends_complete_off_the_pool_should_still_sweep_a_batch.cs`
   (uses `TestDoubles/OffPoolCompletingProducer`).
   - **Why:** test 1 uses `InMemoryMessageProducer`, whose `SendAsync` completes inline, so it only
     proves the tick fires. Confluent.Kafka 2.15.1's `TypedTaskDeliveryHandlerShim` completes
     `ProduceAsync` with `RunContinuationsAsynchronously` (checked in its IL), from librdkafka's thread.
     On the sweeper thread, which has no `SynchronizationContext`, every continuation after an awaited
     send goes to the pool.
   - **The double** completes each send from a dedicated delivery thread through a
     `RunContinuationsAsynchronously` TCS, as Confluent does. Five messages are outstanding.
   - **RED (3/3):** "dispatched 1 of 5 outstanding messages within 00:00:03".

10. **Review #3 (batched drain), ADR 0081 amendment.**
    - `Tasks/When_more_callbacks_are_queued_than_the_batch_size_should_run_a_batch_at_once.cs`. RED
      (3/3): "Expected 32 callbacks in progress at once, but at most 1 were".
    - `Tasks/When_confirmations_are_waiting_should_report_the_queue_depth.cs` (doubles
      `Tasks/TestDoubles/ObservedQueueDepths`, `QueueDepth`). RED: the meter and the tagged queue
      constructor did not exist.
    - **Replaced** tests 6 and 7 with `When_confirming_many_messages_should_raise_confirmations_in_overlapping_batches_off_the_thread_pool`
      (Kafka and RMQ.Sync): 2–32 handlers at once and none on a pool thread. Mutation-checked on both
      brokers: a serial drain gives "Actual: 1"; no `BrighterAsyncContext` gives "ran on a
      thread-pool thread".
11. **Review #4.** `Tasks/When_a_callback_is_first_queued_from_a_flow_with_ambient_state_should_not_carry_it_into_callbacks.cs`
    and `Sweeping/When_the_sweeper_is_started_from_a_flow_with_ambient_state_should_not_carry_it_into_sweeps.cs`
    (`TestDoubles/AmbientRecordingLock`). RED (3/3): both saw the starting caller's `AsyncLocal` value.
12. **Review #5.** `Sweeping/When_the_wall_clock_steps_backwards_should_still_sweep_one_interval_later.cs`
    (`TestDoubles/SteppableWallClock`). RED (3/3): no sweep one interval after a one-hour backward step.

13. **Characterisation of the Polly `continueOnCapturedContext` change on a Proactor pump** (raised by the
    maintainer): `MessageDispatch/Proactor/When_a_handler_clears_the_outbox_should_run_the_resilience_pipeline_on_the_pump_thread.cs`
    (`TestDoubles/FailsOnceOffPoolProducer`, `OutboxClearingCommandProcessor`).
    - **Green on arrival.** RED under the named mutation (Polly given its default of `false`): `OnRetry`
      ran on pool threads 5 and 10, not pump thread 19.
    - It pins that a Proactor handler's post or clear completes, runs Polly's callbacks on the pump
      thread, and resumes there. Blocking on `PostAsync` from the pump thread already deadlocked, so
      no new deadlock is introduced.

## Fix

Four commits on `bugfix/4560-timed-outbox-sweeper-starvation`:

1. `94527b8c2` **docs:** ADR 0081, sweeper health metrics.
2. `84341b0bb` **refactor (structural only):**
   - adds `IAmABrighterSweeperMeter`, `SweepOutcome` and `NullSweeperMeter`;
   - adds the instrument names to `BrighterSemanticConventions`;
   - adds optional `timeProvider` and `meter` parameters to the `TimedOutboxSweeper` constructor.
3. `85b219f14` **fix: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs`**
   - **Sweeps run on a dedicated thread.** The thread waits with a real-time timeout, so a starved
     pool cannot stop it, and with a `TimeProvider` timer, so a test clock can wake it.
   - **The schedule keeps to its due times.** Lag on the next sweep shows any overrun. The sweeper
     re-anchors only when a sweep starts a whole interval or more late, so it never sweeps back to
     back.
   - **`StopAsync` waits for an in-flight sweep.**
   - **A failed sweep is caught and logged**, and `SweepOutcome.Failed` is recorded.
   - **The lock is released on every path**, because `CreateScope` now runs inside the `try`.
   - **`TimerInterval` below 1 throws `ConfigurationException`.**
   - **New `SweeperMeter`**, registered in `AddBrighterInstrumentation`.
   - Both tests that had kept their provider undisposed now dispose it again.
4. `185282398` **fix:** a new `src/Paramore.Brighter/Tasks/SerialCallbackQueue.cs`, used by
   `KafkaMessageProducer` and RMQ.Sync `RmqMessageProducer`. It replaces one `Task.Run` per
   confirmation with a single in-order worker, and `Dispose` completes the queue.

5. `0e84c1094` **fix:** `SerialCallbackQueue` drains on one dedicated background thread, created with the
   first callback, instead of a worker started with `Task.Run`. Each callback runs in a
   `BrighterAsyncContext`, and a confirmation that arrives after `Complete` is dropped instead of
   throwing on the broker's thread.
   - **Test:** `tests/Paramore.Brighter.Core.Tests/Tasks/When_the_thread_pool_is_starved_should_still_run_queued_callbacks.cs`.
   - **RED (3/3)** before the change, green afterwards.
6. **docs:** a release note in `release_notes.md` covering the behaviour changes and the cost of one
   thread per confirming producer.

**Targeted results:**

| Tests | Result |
| --- | --- |
| Core.Tests `Sweeping` | 11/11 green, 6 runs on net9.0 and 2 on net10.0 |
| Full Core.Tests, net9.0 | 1636 passed, 7 skipped |
| Kafka confirm-ordering test | green 3/3 |
| RMQ.Sync confirm-ordering test | green 3/3 |
| Existing Kafka confirmation and dispose tests | 12/12 |
| Existing RMQ.Sync confirmation and dispose tests | 30/30 |


### Fixes after review of #4562 (2026-10-09)

1. **Review #1/#2:** `eece13a93` test, `80512a496` fix.
   - The sweep runs in `BrighterAsyncContext.Run` on the sweeper thread.
   - **Running it in a context was not enough on its own.** A `ConfigureAwait(false)` continuation is
     not inlined on a thread with a `SynchronizationContext`; it goes to the pool. Two such hops were
     found by tracing threads in the test double:
     - `BackgroundDispatchUsingAsync`, whose only caller is the sweeper, now passes
       `continueOnCapturedContext: true`;
     - `ExecuteWithResiliencePipelineAsync` now passes the caller's `continueOnCapturedContext` to
       Polly through a pooled `ResilienceContext`. It used to get Polly's default of `false`.
   - Removing either change turns the test red again.
   - The reviewer's claim that `ConfigureAwait(false)` "resumes on whichever thread completed the
     inner task" is wrong in exactly this case.
   - Confluent.Kafka 2.15.1's `TypedTaskDeliveryHandlerShim` passes `RunContinuationsAsynchronously`
     (`ldc.i4.s 0x40`); this was checked in its IL.
2. **Review #3:** `10287babe` rename; `5bd972f69`/`cf8da1438` batched drain; `0ff90cfa6` replaced
   broker tests; `398db5c2a`/`e4d826dbf` queue-depth metric; ADR 0081 amended (`docs:` commit).
3. **Race fix, found while testing #3:** `08a8ff64c` re-checks the due time after creating the timer.
   The tick-lag test had failed 2 of 8 runs under load; it was green 10/10 afterwards.
4. **Review #4:** `964a85cc8`/`5e08f53ac`: `ExecutionContext.SuppressFlow()` around both thread starts.
5. **Review #5:** `232d069dc`/`3ad95ee1c`: the schedule uses elapsed time from `GetTimestamp`.

**Review points not acted on:**
- **6a:** `StopAsync`'s continuation needs a pool thread. This is bounded by the host's shutdown
  timeout.
- **6b:** a sweep in flight is not cancelled on stop. This is by design, and a test covers it.
- **6c:** "log the backlog at dispose". This is already done: `WaitForConfirmationCallbacks` logs
  `stillInFlight`.

## Verify

Full suites run on 2026-10-08 against `ea92447c9` (all branch commits), on net9.0 and net10.0. The
Kafka suite ran against the local `kafka` container and RMQ.Sync against the shared RabbitMQ.

| Suite | net9.0 | net10.0 |
| --- | --- | --- |
| Core.Tests | 1637 passed, 0 failed, 7 skipped | 1637 passed, 0 failed, 7 skipped |
| Kafka.Tests | 239 passed, 2 failed | 239 passed, 2 failed |
| RMQ.Sync.Tests | 192 passed, 10 failed, 1 skipped | 192 passed, 10 failed, 1 skipped |

Every regression test listed above passed. The failures are the same on both frameworks, and all of
them come from the local environment rather than this change:

- **Kafka (2):** `KafkaMessageProducerHeaderBytesSendTests` (Reactor and Proactor) fail with
  `Connection refused (localhost:8081)`. They need a Confluent Schema Registry, and none is running.
- **RMQ.Sync, mTLS (9):** the `RmqMutualTls*` acceptance and observability tests fail with
  `FileNotFoundException: Client certificate not found`. They need `tests/generate-test-certs.sh` and
  a broker set up for TLS.
- **RMQ.Sync, `DispatchBuilderTests.When_Building_A_Dispatcher` (1):** fails with
  `PRECONDITION_FAILED - inequivalent arg 'durable' for queue 'mary'`. A durable `mary` queue
  already exists on the shared broker, which belongs to another worktree, and the RMQ.Async
  dispatcher tests use the same queue name. This branch does not touch `ChannelFactory` or the
  dispatch tests.

RMQ.Async.Tests was not run: RMQ.Async's `RmqMessageProducer` is unchanged.

**Dropped from scope:** the `samples/` harness (see Scope Notes).

### Re-verify after the review fixes (2026-10-09)

The run was at `6f180d632`, on net9.0 and net10.0. A fresh Kafka container ran with
`schema-registry`, alongside a stock RabbitMQ on 5672 and the native-delay RabbitMQ on 5673.

| Suite | net9.0 | net10.0 |
| --- | --- | --- |
| Core.Tests | 1661 passed, 7 skipped | 1661 passed, 7 skipped |
| Kafka.Tests | **265/265** | **265/265** |
| RMQ.Sync.Tests | 186 passed, 1 skipped, without mTLS | 193 passed, 1 skipped, 9 mTLS failed |
| Extensions.Tests | 753 / 750, all passed | |
| Testing.Tests, InMemory.Tests | all passed | all passed |
| RMQ.Async.Tests | 358 passed, 10 failed: 8 mTLS and 2 `mary` | same |
| Extensions.AspNetCore.Tests | flaky, see below | flaky |

**Environment problems found and fixed along the way:**
- **Clock drift:** the Podman VM's clock was 1 h 19 min behind the host, so Kafka produced
  `Invalid timestamp` and about 110 consumer tests failed. It was stepped with `chronyc makestep`.
- **No native-delay broker:** port 5673 had no broker. The image from `docker/RabbitMQ/Dockerfile`
  crashes with `.erlang.cookie: eacces`, probably because `rabbitmq-plugins enable --offline` runs as
  root during the build. It was run with `--user 0`, so the entrypoint fixes ownership and drops
  privileges.

**Failures that remain are environmental:**
- The mTLS tests need generated certificates.
- `DispatchBuilderTests` fail on a `mary` durable-queue mismatch, because RMQ.Sync and RMQ.Async
  share the queue name.

**Extensions.AspNetCore is flaky.** `UseInboxHandler<T>`'s static initializer throws
`ObjectDisposedException`, because the shared logger factory has already been disposed. The failure
count varies from run to run (1–6), and a rerun passes 65/65 on both frameworks.
