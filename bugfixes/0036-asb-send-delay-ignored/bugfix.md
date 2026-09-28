# Bugfix: Azure Service Bus SendWithDelay appears to deliver immediately (the before-delay arm sees the message)

**Linked Issue**: #4318

**Status**: Verified

## Symptom

**Observed:** Job `101853410391` of `azure-ci` ran against a real Service Bus namespace (probe PR #4317). Both the Proactor and Reactor versions of `When_sending_a_delayed_message_should_deliver_after_delay` failed on the before-delay assertion:

```
Assert.Equal() Failure: Values differ
Expected: MT_NONE
Actual:   MT_EVENT
```

**Expected:** The test calls `SendWithDelayAsync(message, TimeSpan.FromSeconds(5))` and then does one receive with a 2 s timeout. That receive should return nothing. The message should only show up in the later poll loop, which runs every 500 ms for up to 30 s.

**Reproduction:**
1. Set `BrighterTestsASBConnectionString` to a real namespace. CI uses SAS; see `.github/workflows/ci.yml:659`.
2. Remove the `Skip` from `[Fact]` in either generated test. Both are currently skipped as `Deferred: #4240`.
3. Run the `AzureServiceBus` category. The before-delay arm fails.

**Contrasting data point (issue comment):** On the Service Bus **emulator**, a **queue** subscription requeued with a 5 s delay was redelivered 5.0–5.1 s after the `DeferMessageAction`, three cycles in a row. So `ScheduleMessageAsync` does honour the delay in that environment.

**Related observation:** The generated sibling test `When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay` is **not** skipped. It goes through the same `SendWithDelayAsync` → `ScheduleMessageAsync` path and uses the same 2 s before-delay receive, and the ledger reports it as `Pass` from the same probe run. So one delayed-schedule test passes against the real namespace and the other fails. The main difference between them is whether the channel's receiver is already warm when the before-delay receive runs.

## Suspected Location

**Production send path (looks correct on inspection)**
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageProducer.cs:199`: the sync `SendWithDelay` is sync-over-async onto `SendWithDelayAsync`.
- `AzureServiceBusMessageProducer.cs:207-244`: `SendWithDelayAsync`.
  - `:215`: `GetSenderAsync` (admin topic check, then `_serviceBusSenderProvider.Get`) runs before the schedule time is computed.
  - `:223-226`: the zero-delay branch calls `SendAsync`.
  - `:229`: `var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.Add(delay.Value));`. The enqueue time comes from the **client** clock, and it is taken **before** the sender's AMQP link is opened.
  - `:230`: `serviceBusSenderWrapper.ScheduleMessageAsync(...)`.
  - `:240-243`: `finally { await serviceBusSenderWrapper.CloseAsync(); }`. The sender is closed after every send, so each send opens a fresh AMQP sender link.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusWrappers/ServiceBusSenderWrapper.cs:36-38`: `ScheduleMessageAsync` passes straight through to the SDK's `ServiceBusSender.ScheduleMessageAsync`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicMessageProducer.cs:63-98`: `EnsureChannelExistsAsync` (topic existence check).

**Receive path (where the before-delay window actually gets measured)**
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs:172-225`: `ReceiveAsync(timeOut)`.
  - `:177`: `await EnsureChannelAsync()` runs first and is **not bounded by `timeOut`**.
  - `:183-186`: on the first call (`ServiceBusReceiver == null`) it calls `GetMessageReceiverProviderAsync()`, also unbounded.
  - `:195`: `ServiceBusReceiver.ReceiveAsync(_batchSize, timeOut.Value)` is the only step that respects the 2 s timeout.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicConsumer.cs:79-122`: `EnsureChannelAsync`. On first use it makes a management-plane HTTP call, `SubscriptionExistsAsync` at `:86`, and only caches the result afterwards (`_subscriptionCreated` at `:88`).
