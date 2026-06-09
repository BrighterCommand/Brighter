# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4145

## Problem Statement

As a Brighter operator running PostgreSQL box-table provisioning across many schemas and tables, I want the advisory lock that serialises migrations to use a 64-bit key space (matching the principle already established for the MySQL backend) so that the probability of two unrelated migration lock keys colliding — and therefore needlessly serialising on a shared advisory lock — is reduced from roughly 1-in-2^32 to roughly 1-in-2^64.

Today `PostgreSqlAdvisoryLock` derives its lock from `pg_try_advisory_lock(int4 namespace, hashtext(text))`. PostgreSQL's `hashtext` returns a 32-bit value, so distinct Brighter lock-key strings carry a birthday-bound collision probability of approximately 1-in-2^32 across a deployment's active locks. The collision failure mode is benign (two unrelated migrations serialise on a shared lock; correctness is preserved, only the concurrency boundary widens), but the collision surface is larger than the equivalent MySQL path, which derives its key from the first 8 bytes (64 bits) of SHA-256.

## Proposed Solution

Change `PostgreSqlAdvisoryLock` so that both acquisition and release derive the advisory-lock identity from a deterministic 64-bit value computed as SHA-256 over the composite of the Brighter lock namespace and the supplied lock-key string, and pass that single 64-bit value to the single-argument `pg_try_advisory_lock(bigint)` / `pg_advisory_unlock(bigint)` PostgreSQL overloads. The lock-namespace concept (currently the separate `int4` argument) is folded into the hash input rather than remaining a separate parameter, because the `bigint` overload exposes one combined 64-bit lock space rather than a two-`int4` space.

From the caller's perspective nothing changes: callers still pass the same `string lockKey`, the same `NpgsqlConnection`, the same `timeout`, and the same `CancellationToken`. The retry/backoff/timeout behaviour and its `TimeProvider`-driven deadline are unchanged. Only the internal derivation of the integer lock identity changes, reducing the collision space.

This feature has not yet shipped in a released Brighter version (targeted for V10.X), so the lock-key derivation may change freely with no backward-compatibility obligation toward an earlier derivation.

## Requirements

### Functional Requirements

**FR-1: Acquisition uses the single-argument bigint advisory-lock overload.**
`AcquireAsync` MUST acquire the lock by invoking the single-argument `pg_try_advisory_lock(bigint)` overload, supplying the 64-bit key derived per FR-3. It MUST NOT invoke the two-argument `pg_try_advisory_lock(int4, int4)` overload and MUST NOT call `hashtext`.
Example: for `lockKey = "BrighterMigration_public.Outbox"`, the command executed is `SELECT pg_try_advisory_lock(@key)` where `@key` is the bigint derived in FR-3 for that input — there is no separate `@ns` integer argument on the call.

**FR-2: Release uses the single-argument bigint advisory-unlock overload with the identical key.**
`ReleaseAsync` MUST release the lock by invoking the single-argument `pg_advisory_unlock(bigint)` overload, supplying the same 64-bit key value that `AcquireAsync` would derive for the same `lockKey`. The key supplied to release for a given `lockKey` MUST be byte-for-byte equal to the key supplied to acquire for that same `lockKey`. It MUST NOT invoke the two-argument `pg_advisory_unlock(int4, int4)` overload and MUST NOT call `hashtext`.
Example: if `AcquireAsync` was called with `lockKey = "BrighterMigration_public.Outbox"` and derived bigint key `K`, then `ReleaseAsync` called with the same `lockKey` executes `SELECT pg_advisory_unlock(@key)` with `@key == K`. `ReleaseAsync` continues to return `bool` (`true` if the calling session held the lock, `false` otherwise) per the existing `IPostgreSqlAdvisoryLock` contract.

**FR-3: 64-bit key derived from SHA-256 over namespace + lock key.**
The implementation MUST derive the advisory-lock key as a 64-bit value taken from the SHA-256 digest of a composite input formed from the Brighter lock namespace and the `lockKey` string. The "Brighter lock namespace" is the value currently passed as the `int4` namespace argument to the two-argument overload — the constant `BRIGHTER_LOCK_NAMESPACE = 74726` (`PostgreSqlAdvisoryLock.cs:64`). It MUST be folded into the hash input (it MUST NOT be passed as a separate SQL argument). The precise bytes-to-bigint conversion (byte ordering, signed vs. unsigned interpretation, which 8 bytes of the digest are taken) and the exact byte-encoding of the namespace contribution (e.g. whether `74726` is encoded as its decimal string, its big-endian int32 bytes, or a literal prefix) are design-level concerns to be specified in the ADR; this requirement fixes only that the namespace constant `74726` is present in the hashed input and that the result is a 64-bit value derived from SHA-256 over (namespace composed with lockKey).
Example: two distinct lock keys `"BrighterMigration_public.Outbox"` and `"BrighterMigration_billing.Outbox"` MUST produce two different 64-bit key values (absent a 2^64 birthday collision). The namespace contribution MUST be present in the hashed input such that the derivation is not a function of `lockKey` alone.

