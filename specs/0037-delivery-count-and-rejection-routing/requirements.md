# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4341 (family head) · #4386 · #4353 · #4354

> **Numbering convention, read this first.** Requirements in this document are numbered `R-n`, and
> acceptance criteria `AC-n`. They are deliberately **not** numbered `FR-n`, because `FR-nn` is
> already spoken for by spec 0036's conformance behaviour ledger
> (`specs/0036-universal-transport-conformance-tests/conformance-status.md`), which this document
> cites throughout. Throughout: **`R-n` = a requirement of this spec; `FR-nn` = a conformance
> behaviour of spec 0036.**

## Problem Statement

**As an operator of a Brighter consumer**, I would like a message whose handler keeps deferring to
stop being redelivered after the number of attempts I configured, and to land somewhere I can
inspect it, **so that** a poison message cannot loop for ever, consuming broker and compute budget,
and so that I can see why it stopped.

Today that is true on nine of the conformance ledger's twenty-four configurations (Redis,
Kafka ×3, MSSQL, Postgres, RMQ.Async ×2, RMQ.Sync) and false on fifteen. **Thirteen of the fifteen
are in scope here** (AWS ×4, AWS.V4 ×4, GCP ×4, RocketMQ); the other two — MQTT and Azure Service
Bus — fail for different root causes and are named in §Out of Scope. On the thirteen,
`requeueCount` is accepted, configured, and has no effect. The pump enforces the budget by calling
`message.Header.UpdateHandledCount()` and then `message.HandledCountReached(RequeueCount)`
(`Reactor.cs:494`, `:498`; `Proactor.cs:500`, `:504`), which only runs down if the **redelivered**
message presents the incremented count. **All nine** that work requeue by **republishing**, so the
header travels and the budget runs down — the three RMQ rows included, which republish through
`RmqMessagePublisher.RequeueMessageAsync` and carry `HANDLED_COUNT` on the republished copy
(`RmqMessagePublisher.cs:174`, read back at `RmqMessageCreator.cs:208-210`). The RMQ rows differ
only in how the **rejected** message reaches its destination: Brighter's `Reject` is a
`BasicRejectAsync(..., requeue: false)` and RabbitMQ's DLX moves the broker's own stored copy,
rather than Brighter publishing to a destination it owns. The budget is exactly as load-bearing
there as on the other six — at `requeueCount: -1` an RMQ message is requeued for ever and nothing
reaches the DLX. The thirteen that fail ask the broker to re-serve its **own stored copy**, which was never
rewritten — so every redelivery reads the count it was first published with, the pump bumps it to
1, and the test is never true.

**As an operator of a GCP Pub/Sub consumer**, I would like a rejected message to be preserved
somewhere, **so that** I can inspect or replay it. Today `Reject` **acknowledges and discards** it
in both the pull (`GcpPullMessageConsumer.cs:276`, async `:306`) and stream
(`GcpPubSubStreamMessageConsumer.cs:84`) consumers. The `MessageRejectionReason` argument is bound
and never read. `RejectionReason.Unacceptable` (a message a handler cannot parse) and
`RejectionReason.DeliveryError` (the budget spent) both destroy the message. This is silent data
loss on a production path, in all four configurations and both pump variants.

**As an operator whose DLQ is provisioned by infrastructure-as-code**, I would like to create a
DLQ-backed GCP channel with an application service account, **so that** my consumer starts.
Today `GcpPubSubMessageGateway.EnsureSubscriptionExistsAsync` (`:235`, `:251`) unconditionally reads
and writes project-level IAM and hard-fails if it cannot, even when the binding already exists.

**As a Brighter maintainer**, I would like one settled answer to *how a delivery count survives a
requeue the broker does not rewrite*, **so that** the repo does not acquire three different answers
to the same question, and so that the FR-23 conformance column stops being a list of the same defect
three times.

## Proposed Solution

Settle one **delivery-count contract** that every transport must honour, and then bind the three
transports that do not honour it to it.

From a user's point of view, after this work:

1. **`requeueCount` means what it says, everywhere.** Configure `requeueCount: 3` on an SQS, GCP or
   RocketMQ subscription and a message whose handler always defers is delivered a bounded number of
   times and is then rejected with `RejectionReason.DeliveryError` — the same observable outcome
   users already get on Redis, Kafka, MSSQL, Postgres and RabbitMQ.
2. **A GCP `Reject` routes the message rather than destroying it.** A rejected GCP message goes to
   the dead-letter or invalid-message destination chosen by its rejection reason, carrying the
   rejection metadata every other Brighter-managed gateway stamps, before the original is
   acknowledged.
3. **A DLQ-backed GCP channel can be created by a principal that cannot administer project IAM**,
   and therefore against the Pub/Sub emulator — so the GCP half of this work has a local, repeatable
   proof rather than a cloud-only one.
4. **The relationship between a Brighter budget and a native redrive policy is stated, not
   discovered.** When both are configured the effective limit is the smaller of the two, and
   Brighter says so at startup instead of letting a user believe a budget is in force that is not.

The users are Brighter application developers and the operators of their consumers. Nothing in their
configuration API needs to change: the same `requeueCount`, `DeadLetterRoutingKey` and
`InvalidMessageRoutingKey` they already set start having the effect they already expect.

**This document does not choose between the mechanisms that remain open, but it does close one.**
"Read the broker's own delivery counter on receive" and "track the count consumer-side" are both
capable of satisfying the contract below, at different costs on each transport, and choosing
between them is the ADR's job. "Rewrite the stored message on requeue (republish)" is **excluded**,
by NFR-3 and for the reasons C-12 records. §Constraints tabulates all three, the excluded one
included, so the ADR starts from a complete set and the exclusion is visible rather than inferred.

## Requirements

### Terms

Used with exactly these meanings throughout. Where a term names an existing code element, the
element is cited so there is no second reading.

| Term | Meaning |
|---|---|
| **Delivery** | One presentation of a message to a Brighter consumer on a channel — i.e. one message returned from `IAmAMessageConsumer.Receive`/`ReceiveAsync` and dispatched by a pump. |
| **Delivery count** | The value of `MessageHeader.HandledCount` (`MessageHeader.cs:226`) on a message as it is handed to the pump by the consumer, **before** the pump calls `UpdateHandledCount()`. Where a rule reads the count on a message taken from a rejection destination rather than a source channel — R-5, R-28, AC-4, AC-41 — it means the `HandledCount` that message presents on that read; R-28 governs what that value must be. |
| **Delivery budget** | The `Subscription.RequeueCount` value. The maximum number of deliveries Brighter will make before rejecting with `RejectionReason.DeliveryError`. `-1` (the default) means "no budget". |
| **Budget exhaustion** | The pump path at `Reactor.cs:494-498` / `Proactor.cs:500-504`: `UpdateHandledCount()`, then `HandledCountReached(RequeueCount)` true, then `RejectMessage(..., RejectionReason.DeliveryError)`. |
| **Requeue** | `IAmAMessageConsumer.Requeue`/`RequeueAsync` — returning a message to its channel for later redelivery. |
| **Redelivery** | Any delivery of a message after its first. |
| **Native dead-lettering** | The broker moves the message to a dead-letter destination by its own policy once a delivery threshold is crossed, with **no Brighter call at all**: SQS `RedrivePolicy.maxReceiveCount`, Pub/Sub `DeadLetterPolicy.MaxDeliveryAttempts`, Azure Service Bus `$DeadLetterQueue`. |
| **Broker-routed rejection** | Brighter calls `Reject` and the **broker** then moves its own stored copy to a destination Brighter never publishes to: RabbitMQ's DLX, reached by `BasicRejectAsync(deliveryTag, requeue: false)` (`RmqMessageConsumer.cs:362`). Distinct from native dead-lettering — Brighter's budget still has to run down to reach it — and distinct from Brighter-managed dead-lettering, because no Brighter-managed send occurs and therefore no rejection metadata is stamped. **No transport in scope uses this route**; it is defined because the three RMQ rows do, and R-22 protects them. |
| **Native redrive limit** | The delivery threshold of a native dead-letter policy. Written `M` below. |
| **Brighter-managed dead-lettering** | Brighter publishes the message to a destination it owns (`IUseBrighterDeadLetterSupport.DeadLetterRoutingKey` / `IUseBrighterInvalidMessageSupport.InvalidMessageRoutingKey`) and then removes the original. ADRs `0038-aws-sqs-dlq-direct-send`, `0039-redis-dlq-brighter-managed`, `0041-postgres-dlq-brighter-managed`. |
| **Rejection routing** | Selecting the destination for a rejected message from its `RejectionReason`: `Unacceptable` → invalid-message destination, falling back to dead-letter when none is configured; `DeliveryError` and `None` → dead-letter. Reference implementation: `SqsMessageConsumer.DetermineRejectionRoute` (`:541`). |
| **Rejection metadata** | The `Header.Bag` keys a Brighter-managed rejection stamps: `originalTopic`, `originalMessageType`, `rejectionReason`, `rejectionMessage`, `rejectionTimestamp`. Reference: `SqsMessageConsumer.RefreshMetadata` (`:496`). |
| **Exact counter** | A delivery count that advances by exactly 1 per delivery. |
| **Approximate counter** | A delivery count the source documents as approximate or best-effort, and which may therefore advance by more than 1. SQS `ApproximateReceiveCount` and Pub/Sub `delivery_attempt` are both documented as such. |
| **Transport in scope** | One of: `AWSSQS`, `AWSSQS.V4`, `GcpPubSub`, `RocketMQ`. |
| **Configuration** | A row of spec 0036's Conformance Matrix, e.g. `AWS / SqsFifo` or `GCP / StreamOrdering`. |
| **Variant** | `Reactor` or `Proactor`. Spec 0036's FR-14 rule: a behaviour proven in one variant only does not count. Where an AC drives a consumer directly with no pump (e.g. AC-15 to AC-18, AC-42), a *delivery* is one message returned from `Receive`/`ReceiveAsync`, its *delivery count* is that message's `HandledCount` as returned, and "both variants" means the synchronous **and** asynchronous form of every consumer call the AC makes — `Receive`/`ReceiveAsync`, `Reject`/`RejectAsync`, `Acknowledge`/`AcknowledgeAsync`, and so on. |

Throughout, **`R`** denotes a subscription's `RequeueCount` and **`M`** its native redrive limit.

---

### Functional Requirements

#### Group A — The delivery-count contract

**R-1. A redelivered message presents a delivery count strictly greater than the count it presented
on its previous delivery.**
This holds for every transport in scope, on every channel, in both variants, whether the redelivery
follows an explicit `Requeue` or a lease/visibility/ack-deadline expiry.

> *Example (illustrative of the obligation above; it does not add to it).* A message on an
> `AWS / SqsStandard` subscription is delivered, deferred, delivered again, deferred again,
> delivered a third time. The three deliveries present delivery counts *c₁ < c₂ < c₃*. Whether the
> increments are exactly 1 is settled by R-3, not here; whether the three deliveries arrive in any
> particular wall-clock spacing is not constrained.

**R-2. The first delivery of a message presents a delivery count of 0.**
This applies to a message that has never been requeued, whether or not a budget is configured, and
on all four transports in scope. **R-2 is absolute and takes precedence over R-3**: where a
transport's delivery count originates at `1`, or reports an elevated value on a first delivery
because it is approximate, the consumer normalises it to `0` before handing the message to the
pump. Approximation is tolerated only from the second delivery onward.
Where a transport's counter is classified approximate (R-3), the ADR MUST state how its chosen
mechanism satisfies R-2 on a delivery whose broker counter exceeds its documented origin value; if
the mechanism cannot guarantee it, the ADR MUST record the residual risk against C-7's identity
assertion and name the conformance cells exposed to it. Silence on this point is not an available
outcome.

> *Example.* A message is published to `GCP / Stream` and delivered once. The consumer hands the
> pump a message whose `Header.HandledCount` is `0`, equal to the value the producer sent.

> *Why this is an obligation and not an implementation nicety*: the generated conformance assertion
> `DefaultMessageAssertion` compares `expected.Header.HandledCount` with `actual.Header.HandledCount`
> for equality (`tools/Paramore.Brighter.Test.Generator/Templates/DefaultMessageAssertion.cs.liquid:59`). A first delivery that presented
> `1` would fail the round-trip identity assertion used by conformance behaviours FR-2, FR-15,
> FR-16, FR-22 and others across every configuration of the transports in scope. See R-23.

**R-3. For `n >= 2`: where a transport's delivery count is exact, the *n*th delivery presents a
count of `n - 1`; where it is approximate, the count presented is at least `n - 1`.**
R-3 governs redeliveries only. At `n == 1`, R-2's exact `0` governs on every transport in scope,
exact or approximate, and R-3 grants no latitude there.
The ADR must record, for each of the four transports in scope, which of the two classifications
applies and on what evidence, as a four-row table. A transport may not be left unclassified, because
R-11's validation rule and AC-11's Given both read that classification.

> *Example (exact).* Deliveries 1, 2, 3 of a message present counts `0`, `1`, `2`.
> *Example (approximate).* Deliveries 1, 2, 3 of the same message may present `0`, `1`, `3` — R-1
> and R-3 are both satisfied; R-5's bound is still met.

**R-4. A delivery budget of `R` (where `R >= 1`) causes a message whose handler defers on every
delivery to be rejected with `RejectionReason.DeliveryError` after at most `R` deliveries, and never
to be redelivered a further time.**
"Never redelivered a further time" means: the handler is not invoked again for that message, and
the count of handler invocations for it stands at `R` or fewer when the FR-23 observation window
closes. **That window closes when the pump has been quit and awaited** — which the FR-23 template
already does, two statements after its dead-letter poll breaks
(`_channel.Enqueue(MessageFactory.CreateQuitMessage(...)); await pumping;`). The poll itself — every
500 ms, giving up at 60 s — is *not* the close: between the poll's break and the quit the pump is
still dispatching, which is precisely the interval in which a budget that did not hold would keep
redelivering, so closing the window at the break would blind the observation to the failure it
exists to catch. No second observation window is opened and no additional wait is introduced — the
quit and await are already there — so the behaviour's runtime is unchanged by this spec.

> *Example.* `requeueCount: 3` on `AWS / SqsStandard`, handler always defers, no native redrive
> policy in force ahead of it. Delivery 1 presents `0`, pump increments to `1`, `1 >= 3` false,
> requeue. Delivery 2 presents `1`, increments to `2`, false, requeue. Delivery 3 presents `2`,
> increments to `3`, `3 >= 3` true → `RejectMessage(..., DeliveryError)`. Three deliveries, two
> requeues, one rejection. For an approximate counter that jumped `0`, `2`, the rejection occurs on
> delivery 2 — still "at most `R`", which is what R-4 requires.

**R-5. The rejection raised by budget exhaustion is routed by the transport's rejection routing and
carries rejection metadata.**
`DeliveryError` routes to the dead-letter destination. The message arriving there carries the five
rejection-metadata keys of the transport, and a delivery count of at least `R - 1`.

