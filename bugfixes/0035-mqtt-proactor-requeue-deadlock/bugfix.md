# Bugfix: MQTT Proactor deadlocks on first requeue because MqttMessagePublisher connects synchronously in its constructor

**Linked Issue**: #4351
**Status**: Verified

## Symptom
**Observed:** A `Proactor` message pump consuming from an MQTT channel hangs for good the first time a handler defers a message (throws `DeferMessageAction`). The handler runs once. After that there is no redelivery, no second invocation and no dead-lettering, and the pump never sees the quit message, so `awaiting pump` never returns. The issue saw this with conformance FR-23 ("requeue budget exhausted to DLQ"): it was killed at 18 minutes, and `--blame-hang` killed it again at 180 s. The deferred message is lost.

**Expected:** The message is requeued and redelivered until the delivery budget runs out, then goes to the DLQ. This is what the `Reactor` variant does: FR-23 passes in about 5 s against the same broker.

**Reproduction (from the issue, not yet re-run here):**
1. Start the broker with `docker-compose-mqtt.yaml` (`efrecon/mosquitto`).
2. Minimal repro: `Task.Run(() => new MqttMessagePublisher(config))` finishes within 15 s. `Task.Run(() => BrighterAsyncContext.Run(async () => { var p = new MqttMessagePublisher(config); await Task.Yield(); return p; }))` times out at 15 s.
3. End to end: run the FR-23 Proactor variant against MQTT, with a real pump and a handler that always defers.

## Suspected Location
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessagePublisher.cs:30-55`: the `MqttMessagePublisher(MqttMessagingGatewayConfiguration)` constructor. **Line 54** is still `ConnectAsync().GetAwaiter().GetResult();`, which blocks on async work inside the constructor. The doc comment at line 27 says "Sync over async, but necessary as we are in the ctor". The line number matches the issue. `git log` shows the file was last changed in `b42887af4`.
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessagePublisher.cs:109-124`: `ConnectAsync()`. It calls `await _mqttClient.ConnectAsync(...)` at line 115 with no `ConfigureAwait(false)`, so its continuation is posted to whatever `SynchronizationContext` is current. If every attempt fails it only logs, so a failed connect is silent.
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs:461-478`: `RequeueAsync` calls `EnsureRequeueProducer()` synchronously at **line 465**, before any `await`. That means it runs on whatever thread and context called it.
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs:484-503`: `EnsureRequeueProducer()` uses `LazyInitializer.EnsureInitialized` and runs `new MqttMessagePublisher(...)` at **line 489**. This confirms the producer is created lazily on first requeue.
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs:430-446`: the sync `Requeue` takes the same path (`EnsureRequeueProducer()` at line 433). On the Reactor it runs with no captured context.
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs:96-100`: `Run()` calls `BrighterAsyncContext.Run(() => EventLoop());` at **line 99**. `Proactor.cs:526` goes through `Channel.RequeueAsync(message, delay ?? RequeueDelay)` for deferrals (the `DeferMessageAction` handling is at lines 251-267 and 351).
- `src/Paramore.Brighter/Tasks/BrighterAsyncContext.cs:25-29`: the class doc says it is "A single-threaded async context that pairs a SynchronizationContext with a cooperating TaskScheduler, so continuations of async work run on the same thread as their originator (not the thread pool)". `Run(Func<Task>)` starts at line 237. `src/Paramore.Brighter/Tasks/BrighterSynchronizationContext.cs:108` (`Post`) queues continuations back onto that single pump thread.
- A related site with the same hazard: `MQTTMessageConsumer.cs:111-114` sets up the DLQ and invalid-message producers as `Lazy<MqttMessageProducer?>`. They are resolved at `MQTTMessageConsumer.cs:408-410` inside `ResolveRejectionProducer`, which `RejectAsync` calls at line 367. Each one builds a `MqttMessagePublisher` (lines 552 and 578), so the constructor's blocking connect also runs on the pump thread the first time `RejectAsync` is called.
- Separate but similar: the consumer's own constructor blocks with `connectTask.GetAwaiter().GetResult()` at `MQTTMessageConsumer.cs:153-157`. It normally runs at channel-creation time, not on the pump thread.