**FR-4: Determinism — same input yields the same key every time and across processes.**
For a fixed `lockKey` string, the derived 64-bit key MUST be identical on every invocation, in every process, on every host, regardless of platform, locale, culture, or time. The derivation MUST depend only on the (namespace, `lockKey`) input and MUST NOT depend on any runtime, random, environmental, or time-varying state.
Example: deriving the key for `lockKey = "BrighterMigration_public.Outbox"` in process A and again in process B (different machine, different OS culture) MUST yield the exact same bigint value.

**FR-5: Existing retry, backoff, timeout, and TimeProvider behaviour preserved.**
The acquisition control flow MUST remain unchanged apart from the lock-derivation/overload swap: an exponential backoff retry loop starting at 100 ms and doubling up to a 1000 ms cap, a deadline measured against the injected `TimeProvider` via `GetTimestamp`/`GetElapsedTime`, a `TimeoutException` thrown (with a message naming the `lockKey` and the timeout in seconds) when the timeout elapses before the lock is held, and an `InvalidOperationException` thrown when `pg_try_advisory_lock` returns null instead of a boolean. The constructor MUST continue to accept an optional `TimeProvider?` defaulting to `TimeProvider.System`.
Example: with a fake `TimeProvider` whose elapsed time exceeds `timeout` after the first failed acquisition attempt, `AcquireAsync` throws `TimeoutException` whose message contains the `lockKey` and the timeout seconds — exactly as before the change.

### Non-functional Requirements

**NFR-1: Collision-space improvement (measurable).** The birthday-bound collision probability across distinct lock keys MUST be governed by a 64-bit key space (~1-in-2^64) rather than the current 32-bit space (~1-in-2^32). This is demonstrable by argument: the key fed to PostgreSQL is a 64-bit value drawn from SHA-256 (a cryptographic hash with uniform output distribution) rather than the 32-bit `hashtext` output, so collisions among a population of N distinct keys follow the birthday bound over 2^64 rather than 2^32.

**NFR-2: No public API surface change.** The change MUST be confined to the `PostgreSqlAdvisoryLock` implementation. The `IPostgreSqlAdvisoryLock` interface signatures (`AcquireAsync`, `ReleaseAsync`) MUST remain unchanged, so no caller recompilation or call-site change is required.

**NFR-3: Determinism is independently verifiable.** The derivation MUST be unit-testable without a database connection — i.e. it MUST be possible to assert that a given `lockKey` maps to a specific known 64-bit value and that two distinct keys map to two distinct values, in a pure (no-I/O) test.

### Constraints and Assumptions

- C-1: PostgreSQL provides the single-argument `pg_try_advisory_lock(bigint)` and `pg_advisory_unlock(bigint)` overloads (standard in all supported PostgreSQL versions). The implementation relies on these overloads existing.
- C-2: This PostgreSQL box-provisioning feature has NOT shipped in a released Brighter version (targeted V10.X). There is therefore no deployed population of locks using the old `hashtext`-derived keys to remain compatible with; the derivation may change freely.
- C-3: The `lockKey` is supplied by the caller (typically `BrighterMigration_<schema>.<table>` from the runner) and arrives as a `string`; the hashing happens inside the implementation. The interface contract for `lockKey` is unchanged.
- A-1: Per-deployment active-lock population is small (typically fewer than 100 box tables), so even the prior 32-bit space was practically adequate; this change hardens the margin and aligns with the MySQL backend's 64-bit-SHA-256 principle.
- A-2: The "MySQL convention" being mirrored is the *principle* of a 64-bit SHA-256-derived key (see `MySqlMigrationLockName.For`, which uses the first 8 bytes of SHA-256 only in its long-form fallback), not a literal shared code path.

### Out of Scope

- OOS-1: Changing the `IPostgreSqlAdvisoryLock` interface signatures or any other public API.
- OOS-2: Any change to the MySQL, MSSQL, or any other (e.g. Sqlite, DynamoDB) backend or lock helper.
- OOS-3: Rolling-deploy coordination, migration-pause coordination, or any backward-compatibility handling for keys derived by the previous `hashtext` scheme (explicitly out of scope because the feature is unreleased).
- OOS-4: Release-notes or upgrade-guide content describing a lock-key change for existing deployments.
- OOS-5: Specifying the exact bytes-to-bigint conversion mechanics (endianness, signed/unsigned, digest-slice selection) — these are design decisions for the ADR, not requirements.

## Acceptance Criteria

**AC-1 (FR-1): Acquire calls the bigint overload.**
Given a `PostgreSqlAdvisoryLock` and a `lockKey`,
When `AcquireAsync` issues its lock command,
Then the executed SQL invokes the single-argument `pg_try_advisory_lock(bigint)` overload with the FR-3-derived key, and does not reference `hashtext` nor pass a separate namespace argument.

