# Bugfix: Generated ASB conformance tests never receive — no broker infrastructure is provisioned before the send

**Linked Issue**: #4309
**Status**: Triaged

## Symptom

**Observed.** When the generated Azure Service Bus conformance suites are run un-skipped against a
real ASB namespace, almost every test that sends a message and then reads it back observes
`MT_NONE` — the received `Message` is the empty default. The send itself raises no error; nothing is
logged; the message simply never arrives.

**Expected.** A test that builds a producer and a channel from `OnMissingChannel.Create`
publication/subscription pairs, sends, and then receives should get its message back.

**Reproduction.** Un-skip the generated ASB tests and run against a broker:

```
dotnet build tools/Paramore.Brighter.Test.Generator
cd tests/Paramore.Brighter.AzureServiceBus.Tests && dotnet run --no-build \
  --project ../../tools/Paramore.Brighter.Test.Generator --framework net10.0
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests --filter "FullyQualifiedName~MessagingGateway"
```

with `BrighterTestsASBConnectionString` / `BrighterTestsASBNameSpace` set. All 36 generated cases
currently carry `[Fact(Skip = "Deferred: #4240 …")]` except six, so the failure is only visible on a
probe branch that removes the skips.

**Observed CI run** (run `34127954869`, job `101763183832`, `azure-ci`, conclusion `failure`).
The raw log gives `Total tests: 161 / Passed: 131 / Failed: 30` — **30** distinct generated tests
failed, splitting into three distinct causes:

- **26** failed with `Assert.NotEqual() Failure: Values are equal` (i.e. `MT_NONE`). **This bug.**
- **2** failed with `System.UriFormatException` at `AzureServiceBusMessageCreator.cs:329`
  (`GetSource`) — the two delayed-send tests, the only ones that **did** receive a real message.
  This is **#4310**.