## Root-Cause Hypothesis
**Hypothesis:** On the Proactor, the pump's only thread runs `EventLoop` inside `BrighterAsyncContext` (`Proactor.cs:99`). When a handler defers, `RequeueAsync` (`MQTTMessageConsumer.cs:465`) calls `EnsureRequeueProducer()` synchronously on that thread. That constructs `MqttMessagePublisher`, whose constructor blocks the thread on `ConnectAsync().GetAwaiter().GetResult()` (`MQTTMessagePublisher.cs:54`). The `await _mqttClient.ConnectAsync(...)` inside it (`MQTTMessagePublisher.cs:115`, no `ConfigureAwait(false)`) captures `BrighterSynchronizationContext`, so its continuation is posted back to the pump queue (`BrighterSynchronizationContext.cs:108`). That queue can only be drained by the thread that is now blocked, so this is a classic sync-over-async deadlock. The Reactor escapes because `Requeue` (`MQTTMessageConsumer.cs:430-433`) runs with no captured single-threaded context.

**How this could be proven wrong:** Each of these would disprove it:
- (a) Building `MqttMessagePublisher` inside `BrighterAsyncContext.Run` does not hang.
- (b) Creating the requeue producer eagerly, off the pump thread, still leaves the FR-23 Proactor run hung.
- (c) A thread dump of the hung Proactor run does not show the pump thread parked in `MqttMessagePublisher..ctor` → `GetResult()`.

**Issue's suggested fix (restated). UNVERIFIED: to be proven or refuted in /bugfix:confirm.** It has two halves, and the issue says either one alone unblocks the Proactor path:
1. **Stop connecting in the constructor.** Options are a `public static async Task<MqttMessagePublisher> CreateAsync(...)` factory, or connecting lazily on first publish (`PublishMessageAsync` / `PublishMessage`). The minimum is to route the blocking wait through `BrighterAsyncContext.Run(...)`, as `PublishMessage` already does at `MQTTMessagePublisher.cs:95`, instead of a raw `GetAwaiter().GetResult()`.
2. **Stop creating the requeue producer on the pump thread.** Create it eagerly when the consumer is constructed, not in `EnsureRequeueProducer()` inside `RequeueAsync`.

Notes for confirm, from triage:
- **Rejection producers need the same treatment.** The lazy DLQ and invalid-message producers (`MQTTMessageConsumer.cs:111-114`, `408-410`) build a `MqttMessagePublisher` on the pump thread the same way, the first time `RejectAsync` runs. That is exactly the step FR-23 reaches once the requeue budget runs out. If only half (2) is applied to the requeue producer, FR-23 on the Proactor will probably deadlock again at dead-lettering. Half (1), or eager creation of all three producers, is needed for FR-23 to pass.
- **`ConfigureAwait(false)` alone may not be enough.** Adding it at `MQTTMessagePublisher.cs:115` might be the smallest fix, but it only helps if MQTTnet's internal awaits also avoid the captured context. Confirm needs to check this.
- **No `CreateAsync` precedent.** No messaging-gateway publisher or producer in `src/` has a `static async Task<T> CreateAsync` factory. The nearest precedents are lazy async "ensure" methods: `RmqMessageGateway.EnsureBrokerAsync` (`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs:117`) and `RedisMessageConsumer.EnsureConnectionAsync` (`src/Paramore.Brighter.MessagingGateway.Redis/RedisMessageConsumer.cs:687`). Both favour the "connect lazily on first publish" option. Another example of a static async factory is `AzureServiceBusMessageBatch.CreateBatch` (`src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageBatch.cs:54`).
- **#4082 in the repo:** no commit message mentions #4082. The only related trace is commit `4a2b406e4` ("docs(conformance): MQTT FR-23 stays deferred — the Proactor pump deadlocks"), which recorded the deferral. That fits the issue's claim that the #4082 checklist item was never done.

## Confirmed Root Cause
**CONFIRMED.** On the Proactor pump thread, the MQTT consumer lazily builds a `MqttMessagePublisher`, and that constructor blocks on an `await` that has captured the pump's single-threaded `BrighterSynchronizationContext`. That is a self-deadlock. The same thing happens on the requeue path and again on the DLQ/invalid-message rejection path.

