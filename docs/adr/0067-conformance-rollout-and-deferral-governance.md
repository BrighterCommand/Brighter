---
id: 0067-conformance-rollout-and-deferral-governance
title: "Universal Conformance Rollout Sequencing and Auditable-Deferral Governance"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-18
summary: "Sequences the universal messaging-gateway conformance rollout as generate-then-fix-then-clean-up — canonical templates land early and generate everywhere, each carrying an audited deferral marker for configurations not yet proven; per-transport gateway fixes then clear those markers in order (Kafka reference, then the DLQ-ADR transports, then the known-gap transports and FR-20 onboardings); and ADR 0066's legacy-template retirement and gate removal merges last so master never goes red — with deferrals governed by a checked-in conformance ledger cross-checked against a mandatory greppable linked-issue Skip convention, and a size/risk fix-to-conform boundary."
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
interfaces and decides the **gating lifecycle**: canonical templates are ungated *by construction* —
`MessagingGatewayGenerator.SkipTest` consults the three capability gates
(`HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, `HasSupportToRequeue`) only for a
closed list of four legacy templates — and the gates themselves, plus the keys in every
`test-configuration.json`, are removed only after those legacy templates are deleted.

**This means canonical generation does not wait for a flip.** From the moment the first canonical
template merges, it generates for **every** targeted gateway configuration (FR-2, FR-4…FR-9, FR-15,
FR-16, FR-17, FR-22 — FR-3 having been folded into FR-2 by ADR 0066) in both Reactor and Proactor
variants, whatever any configuration's flags say. The four legacy templates are never ungated
throughout — they keep exactly the gating they have today and reach no new configuration. They do not
stop generating where they already do: **all four** generate — for 16, 18, 3 and 3 configurations
respectively — and continue until the terminal cleanup deletes them.

That changes what this ADR sequences. The rollout is no longer "hold everything back, then flip the
switch"; it is "canonical tests generate from the start, and each transport is brought to conformance
under an audited deferral marker until it is". The terminal cleanup — delete the four legacy
templates and their eighty generated copies, then remove the gates and config keys — is the last
merge rather than the enabling one.

The target set is **every transport with a messaging gateway** — all twelve
`src/Paramore.Brighter.MessagingGateway.*` projects (requirements.md FR-13). Membership is *having a
gateway*, not having generator wiring: a missing `test-configuration.json` is a gap FR-20 closes, not
grounds for exclusion. Nor is membership keyed on the `HasSupportTo*` flags — FR-10/FR-11 delete
those, so a keys-based definition would erase itself the moment this change lands.

Nine of the twelve are wired today — **AWS (V3 and V4), GCP, Kafka, MSSQL, PostgreSQL, Redis,
RMQ.Async, RocketMQ** — and generation is per **gateway configuration**, not per project: those nine
declare **twenty** configurations between them (AWS and AWS.V4 four each — SQS Standard/FIFO, SNS
Standard/FIFO; GCP four — Pull, PullOrdering, Stream, StreamOrdering; Kafka two — Standard,
PartitionKey; RMQ.Async two — Classic, Quorum; the rest one apiece), matching the 20
`*MessageGatewayProvider.cs` implementations. The other three — **AzureServiceBus, MQTT, RMQ.Sync** —
are wired by FR-20 and contribute their own rows. **Conformance is therefore a per-configuration
property**, and the ledger below is keyed accordingly. Conformance state is uneven:

- **Kafka already conforms** — it carries the richest hand-written suite
  (`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/`, Reactor + Proactor), which is the
  canonical grounding for the templates; yet it mis-declares **all three** gates
  (`HasSupportToRequeue`, `HasSupportToDeadLetterQueue`, `HasSupportToDelayedMessages`) as `false`
  in both its Standard and PartitionKey configs — the starkest case of a config tuned to keep the
  suite green rather than to describe the gateway.
- **The DLQ-ADR transports** — AWS SQS (`0038-aws-sqs-dlq-direct-send`), Redis
  (`0039-redis-dlq-brighter-managed`), MSSQL (`0040-mssql-dlq-brighter-managed`), PostgreSQL
  (`0041-postgres-dlq-brighter-managed`), and RocketMQ (`0042-rocketmq-dlq-brighter-managed`) —
  already have Brighter-managed DLQ/reject decisions recorded
  per transport, but their end-to-end conformance against the generated suite is unproven; several
  mis-declare gates (AWS `HasSupportToDelayedMessages: false` in three of its four gateway
  configurations — `SqsStandard` declares `true`; PostgreSQL both `false`; and MSSQL
  `HasSupportToDeadLetterQueue: false` despite ADR `0040-mssql-dlq-brighter-managed` giving it a
  Brighter-managed DLQ). **RMQ**
  sits alongside them in the sequence but has **no** per-transport DLQ ADR — RabbitMQ conventionally
  uses a native dead-letter exchange (DLX), and its reject/DLQ conformance rests on the universal
  routing strategy (`0047-message-rejection-routing-strategy` / `0045-provide-dlq-where-missing`),
  not a Brighter-managed-DLQ decision of its own; its fix may therefore be larger than the
  DLQ-ADR transports' and is treated under the size/risk boundary accordingly.
- **Known FR-2 non-conformances are already identified across two transports — five configurations
  (GCP ×4 and RocketMQ) — ahead of any generation run.** (Counted per configuration, which is the
  ledger's unit, it is five rows; per transport it is two.) The mechanism-agnostic FR-2 asserts a
  delayed requeue is not redelivered before the delay and is redelivered after it; neither GCP nor
  RocketMQ does that today, and each is caught by AC-2's before-`D` (immediate-`MT_NONE`) arm. All four GCP configurations ignore the delay argument —
  `GcpPullMessageConsumer.Requeue` calls `ModifyAckDeadline(..., 0)` and its XML doc states the
  delay is "not used by Pub/Sub" (redelivery timing comes from the subscription's RetryPolicy) —
  and `RocketMessageConsumer.Requeue` is a **no-op returning `true`**, its `ChangeInvisibleDuration`
  call commented out pending an upstream RocketMQ C# client release. RocketMQ's is therefore blocked
  on a third-party dependency and is a likely signed-off `Deferred` row rather than an in-spec
  `Fixed`; GCP's requires deciding whether subscription-RetryPolicy redelivery can satisfy FR-2 at
  all. Both are seeded into the ledger as known non-conformances rather than discovered late.
- **The known-gap transports** are worst off. **GCP** has messaging-gateway providers and a
  requeue-to-DLQ generated test but none of the canonical reject-routing or metadata behaviours, and
  its `Requeue` ignores the delay outright, so it fails FR-2 as soon as the canonical templates
  generate for it.

  **AzureServiceBus, MQTT and RMQ.Sync** have messaging gateways and test projects but **no generator
  wiring at all** — no `test-configuration.json`, no provider — so they generate nothing today.
  FR-20 brings all three into the generator: a config declaring their gateway configuration(s), a
  provider implementing both the Reactor and Proactor interfaces of the FR-1 surface, and CI
  infrastructure. They are not starting from zero — counted as test files under
  `tests/Paramore.Brighter.*.Tests/MessagingGateway/`, RMQ.Sync carries 31, MQTT 19,
  AzureServiceBus 15 — and both MQTT and RMQ.Sync implement `IAmAChannelFactoryWithScheduler`, while
  MQTT has its own
  dead-letter ADR (`0043-mqtt-dlq-brighter-managed`). This is the largest new work in the rollout and
  sequences last.

FR-13 forbids *silently* skipping or gating any canonical test to make the suite
green — an audited, linked, ledger-backed deferral marker is permitted and is the expected
transitional state; a bare or reasonless `[Skip]` is not — and requires that any deferred gateway fix be a **named, linked, maintainer-signed-off
follow-up issue referenced from the spec** — "a deferral is auditable, never an open-ended escape
hatch." AC-13 restates this as "no silent skip and no unaudited deferral." C-1 confines the work to
the generator, its templates and configs; the transport-gateway source fixes FR-13 requires; **and
the test-side onboarding FR-20 requires — new `test-configuration.json` files, new provider
implementations, and the CI infrastructure to run them** — with no public Brighter runtime API
redesign beyond FR-1's generated providers. That last clause is what puts step 4's onboarding work
inside the boundary: FR-20 wires existing gateways into the generator, it does not modify them
except where FR-13 applies. OOS-2 keeps native-variant supplementary tests as separate follow-up
issues.

That raises three questions ADR 0066 explicitly leaves open and this ADR answers:

1. In what **order** do transports reach conformance, between the canonical templates landing and
   the terminal cleanup?
2. **How** is a not-yet-conformant transport represented so a deferral is auditable rather than a
   silent skip?
3. **Where** is the boundary between fixing a gateway inline in this spec versus deferring it?

**Parent Requirement**: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)

**Scope**: This ADR decides (1) rollout sequencing and the fix-to-conform boundary (FR-13 / AC-13),
(2) the **FR-20 onboardings** of the three unwired transports — AzureServiceBus, MQTT and RMQ.Sync —
including what a deferred onboarding must produce (FR-20 / AC-23), and (3) the **conformance ledger**
required by FR-21 / AC-24: its cell vocabulary, the linked-issue `Skip` deferral marker that
cross-checks it, and the CI audit that enforces the two against each other. FR-21 states *that* the
ledger exists and what it must contain; this ADR decides *how* it is expressed and enforced.

The provider-interface extension, the gating lifecycle and gate retirement, and the deletion of the
`with_delay` (FR-12) and exhaustion (FR-19) templates are decided in
[ADR 0066](0066-conformance-test-provider-and-ungating.md); individual template content and
native-variant supplementary tests (OOS-2) are out of scope. This ADR does **not** supersede 0066 —
it sequences the rollout around it.

## Decision

We land universal conformance as **generate-then-fix-then-clean-up**: canonical templates merge
early and generate everywhere immediately; each is introduced carrying an audited deferral marker
for every configuration not yet known to conform; per-transport gateway fixes then remove those
markers one configuration at a time; and ADR 0066's legacy retirement and gate removal is the
**last** merge. A checked-in **conformance ledger** is the single source of truth for
per-configuration conformance state, cross-checked against a mandatory, greppable **linked-issue
Skip** convention. The boundary between fixing inline and deferring is a **size/risk threshold**,
maintainer-judged and recorded in the ledger.

**How master stays green.** A canonical template generates for all twenty-plus configurations the
moment it merges, and five of them are known in advance not to conform (GCP ×4 and RocketMQ fail the
mechanism-agnostic FR-2). Master stays green because a canonical test lands **already carrying** a
`Skip = "Deferred: #NNNN …"` for each configuration not yet proven, and the fix phase removes those
markers as transports are brought to conformance — the marker is deleted in the same PR as the
gateway fix. This inverts the usual reading of the Skip convention: it is not an exceptional escape
hatch used at the end, it is the **normal transitional state** of a canonical test between merging
and its transport being fixed.

The trade is deliberate. The alternative — hold every canonical template back until its transport is
fixed — would keep the templates unexercised until late, which is exactly how the broken `with_delay`
template survived undetected. Landing them early runs them against real brokers immediately, and the
audit machinery below is what stops "temporarily skipped" decaying into "permanently invisible": every
marker needs a linked issue and a ledger row from the day it appears, not from the day someone
notices it is still there.

### Architecture Overview

The rollout is a pipeline whose enabling change, fix phase and cleanup are deliberately distinct:

```
  ENABLE (first)          FIX PHASE (each stage merges to a green master)      CLEAN UP (last)
  ----------------------  ---------------------------------------------------  ------------------
  canonical templates     (i)  Kafka        reference — prove templates +   \
  land, ungated by             (already      extended provider end-to-end    |
  construction; SkipTest        conforms)    against the known-good          |
  gates narrowed to the                      transport                       |   ADR 0066
  4 legacy templates      (ii) DLQ-ADR      AWS, AWS.V4, Redis, MSSQL,      |   legacy retirement
                               transports    PostgresSQL, RocketMQ (DLQ-ADR  |   - delete 4 legacy
  each canonical test          (+ RMQ.Async, slugs in References);           |     templates + their
  merges carrying a             no DLQ ADR)  RMQ.Async (native DLX,          |     80 generated
  Deferred marker for                        universal routing 0047/0045)    |     copies
  every config not yet                      — Fixed, or Deferred -> #issue   |   - then remove the
  proven                  (iii)known-gap    GCP (no canonical coverage;      |     3 gates + the
                               transports    Requeue ignores the delay),     |     3 config keys
  the 4 legacy templates                     then the unwired three — ASB,   |
  are never ungated —                        MQTT, RMQ.Sync (FR-20: config + |   merges LAST
  no new generation site                     provider + CI) — most new work; |
                                             conform or signed-off deferral  /
                                                                             |
       conformance-status.md (ledger)  <== single source of truth ==========+
       configuration x behaviour matrix; cells Pass | Fixed | Deferred->#issue
                cross-checked against in-code `Skip = "Deferred: #NNNN ..."`
```

Stage (ii) names projects exactly as the Context does: **AWS** and **AWS.V4** are distinct projects
contributing four configurations each, and **RMQ.Async** is the wired RabbitMQ project — `RMQ.Sync`
is unwired and belongs to stage (iii) under FR-20, never to stage (ii).

The **ledger is the knowing responsibility** (Responsibility-Driven Design): the one information
holder that knows, per transport × canonical behaviour, whether that behaviour conforms as
generated (`Pass`), conforms via an in-spec gateway fix (`Fixed`), or is deferred to a signed-off
issue (`Deferred -> #issue`). No cell may read `Unknown` when the cleanup merges. The in-code Skip
marker is the *doing/enforcing* half — a greppable in-tree signal that a CI audit cross-checks
against the ledger so neither can drift from the other.

### Key Components

- **The conformance ledger** — required by requirements.md **FR-21**, which fixes its location, its
  per-configuration row granularity, its canonical-behaviour columns, and the rule that no cell may
  remain unresolved when the cleanup merges; this ADR decides the cell vocabulary and the enforcement
  machinery below. A markdown matrix checked into the spec directory at
  `specs/0036-universal-transport-conformance-tests/conformance-status.md`. **A row is one gateway
  configuration**, identified as `project / configuration` (e.g. `AWS.V4 / SqsStandard`,
  `GCP / PullOrdering`, `Kafka / PartitionKey`) — twenty rows across the nine wired projects today,
  plus the rows AzureServiceBus, MQTT and RMQ.Sync contribute once FR-20 wires them — because
  generation and therefore conformance are per configuration, not per
  project. A per-configuration row is what lets the ledger express "SQS Standard passes FR-5 but SNS
  FIFO does not"; a project-level row could not.

  **Naming a singular-section configuration.** The three examples above come from `MessagingGateways`
  (plural) sections, where the configuration name is the JSON key. Four wired configurations — Redis,
  MSSQL, PostgresSQL and RocketMQ — use a singular `MessagingGateway` section carrying no name key;
  such a configuration is named by its `CollectionName`, giving rows like
  `Redis / RedisMessagingGateway`. The row's project token is the test-project name (`PostgresSQL`,
  not the `Postgres` gateway project), which the CI audit compares as a string. Without this rule
  four of the twenty rows have no constructible identifier.

  **Placeholder rows, so a deferred onboarding cannot hide.** A transport whose FR-20 onboarding is
  deferred declares no `test-configuration.json`, therefore no configuration, therefore no row — and
  the `Unknown`-free cleanup gate below would pass **vacuously** for exactly the case this ADR calls
  likeliest (ASB). Every one of the twelve targeted transports MUST therefore occupy the ledger. A
  transport that has not yet declared a configuration takes a single placeholder row
  `<Project> / (not yet declared)` — e.g. `AzureServiceBus / (not yet declared)` — whose cells may
  hold transient `Unknown` during the fix phase and, at cleanup, **only** the
  `Deferred -> #NNNN (sign-off: @maintainer)` form — never `Pass` or `Fixed`, since nothing has been
  generated to pass. (Seeding at step 1 therefore sets a placeholder row's cells to `Unknown` like any
  other row; what distinguishes it is that `Unknown` may only ever resolve to `Deferred`, never to
  `Pass` or `Fixed`, while the row remains a placeholder.) The placeholder is replaced by per-configuration rows when the
  configuration lands. **The cleanup gate is evaluated over all twelve targeted transports, not over
  whichever rows happen to exist**: a transport contributing no row at all fails the gate rather than
  passing it silently.

  Columns = the canonical behaviours (FR-2, FR-4…FR-9, FR-15, FR-16, FR-17, FR-22). **FR-3 is deliberately not a column** — ADR 0066 withdrew it as a mechanism
  assertion and folded it into a mechanism-agnostic FR-2. That matters here: had FR-3 survived, the
  14 of the 20 configurations wired today with no scheduler seam would each have needed an
  `N/A (native)` cell,
  reintroducing exactly the native/non-native distinction OOS-1 rejects. With FR-2 mechanism-agnostic
  every column applies to every configuration, and the cell vocabulary below is sufficient. Each cell
  holds exactly one of:
  - `Pass` — conforms as generated, both Reactor and Proactor variants green;
  - `Fixed (#PR/commit)` — conformed via an in-spec gateway fix, linked to the PR/commit;
  - `Deferred -> #NNNN (sign-off: @maintainer)` — a named, linked, maintainer-signed-off follow-up
    issue.
  A transient `Unknown` is permitted only during the fix phase; the cleanup is blocked while any
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
- **The sequencing order** — reference (Kafka) -> DLQ-ADR transports -> known-gap transports ->
  terminal cleanup.
