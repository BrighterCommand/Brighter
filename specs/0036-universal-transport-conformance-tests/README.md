# 0036 — Universal Transport Conformance Tests (Reject / DLQ / Requeue-with-delay / Delayed-send)

**Created:** 2026-07-17
**GitHub Issue:** [#4240](https://github.com/BrighterCommand/Brighter/issues/4240)
**Branch:** `feature/4240-universal-transport-conformance-tests`
**Worktree:** `/Users/ian.cooper/CSharpProjects/github/BrighterCommand/generator-transport-tests`

## Summary

The messaging gateway test generator treats `HasSupportToDelayedMessages`,
`HasSupportToDeadLetterQueue` and `HasSupportToRequeue` as native-capability opt-in switches. In
reality these gate behaviour Brighter provides **universally** — `Reject`, `Requeue` and `Nack` are
on the channel interfaces for every transport, and where a transport has no native delay or DLQ,
Brighter supplies the behaviour itself. The flags are also mis-declared against the code: Kafka
declares all three `false` despite being the canonical grounding for these behaviours, and only
three of the twenty wired gateway configurations declare `HasSupportToDelayedMessages: true`
(`AWS/SqsStandard`, `AWS.V4/SqsStandard`, `RocketMQ`). The
Reject→DLQ / invalid-channel / delayed-requeue behaviours post-date the generator and were
hand-written per transport, producing broad but inconsistent duplication.

Goal: make these canonical behaviours a universal, **ungated** part of the generated suite, produced
identically for every transport in both Reactor and Proactor variants. That means extending the
generated provider interface to express DLQ and invalid-channel routing keys and the transport's own
rejection-metadata key names, retiring the three gates and removing them from every
`test-configuration.json`, deleting two defective templates rather than fixing them, and onboarding
the three gateway transports the generator does not wire today.

The suite proves *that* a behaviour holds, never *how* — native or Brighter fallback is not
asserted, and no `HasNative*` flag is reintroduced (OOS-1 rejects this explicitly). Where an ungated
template shows a transport does not conform, that is a defect in that transport's gateway and
fixing it is in scope.

## Status Checklist

- [ ] **Requirements** — rewritten 2026-07-19; adversarial review rounds 4–7 run, round 7 findings remediated. **Not approved** — round 8 owed.
- [x] **Design (ADRs `0066`, `0067`)** — both drafted, both `Proposed`. 0066: provider interface + gate retirement. 0067: rollout sequencing + deferral governance.
- [ ] **Adversarial Review** — requirements rounds 4–7 in `review-requirements.md`; design round 4 in `review-design.md`. A design pass is owed after the round-7 ADR edits.
- [ ] **Tasks** — `/spec:tasks` (not started)
- [ ] **Implementation** — `/spec:implement` (not started — no code written)

## Scope

Twelve targeted transports — every `src/Paramore.Brighter.MessagingGateway.*` project — counted per
*gateway configuration*, not per project. Nine are wired today, declaring twenty configurations
between them; AzureServiceBus, MQTT and RMQ.Sync are onboarded by FR-20.

Canonical behaviours the generator owns, ungated (FR-2, FR-4 … FR-9, FR-15, FR-16, FR-17, FR-22):
- Requeue with delay — redelivered after the delay, mechanism unasserted
- Plain requeue — `Requeue(M)` with no delay, redelivered and receivable again (FR-22)
- Explicit zero delay — `Requeue(M, TimeSpan.Zero)` is not special-cased (FR-15)
- Reject → DLQ (delivery-error, and the `None`/unspecified default arm)
- Reject → invalid/unacceptable channel
- Fallback ladder (unacceptable → invalid, else → DLQ; delivery-error → DLQ)
- No channels configured (acknowledge and continue)
- Rejection metadata stamping (universal semantic set, per-transport key names)
- Delayed send / `SendWithDelay`
- `Nack` releasing a message for redelivery

Related defects fixed in the same pass:
- The `with_delay` template calls `Requeue(received)` with no `timeout` argument — **deleted** (FR-12).
- The requeue-count-exhaustion template asserts a pump-owned behaviour, not a channel one —
  **deleted** (FR-19). Both deletions need a paired sweep of the 38 generated `.cs` copies checked in.
- Mis-declared gate values are not corrected but removed outright with the gates (FR-11).

Provider interface extension (FR-1): `CreateSubscription` takes nullable dead-letter and
invalid-message routing keys — replacing `bool setupDeadLetterQueue`, a breaking change across all
twenty existing providers — plus an invalid-channel read and a strongly-typed `RejectionMetadataKeys`
accessor. **No scheduler-carrying member**: asserting the delay mechanism is forbidden by NFR-3, and
14 of the 20 wired configurations have no scheduler seam. Scheduler-delegation testing is OOS-2.

**Gating lifecycle.** The three gates are not removed up front. Canonical templates are ungated *by
construction* — `SkipTest` consults the gates only for a closed list of the four legacy templates it
suppresses today — so canonical tests generate everywhere from the moment they land, even for Kafka,
which declares all three gates `false`. The four legacy templates are never ungated — each keeps
exactly the gating it has today until it is deleted, along with its checked-in generated copies (80
in total), and only then do the gates and config keys retire.

⚠️ *Never ungated* is not *never generated*. Most configurations declare these gates `true`, so **all
four** legacy templates generate **today** — the exhaustion template for 16 configurations, plain
requeue for 18, `with_delay` and delayed-message for 3 each — and keep generating on every
regeneration run until deletion. What the lifecycle guarantees is that they gain no **new** generation
site, not that they stop generating where they already do. FR-1(6) has a compile-time consequence
during that interim: the exhaustion template passes `setupDeadLetterQueue` positionally, so its 32
generated call sites break when the parameter is removed and are invisible to a search for the
parameter name.

Rollout is therefore generate-then-fix-then-clean-up, governed by a conformance ledger (FR-21) that
gates the terminal cleanup.

## Notes

Sibling generator defects: #4238 (single `Outbox` async-only), #4239 (`CollectionName` ignored
by sync outbox templates). Surfaced while writing `docs/factories/tests`.
