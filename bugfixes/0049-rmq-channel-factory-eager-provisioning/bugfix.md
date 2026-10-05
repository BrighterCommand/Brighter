# Bugfix: RMQ channel factories do not declare and bind the queue up front, so messages published before the first receive are lost

**Linked Issue**: #4519
**Status**: Verified

## Symptom

A caller builds an RMQ producer, then builds a channel with `ChannelFactory` (Async: `CreateSyncChannel`,
`CreateAsyncChannel` or `CreateAsyncChannelAsync`; Sync: `CreateSyncChannel`). The subscription uses the
default `MakeChannels = OnMissingChannel.Create`. The caller then publishes and receives.

If the publish happens after the channel is built but before the channel's first `Receive`/`Purge` (or, on
Async, `Requeue`), the queue does not exist or is not bound to the exchange yet. RabbitMQ routes the message
to no queue and drops it. Nothing reports the loss:

- the producer publishes with `mandatory: false`, so the broker does not return the message;
- with publisher confirms on, the broker still acks the publish, so the producer sees success.

The test harnesses hide the defect with a 100 ms "priming" receive straight after creating the channel:

- `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/RmqClassicMessageGatewayProvider.cs:111-115`
  (sync) and `:134-138` (async). Comment: "Ensuring that the queue exists before return the channel".
- `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/RmqQuorumMessageGatewayProvider.cs:111-114` and
  `:133-136`.
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/RmqSyncMessageGatewayProvider.cs:107-111` and
  `:303-306`.

All of these run only when `subscription.MakeChannels == OnMissingChannel.Create`.

The hand-written tests use a second workaround: a test-only `QueueFactory` declares and binds the queue
before any send.

- `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/TestHelpers.cs:47-79`
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/TestHelpers.cs:34-65`
- 28 test files use it, e.g. `Reactor/When_binding_a_channel_to_multiple_topics.cs:43`.

This is the same class of defect as ASB #4309 / PR #4518
(`bugfixes/0041-asb-channel-factory-lazy-subscription/bugfix.md`): a channel factory ignores
`MakeChannels`, and the broker throws away messages that have no destination yet.

## Suspected Location

### RMQ.Async: `src/Paramore.Brighter.MessagingGateway.RMQ.Async/ChannelFactory.cs`

- `:62-76` `CreateSyncChannel`: type check, `_messageConsumerFactory.Create(...)`, `new Channel(...)`.
- `:84-98` `CreateAsyncChannel`: same shape, using `CreateAsync` and `new ChannelAsync(...)`.
- `:107-123` `CreateAsyncChannelAsync`: same again, ending in `Task.FromResult(...)` at `:122`.
- `MakeChannels` is never read in this file, and nothing here talks to the broker. (The issue cites
  `:62-112`; the correct range is `:62-123`.)
- The only dependency is the concrete `RmqMessageConsumerFactory` (`:35`, `:51-54`). Its
  `Create`/`CreateAsync` (`RmqMessageConsumerFactory.cs:64-87`, `:89-112`) return the interfaces
  `IAmAMessageConsumerSync`/`IAmAMessageConsumerAsync`, and pass `subscription.MakeChannels` into the
  consumer (`:81`, `:106`). So a factory-side fix needs the concrete `RmqMessageConsumer`, either by a cast
  or by a new internal factory method.

### RMQ.Async: `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`

- **Constructor (`:130-169`) does no I/O.** It stores `_makeChannels` (`:151`) and checks the quorum
  settings (`:161-168`).