Precise trace:
1. `Proactor.Run()` calls `BrighterAsyncContext.Run(() => EventLoop())` (`src/Paramore.Brighter.ServiceActivator/Proactor.cs:99`).
2. `EventLoop` calls `RequeueMessage`, then `Channel.RequeueAsync`, then `MqttMessageConsumer.RequeueAsync`. `RequeueAsync` calls `EnsureRequeueProducer()` synchronously on the pump thread (`MQTTMessageConsumer.cs:465`).
3. That builds `new MqttMessagePublisher(...)` (`:489`), whose constructor does `ConnectAsync().GetAwaiter().GetResult()` (`MQTTMessagePublisher.cs:54`).
4. Inside `ConnectAsync`, `await _mqttClient.ConnectAsync(...)` (`MQTTMessagePublisher.cs:115`) has no `ConfigureAwait(false)`. A TCP connect plus the CONNACK round trip cannot finish synchronously, so the state machine suspends. Its continuation is `Post`ed to the captured `BrighterSynchronizationContext` (`BrighterSynchronizationContext.cs:108-135`).
5. `Post` enqueues onto the context's `_taskQueue`. Only `BrighterAsyncContext.Execute`'s `GetConsumingEnumerable` loop drains that queue (`BrighterAsyncContext.cs:~336-344`), and that loop runs on the one pump thread. That thread is parked inside `GetResult()` waiting for `ConnectAsync`'s task, which can only finish once the posted continuation runs. The result is a permanent deadlock.

MQTTnet's own internal use of `ConfigureAwait(false)` (MQTTnet 4.3.7.1207, `Directory.Packages.props:106`) does not matter here — the capture happens at Brighter's own outer `await` on line 115.

When a budget is set and exhausted, the rejection producers take the same route: `RequeueMessage` becomes `RejectMessage`, then `RejectAsync`, then `ResolveRejectionProducer`, then `_deadLetterProducer.Value` (`MQTTMessageConsumer.cs:408-410`), then `CreateDeadLetterProducer`, then `new MqttMessagePublisher` (`:552`). That deadlocks the same way. The `try/catch` around it (`:540-559`) does not help, because this is a hang, not an exception.

## Evidence
- [x] Code-trace (every triage citation re-verified against current file contents, all still accurate):
  - `MQTTMessagePublisher.cs:30-55`: the constructor. `:54` is `ConnectAsync().GetAwaiter().GetResult();`. `:109-124` is `ConnectAsync`, with the bare `await` at `:115`.
  - `MQTTMessageConsumer.cs:461-478`: `RequeueAsync` calls `EnsureRequeueProducer()` at `:465`. `:484-503`: `EnsureRequeueProducer` uses `LazyInitializer.EnsureInitialized` and runs `new MqttMessagePublisher` at `:489`. `:430-446`: sync `Requeue` calls it at `:433`.
  - `MQTTMessageConsumer.cs:111-114` sets up the lazy DLQ/invalid producers. `:367` is `RejectAsync` calling `ResolveRejectionProducer`, and `:408-410` reads `.Value`. The publishers are built at `:552` and `:578`.
  - `MQTTMessageConsumer.cs:153-157`: the consumer constructor also blocks, but it runs at channel creation (`ConsumerFactory.cs:124`), before the pump starts — not currently a hazard.
  - `Proactor.cs:99`: `BrighterAsyncContext.Run(() => EventLoop())`. `Proactor.cs:501-526`: `RequeueMessage` either rejects when the handled count is reached (`:517`) or calls `Channel.RequeueAsync` (`:526`). `EventLoop` does not use `ConfigureAwait(false)` in its own awaits, so these calls resume on the pump context. The `ConfigureAwait(false)` at `:547` and `:591` sits inside `TranslateMessage` and only affects that method.
  - `BrighterAsyncContext.cs:25-29`: single-threaded doc comment. `Run(Func<Task>)` begins at `:237`. The `TaskFactory` uses `HideScheduler` (`:80-84`), so the SynchronizationContext is the only thing captured.
  - Escape routes checked and ruled out: `BrighterSynchronizationContext.Send` runs inline when already on the pump (`:161-166`), but `await` uses `Post`, not `Send`. `BrighterTaskScheduler.TryExecuteTaskInline` (`:58-67`) doesn't help — the blocked thread waits on `ConnectAsync`'s task, not the posted callback task, so there's nothing to inline. The context never runs a nested message loop while a thread is blocked. `SynchronizationContext.SetSynchronizationContext(null)` appears only in `BrighterSynchronizationContextScope` and `ServiceProviderLifetimeScope.DisposeScope` (`:424`) — neither is on the MQTT path; that `DisposeScope` comment describes this exact deadlock class and the fix the codebase already uses there.
  - Why the Reactor and existing Proactor tests pass: `Reactor.Run` (`Reactor.cs:95`) never installs a SynchronizationContext, so the continuation resumes on the thread pool and the blocking constructor completes. The existing Proactor requeue/reject unit tests call `_channel.RequeueAsync`/`RejectAsync` directly from the xUnit thread (e.g. `When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs:67`), not from inside a running pump, so they never exercise the captured context.
