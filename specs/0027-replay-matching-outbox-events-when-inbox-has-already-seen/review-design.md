# Review: design — replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-06-22
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

This phase is already approved — findings below are informational (confirmatory review of the 2026-06-22 backward-compatibility amendments). None warrant re-opening the phase. The amendments are well-grounded: every file/class/member reference checked exists and matches the codebase, and the amended sections are internally consistent with each other and with the new ACs.

## Verification summary (all confirmed against code)

- **Relational write-path gate**: `RelationDatabaseOutbox.cs:82-85` and `RelationalDatabaseInbox.cs:82-85` currently gate the causation INSERT on `CausationQueries is not null` (a `queries as IRelationalDatabase…CausationQueries`). `InitAddDbCommand`/`CreateAddCommand` select `CausationQueries?.AddCausationCommand ?? queries.AddCommand` (outbox:1279, inbox:473), so the SQL switches purely on the static cast, not column existence — the ADR's stated problem is real.
- **No memoization today**: `SupportsCausationTracking[Async]()` (outbox:983-1007, inbox:266-292) runs `CausationColumnExistsCommand` on every call with no cached field. The ADR's "memoized probe, shared with Add" is a not-yet-implemented fix, consistent with the ADR being amended to *specify* the fix.
- **DynamoDB outbox**: `SupportsCausationTracking() => true` (DynamoDB:551, V4:564); `ReplayCausationAsync` uses `IndexName = _configuration.CausationIndexName` (DynamoDB:583, V4:596); `_client` is `IAmazonDynamoDB`; `DynamoDbConfiguration.CausationIndexName` exists, default `"Causation"` (both V11 and V4). A `DescribeTable` check is feasible with the available client/config.
- **DynamoDB inbox**: `GetCausationIdAsync` queries `KeyIdContextExpression` (Id+ContextKey, the table's own keys) with **no** `IndexName` (DynamoDbInbox.cs:274-294), so `=> true` (line 257) is honest — confirms the outbox-vs-inbox asymmetry.
- **MongoDb / Firestore**: outbox and inbox all `=> true` unconditionally — matches the rewritten NoSQL paragraph.
- **Bulk-add deliberately not causation-tracked**: the `Add(IEnumerable<Message>)` overloads have no causation branch; the new write-path gate only touches the single-message path — no interaction hazard.

## Findings

### 1. Write-path gate subsection does not state who owns the memoized field or how sync/async share it (Score: 45)
The "Write-path gate" subsection says the gate is "memoized once per store instance" but does not specify the storage shape (`bool?` vs `Lazy<bool>`) nor how the sync `Add` and async `AddAsync` paths coherently share one memo when the probe has distinct sync/async variants. Two developers could implement differently. Since Tasks 1–23 are already implemented, likely resolved in code — informational.
**Evidence**: ADR "memoized once per store instance (a one-shot lazy check…)" — no field/seam named, unlike the rest of the ADR.
**Recommendation**: One sentence naming the intended seam (e.g. "a private `bool?` populated lazily by the first probe on either path"). Optional.

### 2. Concurrency hazard of the first-probe race is unacknowledged (Score: 42)
The ADR acknowledges the construct-before-provisioning memo edge case but not the thread-race where two threads run the first probe concurrently. Benign (probe is idempotent, same result), so low severity.
**Evidence**: ADR covers staleness but not concurrency; no mention in Consequences.
**Recommendation**: Half-sentence: "concurrent first probes are harmless (idempotent, same result)."

### 3. ADR does not state the memo is never invalidated beyond a parenthetical (Score: 38)
The construct-before-provisioning case is handled by "provisioning is expected at startup," but the ADR does not explicitly say the per-instance memo is never invalidated — a long-lived singleton that memoized "absent" never re-probes if migrated under it at runtime. Reader must infer "no invalidation."
**Evidence**: ADR parenthetical only.
**Recommendation**: State explicitly that the memo is never invalidated and mid-process migration requires a restart (acceptable given V11 is the mandatory point). Optional.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 3 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0

The amendments correctly satisfy new **AC10** (no forced upgrade) and **AC11** (honest capability reporting). The strengthened "Non-breaking change" NFR aligns with the ADR's new subsection. No internal contradiction remains. The three low-scored items are precision nits on the memoization design, all below threshold and likely already settled in the implemented code.