- **`EnsureChannelAsync` (`:491-525`)** is `protected virtual` and is the only place the queue is
  provisioned. (The issue cites `:491-515`; the method ends at `:525`.) Step by step:
  1. **Guard (`:493`):** the whole body runs only if `Channel == null || Channel.IsClosed`.
  2. **`EnsureBrokerAsync(_queueName)` (`:495`):** goes to `RmqMessageGateway.cs:119-128`, then the circuit
     breaker (`:130-133`), then retry (`:135-139`), then `ConnectToBrokerAsync` (`:141-178`), which gets a
     pooled connection (`:152-159`), opens an AMQP channel with publisher confirms (`:164-168`) and declares
     the exchange (`:171`). The consumer does not pass `makeExchange`, so the default
     `OnMissingChannel.Create` (`RmqMessageGateway.cs:121`) applies whatever `_makeChannels` says.
  3. **`MakeChannels` handling:**
     - `Create` (`:497-501`): `CreateQueueAsync` (`:588-609`; main queue `:595`, invalid-message queue
       `:600`, DLQ `:606`), then `BindQueueAsync` (`:611-634`; routing keys `:619`, invalid-message key
       `:625`, DLQ to dead-letter exchange `:631`).
     - `Validate` (`:502-505`): `ValidateQueueAsync` (`:650-668`) passively declares the main queue (`:660`)
       and the invalid-message queue (`:662`). It does not check bindings or the DLQ. Failures are wrapped
       as `BrokerUnreachableException` (`:666`).
     - `Assume` (`:506-509`): does nothing.
  4. **Delayed-requeue topology (`:511-512`), only when `DelaySupported`:**
     `RmqDelayedRequeue.EnsureTopologyAsync` (`RmqDelayedRequeue.cs:36-60`) also honours `MakeChannels`
     (`Assume` returns `:43-44`; `Validate` passive-declares `{exchange}.requeue` `:47-50`; `Create`
     declares it as `x-delayed-message` `:53-57` and binds the queue `:58-59`).
  5. **`CreateConsumerAsync` (`:514` → `:540-564`):** builds a `PullConsumer`, sets prefetch
     (`BasicQosAsync`, `PullConsumer.cs:52-54`), then `basic.consume` (`BasicConsumeAsync`, `:551-558`).
     **This starts consumption**: the broker begins pushing up to `batchSize` unacked messages into the
     in-memory buffer.
  6. Null checks and log (`:516-523`).
- **Callers of `EnsureChannelAsync`:** `PurgeAsync` `:208`, `ReceiveAsync` `:261`, `RequeueAsync` `:457`.
  The sync `Purge`/`Receive`/`Requeue` (`:200`, `:245`, `:422`) wrap these with `BrighterAsyncContext.Run`.
  `AcknowledgeAsync` (`:183`), `NackAsync` (`:331`) and `RejectAsync` (`:373`) call only
  `EnsureBrokerAsync`, which opens a channel but declares no queue.

### RMQ.Async producer

- `RmqMessageProducer.cs:186`: `EnsureBrokerAsync(makeExchange: _publication.MakeChannels)` declares only
  the exchange.
- `RmqMessageProducer.cs:208`: calls `rmqMessagePublisher.PublishMessageAsync(message, delay.Value,
  cancellationToken)` without a `mandatory` argument.
- `RmqMessagePublisher.cs:94` defaults `mandatory = false`; `:108-111` passes it to `BasicPublishAsync`.
- `mandatory: true` is used only when forwarding to the invalid channel (`RmqMessageConsumer.cs:580`).

### RMQ.Sync: `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/`

**`ChannelFactory.cs`**

- `:61-75` `CreateSyncChannel` does no I/O and never reads `MakeChannels`.
- `:83-86` `CreateAsyncChannel` throws `ConfigurationException` (Reactor-only).
- Correction to the issue: `CreateAsyncChannelAsync` (`:95-111`) does not throw `ConfigurationException`;
  it calls `RmqMessageConsumerFactory.CreateAsync`, which throws `NotImplementedException`
  (`RmqMessageConsumerFactory.cs:91-93`). Inconsistent, but out of scope.

**`RmqMessageConsumer.cs`, `EnsureChannel()` (`:414-447`)**, same shape as Async:

- exchange/URI null checks (`:416-420`); guard `Channel == null || Channel.IsClosed` (`:422`);
  `EnsureBroker(_queueName)` (`:424`), again with the default `makeExchange = Create`
  (`RmqMessageGateway.cs:117`).