**AC-2 (FR-2): Release calls the bigint overload with the identical key.**
Given a `lockKey` for which `AcquireAsync` derives bigint key `K`,
When `ReleaseAsync` is called with the same `lockKey`,
Then the executed SQL invokes the single-argument `pg_advisory_unlock(bigint)` overload with key value exactly equal to `K`, does not reference `hashtext`, and returns a `bool`.

**AC-3 (FR-2): Acquire and release derive byte-identical keys.**
Given any `lockKey`,
When the keys derived for the acquire path and the release path are compared,
Then they are byte-for-byte equal.

**AC-4 (FR-3): Distinct lock keys yield distinct 64-bit keys.**
Given two distinct lock keys `"BrighterMigration_public.Outbox"` and `"BrighterMigration_billing.Outbox"`,
When their keys are derived,
Then the two derived 64-bit values differ.

**AC-5 (FR-3): Namespace participates in the derivation.**
Given the Brighter lock namespace constant `74726` and a `lockKey`,
When the key is derived,
Then the derivation hashes a composite that includes the namespace constant `74726` (the namespace is not passed as a separate SQL argument), and the derived value is not equal to a SHA-256-over-`lockKey`-alone derivation that omits the namespace.

**AC-6 (FR-4): Determinism across repeated and cross-process derivation.**
Given a fixed `lockKey`,
When the key is derived multiple times (including under different OS culture/locale settings simulating a different process/host),
Then every derivation produces the identical 64-bit value.

**AC-7 (FR-5): Timeout behaviour preserved.**
Given an injected fake `TimeProvider` configured so elapsed time exceeds `timeout` after a failed acquisition attempt, and a connection on which `pg_try_advisory_lock` returns `false`,
When `AcquireAsync` runs,
Then it throws `TimeoutException` whose message contains the `lockKey` and the timeout in seconds.

**AC-8 (FR-5): Null-result guard preserved.**
Given a connection on which `pg_try_advisory_lock` returns `null` (not a boolean),
When `AcquireAsync` runs,
Then it throws `InvalidOperationException` whose message names the `lockKey`.

**AC-9 (FR-5): Backoff and TimeProvider wiring preserved.**
Given repeated failed acquisition attempts within the timeout budget,
When `AcquireAsync` retries,
Then it sleeps with exponential backoff starting at 100 ms and doubling to a 1000 ms cap, measures elapsed time via the injected `TimeProvider`, and the constructor defaults `TimeProvider` to `TimeProvider.System` when none is supplied.

**AC-10 (FR-1, FR-2): Lock can be acquired and released against PostgreSQL.**
Given an open `NpgsqlConnection` to a PostgreSQL instance,
When `AcquireAsync` is called with a `lockKey` and then `ReleaseAsync` is called with the same `lockKey` on the same session,
Then acquisition succeeds (no exception) and `ReleaseAsync` returns `true`, confirming the bigint key acquired and released name the same advisory lock.

**AC-11 (NFR-2): Interface unchanged.**
Given the `IPostgreSqlAdvisoryLock` interface,
When the change is complete,
Then the `AcquireAsync` and `ReleaseAsync` signatures are byte-for-byte unchanged and no call site requires modification.

**AC-12 (NFR-1): Collision-space argument holds.**
Given the new derivation,
When the key supplied to PostgreSQL is examined,
Then it is a 64-bit value drawn from SHA-256 (not the 32-bit `hashtext` output), establishing a ~1-in-2^64 birthday bound in place of ~1-in-2^32.

## Additional Context

- Current implementation: `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlAdvisoryLock.cs`. `AcquireAsync` currently runs `SELECT pg_try_advisory_lock(@ns, hashtext(@key))` in an exponential-backoff loop (100 ms doubling to 1000 ms) bounded by a `TimeProvider`-measured deadline; `ReleaseAsync` runs `SELECT pg_advisory_unlock(@ns, hashtext(@key))` and returns a bool. `BRIGHTER_LOCK_NAMESPACE = 74726` ("BRIG" ASCII) is currently the `int4` namespace argument and, under this change, folds into the hash input.
- Interface: `src/Paramore.Brighter.BoxProvisioning.PostgreSql/IPostgreSqlAdvisoryLock.cs` — `AcquireAsync`/`ReleaseAsync` take `string lockKey` plus an `NpgsqlConnection`, timeout, and `CancellationToken`; hashing is an implementation detail, so the contract does not change.
- MySQL reference: `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlMigrationLockName.cs` — `MySqlMigrationLockName.For` uses SHA-256 only in its long-form fallback (composite names > 64 chars), taking the first 8 bytes (16 hex chars) as a suffix; the simple form uses the plaintext `GET_LOCK` name. The principle to mirror is the 64-bit SHA-256-derived key, not an identical code path.
- The `PostgreSqlAdvisoryLock` class XML-doc remark already references issue #4145 as the follow-up; that reference will be superseded/updated by this change's implementation but no separate documentation deliverable is in scope here.
- The bytes-to-bigint mechanics (endianness, signed/unsigned, digest-slice selection) are deferred to the ADR.
