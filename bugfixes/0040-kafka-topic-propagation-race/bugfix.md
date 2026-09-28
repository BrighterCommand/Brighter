# Bugfix: Kafka consumer subscribes to a just-created topic before its metadata propagates; `ConsumeException` catch gives callers no way to tell the causes apart

**Linked Issue**: #4330

**Status**: Verified

## Symptom

**Observed (now latent):** Shape (1) shipped in `80532da85` and `d8bb21176`, and `kafka-ci` is no longer red. The race itself is still there; the test decorator now absorbs it. A generated gateway test makes a fresh topic, then creates a producer and a channel, sends, and receives. The first `Consume` on the new consumer can still throw `ConsumeException: Subscribed topic not available`. `RetryableChannelSync` / `RetryableChannelAsync` catch the resulting `ChannelFailureException` and poll again, so the test passes as long as a message arrives within the budget.

Two gaps remain:
1. **The race is not closed where it starts.** Neither the Kafka gateway nor the test providers wait for a newly created topic to show up in metadata before a consumer subscribes. Anything that calls `Receive` without a pump or the test decorator still sees the failure.
2. **Callers of `Receive` can't tell failures apart.** Every `ConsumeException` comes back as the same `ChannelFailureException("Error connecting to Kafka, see inner exception for details", ...)`. The caller gets nothing to separate "topic not propagated yet" (transient) from "topic really missing" (under `Assume`) from a fatal broker or consumer error, except by digging into the inner exception.

**Expected:**
- After `OnMissingChannel.Create`, a consumer subscribes only once the topic is visible in broker metadata, so the first `Receive` does not fail with "Subscribed topic not available".
- `Receive` either sorts `ConsumeException`s into transient and fatal, like the sibling `KafkaException` path, or exposes enough information for the caller to do it.

**Reproduction (intermittent, environmental):** Run any generated gateway test in the Kafka test project against a fresh broker. Call order in `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Classic/Generated/Reactor/When_posting_a_message_via_the_messaging_gateway_should_be_received.cs`:
- `CreatePublication(GetOrCreateRoutingKey())` at :44
- `CreateSubscription(..., Create)` at :45
- `CreateProducer` at :49
- `CreateChannel` at :50
- `Send` at :56
- `Receive(15000ms)` at :58

To see the failure again, swap `RetryableChannelSync` for the raw channel, or call the consumer directly.

## Suspected Location