- `Create` (`:426-430`): `CreateQueue()` (`:511-525`; declares at `:520`, `:522`, `:524`) and `BindQueue()`
  (`:527-546`; binds at `:537`, `:541`, `:545`).
- `Validate` (`:431-434`): `ValidateQueue()` (`:555-572`; passive declares `:564`, `:566`, wrapped as
  `BrokerUnreachableException` `:570`).
- `Assume` (`:435-438`): nothing.
- Delayed-requeue topology (`:440-441`, `RmqDelayedRequeue.cs:34`, bind at `:54`).
- `CreateConsumer()` (`:443` → `:462-478`): `new PullConsumer(Channel, _batchSize)` (`BasicQos` at
  `PullConsumer.cs:49`), then `BasicConsume` at `:475`.
- Callers: `Purge` (`:191`) and `Receive` (`:253`). Unlike Async, Sync `Requeue` calls only
  `EnsureBroker(_queueName)` (`:367`); `Acknowledge`/`Nack` call `EnsureBroker()` (`:170`, `:220`);
  `Reject` calls `EnsureBroker(_queueName)` (`:313`).

**Producer**

- `RmqMessageProducer.cs:156`: `EnsureBroker(makeExchange: _publication.MakeChannels)`.
- `RmqMessageProducer.cs:177`: `rmqMessagePublisher.PublishMessage(message, delay.Value)`.
- `RmqMessagePublisher.cs:78`: `mandatory = false`, passed at `:97`.

### Where channels are built at runtime

`src/Paramore.Brighter.ServiceActivator/ConsumerFactory.cs:100` (`CreateSyncChannel`) and `:124`
(`CreateAsyncChannel`). The dispatcher's Proactor path calls the synchronous `CreateAsyncChannel`, not
`CreateAsyncChannelAsync`.

## Root-Cause Hypothesis

Neither RMQ `ChannelFactory` honours `Subscription.MakeChannels` when it builds a channel. The queue is
declared and bound only inside `RmqMessageConsumer.EnsureChannelAsync` (Async, `:491-525`) or
`EnsureChannel` (Sync, `:414-447`), and those run lazily on the first `Receive`/`Purge` (and, on Async,
`Requeue`). The producer declares only the exchange and publishes with `mandatory: false`.

So under `OnMissingChannel.Create`, a message published to the exchange between building the channel and
the first receive matches no binding. RabbitMQ drops it silently, and the publisher confirm still acks it.
The harness priming receives and the hand-written tests' `QueueFactory` pre-declares exist only because of
this.

**Findings for confirm.** The issue's open question (a) is borne out:

1. **Provisioning and starting consumption are fused.** `EnsureChannelAsync`/`EnsureChannel` open the AMQP
   channel and declare the exchange, declare/bind or validate the queue, set up the delayed-requeue
   topology, and start `basic.consume` (Async `:514`/`:551`, Sync `:443`/`:475`), all in one call. Calling
   the method as-is from the factory would start broker push into the `PullConsumer` buffer before the pump
   runs, with up to `batchSize` messages unacked from channel creation onwards. Not message loss (they are
   redelivered if the channel closes), but a behaviour change.
2. **The steps can be separated, but there is a trap.** The declare/bind/validate helpers
   (`CreateQueueAsync`, `BindQueueAsync`, `ValidateQueueAsync` and the Sync equivalents) are private and
   need only an open `Channel`. `RmqDelayedRequeue.EnsureTopology[Async]` binds the queue, so it must run
   after the queue is declared. `CreateConsumer[Async]` is the only `basic.consume` call.
   The trap is the guard at Async `:493` / Sync `:422`, which checks only whether the AMQP channel is open.
   If a provisioning-only entry point opened `Channel` without starting the consumer, the next
   `ReceiveAsync` would skip the whole body: `_consumer` would be null, Async would throw
   `ChannelFailureException` at `:263`, and Sync would null-dereference at `_consumer!.DeQueue` (`:256`).
   A split needs its own "provisioned" / "consumer started" state, not the existing `Channel` check.