- `AzureServiceBusTopicConsumer.cs:124-136`: `GetMessageReceiverProviderAsync`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusWrappers/ServiceBusReceiverProvider.cs:98-99`: `CreateReceiver(topic, subscription, PeekLock)` is lazy, so the AMQP link, CBS/SAS auth and connection open all happen inside the first `ReceiveMessagesAsync`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusWrappers/ServiceBusReceiverWrapper.cs:57-59`: `ReceiveMessagesAsync(batchSize, serverWaitTime)`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumerFactory.cs:84-95`: the topic branch (`UseServiceBusQueue == false`). This is the one the generated tests use, not the queue branch that the emulator comment exercised.

**Test harness and tests**
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/Generated/Proactor/When_sending_a_delayed_message_should_deliver_after_delay.cs`:
  - `:46`: skipped `[Fact]`.
  - `:55-56`: producer and channel are created.
  - `:61`: `SendWithDelayAsync(message, 5s)`.
  - `:67-68`: the before-delay arm, which is the **first ever** receive on this channel.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/Generated/Reactor/When_sending_a_delayed_message_should_deliver_after_delay.cs`:
  - `:40`: skipped `[Fact]`.
  - `:55`: `SendWithDelay`.
  - `:61`: before-delay `Receive(2000ms)`.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/Generated/Proactor/When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay.cs`: `:46` is an active `[Fact]`; `:64` is a warm-up receive; `:67` is `RequeueAsync(received, 5s)`; `:74` is the before-delay receive on a **warm** channel. This is the contrasting case.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/AzureServiceBusMessageGatewayProvider.cs`:
  - `:86-101`: `CreateSubscription` is a topic subscription with the default config and no filter or rule.
  - `:183-193`: `EnsureSubscriptionExistsAsync` pre-creates the subscription (#4309 fix). It uses a separate admin client, so it does **not** set the consumer's `_subscriptionCreated` cache.
  - `:315-339`: `CreateProducerAsync` and `CreateChannelAsync`. Each one reads `ASBCreds.ASBClientProvider`, which builds a new provider and so a new `ServiceBusClient`.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/ASBCreds.cs:8-21`: returns a **new** client provider on every property access. Producer, consumer and admin therefore each open their own AMQP/HTTP connections.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/Fakes/FakeServiceBusSenderWrapper.cs:29-34`: `ScheduleMessageAsync` now records the message in `ScheduledMessages`, but it still **discards `scheduleEnqueueTime`** and forwards to `Send`. The issue cites `:27`, but the file has moved on since then. The gap the issue describes is still there: no unit test can check the requested enqueue instant.

## Root-Cause Hypothesis

**Primary hypothesis: a test-timing artefact, not ignored scheduling.**
- The gateway probably does schedule the message correctly.
- The before-delay window in the generated test is not 2 s. It is 2 s plus the unbounded cold-start cost of the channel's **first** receive:
  - the management-plane `SubscriptionExistsAsync` HTTP call (`AzureServiceBusTopicConsumer.cs:86`);
  - opening a fresh AMQP connection and receiver link with SAS/CBS auth, inside the first `ReceiveMessagesAsync` (`ServiceBusReceiverWrapper.cs:59`), on a brand-new `ServiceBusClient` (`ASBCreds.cs:8-21`).
- The producer side also eats into the delay:
  - `DateTime.UtcNow` is read at `AzureServiceBusMessageProducer.cs:229`, **before** the per-send AMQP sender link is opened;
  - the sender is closed after every send (`:242`), so each send opens a new link;
  - the test's clock only starts after `SendWithDelayAsync` returns.
- Against a remote namespace from a GitHub `ubuntu-latest` runner, these costs can plausibly add up to more than about 3 s. The scheduled enqueue time then passes while the "2 s" receive is still in progress, and the message is legitimately delivered in that window.
- This fits all the evidence:
  - (a) The emulator is local and low-latency, and the delay looks exact there.
  - (b) The requeue-with-delay test passes against the same namespace and schedule path, because its before-delay receive (`...requeuing..._with_delay...cs:74`) runs on a channel already warmed by the receive at `:64`.
  - (c) Only the delayed-send test does its first-ever receive inside the timing-sensitive window.

**Falsifiable by:**
- Measuring wall-clock time around the before-delay `ReceiveAsync` in CI. The hypothesis predicts it is well over 2 s, roughly over 3 s whenever the test fails.
- Comparing the received `ServiceBusReceivedMessage.ScheduledEnqueueTime` / `EnqueuedTime` with the time the before-delay receive returned. The hypothesis predicts `EnqueuedTime ≈ ScheduledEnqueueTime ≈ send + 5 s`, not `≈ send`.
- If `EnqueuedTime` is about equal to the send time, or `ScheduledEnqueueTime` is unset or in the past, this hypothesis is **refuted** and the gateway is not scheduling.
- A cheaper check: warm the channel (one receive before `SendWithDelayAsync`) and re-run. The before-delay arm should then pass.

**Alternative hypotheses from the issue and comment. All UNVERIFIED — to be proven or refuted in /bugfix:confirm:**
1. **Clock skew between the CI runner and the namespace** (the comment's "obvious" candidate). The schedule instant comes from the client's `DateTime.UtcNow` at `AzureServiceBusMessageProducer.cs:229`, and Service Bus interprets it against its own clock. The runner would have to be behind the namespace by about 5 s or more for `now + 5 s` to already be in the past. That is unlikely on NTP-synced hosted runners, and it would probably have failed the requeue-with-delay test too, since that test uses the same `:229` path. Test it by logging `ScheduledEnqueueTime` against `EnqueuedTime` and the runner's `DateTime.UtcNow`. **UNVERIFIED.**
2. **Topic subscription vs queue semantics for scheduled messages** (the comment's other lead). The emulator data point used a queue (`UseServiceBusQueue`, `AzureServiceBusConsumerFactory.cs:71-82`). The failing test uses a topic subscription (`:84-95`), provisioned with the default `AzureServiceBusSubscriptionConfiguration` and no custom rule or filter (`AzureServiceBusMessageGatewayProvider.cs:188-192`). Scheduled messages on a topic are documented to be held on the topic and fanned out at enqueue time, and the requeue-with-delay test also uses a topic subscription and passes. This makes a topic-specific cause unlikely, but it has not been measured on a real namespace. **UNVERIFIED.**
3. **Sender `CloseAsync` in the `finally` block interfering with scheduled delivery** (`AzureServiceBusMessageProducer.cs:240-243`). Closing a sender after `ScheduleMessageAsync` has completed should not cancel or advance the schedule, because the schedule is committed broker-side once the call returns. The requeue path goes through the same `finally` and passes. **UNVERIFIED.**

**Separate gap (independent of root cause):** `FakeServiceBusSenderWrapper.ScheduleMessageAsync` (`FakeServiceBusSenderWrapper.cs:29-34`) drops `scheduleEnqueueTime`. The hand-written delayed-send unit tests therefore cannot tell a correct schedule from an immediate send, and the fake should record the requested enqueue time. This does not by itself explain the CI failure.

## Confirmed Root Cause

**Verdict: CONFIRMED (by code-trace; a live-namespace red repro is out of scope for this session).**

The delayed-send test is not broken because scheduling is ignored — it is broken because its
"2 s before-delay" window is measured from the wrong point and is not actually bounded to 2 s.

`SendWithDelayAsync` reads the delay clock early: at
`src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageProducer.cs:229` it
computes `t0 + 5s` where `t0 = DateTime.UtcNow`. Everything that runs after that line — on both the
producer and consumer sides — eats into the 5 s budget before the test's "2 s" receive even starts
waiting:

1. **Producer AMQP cold start after t0.** The test's `ServiceBusClient` is brand new
   (`tests/.../MessagingGateway/ASBCreds.cs:8-21` builds a fresh provider on every access;
   `ServiceBusSenderProvider.Get` lazily calls `_client.CreateSender`). So the TCP/TLS/AMQP
   connection, CBS/SAS auth, and sender link attach all happen *inside*
   `ScheduleMessageAsync` (`AzureServiceBusWrappers/ServiceBusSenderWrapper.cs:36-38`), which runs
   after `t0`. The sender is then closed (`AzureServiceBusMessageProducer.cs:240-243`).
2. **Consumer management-plane call.** The first receive calls `EnsureChannelAsync`
   (`AzureServiceBusConsumer.cs:177`), which — because `_subscriptionCreated` is still false —
   makes an unbounded HTTPS `SubscriptionExistsAsync` call (`AzureServiceBusTopicConsumer.cs:81,86`)
   on yet another fresh admin client.
3. **Consumer AMQP cold start.** `GetMessageReceiverProviderAsync` only calls `_client.CreateReceiver`
   (`AzureServiceBusWrappers/ServiceBusReceiverProvider.cs:98-99`) — verified: this makes no network
   call itself, so connection, CBS auth and receiver-link attach all happen lazily inside
   `ReceiveMessagesAsync(batch, 2s)` (`ServiceBusReceiverWrapper.cs:59`). Link creation is bounded by
   the SDK's retry `TryTimeout` (default 60 s), not by the 2 s `maxWaitTime`.

The receive window therefore actually ends at roughly
`t0 + [producer connect+auth+attach+schedule+close] + [HTTPS SubscriptionExists] + [consumer connect+auth+attach] + 2s`.
If the bracketed one-time costs exceed ~3 s on a GitHub-hosted runner against a real namespace, the
broker correctly enqueues the message at `t0 + 5s` while the "2 s" receive is still blocked in
cold-start — and the message that then arrives is delivered *correctly*, not early. Scheduling is
not broken; the test's timing assumption is.

This is confirmed by the asymmetry with the passing sibling test: verified —
`Generated/Proactor/When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay.cs`
does a warm-up receive (`await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000))`) *before*
`RequeueAsync(received, 5s)`, so its channel's connection, subscription check, and receiver link
are already open by the time its own before-delay receive runs. That test goes through the exact
same `ScheduleMessageAsync`/`CloseAsync` code path and passes — the only structural difference is
that its before-delay receive runs on a warm channel, while the delayed-send test's before-delay
receive is the channel's first-ever receive.

**Alternative hypotheses — ruled out:**
- **Clock skew**: ruled out. The requeue test computes its schedule instant from the same runner
  clock, on the same code line (`:229`), in the same run, and does *more* work after scheduling
  (`AcknowledgeAsync`) — skew severe enough to break the delayed-send test would have broken this
  one too.
- **Topic vs. queue semantics**: ruled out. The requeue test also schedules to a topic subscription
  (same `AzureServiceBusConsumerFactory.cs:84-95` topic branch) and passes.
- **`CloseAsync` interference**: ruled out. The requeue path runs the identical
  `finally { CloseAsync }` (`:240-243`) and passes; closing a sender does not cancel a scheduled
  message (only `CancelScheduledMessageAsync` does).

**Not yet directly measured** (would require a live-namespace run, out of scope for this session):
actual wall-clock time of the cold-start overhead in CI, and the received message's
`EnqueuedTime`/`ScheduledEnqueueTime` values. These would be the falsifying/confirming
measurements if a live run is available.

## Evidence
- [x] Code-trace: see the numbered trace above, all `file:line` references spot-checked against
  the working tree (`ASBCreds.cs`, `ServiceBusReceiverProvider.cs:98-99`, and the requeue test's
  warm-up receive at lines ~40 area were re-read and confirmed to match).
- [ ] Red repro: not obtainable without a live Azure Service Bus namespace (or credentials to the
  CI namespace); the bug is infra-bound. Falsifying/confirming measurements, if a live run becomes
  available: (a) stopwatch the before-delay receive — hypothesis predicts >2s, likely >3s on
  failure; (b) compare `EnqueuedTime` vs `ScheduledEnqueueTime` — hypothesis predicts they are
  both ≈ send + 5s, not ≈ send; (c) inserting a warm-up receive before `SendWithDelayAsync` should
  turn the before-delay arm green.

## Scope Notes

- **Fix belongs on the test/harness side, not production.** `ScheduleMessageAsync` receives the
  correct future instant and the broker honours it (confirmed by the emulator data point and by
  the passing sibling test on the identical schedule path). No production code in
  `AzureServiceBusMessageProducer` needs to change for correctness.
- **Both Proactor and Reactor generated tests need a warm-up receive** before `SendWithDelayAsync`,
  matching what the requeue-with-delay test already does incidentally. Because these are
  `// <auto-generated>` files (by `Paramore.Brighter.Test.Generator`), the fix belongs in the
  generator template or the shared provider harness (e.g. `AzureServiceBusMessageGatewayProvider.CreateChannelAsync`),
  not hand-edited into the generated `.cs` files directly.
