# Bugfix: RMQ consumers declare transient non-exclusive queues, which RabbitMQ 4.3+ rejects by default

**Linked Issue**: #4355
**Status**: Verified

## Symptom
**Observed:** On RabbitMQ 4.3.x (the reporter used stock `rabbitmq:4-management`, 4.3.5), a Brighter RMQ consumer using the default subscription settings fails the first time it calls `Receive`. The error is `ChannelFailureException` wrapping `OperationInterruptedException`: `code=541, INTERNAL_ERROR - Feature 'transient_nonexcl_queues' is deprecated. By default, this feature is not permitted anymore.` (`classId=50, methodId=10`, which is `queue.declare`). According to the issue, every `RMQ.Sync` gateway test fails this way against 4.3.5.

**Expected:** A consumer with default subscription settings can declare its queue and consume on RabbitMQ 4.3+, as it already does on 4.2 and 3.13.

**Reproduction (from the issue, not re-run here):**
1. Start a RabbitMQ 4.3.x broker, e.g. `rabbitmq:4-management`, on `localhost:5672`.
2. Create an `RmqSubscription` without passing `isDurable`, so it defaults to `false`. Leave `makeChannels` at its default of `OnMissingChannel.Create`.
3. Call `Receive` on the consumer. The call chain is `Receive` → `EnsureChannel` → `CreateQueue` → `QueueDeclare(durable:false, exclusive:false, autoDelete:false)`. The broker rejects the declare.

CI is not affected because it pins RabbitMQ 4.2 and 3.13.

## Suspected Location
Line numbers below were checked against the current tree and match the issue.

**Queue declarations (the failing path):**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs:460`, in `CreateQueue()` (lines 450–463): `Channel.QueueDeclare(_queueName.Value, _isDurable, false, false, SetQueueArguments());`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs:462`: `if (_hasDlq) Channel.QueueDeclare(_deadLetterQueueName!.Value, _isDurable, false, false, new Dictionary<string, object>());`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs:541`, in `CreateQueueAsync()` (lines 534–549): `await Channel.QueueDeclareAsync(_queueName.Value, _isDurable, false, false, SetQueueArguments(), ...)`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs:546`: the DLQ declare, again `_isDurable, false, false`.

**Callers (only reached when `makeChannels == OnMissingChannel.Create`):**
- Sync: `EnsureChannel()` at `RmqMessageConsumer.cs:388`, which calls `CreateQueue()` at `:402`. `Validate` uses the passive declare at `:499`; `Assume` declares nothing.
- Async: `EnsureChannelAsync()` at `RmqMessageConsumer.cs:462`, which calls `CreateQueueAsync()` at `:470`. `Validate` uses the passive declare at `:594`.
- `OnMissingChannel.Create` is the constructor default in both gateways (Sync `RmqMessageConsumer.cs:99` and `:132`; Async `:99` and `:134`).

**Where the durability flag comes from:**
- Sync `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqSubscription.cs`: `bool isDurable = false` at `:106` and `:165` (the second constructor passes it through at `:188`). `IsDurable` is set at `:122`.
- Async `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqSubscription.cs`: `bool isDurable = false` at `:113` and `:176` (passed through at `:201`). `IsDurable` is set at `:130`.
- The factories pass it into the consumer: Sync `RmqMessageConsumerFactory.cs:68`; Async `RmqMessageConsumerFactory.cs:74` and `:96`.
- The consumer stores it: Sync `RmqMessageConsumer.cs:138`; Async `:141`.

**Other call sites checked (whole of `src/` and `tests/`):**
- In `src/`, the only non-passive `QueueDeclare` calls are the four above. There is no shared helper that declares queues. The only RMQ gateway projects are `RMQ.Sync` and `RMQ.Async`.
- Two test helpers use the same pattern and will fail the same way on 4.3+ whenever `isDurable` is false (matters for the test suite, not production code):
  - `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/TestHelpers.cs:57`: `channel.QueueDeclare(_channelName.Value, _isDurable, false, false, null);`
  - `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/TestHelpers.cs:58`: `QueueDeclareAsync(channelName.Value, isDurable, false, false, ...)`