3. **Validate is only partly honoured today.** It passively declares the main and invalid-message queues,
   but not bindings, the DLQ, or (on Async) the exchange, because the consumer always calls `EnsureBroker`
   with `makeExchange = Create`. That default is a separate quirk, out of scope here.
4. **Errors at start-up.** Today a broker failure inside `EnsureChannelAsync` becomes
   `ChannelFailureException` in `ReceiveAsync`'s catch blocks (`:292-308`, via `HandleExceptionAsync`
   `:636-648`), which the pump treats as recoverable. Moving provisioning into the factory would surface
   the same failures (after `EnsureBroker`'s circuit breaker and retry, `RmqMessageGateway.cs:130-139`)
   from `ConsumerFactory.Create` at dispatcher start, before any pump error handling.
5. **Unaffected:** `Assume` needs nothing up front and should stay free of I/O.

**Suggested fix (UNVERIFIED — to be proven or refuted in /bugfix:confirm), restated from the issue:**
follow #4518.

- Add an internal entry point on `RmqMessageConsumer` that runs the provisioning part of
  `EnsureChannelAsync`.
- `ChannelFactory` calls it before returning: `BrighterAsyncContext.Run` on the sync paths, `await` in
  `CreateAsyncChannelAsync`. RMQ.Sync calls the `EnsureChannel()` equivalent directly.
- Remove the harness priming receives.

Open points for confirm:

- whether to split declare/bind (plus Validate's passive declare and the delayed-requeue topology) from
  `CreateConsumer[Async]`, given finding 2;
- whether to honour `Validate` up front;
- how start-up failures should surface;
- what to do with the test-side `QueueFactory` pre-declares.

**Test seams.** There is no in-memory RMQ connection or channel double in either test project:

- `TestDoubles/FaultingRmqChannel.cs` is a `DispatchProxy` wrapping a real `IChannel` (injected by
  `CleanupFailureRmqConsumer.cs:43` after a real connection).
- `TestDoubles/TestDoubleRmqMessageConsumer.cs:38-76` are subclasses that override `EnsureChannelAsync` to
  throw.
- No `InternalsVisibleTo` in either RMQ src project.

So a regression test proving the message is not lost must be broker-backed: build the channel through
`ChannelFactory` with `Create`, publish before any receive, then receive.

**Existing `ChannelFactory` tests** are network-free today and must stay so, or be reconsidered:

- `RMQ.Async.Tests/MessagingGateway/When_subscription_matches_should_create_sync_channel.cs`: the
  type-mismatch tests (`:46-99`) use `Create` but throw before any I/O; the "matching" tests (`:103-145`)
  use `OnMissingChannel.Assume` (`:58-63`) against `amqp://localhost`.
- The RMQ.Sync equivalent `When_subscription_matches_should_create_sync_channel.cs`.
- The scheduler-forwarding tests (`When_rmq_{async,sync}_channel_factory_forwards_scheduler_to_consumers.cs`,
  `When_rmq_{async,sync}_channel_factory_has_scheduler_should_pass_to_consumers.cs`) only check the
  `Scheduler` property and create no channels (`:37-61`).
- The generated conformance check
  `When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs` covers only the
  subscription-to-factory declaration.
- `MessageDispatch/When_building_a_dispatcher*.cs` use `ChannelFactory`; whether they create channels with
  `Create` against a broker is to be checked in confirm.

## Confirmed Root Cause

**Verdict: CONFIRMED** (code trace; a red repro needs a live broker).

**Framing (owner decision, 2026-10-04): the producer is not part of the defect.** A publisher does not know
its subscribers, and making it declare their queues would couple the two. So the producer declaring only the
exchange, and publishing with `mandatory: false`, is correct by design. `mandatory: true` is not a fix
either: a message with no subscriber is normal pub/sub. A message published before any subscriber's channel
exists is not delivered, and that is inherent pub/sub behaviour, out of scope. The producer is cited above
only to explain why nothing reports the loss.

**The defect is consumer-side.** Neither RMQ `ChannelFactory` honours `Subscription.MakeChannels`:

- Async `ChannelFactory.cs:62-76`, `:84-98` and `:107-123` (`Task.FromResult` at `:122`) only check the
  subscription type, build a consumer and wrap it.
- Sync `ChannelFactory.cs:61-75` has the same shape.

After a channel has been handed out for a subscription with `Create` (or `Validate`), the caller can expect
the queue to exist and be bound (or to have been checked). In fact nothing happens until the first
`Receive`/`Purge` (or `Requeue` on Async) runs `RmqMessageConsumer.EnsureChannelAsync` (Async `:491-525`) or
`EnsureChannel` (Sync `:414-447`). A message published in that window matches no binding and is lost.

**How wide the window is:**

- **ServiceActivator: small but real.** `Dispatcher.Receive()` (`Dispatcher.cs:405`) builds every consumer,
  and each pump's first action is `Channel.Receive` (`Reactor.cs:123`, Proactor `:162`). So the gap is
  milliseconds, plus the time to build the other subscriptions' consumers. If that first ensure fails, the
  pump sleeps `ChannelFailureDelay` (`Reactor.cs:138-143`, `Proactor.cs:177-182`) and the queue still does
  not exist.
- **Wide for direct `ChannelFactory` callers** that publish before receiving: the conformance harness,
  custom hosts, and RPC. `CommandProcessor.Call` already works around the defect: it builds the channel at
  `CommandProcessor.cs:1489`, then calls `Purge` at `:1497`, with the comment "or we won't have anything
  to send to".

## Evidence
- [x] Code-trace:
  1. **No broker I/O in either factory or in construction.**
     - The factories only build the consumer (Async `RmqMessageConsumerFactory.cs:64-112`).
     - The consumer constructor (`RmqMessageConsumer.cs:130-169`), the base `RmqMessageGateway` constructor
       (`RmqMessageGateway.cs:72-96`: policies and a `ConnectionFactory`, no connect) and the
       `Channel`/`ChannelAsync` constructors do no I/O.
     - `MakeChannels` is never read in either factory.
  2. **The queue is declared and bound only in `EnsureChannel[Async]`.**
     - Async, in order:
       - guard `:493`;
       - `EnsureBrokerAsync` `:495`: connect, publisher-confirm channel `RmqMessageGateway.cs:164-168`,
         exchange `:171`;
       - Create `:497-501`: declares `:595`/`:600`/`:606`, binds `:619`/`:625`/`:631`;
       - Validate `:502-505`: passive declares `:660`/`:662`;
       - Assume `:506-509`;
       - delayed topology `:511-512`;
       - `CreateConsumerAsync` `:514`: `BasicConsumeAsync` `:551`.
     - Async callers: `PurgeAsync` `:208`, `ReceiveAsync` `:261`, `RequeueAsync` `:457`.
     - Sync follows the same shape (`:422`-`:443`, `BasicConsume` `:475`); callers `Purge` `:191` and
       `Receive` `:253`.
     - Grepping `src/` for `QueueDeclare|QueueBind` finds only the two consumers and the two
       `RmqDelayedRequeue`s.
  3. **Nothing reports the loss.** This is pub/sub semantics, not a defect.
     - The producer declares only the exchange (Async `RmqMessageProducer.cs:186`, Sync `:156`).
     - It publishes with `mandatory=false` (Async `RmqMessagePublisher.cs:94`, Sync `:78`).
     - RabbitMQ drops an unroutable non-mandatory message and still sends `basic.ack` under publisher
       confirms, so `OnPublishSucceeded` fires.
  4. **No other path declares the queue before the first receive.**
     - `ConsumerFactory.cs:100`/`:124` only wrap the factory result.
     - `Dispatcher` only creates and opens consumers.
     - `CombinedChannelFactory.cs:47-80` only delegates.
     - `ServiceActivatorHostedService.cs:75` only calls `Receive()`.
     - There is no RMQ ensure-topology helper.
     - The only production `Purge` is RPC (`CommandProcessor.cs:1497`).
  5. **The test harnesses hide the defect** with priming receives: `RmqClassicMessageGatewayProvider.cs:114`
     and `:137`, `RmqQuorumMessageGatewayProvider.cs:113` and `:135`, and Sync
     `RmqSyncMessageGatewayProvider.cs:110` and `:305`.
- [ ] Red repro: not run, since it needs a broker. Planned broker-backed regression test for `/bugfix:test`:
  1. Build the channel via `ChannelFactory` with `Create`, with no priming receive.
  2. Publish.
  3. Receive, and expect the message. Today it is lost.

  Cover each factory method: Async `CreateSyncChannel`, `CreateAsyncChannel` and `CreateAsyncChannelAsync`,
  and Sync `CreateSyncChannel`. Add a test that `Validate` against a missing queue throws
  `ChannelFailureException` from the factory, and a test that `Assume` does no I/O.

### Suggested-fix assessment: PARTIAL

The direction is right: an internal entry point on `RmqMessageConsumer`, called by the factory before it
returns, with the harness priming receives removed. Taken literally, though, it has two gaps:

- **Assume would do I/O.** Unlike ASB, RMQ's ensure is not a no-op for `Assume`. It still connects,
  declares the exchange, sets up the delayed topology and starts `basic.consume`. The entry point must
  return immediately on `Assume`. That keeps the network-free factory tests I/O-free: the
  `When_subscription_matches_should_create_sync_channel.cs` "matching" tests use `Assume` (Async
  `:160`/`:162`), and so do the `*_has_scheduler_*` tests.
- **Failure wrapping and cleanup were unspecified.**

**Recommended shape (a): call the existing `EnsureChannel[Async]` behind an `Assume` guard.**

- Async: `internal Task EnsureChannelExistsAsync(CancellationToken)`. Sync: `internal void
  EnsureChannelExists()`, called directly with no `BrighterAsyncContext`.
- **Factory order:**
  1. type check first, so `ConfigurationException` is still thrown before any I/O;
  2. cast to `RmqMessageConsumer` (safe, since the factory only builds that type);
  3. ensure. Async sync paths use `BrighterAsyncContext.Run`. `CreateAsyncChannelAsync` validates
     synchronously, then awaits in a private async method.
- **On failure:**
  - dispose the consumer, which releases the pooled connection (`RmqMessageGatewayConnectionPool.cs:90`);
  - rethrow, wrapping anything that is not a `ConfigurationException` as `ChannelFailureException`. That
    matches the consumer's `HandleException` (`:636-648`) and ASB 0041.

Side effect of (a): `basic.consume` starts at channel creation, so up to `BufferSize` messages are
prefetched, unacked, before the pump runs. Nothing is lost:

- dispose cancels the consumer and nacks the buffer with requeue (`PullConsumer.cs:120-138`);
- a channel close also requeues them.

On the ServiceActivator path the extra hold is milliseconds. Threading is fine: the factory and the pump use
the channel one after the other, never at the same time.

**Alternative (b): split out a provisioning-only method.** This needs a guard change. Today a
provisioning-only call would open `Channel`, so the next `Receive` would skip `CreateConsumer[Async]`:
Async would throw at `:263`, and Sync would null-dereference at `:256`. It would need a separate
consumer-started check, plus resetting `_consumer` on reconnect. Choose (b) only if consumption must not
start before the pump runs.

## Scope Notes

**In scope:**
- RMQ.Async `ChannelFactory.cs` (all three methods) and `RmqMessageConsumer.cs` (the entry point).
- RMQ.Sync `ChannelFactory.cs` (`CreateSyncChannel`) and `RmqMessageConsumer.cs`.
- Remove the six priming receives: `RmqClassicMessageGatewayProvider.cs:111-115`/`:134-138`,
  `RmqQuorumMessageGatewayProvider.cs:110-114`/`:132-136`, and Sync
  `RmqSyncMessageGatewayProvider.cs:107-111`/`:303-306`.
- Release note under `## Master`. It must cover:
  - **Provisioning and validation failures now surface from channel creation.** They escape through
    `Dispatcher.Receive()` (`:405`) and `ServiceActivatorHostedService.StartAsync` (`:75`), so the host fails
    to start. They also escape through `Dispatcher.Open` (`:370`) and `SetActivePerformers` (`:459`).
    Previously the pump logged them and retried every `ChannelFailureDelay`.
  - **The built-in retry covers only connect, open-channel and exchange declare**
    (`RmqMessageGateway.cs:130-139`).
    - It retries only `BrokerUnreachableException`: `ConnectionRetryCount` (default 3) attempts with
      exponential backoff from `RetryWaitInMilliseconds` (2 s, 4 s and 8 s, about 14 s per channel).
    - The circuit breaker opens after one failure, for `CircuitBreakTimeInMilliseconds` (default 60 s).
    - Queue declare, bind and validate are not retried.
  - **`Validate` against a missing queue** now throws `ChannelFailureException` at channel creation.

**Tests that build channels via RMQ `ChannelFactory` with `Create`.** All of them are broker-backed in CI.

- **Dispatcher tests.** Async `MessageDispatch/When_building_a_dispatcher.cs` and `..._async.cs`, and Sync
  `When_building_a_dispatcher.cs`, now do I/O at `Receive()`. Without a broker they fail instead of silently
  passing.
- **Redundant `QueueFactory` calls (removal optional).** Four tests that go through `ChannelFactory` keep a
  `QueueFactory` pre-declare that becomes redundant:
  - Async Proactor and Reactor `When_rmq_async_consumer_requeues_without_native_delay_should_use_producer.cs`
    (`:75`);
  - Sync `When_requeuing_a_message_via_the_messaging_gateway.cs` (`:102`);
  - Sync `When_rmq_sync_consumer_requeues_without_native_delay_should_use_producer.cs` (`:75`).
- **The other 24 `QueueFactory` users** build `RmqMessageConsumer` directly, or have no consumer, and must
  keep it. A directly built consumer stays lazy by contract.

**Out of scope (record only):**
- **Producer queue declaration and `mandatory`.** Not a defect (owner decision above).
- **Consumer always creates the exchange.** It calls `EnsureBroker` with `makeExchange = Create` (Async
  `RmqMessageGateway.cs:121`, Sync `:117`), so `Validate`/`Assume` consumers still create it, and `Validate`
  never checks it.
- **Latent guard gap.** `Ack`/`Nack`/`Reject` (both transports) and Sync `Requeue` (`:367`) can reopen a
  closed `Channel` through `EnsureBroker` without a consumer. The next `Receive` then skips
  `EnsureChannel`. It probably self-heals, but it is a race. Option (b)'s consumer check would close it.
- **Sync `CreateAsyncChannelAsync`** throws `NotImplementedException` where `CreateAsyncChannel` throws
  `ConfigurationException`.
- **`CommandProcessor.Call`'s `Purge`** (`:1497`) is redundant for RMQ but still needed for other
  transports. Keep it. After the fix, RMQ provisioning happens at `:1489`, outside
  `ExecuteWithResiliencePipeline`.
- **`Dispatcher.CreateConsumers` leaks** the consumers it has already built if it throws part-way
  (`Dispatcher.cs:584-595`). This matters more now that consumers hold open channels. A ServiceActivator
  concern, outside this fix.

## Regression Test

Broker-backed apart from the two `Assume` tests. RMQ.Async tests run against `docker-compose-rmq.yaml` (RabbitMQ 4.2). RMQ.Sync tests run against `rabbitmq:3.13-management`, which is RMQ.Sync's CI image.

| Test | How RED was observed |
|---|---|
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Reactor/When_creating_a_sync_channel_with_create_should_deliver_a_message_published_before_the_first_receive.cs` | Failed before the fix. `Expected: <id>`, `Actual: ` (empty), because the receive timed out after 5 s with `MT_NONE`. |
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_creating_an_async_channel_with_create_should_deliver_a_message_published_before_the_first_receive.cs` | Failed before the fix, the same way. |
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_creating_an_async_channel_asynchronously_with_create_should_deliver_a_message_published_before_the_first_receive.cs` | Failed before the fix, the same way. It was a genuine failure, not a characterisation test. |
| `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_creating_a_sync_channel_with_create_should_deliver_a_message_published_before_the_first_receive.cs` | Run against RabbitMQ 3.13, which is RMQ.Sync's CI image. Failed before the fix, the same way. |
| `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_creating_a_channel_with_validate_for_a_missing_queue_should_throw.cs` | Failed before the wrapping was added: `Expected ChannelFailureException`, `Actual BrokerUnreachableException`. The raw RabbitMQ exception escaped the factory. |
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Reactor/When_creating_a_channel_with_validate_for_a_missing_queue_should_throw.cs` | Failed the same way, with `BrokerUnreachableException`. |
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_creating_an_async_channel_with_validate_for_a_missing_queue_should_throw.cs` | Failed the same way. |
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_creating_an_async_channel_asynchronously_with_validate_for_a_missing_queue_should_throw.cs` | Failed the same way. |
| `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/When_creating_a_channel_with_assume_should_not_connect_to_the_broker.cs` | Characterisation test, network-free: it points at an unreachable broker on port 1 with zero retries. It failed with `ChannelFailureException` when the `Assume` guard was removed from `RmqMessageConsumer.EnsureChannelExistsAsync`; the guard was then restored. |
| `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/When_creating_a_channel_with_assume_should_not_connect_to_the_broker.cs` | Characterisation test. It failed the same way when the `Assume` guard was removed from `RmqMessageConsumer.EnsureChannelExists`; the guard was then restored. |

## Fix

This is shape (a), approved at Confirm. Each factory path went in with its own regression test, one slice and one commit at a time.

**RMQ.Async**

- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`: added `internal Task EnsureChannelExistsAsync(CancellationToken)`. It returns immediately for `Assume`; otherwise it runs the existing `EnsureChannelAsync`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/ChannelFactory.cs`:
  - The subscription type is still checked first, so a mismatch throws `ConfigurationException` before any I/O.
  - `CreateSyncChannel` and `CreateAsyncChannel` provision through `EnsureChannelExists`, which runs the ensure step under `BrighterAsyncContext.Run`.
  - `CreateAsyncChannelAsync` was first split into synchronous validation plus `CreateAsyncChannelCoreAsync` (a `refactor:` commit). The core then awaits `EnsureChannelExistsAsync` and passes the caller's cancellation token through.
  - On failure, both helpers dispose the consumer, which releases its pooled connection. They rethrow `ConfigurationException` and wrap anything else as `ChannelFailureException`.

**RMQ.Sync**

- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs`: added `internal void EnsureChannelExists()` with the same `Assume` guard. It calls the synchronous `EnsureChannel()` directly.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/ChannelFactory.cs`: `CreateSyncChannel` provisions through the same dispose-and-wrap helper.

**Harness and release note**

- Removed the six 100 ms priming receives from `RmqClassicMessageGatewayProvider.cs`, `RmqQuorumMessageGatewayProvider.cs` and `RmqSyncMessageGatewayProvider.cs`.
- Added a `## Master` release note in `release_notes.md` covering:
  - provisioning and validation failures now surface at channel creation (dispatcher start-up);
  - retry applies only to connecting;
  - prefetch now starts at channel creation.

**Not changed**

- The test-side `QueueFactory` pre-declares are still in place. The 24 tests that build `RmqMessageConsumer` directly need them, because a consumer built directly stays lazy. Removing the four that became redundant was optional, and I did not do it.
- The producer side is unchanged, as decided by the owner.