- **`FakeServiceBusSenderWrapper` gap is still open**, contrary to what triage assumed.
  `tests/Paramore.Brighter.AzureServiceBus.Tests/Fakes/FakeServiceBusSenderWrapper.cs:29-34` records
  the message into `ScheduledMessages` but still discards `scheduleEnqueueTime` and forwards to
  `Send`. No unit test can currently assert the requested enqueue instant. This is independent of
  the CI root cause but was called out by the issue as something that "should be closed" regardless.
- **Other transports may share this sensitivity.** Any other generated
  `When_sending_a_delayed_message_should_deliver_after_delay` test whose consumer lazily creates
  its subscription/connection on first receive could have the same cold-start-vs-window problem.
  Worth a quick check once this transport's fix lands, but out of scope for this bug.
- **The `Skip = "Deferred: #4240"` markers** on both delayed-send tests should only be removed once
  the warm-up is in place — removing them first would just reintroduce the flaky failure.
- **Not a bug, but noted**: `ASBCreds` opens a new `ServiceBusClient`/`ServiceBusAdministrationClient`
  on every access, and every `SendAsync`/`SendWithDelayAsync` call attaches and detaches a fresh
  sender link. Neither breaks correctness; both inflate cold-start cost across the ASB conformance
  suite generally. Not part of this fix's scope.