- **The class of in-spec gateway fixes** — the localized reject/requeue/scheduler-wiring changes of
  the same shape the per-transport DLQ ADRs already established (`Fixed` rows), versus follow-up
  issues for anything needing a new subsystem or runtime API change (`Deferred` rows). Those ADRs
  are cited by slug in References; note that ADR *numbers* 0038–0043 are each reused in this
  repository, so a bare number or a numeric range does not identify them.

### Technology Choices

- **Generate-then-fix over flip-then-fix.** Landing canonical templates early — behind audited
  per-configuration deferral markers — exercises them against real brokers from the start, so a
  defective template is caught in days rather than surviving to the end of the rollout (the fate of
  the `with_delay` template). Master stays green at every merge because each canonical test arrives
  with markers already in place and sheds them as fixes land. The terminal cleanup (delete the four
  legacy templates + 80 generated copies, then remove the three gates + config keys) is a single,
  atomic, reversible capstone whose blast radius is known. Retiring the gates *first* would instead
  ungate the four legacy templates onto every configuration — putting known-bad old tests on master
  and forcing bare `[Skip]`s to green it, exactly what FR-13/AC-13 forbid.
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

1. **Seed the ledger.** Build `conformance-status.md` with every targeted gateway configuration ×
   canonical behaviour set to `Unknown`. This is the work list and the cleanup gate.
