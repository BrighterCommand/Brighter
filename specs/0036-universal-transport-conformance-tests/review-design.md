# Review: design — 0036-universal-transport-conformance-tests (re-review 3)

**Date**: 2026-07-19
**Threshold**: 60
**Verdict at time of review**: NEEDS WORK (2 findings ≥ 60)
**Status after remediation**: all 6 findings addressed in commit `2745b2095` — pending re-verification

## Prior Findings — Status

| # | Prior finding | Prior score | Status |
|---|---------------|-------------|--------|
| 1 | FR-2/FR-3 mechanism-conditionality vs seam coverage | 75 | Partially resolved → now addressed |
| 2 | Alternative 4 under-argued | 65 | Resolved |
| 3 | Gate-inventory inaccuracies | 50 | Resolved |
| 4 | DLQ/invalid read semantics | 50 | Resolved |

Verified this round: `IAmAChannelFactoryWithScheduler` is implemented by exactly six gateways
(Kafka, MQTT, MsSql, Redis, RMQ.Async, RMQ.Sync); the target set is 9 projects / **20** gateway
configurations matching 20 `*MessageGatewayProvider.cs` files, of which 6 can carry a scheduler.
`SqsMessageConsumer.RequeueAsync` issues `ChangeMessageVisibilityAsync` (line 402);
`PostgresMessageConsumer.Requeue` binds the delay as a query parameter (line 459). Orphan sweep for
`CreateChannelWith*Scheduler` / `SpyScheduledChannel` / `SpySchedulerAsync` was clean. Every gate
value asserted across the three documents checks out against the JSON. The `MT_NONE`-on-empty read
contract is sufficient for AC-5/AC-18.

## Findings (all now addressed)

### 1. "The other 14 delay natively" is factually wrong for GCP (×4) and RocketMQ (Score: 75) — FIXED

The claim was generalized from the two configurations that had been verified (SQS, PostgreSQL) and
is false for five of the fourteen. `GcpPullMessageConsumer.Requeue` (line 283) ignores `delay` and
calls `ModifyAckDeadline(subscriptionName, [ackId], 0)`; its own XML doc reads *"An optional delay
(not used by Pub/Sub)"* — redelivery timing is governed by the subscription's RetryPolicy.
`GcpPubSubStreamMessageConsumer.Requeue` (line 217) is the same. `RocketMessageConsumer.Requeue`
(line 179) is a **no-op returning `true`**, its `ChangeInvisibleDuration` call commented out
pending an upstream RocketMQ C# client fix.

The conclusion survives (these transports have neither a scheduler seam nor a native requeue delay),
but it understated rollout risk: the mechanism-agnostic FR-2 will **fail on 5 configurations** at the
flip, and 0067 sequenced RocketMQ among transports expected to need only localized `Fixed` work when
it is in fact blocked on a third-party dependency.

**Resolution**: corrected in 0066 ("Why there is no scheduler member" now distinguishes the nine that
delay natively from the five that do neither), in requirements.md FR-3's withdrawal rationale, and in
0067 §Context, which now seeds GCP ×4 and RocketMQ as known FR-2 non-conformances with RocketMQ
flagged as a likely signed-off `Deferred` row.

### 2. FR-3 withdrawal incomplete: live design text still directed implementers to the withdrawn arm (Score: 70) — FIXED

The withdrawal landed in the Decision, interface sketch, "Why there is no scheduler member",
Alternative 4 and 0067's ledger rationale, but not in Consequences, Risks, Alternative 3, References
or several spots in requirements.md. Two were live instructions: 0066's Negative bullet required a
provider to supply *"a scheduler-backed channel"*, and its 0067 reference told the ledger to track
*"the in-memory scheduler arm"* — neither exists in the design.

**Resolution**: swept all `FR-2/FR-3` pairings to `FR-2`; removed the scheduler-backed-channel and
in-memory-arm clauses; restated Alternative 3 against the amended FR-2; put the
`0037-universal-scheduler-delay` gloss in the past tense; dropped the producer `Scheduler` from
requirements.md's Objective and Test Boundary; corrected OOS-3's transitive-proof justification
(scheduler forwarding is **not** proven transitively now, so those wiring tests are excluded as
OOS-2 work rather than as redundant); and corrected the Kafka coverage-gap paragraph, which had
claimed the generated suite closes an async scheduler gap it no longer closes.

### 3. AC-1 demanded "separate members" the design does not provide (Score: 50) — FIXED

AC-1 required *"separate members exist to create channels configured with a DLQ only, an invalid
channel only, and neither"*; 0066 provides one `CreateSubscription` with two nullable defaulted
routing-key parameters — same expressiveness, fewer members. **Resolution**: AC-1 reworded to
"the provider exposes a means to …", naming the nullable-parameter approach.

### 4. Read contract covered "queue empty" but not "queue not configured" (Score: 45) — FIXED

**Resolution**: contract extended — reading a DLQ or invalid channel the subscription does not
configure also returns `MT_NONE` rather than throwing; and the FR-5/FR-17 templates are stated to
configure both routing keys and assert the message landed on one and not the other.

### 5. MSSQL's mis-declared `HasSupportToDeadLetterQueue: false` absent from the inventory (Score: 35) — FIXED

`tests/Paramore.Brighter.MSSQL.Tests/test-configuration.json` declares it `false` despite ADR
`0040-mssql-dlq-brighter-managed`. **Resolution**: added to 0067 §Context, requirements.md FR-11 and
§Additional Context.

### 6. Alternative 4 conflated six *gateways* with six *configurations* (Score: 30) — FIXED

**Resolution**: split the two facts — six gateways implement the interface, four are in the target
set, contributing 6 of the ~20 target configurations; MQTT and RMQ.Sync noted as out of scope.

## Summary (as reviewed)

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 3 |

**Total findings**: 6
**Findings at or above threshold (60)**: 2
**All six addressed** in commit `2745b2095`.
