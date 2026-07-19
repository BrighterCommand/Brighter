# 0036 — Universal Transport Conformance Tests (Reject / DLQ / Requeue-with-delay / Delayed-send)

**Created:** 2026-07-17
**GitHub Issue:** [#4240](https://github.com/BrighterCommand/Brighter/issues/4240)
**Branch:** `feature/4240-universal-transport-conformance-tests`
**Worktree:** `/Users/ian.cooper/CSharpProjects/github/BrighterCommand/generator-transport-tests`

## Summary

The messaging gateway test generator treats `HasSupportToDelayedMessages` and
`HasSupportToDeadLetterQueue` as native-capability opt-in switches. In reality these gate
behaviour Brighter provides **universally** (scheduler fallback for delayed send/requeue;
Brighter-provisioned DLQ / invalid-message producer driven by the `Reject` flow). The flags
are also mis-declared against the code (Postgres, SQS). The Reject→DLQ / invalid-channel /
requeue-via-producer / requeue-via-scheduler behaviours post-date the generator and were
hand-written per transport, producing broad but inconsistent duplication.

Goal: distil the hand-written tests into a canonical, ungated set of generator templates that
enforce these as universal Brighter conformance obligations, extend the provider interface to
supply the required consumer/scheduler wiring, retire the opt-in gates, fix the broken
`requeuing_with_delay` template, and (optionally) reintroduce genuinely *native* behaviour as
distinct `HasNative...` flags.

## Status Checklist

- [x] **Requirements** — ✅ APPROVED 2026-07-18 (`/spec:review` ×2 + coverage expansion: Nack FR-16, unknown-reason→DLQ FR-17, Kafka surface-area reconciliation)
- [ ] **Design (ADR `0066`)** — `/spec:design` (not started) — issue notes this is design-led and likely wants an ADR for the provider-interface change
- [ ] **Adversarial Review** — `/spec:review` (not started)
- [ ] **Tasks** — `/spec:tasks` (not started)
- [ ] **Implementation** — `/spec:implement` (not started)

## Scope (from issue #4240)

Canonical behaviours the generator should own (ungated):
- Requeue with delay — via producer, and via scheduler fallback
- Reject → DLQ (Brighter-provisioned)
- Reject → invalid/unacceptable channel
- Fallback ladder (unacceptable → invalid, else → DLQ; delivery-error → DLQ)
- No channels configured (acknowledge/delete + log, per transport semantics)
- Rejection metadata stamping
- Delayed send / `SendWithDelay`

Related defects to fix in the same pass:
- Broken `requeuing_with_delay` template calls `Requeue(received)` with no `timeout` argument.
- Correct mis-declared configs (Postgres, SQS) when retiring/repurposing the flags.

Provider interface extension: a consumer configured with dead-letter and invalid-message
routing keys; InMemoryScheduler wired to the producer for these tests.

## Notes

Sibling generator defects: #4238 (single `Outbox` async-only), #4239 (`CollectionName` ignored
by sync outbox templates). Surfaced while writing `docs/factories/tests`.
