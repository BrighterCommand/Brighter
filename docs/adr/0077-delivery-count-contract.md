---
id: 0077-delivery-count-contract
title: "Delivery Count Contract"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-09-24
summary: "SQS, Pub/Sub and RocketMQ consumers read the broker's own delivery counter on receive and subtract 1 so a first delivery presents 0, keeping the stamped handled count on a Brighter-routed rejection copy (one carrying the rejectionReason metadata key); the pump stays the only budget enforcer, budget findings are three lower-rung ISpecification<Subscription> rules plus a channel-creation log, and GCP and RocketMQ keep pre-registered conditional branches."
tags:
  - "transports"
  - "retry"
  - "dead-letter-queue"
  - "message-pump"
---

# 77. Delivery Count Contract

Date: 2026-09-24

## Status

Proposed

## Context

**Parent Requirement**: [specs/0037-delivery-count-and-rejection-routing/requirements.md](../../specs/0037-delivery-count-and-rejection-routing/requirements.md)

**Scope**: This ADR decides the cross-transport delivery-count contract — Groups A–C and F–H (R-1 to R-15, R-22 to R-28) and the NFRs they touch — and how `AWSSQS` and `AWSSQS.V4` (kept in lockstep, `0038-aws-sqs-dlq-direct-send`), `GcpPubSub` (pull and stream consumers) and `RocketMQ` are bound to it. GCP rejection routing and DLQ channel creation without project IAM admin (R-16 to R-21, Groups D–E) belong to **ADR 0078, "GCP rejection routing and DLQ channel creation" ** (`0078-gcp-rejection-routing-and-dlq-channel-creation`, Proposed). R-15 is shared: this ADR relies only on its outcome (`GcpPubSubSubscription` exposes a dead-letter routing key); 0078 decides how.

**The problem in one line.** The pump enforces `RequeueCount` by `UpdateHandledCount()` then `HandledCountReached(RequeueCount)` (`Reactor.cs:494-507`, `Proactor.cs:500-513`), which only runs down if a redelivered message presents a larger `HandledCount`. SQS, Pub/Sub and RocketMQ re-serve their own stored copy, which Brighter never rewrites, so on 13 configurations the budget is inert.

**Settled inputs taken as given:** one spec (C-2); republish-on-requeue excluded (NFR-3, C-12); `0038-aws-sqs-dlq-direct-send`'s DLQ strategy stands (C-1); RocketMQ is conditional (R-14, C-5); budget findings go through `ValidatePipelines` (R-25/R-26); the GCP bar is the Pub/Sub emulator (R-21, C-4), A-1 verified, A-2/A-3 not.

## Decision

### Architecture Overview

**On receive, each consumer reads the broker's own delivery counter and normalises it so that a first delivery presents `0` (R-2) — except when the received message is a Brighter-routed rejection copy, in which case the consumer keeps the handled count stamped in the message (R-28).** A message is a routed copy when its `Header.Bag` carries the `rejectionReason` metadata key. A message put back on its source with that metadata still attached is therefore treated as a routed copy and rejected again. Stripping the metadata is the replaying tool's responsibility (edge case 2). No broker call is added, no state is kept, and the pump is unchanged: it remains the only budget enforcer.

```
broker receive ──► transport creator/parser (per-transport seam)
                     │ knows: raw broker counter, header-carried count, Header.Bag
                     ▼
                   DeliveryCount.Resolve(headerCount, brokerCount, bag)   ◄── core, decides the source
                     ▼
                   Message.Header.HandledCount ──► pump: UpdateHandledCount(); HandledCountReached(R) ──► Reject(DeliveryError)
                                                    (enforces; unchanged)
```

**Why the budget fires on delivery `R` exactly (R-4).** Broker counter on delivery *n* is `b(n) ≥ n`, with `b(1) = 1` as the origin. The consumer presents `c(n) = b(n) − 1`; the pump increments to `c(n) + 1 = b(n)` and tests `b(n) ≥ R`.

| delivery *n* | broker `b` (exact) | presented `c` | after `UpdateHandledCount` | `≥ R` for `R = 3`? |
|---|---|---|---|---|
| 1 | 1 | 0 | 1 | no → requeue |
| 2 | 2 | 1 | 2 | no → requeue |
| 3 | 3 | 2 | 3 | **yes → `Reject(DeliveryError)`** |

Exact counter: rejection on delivery `R`, after `R − 1` requeues. Approximate (`b(n) ≥ n`): on or before delivery `R` — R-4's "at most `R`". `R = 1`: `0 → 1 ≥ 1`, rejected on first delivery. `R = 0` or `R < −1`: `1 ≥ R` true, first deferral rejects (R-7). `R = −1`: budget never consulted (`MessagePump.cs:171`, R-6).