**How far `_isDurable` reaches:**
- It is used only for queue declarations: the main queue and the DLQ.
- It does not affect exchanges. `ExchangeConfigurationHelper` (Sync `:61` and `:71`; Async `:67` and `:78`) uses `connection.Exchange.Durable` and `connection.DeadLetterExchange.Durable`, with `autoDelete: false` hard-coded. The RabbitMQ deprecation is about queues, so exchange declarations are not implicated.
- Async has one extra use: `RmqMessageConsumer.cs:154-158` throws `ConfigurationException` if `QueueType.Quorum` is used with `isDurable == false`. So in Async, quorum subscriptions are already forced to be durable and never take the failing path. The failing path is classic queues with the default `isDurable: false`. The Sync gateway has no equivalent quorum check.

## Root-Cause Hypothesis
**Hypothesis (falsifiable):** When `OnMissingChannel.Create` is in effect, both RMQ gateways declare the subscription queue, and the DLQ if configured, with `durable = _isDurable`, `exclusive = false`, `autoDelete = false`. Because `isDurable` defaults to `false` in both `RmqSubscription` types, the default declaration is `durable:false, exclusive:false, autoDelete:false`. That is exactly RabbitMQ's deprecated `transient_nonexcl_queues` feature, which 4.3+ refuses by default with a connection-level 541 `INTERNAL_ERROR`. This hypothesis is refuted if either of these turns out to be true:
- A declaration with `durable:true` (or with `exclusive` or `autoDelete` set to true) still fails against 4.3.x.
- The default `durable:false` declaration succeeds against a stock 4.3.x broker.

**Reporter's suggested direction (restated): UNVERIFIED — to be proven or refuted in /bugfix:confirm.** The issue says:
- Setting `isDurable: true` on the subscription avoids the failure, and fixes the DLQ declaration too, because the DLQ inherits `_isDurable`.
- Brighter should decide on purpose what to declare when `isDurable: false`. The options that keep working on 4.3+ are:
  - declare a durable queue anyway, or
  - declare an auto-delete and/or exclusive queue where a transient lifetime is what the caller actually wants.
- Either option changes queue lifetime semantics for existing users, so it needs to be settled deliberately, not defaulted silently.
- Allowing the feature through broker config is only a stopgap, because RabbitMQ says it will be removed in a future major version.

## Confirmed Root Cause
In both `RmqSubscription` types, `isDurable` defaults to `false`. The value passes through the
factory into `RmqMessageConsumer._isDurable` unchanged (it is `readonly` in both gateways).
`CreateQueue()` (Sync) and `CreateQueueAsync()` (Async) use it as the `durable` argument for both
the main-queue declare and the DLQ declare, with `exclusive` and `autoDelete` hard-coded to `false`.
RabbitMQ's deprecated `transient_nonexcl_queues` feature covers any queue declared with
`durable=false, exclusive=false` — `autoDelete` plays no part in the feature check. From 4.3 that
feature is denied by default, so `queue.declare` (class 50, method 10) fails with 541
`INTERNAL_ERROR`. Nothing between the subscription and the declare changes durability; the Async
quorum check (`RmqMessageConsumer.cs:154-158`) only rejects a bad combination, it never coerces the
value.

**Correction to the Root-Cause Hypothesis above:** the hypothesis's second refutation bullet
("`autoDelete` set to true" as an escape) is **wrong** — the feature is defined purely by
`durable=false, exclusive=false`, so `autoDelete` does not affect whether 4.3 rejects the declare.
Everything else in the hypothesis holds.

