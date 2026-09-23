# 0037 — Delivery Count and Rejection Routing (the FR-23 family)

**Created:** 2026-09-21
**GitHub Issues:** [#4341](https://github.com/BrighterCommand/Brighter/issues/4341) (family head) ·
[#4386](https://github.com/BrighterCommand/Brighter/issues/4386) ·
[#4353](https://github.com/BrighterCommand/Brighter/issues/4353) ·
[#4354](https://github.com/BrighterCommand/Brighter/issues/4354) (folded in — see Scope)
**Next ADR:** `docs/adr/0077-…` — **not** 0072. `0070`–`0076` are all claimed by in-flight branches
(PR #4282 alone takes 0070–0076), and master already carries duplicate 0066/0067 pairs. Re-check
`git ls-tree origin/master docs/adr/` plus open PRs immediately before creating the file.
**Branch:** `feature/4341-delivery-count-and-rejection-routing` (created 2026-09-21 off `f906efc0b`)
**Worktree:** `/Users/ian.cooper/CSharpProjects/github/BrighterCommand/generator-transport-tests`

## Summary

Three issues, one root cause, plus one prerequisite that only GCP is missing.

**The shared root cause** — *a requeue that does not persist the delivery count cannot exhaust a
Brighter-side budget.* The pump enforces `RequeueCount` by calling
`message.Header.UpdateHandledCount()` and then `message.HandledCountReached(RequeueCount)`
(`Reactor.cs:494-509`, `Proactor.cs:500-513`). That only works when the redelivered message carries
the incremented count. Transports that requeue by **republishing** (Redis, Kafka ×3, MSSQL,
Postgres, RMQ ×3) carry it and their budgets run down. Transports that requeue by asking the broker
to re-serve its **own stored copy** do not:

| transport | requeue mechanism | consequence |
|---|---|---|
| AWS SQS / SQS.V4 (#4341) | `ChangeMessageVisibility` | budget inert; native `maxReceiveCount` redrive dead-letters instead, with **no rejection metadata** |
| GCP Pub/Sub (#4386) | `ModifyAckDeadline(…, 0)` | budget inert; `MaxDeliveryAttempts` is the only redrive |
| RocketMQ (#4353) | **nothing at all** (the one broker call is commented out) | budget inert **and** no broker redrive — the message is never dead-lettered by anyone |

**The GCP-only prerequisite** (#4386) — GCP `Reject` *acknowledges and discards*. No DLQ, no
invalid-message channel, `MessageRejectionReason` bound and never read, in both the Pull
(`GcpPullMessageConsumer.cs:276`, async `:306`) and Stream (`GcpPubSubStreamMessageConsumer.cs:84`)
consumers, across all four configurations × both variants. `GcpPubSubSubscription` does not
implement `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport`, which nine other
gateways do. Even a working delivery budget would have nowhere to send the rejected message.

## Why one spec and not three

Taken separately the repo acquires three different answers to the same question: *how does a
delivery count survive a requeue the broker does not rewrite?* The candidate answers — rewrite the
stored message, read the broker's own delivery counter, or track it consumer-side — each bind all
three transports, and the choice has different costs on each. [#4240's triage
comment](https://github.com/BrighterCommand/Brighter/issues/4240#issuecomment-5758194438) routes
#4341 as the family head for exactly this reason. This spec settles the contract once; #4386 and
#4353 become implementations of it, plus #4386's own rejection-routing decision.

## Scope

**In scope**

- The delivery-count contract: what `HandledCount` means on a delivered message, and what a
  transport must do on requeue so a Brighter-side budget can run down. Binds SQS, SQS.V4, GCP
  Pub/Sub and RocketMQ; must not regress the republishing transports.
- The interaction of a Brighter budget with a native redrive policy (`maxReceiveCount`,
  `MaxDeliveryAttempts`) — including the `min(requeueCount, maxReceiveCount)` consequence #4341
  calls out, and whether a message rescued by native redrive is allowed to arrive without rejection
  metadata.
- GCP Brighter-managed rejection routing (#4386): `GcpPubSubSubscription` implementing the two
  support interfaces, `GcpPubSubConsumerFactory` wiring the producers, both consumers re-publishing
  to the routing key selected by `reason` **before** acking the original, and stamping the standard
  `RejectionMetadataKeys` so FR-8 can assert for real instead of being relaxed.
- The conformance cells this closes or re-points: 8 AWS/AWS.V4 FR-23, 1 RocketMQ FR-23, and the GCP
  FR-4 / FR-5 / FR-6 / FR-8 / FR-17 cells (5 behaviours × 4 configs × 2 variants).

**Out of scope (named, so the boundary is deliberate)**

- Changing ADR 0038's DLQ strategy. It is settled and the path it specifies works; this spec makes
  the budget-exhaustion route *reachable*, nothing more.
- MQTT FR-23 (#4351) — a Proactor requeue **deadlock**, a different root cause, routed to `/bugfix`.
- #4321 (GCP zero-delay requeue latency) and #4354 (GCP DLQ channel creation needs project IAM
  admin) — separate issues, though **#4354 gates verification** (see below).
- RMQ's invalid-message destination (#4387), which should *follow* whatever #4386 settles rather
  than invent a second pattern.

## The two scope calls taken at Requirements

**1. #4354 is folded in, and the GCP bar is a local emulator run.** The 0036 ledger records that the
Pub/Sub emulator "implements neither of the two APIs the DLQ path needs", so without #4354 the #4386
half could only ever be proven on `gcp-ci` against a real cloud project. Tolerating `Unimplemented`
and `PermissionDenied` from the IAM calls is therefore first-class work here (R-20/R-21), not a
footnote. ⚠️ **#4354's own text is incomplete**: `GcpPubSubMessageGateway.cs:251` calls a *second*
IAM helper, `UpdateIAmRoleForSubscriptionAsync` (`:527`), under the same `DeadLetter != null`
condition and making the same calls — tolerating only the first would still hard-fail one line
later. Both are in scope. *Verified in the source, 2026-09-21.*

**2. RocketMQ is conditional by design** (R-14). If a redelivered RocketMQ message presents a
strictly increasing broker-supplied delivery-attempt value **without** any
`ChangeInvisibleDuration` call, it needs no upstream client fix and is implemented here. If not, it
stops at *bound to the contract but unimplemented*, with the blocker recorded and the measurement
written into the ledger. Both sides of the branch have a defined "done"; AC-23 measures it.
`MessageView.DeliveryAttempt` is public in the pinned RocketMQ.Client 5.2.1, so the condition is
plausibly satisfiable — but whether the *broker increments it on a lease lapse* is unverified,
which is exactly why the branch exists.

## Prior art to read before designing

⛔ **`0074-lifetime-validation-evaluation-site`** (unmerged, [PR #4282](https://github.com/BrighterCommand/Brighter/pull/4282),
branch `spec/scoped-lifetime-per-pipeline`) — **read before designing R-25's evaluation site.** It
already decided how a validation rule reaches `ValidatePipelines()` from an assembly core cannot
reference, and gives a two-rung ladder: an `ISpecification<Subscription>` when the entity is a core
type, a separately-registered `IAmAPipelineValidator` when it cannot be. R-25 is bound to one of
those two rungs; a third mechanism is out of bounds.

- [`0038-aws-sqs-dlq-direct-send`](../../docs/adr/0038-aws-sqs-dlq-direct-send.md) — SQS: direct DLQ send replaces
  `ChangeMessageVisibility`. Explicitly considered and **rejected** leaning on native redrive.
- [`0039-redis-dlq-brighter-managed`](../../docs/adr/0039-redis-dlq-brighter-managed.md) — Redis.
- [`0041-postgres-dlq-brighter-managed`](../../docs/adr/0041-postgres-dlq-brighter-managed.md) — Postgres.
- `specs/0036-universal-transport-conformance-tests/conformance-status.md` — the ledger; FR-23 rows
  and the GCP block are the measurements this spec has to move.

## Status Checklist

- [x] **Requirements** — drafted 2026-09-21, `requirements.md`. Now **28 `R-n`, 8 `NFR-n`, 41 `AC-n`**, full R→AC map, integrity-checked (no unmapped requirement, no dangling AC reference, no numbering gap). Revised same day to route the budget-configuration findings through `ValidatePipelines` (R-25/R-26), then again to remediate round 1 of adversarial review. **Not approved** — re-review owed.
- [x] **Adversarial Review (requirements) — round 1**, 2026-09-21. NEEDS WORK, 18 findings, 14 at or above threshold 60. All 14 remediated, plus 3 of the 4 below-threshold findings.
- [x] **Adversarial Review (requirements) — round 2**, 2026-09-21. NEEDS WORK, 12 findings, 8 at or above threshold 60 — **eight of the twelve were defects round 1's remediation introduced**. All 12 remediated ⚠️ **on paper only for five of them** — see the correction notice in round 2's log.
- [x] **Adversarial Review (requirements) — round 3**, 2026-09-22. NEEDS WORK, 14 findings, 9 at or above threshold 60. Its Critical finding was that five round-2 remediations had been logged but never written to the file; all five are now applied and verified in the document, along with all 14 of round 3's findings. Verdict on convergence was **not converging**, for that reason alone. All three rounds and their remediation logs are in `review-requirements.md`.
- [x] **Adversarial Review (requirements) — round 4**, 2026-09-22. NEEDS WORK, 13 findings. All 13 remediated; the decision it turned on (keep NFR-3, exclude republish) is recorded as C-12, and R-28 was added.
- [x] **Adversarial Review (requirements) — round 5**, 2026-09-22. NEEDS WORK, 6 findings, 4 at or above threshold 60. All thirteen round-4 remediations spot-checked present; finding 1 was a round-3 fix that landed in R-24 but not in its AC-27. All 6 remediated 2026-09-23 and verified against the file. **Re-run `/spec:review requirements` (round 6) before approving.**
- [ ] **Design (ADR)** — `/spec:design` (not started)
- [ ] **Adversarial Review (design)** — `/spec:review`
- [ ] **Tasks** — `/spec:tasks` (not started)
- [ ] **Implementation** — `/spec:implement` (not started — no code written)

**TDD gear:** `review-before` (armed by default). No `.current-gear` file exists for this spec.

**Numbering note:** requirements are `R-n`, not `FR-n`, because `FR-nn` already means a spec 0036
conformance behaviour and this document cites those throughout.
