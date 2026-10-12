# Review: design — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-12
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Review history for ADR `0085-outbox-stale-message-telemetry`: pass 1 NEEDS WORK (15 findings, 2 at or above
> 60); pass 2 NEEDS WORK (15, 3), all three from a timer-callback design, after which the mechanism was rewritten
> around one background loop that owns scheduling and outcome decisions; pass 3 NEEDS WORK (11, 1); pass 4 PASS
> (8, 0). The ADR was then renumbered from 0084, because ADR `0084-outbox-metrics` merged with PR #4484, and
> updated for that ADR and for the new comment rules in CONTRIBUTING.md. Pass 5 NEEDS WORK (7, 1: the problem
> table contradicted the added-minus-cleared signal of ADR `0084-outbox-metrics`); pass 6 NEEDS WORK (7, 2: a false claim about
> `OutboxMeter` under `Sdk.CreateMeterProviderBuilder()` and a broken table); pass 7 NEEDS WORK (6, 2: the same
> setup and the same table); pass 8 PASS (3, 0), recorded below. Both mermaid diagrams render on every pass, and from pass 6 every table
> was also checked to render (mermaid-cli 11 and markdown-it 14.1.0).

## Findings

### 1. "No further setup" is stated without the shared-container condition (Score: 50)

The Positive bullet and the requirement's Proposed Solution step 3 promise the instruments to any application that calls `AddBrighterInstrumentation`, while the ADR says a provider from `Sdk.CreateMeterProviderBuilder()` is not supported.

**Recommendation**: Qualify the Positive bullet, and say in the ADR that it narrows the requirement's step 3.

**Status**: Fixed in the ADR. requirements.md, already approved, keeps step 3 as it is.

---

### 2. The unsupported-setup row does not say whether that setup throws (Score: 35)

Without an `IMeterFactory` in the SDK's own container, the deferred `Configure` cannot build `PublishConfirmationMeter` today, and resolving the new meter there would fail the same way. This was inferred, not run against Brighter.

**Recommendation**: Say so, or leave it at "not supported".

**Status**: Left at "not supported".

---

### 3. The lifetime of the start-up scope is not stated (Score: 30)

**Recommendation**: Say that the scope is disposed before `StartAsync` returns, and that the outbox and producers' configuration it keeps are the container's singletons.

**Status**: Fixed.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0