## Evidence
- [x] **Code-trace** (no 4.3.x broker available in this environment — infra-bound, trace accepted
  per the workflow's evidence standard). Every `file:line` below was re-verified against the
  current tree at `09f5d988f` (branch `bugfix/4355-rmq-transient-nonexcl-queues`, based on
  `master`):
  1. **Default value.** Sync `RmqSubscription.cs:106` and `:165` (`bool isDurable = false`), passed
     through at `:188`, stored at `:122`. Async `RmqSubscription.cs:113` and `:176`, passed through
     at `:201`, stored at `:130`.
  2. **Factory → consumer.** Sync `RmqMessageConsumerFactory.cs:68` passes `rmqSubscription.IsDurable`
     as-is. Async `RmqMessageConsumerFactory.cs:74` (`Create`) and `:96` (`CreateAsync`) do the same.
     `MakeChannels` defaults to `OnMissingChannel.Create` and is likewise passed through unchanged.
  3. **Consumer field.** Sync `RmqMessageConsumer.cs:138` sets `_isDurable = isDurable`; Async
     `:141` does the same. The field is `readonly` in both (Sync `:61`, Async `:59`) — nothing
     reassigns it afterwards. The Async constructor (`:155-161`) only throws `ConfigurationException`
     for `QueueType.Quorum` with `!_isDurable`; it never changes the value.
  4. **Gate.** Sync `EnsureChannel()` (`:388`) calls `CreateQueue()` (`:402`) only when
     `_makeChannels == Create` (`:400`). Async `EnsureChannelAsync()` (`:462`) calls
     `CreateQueueAsync()` (`:470`) behind the same check (`:468`). `Validate` uses a passive declare
     (Sync `:499`, Async `:594`); `Assume` declares nothing.
  5. **Declares.** Sync `:460` (main queue) and `:462` (DLQ); Async `:541-542` (main queue) and
     `:546-547` (DLQ). All four hard-code `exclusive:false, autoDelete:false`.
  6. **Queue arguments.** `SetQueueArguments()` (Sync `:507-537`, Async `:602-638`) adds only
     `x-queue-type` (Async, Quorum only), `x-ha-policy`, dead-letter settings, `x-message-ttl`,
     `x-max-length` and `x-overflow` — nothing that affects durability or exclusivity.
  7. **Exhaustive.** A grep of `src/`, `tests/` and `samples/` finds no other non-passive
     `QueueDeclare`/`QueueDeclareAsync` beyond these four, plus the two test helpers already listed
     in Suspected Location.
- [ ] Red repro: not run — no RabbitMQ 4.3.x broker available in this environment.
- **External corroboration** (repo has no vendored docs and the local NuGet cache has nothing on
  `transient_nonexcl_queues`, so this is web evidence, not independently re-derived from a live
  broker): the RabbitMQ .NET client's own 4.3 compatibility issue
  ([rabbitmq-dotnet-client#1931](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1931)),
  the same failure in other client libraries
  ([php-amqplib#1241](https://github.com/php-amqplib/php-amqplib/issues/1241),
  [wolverine#1871](https://github.com/JasperFx/wolverine/issues/1871)), and RabbitMQ server
  discussions of the deprecation
  ([#12972](https://github.com/rabbitmq/rabbitmq-server/discussions/12972),
  [#16324](https://github.com/rabbitmq/rabbitmq-server/discussions/16324)). Together: a declare with
  `durable=false, exclusive=false` is denied by default from 4.3, the
  `deprecated_features.permit.transient_nonexcl_queues=true` broker setting is only a temporary
  opt-in, and the recommended replacements are a durable queue (optionally with `x-expires`) or an
  exclusive queue.

## Suggested-Fix Assessment
**PARTIAL.** The direction — declare something other than `durable:false, exclusive:false` — is
right, but the issue's three listed options need correcting:
1. **Auto-delete does not fix it.** `durable:false, exclusive:false, autoDelete:true` is still a
   transient non-exclusive queue; 4.3 still refuses it.
2. **Exclusive is not a sensible default.** An exclusive queue belongs to the one connection that
   declared it and is deleted when that connection closes. Brighter's consumer rebuilds its
   connection/channel on failure (`ResetConnectionToBroker`/`EnsureBroker`), so an exclusive default
   would lose the queue (and any messages on it) on every reconnect, and a second consumer instance
   could not declare it at all.
3. **Durable (optionally plus `x-expires` for an opt-in expiry) is the only option that works
   across Brighter's existing topologies.**

**What changes on the wire if the default flips to `durable:true`:** today's `isDurable:false`
behaviour is already largely incoherent — because `autoDelete`/`exclusive` are always `false`, these
"transient" queues are never actually cleaned up; they sit on the broker until it restarts. The only
real difference from durable is that the queue definition (and any persistent messages on it)
disappears on a broker restart, which is data loss, not a useful feature. The real compatibility
risk of flipping the default is **`PRECONDITION_FAILED` (406, `inequivalent arg 'durable'`)** for
anyone who already has a non-durable queue declared on an existing broker (3.13/4.0/4.1/4.2) —
Brighter would then try to redeclare it as durable and fail until the queue is deleted or the
broker restarted. Message persistence is a separate, existing per-message flag
(`Message.Persist`, `RmqMessagePublisher.cs:249-251`) unaffected by the queue's durable flag.

## Scope Notes
- **Four declare sites** need the same fix, all in this repo: Sync `RmqMessageConsumer.cs:460`
  (main queue) and `:462` (DLQ); Async `RmqMessageConsumer.cs:541` (main queue) and `:546` (DLQ).
  The DLQ always reuses the main queue's `_isDurable`, so a flag-level fix covers both
  automatically; an arguments-level fix (e.g. adding `x-expires`) would need to touch the DLQ
  declare separately, since Sync passes it an empty `Dictionary` and Async passes no arguments.
- **Four default sites**: Sync `RmqSubscription.cs:106`/`:165`; Async `RmqSubscription.cs:113`/`:176`.
  The consumer constructors take `isDurable` as a required parameter with no default of their own
  (Sync `:92`/`:125`, Async `:92`/`:127`), so code that constructs a consumer directly (bypassing
  `RmqSubscription`) is unaffected by a subscription-default change.
- **Sync/Async are not at parity, but not due to a missing guard**: Sync has no `QueueType`/Quorum
  concept at all (no `x-queue-type` in its `SetQueueArguments()`, no queue-type parameter in its
  factory), so "Sync has no quorum check" (noted in triage) is simply inapplicable to Sync, not a gap.
- **Test helpers must move too**: `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/TestHelpers.cs:57`
  and `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/TestHelpers.cs:58` declare with the
  same `false, false` pattern and will fail on 4.3+ whenever called with `isDurable:false`.
- **No other affected paths**: no shared RMQ common project exists; only `RMQ.Sync` and `RMQ.Async`.
  Producers declare only exchanges (`ExchangeConfigurationHelper`, Sync `:64-75`/Async `:70-82`),
  which this feature does not cover and `_isDurable` does not touch. No other transport shares this
  code. Some samples build `RmqSubscription` without `isDurable` and will break on 4.3 until the
  default changes (one sample, `GreetingsServer/Program.cs:52`, already sets `isDurable: true`).
- **Local-dev exposure**: `docker-compose-rmq.yaml:5` uses unpinned `rabbitmq:management`, which now
  resolves to 4.3.x — contributors using that compose file will see every RMQ test fail today. CI
  (`.github/workflows/ci.yml:237`/`:265`) and `docker-compose.yaml:5` are pinned to 4.2/3.13, so CI
  does not currently exercise this. Worth a follow-up: add a 4.3 CI job so the regression test
  actually runs against a broker that enforces the deprecation.
- **Unrelated defect found along the way, out of scope for #4355**: Sync `RmqMessageConsumer.cs:527`
  sets `x-message-ttl` from `_ttl.Value.Milliseconds` (only the 0–999ms sub-second component of the
  `TimeSpan`, not the total), while Async `:628` correctly uses `TotalMilliseconds`. Worth its own
  issue/bugfix — not touched here.

## Regression Test
Two pure unit tests (no broker required), one per gateway, each covering both the non-generic
`RmqSubscription` constructor and the generic `RmqSubscription<T>` constructor. Both are RED today
(each asserts `IsDurable == true` on a subscription built without specifying `isDurable`, which
currently defaults to `false`), approved by the user, and target the fix direction chosen at the
Confirm gate (flip `isDurable`'s default to `true` in both gateways):

- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/When_creating_rmq_subscription_without_specifying_durability_should_default_to_durable.cs`
  — class `RmqSubscriptionDurabilityDefaultTests`, two Facts.
- `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/When_creating_rmq_subscription_without_specifying_durability_should_default_to_durable.cs`
  — class `RmqSubscriptionDurabilityDefaultTests`, two Facts.

Not yet covered by a regression test (left for `/bugfix:fix` and beyond, per Scope Notes):
- The two `TestHelpers.cs` files that declare queues with the same `false, false` pattern will need
  updating alongside the fix so existing broker-dependent tests keep passing; they don't need their
  own new regression test, since they're test infrastructure, not production code.
- The `PRECONDITION_FAILED` upgrade-compatibility risk (existing non-durable queue on an older
  broker) is a deployment/migration concern, not something a unit test can pin — worth a release
  note, not a test.

## Fix
Minimal fix per the Confirm gate's chosen direction: flip `isDurable`'s default from `false` to
`true` on both `RmqSubscription` constructors in both gateways, plus the two test-double call sites
that would otherwise mismatch the new default and fail with `PRECONDITION_FAILED`.

**Production code:**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqSubscription.cs` — `isDurable = false` →
  `isDurable = true` on both constructors (non-generic and `RmqSubscription<T>`); XML doc updated
  to explain why.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqSubscription.cs` — same change, both
  constructors; XML doc updated.

**Test infrastructure (needed to avoid a new regression, per Scope Notes):**
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_requeuing_a_message_via_the_messaging_gateway.cs`
  and `.../When_rmq_sync_consumer_requeues_without_native_delay_should_use_producer.cs` — each
  builds an `RmqSubscription` (now durable by default) and separately pre-creates the same queue via
  the `QueueFactory` test double (still defaults to non-durable). Left alone, the two declares of
  the same queue would disagree on `durable` and RabbitMQ would reject the second with 406
  `PRECONDITION_FAILED` — an AMQP protocol rule independent of the 4.3 deprecation, so this would
  have broken on every broker version, not just 4.3+. Fixed by passing `isDurable: true` explicitly
  to the `QueueFactory` call in both files, matching the subscription.
- The same pattern and fix in the Async gateway:
  `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_rmq_async_consumer_requeues_without_native_delay_should_use_producer.cs`
  and the `Reactor/` file of the same name.
- `QueueFactory`'s own default (`isDurable = false`) was deliberately left unchanged in both
  `TestHelpers.cs` files — most other callers construct `RmqMessageConsumer` directly with an
  explicit `isDurable: false` (bypassing `RmqSubscription`, so unaffected by this fix) and rely on
  `QueueFactory`'s default to match; changing that default would have broken those tests instead.

**Explicitly not changed** (per Change Scope / Scope Notes — out of bounds for this fix):
- No `x-expires` opt-in for a short-lived queue alternative.
- No change to `docker-compose-rmq.yaml`'s image pin or CI's broker versions.
- The unrelated `x-message-ttl` truncation bug in Sync `RmqMessageConsumer.cs:527` (uses
  `.Milliseconds` instead of `.TotalMilliseconds`) — left untouched, noted for its own issue.

**Verification (ahead of `/bugfix:verify`):** ran both full test suites against a live RabbitMQ
broker (`docker-compose-rmq.yaml`, stock `rabbitmq:management` image, resolved to 4.2.3 in this
environment — the unpinned image did not reproduce 4.3 here, but this still validates no
durability-mismatch regression from the fix). Sync: 43 passed, 0 unexpected failures (11
pre-existing environment failures: 9 missing mTLS test certs, 2 missing delayed-message exchange
plugin on the stock image — unrelated to this change). Async: 104 passed, 0 unexpected failures (9
pre-existing mTLS cert failures). The two new regression tests and the four `QueueFactory`-paired
tests all passed.

## Critical Files for Implementation
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqSubscription.cs`
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqSubscription.cs`
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/TestHelpers.cs` (and the Async equivalent)
