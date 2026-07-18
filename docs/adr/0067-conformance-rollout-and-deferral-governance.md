---
id: 0067-conformance-rollout-and-deferral-governance
title: "Universal Conformance Rollout Sequencing and Auditable-Deferral Governance"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-18
summary: "Sequences the universal messaging-gateway conformance rollout as fix-then-flip — per-transport gateway fixes land first (Kafka reference, then the DLQ-ADR transports, then the known-gap transports), with ADR 0066's gate-retirement flip merged last so master never goes red — and governs deferrals through a checked-in conformance ledger cross-checked against a mandatory greppable linked-issue Skip convention, with a size/risk fix-to-conform boundary."
tags:
  - "test-generation"
  - "testing"
  - "message-rejection"
  - "meta"
---

# 0067. Universal Conformance Rollout Sequencing and Auditable-Deferral Governance

Date: 2026-07-18

## Status

Proposed

## Context

Sibling ADR [0066](0066-conformance-test-provider-and-ungating.md) extends the generated provider
interfaces and, in a single change, retires the three capability gates
(`HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, `HasSupportToRequeue`) from
`MessagingGatewayGenerator.SkipTest` and removes the keys from every `test-configuration.json`. The
moment that change merges, **every** messaging-gateway transport the generator targets begins
generating the canonical conformance behaviours (FR-2, FR-4…FR-9, FR-15, FR-16, FR-17 — FR-3 having
been folded into FR-2 by ADR 0066) in both Reactor and Proactor variants — ungated.

The generator's target set is exactly the transports that declare a messaging-gateway
`test-configuration.json` with `HasSupportTo*` keys. Verified, that is **nine test projects** —
**AWS (V3 and V4), GCP, Kafka, MSSQL, PostgreSQL, Redis, RMQ.Async, and RocketMQ** — but generation
is per **gateway configuration**, not per project, and those nine projects declare roughly **twenty**
configurations between them: AWS.Tests and AWS.V4.Tests carry four each (SQS Standard/FIFO, SNS
Standard/FIFO), GCP four (Pull, PullOrdering, Stream, StreamOrdering), Kafka two (Standard,
PartitionKey), RMQ.Async two (Classic, Quorum), with the rest one apiece — matching the 20
`*MessageGatewayProvider.cs` implementations. **Conformance is therefore a per-configuration
property**, and the ledger below is keyed accordingly. Their conformance state is uneven:

- **Kafka already conforms** — it carries the richest hand-written suite
  (`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/`, Reactor + Proactor), which is the
  canonical grounding for the templates; yet it mis-declares **all three** gates
  (`HasSupportToRequeue`, `HasSupportToDeadLetterQueue`, `HasSupportToDelayedMessages`) as `false`
  in both its Standard and PartitionKey configs — the starkest case of a config tuned to keep the
  suite green rather than to describe the gateway.
- **The DLQ-ADR transports** — AWS SQS (`0038`), Redis (`0039`), MSSQL (`0040`), PostgreSQL
  (`0041`), and RocketMQ (`0042`) — already have Brighter-managed DLQ/reject decisions recorded
  per transport, but their end-to-end conformance against the generated suite is unproven; several
  mis-declare gates (AWS `HasSupportToDelayedMessages: false` in three of its four gateway
  configurations — `SqsStandard` declares `true` — and PostgreSQL both `false`). **RMQ**
  sits alongside them in the sequence but has **no** per-transport DLQ ADR — RabbitMQ conventionally
  uses a native dead-letter exchange (DLX), and its reject/DLQ conformance rests on the universal
  routing strategy (`0047-message-rejection-routing-strategy` / `0045-provide-dlq-where-missing`),
  not a Brighter-managed-DLQ decision of its own; its fix may therefore be larger than the
  DLQ-ADR transports' and is treated under the size/risk boundary accordingly.
- **The known-gap transports** are worst off: GCP has messaging-gateway providers and a
  requeue-to-DLQ generated test but none of the canonical reject-routing / scheduler-fallback /
  metadata behaviours; and Azure / Azure Service Bus, which the parent requirement calls "currently
  partial," in fact has **no** `test-configuration.json` at all
  (`tests/Paramore.Brighter.Azure.Tests` and `tests/Paramore.Brighter.AzureServiceBus.Tests`
  exist but are not in the generator's target set), so bringing it under generated conformance
  requires first adding a config and provider — the largest gap of all.

FR-13 forbids silently skipping, `[Skip]`-ping, or gating any canonical test to make the suite
green, and requires that any deferred gateway fix be a **named, linked, maintainer-signed-off
follow-up issue referenced from the spec** — "a deferral is auditable, never an open-ended escape
hatch." AC-13 restates this as "no silent skip and no unaudited deferral." C-1 confines the work to
the generator, its templates and configs, plus the transport-gateway source fixes FR-13 requires —
no public Brighter runtime API redesign beyond FR-1's generated providers. OOS-2 keeps
native-variant supplementary tests as separate follow-up issues.

That raises three questions ADR 0066 explicitly leaves open and this ADR answers:

1. In what **order** do transports reach conformance relative to the ungating flip?
2. **How** is a not-yet-conformant transport represented so a deferral is auditable rather than a
   silent skip?
3. **Where** is the boundary between fixing a gateway inline in this spec versus deferring it?

**Parent Requirement**: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)

**Scope**: This ADR decides rollout sequencing, the conformance-ledger + linked-issue Skip deferral
mechanism, and the fix-to-conform boundary (FR-13 / AC-13). The provider-interface extension and
gate retirement are decided in [ADR 0066](0066-conformance-test-provider-and-ungating.md);
individual template content and native-variant supplementary tests (OOS-2) are out of scope. This
ADR does **not** supersede 0066 — it sequences the rollout around it.

## Decision

We land universal conformance as **fix-then-flip**: per-transport gateway conformance fixes merge
first, in a defined order, and ADR 0066's gate-retirement flip is the **last** merge, once every
targeted transport is either green or carries a signed-off deferral — so master is never red. A
checked-in **conformance ledger** is the single source of truth for per-transport conformance
state, cross-checked against a mandatory, greppable **linked-issue Skip** convention. The boundary
between fixing inline and deferring is a **size/risk threshold**, maintainer-judged and recorded in
the ledger.

### Architecture Overview

The rollout is a pipeline whose fix phase and flip are deliberately distinct:

```
  FIX PHASE (each stage merges to a green master)                        CAPSTONE
  ---------------------------------------------------------------------  ------------------
  (i)  Kafka          reference — prove templates + extended provider  \
       (already        end-to-end against the known-good transport      |
        conforms)                                                       |
  (ii) DLQ-ADR        AWS SQS (0038), Redis (0039), MSSQL (0040),       |   ADR 0066
       transports      PostgreSQL (0041), RocketMQ (0042);             |   ungating flip
       (+ RMQ, which    RMQ (no per-transport DLQ ADR — native DLX,    |   (remove 3 gates
        has no DLQ ADR)  universal routing 0047/0045)                  |    + config keys)
                       — brought to conformance (Fixed) or deferred    |
                        (Deferred -> #issue)                           |
  (iii)known-gap      GCP (no canonical coverage today), Azure/ASB     |   merges LAST
       transports      (not yet in generator target set) — most new    |
                       work; conform or signed-off deferral            /
                                                                        |
       conformance-status.md (ledger)  <== single source of truth =====+
       transport x behaviour matrix; cells Pass | Fixed | Deferred->#issue
                cross-checked against in-code `Skip = "Deferred: #NNNN ..."`
```

The **ledger is the knowing responsibility** (Responsibility-Driven Design): the one information
holder that knows, per transport × canonical behaviour, whether that behaviour conforms as
generated (`Pass`), conforms via an in-spec gateway fix (`Fixed`), or is deferred to a signed-off
issue (`Deferred -> #issue`). No cell may read `Unknown` when the flip merges. The in-code Skip
marker is the *doing/enforcing* half — a greppable in-tree signal that a CI audit cross-checks
against the ledger so neither can drift from the other.

### Key Components

- **The conformance ledger** — a markdown matrix checked into the spec directory at
  `specs/0036-universal-transport-conformance-tests/conformance-status.md`. **A row is one gateway
  configuration**, identified as `project / configuration` (e.g. `AWS.V4 / SqsStandard`,
  `GCP / PullOrdering`, `Kafka / PartitionKey`) — roughly twenty rows across the nine projects, plus
  Azure/ASB once added — because generation and therefore conformance are per configuration, not per
  project. A per-configuration row is what lets the ledger express "SQS Standard passes FR-5 but SNS
  FIFO does not"; a project-level row could not. Columns = the canonical behaviours (FR-2, FR-4…FR-9,
  FR-15, FR-16, FR-17). **FR-3 is deliberately not a column** — ADR 0066 withdrew it as a mechanism
  assertion and folded it into a mechanism-agnostic FR-2. That matters here: had FR-3 survived, the
  14 of ~20 configurations with no scheduler seam would each have needed an `N/A (native)` cell,
  reintroducing exactly the native/non-native distinction OOS-1 rejects. With FR-2 mechanism-agnostic
  every column applies to every configuration, and the cell vocabulary below is sufficient. Each cell
  holds exactly one of:
  - `Pass` — conforms as generated, both Reactor and Proactor variants green;
  - `Fixed (#PR/commit)` — conformed via an in-spec gateway fix, linked to the PR/commit;
  - `Deferred -> #NNNN (sign-off: @maintainer)` — a named, linked, maintainer-signed-off follow-up
    issue.
  A transient `Unknown` is permitted only during the fix phase; the flip is blocked while any
  `Unknown` remains.
- **The greppable linked-issue Skip convention** — any deferred canonical test carries an explicit
  `Skip` string of the form
  `Skip = "Deferred: #NNNN — <behaviour> not yet conformant for <transport> (maintainer sign-off)"`.
  No such convention exists in `tools/.../Templates/MessagingGateway/` today, so this is new. A
  bare or reasonless `Skip`, or one whose value does not match the required `Deferred: #<n>`
  pattern, is a CI failure.
- **The CI audit check** — a read-only source scan over **in-tree artifacts only** (the
  messaging-gateway templates, the generated test tree, and the ledger) that (a) fails if any
  messaging-gateway test carries a `Skip` not matching `Deferred: #<n>`, and (b) cross-checks the
  in-tree links: every `Skip` must map to a ledger row marked `Deferred -> #issue`, and every
  `Deferred` ledger row must carry an issue link and a recorded sign-off entry. A `Skip` without a
  ledger row, or a `Deferred` row missing its issue link or sign-off entry, fails audit.
  Deliberately, the build does **not** query the live issue tracker for issue *state* (open/closed)
  or re-verify sign-off provenance — that coupling would make the build brittle against
  tracker changes and exceeds what FR-13 asks. Confirming the issue is genuinely open and the
  sign-off real is the **maintainer review gate's** job, not the build's; the build enforces only
  that the auditable in-tree trail (Skip ↔ ledger row ↔ issue link + sign-off entry) is present and
  consistent.
- **The sequencing order** — reference (Kafka) -> DLQ-ADR transports -> known-gap transports -> flip.
- **The class of in-spec gateway fixes** — the localized reject/requeue/scheduler-wiring changes of
  the same shape the per-transport DLQ ADRs (0038–0043, 0046) already established (`Fixed` rows),
  versus follow-up issues for anything needing a new subsystem or runtime API change (`Deferred`
  rows).

### Technology Choices

- **Fix-then-flip over flip-then-fix.** Landing the fixes first keeps master green at every merge;
  the flip (removing the three gates + config keys) becomes a single, atomic, reversible capstone
  whose blast radius is known. Flip-then-fix would put a red or `[Skip]`-riddled suite on master and
  normalize red — exactly what FR-13/AC-13 forbid.
- **Ledger + linked-issue Skip, cross-checked, over ledger-only or Skip-only.** Auditability is
  wanted from *both* directions: a single at-a-glance coverage matrix (the ledger) *and* an in-code,
  greppable marker at the exact deferred test (the Skip). Cross-checking the two is what makes a
  deferral provably owned rather than an open-ended escape hatch (FR-13). Ledger-only leaves the
  deferral invisible in the test tree; Skip-only gives no single coverage view and no sign-off
  provenance beyond a string.
- **Size/risk threshold over fix-everything or defer-everything.** A localized, low-risk gateway
  change (à la the DLQ ADRs) is fixed inline; anything requiring a new subsystem, a public runtime
  API change beyond FR-1's generated providers (forbidden by C-1), or a cross-cutting redesign is
  deferred. This bounds the spec while maximizing the conformance that actually lands at merge.

### Implementation Approach

1. **Seed the ledger.** Build `conformance-status.md` with every target transport × canonical
   behaviour set to `Unknown`. This is the work list and the flip gate.
2. **Prove the reference (Kafka).** Run the generated canonical suite against Kafka, whose
   hand-written suite already conforms, to validate the templates and the ADR-0066 extended provider
   interface end-to-end. Kafka's three mis-declared gates disappear with the 0066 config cleanup
   (the flip removes the keys outright). Mark both Kafka configuration rows (Standard,
   PartitionKey) `Pass`.
3. **Bring the DLQ-ADR transports to conformance.** For each of AWS SQS (0038), Redis (0039), MSSQL
   (0040), PostgreSQL (0041), and RocketMQ (0042) — and RMQ, which has no per-transport DLQ ADR and
   whose reject/DLQ conformance may be a larger fix — run the generated suite; for each
   non-conformant behaviour, apply the fix-to-conform boundary. If localized and low-risk (add a
   lazy DLQ/invalid producer, forward the scheduler to consumers, stamp the metadata semantic set
   under the transport's own keys), fix inline and record `Fixed (#PR)`. Otherwise open a follow-up
   issue, obtain maintainer sign-off, add the `Deferred: #NNNN` Skip to the generated test, and
   record `Deferred -> #issue`.
4. **Close the known-gap transports last.** GCP (no canonical coverage today) and Azure/ASB (not yet
   in the generator target set — adding a `test-configuration.json` and provider is a prerequisite,
   and is itself a candidate `Deferred` item if out of proportion to this spec). These carry the
   most new work and are sequenced last so the machinery is proven before it meets the hardest gaps.
5. **Merge the flip.** Only when the ledger has **no `Unknown` cells** — every behaviour is `Pass`,
   `Fixed`, or `Deferred -> #issue` — merge ADR 0066's ungating change (remove the three gates from
   `SkipTest`, the three properties from `MessagingGatewayConfiguration`, and the keys from every
   `test-configuration.json`).
6. **Enforce in CI.** The audit scan runs on every PR over in-tree artifacts only: it fails any
   messaging-gateway test whose `Skip` does not match `Deferred: #<n>`, and it fails if a `Skip`
   has no matching `Deferred` ledger row or a `Deferred` row is missing its issue link or sign-off
   entry. It does not query the issue tracker for live state — issue-open and sign-off validity are
   the maintainer review gate's responsibility.

**Reactor/Proactor parity applies to the ledger.** A behaviour is `Pass`/`Fixed` for a transport
only when **both** the Reactor and Proactor variants pass (FR-14); if only one variant conforms, the
cell stays `Unknown` (blocks the flip) or is split into a deferral for the lagging variant.

**Infra reality.** `Pass` means the generated suite actually *ran* against the transport's broker
(container/emulator), not merely compiled. Timing-sensitive behaviours use NFR-2 bounded
receive-retry loops so broker propagation delay does not produce false failures.

## Consequences

### Positive

- Master is never red: every merge in the fix phase leaves a green suite, and the flip is a single
  atomic, reversible capstone.
- Universal conformance is provably **complete or auditably deferred** at merge — AC-13's "no silent
  skip and no unaudited deferral" is enforced mechanically, not by convention.
- The ledger gives at-a-glance per-transport coverage and doubles as the flip gate: the flip cannot
  land while any cell is `Unknown`.
- Deferrals cannot rot into silent gaps — each is a named, linked, signed-off issue that CI keeps
  honest against both the ledger and the in-code Skip.
- Proving the machinery on the known-good transport (Kafka) first de-risks the templates and the
  extended provider before they meet the hardest gaps (GCP, Azure/ASB).

### Negative

- Fix-then-flip **serializes** the rollout behind the flip, so universal ungated generation is not
  visible on master until late — the payoff is deferred to the capstone.
- The ledger, the Skip convention, and the CI audit are **new machinery** to build and maintain
  (none exists today), adding process weight to the spec.
- The size/risk fix-to-conform boundary is a **judgement call** that can be contested per transport ×
  behaviour; the heuristic reduces but does not remove disagreement.
- A long fix phase risks **drift** between the templates/provider interface (evolving under 0066's
  tasks) and the transport gateways being fixed against them.
- Azure/ASB needing a config + provider before it can even be generated means its conformance may
  realistically land as a signed-off deferral rather than inline — universal generation is
  "complete or deferred," not necessarily "complete," for that transport at flip time.

## Risks and Mitigations

- *Risk*: the deferral list grows unbounded, hollowing out "universal." *Mitigation*: every deferral
  is a ledger `Deferred` row with an open, linked, maintainer-signed-off issue — growth is visible,
  owned, and audited; the sign-off gate is where scope creep is caught.
- *Risk*: a transport needs infrastructure (containers/emulators) that is flaky or unavailable, so
  conformance can't be proven. *Mitigation*: `Pass` requires the suite to actually run, not just
  compile; NFR-2 bounded retries absorb propagation jitter; a transport that genuinely can't be
  exercised in CI becomes a signed-off `Deferred` row rather than a silently skipped test.
- *Risk*: the flip is delayed indefinitely behind stragglers. *Mitigation*: the ledger surfaces
  exactly which transport × behaviour cells remain `Unknown`, so the block is explicit and
  actionable; a straggler can be converted to a signed-off deferral to unblock the flip without
  hiding the gap.
- *Risk*: template/gateway drift over a long fix phase. *Mitigation*: the provider interface is
  generated and compiled against each hand-written provider (per 0066), so drift is a build break,
  and the ledger is re-run per transport as the templates settle.

## Alternatives Considered

1. **Flip-then-fix** — retire the gates first, then chase conformance. Rejected: master carries a
   known-failing or `[Skip]`-riddled suite for the duration, normalizing red and violating
   FR-13/AC-13; the ungating "win" is illusory while transports are non-conformant.
2. **Reference-first then big-bang** — prove Kafka, then land all remaining fixes plus the flip in
   one enormous final merge. Rejected: one unreviewable, un-bisectable merge; a single regression
   anywhere blocks the whole rollout.
3. **Ledger-only, no in-code marker** — track deferrals solely in `conformance-status.md`. Rejected:
   a deferral is invisible in the test tree, so a reader of the suite cannot tell a passing test from
   a quietly deferred one without consulting an external document; no greppable enforcement point.
4. **Skip-with-issue only, no ledger** — require the `Deferred: #<n>` Skip but keep no matrix.
   Rejected: no single coverage view, and sign-off provenance is reduced to whatever a string
   asserts; there is nothing to cross-check the Skip against.
5. **Fix-everything-in-spec** — forbid all deferrals; conform every transport inline. Rejected:
   risks an unbounded, long-running spec, and some gaps (Azure/ASB config+provider, subsystem-level
   changes, runtime API changes forbidden by C-1) genuinely belong in follow-up work.
6. **Defer-all-gateway-fixes** — flip and open issues for every non-conformance. Rejected: universal
   conformance would be almost entirely promissory at merge, defeating the spec's purpose of proving
   transports honour the universal obligations.

## References

- Requirements: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)
- Related ADRs (this ADR builds on all of these; it supersedes none):
  - [`0066-conformance-test-provider-and-ungating`](0066-conformance-test-provider-and-ungating.md) [Proposed] — **sibling; the most important relation.** Extends the provider interfaces and retires the three gates + config keys in one change; this ADR sequences the per-transport rollout *around* that flip and governs deferrals. 0067 does not supersede 0066.
  - `0037-add-messaging-gateway-generated-test` [Accepted] — created the generator and `MessagingGatewayConfiguration` this whole spec extends; the target-set and `SkipTest` gating originate here.
  - `0047-message-rejection-routing-strategy` — the `Reject()` fallback ladder and origin-metadata contract; a transport is "conformant" in the ledger precisely when it honours this.
  - Per-transport DLQ ADRs — collectively the class of localized, in-spec `Fixed` changes and the reason these transports sequence ahead of GCP/Azure: `0038-aws-sqs-dlq-direct-send`, `0039-redis-dlq-brighter-managed`, `0040-mssql-dlq-brighter-managed`, `0041-postgres-dlq-brighter-managed`, `0042-rocketmq-dlq-brighter-managed`, `0043-mqtt-dlq-brighter-managed`, `0046-kafka-dlq-producer-for-requeue`.
  - `0037-universal-scheduler-delay`, `0039-transport-scheduler-wiring`, `0045-provide-dlq-where-missing` — the runtime mechanisms (scheduler-backed delayed requeue, channel-factory scheduler wiring, Brighter-managed DLQ/invalid channels) that conformance depends on and the in-spec fixes wire up.
- External references: none.