**Product code: topic creation and subscription order**
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagingGateway.cs:57-75`: `EnsureTopic()`. Under `Create`, it calls `FindTopic()` and then `MakeTopic()` when the topic isn't found.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagingGateway.cs:77-104`: `MakeTopic()`. It returns as soon as `adminClient.CreateTopicsAsync(...)` (:84) completes, or on `TopicAlreadyExists` (:96-102). It never checks afterwards that the topic is visible in metadata.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagingGateway.cs:106-170`: `FindTopic()`. It already has a single-shot `adminClient.GetMetadata(Topic.Value, TopicFindTimeout)` probe (:115, :117-123), which is the obvious building block for a describe/poll loop.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:268-279`: the consumer constructor. It calls `_consumer.Subscribe([Topic.Value])` at **:269, before** it sets `MakeChannels`, `NumPartitions`, `ReplicationFactor` and `TopicFindTimeout` (:273-277) and before it calls `EnsureTopic()` at **:279**. So the consumer subscribes before its own create/validate step runs.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageProducer.cs:194-202`: `Init()` calls `EnsureTopic()` at :201. It is invoked from `KafkaMessageProducerFactory.cs:71`. In the generated tests the producer is built first (:49), so the producer's `MakeTopic` is what actually creates the topic, and the consumer subscribes right after.

**Product code: exception classification**
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:531-535`: the `catch (ConsumeException)`. It logs, then always throws `ChannelFailureException`. There is no `Error.IsFatal` or `Error.Code` check.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:537-544`: the sibling `catch (KafkaException)`. It does check `kafkaException.Error.IsFatal` (:540) and rethrows fatal errors (:541).
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:500-501`: the `_hasFatalError` latch. It is set by the error handler (`HandleError`, `IsFatal` check at :798) and gives a separate, handler-driven fatal path.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:565-587`: `ReceiveAsync` wraps the sync `Receive` (:574), so it inherits the same classification.

**Test harness: where shape (2) would hook in on the test side**
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaClassicMessageGatewayProvider.cs`:
  - `GetOrCreateRoutingKey` :265-268 (`gen.test.{Uuid}` at :267)
  - `CreateSubscription` :231-258
  - `CreateProducer` :184-196
  - `CreateChannel` :163-170, wraps in `RetryableChannelSync` at :169
  - `CreateChannelAsync` :172-182, wraps in `RetryableChannelAsync` at :181
  - The only `AdminClientBuilder` use in the file is in cleanup (:435, `DeleteTopics`). There is no wait or poll after creation.
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaConsumerMessageGatewayProvider.cs`: same shape. `CreateChannel` :163-170 (:169), `CreateChannelAsync` :172-182 (:181), `GetOrCreateRoutingKey` :268-271, cleanup `AdminClientBuilder` :438.
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaPartitionKeyMessageGatewayProvider.cs`: same shape. `CreateChannel` :171-178 (:177), `CreateChannelAsync` :180-190 (:189), `GetOrCreateRoutingKey` :273-276 (`gen.pk.test.{Uuid}` at :275), cleanup `AdminClientBuilder` :443.