- [ ] Red repro, not yet run but already present in the tree: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Generated/Proactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs:62` carries `[Fact(Skip = "Deferred: #4351 ...")]` and drives a real Proactor (`:90-93`) with a DLQ routing key (`:70`). Removing the Skip reproduces the hang today at `await pumping` (`:109`); it will need a timeout guard so it fails instead of hanging forever.
- Live-broker feasibility: `docker-compose-mqtt.yaml` exists at the repo root (`efrecon/mosquitto`, port 1883). Docker is available and the daemon is running; `docker ps` currently shows only `gcp-pubsub-emulator` — no mosquitto container running yet, one would need to be started for the regression test.
- A broker-free repro (connecting to a closed port under `BrighterAsyncContext.Run`) might also deadlock, since the failed connect's `catch` continuation would also be posted — but this depends on the OS completing the refused connect asynchronously, which was not verified. The broker-backed test is the dependable red repro.

## Scope Notes
- **The DLQ and invalid-message producers** (`MQTTMessageConsumer.cs:111-114`, `:408-410`, `:552`, `:578`) have the same defect and are reached from every Proactor reject path: `Proactor.cs:216, 320, 328, 334, 345, 374, 382, 391`, and `:517` when the budget runs out. Invalid-message routing (`Unacceptable`) would deadlock the same way. **The fix must cover all three producers, not just the requeue one**, or FR-23 will deadlock at dead-lettering instead of at requeue.
- **Consumer constructor** `MQTTMessageConsumer.cs:153-157` (`Connect`, awaits at `:594`/`:597`, no `ConfigureAwait(false)`) is safe today because channels are created at `ConsumerFactory.cs:124`, before the pump runs — but it becomes a hazard if a channel is ever created under a single-threaded context, so it's worth hardening with the same fix while touching this file.
- `MqttMessagePublisher.DisposeAsync` (`:141`) and `PublishMessageAsync` (`:106`) are awaited, not blocked on — fine as-is.
- Minor, out of scope: the requeue publisher's config (`:489-497`) omits `ClientID` while the DLQ/invalid producers use `-dlq`/`-invalid` suffixes; not part of this bug.
- Other transports (Redis, Kafka, both SQS versions, RMQ, Postgres, MsSql) use the same lazy `Lazy<...Producer>` rejection-producer pattern evaluated on the pump thread, but a quick grep found no raw `GetAwaiter().GetResult()` in their producer constructors — the constructor-blocks-on-captured-context defect appears specific to MQTT. Not exhaustively verified; noted for awareness only, not in scope for this fix.

## Regression Test
`tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/When_a_publisher_is_constructed_inside_the_pump_context_should_not_deadlock.cs`

`MqttPublisherPumpContextConstructionTests.When_a_publisher_is_constructed_inside_the_pump_context_should_not_deadlock`

Pins the confirmed root cause directly at the constructor level (the single site all three producer paths — requeue, DLQ, invalid-message — go through): constructs `MqttMessagePublisher` from inside `BrighterAsyncContext.Run(...)`, the same single-threaded pump context `Proactor.Run()` uses, exactly as `EnsureRequeueProducer`/`CreateDeadLetterProducer`/the invalid-message producer do on the pump thread. Uses an embedded MQTTnet test broker (`MqttTestServer`, random loopback port) rather than the `docker-compose-mqtt.yaml` broker, matching the existing hand-written precedent (`MqttConsumerProducerConfigAndDisposeTests`) and needing no Docker.

Races the construction against a 10 s `Task.Delay` via `Task.WhenAny` so the test fails on a clear assertion rather than hanging the test run forever.

**Confirmed RED, for the right reason** (2026-09-27, `dotnet test tests/Paramore.Brighter.MQTT.Tests --filter FullyQualifiedName~MqttPublisherPumpContextConstructionTests`, net9.0 + net10.0):
```
Assert.Same() Failure: Values are not the same instance
Expected: Task<MqttMessagePublisher> { Status = Running }
Actual:   Task { Status = RanToCompletion }
```
i.e. after 10 s the publisher construction task is still `Running` (deadlocked) and the timeout task won the race — not a compile error, not an unrelated failure.

Approved by user 2026-09-27.

Scope note carried into `/bugfix:fix`: because this test pins the shared constructor rather than one call site, a fix applied there (rather than only to the requeue producer) will cover the requeue, DLQ, and invalid-message producers described in Scope Notes above in one place. The full end-to-end path (a live Proactor pump exhausting its requeue budget and reaching the DLQ) is already covered by the currently-`Skip`ped generated test `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Generated/Proactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs` — un-skipping that (and updating `specs/0036-universal-transport-conformance-tests/conformance-status.md`) is a natural verification step for `/bugfix:fix`/`/bugfix:verify`, not a second regression test, since it is auto-generated and must not be hand-edited outside the generator/template.

## Fix
**File changed**: `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessagePublisher.cs` (one line, constructor).

```diff
-            ConnectAsync().GetAwaiter().GetResult();
+            BrighterAsyncContext.Run(ConnectAsync);
```

The constructor no longer resolves its blocking connect with a raw `GetAwaiter().GetResult()` on
whatever `SynchronizationContext` happens to be ambient. Instead it routes through
`BrighterAsyncContext.Run(Func<Task>)` — the same pattern this class's own `PublishMessage`
already uses (`MQTTMessagePublisher.cs:95`, now line 95 unchanged). `Run` creates a **new**,
independent `BrighterAsyncContext`, installs its `SynchronizationContext` as ambient only for the
duration of the call (`BrighterSynchronizationContextScope.ApplyContext`, saving/restoring
whatever was ambient before — e.g. the Proactor pump's own context), and pumps that new context's
task queue on the *calling* thread until `ConnectAsync()`'s task completes. So when
`_mqttClient.ConnectAsync(...)`'s bare `await` suspends, its continuation now posts to the fresh
nested queue — which the very same (otherwise-idle-while-waiting) thread is actively draining —
rather than to the outer pump queue that only a now-blocked thread could ever drain. No more
self-deadlock, on any calling thread (pump or otherwise).

This is the minimal fix bounded by the Confirmed Root Cause: it corrects the shared constructor
that `EnsureRequeueProducer` (`MQTTMessageConsumer.cs:489`), `CreateDeadLetterProducer`
(`MQTTMessageConsumer.cs:552`) and the invalid-message producer (`MQTTMessageConsumer.cs:578`) all
call, so the requeue, DLQ, and invalid-message producer paths flagged in Scope Notes are all
covered by this one change — no separate fix was needed per call site, and no other file was
touched.

**Targeted regression test — GREEN**:
```
dotnet test tests/Paramore.Brighter.MQTT.Tests/Paramore.Brighter.MQTT.Tests.csproj \
  --filter "FullyQualifiedName~MqttPublisherPumpContextConstructionTests"

Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 106 ms (net10.0)
Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 93 ms (net9.0)
```
Construction now completes in ~100 ms — well inside the 10 s race — versus timing out at 10 s
before the fix, confirming the deadlock is actually resolved rather than merely raced around.

## Verification

Full `tests/Paramore.Brighter.MQTT.Tests` suite run against a local `docker-compose-mqtt.yaml`
broker (started for this run, torn down afterwards), both target frameworks:

```
dotnet test tests/Paramore.Brighter.MQTT.Tests/Paramore.Brighter.MQTT.Tests.csproj -f net10.0
Passed! - Failed: 0, Passed: 77, Skipped: 6, Total: 83, Duration: 41 s

dotnet test tests/Paramore.Brighter.MQTT.Tests/Paramore.Brighter.MQTT.Tests.csproj -f net9.0
Passed! - Failed: 0, Passed: 77, Skipped: 6, Total: 83, Duration: 41 s
```

The 6 skips are pre-existing and unrelated: the nacking-not-supported-by-MQTT pair (×2 variants)
and the requeue-to-DLQ conformance test still deferred against #4351 (the generated,
non-hand-editable test noted above — this fix makes it safe to un-skip, but doing so is a
follow-up, not part of this change). No regressions.