- **2** failed with `ServiceBusException : SubCode=40900. Conflict` on the topic entity — the two
  `When_multiple_threads_try_to_post_a_message_at_the_same_time` tests. A **third, distinct** symptom
  (a concurrent topic-creation race in the producer's `EnsureChannelExistsAsync`), neither #4309 nor
  #4310. Flagged, not triaged here.

The tests that passed include `When_infrastructure_missing_and_assume/validate_channel_should_throw_exception`
and `When_posting_a_message_but_no_broker_created_should_throw_exception` — consistent with the
diagnosis, since those never expect a successful receive.

## Suspected Location

Gateway (production code):

- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannelFactory.cs:54-67` —
  `CreateSyncChannel`: builds a `Channel` around a consumer; creates nothing on the broker.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannelFactory.cs:75-88` —
  `CreateAsyncChannel`: same, for `ChannelAsync`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannelFactory.cs:97-99` —
  `CreateAsyncChannelAsync` is `=> Task.FromResult(CreateAsyncChannel(subscription))`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannelFactory.cs:101-114` —
  `GetAndCheckSubscription`, the *only* thing done with the subscription: a type check and a
  `TimeOut >= 400ms` check. `MakeChannels` is never read.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicConsumer.cs:79-122` —
  `EnsureChannelAsync`, where `CreateSubscriptionAsync` actually happens (line 98).
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs:372` —
  `protected abstract Task EnsureChannelAsync();` and its call sites at
  `:106` (`AcknowledgeAsync`), `:177` (`ReceiveAsync`), `:243` (`NackAsync`), `:300` (`RejectAsync`).
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageProducer.cs:248` —
  `GetSenderAsync` calls `EnsureChannelExistsAsync(topic)`; this is on the **send** path
  (`SendWithDelayAsync:207` -> `GetSenderAsync:215`), **not** on producer construction.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicMessageProducer.cs:61-88` —
  `EnsureChannelExistsAsync` creates the **topic** only, never a subscription.

Test harness:

- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/AzureServiceBusMessageGatewayProvider.cs:115-120`
  (`CreateChannel`) and `:251-258` (`CreateChannelAsync`) — delegate straight to the channel factory
  and return; no provisioning, no priming receive.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/AzureServiceBusMessageGatewayProvider.cs:58-62` —
  a fresh `Uuid` topic and subscription name per call, so nothing pre-exists on the broker.
- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/Generated/Proactor/When_posting_a_message_via_the_messaging_gateway_should_be_received.cs:54-63` —
  the canonical ordering.

## Root-Cause Hypothesis

**UNVERIFIED — to be proven or refuted in /bugfix:confirm.**

`AzureServiceBusChannelFactory` provisions no broker-side infrastructure. The ASB *subscription* is
created lazily, on the consumer's first `EnsureChannelAsync`, which only runs from
`ReceiveAsync`/`AcknowledgeAsync`/`NackAsync`/`RejectAsync`/`PurgeAsync`. Every generated test sends
before it first receives. An ASB **topic** with no subscription attached silently discards published
messages, so the message is dropped; the subsequent first `ReceiveAsync` creates the subscription too
late and every read returns `MT_NONE`.

**Refutation test.** Insert a `CreateSubscriptionAsync` (or a priming `ReceiveAsync`) between
`CreateChannelAsync` and the send in one generated test; if the message is then received, the
hypothesis holds. If it still returns `MT_NONE`, it is refuted.

### Claim-by-claim verification (triage sub-agent, against the code and the raw CI log)

**Claim 1 — CORROBORATED.** `CreateAsyncChannel` at `:75-88`, `CreateAsyncChannelAsync` at `:97-99`
is exactly `Task.FromResult(CreateAsyncChannel(subscription))`. The sync twin `CreateSyncChannel`
at `:54-67` is structurally identical and equally inert. There is *no* `CreateSyncChannelAsync` on
this factory, unlike the AWS one.

**Claim 2 — CORROBORATED, but the CALL-SITE LABELS WERE WRONG.** The four cited line numbers in
`AzureServiceBusConsumer.cs` are all real `await EnsureChannelAsync()` calls, but not the operations
originally named:

| line | actual method | originally said |
| --- | --- | --- |
| 106 | `AcknowledgeAsync` | Receive |
| 177 | `ReceiveAsync` | ReceiveAsync |
| 243 | `NackAsync` | Ack |
| 300 | `RejectAsync` | Purge |

`Purge` is a **fifth** trigger, in the derived class: `AzureServiceBusTopicConsumer.PurgeAsync`
(`:71-77`) deletes the topic and then calls `EnsureChannelAsync()` at `:76`. There is **no** sync
`EnsureChannel` — the abstract member at `AzureServiceBusConsumer.cs:372` is async-only, and every
sync entry point (`Receive:161`, `Acknowledge:95`, `Nack:232`, `Reject:287`, `Purge:145`) is
sync-over-async via `BrighterAsyncContext.Run`. Both pumps hit the same lazy path.

**Claim 3 — PARTLY WRONG. The most significant correction.** The ordering
`CreateProducerAsync -> CreateChannelAsync -> send -> receive` is confirmed verbatim, and
`CreateChannelAsync` creating nothing is confirmed. But **`CreateProducerAsync` does NOT create the
topic.** `AzureServiceBusMessageProducerFactory.CreateAsync` is `Task.FromResult(Create())`
(`:96-99`), and `Create()` (`:67-94`) merely `new`s up producer instances — no admin-client call.
The topic is created *lazily too*, during the send: `SendWithDelayAsync` -> `GetSenderAsync` ->
`EnsureChannelExistsAsync` (`AzureServiceBusMessageProducer.cs:248`).

This **sharpens** the diagnosis rather than weakening it: at the instant of publish the topic has
just been created microseconds earlier and provably has zero subscriptions. It also explains the two
`SubCode=40900` failures — N threads race into `EnsureChannelExistsAsync` and collide creating the
same topic.

**Claim 4 — CORROBORATED, three for three.** Every hand-written ASB test that actually round-trips
through the broker pre-creates the subscription: `When_posting_a_message_via_the_producer.cs:65`,
`When_consuming_a_message_via_the_consumer.cs:94`, and
`When_posting_a_large_message_via_the_producer.cs:66-68` (queue, topic **and** subscription). The
remaining hand-written files are pure mapping unit tests that never touch a broker, so they are not
controls either way. There is **no** hand-written counter-example that sends and receives without
pre-creating — which is exactly why this defect survived until the generated suite exercised the
untouched path.

**Claim 5 — CORROBORATED, and clinched by the CI log.** The delayed test is the sole generated case
whose delivery is deferred. Both delayed tests failed with `UriFormatException`, and the stack frame
is `When_sending_a_delayed_message_should_deliver_after_delay.cs(76,0)` — **line 76 is the receive
inside the after-delay loop**, not the before-delay arm at line 67. So the before-delay assertion
passed (correctly `MT_NONE`) and the after-delay receive returned a genuine broker message, which
then blew up in `GetSource`. These two received; the other 26 did not. The exception proves the rule,
in both pumps.

**Claim 6 — NOT CONFIRMABLE FROM CODE; inferred.** Nothing in the Brighter source asserts or
documents that an ASB topic with no subscription discards published messages — it is broker
semantics, relied on only implicitly. What the evidence shows: the send completes without throwing
(`SendWithDelayAsync` logs `PublishedMessage` and returns), no `ChannelFailureException` is raised
anywhere, and 26 subsequent reads return `MT_NONE`. Consistent with silent discard and inconsistent
with any error-signalling alternative — but an inference from behaviour, not from code. **Nail this
down in /bugfix:confirm** (e.g. assert the topic's message count, or pre-create the subscription and
observe the same test pass).

### Additional determinations

**The factory receives and ignores `MakeChannels`.** `Subscription.MakeChannels`
(`src/Paramore.Brighter/Subscription.cs:91`) is present on the `AzureServiceBusSubscription` handed
to `CreateSyncChannel`/`CreateAsyncChannel`. The generated tests set it to `OnMissingChannel.Create`.
The factory reads it **nowhere** — `GetAndCheckSubscription` (`:101-114`) checks only type and
`TimeOut`. The setting is honoured, but only later and only by the consumer
(`AzureServiceBusTopicConsumer.cs:81`, `:92`). This is the substance of the issue's open design
question, and it is factually accurate.

**ASB is the outlier among test gateway providers** — the only one that does neither of the two
things every other broker-backed provider does:

- **AWS** — `SnsStandardMessageGatewayProvider.cs:191-202` and `:204-217`: after building the
  channel, `if (subscription.MakeChannels == OnMissingChannel.Create)` it fires a 100 ms
  `Receive`/`ReceiveAsync` to force provisioning. And the AWS `ChannelFactory` *already* provisions
  eagerly — `src/Paramore.Brighter.MessagingGateway.AWSSQS/ChannelFactory.cs:116` (`EnsureQueueAsync`)
  and `:130` (`EnsureSubscriptionAsync`), both passed `_subscription.MakeChannels`. Belt and braces.
- **RMQ** — `RmqClassicMessageGatewayProvider.cs:91-109` and `:111-132`: the RMQ `ChannelFactory`
  does *not* provision (`src/Paramore.Brighter.MessagingGateway.RMQ.Async/ChannelFactory.cs:84-123`,
  same inert shape as ASB), so the provider compensates with the same guarded priming receive,
  commented *"Ensuring that the queue exists before return the channel"*.
- **GCP** — `GcpPullMessageGatewayProvider.cs:198-211`: no priming, because none is needed —
  `GcpPubSubChannelFactory.CreateAsyncChannelAsync` calls `await EnsureSubscriptionExistsAsync(...)`
  at `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubChannelFactory.cs:74`, *before*
  building the channel.

The field splits into "factory provisions eagerly" (AWS, GCP) and "provider primes with a short
receive" (RMQ, and AWS redundantly). **ASB does neither.** That is the whole defect, and it also
frames the two available fix shapes — choosing between them is `/bugfix:fix`'s job, and the choice is
exactly the open design question the issue raises.

**The queue path is probably NOT affected.**
`AzureServiceBusQueueMessageProducer.EnsureChannelExistsAsync`
(`AzureServiceBusQueueMessageProducer.cs:62-89`) creates the *same* queue entity the consumer later
reads from, so a send-before-receive still lands. It is the topic/subscription split that loses the
message. The test provider is topic-mode only (`CreatePublication` never sets `UseServiceBusQueue`),
so all 30 failures are topic-mode. `AzureServiceBusQueueConsumer` mirrors the topic consumer's shape
(`EnsureChannelAsync:89`, guards at `:91`/`:102`, `PurgeAsync:81` calling it at `:86`).

**Cross-references.** #4310 is genuinely masked by this bug: it can only fire on a message that was
actually received, and only two generated tests get that far. **Fixing #4309 will convert most of the
26 `MT_NONE` failures into #4310 failures until #4310 is also fixed.** The `SubCode=40900`
topic-creation race on the concurrent-post tests is a third defect with no tracking number yet.

## Confirmed Root Cause
_(left blank — filled by /bugfix:confirm)_

## Evidence
_(left blank — filled by /bugfix:confirm)_

## Scope Notes
_(left blank — filled by /bugfix:confirm)_

## Regression Test
_(left blank — filled by /bugfix:test)_

## Fix
_(left blank — filled by /bugfix:fix)_