**Shipped shape (1), confirmed to match comment 2**
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/RetryableChannelSync.cs:63-100`:
  - catches `ChannelFailureException` (:80)
  - captures it as `ExceptionDispatchInfo` (:82)
  - rethrows at once if the budget is gone (:84-85)
  - returns any non-`MT_NONE` message (:90-91)
  - otherwise, when the budget runs out, rethrows the held failure with `failure?.Throw()` (:97)

  In short, a failure is swallowed only if a message actually arrives.
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/RetryableChannelAsync.cs:84-101`: the same shape (catch at :84, capture at :86, `failure?.Throw()` at :101).
- The decorator already has a unit test: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_the_inner_channel_throws_a_transient_channel_failure_should_retry_within_the_remaining_timeout.cs`.

## Root-Cause Hypothesis

**(a) Why the race is still open at its source.** Two product-code facts cause it, and nothing in the harness offsets either one:

1. **`MakeTopic()` does not wait for propagation** (`KafkaMessagingGateway.cs:77-104`). `CreateTopicsAsync` completes when the controller accepts the request, not when the partition leader's metadata reaches the broker that serves the consumer's metadata requests. `EnsureTopic()` returns as soon as `MakeTopic()` does. In the generated tests the producer's `Init()` → `EnsureTopic()` (`KafkaMessageProducer.cs:201`) creates the topic. The channel is built next (test :50), and its consumer subscribes at once.
2. **The consumer subscribes before it runs its own `EnsureTopic()`** (`KafkaMessageConsumer.cs:269` vs `:279`). Even if `EnsureTopic()` did wait for propagation, the consumer constructor would already have called `Subscribe` before the wait. The wait would protect nothing on the consumer side. The fields `EnsureTopic()` needs (`MakeChannels`, `NumPartitions`, `ReplicationFactor`, `TopicFindTimeout`) are also assigned only after `Subscribe` (:273-277). So the order is: subscribe, then configure, then ensure. It should be: configure, ensure and wait, then subscribe.

The first `Consume` after that subscribe can therefore hit the broker before the topic exists in its metadata and fail with `UNKNOWN_TOPIC_OR_PART` ("Subscribed topic not available"). The harness adds nothing between `CreateProducer` and `CreateChannel` in any of the three providers.

**Falsifiable prediction for (a):** Add a `GetMetadata`/describe poll that waits until the topic shows up with its partitions and a leader. Put it either (i) in `MakeTopic()` after `CreateTopicsAsync`, with the consumer constructor reordered so `EnsureTopic()` runs before `Subscribe`, or (ii) in the test providers between producer/topic creation and `CreateChannel`. Then a raw, undecorated `Receive` in the generated tests should never throw `ConsumeException: Subscribed topic not available` across many repeated runs. If the exception still appears with the poll in place, the hypothesis is refuted: the failure is not about metadata propagation (it could be, for example, group coordinator or partition-assignment timing instead).

A narrower prediction, to check in /bugfix:confirm: reordering `KafkaMessageConsumer.cs:269` to after `:279` **on its own**, without a propagation wait, will **not** close the race. `MakeTopic` still returns before propagation, and under `Create` the consumer's `FindTopic` may see "not found" and call `MakeTopic` → `TopicAlreadyExists` → return immediately. Both changes are needed.

**(b) How the missing `Error.IsFatal` check relates to (a).** Hypothesis: **adding an `IsFatal` split to the `ConsumeException` catch is neither needed nor enough to close the race at its source.** It is a separate defect about what callers can diagnose.

- **Not enough:** "Subscribed topic not available" is `ErrorCode.UnknownTopicOrPart` with `IsFatal == false`. A topic that is genuinely missing under `OnMissingChannel.Assume` produces the same code and is also non-fatal. An `IsFatal` check therefore puts "not yet propagated" and "really missing" in the same bucket. Separating them needs `Error.Code` plus context (was the topic just created under `Create`?), not `IsFatal`. The only thing an `IsFatal` split separates is "broker or consumer is fatally broken" from the other two cases.
- **Not needed:** shape (2) closes the race *before* `Subscribe` by using the admin client, not by reading consume errors. A describe/poll loop never goes through the `ConsumeException` catch, so it does not depend on how that catch classifies errors.
- **How they do connect:** they are two halves of one design gap. The Kafka gateway has no notion of a transient "topic is still coming" state. `MakeTopic` doesn't model it, and at consume time it isn't reported separately from other `ConsumeException`s. Because the consumer can't classify, the test decorator has to treat every `ChannelFailureException` as maybe-transient. That forced the "only swallow if a message arrives" workaround in `RetryableChannelSync.cs:80-98`. There is also a structural detail: `ConsumeException` derives from `KafkaException`, so the `ConsumeException` catch at `:531` runs first for every consume error. That makes the `IsFatal` branch at `:540` unreachable for consume errors. Fatal consume errors are then caught only indirectly, through the error-handler latch (`:500-501`, `:798`), and only on the *next* `Receive`.

**Falsifiable prediction for (b):** Log `consumeException.Error.Code` and `Error.IsFatal` inside the `:531` catch during a forced reproduction (raw channel, no propagation wait). The expected result is `Code == UnknownTopicOrPart` and `IsFatal == false`. A consumer subscribed to a topic that really is missing under `Assume` should give the identical pair. If the two cases turn out to differ in `IsFatal`, then (b) is wrong and an `IsFatal` split alone *would* let callers tell them apart.

**Fixes suggested in the issue thread, restated.** All are **UNVERIFIED — to be proven or refuted in /bugfix:confirm**:
- Shape (2): after `OnMissingChannel.Create`, have the Kafka provider(s) run an admin-client describe/poll on the new topic before the consumer subscribes. This triage adds a note: if the fix goes in the product rather than the test providers, it also needs `KafkaMessageConsumer.cs:269` moved after `EnsureTopic()` at `:279`, or the wait won't cover the consumer's own subscription.
- Product-level: give the `ConsumeException` catch at `KafkaMessageConsumer.cs:531-535` the same `Error.IsFatal` check as the `KafkaException` catch at `:537-544`. Per (b), that alone won't let callers tell "not yet propagated" from "really missing". That would need `Error.Code`-based classification, or a dedicated exception or property on `ChannelFailureException`.

## Confirmed Root Cause

**CONFIRMED, with qualifications.** Both mechanisms in hypothesis (a) are real, and the (b) analysis holds, but two refinements matter for scoping the fix:

1. **`KafkaMessageConsumer` subscribes before it configures or ensures the topic.** `_consumer.Subscribe([Topic.Value])` runs at `KafkaMessageConsumer.cs:269`. `MakeChannels`, `NumPartitions`, `ReplicationFactor` and `TopicFindTimeout` are assigned at `:273-277`, and `EnsureTopic()` runs at `:279`. `Topic` itself is set earlier (`:143`), so the subscribe targets the right topic, just at the wrong time. librdkafka's `Subscribe` is asynchronous — it records the subscription and the group-join/metadata request happen on librdkafka's background thread, never failing synchronously. If the broker's metadata response doesn't include the topic, librdkafka queues a consumer error and `Consume` raises it on the next `Receive` as `ConsumeException("Subscribed topic not available: <t>: Broker: Unknown topic or partition")`.
2. **`MakeTopic()` returns without waiting for the topic to become visible** (`KafkaMessagingGateway.cs:77-104`). It returns when `CreateTopicsAsync` (`:84`) completes, or immediately on `TopicAlreadyExists` (`:94-102`, corrected from the triage's `:96-102`).
3. **Refinement to the mechanism:** CI and `docker-compose-kafka.yaml` run a single-node KRaft broker with combined broker/controller roles (same in the `kafka-ci` job, `.github/workflows/ci.yml:319-344`). So the window is not cross-broker propagation lag — it's the gap between the controller committing the topic record (when `CreateTopics` returns) and that same broker's own metadata image being updated. The window is real but smaller than "cluster propagation" implies; cross-broker lag would only widen it on a multi-broker deployment.
4. **(b) holds:** `ConsumeException` derives from `KafkaException`, so the catch at `:531` always wins first, and the `IsFatal` branch at `:540` is unreachable for consume errors. "Subscribed topic not available" carries `Error.Code == UnknownTopicOrPart`, `IsFatal == false` — reasoned from librdkafka/Confluent.Kafka error-model behavior (`CreatePossiblyFatalMessageError`, which sets `IsFatal=true` only for `Local_Fatal`), not read directly from source not available locally. The same `(Code, IsFatal)` pair is produced whether the topic is "not yet visible" or genuinely missing under `Assume`, so an `IsFatal` split alone cannot separate the two cases — confirming (b)'s conclusion that this is a related but distinct defect from the race itself.

**Where the triage's predictions were overstated:**
- The triage predicted reordering `Subscribe` after `EnsureTopic()` **alone**, without a propagation wait, would not close the race. This is **overstated**. On the single-node CI broker, the consumer's own `FindTopic` → `GetMetadata` call goes to the same broker as its later group-metadata request. Whenever `FindTopic` returns found, reordering alone *does* close the window. The race only survives reordering when `FindTopic` misses and falls into the `TopicAlreadyExists` early-return path in `MakeTopic`. So reordering shrinks the race substantially but does not eliminate it — both changes (reorder + wait) are still needed, just not for the reason originally stated.
- `FindTopic`'s "found" predicate (`KafkaMessagingGateway.cs:123`: `Error.Code != UnknownTopicOrPart`) is **too loose** to reuse as a readiness poll: a topic that exists but is still settling (e.g. `LeaderNotAvailable`, partitions with no leader) counts as "found" today and is only logged as a warning (`:127-150`). Any readiness poll built on `GetMetadata` needs a stricter predicate — `Error.Code == NoError`, correct partition count, every partition with `Leader >= 0`.

## Evidence

- [x] Code-trace (no red repro run — the race needs live broker infrastructure and is intermittent; this is the accepted infra-bound-bug path per the confirm criteria):
  1. Test flow `Classic/Generated/Reactor/When_posting_a_message_via_the_messaging_gateway_should_be_received.cs:44-58` confirmed: `CreateProducer` → `KafkaMessageProducerFactory.cs:68-71` → `KafkaMessageProducer.cs:201` `EnsureTopic()`, where `FindTopic` returns false so `MakeTopic` creates the topic with no visibility wait. `CreateChannel` (`KafkaClassicMessageGatewayProvider.cs:163-170`) → `ChannelFactory.cs:70` → `KafkaMessageConsumerFactory.cs:86` → the `KafkaMessageConsumer` constructor, which calls `Subscribe` at `:269` — starting the async group-metadata request before `EnsureTopic()` at `:279`. Nothing sits between producer creation and channel creation in any of the three providers.
  2. `FindTopic`'s `adminClient.GetMetadata(Topic.Value, TopicFindTimeout)` (`KafkaMessagingGateway.cs:115`) sends a fresh `MetadataRequest` to a broker — it does not answer from a local cache. A topic the broker doesn't know about comes back as an entry with `Error.Code == UnknownTopicOrPart`, which `FindTopic` reads correctly as not-found (`:123`). So a describe/poll loop built on `GetMetadata` behaves as the hypothesis assumed **on a single broker**: not-found until the broker's own metadata image has the topic, then found. On a multi-broker cluster the admin client may query a different broker than the consumer's group-metadata request does, making such a poll best-effort rather than a guarantee there — not the CI topology, but relevant to scoping a general-purpose fix.
  3. Receive classification confirmed unchanged: `:531-535` wraps every `ConsumeException` with no `Error.Code`/`IsFatal` check; `:537-544` is the only `IsFatal` check, for the sibling `KafkaException` catch. `_hasFatalError` latch at `:500-501`, set via `HandleError` at `:798`. `ReceiveAsync` (`:565-587`) wraps `Receive` at `:574` and inherits the same classification.
  4. librdkafka's consumer-group code maps both "missing from the metadata response" and a broker-reported `UNKNOWN_TOPIC_OR_PART` to the same `UNKNOWN_TOPIC_OR_PART` consumer error — reasoned from documented librdkafka behavior, not read from source unavailable locally. Nothing in the repo currently asserts this; the only occurrences of the code are `KafkaMessagingGateway.cs:123` and a stub string in `When_the_inner_channel_throws_a_transient_channel_failure_should_retry_within_the_remaining_timeout.cs:164,202`.
  5. librdkafka reports the "subscribed topic not available" error once per subscribed topic; later polls return empty (`MT_NONE`) until a metadata refresh picks the topic up — matching the comment already in `RetryableChannelSync.cs:39-44`. The raw failure is one-shot; recovery speed depends on librdkafka's internal metadata-refresh interval, not on anything Brighter controls today.
  6. All triage `file:line` references re-verified against HEAD and are current, with one small drift: `MakeTopic`'s `catch (CreateTopicsException)` starts at `:94` (not `:96` as triage said); the `TopicAlreadyExists` check is at `:96` and the log at `:102`.

## Scope Notes

Fixing `KafkaMessageConsumer`'s constructor covers every production path — `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumerFactory.cs:86` is the only place in `src/` that constructs it (reached via `ChannelFactory.cs:70,89,109`).

**Additional call sites that inherit the wait/reorder fix and should be covered by the regression test or explicitly excluded:**
- `KafkaMessageProducer.cs:194-202` `Init()`, reached from `KafkaMessageProducerFactory.cs:68-71` / `KafkaProducerRegistryFactory.cs:65`.
- `KafkaMessageConsumer.cs:1038-1061` `CreateProducer`, used to build the DLQ, invalid-message and requeue producers (inherits `MakeChannels`). These are lazy (`:144-154`) and `CreateProducer` swallows exceptions — a poll timeout here would silently return null rather than surface, which the fix must not make worse.
- All of the above gain up to `TopicFindTimeout` (default 10s, `:170`) of extra construction latency for a brand-new topic once a wait is added — worth flagging as an intentional latency/reliability tradeoff, not a regression.

**Production case not covered by a test-provider-only fix (variant (ii) in the original hypothesis):** a consumer started with `OnMissingChannel.Create` and *no* producer creates the topic itself, after `Subscribe` (`:269` then `:279`) — so its first group-metadata request is near-deterministically going to miss. This has not been reproduced directly but follows from the traced construction order. **Only a product-side fix (in `KafkaMessagingGateway`/`KafkaMessageConsumer`) covers this case; a test-harness-only fix (providers only) would not.** This resolves the "shape 2 test-side vs. product-side" ambiguity in the original issue thread in favor of the product-side location.

**A second place in the test harness already absorbs (masks) this same race, not listed in triage:** `ReceiveOne`/`ReceiveOneAsync` swallow `ChannelFailureException` and return `Message.Empty` at `KafkaClassicMessageGatewayProvider.cs:350-379`, `KafkaConsumerMessageGatewayProvider.cs:353-382`, `KafkaPartitionKeyMessageGatewayProvider.cs:358-387`. These back `GetMessageFromDeadLetterQueue`/`GetMessageFromInvalidChannel`, whose consumers are created against DLQ topics a lazy producer created moments earlier — masking both the propagation race and genuine failures in the DLQ/invalid-channel tests. Not in scope for this fix but worth a follow-up issue.

**`Assume` is untouched by design** (`EnsureTopic` returns immediately at `:59-60` under `Assume`) — callers under `Assume` still need error classification to distinguish "really missing" from other causes; per (b), this stays a separate defect from the propagation-wait fix and is not closed by it.

**Cross-backend parity:** not investigated beyond Kafka; none found in scope for this bug.

**Minor operational note:** if a deployment sets `auto.create.topics.enable=true`, repeated `GetMetadata` polling for a named topic could auto-create it with broker defaults — already a latent risk in today's single-shot `FindTopic` call, and would apply more often with a poll loop. CI disables this (`KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"`), so it does not affect this bug's tests, but the fix's poll loop should not make the exposure worse for users who do have it enabled.