> *Example.* `requeueCount: 3`, `deadLetterRoutingKey` configured. The message arrives on the
> dead-letter destination with `rejectionReason = "DeliveryError"`, a non-empty `rejectionMessage`,
> a `rejectionTimestamp`, an `originalTopic` equal to the source topic, an `originalMessageType`,
> and `HandledCount >= 2`.
>
> The `>= R - 1` bound (not `== R`) is the bound spec 0036's FR-23 template already asserts, and it
> is the strongest bound true of both routes to the DLQ: where Brighter sends the message it sends
> the final in-memory header and the count reads at least `R` — exactly `R` where the counter is
> exact, more where an approximate counter jumped past `R - 1` on the rejecting delivery; where the broker moves its own stored copy the
> copy was written by the last requeue and reads one less. **R-28 is what makes this bound survive
> the mechanism**: it forbids a read of the dead-letter destination from reporting that
> destination's own delivery count instead.

**R-6. A delivery budget of `-1` disables budget enforcement, and this behaviour does not change.**
With `RequeueCount == -1` (the `Subscription` default, `Subscription.cs:203`), no delivery count is
ever compared against a budget, no `DeliveryError` rejection is raised by the pump, and a deferring
handler's message is requeued indefinitely, absent a native redrive limit (R-8). R-1, R-2 and R-3 still hold — the count still advances —
but nothing acts on it.

> *Example.* `requeueCount` left at its default on `RocketMQ / RocketMQMessagingGateway`, handler
> always defers. `DiscardRequeuedMessagesEnabled()` (`MessagePump.cs:171`) is false, the message is
> redelivered for as long as the pump runs, and nothing reaches the dead-letter destination.

**R-7. A delivery budget of `1` rejects on the first delivery; a budget of `0`, and any budget
below `-1`, rejects on the first delivery and is surfaced by startup pipeline validation as a
probable misconfiguration.**
`R == 1`: the first deferral exhausts the budget (count `0`, incremented to `1`, `1 >= 1`), so the
message is rejected without ever being requeued. `R == 0`: enforcement is enabled (`0 != -1`) and
the first deferral also rejects immediately. `R < -1` (e.g. `-3`): enforcement is likewise enabled,
because the pump's switch is `RequeueCount != -1` (`MessagePump.cs:171`) and not a sign test, and
the first deferral rejects immediately for the same reason — `HandledCountReached` compares
`HandledCount >= requeueCount` (`Message.cs:161-164`), and `1 >= -3` is true. `-1` is therefore the
**only** value that disables the budget; every other negative value behaves as `0` does.
Because a zero or below-`-1` budget is indistinguishable in effect from `1` and is far more likely
to be a mistake than an intent, both are reported by a **`ValidatePipelines` check** (R-25) at
`ValidationSeverity.Warning`, naming the subscription, the configured value, and the two values
probably intended — `-1` (unlimited) or `1` (once only). None of these values is a configuration
error, none throws, and a Warning never blocks startup.

> *Example.* `requeueCount: 1` on `AWS.V4 / SqsFifo`, handler defers once. One delivery, zero
> requeues, one `DeliveryError` rejection, message on the DLQ with `HandledCount >= 0`.

**R-28. A delivery count read from a rejection destination is the count stamped by whatever routed
the message there, never that destination's own delivery count.**
R-2's normalisation is a property of the delivery a **pump** consumes. It must not restate the count
of a message read back from a dead-letter or invalid-message destination: that count is evidence of
what happened on the *source* channel, and R-5, AC-4 and spec 0036's FR-23 template all read it as
such. A message read from a rejection destination therefore presents:

- the count the rejecting consumer sent — the final in-memory header, at least `R` and exactly `R`
  where the counter is exact — where Brighter published
  it there (Brighter-managed dead-lettering);
- the count the last requeue wrote — `R - 1` — where the broker moved its own stored copy on
  Brighter's reject (broker-routed rejection);