2. **Narrow the gates and land the canonical templates.** Apply ADR 0066's step A — `SkipTest`
   consults the three gates only for the closed legacy list — then merge the canonical templates.
   They generate for every targeted configuration immediately, each carrying a `Deferred: #NNNN`
   marker for configurations not yet proven.
3. **Prove the reference (Kafka).** Run the generated canonical suite against Kafka, whose
   hand-written suite already conforms, to validate the templates and the ADR-0066 extended provider
   interface end-to-end. **Kafka's three mis-declared gates are irrelevant here**: they still read
   `false` at this point and are not removed until the final cleanup, but they no longer reach
   canonical templates, so the canonical suite generates and runs for Kafka regardless. Mark both
   Kafka configuration rows (Standard, PartitionKey) `Pass` and drop their deferral markers.
4. **Bring the DLQ-ADR transports to conformance.** For each of **AWS**, **AWS.V4**, **Redis**,
   **MSSQL**, **PostgresSQL** and **RocketMQ** (DLQ-ADR slugs in References) — and **RMQ.Async**,
   which has no per-transport DLQ ADR and whose reject/DLQ conformance may be a larger fix — run the
   generated suite; for each non-conformant behaviour, apply the fix-to-conform boundary. If
   localized and low-risk (add a lazy DLQ/invalid producer, forward the scheduler to consumers, stamp
   the metadata semantic set under the transport's own keys), fix inline, **remove that
   configuration's deferral marker in the same PR**, and record `Fixed (#PR)`. Otherwise open a
   follow-up issue, obtain maintainer sign-off, leave the `Deferred: #NNNN` Skip in place, and record
   `Deferred -> #issue`.
5. **Close the known-gap transports last.** First GCP (no canonical coverage; `Requeue` ignores the
   delay), then the three FR-20 onboardings — AzureServiceBus, MQTT, RMQ.Sync — each needing a
   `test-configuration.json`, provider implementation(s) and CI infrastructure before it can generate
   at all. These carry the most new work and are sequenced last, once the templates and the extended
   provider interface are proven on transports that already generate. An onboarding that cannot be
   completed in-spec (most likely for CI-infrastructure reasons) takes a signed-off `Deferred` ledger
   row like any other gap — it does not drop out of the target set.
6. **Merge the cleanup.** Only when the ledger has **no `Unknown` cells** — every behaviour is
   `Pass`, `Fixed`, or `Deferred -> #issue` — merge ADR 0066's step C: delete the four legacy
   templates in both variants **and their eighty generated copies**, then remove the three gate
   branches from `SkipTest`, the three properties from `MessagingGatewayConfiguration`, and the keys
   from every `test-configuration.json`. The order within this step matters — removing a key before
   its template is deleted would ungate that legacy template.
7. **Enforce in CI.** The audit scan runs on every PR over in-tree artifacts only: it fails any
   messaging-gateway test whose `Skip` does not match `Deferred: #<n>`, and it fails if a `Skip`
   has no matching `Deferred` ledger row or a `Deferred` row is missing its issue link or sign-off
   entry. It does not query the issue tracker for live state — issue-open and sign-off validity are
   the maintainer review gate's responsibility.

**Reactor/Proactor parity applies to the ledger.** A behaviour is `Pass`/`Fixed` for a transport only
when **both** the Reactor and Proactor variants pass (FR-14).

Partial conformance is a routine expected state, not an edge case: the async gateway paths are
genuinely distinct code, and parity applies to every canonical column on every row. It is recorded as
**a single `Deferred -> #NNNN` cell whose issue names the lagging variant** — never as a split cell,
two values in one cell, or a per-variant column split. The cell vocabulary stays closed at
`Pass` / `Fixed (#PR/commit)` / `Deferred -> #NNNN (sign-off: @maintainer)` plus transient `Unknown`,
and "exactly one of" continues to hold, so the CI audit needs no additional cell grammar.

This loses nothing that matters. The ledger cell records **conformance**, and FR-14 makes parity part
of what conformance means — so a transport whose Proactor variant fails does not conform for that
behaviour, and `Deferred` is the honest reading. The detail that one variant already passes belongs
in the linked issue, where the remaining work is described, not in the matrix. (During the fix phase a
partially-conforming behaviour may sit at `Unknown`; it must resolve to `Deferred` with a signed-off
issue, or to `Pass`/`Fixed` once both variants pass, before the cleanup merges.)

**Infra reality.** `Pass` means the generated suite actually *ran* against the transport's broker
(container/emulator), not merely compiled. Timing-sensitive behaviours use NFR-2 bounded
receive-retry loops so broker propagation delay does not produce false failures.

## Consequences

### Positive

- Master is never red: canonical tests arrive carrying audited deferral markers and shed them as
  fixes land, so every merge leaves a green suite, and the cleanup is a single atomic, reversible
  capstone.
- Canonical templates are **exercised from the moment they merge**, against every configuration that
  already conforms — so template defects surface immediately instead of at the end of the rollout.
- Universal conformance is provably **complete or auditably deferred** at merge — AC-13's "no silent
  skip and no unaudited deferral" is enforced mechanically, not by convention.
- The ledger gives at-a-glance per-configuration coverage and doubles as the cleanup gate: the cleanup cannot
  land while any cell is `Unknown`.
- Deferrals cannot rot into silent gaps — each is a named, linked, signed-off issue that CI keeps
  honest against both the ledger and the in-code Skip.
- Proving the machinery on the known-good transport (Kafka) first de-risks the templates and the
  extended provider before they meet the hardest gaps (GCP, then the FR-20 onboardings).

### Negative

- **Deferral markers are visible on master for most of the rollout**, and there will be many of them
  at the start — one per unproven configuration × behaviour. A reader browsing the generated suite
  mid-rollout sees a lot of skipped tests, which looks like the "normalized red" this ADR set out to
  avoid. The distinction is real but not self-evident from the test tree alone: every marker is
  linked, signed off and ledger-backed, and CI fails any that is not. The ledger is what makes the
  difference legible, which raises the cost of letting it drift.
- The **legacy templates linger** in the tree, and **all four** keep generating and running, until
  the final cleanup. This is a real cost, not a cosmetic one: the exhaustion template runs for
  16 configurations, plain requeue for 18, `with_delay` and delayed-message for 3 each — 80 generated
  files consuming CI time and asserting behaviours this spec has judged wrong (a pump-owned
  behaviour, and a delayed requeue that passes no delay). A contributor can also read them and
  mistake them for live coverage. The alternative — deleting them early — was rejected because it
  would leave the canonical replacements unbuilt and the transports unproven in the interim; the cost
  is accepted for the duration of the rollout and discharged in one atomic cleanup.
- The ledger, the Skip convention, and the CI audit are **new machinery** to build and maintain
  (none exists today), adding process weight to the spec.
- The size/risk fix-to-conform boundary is a **judgement call** that can be contested per transport ×
  behaviour; the heuristic reduces but does not remove disagreement.
- A long fix phase risks **drift** between the templates/provider interface (evolving under 0066's
  tasks) and the transport gateways being fixed against them.
- Including the three unwired transports (FR-20) makes this spec **materially larger**: AzureServiceBus,
  MQTT and RMQ.Sync each need a `test-configuration.json`, provider implementation(s) and CI
  infrastructure before a single canonical test can run for them. ASB in particular is a cloud
  service whose CI story is not solved here. The mitigation is that FR-13's deferral rule applies
  unchanged — an incomplete onboarding becomes a signed-off ledger row, not a silent exclusion — but
  the honest expectation is that one or more of the three lands as `Deferred` at cleanup time.

## Risks and Mitigations

- *Risk*: the deferral list grows unbounded, hollowing out "universal." *Mitigation*: every deferral
  is a ledger `Deferred` row with an open, linked, maintainer-signed-off issue — growth is visible,
  owned, and audited; the sign-off gate is where scope creep is caught.
- *Risk*: a transport needs infrastructure (containers/emulators) that is flaky or unavailable, so
  conformance can't be proven. *Mitigation*: `Pass` requires the suite to actually run, not just
  compile; NFR-2 bounded retries absorb propagation jitter; a transport that genuinely can't be
  exercised in CI becomes a signed-off `Deferred` row rather than a silently skipped test.
- *Risk*: the cleanup is delayed indefinitely behind stragglers. *Mitigation*: the ledger surfaces
  exactly which transport × behaviour cells remain `Unknown`, so the block is explicit and
  actionable; a straggler can be converted to a signed-off deferral to unblock the cleanup without
  hiding the gap.
- *Risk*: template/gateway drift over a long fix phase. *Mitigation*: the provider interface is
  generated and compiled against each hand-written provider (per 0066), so drift is a build break,
  and the ledger is re-run per transport as the templates settle.

## Alternatives Considered

1. **Flip-then-fix** — retire the three gates first, then chase conformance. Rejected, and the
   distinction from what we *do* adopt is worth being precise about, because both involve tests that
   do not run for a while.

   Retiring the gates first ungates the **four legacy templates** as a side effect — including the
   broken `with_delay` template and the exhaustion template, which assert the wrong things and which
   this spec deletes. Master then carries known-failing *old* tests, and the only ways to green it
   are a bare `[Skip]` or a rushed fix to code that is about to be deleted. That is what normalizes
   red and violates FR-13/AC-13.

   What we adopt instead lands **canonical** tests early, each carrying a linked, signed-off,
   ledger-backed deferral marker for configurations not yet proven — and never ungates a legacy
   template at all. The suppressed tests are the ones being retired; the deferred tests are the ones
   being adopted, and every deferral is owned and audited from the moment it appears. The superficial
   similarity — "some tests aren't running yet" — hides opposite intents: one defers *removal* of
   tests we don't want, the other defers *enforcement* of tests we do.
2. **Reference-first then big-bang** — prove Kafka, then land all remaining fixes plus the cleanup in
   one enormous final merge. Rejected: one unreviewable, un-bisectable merge; a single regression
   anywhere blocks the whole rollout.
3. **Ledger-only, no in-code marker** — track deferrals solely in `conformance-status.md`. Rejected:
   a deferral is invisible in the test tree, so a reader of the suite cannot tell a passing test from
   a quietly deferred one without consulting an external document; no greppable enforcement point.
4. **Skip-with-issue only, no ledger** — require the `Deferred: #<n>` Skip but keep no matrix.
   Rejected: no single coverage view, and sign-off provenance is reduced to whatever a string
   asserts; there is nothing to cross-check the Skip against.
5. **Fix-everything-in-spec** — forbid all deferrals; conform every transport inline. Rejected:
   risks an unbounded, long-running spec, and some gaps (RocketMQ's upstream-client block, subsystem-level
   changes, runtime API changes forbidden by C-1) genuinely belong in follow-up work.
6. **Defer-all-gateway-fixes** — flip and open issues for every non-conformance. Rejected: universal
   conformance would be almost entirely promissory at merge, defeating the spec's purpose of proving
   transports honour the universal obligations.

## References

- Requirements: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)
- Related ADRs (this ADR builds on all of these; it supersedes none):
  - [`0066-conformance-test-provider-and-ungating`](0066-conformance-test-provider-and-ungating.md) [Proposed] — **sibling; the most important relation.** Extends the provider interfaces, makes canonical templates ungated by construction, and retires the three gates + config keys only as a terminal cleanup after the four legacy templates are deleted; this ADR sequences the per-transport rollout around that lifecycle and governs deferrals. 0067 does not supersede 0066.
  - `0037-add-messaging-gateway-generated-test` [Accepted] — created the generator and `MessagingGatewayConfiguration` this whole spec extends; the target-set and `SkipTest` gating originate here.
  - `0047-message-rejection-routing-strategy` — the `Reject()` fallback ladder and origin-metadata contract; a transport is "conformant" in the ledger precisely when it honours this.
  - Per-transport DLQ ADRs — collectively the class of localized, in-spec `Fixed` changes and the reason these transports sequence ahead of GCP: `0038-aws-sqs-dlq-direct-send`, `0039-redis-dlq-brighter-managed`, `0040-mssql-dlq-brighter-managed`, `0041-postgres-dlq-brighter-managed`, `0042-rocketmq-dlq-brighter-managed`, `0043-mqtt-dlq-brighter-managed`, `0046-kafka-dlq-producer-for-requeue`.
  - `0037-universal-scheduler-delay`, `0039-transport-scheduler-wiring`, `0045-provide-dlq-where-missing` — the runtime mechanisms (scheduler-backed delayed requeue, channel-factory scheduler wiring, Brighter-managed DLQ/invalid channels) that conformance depends on and the in-spec fixes wire up.
- External references: none.