## Regression Test

**File**: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_a_consumer_subscribes_right_after_the_producer_creates_the_topic_should_not_throw_subscribed_topic_not_available.cs`
**Class**: `KafkaTopicPropagationRaceTests`
**Method**: `When_a_consumer_subscribes_right_after_the_producer_creates_the_topic_should_not_throw_subscribed_topic_not_available`

Runs 60 concurrent attempts (`MaxDegreeOfParallelism = 8`), each: producer creates a fresh unique
topic under `OnMissingChannel.Create`, a consumer subscribes to it immediately after via a **raw**
channel built directly through `ChannelFactory`/`KafkaMessageConsumerFactory` (bypassing
`RetryableChannelSync`, which is what makes the generated gateway tests tolerate this failure).
Asserts zero `ChannelFailureException` across all attempts.

**Testability constraint (confirmed at /bugfix:test):** `KafkaMessageConsumer`/`KafkaMessagingGateway`
have no injectable seam for `IConsumer`/`IAdminClient` — both are built internally via
`ConsumerBuilder`/`AdminClientBuilder`. A live broker is required; there is no broker-free unit-test
path for the ordering defect itself (introducing one would mean adding new testability surface as
part of the fix, which was explicitly ruled out of scope for this bug).

**RED evidence:** A plain sequential loop (attempted first, matching the originally-approved
strategy) did not reproduce the race locally across 100+ cumulative attempts, including at up to 200
concurrent attempts with 32-way parallelism, on an unthrottled local broker — the several broker
round-trips already inside `EnsureTopic`/`MakeTopic` happen to outlast the propagation window on a
fast, idle machine. RED was reliably obtained by throttling the local broker's CPU
(`docker update --cpus=0.25 kafka`, simulating a loaded/shared CI runner) under the same 60-attempt/
8-way-concurrent load: 3 separate runs, 2 of which failed with the **exact reported symptom**:

```
1/60 (then 3/60) attempts raced a just-created topic: Error connecting to Kafka, see inner exception for details
| inner: ConsumeException: Subscribed topic not available: gen.race.test.<uuid>: Broker: Unknown topic or partition
```

This is the identical exception shape from issue #4330's own CI sightings. The broker's CPU
allocation was restored (`docker update --cpus=4 kafka`) after confirming RED. The committed test
runs unthrottled — expected to rarely fail on a fast/idle CI runner and to catch the regression on a
loaded one, consistent with the bug's own observed CI frequency (4 sightings, not a constant
failure).

**Full-suite check:** `dotnet test tests/Paramore.Brighter.Kafka.Tests` — 240/240 passed (unthrottled),
no regressions.

**Approved** via the `/test-first` approval gate.

## Fix

**Files changed:**
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagingGateway.cs`
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs`

**Summary:** Two minimal changes, matching the two mechanisms in the Confirmed Root Cause:

1. **`KafkaMessageConsumer`'s constructor** now assigns `MakeChannels`/`Topic`/`NumPartitions`/
   `ReplicationFactor`/`TopicFindTimeout` and calls `EnsureTopic()` **before** building the consumer
   and calling `Subscribe`, instead of after. `EnsureTopic()`'s work (including the new wait, below)
   now actually runs ahead of the subscribe it exists to protect.
2. **`KafkaMessagingGateway.MakeTopic()`** now waits, after either successfully creating the topic
   or catching `TopicAlreadyExists`, for the topic to become visible in the broker's own metadata,
   via a new private `WaitForTopicVisible(IAdminClient)` method: it polls `GetMetadata` (500ms
   per-call timeout) in a loop bounded by the existing `TopicFindTimeout` budget, with a 50ms delay
   between polls, until the topic reports `Error.Code == NoError`, the expected partition count, and
   every partition has an assigned leader (`Leader >= 0`) — deliberately stricter than `FindTopic`'s
   own "found" predicate, per the Confirmed Root Cause's note that `FindTopic`'s predicate is too
   loose to reuse for readiness. The wait is unconditional after the try/catch, so it covers both
   the "this call created the topic" and "topic already existed" (`TopicAlreadyExists`) paths, as
   required. On timeout it returns without throwing (best-effort; strictly better than today's no
   wait at all, and does not turn a previously-succeeding path into a newly-throwing one).

**Explicitly not changed (per Scope Notes, out of scope for this bug):**
- The `ConsumeException`/`Error.IsFatal` classification gap in `KafkaMessageConsumer.cs:531-535` —
  confirmed to be a separate defect that doesn't help close this race (an `IsFatal` split can't tell
  "not yet propagated" from "really missing" — both are `UnknownTopicOrPart`, `IsFatal == false`).
- The `ReceiveOne`/`ReceiveOneAsync` masking sites in the three test gateway providers (DLQ/invalid-
  channel test helpers) — a separate, pre-existing latent issue, not part of this bug's fix.
- `OnMissingChannel.Assume` — untouched by design; `EnsureTopic()` returns immediately for it.

**Verification:**
- Regression test (`KafkaTopicPropagationRaceTests`) passes unthrottled (expected — it also passed
  unthrottled before the fix, since the race needs load/contention to manifest locally).
- **Fix proven** by rerunning the exact same test under the exact same throttled-broker conditions
  that reproduced RED before the fix (`docker update --cpus=0.25 kafka`, 60 concurrent attempts,
  8-way parallelism): **5/5 runs green** post-fix, versus 2/3 runs red pre-fix with the reported
  `ConsumeException: Subscribed topic not available` failure. Broker CPU restored
  (`docker update --cpus=4 kafka`) after verification.
- Full Kafka suite run pending final confirmation (`/bugfix:verify` will re-run it); the fix compiles
  cleanly (`dotnet build` on `Paramore.Brighter.MessagingGateway.Kafka`, 0 errors).
