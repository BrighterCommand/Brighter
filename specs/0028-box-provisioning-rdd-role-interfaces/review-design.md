# Review: design — 0028-box-provisioning-rdd-role-interfaces

**Date**: 2026-05-07
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Step 7 leaves the nullability/optionality of the new `I{Backend}AdvisoryLock` ctor parameter unspecified (Score: 45)

The round-7 fix reads: *"Your derived runner ctor adds an `I{Backend}AdvisoryLock` parameter (or your backend's lock-primitive interface from step 4) and forwards `(detectionHelper, configuration, lockTimeout, logger)` to the base ctor; store the lock primitive as a private field for use inside `CreateUnitOfWorkAsync`."* Existing precedent in the codebase makes the parameter **optional with a self-defaulting fallback** — `MsSqlBoxMigrationRunner.cs:45` declares `IMsSqlAdvisoryLock? advisoryLock = null` and the body assigns `advisoryLock ?? new MsSqlAdvisoryLock()`; PG/MySQL match this exactly. A new contributor reading step 7 alone would not know whether to make the parameter required or optional-with-default. Score 45 (Low) because both shapes work and the sentence "Use the existing four relational runners as references (after spec 0028 lands)" earlier in step 7 effectively defers the convention question.

**Evidence**:
- ADR §A.4 step 7 line 492 round-7 sentence: silent on nullability.
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:42-46` — `IMsSqlAdvisoryLock? advisoryLock = null` with `?? new MsSqlAdvisoryLock()` fallback in body.
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs:70-80` — same shape.
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxMigrationRunner.cs:70-80` — same shape.

**Recommendation**: Optional. If pursued, append: *"Mirror the existing four runners' shape — declare the parameter as nullable with a self-defaulting fallback (`IMsSqlAdvisoryLock? advisoryLock = null` plus `advisoryLock ?? new MsSqlAdvisoryLock()` in the body)."* Below threshold; not a blocker.

---

### 2. "SQLite/Spanner pattern" lumping in step 7's final clause is mildly misleading — Spanner does not reach this checklist item at all (Score: 35)

The round-7 sentence ends: *"Backends without an advisory-lock primitive (SQLite/Spanner pattern) skip this addition — their UoW folds the lock into transaction-begin per step 5."* But step 7's preceding text already says: *"If degenerate (no version inference — Spanner pattern), implement `IAmABoxMigrationRunner` directly and skip this base."* A Spanner-style implementer never derives from `RelationalBoxMigrationRunnerBase` and so never reaches the "skip this addition" instruction. The pairing of SQLite and Spanner in the lock-primitive clause is therefore a category error — SQLite is the only backend that simultaneously (a) inherits the base, (b) lacks an advisory lock, and (c) folds lock into transaction-begin. Spanner is exempt at a higher level.

**Evidence**: ADR §A.4 step 7 line 492: *"Backends without an advisory-lock primitive (SQLite/Spanner pattern) skip this addition"* immediately preceding *"If degenerate (no version inference — Spanner pattern), implement `IAmABoxMigrationRunner` directly and skip this base."*

**Recommendation**: Replace "SQLite/Spanner pattern" with "SQLite pattern" in that clause. Score 35 (Low) — cosmetic; Spanner's full exemption is repeated three times in the same paragraph and no implementer would be misled.

---

### 3. §A.1 "Provisioner ctor cascade" leading sentence still claims "ten ... three new ctor parameters" while sub-bullet exempts the Spanner pair (still-open round-7 finding #2, score: 52)

Round-7 finding #2 (score 52, Medium, below threshold) was intentionally not fixed and is still present at line 129 / line 486. No new angle pushes this above threshold; documented here for completeness only.

---

### 4. §A.4 step 6 line 491 mixes how-to-guide voice with documentation-of-existing-state voice (still-open round-7 finding #3, score: 38)

Round-7 finding #3 (score 38, Low, below threshold) was intentionally not fixed and is still present at line 491. Documented here for completeness only.

---

## Round-7 fix verification (not findings)

**Round-7 finding #1 (advisory-lock ambiguity in step 7)** — RESOLVED. The new sentence at line 492 now names the missing wiring shape: *"Your derived runner ctor adds an `I{Backend}AdvisoryLock` parameter (or your backend's lock-primitive interface from step 4) and forwards `(detectionHelper, configuration, lockTimeout, logger)` to the base ctor; store the lock primitive as a private field for use inside `CreateUnitOfWorkAsync`. Backends without an advisory-lock primitive (SQLite/Spanner pattern) skip this addition — their UoW folds the lock into transaction-begin per step 5."*

Internal consistency verified:
- Forwarded tuple `(detectionHelper, configuration, lockTimeout, logger)` matches the §B.2 sample base ctor parameter order at lines 332-336 exactly.
- "Lock primitive from step 4" reference is consistent with step 4's introduction of `I{Backend}AdvisoryLock` (line 484).
- "Store the lock primitive as a private field for use inside `CreateUnitOfWorkAsync`" composes correctly with step 5 (line 485) — UoW ctor takes `(connection, advisoryLock, ILogger)` — and with §B.3's logger plumbing row example `new MsSqlProvisioningUnitOfWork(connection, advisoryLock, Logger)`.
- Codebase precedent verified: `IMsSqlAdvisoryLock`, `IPostgreSqlAdvisoryLock`, `IMySqlAdvisoryLock` exist as expected (`src/Paramore.Brighter.BoxProvisioning.{MsSql,PostgreSql,MySql}/I{Backend}AdvisoryLock.cs`); SQLite and Spanner have no such interface, matching the "skip this addition" exemption.
- The two existing pattern conventions in the four relational runners (each takes `I{Backend}AdvisoryLock? advisoryLock = null` with self-defaulting body) are not contradicted by the new sentence — though they are also not specifically affirmed (see finding #1).

Round-7 findings #2 (score 52) and #3 (score 38) were intentionally not fixed; they remain in the ADR at the same locations and at the same scores.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 3 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0