## Regression Test

**No new automated test written.** The confirmed defect is a missing warm-up receive in the
generated ASB delayed-send test (`Generated/Proactor/When_sending_a_delayed_message_should_deliver_after_delay.cs`
and its Reactor equivalent) — not a production-code defect. Observing a genuine RED failure for
this cause requires a live Azure Service Bus namespace (the timing-window symptom does not exist
in-process; the fake gateway has no cold-start cost to reproduce). `BrighterTestsASBConnectionString`
/ `BrighterTestsASBNameSpace` are not configured in this environment, and no ASB credentials were
supplied, so a live RED/GREEN cycle could not be run here — consistent with `/bugfix:confirm`
treating this as an infra-bound, code-trace-only case.

The user chose to skip the live RED step and proceed directly to the fix, with this documented
as the rationale, rather than write a narrower unit test that would not reproduce the actual
symptom.

**Regression coverage plan:** the existing generated tests
(`Generated/Proactor/` and `Generated/Reactor/When_sending_a_delayed_message_should_deliver_after_delay.cs`,
currently `Skip = "Deferred: #4240"`) serve as the regression test once:
1. The warm-up-receive fix lands in the generator template / provider harness.
2. The `Skip` attribute is removed.
3. A live-namespace CI run (`azure-ci`) confirms the before-delay arm now passes green.