**What the dead-letter copy carries (R-5, R-28).** Brighter sends the in-memory header, whose count is `c(R) + 1 = b(R) ≥ R`; the senders serialise it (SQS `SqsMessageSender.cs:130` / `SnsMessagePublisher.cs:110`, RocketMQ `RocketMqMessagePublisher.cs:103`, GCP `Parser.cs:307`). The copy also carries `rejectionReason` (`SqsMessageConsumer.cs:508`, `RocketMessageConsumer.cs:256`), so on a DLQ read the discriminator keeps the stamped count (`≥ R`; exactly `R` on an exact counter), not the DLQ's own counter, which would normalise to `0`.

### Key Components

Roles (Responsibility-Driven Design):

| Role | Object | Knowing | Doing | Deciding |
|---|---|---|---|---|
| **Counter reader** (per transport) | `SqsMessageCreator` / `SqsInlineMessageCreator` (both packages), `GcpPubSub/Parser`, `RocketMessageConsumer.CreateMessage` | raw broker counter, header-carried count, bag | extracts the inputs without allocating | nothing — delegates |
| **Count resolver** | new `Paramore.Brighter.DeliveryCount` (static, core) | the normalisation rule and the discriminator | `Normalise`, `Resolve` | which count source wins |
| **Metadata vocabulary** | new `Paramore.Brighter.RejectionMetadataKeyNames` (static, core) | the five rejection-metadata key names (`"rejectionReason"`, `"originalTopic"`, …) | — | — |
| **Budget capability** | new core role interface `IAmADeliveryCountingSubscription`, implemented by `SqsSubscription` ×2, `GcpPubSubSubscription`, `RocketSubscription` | `NativeRedriveLimit` (M), `DeliveryBudgetUnenforceableReason` (R-11's predicate) | — | whether this subscription can advance its count |
| **Budget rules** | three `ISpecification<Subscription>` rules in `ConsumerValidationRules` | — | report Warnings via `ValidatePipelines` | whether R-7 / R-10 / R-11 fires |
| **Channel-creation diagnostic** | new core `DeliveryBudgetDiagnostics.WarnIfUnenforceable(Subscription)`, called by each in-scope channel factory | — | logs R-11 once per channel | — |
| **Budget enforcer** | `Reactor` / `Proactor` | — | `UpdateHandledCount`, `HandledCountReached`, `RejectMessage` | **unchanged** |

**Why `DeliveryCount` is a shared core helper, not four copies.** The rule must be identical across four transports and two AWS packages (NFR-6); a copy in `AWSSQS` and another in `AWSSQS.V4` is exactly the divergence NFR-6 calls a defect. Every in-scope assembly already references `Paramore.Brighter`, and the helper is pure arithmetic plus one dictionary lookup. The per-transport seams still decide *where the raw value comes from* — broker knowledge core must not have.

**`DeliveryCount` contract:**

```csharp
public static class DeliveryCount
{
    // R-2/R-3/AC-33: 1→0, 2→1, 3→2; null, 0 or negative → 0; never negative.
    public static int Normalise(int? brokerCount) => brokerCount is > 1 ? brokerCount.Value - 1 : 0;

    // R-28: routed rejection copy → stamped count; broker counter unavailable → header count
    // (today's behaviour); otherwise → normalised broker counter.
    public static int Resolve(int headerCount, int? brokerCount, IDictionary<string, object> bag)
        => bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason) || brokerCount is null or < 1
            ? headerCount
            : Normalise(brokerCount);
}
```

No copy of the bag is made (`MessageHeader.Bag` is a `Dictionary<string, object>`, `MessageHeader.cs:162`).

**Error conditions** (none logs per message — NFR-2 forbids the allocation, NFR-4 the per-message noise):

| Condition | Presented count | Logged |
|---|---|---|
| SQS `ApproximateReceiveCount` absent (`Attributes` null on V4, or key missing) or unparseable | header count (today's behaviour) | nothing |
| Pub/Sub `DeliveryAttempt == 0` / `GetDeliveryAttempt == null` (no `DeadLetterPolicy`, A-1) | header count (normally `0`) | R-11 Warning once at channel creation (R-26) |
| RocketMQ `DeliveryAttempt <= 0` | header count | R-11 at channel creation only on the AC-25 branch |
| Routed rejection copy | stamped header count | nothing |

#### Where each transport reads its counter

| Transport | Raw counter | Seam | Note |
|---|---|---|---|
| `AWSSQS` | `sqsMessage.Attributes["ApproximateReceiveCount"]`, already on the wire (`MessageSystemAttributeNames = ["All"]`, `SqsMessageConsumer.cs:188-194`) | `SqsMessageCreator.ReadHandledCount` (`:323`, called `:80`, bag read `:70`); `SqsInlineMessageCreator.ReadHandledCount` (`:350`, called `:60`) | The inline creator reads the handled count **before** the bag and topic (`:60` vs `:76`); the call order is swapped so `Resolve` can see them. |
| `AWSSQS.V4` | same attribute; `Attributes` may be null in SDK v4 | `SqsMessageCreator.ReadHandledCount` (`:330`, called `:77`); `SqsInlineMessageCreator.ReadHandledCount` (`:317`, called `:60`) | lockstep twin; same call-order swap |
| `GcpPubSub` pull | `ReceivedMessage.DeliveryAttempt` (`int`, 0 without a policy) | `Parser.ToBrighterMessage(ReceivedMessage)` (`~:84`; `ReadHandleCount` `:167`) | `Resolve` runs after the bag is filled from attributes |
| `GcpPubSub` stream | `PubsubExtensions.GetDeliveryAttempt(PubsubMessage)` (`int?`) on `GcpStreamMessage.Message` (`GcpStreamConsumer.cs:93`) | `Parser.ToBrighterMessage(GcpStreamMessage)` (`:33`) | The library carries the value as attribute `googclient_deliveryattempt`. **Decided here:** add that key to `Parser.s_ignoreHeaders` so it never enters `Header.Bag` — otherwise it would be re-published on a routed copy (0078) and read back stale. |
| `RocketMQ` | `MessageView.DeliveryAttempt` | `RocketMessageConsumer.ReadHandledCount` (`:422`, called `:292`) | implemented only on R-14's AC-24 branch (below) |

**Why the discriminator is `"rejectionReason"`.** `"rejectionReason"` is the key every Brighter-managed route stamps, spelled identically (`SqsMessageConsumer.cs:508`, V4 `:501`, `RocketMessageConsumer.cs:256`, and the Redis, Postgres, MsSql and MQTT consumers); bag keys survive the wire verbatim and case-sensitive (`JsonSerialisationOptions.cs:11-14`; `DictionaryStringObjectJsonConverter.cs:22`). It is deliberately **not** the pump's `Message.RejectionReasonHeaderName = "RejectionReason"` (`Message.cs:51`, stamped at `Reactor.cs:426`), which is written only on pump-driven rejections — a direct `channel.Reject(...)`, as conformance FR-4/5/6/8/17 use, never carries it. `RejectionMetadataKeyNames` gives the literals one name; the in-scope `RefreshMetadata` methods adopt the constants in a separate structural commit (Tidy First).

**A null-reason `Reject` stamps `rejectionReason = "None"`.** Today `RefreshMetadata` returns before stamping `rejectionReason` when `reason == null` (`SqsMessageConsumer.cs:506`, `RocketMessageConsumer.cs:254`), so a `channel.Reject(message, null)` copy carries no discriminator and a DLQ read would present the DLQ's own count. **Decided:** on the in-scope transports (`AWSSQS`, `AWSSQS.V4`, `RocketMQ`, and GCP via 0078), `RefreshMetadata` stamps `rejectionReason = RejectionReason.None.ToString()` when `reason` is null — consistent with the route `DetermineRejectionRoute` already selects for a null reason (`reason?.RejectionReason ?? RejectionReason.None`, `SqsMessageConsumer.cs:275`). `rejectionMessage` stays absent. This is an additive refinement of `0047-message-rejection-routing-strategy`'s metadata on the in-scope transports only; the other Brighter-managed transports are not changed here.

#### R-28 per transport (AC-41's table)

| Transport | How R-28 is satisfied | Discriminator |
|---|---|---|
| `AWSSQS` | The DLQ copy carries `rejectionReason` and `handled-count` (`SqsMessageSender.cs:130`); `Resolve` keeps the stamped count; the DLQ's own `ApproximateReceiveCount` is ignored. | `rejectionReason` present in `Header.Bag` |
| `AWSSQS.V4` | identical | same |
| `GcpPubSub` | same rule; the routed copy's metadata and `HandledCount` attribute (`Parser.cs:307`) are produced by 0078's routing, which must stamp `RejectionMetadataKeyNames` (constraint on 0078). | same |
| `RocketMQ` | DLQ copy carries `rejectionReason` (`:256`) and `HandledCount` (`RocketMqMessagePublisher.cs:103`). On the AC-25 branch the receive path is unchanged, so the header count is what is read already. | same (AC-24); not needed (AC-25) |

Native dead-lettering (R-9) carries no `rejectionReason`, so the redrive target presents its own normalised count; R-28 does not bind that case and AC-9 asserts no count.

**Discriminator edge cases:**
1. **Null-reason `Reject`** — covered by stamping `"None"` (above).
2. **Put back on its source with metadata intact** (a DLQ redrive, a replay tool, a manual move — all outside Brighter's control). The message still carries `rejectionReason`, so it is treated as a routed copy: its stamped count `S` is kept, and since `S ≥ R` for the budget that dead-lettered it, the pump rejects it again on its first delivery (whenever `R ≤ S + 1`). This is the intended behaviour, not a defect: **putting a message back for another attempt means resetting it, and that is the responsibility of the tool that puts it back** — it must remove the rejection metadata (`rejectionReason` and the other `RejectionMetadataKeyNames`) and may reset `HandledCount`. Brighter documents this (dead-letter documentation and release notes). If the source's budget is larger than the one that dead-lettered it (`R > S + 1`), the count stays at `S` and only a native limit bounds the message; also documented.
3. **User-supplied `rejectionReason` bag key on an ordinary message** — treated as a routed copy, as in case 2. The keys are Brighter-reserved (`0047-message-rejection-routing-strategy`); release notes say so. Accepted.
4. **R-17's no-destination case** — nothing is routed; not applicable.
5. **Header-carried count on source channels** — where a broker counter is available, the header count is now ignored on source-channel deliveries: a producer that sends `HandledCount = N` sees `0` presented on first delivery. Follows from R-2; recorded as a negative consequence.

#### Conformance oracle change for the redelivery arms (R-1 vs R-23)

R-2 protects the identity assertion on a *first* delivery only. The FR-2, FR-15, FR-16 and FR-22 templates also assert identity on the **redelivered** message (`_messageAssertion.Assert(message, redelivered)` — e.g. `Templates/MessagingGateway/Reactor/When_requeuing_a_failed_message_should_be_redelivered.cs.liquid:77`, `…_with_zero_delay…:86`, `…_with_delay…:85`, `When_nacking_a_message_it_should_be_redelivered.cs.liquid:81`, and the Proactor twins), and the assertions compare `HandledCount` for equality (`AwsMessageAssertion.cs:58` in both AWS test projects, `RocketMqMessageAssertion.cs:74`, `DefaultMessageAssertion.cs.liquid:59`). R-1 *requires* that redelivered count to be `≥ 1`, so without a change today's AWS `Pass` cells (and RocketMQ FR-16/FR-22 on AC-24) would fail, breaking R-23.

**Decided:** in those four behaviours' redelivery arms only, both pump variants, the generated test asserts `redelivered.Header.HandledCount >= message.Header.HandledCount`, then sets `redelivered.Header.HandledCount = message.Header.HandledCount` before calling the transport's message assertion. The transport assertion classes are untouched, first-delivery arms keep exact equality (R-2 still bites), and the change holds on all 24 configurations (republishing transports present `0 ≥ 0`). It is a change to the conformance oracle under `0067-conformance-rollout-and-deferral-governance`, made in the templates and regenerated, never hand-edited. The requirements record this in R-23 and C-7.

### Technology Choices

#### Counter classification (R-3, AC-34's table)

| Transport | Classification | Evidence |
|---|---|---|
| `AWSSQS` | **approximate** | attribute is `ApproximateReceiveCount`; AWS documents it approximate (A-4) |
| `AWSSQS.V4` | **approximate** | same attribute |
| `GcpPubSub` | **approximate** | Google.Cloud.PubSub.V1 3.36.0 XML doc, `ReceivedMessage.DeliveryAttempt`: "calculated at best effort and is approximate … If a DeadLetterPolicy is not set … this will be 0" (A-1, verified) |
| `RocketMQ` | **approximate** (conservative) | no vendor statement of exactness found for `MessageView.DeliveryAttempt` in RocketMQ.Client 5.2.1; absent evidence, approximate. AC-23 may reclassify by amendment. |

Consequence: no transport is exact, so AC-34's second clause and AC-41's "exactly `3`" clause bind none; AC-13 compares v3/v4 bounds (`≤ R`), not equality.

#### First delivery on an approximate counter (R-2, AC-33)

`Normalise` maps the documented origin `1` to `0`. **If a broker reports `> 1` on what the test regards as a first delivery, no mechanism within NFR-1 to NFR-3 can reach `0`** — an SQS receive whose response was lost is a real, counted receive; a Pub/Sub over-count is indistinguishable from a real redelivery.

**Residual risk against C-7:** an occasional, non-systematic first delivery presenting `≥ 1` (needs a lost response or a best-effort over-count). Exposed cells: every in-scope configuration whose template calls `_messageAssertion.Assert` on a first delivery — all 8 AWS/AWS.V4 configurations on FR-2/15/16/22's first-receive arms and other `Pass` AWS behaviours asserting identity on a first receive; `RocketMQ` FR-16/FR-22 on AC-24. It surfaces as a flaky, not systematic, failure; the mitigation is to report, not mask: observed failures are recorded against this ADR.

#### R-13 (GCP): the branch rule

Mechanism: the broker counter above. Input: AC-39's measurement (the selector is this ADR's amended conclusion under the rule below), run on the emulator once 0078/R-20 make DLQ-backed channels creatable; this ADR is then amended with a dated *Measurement outcome* entry (AC-39(b)/(c)).

- **A-2 holds** (populated, strictly increasing, first value `1`): the mechanism satisfies R-1 to R-5 within NFR-1 to NFR-3. **AC-19 claimed; AC-40 not applicable.** The first-delivery residual risk applies as on SQS.
- **A-2 refuted, or populated but not strictly increasing, or first value `> 1`:** the `delivery_attempt`-independent search below has been run and found nothing. **AC-40 claimed; AC-19 not applicable.** GCP is *bound but unimplemented*; `GcpPubSubSubscription.DeliveryBudgetUnenforceableReason` becomes non-null for every subscription; the four `GCP / *` FR-23 cells stay `Deferred`, re-pointed at the emulator limitation.

**The `delivery_attempt`-independent search (R-13, A-2), run in advance:**

| Candidate | Verdict |
|---|---|
| Header-carried `HandledCount` | never rewritten on `ModifyAckDeadline`/Nack — cannot advance (the defect itself) |
| Republish on requeue | excluded (C-12, NFR-3) |
| `GetSubscription` / any per-message lookup | excluded (NFR-1) |
| Consumer-side in-memory map | rejected (Alternatives) |
| Data encoded in `AckId` | opaque, undocumented — not a contract |
| **Conclusion** | none fits; a refuted A-2 selects AC-40 |

#### GCP stream consumer lease-lapse procedure for AC-42 (first branch only)

Both `GCP / Stream` and `GCP / StreamOrdering`, emulator, both variants (NFR-8), through `GcpPubSubStreamMessageConsumer`.

- **Configuration:** `DeadLetterPolicy { MaxDeliveryAttempts = 5 }`; subscription `AckDeadlineSeconds = 10`; `bufferSize: 2`, `noOfPerformers: 1` (the factory derives the flow-control cap from `BufferSize × NoOfPerformers`, `GcpPubSubConsumerFactory.cs:88-91`, so a second slot exists while the first delivery is held); `StreamingConfiguration = b => b.Settings = new SubscriberClient.Settings { MaxTotalAckExtension = TimeSpan.FromSeconds(10) }` (survives because the hook runs before `builder.Settings ??= …`, `GcpPubSubConsumerFactory.cs:110-121`).
- **Sequence:** (1) publish with `HandledCount = 0`; (2) `Receive`/`ReceiveAsync` returns m1 — record its count, do not ack, nack or requeue; (3) poll `Receive` every 500 ms for up to 45 s (lease extension stops at 10 s, lapses ~20 s); (4) assert the redelivery m2 presents a count strictly greater than m1's; (5) only then `Acknowledge` m2 and m1, so the `WaitForProcessing` shutdown (`GcpStreamConsumer.cs:49`) completes.
- **Alternative test for `StreamOrdering`** if same-key serialisation blocks the primary (unverified, R-13): same procedure on the same ordering-enabled subscription with a message published **without** an ordering key. Ordered delivery withholds a same-key redelivery while its predecessor is outstanding by design, so "hold, then observe the lapse" is not drivable with a key; a keyless message still exercises the stream consumer's lease-lapse path on that configuration. The switch, if taken, is recorded in the ledger.
- **On the AC-40 branch** this obligation lapses (R-13).

#### RocketMQ conditional (R-14, AC-23..AC-25)

**Measurement (AC-23):** from a clean store (`docker-compose -f docker-compose-rocketmq.yaml down -v; up -d`), `requeueCount: 3`, a deferring handler, Reactor variant. The test reads the raw `MessageView.DeliveryAttempt` from `message.Header.Bag["ReceiptHandle"]`, which is the `MessageView` (`RocketMessageConsumer.cs:181`) — no production change needed to observe it. Three deliveries across 10 s invisibility lapses, no `ChangeInvisibleDuration` call (commented out, `:186-187`). **Dispose the consumer and create a fresh one between deliveries 2 and 3**: the 5.2.1 assembly contains a client-side `IncrementAndGetDeliveryAttempt`, and a value that survives a fresh client proves the increment is broker-supplied, as R-14's condition requires.

- **Condition holds (AC-24):** `ReadHandledCount` uses `DeliveryCount.Resolve(header, view.DeliveryAttempt, bag)`; `Requeue` stays a broker no-op; `DeliveryBudgetUnenforceableReason` is null; the FR-23 cell moves to `Fixed`.
- **Condition fails (AC-25):** receive path unchanged; `RocketSubscription.DeliveryBudgetUnenforceableReason` returns fixed text naming the upstream `ChangeInvisibleDuration` blocker; the cell stays `Deferred`, re-pointed; #4353 and the ledger record the three values. AC-38 protects the nine `Fixed` cells on both branches.

#### Budget rules: which rung (R-25)

All three rules take the **lower rung** — `ISpecification<Subscription>`, registered alongside the existing four in `RegisterConsumerValidationSpecs` (`ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:199-215`) and harvested at `BrighterPipelineValidationExtensions.cs:79`. **No dependency on #4282.**

```csharp
public interface IAmADeliveryCountingSubscription
{
    int? NativeRedriveLimit { get; }                    // M; null when none configured or not visible to Brighter
    string? DeliveryBudgetUnenforceableReason { get; }  // null when R-1 holds for this subscription
}
```

- **R-7:** `RequeueCount == 0 || RequeueCount < -1` → Warning naming the subscription, the value, and the likely intents `-1` and `1`.
- **R-10:** `R != -1 && NativeRedriveLimit is int m && R >= m` → Warning naming `R`, `M`, and that the effective limit is `M`.
- **R-11:** `R != -1 && DeliveryBudgetUnenforceableReason is not null` → Warning naming the subscription, `R`, and the reason.
- Subscriptions not implementing the interface pass R-10/R-11 vacuously — including the nine republishing configurations, so R-22 is untouched. Severity `ValidationSeverity.Warning`, per the `RequestTypeSubtype` precedent (`ConsumerValidationRules.cs:114-123`).

| Subscription | `NativeRedriveLimit` | Shape that trips R-11 (AC-11) |
|---|---|---|
| `SqsSubscription` (both packages) | `QueueAttributes.RedrivePolicy?.MaxReceiveCount` (`SqsSubscription.cs:72`, `SqsAttributes.cs:110`, `RedrivePolicy.cs:34`) | **none** — `ApproximateReceiveCount` is always returned. A redrive policy configured outside Brighter is invisible, so R-10 cannot fire for it. |
| `GcpPubSubSubscription` | `DeadLetter?.MaxDeliveryAttempts` (`GcpPubSubSubscription.cs:75`, `DeadLetterPolicy.cs:47`) | `DeadLetter == null` with `R != -1` (A-1); on the AC-40 branch every subscription with `R != -1` |
| `RocketSubscription` (`RocketMqSubscription.cs:10`) | `null` — the broker's max-retry setting lives server-side on the consumer group, invisible to the client | none on AC-24; every `R != -1` on AC-25 |

AC-11's first branch applies, since GCP has a tripping shape on both R-13 branches.

**R-26:** `DeliveryBudgetDiagnostics.WarnIfUnenforceable(subscription)` is called once on each non-delegating channel-creation path of the in-scope channel factories — the method every public entry reaches, never a method that only delegates to another (`CreateAsyncChannel` → `CreateAsyncChannelAsync` on GCP `:54-55` and AWS `:82-83`, which would log twice): `AWSSQS/ChannelFactory.cs` `CreateSyncChannelAsync` (`:204`) and `CreateAsyncChannelAsync` (`:92`) and the V4 twins (uniform shape; a no-op for SQS), `GcpPubSubChannelFactory` `CreateSyncChannel` (`:26`) and `CreateAsyncChannelAsync` (`:65`), `RocketMqChannelFactory.cs:13`, `:28`, `:43`. The channel factory is the site both the dispatcher (`ConsumerFactory.cs:100`, `:124`) and direct users (conformance providers; AC-11/AC-25/AC-40) pass through; it logs once per channel, never on the receive path (NFR-4, AC-29).

#### R-8 / R-9

No code. **Brighter does not assume it is the one that dead-letters.** Queues and subscriptions are often provisioned outside Brighter, so a native redrive policy (SQS `RedrivePolicy.maxReceiveCount`, Pub/Sub `DeadLetterPolicy.MaxDeliveryAttempts`) may move a message before the budget is spent, and that is a legitimate outcome. `min(R, M)` emerges because whichever threshold is reached first acts; Brighter neither suppresses nor stamps a native redrive (R-9), so a natively redriven message arrives without rejection metadata and presents the destination's own count. R-10 is the only Brighter-side expression of R-8, and it can fire only where `M` is visible to Brighter (a policy Brighter created); a policy configured outside Brighter is invisible and is not warned about.

### Implementation Approach

Structural and behavioural commits kept apart (Tidy First, C-10):

1. **Structural:** add `RejectionMetadataKeyNames`; swap the inline creators' call order; in-scope `RefreshMetadata` adopt the constants.
2. **Core:** `DeliveryCount` (+ AC-33 unit tests, including the discriminator); `IAmADeliveryCountingSubscription`; `DeliveryBudgetDiagnostics`; the three rules (AC-7, AC-10, AC-11, AC-32 in `Paramore.Brighter.Core.Tests`).
3. **Conformance oracle:** the redelivery-arm change in the FR-2/15/16/22 templates, regenerated — lands before or with step 4 so no `Pass` cell goes red.
4. **SQS, both packages in one commit (NFR-6):** creators; null-reason `"None"` stamping in `RefreshMetadata`; interface on `SqsSubscription`; factory calls.
5. **GCP:** parser (pull + stream, `googclient_deliveryattempt` ignore entry); interface; factory calls. FR-23 gated on AC-39, after 0078/R-20.
6. **RocketMQ:** AC-23 measurement, then the AC-24 or AC-25 branch (null-reason stamping lands on either branch).
7. **Harness (R-27):** `R = 3, M = 5` on the 12 providers (GCP's native-policy shape: 0078 Implementation step 5); GCP IAM members; dispatch-count and recording consumer in `ConformanceDeferredPump.cs.liquid`; the `<= RequeueCount` assertion in both FR-23 templates.

**Testing** follows `.agent_instructions/generated_tests.md` — templates edited, tests regenerated, never hand-edited.

| Transport | Environment | Tests |
|---|---|---|
| AWS | LocalStack | FR-23 generated, both variants (AC-12); AC-13 v3/v4 comparison |
| GCP | Pub/Sub emulator (`docker-compose-gcp.yaml`) | AC-39 → AC-19 or AC-40; AC-42 incl. the stream procedure |
| RocketMQ | local compose | AC-23 → AC-24 or AC-25; AC-38 |

Bespoke tests (constructed subscriptions, not provider-supplied): AC-1, AC-5, AC-6, AC-35, AC-8, AC-9, AC-41, AC-42. AC-37: allocated-bytes, median of 5 × 1,000 receives, `post <= base`; the new reads are value-typed (`int?`, `TryGetValue` on existing dictionaries, `int.TryParse` on an existing string, one ordinal string comparison). AC-27: compile-only samples in the five projects R-24 names.

**AC-28 broker-call enumeration — no call added or removed:**

| Transport | Per delivery (before = after) | Per requeue (before = after) |
|---|---|---|
| AWSSQS / V4 | `ReceiveMessage` (system attribute already requested) | `ChangeMessageVisibility` |
| GcpPubSub pull | `Pull` | `ModifyAckDeadline(…, 0)` |
| GcpPubSub stream | streaming pull (no per-message RPC) | local Nack on the stream |
| RocketMQ | `SimpleConsumer.Receive` | none |

## Consequences

### Positive

- `requeueCount` enforces on all 8 AWS/AWS.V4 configurations, and conditionally on GCP and RocketMQ, with the existing pump code as the only enforcer.
- One rule in one place; v3/v4 lockstep holds by construction.
- No broker call, no state (NFR-1..NFR-3); restarts and competing consumers do not reset the count, because the broker keeps it.
- R-28 holds without a new wire key; DLQ inspection keeps its evidence, including for null-reason rejections.
- R-11 is never silent: validation Warning plus channel-creation log.

### Negative

- **The header-carried count is ignored on source channels where a broker counter exists** — a producer-set non-zero `HandledCount` no longer carries prior budget spend.
- All counters are approximate: rejection may come before delivery `R`, and v3/v4 may reject on different deliveries.
- GCP needs a `DeadLetterPolicy` to have a counter at all (A-1); without one the budget is unenforceable and only warned about.
- The conformance oracle weakens from equality to `>=` on the `HandledCount` of four behaviours' redelivery arms.
- A null-reason rejection now carries `rejectionReason = "None"` on the in-scope transports — a visible change to what a DLQ consumer sees, and a divergence from the out-of-scope Brighter-managed transports until they follow.
- A message put back on its source with rejection metadata attached is rejected again; resetting it is left to the replaying tool, which Brighter can only document (edge case 2).
- RocketMQ and GCP may still end *bound but unimplemented* (C-12's recorded price).

### Risks and Mitigations

| Risk | Mitigation |
|---|---|
| A-2 refuted on the emulator | pre-registered branch rule (AC-40); independent search already recorded |
| First-delivery over-count (C-7) | recorded residual risk; observed failures logged against this ADR |
| `StreamOrdering` lapse blocked by ordering | alternative test defined above |
| RocketMQ counter is client-local, not broker-supplied | fresh-client step in AC-23 |
| Stale `googclient_deliveryattempt` travels on a routed GCP copy | excluded from the bag here; 0078 must not reintroduce it |
| A replay tool puts messages back without stripping metadata, and they bounce straight back to the DLQ | documented as the tool's responsibility; the bounce is immediate and visible (a `DeliveryError` rejection on first delivery), not a silent loop |
| Unverified library behaviours: whether `SubscriberClient` injects/overwrites `googclient_deliveryattempt`; whether `bufferSize: 2` admits the stream redelivery while the first is held | confirmed at implementation; this ADR amended if either fails |

## Alternatives Considered

1. **Consumer-side in-memory tracking** keyed by message id (incremented on `Requeue`, read on receive). *For:* no broker support needed, would work on RocketMQ today, exact, independent of A-1/A-2. *Against:* resets on restart and deploy — exactly when poison messages bite; invisible to competing consumers (N performers or pods → `N × R` deliveries); needs eviction for NFR-2, which makes the count forgettable, and leaks without it; does not advance on an expiry redelivery that never passes through `Requeue` (AC-42); invents state the broker already keeps. Rejected.
2. **Republish on requeue** (the nine conforming transports' mechanism). **Excluded by requirement** (NFR-3, C-12): a net broker call per requeue; bypasses the user's visibility timeout / ack deadline; SQS FIFO content-based dedup would silently discard the copy, since `MessageDeduplicationId` is set only when the bag carries one (`SqsMessageSender.cs:100-102`). Listed so it is not re-opened.
3. **`max(header count, normalised broker count)` everywhere, no discriminator.** Gives replayed messages a working budget, but lets a DLQ's own count override the stamped count on repeated DLQ redeliveries — violating R-28's "never that destination's own count". Rejected.
4. **A new wire key (e.g. `x-brighter-routed`) as discriminator.** Every transport must write and read a new key, while `rejectionReason` is already stamped by every Brighter-managed route. Rejected (user's call).
5. **Detect a return to the source and use the broker count there** — either `Header.Topic != originalTopic`, or `originalTopic` compared with the reading channel's own identity (queue URL, subscribed topic ARN). Would give a message put back with metadata a working budget. Rejected: putting a message back is outside Brighter's control and resetting it is the replaying tool's job; the message-`Topic` variant also fails on SQS's own redrive (`StartMessageMoveTask` keeps attributes, so `Topic` stays the DLQ URL, `SqsMessageSender.cs:115`), and the channel-identity variant would thread consumer identity into every creator/parser for an edge case Brighter should only document.
6. **Leave null-reason rejections unstamped.** No change to `0047-message-rejection-routing-strategy`'s metadata, but a DLQ read of such a copy shows the DLQ's own count. Rejected (user's call).
7. **Upper rung (`IAmAPipelineValidator` per transport) for R-10/R-11.** The inputs are an integer and a predicate on a subscription, which a core role interface carries; the upper rung adds a dependency on #4282 for nothing. Rejected, per `0074-lifetime-validation-evaluation-site`'s test.
8. **Log R-11 in `ServiceActivator.ConsumerFactory` only.** One site, but misses channels created directly through a channel factory — where AC-11/AC-25/AC-40 create them. Rejected in favour of the channel factories calling a shared core helper.
9. **Implement the SQS budget as native redrive** (`RedrivePolicy.maxReceiveCount = requeueCount` on queues Brighter creates). Rejected as *the* mechanism: the dead-lettered message loses the rejection metadata, `Unacceptable` cannot reach the invalid-message channel, it reverses `0038-aws-sqs-dlq-direct-send`, and it does nothing on queues Brighter does not create (`makeChannels: Assume`/`Validate`). Native redrive remains a legitimate *co-existing* route (R-8/R-9, above).

## References

- Requirements: [specs/0037-delivery-count-and-rejection-routing/requirements.md](../../specs/0037-delivery-count-and-rejection-routing/requirements.md)
- Related ADRs:
  - `0006-blocking-and-non-blocking-retries` — defer, requeue and the DLQ threshold this makes reachable.
  - `0038-aws-sqs-dlq-direct-send` — the SQS direct-send route (C-1) and v3/v4 lockstep.
  - `0042-rocketmq-dlq-brighter-managed` — RocketMQ `Reject` → DLQ producer + Ack.
  - `0045-provide-dlq-where-missing`, `0046-kafka-dlq-producer-for-requeue`.
  - `0047-message-rejection-routing-strategy` — routing by `RejectionReason` and the rejection metadata the discriminator reads; refined here for null reasons on the in-scope transports.
  - `0053-pipeline-validation-at-startup` — the `ValidatePipelines` seam.
  - `0061-reject_mapping_errors`.
  - `0066-conformance-test-provider-and-ungating`, `0067-conformance-rollout-and-deferral-governance` — the FR-23 ledger cells this moves and the redelivery-arm oracle change.
  - `0074-lifetime-validation-evaluation-site` (PR #4282, unmerged) — the two-rung ladder; lower rung taken, so no dependency.
  - `0078-gcp-rejection-routing-and-dlq-channel-creation` (Proposed) — GCP rejection routing and DLQ channel creation.
  - Supersedes none.
- External references: Google.Cloud.PubSub.V1 3.36.0 (`ReceivedMessage.DeliveryAttempt`, `PubsubExtensions.GetDeliveryAttempt`); AWS SQS `ApproximateReceiveCount`; RocketMQ.Client 5.2.1 (`MessageView.DeliveryAttempt`).

### Deferred to ADR 0078

Not decided here: R-16 to R-21 (GCP routing; `Reject` composition per consumer — R-19's ADR MUSTs; evidencing the failed-ack and failed-release outcomes); the `makeChannels` of the GCP dead-letter producer (AC-18/AC-43); NFR-5's Resource Manager exception type (AC-21); R-20's IAM tolerance.

**Constraints this ADR places on 0078:** the routed GCP copy stamps `RejectionMetadataKeyNames` (`rejectionReason` — `"None"` for a null reason) and carries `HandledCount`; it must not re-publish `googclient_deliveryattempt`.