- whatever the last write left it, where a native policy moved it with no Brighter call (native
  dead-lettering: R-9's case, which carries no rejection metadata and which R-28 does not bound).

R-5's `>= R - 1` bound is exactly the envelope of the first two, which is why R-28 is what keeps R-5
assertable.

> *Why this is an obligation and not an implementation nicety*: every conformance read of a rejection
> destination goes through the production consumer, so a mechanism that synthesises the count on
> receive synthesises it there too.
> `SqsStandardMessageGatewayProvider.GetMessageFromDeadLetterQueueAsync` builds a real channel
> through `ChannelFactory` (`:294`), the GCP provider does the same
> (`GcpPullMessageGatewayProvider.cs:298`), and the RocketMQ provider wraps its `SimpleConsumer` in a
> real `RocketMessageConsumer` (`:298`). A mechanism that simply substituted the broker's counter for
> the stamped header would report the **rejection destination's** delivery count — `0` after R-2's
> normalisation — falsifying R-5, AC-4 and the FR-23 template's existing
> `HandledCount >= RequeueCount - 1` assertion on every cell this spec moves. A user inspecting their
> own DLQ with a Brighter consumer would lose the same evidence. C-12 makes this the ordinary case
> rather than a corner: with republish excluded, a synthesised count is the likely mechanism.

The ADR MUST record, for each of the four transports in scope, how its chosen mechanism satisfies
R-28, as a four-row table alongside R-3's. Where it satisfies R-28 by telling a routed message apart
from a source-channel delivery, the ADR MUST name the discriminator. Silence on this point is not an
available outcome.

> *Example.* `requeueCount: 3` on `AWS / SqsStandard`, budget exhausted, Brighter sends the message
> to its dead-letter queue. A read of that queue presents `HandledCount >= 3` — `3` unless the
> approximate `ApproximateReceiveCount` jumped (A-4), and never the `0` that the
> dead-letter queue's own `ApproximateReceiveCount` of `1` would normalise to.
> *Example (R-9's case).* `requeueCount: 10`, `maxReceiveCount: 3`. SQS redrives its own stored copy
> with no Brighter call, so the redrive target presents the count that copy was published with and no
> rejection metadata. R-28 does not require that to be `>= R - 1`, and AC-9 asserts no count.

#### Group B — Interaction with native dead-lettering

**R-8. When both a delivery budget `R` and a native redrive limit `M` are configured on the same
channel, the effective delivery limit is `min(R, M)`, and which of the two fired is observable.**
Brighter does not suppress, reconfigure or work around a native policy. Whichever threshold is
reached first determines the route the message takes, and R-9 makes the two routes distinguishable.

> *Example (Brighter wins).* SQS `requeueCount: 3`, `RedrivePolicy.maxReceiveCount: 5`. The budget
> is exhausted on delivery 3 (or earlier on an approximate counter, R-4), Brighter rejects, and the message reaches the Brighter-managed DLQ
> with rejection metadata. SQS's own counter never reaches 5.
> *Example (native wins).* SQS `requeueCount: 10`, `maxReceiveCount: 3`. SQS redrives its stored
> copy on the 3rd receive. The budget, which needed 10, is never spent.
> *Example (tie).* `requeueCount: 3`, `maxReceiveCount: 3` — the two thresholds coincide and which
> fires is not determined by Brighter. R-10 requires this to be warned about, not resolved.

**R-9. A message dead-lettered by a native redrive policy without Brighter having rejected it
arrives at the dead-letter destination WITHOUT Brighter rejection metadata, and Brighter MUST NOT
fabricate metadata for a route it did not take.**
The absence of rejection metadata is the *evidence* that Brighter did not reject the message — it is
how #4341 was detected — and that diagnostic must be preserved. This is a decision, recorded here
so it is not left implicit: metadata absence on a natively dead-lettered message is **permitted and
intended**, not a defect.

> *Example.* SQS `requeueCount: 10`, `maxReceiveCount: 3`, handler always defers. The message
> arrives on the redrive target queue with its original `handled-count` attribute and with **no**
> `rejectionReason`, `rejectionMessage`, `rejectionTimestamp` or `originalTopic` keys. A DLQ
> consumer reading it can conclude from that absence alone that a native policy, not Brighter,
> moved it.

**R-10. Startup pipeline validation reports a Warning when a configured delivery budget cannot be
relied on to fire ahead of a native redrive limit — that is, when `R != -1` and `M` is configured
and `R >= M`.**
Reported by a `ValidatePipelines` check (R-25). The finding names the subscription, `R` and `M`, and
states that the effective limit is `M`. It is a Warning, never an exception: the configuration is
legal and may be deliberate, and a Warning does not block startup even under `throwOnError: true`.

> *Example.* `GCP / Pull` with `requeueCount: 5` and `DeadLetterPolicy.MaxDeliveryAttempts: 5` —
> the harness configuration **as it stands before R-27**
> (`GcpPullMessageGatewayProvider.cs:151,155`) — is reported. So is `AWS / SqsStandard` with
> `requeueCount: 3` and `RedrivePolicy(dlq, 3)` (`SqsStandardMessageGatewayProvider.cs:104,108`).
> Both are real configurations in this repo today; they are harmless only because the budget is
> currently inert, they start racing the moment it works (A-5), and **R-27 removes both** — after it,
> a subscription in this state has to be constructed by the test that wants one.

**R-11. A delivery budget configured on a transport that cannot satisfy R-1 for that subscription is
reported by startup pipeline validation, and additionally logged once at channel creation.**
This is the anti-silence requirement: a budget that cannot run down must never be merely inert. The
finding names the subscription, the configured `R`, and the reason the count cannot advance. It is
raised once per channel, never per message. R-26 explains why this one condition is reported by both
routes while R-7 and R-10 use validation alone.

> *Example.* A `GcpPubSubSubscription` with `requeueCount: 3` and **no** `DeadLetter` policy, if the
> ADR's chosen mechanism is one whose counter Pub/Sub only populates on DLQ-backed subscriptions
> (see Assumption A-1): channel creation logs a Warning saying the budget is not enforceable on that
> subscription. *Example.* A `RocketMqSubscription` with `requeueCount: 3`, should R-14's condition
> fail: channel creation logs a Warning citing the blocker.

#### Group C — Transports bound by the contract

**R-12. `Paramore.Brighter.MessagingGateway.AWSSQS` and `Paramore.Brighter.MessagingGateway.AWSSQS.V4`
satisfy R-1 to R-5 in lockstep, across all eight AWS and AWS.V4 configurations and both
variants.**
"Lockstep" is ADR `0038-aws-sqs-dlq-direct-send`'s requirement and means identical observable behaviour: the same contract,
the same routing, the same metadata keys, the same log messages. The eight configurations are
`AWS / SnsStandard`, `AWS / SnsFifo`, `AWS / SqsStandard`, `AWS / SqsFifo` and the four `AWS.V4`
twins. FIFO queues are included: nothing in R-1 to R-5 permits a FIFO channel to be exempted.

> *Example.* `requeueCount: 3` on `AWS.V4 / SnsFifo`, handler always defers. Three deliveries (or
> fewer on an approximate counter, R-4) presenting counts `0`, `1`, `2` (or an approximate sequence satisfying R-3), a `DeliveryError`
> rejection, and a dead-lettered message with `rejectionReason = "DeliveryError"` — observed in
> both the `Reactor` and `Proactor` variants.

**R-13. `Paramore.Brighter.MessagingGateway.GcpPubSub` satisfies R-1 to R-5 in both the pull and the
stream consumer, across all four GCP configurations and both variants.**
The four configurations are `GCP / Pull`, `GCP / PullOrdering`, `GCP / Stream`,
`GCP / StreamOrdering`. Both consumer classes are bound: `GcpPullMessageConsumer` and
`GcpPubSubStreamMessageConsumer`. An ordering-key subscription is not exempted.

**R-13 is conditional in the same shape R-14 is, on the ADR's recorded conclusion — to which
assumption A-2 supplies one route — and both sides have a defined "done".** A-2 — that the Pub/Sub emulator populates the delivery counter on a DLQ-backed
subscription — cannot be measured until R-20 lands, and R-21 makes the emulator the verification
bar.

- **If the ADR records a GCP mechanism that satisfies R-1 to R-5 within NFR-1 to NFR-3** — which
  A-2 holding makes available through the broker counter, and which a `delivery_attempt`-independent
  mechanism can also supply — "done" = the four `GCP / *` FR-23 cells move to `Fixed`, both variants,
  on emulator evidence. A-2 holding is what makes the counter *available*; it is the ADR's recorded
  conclusion, not the measurement alone, that selects this branch (see the note under AC-40).
- **If the ADR records that no GCP mechanism satisfies R-1 to R-5 within NFR-1 to NFR-3** — whether
  because A-2 was refuted and no `delivery_attempt`-independent mechanism fits, or because A-2 held
  but no mechanism reaches R-2's exact `0` or R-28's rule — GCP is *bound but unimplemented* on R-1
  to R-5, and "done" = (a) R-11's Warning fires for any `GcpPubSubSubscription` with
  `RequeueCount != -1`; (b) the four `GCP / *` FR-23 cells stay `Deferred`, re-pointed at the
  emulator limitation rather than at #4240; (c) the measurement that settled it is written into
  `conformance-status.md`'s GCP paragraph.

**A-2 shapes the route, not the branch.** If A-2 is refuted, R-13's obligation is unchanged but the
broker counter is unavailable, so the ADR must look for a GCP mechanism that does not depend on
`delivery_attempt` before it may record the second branch.

**The GCP stream consumer's expiry redelivery is an ADR input, not a requirements decision.** R-1
binds `GcpPubSubStreamMessageConsumer`'s expiry path as it binds every other, and AC-42 asserts it;
*how* a test drives a lease lapse through that consumer is left to the ADR, because it turns on
client-library behaviour this document should not fix. Facts verified in source, which the ADR
starts from:

- While a received message is unanswered, `BrighterStreamHandler.HandleMessage` awaits
  `WaitForCompleteAsync()` (`GcpStreamConsumer.cs:71-84`) and `SubscriberClient` keeps extending
  its lease — by default for up to 60 minutes (`DefaultMaxTotalAckExtension`, Google.Cloud.PubSub.V1
  3.36.0). The subscription's `AckDeadlineSeconds` does not govern that. A
  `MaxTotalAckExtension` set through `GcpPubSubSubscription.StreamingConfiguration` survives the
  factory, because the hook runs before `builder.Settings ??= …` (`GcpPubSubConsumerFactory.cs:110-121`).
- The client's concurrent-processing limit is `BufferSize × NoOfPerformers`
  (`GcpPubSubConsumerFactory.cs:88-91`), `1` by default (`Subscription.cs:200-201`), applied
  client-wide, and `StreamingConfiguration` cannot raise it because the factory overwrites
  `FlowControlSettings` after the hook. A held, unanswered delivery occupies that slot, so at the
  default a redelivery is never dispatched while the first is held.
- On `GCP / StreamOrdering` the redelivery shares the held message's ordering key, and ordered
  delivery may dispatch same-key messages only sequentially — *not verified*.
- Shutdown uses `ShutdownMode.WaitForProcessing` with no `Timeout` (`GcpStreamConsumer.cs:49`),
  which the library bounds by a default derived from the maximum extension duration.

**On R-13's first branch** (the ADR records a satisfying GCP mechanism; AC-19 claimed), the ADR MUST
record, for each of `GCP / Stream` and `GCP / StreamOrdering`, the subscription and client
configuration and the receive / complete sequence by which AC-42 is driven — **or**, where a
configuration cannot be driven that way, an **alternative test** of R-1's expiry clause for it, and
why. An alternative test must itself be executed and assertable: it runs against the Pub/Sub
emulator (R-21), in both variants (NFR-8), through `GcpPubSubStreamMessageConsumer`, and asserts that
a redelivery not preceded by a `Requeue` call presents a strictly greater count. **An argument is not
evidence** — in particular, that the stream consumer shares code with the pull consumer does not
evidence the stream consumer. Neither configuration may be left without one of the two. On R-13's
second branch (AC-40) AC-42 does not apply to GCP (see the guard at the head of §Delivery-count
contract), and this obligation lapses with it.

**R-15 to R-19 (rejection routing) do not depend on A-2 and are unaffected by either branch.** They
depend on R-20 alone, which is why the twenty GCP rejection-routing cells in AC-30 are unconditional
while the four GCP FR-23 cells are not.

> *Example.* `requeueCount: 3` and a `DeadLetterPolicy` with `MaxDeliveryAttempts: 5` on
> `GCP / StreamOrdering`, handler always defers. The budget is exhausted on delivery 3 (or earlier on
> an approximate counter, R-4) — ahead of
> Pub/Sub's own threshold of 5 — and the message reaches the Brighter dead-letter destination with
> `rejectionReason = "DeliveryError"`.

**R-14. RocketMQ is bound by the contract unconditionally; whether it is *implemented* here is
conditional, and both sides of the branch have a defined "done".**

> **The condition, stated so it can be tested:** a RocketMQ message redelivered after its
> invisibility lease lapses presents a broker-supplied delivery-attempt value strictly greater than
> the value it presented on the previous delivery, **without** any call to
> `consumer.ChangeInvisibleDuration`.

- **If the condition holds** — RocketMQ satisfies R-1 to R-5 in this spec, in both variants.
  `RocketMessageConsumer.Requeue` (`:179`) may remain a no-op with respect to the broker; nothing in
  R-1 to R-5 requires a broker call on requeue. "Done" = the `RocketMQ / RocketMQMessagingGateway`
  FR-23 cell moves to `Fixed`, both variants green against `docker-compose-rocketmq.yaml`.
- **If the condition does not hold** — RocketMQ is *bound but unimplemented*. "Done" = (a)
  `RocketMqSubscription` continues to implement both support interfaces, which it already does; (b)
  R-11's Warning fires for any RocketMQ subscription with `RequeueCount != -1`; (c) the FR-23 cell
  stays `Deferred` with its pointer changed from #4353 to the specific upstream
  `ChangeInvisibleDuration` blocker, and #4353 is updated to record the measurement that settled
  the branch; (d) the measurement — inputs, counts observed on each delivery, and the conclusion —
  is written into `conformance-status.md`'s RocketMQ paragraph so it is not re-derived.

> *Example of measuring the condition.* `requeueCount: 3` on
> `RocketMQ / RocketMQMessagingGateway`, handler always defers, `Reactor` variant. RocketMQ's
> invisibility lease is 10 s, so three deliveries occupy roughly 30 s, inside the FR-23 template's
> 60 s poll ceiling. Log the broker's delivery-attempt value on each of the three deliveries. Values
> `1, 2, 3` (or any strictly increasing sequence) satisfy the condition; values `1, 1, 1` do not.

#### Group D — GCP rejection routing (#4386)

**R-15. `GcpPubSubSubscription` offers a dead-letter destination and an invalid-message destination
in the same way every other Brighter-managed gateway does.**
It implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`, joining the
eight transport gateways plus InMemory that already do
(`AWSSQS`, `AWSSQS.V4`, `Kafka`, `MQTT`, `MsSql`, `Postgres`, `Redis`, `RocketMQ`, `InMemory`).
This is additive: `GcpPubSubSubscription` keeps its existing `DeadLetter` (`DeadLetterPolicy`)
property, which configures Pub/Sub's *native* policy and is a different thing from
`DeadLetterRoutingKey`.

> *Example.* `new GcpPubSubSubscription<MyCommand>(..., deadLetterRoutingKey: new
> RoutingKey("orders.DLQ"), invalidMessageRoutingKey: new RoutingKey("orders.Invalid"))` compiles
> and both values are readable through the two interfaces.

**R-16. A GCP `Reject` routes the message to the destination selected by its rejection reason before
acknowledging the original, and never discards a message for which a destination is configured.**
Routing follows the settled rejection-routing rules: `Unacceptable` → invalid-message destination,
falling back to the dead-letter destination when no invalid-message destination is configured;
`DeliveryError` and `None` → dead-letter destination. Order matters: the publish to the destination
precedes the acknowledgement of the original, so a failed publish cannot lose the message
silently. Both `GcpPullMessageConsumer.Reject`/`RejectAsync` and
`GcpPubSubStreamMessageConsumer.Reject`/`RejectAsync` are bound.

> *Example (`Unacceptable`, both destinations configured).* `Reject(message, new
> MessageRejectionReason(RejectionReason.Unacceptable, "could not deserialize"))` on `GCP / Pull`
> publishes the message to `orders.Invalid`, then acknowledges the original. A read of
> `orders.Invalid` returns it; a read of `orders.DLQ` returns `MT_NONE`.
> *Example (`Unacceptable`, dead-letter only).* Same call with no invalid-message destination
> configured publishes to `orders.DLQ`, then acknowledges.
> *Example (`DeliveryError`).* `Reject(message, new
> MessageRejectionReason(RejectionReason.DeliveryError, "Handle count of messages reached"))`
> publishes to `orders.DLQ`, then acknowledges.
> *Example (`None`).* `Reject(message, new MessageRejectionReason(RejectionReason.None))` publishes
> to `orders.DLQ`, then acknowledges.

**R-17. A GCP `Reject` with no destination configured acknowledges the message and logs that fact at
Warning, naming the message id and the rejection reason.**
This is the one case in which a rejected GCP message is still discarded, and it must not be silent.

> *Example.* `Reject(message, new MessageRejectionReason(RejectionReason.Unacceptable, "bad
> payload"))` on a subscription with neither `DeadLetterRoutingKey` nor `InvalidMessageRoutingKey`
> logs a Warning containing the message id and `"Unacceptable"`, then acknowledges.

**R-18. A message routed by a GCP `Reject` carries the five rejection-metadata keys.**
`originalTopic` = the source topic; `originalMessageType` = the message type header as it was on
receive; `rejectionReason` = the enum name; `rejectionMessage` = the description, when a non-empty
description was supplied; `rejectionTimestamp` = an ISO-8601 round-trip (`"o"`) UTC timestamp. The
GCP-specific receipt handle is removed on the way out, so the routed copy does not carry a stale ack
id. Key names match the transport's existing casing convention.

> *Example.* A message rejected with `new MessageRejectionReason(RejectionReason.DeliveryError,
> "Handle count of messages reached; rejecting at limit")` from topic `orders` arrives on
> `orders.DLQ` with `originalTopic = "orders"`, `rejectionReason = "DeliveryError"`,
> `rejectionMessage = "Handle count of messages reached; rejecting at limit"`, a parseable
> `rejectionTimestamp`, an `originalMessageType`, and **no** `ReceiptHandle` key.

**R-19. If the routing publish fails, the original message is not acknowledged and the failure is
logged at Error.**
The message therefore becomes eligible for redelivery rather than being destroyed.

**"Not acknowledged" means released for prompt redelivery, by the same call that consumer's
`Requeue` already makes** — on `GcpPullMessageConsumer`, `ModifyAckDeadline(…, 0)`
(`GcpPullMessageConsumer.cs:349`, async `:384`); on `GcpPubSubStreamMessageConsumer`, a Nack via
`GcpStreamMessage.Reject()` (`GcpPubSubStreamMessageConsumer.cs:217-224`). It does **not** mean
leaving the message outstanding. On the stream consumer that would be a production hazard:
`SubscriberClient` keeps extending an uncompleted message's lease for up to 60 minutes, and the held
message occupies the client-wide flow-control slot (`1` by default), so one failed routing publish
would stall the whole subscription (the facts are recorded under R-13). Releasing the message
*replaces* the acknowledgement the success path makes. It is not an additional broker call, so
NFR-3 is unaffected.

GCP has no incumbent behaviour on this path — today it acks and discards unconditionally — so the
choice here is a greenfield one, and it is made in favour of preserving the message. The competing
risk, an undeliverable message looping indefinitely, is bounded by the delivery budget this spec
makes enforceable: a message that cannot be routed is redelivered, its count advances under R-1, and
R-4 terminates the loop.

`SqsMessageConsumer.RejectAsync` takes the opposite branch — on a failed DLQ send it deletes the
source (`:307-316`), which its own code comment records as deliberate infinite-loop avoidance. That
behaviour predates this spec and is **not changed here**; whether it should change is recorded as an
open question for a separate issue (see §Out of Scope). The two transports therefore differ on this
path until that question is answered, and the difference is recorded rather than implicit.

> *Example.* The dead-letter topic does not exist. `Reject(message, new
> MessageRejectionReason(RejectionReason.DeliveryError, "budget spent"))` logs an Error naming the
> message id and the reason, releases the message with the consumer's requeue call instead of
> acknowledging it, and the message is redelivered promptly — not after its ack deadline lapses, and
> on the stream consumer without holding the subscription's flow-control slot.

#### Group E — GCP DLQ channel creation without project IAM admin (#4354)

**R-20. Creating a DLQ-backed GCP channel succeeds when the calling principal cannot read or write
project-level IAM, and when no Cloud Resource Manager surface is reachable at all.**
`Unimplemented`, `PermissionDenied` and `Unauthenticated` from the IAM-related calls made during
subscription creation are tolerated: each is logged at Warning, and channel creation continues.

**A tolerated-call Warning names four things: the helper it abandoned, the RPC, the resource, and the
status code.** That list is fixed here and every other statement of it in this document — the bullets
below, NFR-5, AC-20 — refers to it rather than restating it. The **helper** name is not decorative:
it is what makes AC-20's "exactly two Warnings, one per helper" checkable at all.

**The unit of tolerance is the helper, not the RPC**, because the calls are not independent:
`GetProjectAsync` exists only to derive the member that the `GetIamPolicy`/`SetIamPolicy` calls then
consume (`GcpPubSubMessageGateway.cs:482-491`, `:534-543`), so continuing past a tolerated
`GetProjectAsync` with no member derived would carry a null into the binding (`:506-511`).
Therefore:

- a tolerated status from `GetProjectAsync` **abandons that whole IAM helper** — no binding is
  attempted, and one Warning is logged carrying the four elements above;
- a tolerated status from `GetIamPolicyAsync` **abandons the remainder of that helper**, so
  `SetIamPolicyAsync` is not called, with one Warning; a tolerated status from `SetIamPolicyAsync`
  ends that helper with one Warning;
- in every case the other helper still runs on its own terms, and subscription creation itself is
  unaffected — `EnsureSubscriptionExistsAsync` awaits each helper for effect only and never reads a
  result (`:235`, `:251`).

**Both helpers run whenever `DeadLetter != null`**, so a condition that fails for one fails for both:
the tolerated-call count, and therefore the Warning count, is **two** in the ordinary case, one from
each helper. A failure to *construct* the Resource Manager client —
credential resolution failing before any RPC is issued, so there is no status code to inspect — is
tolerated on the same terms and logged the same way.

`Unauthenticated` is in the tolerated set because it is the status the emulator actually produces.
`PUBSUB_EMULATOR_HOST` cannot redirect Cloud Resource Manager, which is a different service with its
own client, so `GetProjectAsync` leaves for real GCP and is rejected there; spec 0036 recorded this
measurement (`conformance-status.md`, §GCP). A tolerance list without it leaves channel creation
hard-failing one call *before* the `GetIamPolicy` case, and R-21's local bar unreachable.

No other status code is swallowed — `NotFound`, `InvalidArgument`, `DeadlineExceeded`,
`ResourceExhausted` and every other status still propagate, so a genuine misconfiguration is not
masked. The tolerance is scoped to the IAM-related calls tabulated below; it does not extend to
topic creation, subscription creation, or any call on the message path.

**R-20 covers both IAM helpers, not one.** The calls to tolerate are those made from
`GcpPubSubMessageGateway.EnsureSubscriptionExistsAsync` when `DeadLetter != null`:

| call site | method | RPCs |
|---|---|---|
| `:235` | `UpdateIAmRoleForDeadLetterAsync` (`:477`) | `ProjectsClient.GetProjectAsync`; `IAMPolicyClient.GetIamPolicyAsync` / `SetIamPolicyAsync` on the dead-letter topic |
| `:251` | `UpdateIAmRoleForSubscriptionAsync` (`:527`) | `ProjectsClient.GetProjectAsync`; `IAMPolicyClient.GetIamPolicyAsync` / `SetIamPolicyAsync` on the subscription |

⚠️ #4354's text names only the first. The second is called under the same `DeadLetter != null`
condition and makes the same two kinds of call, so tolerating only the first would still hard-fail
on the emulator one line later. Both are in scope.

> *Example (emulator, members not configured).* Against the Pub/Sub emulator
> (`gcr.io/google.com/cloudsdktool/cloud-sdk:emulators`, `PUBSUB_EMULATOR_HOST=localhost:8085`),
> `GetProjectAsync` returns `Unauthenticated` in both helpers. Each abandons its binding at that
> point, so `GetIamPolicyAsync` is never reached; two Warnings are logged, one per helper, and
> channel creation returns a usable channel.
> *Example (emulator, members configured).* With `DeadLetter.PublisherMember` and `SubscriberMember`
> set (C-11), no Resource Manager call is issued at all; only `GetIamPolicyAsync`'s `Unimplemented`
> is tolerated — **once in each helper, so two Warnings are logged**.
> *Example (real project, restricted principal).* Against a real project with a service account
> holding `pubsub.subscriber` and `pubsub.publisher` but not `resourcemanager.projects.get`,
> `GetProjectAsync` returns `PermissionDenied` in **both** helpers, each abandons its binding, two
> Warnings are logged, and channel creation succeeds.

**R-21. The GCP work in this spec is verified by a local, repeatable run against the Pub/Sub
emulator.**
`gcp-ci` against a real project is welcome as corroboration but is not the bar. A GCP requirement
whose only evidence is a cloud-only run is not "done" under this spec. This is why R-20 is scoped in
rather than deferred to #4354: it is the only route to a local proof.

> *Example.* `docker-compose -f docker-compose-gcp.yaml up -d`, then the scoped GCP conformance
> suite, then `docker-compose -f docker-compose-gcp.yaml down -v` and a repeat run, produce the same
> result twice from a clean store.

#### Group F — Non-regression

**R-22. The nine configurations that already satisfy conformance behaviour FR-23 continue to do so,
unchanged.**
Those are `Redis / RedisMessagingGateway`, `Kafka / Classic`, `Kafka / Consumer`,
`Kafka / PartitionKey`, `MSSQL / MSSQLMessagingGateway`, `PostgresSQL / PostgresMessagingGateway`,
`RMQ.Async / Classic`, `RMQ.Async / Quorum`, `RMQ.Sync / RmqSyncMessagingGateway`. **All nine**
requeue by republishing, so the header travels; the three RMQ rows differ only in that a rejected
message reaches its destination by broker-routed rejection (the DLX) rather than a Brighter-managed
send. They already satisfy R-1 to R-5,
and nothing in this spec alters their consumers, their subscriptions, their producers, or the
pump code they share.

> *Example.* Before and after this spec, `Kafka / Classic` with `requeueCount: 3` and a deferring
> handler produces three deliveries presenting counts `0`, `1`, `2`, a `DeliveryError` rejection,
> and a dead-lettered message with `HandledCount >= 2`. The same holds for the other eight.

**R-23. No conformance cell currently recorded as `Pass` or `Fixed` regresses, in any behaviour
column, for any configuration.**
Broader than R-22: the delivery-count contract touches the receive path of the transports in scope,
and that path serves all twelve conformance behaviours, not only FR-23. R-2 exists precisely because
this could otherwise break the identity assertions of FR-2, FR-15, FR-16 and FR-22 on eight AWS
cells.

> *Example.* `AWS / SqsStandard` currently reads `Pass` on FR-2, FR-4, FR-5, FR-6, FR-7, FR-8,
> FR-9, FR-15, FR-16, FR-17 and FR-22. After this spec all eleven still read `Pass`, and FR-23 moves
> from `Deferred` to `Fixed`.

**R-24. No public API currently shipped in V10 is removed, renamed, or has its meaning changed in a
way that breaks a compiling V10 consumer.**
Additions are permitted: new interface implementations on existing subscription types, new optional
constructor parameters appended with defaults, new log messages. `Subscription.RequeueCount`'s
default stays `-1`. `MessageHeader.HandledCount`'s type and name are unchanged.

**The evidence for R-24 is a committed artefact, not an inspection.** Compile-only samples are
committed, written against the V10 public surface as it stands before this spec, constructing the
affected types with the argument shapes a V10 application uses today. They are compiled by the
normal build, so a breaking change fails the build rather than awaiting review, and they are the
artefacts AC-27 names. `#pragma warning disable` is not permitted in them: a new obsoletion warning
must surface.

**Each sample lives in a project that already references the assembly it exercises.** No test project
gains a reference it does not have today, and no new project is created:

| Sample covers | Committed in | Already references |
|---|---|---|
| `Subscription`, `MessageHeader`, `Message` | `tests/Paramore.Brighter.Core.Tests/` | `Paramore.Brighter` |
| `SqsSubscription` | `tests/Paramore.Brighter.AWS.Tests/` | `…MessagingGateway.AWSSQS` |
| `SqsSubscription` (v4) | `tests/Paramore.Brighter.AWS.V4.Tests/` | `…MessagingGateway.AWSSQS.V4` |
| `GcpPubSubSubscription` | `tests/Paramore.Brighter.Gcp.Tests/` | `…MessagingGateway.GcpPubSub` |
| `RocketMqSubscription` | `tests/Paramore.Brighter.RocketMQ.Tests/` | `…MessagingGateway.RocketMQ` |

The samples are compile-only: they construct and assign, and assert nothing, so they neither need
broker infrastructure nor run as tests. `Paramore.Brighter.Core.Tests` references no messaging
gateway and must not acquire one for this purpose.

> *Example.* An application that today constructs
> `new SqsSubscription<MyCommand>(subscriptionName: ..., channelName: ..., routingKey: ...,
> requeueCount: 3)` compiles unchanged and continues to behave as before, except that its budget
> now runs down — which is the fix, not a break.

#### Group G — How the budget findings are surfaced

**R-25. The three budget-configuration findings are reported by startup pipeline validation, as
`ISpecification<Subscription>` rules, at `ValidationSeverity.Warning`.**
Brighter already has this seam and it is the right home: `ValidatePipelines()` resolves subscriptions
and collects `ISpecification<Subscription>` rules
(`BrighterPipelineValidationExtensions.cs:78-84`), and `ConsumerValidationRules`
(`Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`) is where consumer
subscription rules already live. ADR `0053-pipeline-validation-at-startup` established it precisely for "configuration mistakes …
only discovered at runtime". `Warning`-severity findings never block startup regardless of
`throwOnError`, which is the correct severity for all three: each names a *legal* configuration that
is probably not what its author meant. `ConsumerValidationRules.RequestTypeSubtype()` is the
existing precedent for a Warning-severity subscription rule.

| Rule | Fires when | The finding must name |
|---|---|---|
| **Zero or negative budget** (R-7) | `RequeueCount == 0` or `RequeueCount < -1` | the subscription, the configured value, and the two likely intents — `-1` (unlimited) or `1` (once only) |
| **Budget outranked by native redrive** (R-10) | `R != -1`, a native redrive limit `M` is configured, and `R >= M` | the subscription, `R`, `M`, and that the effective limit is `M` |
| **Budget structurally unenforceable** (R-11) | the transport cannot satisfy R-1 for this subscription | the subscription, `R`, and the reason the count cannot advance |

> *Example.* An application calling `.ValidatePipelines()` with a subscription configured
> `requeueCount: 0` starts successfully and its validation report contains one Warning naming that
> subscription, `0`, `-1` and `1`. With `requeueCount: 3` it contains no such Warning.

⚠️ **Open for the ADR: how a rule sees a transport-specific native limit — and there is prior art
that must be followed rather than re-invented.**

`RequeueCount` is on `Subscription` in **core** (`src/Paramore.Brighter/Subscription.cs:109`), so
R-7's rule is transport-agnostic and lands in `ConsumerValidationRules` as-is. `M` is not: it is
`SqsAttributes.RedrivePolicy.MaxReceiveCount` (`SqsAttributes.cs:110`) and
`GcpPubSubSubscription.DeadLetter.MaxDeliveryAttempts` (`DeadLetterPolicy.cs:47`), owned by
transport assemblies that neither core nor `ServiceActivator` may reference.

**This exact question is already decided**, for a different set of inputs, by
`0074-lifetime-validation-evaluation-site` on the unmerged branch `spec/scoped-lifetime-per-pipeline`
([PR #4282](https://github.com/BrighterCommand/Brighter/pull/4282), `Accepted`, implementation
complete). It establishes a two-rung ladder, and states the test for choosing a rung:

> *"An assembly that owns a concept contributes the rules for that concept… `ServiceActivator`
> already supplies four `ISpecification<Subscription>` rules through the container today. That seam
> works because `Subscription` is a core type. This ADR's entity types cannot be core types, so the
> pull moves up one level — from specifications to validators."*

| Rung | Use when | Mechanism |
|---|---|---|
| **Specification** | the entity the rule reads **is** a core type | the owning assembly registers an `ISpecification<Subscription>`; `ValidatePipelines()` already collects them |
| **Validator** | the entity **cannot** be a core type | the owning assembly registers its own `IAmAPipelineValidator` beside the core one; both hosts resolve `IEnumerable<IAmAPipelineValidator>` and `Combine(...)` |

ADR `0074-lifetime-validation-evaluation-site` took the upper rung only because its inputs were *container* concepts — service
descriptors, which can never be core types. **Ours are not**: `M` is an integer on a subscription,
and a core role interface exposing it (the idiom `IUseBrighterDeadLetterSupport` already
establishes, and which lives in core at `src/Paramore.Brighter/IUseBrighterDeadLetterSupport.cs`)
makes the entity a core type again and keeps the rule on the **lower** rung.

**R-11's input has a different shape from R-10's, and the ADR must place it explicitly.** R-10 reads
an integer, `M`. R-11 reads a per-subscription *capability* — "can the delivery count advance for
this subscription?" — whose answer depends on the transport, on transport-specific configuration
state (by A-1's worked example, a `GcpPubSubSubscription` with no `DeadLetterPolicy`), and on the
mechanism the ADR itself selects. The argument that returns R-10 to the lower rung does not carry
over on its own: an integer is trivially exposed by a core role interface, a capability judgement is
not until someone defines the predicate.

The ADR MUST therefore do one of two things for R-11, and record which:

- **Lower rung** — define the core-visible predicate the transport subscription answers (the shape
  `IUseBrighterDeadLetterSupport` already establishes: a core role interface the transport
  subscription implements, here exposing whether this subscription can enforce a delivery budget and
  why not when it cannot), so R-11's rule stays an `ISpecification<Subscription>` alongside R-7's
  and R-10's.
- **Upper rung** — register the rule as an `IAmAPipelineValidator` from the transport assembly, and
  record the #4282 sequencing dependency that follows.

The ADR MUST also name, for each of the four transports in scope, at least one subscription shape
that trips R-11 under its chosen mechanism — or state that none does. AC-11 reads that list, and
its second branch defines what "done" means for R-11 when the list is empty.

**The requirements therefore constrain the ADR as follows, without choosing for it:** the ADR MUST
adopt one of the two rungs above for each of the three rules and MUST record which and why. Introducing a *third* mechanism for
contributing a validation rule from another assembly is out of bounds — that is the specific
outcome this note exists to prevent. The lower rung is expected to suffice; if the ADR takes the
upper one it must say what the role interface could not carry.

⚠️ **Sequencing:** PR #4282 changes both validation hosts from resolving one validator to resolving
and combining every registered one. If this spec takes the upper rung it depends on that change and
therefore on #4282 merging; the lower rung does not. The ADR must state the dependency it incurs.

**R-26. R-11's finding is additionally logged once at channel creation; R-7's and R-10's are not.**
`ValidatePipelines()` is opt-in by design (ADR `0053-pipeline-validation-at-startup`, NFR-1), so a finding reported only there is
invisible to an application that never opts in. That is acceptable for R-7 and R-10, which describe
a configuration the author *chose* and can review. It is not acceptable for R-11, which describes
Brighter being unable to honour a setting it accepted — the exact silence this defect family exists
to close, and not the user's mistake. R-11 is therefore reported by both routes: the R-25 rule, and
a single Warning at channel creation, once per channel.

#### Group H — Conformance harness configuration

**R-27. The conformance harness is configured so that the Brighter budget fires ahead of any native
redrive limit on every configuration this spec moves, and so that the GCP providers reach channel
creation without a Cloud Resource Manager call, and so that the FR-23 behaviour can observe how many
times a message was delivered.**
Three changes, all to test-infrastructure code, all required for the behaviours above to be
exercised deterministically rather than raced or taken on trust.

**(a) `R < M` on all twelve providers** — four AWS, four AWS.V4, four GCP, each verified to hold the values in the table below as of 2026-09-21. Today `R == M` on both families (A-5), which is harmless only
while the budget is inert and becomes R-8's undetermined tie case the moment it works. The budget
must be strictly below the native limit so the Brighter route is the one the FR-23 behaviour
exercises:

| Provider files | Today | Required |
|---|---|---|
| `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/` — `SnsStandardMessageGatewayProvider.cs`, `SnsFifoMessageGatewayProvider.cs`, `SqsStandardMessageGatewayProvider.cs`, `SqsFifoMessageGatewayProvider.cs`, and the four same-named files under `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/` | `requeueCount: 3`, `maxReceiveCount: 3` | `R = 3`, `M = 5` |
| `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/` — `GcpPullMessageGatewayProvider.cs`, `GcpPullOrderingMessageGatewayProvider.cs`, `GcpStreamMessageGatewayProvider.cs`, `GcpStreamOrderingMessageGatewayProvider.cs` | `requeueCount: 5`, `MaxDeliveryAttempts: 5` | `R = 3`, `M = 5` |

`M = 5` is the lowest value Pub/Sub accepts (C-6), so `R <= 4` is forced on GCP; `R = 3` is chosen on
both families so the two read alike and so NFR-7's window is comfortably met.

**(b) The four GCP providers set both IAM members explicitly** — each to a **non-empty**
`serviceAccount:…`-shaped string — per C-11, so the Resource Manager call is not issued on the
emulator at all. Non-empty matters: the guard is `string.IsNullOrEmpty`, so `""` trips it *and*
fails to derive a replacement (the `??=` fallback does not fire for an empty string), which is the
one way to set the members and still reach the path R-27(b) exists to avoid. This is belt and braces with R-20, not a substitute for
it: R-20 governs what happens to an application that does *not* set them, which is the default
a user meets.

**(c) The shared conformance harness counts deliveries, and both FR-23 templates assert the count.**
R-4's obligation — that a message is not delivered again once its budget is spent — is only
observable if the harness can say how many times the message was dispatched to a handler. It cannot
today: `ConformanceDeferredPump` constructs a **fresh handler instance per dispatch**
(`ConformanceDeferredPump.cs.liquid:128`, `:152`) and the handler body only throws
`DeferMessageAction` (`:45-49`, async `:52-57`), so no count accumulates anywhere. R-27(c) therefore
requires:

1. **A dispatch count exposed by the shared pump**, keyed on the identity of the originating
   message, reached from a handler through `Context.OriginatingMessage`, which both pumps set before
   dispatch (`Reactor.cs:416`, `Proactor.cs:483`) and `PipelineBuilder` supplies to the handler
   (`:280`, `:323`).
   **The key is the originating message's `x-original-message-id` when it carries one, and its
   `Header.MessageId` otherwise** — the identity rule `ConformanceDeferredPump.AssertIsTheMessageSent`
   already applies, via `Message.OriginalMessageIdHeaderName`. `Header.MessageId` alone is **not**
   sufficient: a transport that requeues by republishing mints a fresh id per redelivery and records
   the first in `x-original-message-id` (`RmqMessagePublisher.cs:131`, `:142`), so a count keyed on
   `MessageId` alone would record `1` against each of three distinct keys and `count <= RequeueCount`
   would pass vacuously. That is the state of the three RMQ configurations, which R-27(c)(4)
   regenerates, so the fallback is load-bearing today and not a hypothetical.
   A transport that republishes under a fresh id is still **one** message for counting purposes.
   The key is also **not** the command's own id, which `CommandBody()` mints at serialise time
   (`ConformanceDeferredPump.cs.liquid:112-115`) and which is a different value from the one the
   message carries.
2. **Reset between tests**, so a count never leaks from one test to the next through a pump shared
   within a project.
3. **Read after the pump has been quit and awaited** — the instant R-4 defines as the close of the
   observation window. That is strictly later than the dead-letter poll's break, so a dispatch that
   happened between the message reaching the destination and the pump stopping is still counted.
   A test may also read the dispatch count **live** as a stop trigger — AC-1 quits the pump when it
   reaches 3 — but the value it asserts is the one read after quit and await.
4. **An assertion in both FR-23 templates** (`…/Reactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`
   and its Proactor twin) that the count is **`<= RequeueCount`**, and regeneration of the generated
   FR-23 tests for every configuration.
5. **Availability to the ACs outside FR-23 that also assert a count** — AC-1, AC-5, AC-6, AC-34's
   second clause, AC-35 and AC-42 — which run against bespoke subscriptions rather than a conformance
   provider (R-27(a) fixes all twelve providers at `R = 3`, so none supplies `-1`, `4`, `1`, `0`,
   `-3` or a short visibility timeout / ack deadline / invisible duration). The pump-driven ACs among
   them (all but AC-42) use the same counter and the same key; obligation 6 specifies the requeue
   observation they also make. AC-42 runs no pump, so it has no dispatch count: it uses only
   obligation 6's per-receive record, or reads the count directly from the returned message.
6. **A requeue observation, specified to the same degree.** AC-6's and AC-35's "`Requeue` is never
   called" is counted, not inferred: a recording consumer the test **composes around** the real
   consumer — a decorator implementing the same consumer interface, forwarding every call and
   counting `Requeue`/`RequeueAsync` by the key of obligation 1 — reset and read as obligations 2 and
   3 require. "Never called" is then `count == 0` for that message id. It is **not** a mock standing
   in for a transport (C-10 forbids that), **not** a counter added to production code, and **not**
   an inference from the absence of a redelivery inside some unstated interval.
   The same recording consumer also **records the integer value of `Header.HandledCount` at the
   moment its `Receive`/`ReceiveAsync` returns each message, before the pump sees it** — a value
   copied, not a reference to the message, because the pump then increments that same header
   (`MessageHeader.cs:572-575`) — in delivery order and by the key of obligation 1. That record
   is the per-delivery count AC-1, AC-34's second clause and AC-42 assert on; it is read as
   obligations 2 and 3 require (for a pump-less AC such as AC-42, after its last receive).

**"Dispatch count", "delivery count presented to the pump" and "handler invocation count" are used
interchangeably for this counter**: it advances once per dispatch of a message to a handler. It is
test-harness state and has no relationship to `MessageHeader.HandledCount`, which is the
delivery count of Group A.

Without (c) the invocation-count clauses of R-4, AC-3, AC-5, AC-6 and AC-35, and the per-delivery
counts of AC-1, AC-34 and AC-42, are unassertable, and the
FR-23 behaviour can only show that a message *reached* the DLQ, not that it stopped being
delivered.

> *Example.* After R-27(a), `AWS / SqsStandard` with a deferring handler exhausts its budget of 3 on
> the third delivery (or earlier on an approximate counter, R-4) and reaches the Brighter-managed DLQ; SQS's own counter reaches 3 of 5 and never
> redrives. Before R-27(a) the same run is R-8's tie and either route may fire.
> *Example.* After R-27(c), the same run asserts that the dispatch count for that message id stands
> at 3 or fewer — 3 on an exact counter, possibly fewer on SQS's approximate one (R-4) — when the pump
> has been quit and awaited (R-27(c)(3)), not when the DLQ poll breaks. Before R-27(c) the run cannot tell 3 deliveries from 30.

---

### Non-functional Requirements

**NFR-1. No additional broker round trip per delivery.** Whatever supplies the delivery count must
come from data already present on the receive response or already carried by the message. A
mechanism that issues an extra `GetQueueAttributes`, `GetSubscription`, or equivalent call per
received message is excluded.

> *Baseline to hold.* `SqsMessageConsumer` already requests `MessageSystemAttributeNames = ["All"]`
> on every receive (`:188-194`, V4 `:276`), so SQS's system attributes are on the wire at no extra
> cost. `grep -rn "ApproximateReceiveCount" src/` returns nothing — nothing reads them today.

**NFR-2. No new per-message heap allocation on the receive path.** Reading and normalising a
delivery count must not introduce a new collection, dictionary, string concatenation or boxed value
per received message beyond what the path already performs. Verified by measurement, not by
inspection: AC-37 fixes the method (an allocated-bytes delta over a fixed number of receives,
compared against the pre-change baseline) and the budget (no increase).

**NFR-3. No additional broker round trip per requeue.** A mechanism that satisfies R-1 by adding a
broker call on every requeue is permitted only if it replaces an existing call, not if it adds one.

**NFR-4. A budget that will not behave as its author probably intended is visible at Warning, at
most once per subscription per rule, and never per message.**
R-7 (a budget that rejects on the first delivery), R-10 (a budget outranked by a native redrive
limit) and R-11 (a budget that cannot run down at all) are the three instances, surfaced through
startup pipeline validation (R-25) and, for R-11 only, additionally at channel creation (R-26).
Budget exhaustion itself continues to log through the existing `Log.DroppingMessage` path.

**"Per rule", not "per subscription", because R-25's three rules are independent and their conditions
are not mutually exclusive.** A subscription with `requeueCount: 0` on a transport that cannot
satisfy R-1 trips R-7's rule *and* R-11's, and R-26 then adds R-11's channel-creation Warning on top.
Two validation findings plus one log line, for one subscription, is correct behaviour rather than a
breach of this NFR. What this NFR forbids is repetition **per message**: nothing on the receive path
raises a budget Warning, so a high-throughput consumer cannot be flooded by one. AC-29 counts the
channel-creation route; AC-7, AC-10 and AC-11 each count their own rule's finding.

**NFR-5. Tolerated IAM failures are visible and narrow.** Each tolerated `Unimplemented`,
`PermissionDenied` or `Unauthenticated` logs one Warning carrying the four elements R-20 fixes — the
helper abandoned, the RPC, the resource, and the status code. No `catch (Exception)` may be used to implement R-20 — the tolerance is by status code,
so an unrelated failure is never hidden. R-20's client-construction case, which carries no status
code, is scoped to the Resource Manager client construction alone and logs a Warning carrying the
same four elements, with the RPC element reading as the operation abandoned rather than a call made.
**The ADR MUST name the exception type caught there**: "the narrowest type that call site can throw"
is not a specification, and two implementers would choose two different types. A
tolerated failure never widens: the subscription is still created, and no other call is affected.

**NFR-6. The AWS v3 and v4 packages remain behaviourally indistinguishable.** Any divergence
introduced by this spec between `…MessagingGateway.AWSSQS` and `…MessagingGateway.AWSSQS.V4` is a
defect, per ADR `0038-aws-sqs-dlq-direct-send`'s dual-package constraint.

**NFR-7. The FR-23 behaviour completes inside the conformance template's observation window for
every configuration this spec moves.** The template polls the dead-letter destination every 500 ms
and gives up after 60 s. Any budget/lease combination this spec relies on must fit — e.g. RocketMQ's
10 s invisibility lease and a budget of 3 occupy ~30 s and fit; a budget of 10 on the same lease
would not. A configuration that cannot fit the window is a **harness defect to be fixed under
R-27**, not evidence about the transport and not grounds for leaving a cell `Deferred`.

Evidence is unconditional for the eight AWS cells (AC-12) and conditional for the rest, because two
of the three ACs are branch-guarded: AC-19 (the four GCP cells) applies only on the branch the ADR's
recorded conclusion selects (see the note under AC-40; AC-39 is a measurement, not a selector), and AC-24 (RocketMQ) only when R-14's condition holds. On the AC-40 or AC-25 branch the
corresponding configurations are not moved by this spec at all, so NFR-7 has nothing to evidence for
them — it constrains only the configurations a branch actually moves.

**NFR-8. Both variants, always.** Every behavioural requirement in Groups A–D is demonstrated in
both variants (see Terms, *Variant*): `Reactor` and `Proactor` where a pump runs, and the paired
synchronous and asynchronous consumer calls — sync with sync, async with async — where an AC drives a
consumer directly. Spec 0036's FR-14 rule: one variant is not evidence.

---

### Constraints and Assumptions

#### Settled inputs — not open for re-litigation here

- **C-1. ADR `0038-aws-sqs-dlq-direct-send`'s DLQ strategy stands.** `Reject` sends directly to the Brighter-managed DLQ and
  deletes the original; native redrive is not leaned on for rejected messages. ADR `0038-aws-sqs-dlq-direct-send` explicitly
  considered and rejected the visibility-trick alternative. This spec makes the *budget-exhaustion
  route into* that path reachable; it does not change the path.
- **C-2. This is one spec, not three.** #4341 is the family head. #4386 and #4353 become
  implementations of the contract settled here, plus #4386's own rejection-routing decision.
- **C-3. #4354 is in scope** (R-20), because it is the only route to C-4.
- **C-4. The GCP verification bar is a local emulator run** (R-21), not a `gcp-ci`-only run.
- **C-5. RocketMQ is conditional by design** (R-14). The branch is real and both sides have a
  defined "done".

#### Candidate mechanisms — inputs the ADR must weigh; this document chooses none

Listed here so the ADR starts from a complete set, and so no reader mistakes a requirement above for
an instruction to implement one of these.

| Mechanism | What is already true | What the ADR must weigh |
|---|---|---|
| **Read the broker's own delivery counter on receive** | SQS `ApproximateReceiveCount` is already on the wire (NFR-1). Pub/Sub exposes `ReceivedMessage.DeliveryAttempt` and `PubsubExtensions.GetDeliveryAttempt(PubsubMessage)` (Google.Cloud.PubSub.V1 3.36.0). RocketMQ exposes `MessageView.DeliveryAttempt` as a public property, set from `systemProperties.DeliveryAttempt` on receive (RocketMQ.Client 5.2.1). | All three counters are documented approximate/best-effort (R-3). The seams are `SqsMessageCreator.ReadHandledCount` (`:323`), `SqsInlineMessageCreator.ReadHandledCount` (`:350`), `GcpPubSub/Parser.ToBrighterMessage` (`:84`) and `ReadHandleCount` (`~:169`), `RocketMessageConsumer.ReadHandledCount` (`:422`). Note the origin offset: all three counters read `1` on first delivery, while R-2 requires `0`. **And note R-28**: the same counter is present on the *rejection destination's* own queue, so substituting it for the stamped header there would report that queue's count and destroy R-5's evidence — a mechanism on this row must discriminate a routed message from a source-channel delivery, and name how. |
| **Rewrite the stored message on requeue (republish)** — ⛔ **excluded by NFR-3, see C-12** | This is what the nine conforming transports do. | Nothing left to weigh: C-12 records the exclusion and its two reasons. Listed so the ADR does not re-open it, and so a future spec that revisits NFR-3 finds the argument rather than re-deriving it. |
| **Track the count consumer-side** | No broker dependency at all. | Does not survive a process restart or competing consumers; an unbounded in-memory map is an allocation and leak risk (NFR-2). |

#### Assumptions — each must be confirmed or refuted by the ADR or by measurement

- **A-1. Pub/Sub populates `delivery_attempt` only when the subscription carries a
  `DeadLetterPolicy`.** *Verified* against Google.Cloud.PubSub.V1 3.36.0's own XML documentation:
  *"Upon the first delivery of a given message, `delivery_attempt` will have a value of 1… If a
  DeadLetterPolicy is not set on the subscription, this will be 0."* `GetDeliveryAttempt` returns
  `null` in the same case. **Consequence**: if the ADR chooses the broker-counter mechanism for GCP,
  R-11's Warning is load-bearing for subscriptions without a `DeadLetterPolicy`.
- **A-2. The Pub/Sub emulator populates `delivery_attempt` on a DLQ-backed subscription, and
  advances it on each redelivery.** Both halves are required: a counter the emulator populates but
  never advances (`1, 1, 1`) fails R-1 exactly as an absent one does, and AC-39 measures the
  sequence, not merely its presence.
  *Not verified.* It cannot be verified until R-20 makes DLQ-backed channel creation possible on the
  emulator. **If it is refuted, the ADR must find a GCP mechanism that does not read
  `delivery_attempt`**; if none satisfies R-1 to R-5, R-13's second branch applies and GCP becomes
  *bound but unimplemented* with the four `GCP / *` FR-23 cells re-pointed and the measurement
  recorded. A-2 holding does not by itself select R-13's first branch: the ADR's recorded conclusion
  does (R-13). Falling back to a `gcp-ci`-only
  proof is not one of the available outcomes (R-21). The twenty GCP rejection-routing cells (R-15 to
  R-19) do not depend on A-2.
- **A-3. The RocketMQ broker increments `DeliveryAttempt` on a lease-lapse redelivery, with no
  `ChangeInvisibleDuration` call.** *Not verified.* This is exactly R-14's condition, and AC-23
  measures it.
- **A-4. SQS's `ApproximateReceiveCount` and Pub/Sub's `delivery_attempt` are approximate by their
  vendors' own documentation.** Hence R-3's two-tier obligation and R-4's "at most `R`" upper bound
  rather than an equality.
- **A-5. The existing harness configurations put the Brighter budget and the native redrive limit at
  the same value** — AWS `requeueCount: 3` / `maxReceiveCount: 3`
  (`SqsStandardMessageGatewayProvider.cs:104,108` and the three sibling providers), GCP
  `requeueCount: 5` / `MaxDeliveryAttempts: 5` (`GcpPullMessageGatewayProvider.cs:151,155` and its
  three siblings). Once the contract works these race (R-8's tie case), so the harness must be
  re-configured to `R < M` for the FR-23 behaviour to exercise the Brighter route deterministically.
  **Owned by R-27**, which names the twelve provider files and the required values.

#### Platform and repository constraints

- **C-6. Pub/Sub requires `DeadLetterPolicy.MaxDeliveryAttempts` to be between 5 and 100**
  (`DeadLetterPolicy.cs:47`). So on a DLQ-backed GCP subscription a Brighter budget must be `<= 4`
  to be reachable ahead of native redrive under R-8.
- **C-7. `DefaultMessageAssertion` asserts `HandledCount` equality**
  (`tools/Paramore.Brighter.Test.Generator/Templates/DefaultMessageAssertion.cs.liquid:59`), which is what makes R-2 non-negotiable.
- **C-8. `RocketMqSubscription` already implements both support interfaces**, so R-14's
  "bound but unimplemented" outcome requires no new binding work — only the Warning and the ledger
  entry.
- **C-9. RocketMQ local infrastructure is fragile** and `rocketmq-ci` has been commented out since
  #3696, so local is the only place RocketMQ ever runs. The dead-letter topics are shared and
  persistent; only `docker-compose … down -v` followed by `up -d` gives a clean store. Budget for
  this in R-14's measurement.
- **C-10. House conventions apply** (`.agent_instructions/testing.md`,
  `.agent_instructions/design_principles.md`): xUnit, `When_[condition]_should_[expected_behavior]`
  method and file naming, `[Behavior]Tests` class naming, one test case per file, Arrange/Act/Assert
  comments, no mocks used for isolation, Docker Compose for broker infrastructure, structural
  changes separated from behavioural ones, and the TDD review gear honoured.
- **C-11. Explicitly configured IAM members skip the Cloud Resource Manager call.** Both helpers
  derive the default Pub/Sub service account only when the member is unset:
  `UpdateIAmRoleForDeadLetterAsync` reads `DeadLetter.PublisherMember` (`DeadLetterPolicy.cs:27`) and
  `UpdateIAmRoleForSubscriptionAsync` reads `GcpPubSubSubscription.SubscriberMember`
  (`GcpPubSubSubscription.cs:102`); each calls `GetProjectAsync` only inside a
  `string.IsNullOrEmpty(...)` guard (`GcpPubSubMessageGateway.cs:482-491`, `:534-543`). Setting both
  therefore removes the Resource Manager dependency entirely. This is existing behaviour, not work:
  R-27(b) uses it in the harness, and R-20 covers the default case in which a user has set neither.
  *Verified in source 2026-09-21.*
- **C-12. NFR-3 stands, and it excludes the republish mechanism.** *Decided 2026-09-22.* No
  transport in scope has an in-place update API — not SQS, not Pub/Sub, not RocketMQ — so
  "rewrite the stored message on requeue" necessarily means delete-and-republish. That adds a net
  broker call per requeue on all three (SQS `ChangeMessageVisibility` → `SendMessage` +
  `DeleteMessage`; Pub/Sub `ModifyAckDeadline` → `Publish` + `Ack`; RocketMQ's requeue issues no
  broker call at all today), which is what NFR-3 forbids. Two reasons to keep NFR-3 rather than
  reword it:
  1. **Bypassing the broker's own redelivery is counter-intuitive for users.** A user who
     configured a visibility timeout or an ack deadline expects Brighter to use it. Republishing
     changes message ids, queue position and the broker's own receive metrics, none of which a
     requeue is expected to disturb.
  2. **SQS FIFO would break silently.** Content-based deduplication hashes the body, and a requeue
     changes only a header attribute, so a republished copy is discarded as a duplicate inside the
     dedup window unless Brighter mints a fresh `MessageDeduplicationId` per requeue — which it
     does not do today (`SqsMessageSender.cs:100-102` sets one only when the bag carries it). R-12
     puts all four FIFO configurations in scope, so this is not an edge case.

  **The cost of this decision, recorded so it is not rediscovered as a surprise:** the republish
  mechanism would have made the contract exact on every transport and dissolved both of this
  spec's conditionals — RocketMQ would satisfy R-1 to R-5 without waiting on the upstream
  `ChangeInvisibleDuration` fix, and GCP would not depend on the emulator populating
  `delivery_attempt`. Keeping NFR-3 keeps R-14's branch and A-2's risk live, and accepts that GCP
  FR-23 and RocketMQ FR-23 may both end at *bound but unimplemented*. R-13 and R-14 already define
  a "done" on both branches, which is what makes that acceptable.

  It also makes R-28 load-bearing: with republish excluded, a delivery count that is synthesised on
  read is the likely mechanism, and R-28 is what stops that synthesis destroying R-5's evidence.

---

### Out of Scope

Named explicitly so the boundary is deliberate rather than accidental.

- **#4351 — MQTT FR-23.** A `Proactor` sync-over-async deadlock: `MqttMessagePublisher`'s
  constructor blocks on its own connect and `MqttMessageConsumer` builds its requeue producer lazily
  inside `RequeueAsync`, on the pump's single thread. A different root cause from this family; the
  `MQTT / MqttMessagingGateway` FR-23 cell does not move here. Routed to `/bugfix`.
- **#4321 — GCP zero-delay requeue latency.** Separate issue.
- **#4387 — RMQ invalid-message destination.** It should *follow* whatever R-15 to R-19 settle for
  GCP rather than invent a second pattern, but it is not implemented here.
- **Changing ADR `0038-aws-sqs-dlq-direct-send`'s DLQ strategy** (C-1).
- **Changing SQS's failed-DLQ-send behaviour to match R-19.** `SqsMessageConsumer.RejectAsync`
  currently deletes the source message when the DLQ send throws (`:307-316`), which its own code
  comment records as deliberate infinite-loop avoidance. R-19 takes the opposite branch for GCP,
  which has no incumbent behaviour to unwind. **Whether the SQS branch should now change is an open
  question, not a settled endorsement**: it is raised as its own issue when this spec's requirements
  are approved, and the two transports differ on this path until that issue is answered.
- **Azure Service Bus.** It dead-letters natively, has no emulator in this repo, and is not part of
  this defect family. Its `Deferred` cells do not move here.
- **The RabbitMQ transports.** All three already satisfy FR-23 via the DLX; R-22 protects them and
  nothing else touches them.
- **The upstream RocketMQ C# client `ChangeInvisibleDuration` fix.** Not ours to make, and R-14
  exists so this spec does not wait on it.
- **Changing `Subscription.RequeueCount`'s default** from `-1` (R-6, R-24).
- **`RocketMessageConsumer.ReadDelay`'s header defect** — recorded in §Additional Context, not
  fixed here.
- **Any conformance cell not named in AC-30.** In particular, this spec does not claim the GCP
  FR-2, FR-7, FR-9, FR-15, FR-16 or FR-22 cells: R-20 may make them *runnable* locally for the first
  time, but a cell moves on evidence, and whatever such a run reveals is new information deserving
  its own issue.

---

## Acceptance Criteria

Each is written so it can become a test assertion as written. Both variants are required throughout
per NFR-8; where an AC names one variant in an example, the obligation covers both.

### Delivery-count contract

**Which transports these ACs bind.** AC-1, AC-42, AC-3, AC-4, AC-34's second clause and AC-41 are
unconditional for `AWSSQS` and `AWSSQS.V4` (R-12). For `GcpPubSub` and `RocketMQ` they apply only on
the branch R-13 / R-14 selects as *implemented*; on the *bound but unimplemented* branch, R-13(a)-(c)
and R-14(a)-(d) define "done" instead, and AC-40 (GCP) and AC-25 (RocketMQ) are the criteria that
apply. "Any / each transport in scope" below is read under this guard. AC-2, AC-5, AC-6 and AC-35
are not guarded. AC-2 because a first delivery presenting `0` is true today and must stay true on
both branches — the *bound but unimplemented* branch may still touch the receive path (AC-38), and
AC-2 is also R-23's criterion. AC-5, AC-6 and AC-35 because they need no count to advance: at `-1`
the budget is never consulted (`MessagePump.cs:171`), and at `0`, `1` or below `-1`
`HandledCountReached` is true on the first deferral whatever the transport presents.

**AC-1** (R-1, R-3) — **Given** a subscription on any transport in scope with `requeueCount: -1` — so
the budget cannot end the run before the third delivery, which at `requeueCount: 3` an approximate
counter may (R-4's example) — no native redrive limit at or below 3, on GCP a `DeadLetterPolicy`
with `MaxDeliveryAttempts: 5` where the ADR's GCP mechanism needs the policy (A-1), and a
handler that defers on every delivery, **When** the message is delivered three times — the test
quitting and awaiting the pump once the dispatch count (R-27(c)(1)) for that message reaches 3 —
**Then** the delivery count presented on each delivery, as recorded by R-27(c)(6)'s recording
consumer, is strictly greater than the count presented on the previous delivery.

**AC-42** (R-1) — the expiry path. **Given**, on each transport in scope — and on GCP, for **both**
consumer classes, `GcpPullMessageConsumer` and `GcpPubSubStreamMessageConsumer` (R-13) — a
subscription whose lease is short enough to lapse within the test, set through the knob that
actually governs it for that consumer: SQS, the queue's visibility timeout; GCP pull, the
subscription's `AckDeadlineSeconds` (`GcpPullMessageConsumer` does a raw `Pull` and never extends
the deadline); GCP stream, the configuration the ADR records under R-13's stream-consumer input (see
"The GCP stream consumer's expiry redelivery is an ADR input" under R-13); RocketMQ, the invisible
duration — no native redrive limit at or below 2, on GCP a `DeadLetterPolicy` with
`MaxDeliveryAttempts: 5` where the ADR's GCP mechanism needs the policy (A-1), and a message
published with `HandledCount = 0`, **When** the test receives the message through the transport's
consumer, neither acknowledges, rejects nor requeues it, waits for that timeout to lapse, and
receives it again — through `Receive` and, separately, `ReceiveAsync` (NFR-8) — **Then** the second
delivery presents a delivery count strictly greater than the first. No pump and no `Requeue` call
is involved, so a count advanced only inside `Requeue` fails this criterion. On RocketMQ this is the
lease-lapse redelivery AC-23 measures; AC-42 asserts what AC-23 records. On `GCP / Stream` and
`GCP / StreamOrdering` the procedure that drives the lapse and the second receive — and completes
the deliveries the test holds — is the one the ADR records under R-13, not one this criterion
prescribes; the obligation (a strictly greater count on the expiry redelivery) is unchanged. Where
the ADR records an alternative test for a stream configuration instead (R-13), AC-42 is met for that
configuration when that alternative test passes.

**AC-2** (R-2, R-23; see C-7) — **Given** a message published with `HandledCount = 0` to any transport
in scope, **When** it is delivered for the first time, **Then** the consumer presents
`Header.HandledCount == 0`, and the conformance identity assertion comparing sent and received
`HandledCount` passes.

**AC-3** (R-4) — **Given**, on each transport in scope and in both variants, `requeueCount: 3`, a
`deadLetterRoutingKey`, a handler that always defers, and no native redrive limit at or below 3 —
on GCP, a `DeadLetterPolicy` with `MaxDeliveryAttempts: 5` (C-6's floor; where the ADR's GCP
mechanism needs the policy, a subscription without one is AC-11's R-11 case, not AC-3's; where it
does not, AC-3 also applies to a GCP subscription with no `DeadLetterPolicy`) —
**When** the pump runs, **Then** the handler is invoked at
most 3 times, a rejection with `RejectionReason.DeliveryError` is issued exactly once, and the
handler invocation count for that message still stands at 3 or fewer when the FR-23 observation
window closes — the instant the pump has been quit and awaited, which the template already reaches
immediately after its 60 s poll, so no additional wait is introduced (R-4, R-27(c)(3)).

**AC-4** (R-5) — **Given** the run in AC-3, **When** the dead-letter destination is read, **Then** the
message is present, its `Header.HandledCount` is `>= 2`, and its bag carries `rejectionReason ==
"DeliveryError"`, a non-empty `rejectionMessage`, a parseable ISO-8601 `rejectionTimestamp`, an
`originalTopic` equal to the source topic, and an `originalMessageType`.

**AC-5** (R-6) — **Given**, on each transport in scope and in both variants, `requeueCount: -1`, no
native redrive policy (SQS: no `RedrivePolicy`; GCP: no `DeadLetterPolicy`; RocketMQ: no broker-side
max-retry dead-lettering within the run), and a handler that always defers, **When** the pump runs for 60 s, **Then** the handler is invoked more than 3 times, no `DeliveryError` rejection is issued,
and reading the dead-letter destination returns `MessageType.MT_NONE`.

**AC-6** (R-7) — **Given**, on each transport in scope and in both variants, `requeueCount: 1` and a
handler that defers on its first invocation, **When** the pump runs, **Then** the handler is invoked exactly once, `Requeue` is never called for
that message, a `DeliveryError` rejection is issued, and the message is on the dead-letter
destination.

**AC-7** (R-7, R-25) — **Given** a subscription configured `requeueCount: 0` in an application that
calls `.ValidatePipelines(throwOnError: true)`, **When** the host starts, **Then** startup succeeds,
and the validation result contains exactly one `ValidationSeverity.Warning` finding naming that
subscription, the value `0`, and both `-1` and `1` as the likely intents. **And Given** the same
application with `requeueCount: 3`, **When** the host starts, **Then** the validation result contains
no zero-budget finding.

**AC-33** (R-2, R-3, C-7) — **Given** the delivery-count normalisation the ADR specifies for a
transport whose broker counter originates at `1`, **When** it is applied to the broker values `1`,
`2` and `3`, **Then** it yields `0`, `1` and `2` respectively; **And Given** it is applied to an
absent counter or `0` (Pub/Sub on a subscription with no `DeadLetterPolicy`, A-1), **Then** it yields
`0`; **And** the result is never negative.
⚠️ **And Given** the ADR, **When** it is read, **Then** it states how R-2's exact `0` is achieved on a
first delivery whose approximate counter exceeds its origin, or records the residual risk and the
cells exposed to it. **This clause is a manual gate** — a document review, not an assertion.

**AC-34** (R-3) — ⚠️ **Given** the ADR, **When** it is read, **Then** it contains a four-row table
classifying `AWSSQS`, `AWSSQS.V4`, `GcpPubSub` and `RocketMQ` as exact or approximate, each with its
evidence, and no transport in scope is unclassified. **This clause is a manual gate** — a document
review, not an assertion. **And Given** each transport the table classifies **exact**, `requeueCount: 4` and a handler that defers on every delivery, **When** three
deliveries occur, **Then** they present counts of exactly `0`, `1` and `2`.

**AC-35** (R-7) — **Given**, on each transport in scope and in both variants, `requeueCount: 0` and a handler
that defers on its first invocation, **When** the pump runs, **Then** the handler is invoked exactly
once, `Requeue` is never called for that message, a `DeliveryError` rejection is issued, and the
message is on the dead-letter destination. **And Given** `requeueCount: -3`, **When** the pump runs,
**Then** the same outcome holds — enforcement is enabled and the first deferral rejects. **And Given**
`requeueCount: -1`, **When** the pump runs, **Then** no rejection is issued (AC-5), confirming `-1` is
the only value that disables the budget.

**AC-41** (R-28, R-5) — **Given**, on each transport in scope and in both variants, a run in which the
budget is exhausted and Brighter routes the message to its dead-letter destination, **When** that
destination is read through the ordinary consumer path the conformance providers use — a real channel
from `ChannelFactory`, not a bespoke reader — **Then** the message presents
`Header.HandledCount >= R - 1`, and on the Brighter-managed route `>= 3` for `requeueCount: 3` —
specifically `3` on a transport the ADR's R-3 table classifies **exact** (AC-34); **And** it does **not** present the value the rejection destination's own delivery counter
would yield after R-2's normalisation, which for a first read of that destination is `0`.
**And Given** the same read on a message a *native* redrive policy moved with no Brighter call (AC-9's
run), **Then** no count is asserted — R-28 does not bound that case.
⚠️ **And Given** the ADR, **When** it is read, **Then** it contains a four-row table recording, for
each transport in scope, how its mechanism satisfies R-28, naming the discriminator wherever one is
used. **This clause is a manual gate** — a document review, not an assertion.

### Native redrive interaction

**AC-8** (R-8) — **Given** an SQS subscription with `requeueCount: 3` and
`RedrivePolicy(maxReceiveCount: 5)` and a handler that always defers, **When** the pump runs,
**Then** the message reaches the Brighter-managed dead-letter destination carrying
`rejectionReason == "DeliveryError"`, and reading the native redrive target returns
`MessageType.MT_NONE`.

**AC-9** (R-8, R-9) — **Given** an SQS subscription **constructed by the test** (not taken from a
conformance gateway provider, whose values R-27 fixes at `R = 3`, `M = 5`) with `requeueCount: 10` and
`RedrivePolicy(maxReceiveCount: 3)` and a handler that always defers, **When** the pump runs,
**Then** the message reaches the native redrive target and the message found there carries **none**
of the keys `rejectionReason`, `rejectionMessage`, `rejectionTimestamp`, `originalTopic`,
`originalMessageType`.

**AC-10** (R-10, R-25) — **Given** a subscription **constructed by the test** (not taken from a
conformance gateway provider, whose values R-27 fixes at `R = 3`, `M = 5`) with `requeueCount: 5` and a
native redrive limit of `5`, in an application that calls `.ValidatePipelines()`, **When** the host starts, **Then** the
validation result contains a `ValidationSeverity.Warning` finding naming the subscription, `5` as
the budget, `5` as the native limit, and the statement that the effective limit is the native one;
and startup is not blocked. **And Given** `requeueCount: 3` with a native limit of `5`, **When** the
host starts, **Then** the validation result contains no such finding.

**AC-11** (R-11, R-25, R-26) — **Given** a subscription carrying `requeueCount: 3` of one of the
shapes the ADR names under R-25 as tripping R-11 — at minimum, if the ADR selects a counter-based
GCP mechanism, a `GcpPubSubSubscription` with no `DeadLetterPolicy` (A-1) — **When** the
host starts with `.ValidatePipelines()`, **Then** the validation result contains a
`ValidationSeverity.Warning` finding naming the subscription, the value `3`, and the reason.
**And Given** the same subscription in an application that does **not** call `.ValidatePipelines()`,
**When** the channel is created, **Then** exactly one Warning is logged naming the subscription, the
value `3`, and the reason, and no further such Warning is logged for that channel however many
messages are received.
**And Given** instead that the ADR states under R-25 that **no** subscription shape trips R-11 — which
R-25 explicitly permits — **When** R-11's rule and R-26's channel-creation log are nonetheless
implemented, **Then** each is exercised against a subscription that answers R-11's predicate
negatively and one that answers it positively, the negative producing no finding and no log line and
the positive producing exactly one of each; **And** the ADR's statement is recorded as the evidence
that no in-scope transport supplies a positive case in a real configuration.
R-11 is the anti-silence requirement (R-26), so it is built and verified on **both** branches: "no
shape trips it today" is not a licence to leave it unimplemented, because the predicate is what a
future transport answers.

### AWS

**AC-12** (R-12, R-23) — **Given** each of the eight configurations `AWS / SnsStandard`,
`AWS / SnsFifo`, `AWS / SqsStandard`, `AWS / SqsFifo`, `AWS.V4 / SnsStandard`, `AWS.V4 / SnsFifo`,
`AWS.V4 / SqsStandard`, `AWS.V4 / SqsFifo`, **When** the scoped conformance suite is run against
LocalStack in both variants, **Then** conformance behaviour FR-23 passes, and every behaviour column
that read `Pass` or `Fixed` before this spec still does.

**AC-13** (R-12, NFR-6) — **Given** the FR-23 run of AC-12, **When** the observable outcomes of the
v3 and v4 packages are compared for the same configuration, **Then** these are identical: the number
of deliveries **only if** the ADR's R-3 table (AC-34) classifies SQS **exact** — otherwise each
package's count is `<= R` and the two are not compared for equality, because an approximate counter
may reject on different deliveries in separately provisioned queues (R-4) — the rejection reason, the destination kind (Brighter-managed dead-letter versus native
redrive target), the **set** of rejection-metadata keys present, the values of `rejectionReason`,
`rejectionMessage` and `originalMessageType`, and the Warning messages.
**And** these are instance-specific and compared for presence and shape only, never for equality:
`originalTopic` and the destination's own name, because the two packages run against separately
provisioned topics and queues; and `rejectionTimestamp`, which differs on every run.

### GCP

**AC-14** (R-15) — **Given** a `GcpPubSubSubscription` constructed with a `deadLetterRoutingKey` and
an `invalidMessageRoutingKey`, **When** it is assigned to `IUseBrighterDeadLetterSupport` and
`IUseBrighterInvalidMessageSupport`, **Then** both assignments compile and both routing keys read
back the configured values.

**AC-15** (R-16, R-18) — **Given** each of the four GCP configurations, both consumers and both
variants, a `deadLetterRoutingKey` of `<topic>.DLQ` and an `invalidMessageRoutingKey` of
`<topic>.Invalid`, **When** `Reject` is called with each of `Unacceptable`, `DeliveryError` and
`None`, **Then** the message appears on `<topic>.Invalid` for `Unacceptable` and on `<topic>.DLQ`
for `DeliveryError` and `None`; in every case it carries `originalTopic`, `originalMessageType`,
`rejectionReason` and `rejectionTimestamp`, and carries `rejectionMessage` whenever a non-empty
description was supplied with the rejection reason (R-18) — so the `None` call made without a
description in R-16's example carries four keys, not five; in every case it carries no
`ReceiptHandle` key; and in every case a subsequent read of the source subscription returns
`MessageType.MT_NONE`.

**AC-16** (R-16) — **Given**, on each of the four GCP configurations and both consumers, a
subscription with a `deadLetterRoutingKey` and **no**
`invalidMessageRoutingKey`, **When** `Reject` is called with `RejectionReason.Unacceptable`,
**Then** the message appears on the dead-letter destination.

**AC-17** (R-17) — **Given**, on each of the four GCP configurations and both consumers, a
subscription with neither routing key, **When** `Reject` is called
with `new MessageRejectionReason(RejectionReason.Unacceptable, "bad payload")`, **Then** a Warning is
logged containing the message id and `"Unacceptable"`, and a subsequent read of the source
subscription returns `MessageType.MT_NONE`.

**AC-18** (R-19) — **Given**, on each of the four GCP configurations and both consumers, a
subscription whose configured dead-letter topic does not exist and whose ack deadline is longer than
the test's redelivery wait, **When** `Reject` is called with `RejectionReason.DeliveryError`,
**Then** an Error is logged naming the message id and `"DeliveryError"`, the original is **not**
acknowledged, and the message is delivered again **before its ack deadline would have lapsed** —
evidence that it was released by the consumer's requeue call (R-19), not left outstanding.

**AC-39** (R-13, A-2 — measurement) — ⚠️ **Given** a DLQ-backed GCP subscription on the Pub/Sub
emulator, made creatable by R-20, and a handler that always defers, **When** the message is delivered
three times, **Then** the value the emulator supplies for the broker delivery counter on each
delivery is recorded.
**AC-39 is a measurement, not a pass/fail gate** — it settles assumption A-2 and so determines which
mechanisms the ADR can reach for. It does **not** by itself select between AC-19 and AC-40; the ADR's
recorded conclusion does (see the note under AC-40). Its own exit criteria are assertable and all
four are required: (a) the observed values are written into `conformance-status.md`'s GCP paragraph;
(b) the ADR records whether A-2 held and, if it did not, which `delivery_attempt`-independent
mechanism it selected in consequence, or that none fits; (c) the ADR records either a GCP mechanism that satisfies R-1
to R-5 within NFR-1 to NFR-3, or that none does; (d) exactly one of AC-19 and AC-40 is then claimed,
selected by (c), and the other is recorded as not applicable with (c) and this measurement as the
reason.

**AC-19** (R-13 — the ADR records a satisfying GCP mechanism) — **Given** the ADR records a GCP
mechanism that **satisfies R-1 to R-5 within NFR-1 to NFR-3** — whether that is the broker counter,
because AC-39 recorded it populated and strictly advancing, or a `delivery_attempt`-independent
mechanism selected because AC-39 refuted A-2 — **And Given** each of `GCP / Pull`, `GCP / PullOrdering`, `GCP / Stream`,
`GCP / StreamOrdering` with `requeueCount: 3` and a `DeadLetterPolicy` with
`MaxDeliveryAttempts: 5`, in both variants, **When** a handler that always defers is driven by a
real pump, **Then** the handler is invoked at most 3 times and the message reaches the
Brighter-managed dead-letter destination carrying `rejectionReason == "DeliveryError"`.

**AC-40** (R-13 — the ADR records that no GCP mechanism satisfies) — **Given** the ADR records that
**no** mechanism satisfies R-1 to R-5 for GCP within NFR-1 to NFR-3, whatever AC-39 measured — the
ordinary route to which is AC-39 recording a counter that is unpopulated, or populated but **not**
strictly advancing across redeliveries (e.g. the sequence `1, 1, 1`), but a satisfying mechanism can
also fail to exist after a *good* measurement, e.g. where the counter advances but R-2's exact `0`
cannot be reached on an elevated first delivery — **When** a GCP channel is created with
`requeueCount: 3`, **Then** R-11's Warning is logged naming the subscription and the blocker;
**And** the four `GCP / *` FR-23 cells read `Deferred` re-pointed at the emulator limitation rather
than at #4240; **And** AC-39's measurement is recorded in `conformance-status.md`'s GCP paragraph;
**And** `GcpPubSubSubscription` still implements both support interfaces, so R-15 to R-19 are
unaffected and their twenty cells still move.

> **AC-19 and AC-40 partition every outcome, and the selector is the ADR's conclusion rather than
> AC-39's measurement.** The ADR either records a mechanism that satisfies R-1 to R-5 within NFR-1 to
> NFR-3 (AC-19) or records that none does (AC-40); AC-39(b) already requires it to say which, so
> exactly one applies and neither can be claimed alongside the other. The measurement chooses which
> *mechanism* the ADR can reach for, not which AC governs — which is why a counter that is populated
> and advancing does not by itself entitle the four cells to move, and why a refuted A-2 does not by
> itself condemn them.

**AC-20** (R-20, NFR-5) — **Given** the Pub/Sub emulator with `PUBSUB_EMULATOR_HOST` set and a
subscription carrying a `DeadLetterPolicy` with **neither** `DeadLetter.PublisherMember` nor
`SubscriberMember` configured, **When** a channel is created, **Then** channel creation succeeds and
returns a usable channel; the **`GetProjectAsync` step's tolerated outcome** — an `Unauthenticated`
or `PermissionDenied` status, **or** a failure to construct the Resource Manager client, whichever
the ambient credentials produce — is tolerated in **both**
`UpdateIAmRoleForDeadLetterAsync` and `UpdateIAmRoleForSubscriptionAsync`; each abandons its binding
at that point so `GetIamPolicyAsync` is not reached; and **exactly two** Warnings are logged, one per
helper, each carrying R-20's four elements.
> Which of the two shapes occurs is decided by ambient credentials, not by this spec, so AC-20 must
> not hard-assert the status. `GcpMessagingGatewayConnection.CreateProjectsClientAsync` (`:173`)
> builds `new ProjectsClientBuilder { Credential = Credential }`, which resolves Application Default
> Credentials when `Credential` is null: where ADC resolve, `GetProjectAsync` leaves and is refused
> with a status (spec 0036 measured `Unauthenticated`); where they do not — an ordinary laptop or CI
> agent running only the emulator, which is R-21's bar — construction throws and **no RPC is issued
> at all**. R-20 tolerates both on the same terms, and both must reach the same observable outcome.
**And Given** the same subscription with both members configured (C-11), **When** a channel is
created, **Then** no Resource Manager call is issued; `GetIamPolicyAsync`'s `Unimplemented` is
tolerated once per helper; **exactly two** Warnings are logged; and channel creation succeeds.

**AC-21** (R-20, NFR-5) — **Given** the status-code filter R-20 mandates, exercised directly as a
unit test rather than through a broker, **When** it is presented with an `RpcException` carrying
`NotFound`, `InvalidArgument`, `DeadlineExceeded` or `ResourceExhausted`, **Then** the exception is
rethrown unchanged and nothing is logged as tolerated; **And When** it is presented with
`Unimplemented`, `PermissionDenied` or `Unauthenticated`, **Then** it is tolerated and one Warning is
logged.
**And Given** R-20's client-construction case — a Resource Manager client that cannot be constructed
because credentials do not resolve, so there is no `RpcException` to inspect — **When** it is
exercised directly, **Then** it is tolerated, one Warning carrying R-20's four elements is logged,
the type caught is the one the ADR names (NFR-5), and nothing wider is caught. ⚠️ The
"type the ADR names" clause is not writable until the ADR exists.
> A broker-level negative case is not available and must not be written: when `DeadLetter != null`,
> `EnsureSubscriptionExistsAsync` creates the dead-letter topic and subscription with
> `OnMissingChannel.Create` before the IAM helpers run (`GcpPubSubMessageGateway.cs:220-236`), so
> `NotFound` cannot arise on that path; the emulator returns only tolerated codes; and C-10 forbids
> mocking a transport for isolation. Exercising the filter directly is what keeps R-20's
> "no other status code is swallowed" clause, and NFR-5, verifiable at all.

**AC-22** (R-21) — **Given** a clean emulator (`docker-compose -f docker-compose-gcp.yaml down -v`
then `up -d`), **When** the GCP conformance behaviours **FR-4, FR-5, FR-6, FR-8 and FR-17** (the
rejection-routing behaviours AC-15 exercises) are run twice across all four GCP configurations and
both variants with a clean store between runs — together with **FR-23** (AC-19) whenever AC-19 is the
branch that applies, which the ADR's recorded conclusion selects rather than AC-39's measurement (see
the note under AC-40) — **Then** both runs produce the same result, with no `gcp-ci` involvement.
These are the same behaviours AC-30's GCP rows move.

### RocketMQ

**AC-23** (R-14, condition) — ⚠️ **Given** `RocketMQ / RocketMQMessagingGateway` with `requeueCount: 3`
and a handler that always defers, **When** the message is delivered three times over roughly 30 s of
lease lapses with no `ChangeInvisibleDuration` call, **Then** the broker-supplied delivery-attempt
value observed on each delivery is recorded.
**AC-23 is a measurement, not a pass/fail gate** — both outcomes are legitimate, and the measurement
selects which of AC-24 and AC-25 applies. Its own exit criteria are assertable and all three are
required: (a) the three observed values are written into `conformance-status.md`'s RocketMQ
paragraph; (b) #4353 is updated with the same measurement; (c) exactly one of AC-24 and AC-25 is
then claimed, and the other is recorded as not applicable with AC-23's measurement as the reason.

**AC-24** (R-14, condition holds) — **Given** AC-23 recorded a strictly increasing sequence, **When**
the FR-23 behaviour is run in both variants against `docker-compose-rocketmq.yaml` from a store
cleaned with `down -v`, **Then** the message reaches the dead-letter destination inside the 60 s
window carrying `rejectionReason == "DeliveryError"`, and the `RocketMQ / RocketMQMessagingGateway`
FR-23 cell reads `Fixed`.

**AC-25** (R-14, condition does not hold) — **Given** AC-23 recorded a non-increasing sequence,
**When** a RocketMQ channel is created with `requeueCount: 3`, **Then** a Warning is logged naming the
subscription and the blocker; **And** the `RocketMQ / RocketMQMessagingGateway` FR-23 cell reads
`Deferred` pointing at the upstream `ChangeInvisibleDuration` blocker rather than at #4353; **And**
`RocketMqSubscription` still implements both support interfaces.

### Non-regression and cost

**AC-26** (R-22, R-23) — **Given** the nine configurations `Redis / RedisMessagingGateway`,
`Kafka / Classic`, `Kafka / Consumer`, `Kafka / PartitionKey`, `MSSQL / MSSQLMessagingGateway`,
`PostgresSQL / PostgresMessagingGateway`, `RMQ.Async / Classic`, `RMQ.Async / Quorum`,
`RMQ.Sync / RmqSyncMessagingGateway`, **When** their scoped conformance suites are run in both
variants against their compose files, **Then** FR-23 passes on all nine and no other cell that read
`Pass` or `Fixed` regresses.

**AC-27** (R-24) — **Given** the five compile-only V10 compatibility samples R-24 requires, each
committed in the project R-24's table names — `tests/Paramore.Brighter.Core.Tests/` for
`Subscription`, `MessageHeader` and `Message`; `tests/Paramore.Brighter.AWS.Tests/`,
`tests/Paramore.Brighter.AWS.V4.Tests/`, `tests/Paramore.Brighter.Gcp.Tests/` and
`tests/Paramore.Brighter.RocketMQ.Tests/` for `SqsSubscription`, `SqsSubscription` (v4),
`GcpPubSubSubscription` and `RocketMqSubscription` respectively — each exercising its types with the
argument shapes a V10 application uses today, no sample containing `#pragma warning disable`, no
new project created, and no test project having gained a reference, **When** the solution is built against the post-change
assemblies, **Then** it compiles with no new errors and no new obsoletion warnings — and a breaking
change fails the build rather than awaiting review.

**AC-28** (NFR-1, NFR-3) — **Given** the diff for each transport in scope, **When** the broker-call
sites on its receive and requeue paths are enumerated from that diff and compared with the same
enumeration on the base revision, **Then** the per-delivery and per-requeue call counts are equal,
and the enumeration for each of the four transports is recorded in the ADR.
⚠️ **Manual gate.** No automated suite proves AC-28; the recorded enumeration is its evidence.
NFR-2 is *not* covered by this AC — it is covered by AC-37.

**AC-29** (NFR-4) — **Given** a channel whose budget will not behave as its author probably intended
(the conditions of R-7, R-10 or R-11), **When** 100 messages are received on it, **Then** no Warning
about the budget is logged on the receive path at all, and the count of **channel-creation**
budget Warnings for that channel stands at exactly one if R-11's condition holds and zero
otherwise — R-26 gives the channel-creation route to R-11 alone.
**And Given** the same channel, **When** the count is taken again after a further 100 messages,
**Then** it is unchanged.
Validation findings are **not** counted here: AC-7, AC-10 and AC-11 each assert their own rule's
single finding, and a subscription that trips two rules is expected to produce two findings.

**AC-36** (R-27) — **Given** the eight AWS/AWS.V4 gateway providers and the four GCP gateway
providers named in R-27, **When** their configured values are read, **Then** every one satisfies
`R < M`: `requeueCount: 3` against a native limit of `5` on both families; **And Given** the four GCP
providers, **When** they are read, **Then** each sets `DeadLetter.PublisherMember` and
`SubscriberMember` to a non-empty `serviceAccount:…`-shaped string. **And Given** an FR-23 run on any of the twelve, **When** the budget is
exhausted, **Then** the message reaches the Brighter-managed dead-letter destination carrying
`rejectionReason == "DeliveryError"` — the native redrive target is not the route taken.
**And Given** R-27(c), **When** any generated FR-23 test is run, **Then** it asserts the harness's
dispatch count for the message under test, and a run in which the message was dispatched more times
than its budget allows fails on that assertion rather than passing on DLQ arrival alone.

**AC-37** (NFR-2) — **Given** a consumer on each transport in scope, **When** 1,000 messages are
received on the base revision and on the post-change revision, and the allocated-bytes delta per
received message is compared between them (e.g. by `GC.GetAllocatedBytesForCurrentThread()` around
the receive loop), **Then** the post-change figure is not greater than the base figure on every
transport in scope. **The tolerance is zero** — NFR-2's budget is "no increase", not "no significant
increase". To keep that assertable against allocation noise, the figure compared is the **median of
five runs** of 1,000 messages on each revision, and the comparison is `post <= base` on those
medians.

**AC-38** (R-22, R-23) — **Given** `RocketMQ / RocketMQMessagingGateway`, whose FR-4, FR-5, FR-6,
FR-7, FR-8, FR-9, FR-16, FR-17 and FR-22 cells currently read `Fixed`, **When** its scoped
conformance suite is run in both variants against `docker-compose-rocketmq.yaml` from a store cleaned
with `down -v`, **Then** all nine still pass — on **both** branches of R-14, because the
"bound but unimplemented" branch still touches `RocketMessageConsumer`'s receive path if the ADR
normalises counts there.

### Ledger

**AC-30** (all) — **Given** the work is complete, **When**
`specs/0036-universal-transport-conformance-tests/conformance-status.md` is read, **Then** the
following cells have moved, each on recorded evidence:

| Cells | From | To | Evidence |
|---|---|---|---|
| FR-23 × 8: `AWS / SnsStandard`, `AWS / SnsFifo`, `AWS / SqsStandard`, `AWS / SqsFifo`, and the four `AWS.V4` twins | `Deferred -> #4341` | `Fixed (#4341)` | LocalStack, both variants |
| FR-4, FR-5, FR-6, FR-8, FR-17 × `GCP / Pull`, `GCP / PullOrdering`, `GCP / Stream`, `GCP / StreamOrdering` — **20 cells** | `Deferred -> #4240` | `Fixed (#4386)` | Pub/Sub emulator, both variants |
| FR-23 × the four GCP configurations — **4 cells** | `Deferred -> #4240` | `Fixed (#4386)` **if** the ADR records a GCP mechanism that satisfies R-1 to R-5 within NFR-1 to NFR-3 (AC-19); otherwise `Deferred` re-pointed at the emulator limitation (AC-40, R-13) | Pub/Sub emulator, both variants |
| FR-23 × `RocketMQ / RocketMQMessagingGateway` — **1 cell** | `Deferred -> #4353` | `Fixed (#4353)` **if** R-14's condition holds; otherwise `Deferred` re-pointed at the upstream blocker | Local RocketMQ, both variants |

**33 cells** in total: **28 unconditional** — the 8 AWS/AWS.V4 FR-23 cells and the 20 GCP
rejection-routing cells, which depend on R-20 and not on A-2 — and **5 conditional**: the 4 GCP
FR-23 cells on the ADR's recorded conclusion about a satisfying GCP mechanism (R-13; A-2's
measurement chooses which mechanisms it can reach for), and the 1 RocketMQ FR-23 cell on R-14's condition. Each
conditional cell has a defined outcome on both branches, so "conditional" never means "unresolved".

⚠️ **Manual gate.** AC-30 and AC-31 are ledger edits; no automated suite proves them. Their evidence
is the conformance run each cell cites, recorded in the ledger beside the cell it moved.

**AC-31** (honesty) — **Given** the work is complete, **When** the ledger is read, **Then** it states
explicitly which cells this spec **could not** move and why:

- `MQTT / MqttMessagingGateway` FR-23 — a `Proactor` deadlock (#4351), a different root cause.
- `AzureServiceBus / AzureServiceBusMessagingGateway` FR-23 and its other `Deferred` cells — no
  local emulator exists; ASB dead-letters natively and is not in this defect family.
- The remaining GCP cells (FR-2, FR-7, FR-9, FR-15, FR-16, FR-22 × 4 configurations) — R-20 may make
  them locally runnable for the first time, but they move only on their own evidence, and any defect
  such a run reveals gets its own issue.
- `AWS / SqsFifo` and `AWS.V4 / SqsFifo` FR-9, `MSSQL` FR-16, `Redis` FR-16, `MQTT` FR-16,
  `RMQ.*` FR-5, `RocketMQ` FR-2 and FR-15 — unrelated to this family. (`MQTT / MqttMessagingGateway`
  therefore appears twice in this list: FR-16 here, FR-23 above.)

**AC-32** (R-25, R-26) — **Given** an application that calls `.ValidatePipelines(throwOnError: true)`
and whose only findings are budget-configuration findings from R-7, R-10 or R-11, **When** the host
starts, **Then** the host starts successfully — no `PipelineValidationException` is thrown — because
all three are `ValidationSeverity.Warning`, and Warning findings never block startup regardless of
`throwOnError`.

---

### Requirement → Acceptance Criteria map

Every requirement maps to at least one acceptance criterion.

| Requirement | Acceptance Criteria |
|---|---|
| R-1 | AC-1, AC-42 |
| R-2 | AC-2, AC-33 |
| R-3 | AC-33, AC-34 |
| R-4 | AC-3 |
| R-5 | AC-4, AC-41 |
| R-6 | AC-5 |
| R-7 | AC-6, AC-7, AC-35 |
| R-8 | AC-8, AC-9, AC-10 |
| R-9 | AC-9 |
| R-10 | AC-10 |
| R-11 | AC-11 |
| R-12 | AC-12, AC-13 |
| R-13 | AC-19, AC-39, AC-40 |
| R-14 | AC-23, AC-24, AC-25 |
| R-15 | AC-14 |
| R-16 | AC-15, AC-16 |
| R-17 | AC-17 |
| R-18 | AC-15 |
| R-19 | AC-18 |
| R-20 | AC-20, AC-21 |
| R-21 | AC-22 |
| R-22 | AC-26, AC-38 |
| R-23 | AC-2, AC-12, AC-26, AC-38 |
| R-24 | AC-27 |
| R-25 | AC-7, AC-10, AC-11, AC-32 |
| R-26 | AC-11, AC-32 |
| R-27 | AC-36 |
| R-28 | AC-41 |
| NFR-1 | AC-28 |
| NFR-2 | AC-37 |
| NFR-3 | AC-28 |
| NFR-4 | AC-29, AC-32 |
| NFR-5 | AC-20, AC-21 |
| NFR-6 | AC-13 |
| NFR-7 | AC-12, AC-19, AC-24 |
| NFR-8 | AC-1, AC-12, AC-15, AC-19, AC-26 |

**AC-30** (ledger movement) and **AC-31** (what this spec could not move) are deliberately not
mapped to a single requirement: they are cross-cutting exit criteria for the spec as a whole, and
every transport requirement in Groups C, D and E contributes evidence to them.

**Which criteria a green suite does not prove.** These are manual gates, each marked ⚠️ where it
appears:

- **AC-23** and **AC-39** — measurements that select a branch; both outcomes are legitimate, so
  neither can fail.
- **AC-28** — a recorded enumeration of broker calls.
- **AC-30** and **AC-31** — ledger edits.
- **The ledger clauses of AC-24, AC-25 and AC-40** — "the cell reads `Fixed`", "the cell reads
  `Deferred` pointing at the upstream blocker", "the four `GCP / *` FR-23 cells read `Deferred`
  re-pointed …" and "AC-39's measurement is recorded in `conformance-status.md`". The *behavioural*
  clauses of those three ACs are ordinary assertions; only their ledger clauses are gates.
- **AC-33's ADR-review clause**, **AC-34's first clause** and **AC-41's final clause** — document
  reviews of the ADR's first-delivery statement, its exact/approximate classification table, and its
  R-28 table. Every behavioural clause of AC-41 is an ordinary assertion; only that last clause is a
  gate.

Every other clause of every other AC is an assertion a test can make.

**Assertable, but not writable until the ADR exists** — these read a decision the ADR must first
record, so the test can be written only after design: **AC-11**'s Given (the subscription shapes that
trip R-11), **AC-21**'s client-construction clause (the exception type the ADR names, NFR-5),
**AC-33**'s first clause (the normalisation the ADR specifies), **AC-34**'s second
clause (the transports the ADR's table classifies exact), and **AC-42** on `GCP / Stream` and
`GCP / StreamOrdering` (the procedure the ADR records under R-13's stream-consumer input, or the
alternative test it records there instead).
**AC-19** and **AC-40** are a third kind: both are writable now, but *which one is claimed* is
selected by the ADR's recorded conclusion rather than by a measurement (see the note under AC-40).

---

## Additional Context

### How the family was found

All three defects were surfaced by spec 0036's conformance behaviour FR-23 (*requeue budget
exhausted to DLQ*), which is the only behaviour in that suite that drives a **real Brighter pump**
rather than calling `Requeue` on the channel. The budget is enforced in the pump and nowhere else, so
a test that requeues the channel directly cannot reach the code it is named for, however green it
goes. FR-22 requeues from the test's own thread and passes on all three transports; FR-23 is the
composition that fails.

On SQS the evidence was the **absence** of metadata: the message did reach the DLQ, but as
`handledCount=0` with no `rejectionReason`. `RefreshMetadata` stamps that metadata inside
`RejectAsync`, so its absence proved Brighter had never rejected the message — SQS redrive had moved
its own stored copy. R-9 preserves that diagnostic deliberately.

On RocketMQ the absence was total, and timing was ruled out by measurement rather than argument: at
a 30 s ceiling and again at a 150 s ceiling (roughly fifteen redeliveries against a budget of three),
the dead-letter poll returned `MT_NONE`. The budget does not run down slowly; it does not run down
at all.

### Where each defect lives

- `SqsMessageConsumer.RequeueAsync` (`:384`; V4 `:377`) — `ChangeMessageVisibilityAsync`, stored
  message never rewritten. `handled-count` is written only on send (`SqsMessageSender.cs:130`,
  `SnsMessagePublisher.cs:110`); a visibility change is not a send.
- `GcpPullMessageConsumer.Requeue`/`RequeueAsync` (`:335`, `:369`) — `ModifyAckDeadline(…, 0)`, same
  shape. `GcpPubSubStreamMessageConsumer.Requeue` (`:217`) — `GcpStreamMessage.Reject()`, same
  shape.
- `RocketMessageConsumer.Requeue` (`:179`) — resolves the `MessageView` and returns `true`; the one
  broker call is commented out pending an upstream C# client fix.
- `GcpPullMessageConsumer.Reject`/`RejectAsync` (`:276`, `:306`) and
  `GcpPubSubStreamMessageConsumer.Reject` (`:84`) — acknowledge and discard; `reason` bound, never
  read.
- `GcpPubSubMessageGateway.EnsureSubscriptionExistsAsync` (`:235`, `:251`) — the two IAM helpers R-20
  covers.

### Prior art this follows

ADR `0038-aws-sqs-dlq-direct-send` (SQS), `0039-redis-dlq-brighter-managed` (Redis) and
`0041-postgres-dlq-brighter-managed` (Postgres) are the three decisions that
settled Brighter-managed dead-lettering for destructive-ack transports. R-15 to R-19 take the same
decision again for GCP, and the argument for taking it again is the same one: Pub/Sub's
`DeadLetterPolicy` dead-letters on `MaxDeliveryAttempts` exhaustion, not on an explicit reject — and
an explicit reject *acks*, which is precisely the action that tells Pub/Sub the message was handled
successfully and stops the delivery count ever reaching the threshold. GCP has neither a native nor
a Brighter-managed route today. RMQ.Async, RMQ.Sync and Azure Service Bus are absent from the
support interfaces too, but they have an answer — they dead-letter natively — which is why their
FR-4/6/17 cells pass on routing alone under spec 0036's FR-8 relaxation. GCP has no such answer, and
that is the difference this spec closes.

### ⚠️ Incidental defect noticed, deliberately out of scope

`RocketMessageConsumer.ReadDelay` (`~:433`) reads `HeaderNames.HandledCount` where it means to read a
delay header:

```csharp
static TimeSpan ReadDelay(MessageView message)
{
    if (message.Properties.TryGetValue(HeaderNames.HandledCount, out var delayString)
        && TimeSpan.TryParse(delayString, out var delay))
```

A copy-paste defect, adjacent to `ReadHandledCount` (`:422`) which this spec does touch. It is
**not** folded into scope — it is a different behaviour with a different blast radius — but it is
recorded here so it is not lost. It should get its own `/bugfix`.

### The one trap this document was written against

Spec 0036's FR-7 carried an obligation whose prose was order-free but whose worked example said
"send M1 then M2". The generator template acquired an ordering assumption from the example and it
became a real bug. Every example above has therefore been checked against the obligation
immediately preceding it, and marked as illustrative where the risk was highest. Two places where a
detail was deliberately *lifted into the obligation* rather than left in an example: R-3's
exact-versus-approximate distinction (otherwise R-1's `0, 1, 2` example would have quietly narrowed
R-1 to exact counters), and R-4's "at most `R`" (otherwise the same example would have narrowed a
bound into an equality). One place where a detail was deliberately *stripped*: R-1's example says
nothing about wall-clock spacing between deliveries, because the obligation does not constrain it
and RocketMQ's 10 s lease would have been read as a requirement if it had appeared there.