No `.confirm-approved`-style automated gate exists for step 3 above — verifying the fix will
require watching the next `azure-ci` run against a real namespace, since this environment cannot
run it locally.

## Fix

**Files changed:**
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/AzureServiceBusMessageGatewayProvider.cs`
  — `CreateChannel` (sync) and `CreateChannelAsync` (async) each now do one bounded warm-up
  receive (`TimeSpan.FromMilliseconds(1000)`) on the newly-created channel before returning it.

**Summary:** Every ASB conformance test's channel is now warmed (management-plane subscription
check done, AMQP connection/receiver link opened) at `CreateChannel`/`CreateChannelAsync` time,
before any test-specific timing assertion runs — matching what the passing
`When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay` test already did
incidentally via its own warm-up receive. This removes the unbounded cold-start cost from the
delayed-send test's "2 s before-delay" window, which was the confirmed root cause. No production
code changed — `AzureServiceBusMessageProducer.SendWithDelayAsync`'s scheduling was already
correct.

**Scoping notes:**
- The fix was placed in the ASB-specific provider harness, not the shared `.liquid` generator
  template — the template is used to generate this canonical test for every transport, and the
  cold-start cause is ASB-specific (management-plane HTTP call + fresh AMQP client per test), so
  changing the shared template would have altered behaviour for every other transport's copy of
  this test, which is outside this bug's confirmed scope.
- **Not fixed here, deliberately:** the `FakeServiceBusSenderWrapper.ScheduleMessageAsync` gap
  (discarding `scheduleEnqueueTime`) noted in Scope Notes. No red test was written for it in
  `/bugfix:test` (the user chose to skip the live-RED step entirely for this bug), so per the
  bugfix workflow's own rule — only scope items with their own red test get fixed at this step —
  it is left open as a separate, independent follow-up.
- The `Skip = "Deferred: #4240"` markers on both delayed-send tests were left in place. Per the
  Regression Test section's plan, they should only be removed once a live-namespace `azure-ci`
  run confirms the before-delay arm now passes green with this warm-up in place.

**Verification run:** `dotnet build` on the test project succeeded (0 errors, pre-existing
warnings only). `dotnet test --filter "Category!=AzureServiceBus"` was run before and after the
change: 326 passed / 10 failed in both runs (identical failures — pre-existing tests that require
`BrighterTestsASBConnectionString`/`BrighterTestsASBNameSpace`, unrelated to this change and unset
in this environment). No regression. The delayed-send test itself remains `Skip`-gated and
requires a live namespace to observe green, which is not available in this session.

**Full-suite verify run (`/bugfix:verify`):** `dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj`
(no filter) → 332 passed, 38 failed, 6 skipped, 376 total. All 38 failures share the single error
`System.Exception : ASB ConnectionString or Namespace not set not set` (confirmed by grepping every
`Error Message:` line for distinct values) — i.e. every failure is a pre-existing test that needs a
live ASB namespace/connection string, unrelated to this change. The 6 skipped are the
`Deferred: #4240` delayed-send/related tests. No regressions from the warm-up-receive fix; this
project has no local, credential-free path to exercise the actual live-namespace symptom, so full
confirmation that the before-delay arm now passes still requires watching the next `azure-ci` run.
