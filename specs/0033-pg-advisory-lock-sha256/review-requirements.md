# Review: requirements — 0033-pg-advisory-lock-sha256

**Date**: 2026-06-09
**Threshold**: 60
**Verdict**: PASS

No finding scored at or above the threshold of 60; the requirements are testable, bounded, and consistent with the codebase, with only sub-threshold prose/specificity issues.

## Findings

### 1. AC-5 is not independently testable because the "Brighter lock namespace" value is never pinned (Score: 55)

FR-3 and AC-5 require that "the namespace participates in the derivation" and that the result "is not equal to a SHA-256-over-`lockKey`-alone derivation that omits the namespace." But the document never states what the namespace *is*. In the code it is a concrete constant `BRIGHTER_LOCK_NAMESPACE = 74726` (`PostgreSqlAdvisoryLock.cs:64`). Without naming the namespace token or at least committing that "the same namespace constant currently used" is folded in, a test author cannot construct the negative oracle ("a derivation that omits the namespace") unambiguously — they must guess whether the namespace contributes as the integer `74726`, the string `"74726"`, the bytes "BRIG", or something the ADR invents. The doc defers byte mechanics to the ADR (OOS-5), which is fine, but the *identity/value* of the namespace input is a requirement-level fact needed to test AC-5, not a pure design mechanic.

**Evidence**: "the derivation hashes a composite that includes the namespace ... and the derived value is not equal to a SHA-256-over-`lockKey`-alone derivation that omits the namespace." (AC-5); doc nowhere states the namespace value, whereas code fixes it at `BRIGHTER_LOCK_NAMESPACE = 74726`.

**Recommendation**: State that the existing Brighter lock namespace (the constant currently passed as the `int4` namespace argument) is the value folded into the hash input, or explicitly delegate the exact namespace-bytes contribution to the ADR while keeping AC-5 phrased only as "differs from a lockKey-only hash."

---

### 2. Problem Statement mis-describes the current SQL overload as a single-text-arg form (Score: 45)

The Problem Statement says today's code "derives its lock from `pg_try_advisory_lock(int4 namespace, hashtext(text))`." The actual call is `SELECT pg_try_advisory_lock(@ns, hashtext(@key))` (`PostgreSqlAdvisoryLock.cs:90`), i.e. the two-`int4` overload `pg_try_advisory_lock(int4, int4)` — `hashtext` returns `int4`. FR-1 correctly names it ("MUST NOT invoke the two-argument `pg_try_advisory_lock(int4, int4)` overload"), so the spec is internally self-correcting, but the Problem Statement's notation invites a reader to think `hashtext(text)` is itself an overload signature rather than an argument expression. Cosmetic/prose-level, not a behavior gap.

**Evidence**: "Today `PostgreSqlAdvisoryLock` derives its lock from `pg_try_advisory_lock(int4 namespace, hashtext(text))`." vs. code `command.CommandText = "SELECT pg_try_advisory_lock(@ns, hashtext(@key))"`.

**Recommendation**: Rewrite as "...the two-argument `pg_try_advisory_lock(int4, int4)` overload, passing a fixed namespace int4 and `hashtext(lockKey)` (which returns int4) as the second argument."

---

### 3. NFR-1 / NFR-3 / AC-12 conflate "64-bit value" with "64 bits of entropy preserved through bigint conversion" (Score: 40)

NFR-1 and AC-12 assert a "~1-in-2^64 birthday bound" purely "by argument" from feeding a 64-bit SHA-256-derived value to Postgres. But OOS-5 defers the bytes-to-bigint conversion (which 8 bytes, signed/unsigned) to the ADR. The collision-space *claim* therefore depends on a design decision the requirements deliberately leave open: any conversion that takes a full 8 distinct bytes preserves 2^64, but a buggy conversion (e.g. masking to fewer bits, or reusing 4 bytes) would not, and nothing in the acceptance criteria actually tests the 2^64 property (AC-4 only checks that *two specific* keys differ — a 1-bit hash would pass AC-4). NFR-3/AC-6 are testable; NFR-1/AC-12 are "demonstrable by argument" only, so they're weak as acceptance gates. Acceptable for a hardening change of this size, but worth noting the AC does not empirically bound collisions.

**Evidence**: "This is demonstrable by argument" (NFR-1); "When the key supplied to PostgreSQL is examined, Then it is a 64-bit value drawn from SHA-256" (AC-12); contrast OOS-5 deferring "digest-slice selection."

**Recommendation**: Add an AC that the derived value uses 64 distinct bits of the SHA-256 digest (e.g. assert the conversion consumes 8 distinct digest bytes), so the 2^64 claim is checkable rather than asserted.

---

### 4. A-2 overstates the MySQL "64-bit principle" relative to the actual MySQL code (Score: 38)

The Problem Statement says MySQL "derives its key from the first 8 bytes (64 bits) of SHA-256." The MySQL helper only applies a SHA-256 suffix in its *long-form fallback* (`MySqlMigrationLockName.For` returns the plain `BrighterMigration_<schema>.<table>` simple form for names ≤ 64 chars, with no hash at all), and its own docstring claims the suffix gives "~1 in 2^32" collision resistance (`MySqlMigrationLockName.cs:53-58`) even though the code slices 8 bytes. A-2 commendably catches the "long-form fallback only" nuance and says it mirrors the *principle*, not a code path — so this is largely mitigated. The residual issue is that the Problem Statement's unqualified "matching the principle already established for the MySQL backend" plus "MySQL path, which derives its key from the first 8 bytes (64 bits)" reads as if MySQL universally enjoys a 2^64 margin, which its own code comments contradict.

**Evidence**: "the equivalent MySQL path, which derives its key from the first 8 bytes (64 bits) of SHA-256." (Problem Statement) vs. `MySqlMigrationLockName.cs:54-56` "Birthday-bound collision probability is ~1 in 2^32 ... the suffix is the first 8 bytes."

**Recommendation**: Qualify the Problem Statement to match A-2: note MySQL applies the SHA-256 suffix only in the long-form fallback and that its own documentation cites 2^32; frame this change as adopting the 64-bit SHA-256 *principle* for the always-hashed PG path.

---

### 5. FR-5/AC-9 hard-code backoff constants as requirements, coupling the spec to implementation detail (Score: 30)

FR-5 and AC-9 pin "starting at 100 ms and doubling up to a 1000 ms cap." These match the code (`delayMs = 100`, `Math.Min(delayMs * 2, 1000)`), and the intent ("preserve existing behaviour") is sound, but stating exact millisecond constants as functional requirements over-specifies retry timing that is incidental to this change (the lock-derivation swap). It risks brittle tests asserting sleep durations. Minor — the change *is* "preserve exactly," so pinning is defensible.

**Evidence**: "an exponential backoff retry loop starting at 100 ms and doubling up to a 1000 ms cap" (FR-5).

**Recommendation**: Phrase as "the existing exponential-backoff schedule (currently 100 ms initial, 1000 ms cap) is preserved unchanged," signalling that the values are characterised-as-is rather than newly mandated.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 4 |

**Total findings**: 5
**Findings at or above threshold (60)**: 0
