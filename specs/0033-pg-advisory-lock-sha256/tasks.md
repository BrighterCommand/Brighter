# Tasks: PG Advisory Lock SHA-256 64-bit Key (Spec 0033)

**Linked**: requirements.md · ADR 0062 · issue #4145

## Tasks

> Ordered by dependency. **Task 1 and Task 2 are mutually dependent and MUST land in the same
> commit.** The deadline test holds the contended lock by hand-rolling SQL keyed to the
> production derivation; migrating it *before* the production change breaks it (holder locks the
> new bigint key while `AcquireAsync` still uses `hashtext` → no contention → no
> `TimeoutException`), and changing production *before* migrating it breaks it the other way.
> Treat Task 1 as part of making Task 2's change land green: do Task 2's `/test-first` cycle
> (new objsubid test) first, then in the SAME implementation/commit apply both the production
> SQL swap (Task 2) and the deadline-test holder migration (Task 1). Tasks 3–4 add focused
> integration coverage and can follow in separate commits. Task 5 is a non-behavioral doc tidy.

### Task 1 — REGRESSION (structural, lands with Task 2): migrate the deadline/timeout test to hold the new bigint lock

This task MODIFIES an existing test, so it does not use the TDD `/test-first` template; it is a
behavior-preserving regression fix that must be committed **together with** Task 2's production
change (see ordering note above).

- [x] **REGRESSION FIX: deadline/TimeProvider test contends on the new single-arg bigint lock**
  - File (existing): `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_postgres_advisory_lock_deadline_is_evaluated_against_the_injected_time_provider_it_should_be_independent_of_wall_clock.cs`
  - Problem: the holder session hand-rolls `SELECT pg_advisory_lock(@ns, hashtext(@key))` (lines 71–73) and releases with the matching two-arg `pg_advisory_unlock(@ns, hashtext(@key))` (lines 122–124), with a mirrored `private const int LOCK_NAMESPACE = 74726` (line 50). Once Task 2 switches the system-under-test (`PostgreSqlAdvisoryLock.AcquireAsync`) to the single-arg bigint key, the holder and the contender no longer name the same lock, the contention disappears, and `AcquireAsync` would succeed immediately instead of timing out — the test would fail or hang.
  - Change required:
    - Replace the holder SQL with the single-arg overload: `SELECT pg_advisory_lock(@key)` with `@key` a `long`.
    - Replace the releaser SQL (DisposeAsync) with `SELECT pg_advisory_unlock(@key)` using the same `long`.
    - Because `DeriveLockKey` is `private` in the production class (per ADR 0062, NFR-3), replicate the derivation inline in the test: `SHA256.HashData(Encoding.UTF8.GetBytes($"74726:{_lockKey}"))` then `BinaryPrimitives.ReadInt64BigEndian(hash)`. Keep the `74726` namespace prefix so the test's holder lock is byte-identical to the production key.
    - Update the `LOCK_NAMESPACE` constant comment (lines 48–50) to describe the composite-prefix scheme rather than the `(namespace, hashtext(key))` pair.
  - Verification (no oracle change to the assertions): the migrated test still throws `TimeoutException` whose message contains `_lockKey`, and the wall-clock ceiling assertion still holds — proving the holder genuinely contends on the new bigint key and the deadline fires against the injected `FakeTimeProvider`. This preserves AC-7/AC-9.
  - Scope note (verified during planning): a grep of `tests/Paramore.Brighter.PostgresSQL.Tests/` for `hashtext|pg_advisory_lock|pg_try_advisory` returns this file plus six others; the six others either mention `pg_advisory_lock` only in prose comments or use `FakePostgreSqlAdvisoryLock` / the real `PostgreSqlBoxMigrationRunner` path (`When_postgres_migration_is_cancelled_mid_flight...` runs both holder and fresh runner through the production lock, so they stay in sync automatically). `HoldingPostgreSqlAdvisoryLock` delegates to an injected real inner `IPostgreSqlAdvisoryLock` and has no hand-rolled SQL. **Only this file needs migration.**

### Task 2 — primary behavioral change: acquire + release round-trip via the single-arg bigint overload

- [x] **TEST + IMPLEMENT: AcquireAsync and ReleaseAsync use the single-arg pg_(try_)advisory_(un)lock(bigint) overload with a SHA-256-derived 64-bit key**
  - **USE COMMAND**: `/test-first PostgreSqlAdvisoryLock acquires and releases a session advisory lock against real PostgreSQL using the single-argument bigint overload, verified via pg_locks objsubid = 1`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning"
  - Test file: `When_postgres_advisory_lock_acquires_and_releases_it_should_use_the_single_arg_bigint_overload.cs`
  - Test should verify (integration, real PostgreSQL via `PostgreSqlSettings.TestsBrighterConnectionString` + `new PostgresSqlTestHelper().SetupDatabase()`, on an open `NpgsqlConnection`):
    - `AcquireAsync(connection, lockKey, timeout, ct)` completes without throwing (AC-10).
    - While the lock is held, querying `SELECT classid, objid, objsubid FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()` (on the same backend session) returns a row with **`objsubid = 1`**, proving the single-arg `bigint` overload was used and the two-`int4` overload / `hashtext` was NOT (AC-1, AC-2, AC-12). (The two-arg overload would record `objsubid = 2`.)
    - `ReleaseAsync(connection, lockKey, ct)` returns `true` and the advisory lock is then absent from `pg_locks` for that session — confirming acquire and release name the same lock and the bigint key was released (AC-2, AC-3, AC-10).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should (in `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlAdvisoryLock.cs`):
    - Add `private static long DeriveLockKey(string lockKey)`: build `composite = $"{BRIGHTER_LOCK_NAMESPACE}:{lockKey}"`, compute `SHA256.HashData(Encoding.UTF8.GetBytes(composite))`, return `System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash)` (first 8 bytes, fixed big-endian, signed `long`) — FR-3, FR-4 (ADR lines 79–84). Add `using System.Security.Cryptography;` and `using System.Text;`.
    - Keep `private const int BRIGHTER_LOCK_NAMESPACE = 74726;` (line 64) but fold it into the hash; it is no longer a SQL argument (FR-3, AC-5).
    - In `AcquireAsync` (lines 89–92): replace the command with `command.CommandText = "SELECT pg_try_advisory_lock(@key)"; command.Parameters.AddWithValue("@key", DeriveLockKey(lockKey));`. Drop the `@ns` parameter and `hashtext` (FR-1, AC-1). Preserve the rest of the loop verbatim — `startTimestamp`/`delayMs`, `while (true)`, null-result `InvalidOperationException` guard, `GetElapsedTime >= timeout` `TimeoutException`, `Task.Delay`, `delayMs = Math.Min(delayMs * 2, 1000)` (FR-5).
    - In `ReleaseAsync` (lines 117–120): replace with `command.CommandText = "SELECT pg_advisory_unlock(@key)"; command.Parameters.AddWithValue("@key", DeriveLockKey(lockKey));`. Drop `@ns`/`hashtext`. Keep the `raw is bool released && released` return (FR-2, AC-2).
    - Do not touch the constructor, `_timeProvider`, or `IPostgreSqlAdvisoryLock` (NFR-2, AC-11).
    - In the SAME commit, apply Task 1's deadline-test migration (the two changes are interdependent; see ordering note).

### Task 3 — distinct keys map to distinct lock identities

- [x] **TEST + IMPLEMENT: two distinct lock keys acquire distinct advisory-lock identities and do not block each other** — coverage-only (characterisation); passes against Task 2 impl, no new production code.
  - **USE COMMAND**: `/test-first two distinct Brighter lock keys acquired on separate PostgreSQL sessions occupy distinct advisory-lock identities and do not contend`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning"
  - Test file: `When_two_distinct_lock_keys_are_acquired_they_should_occupy_distinct_advisory_lock_identities.cs`
  - Test should verify (integration, two separate `NpgsqlConnection` sessions):
    - `AcquireAsync` for `"BrighterMigration_public.Outbox"` on session A and `"BrighterMigration_billing.Outbox"` on session B both succeed without either timing out (they do not block one another) — AC-4.
    - Inspecting `pg_locks` shows two advisory rows with distinct `(classid, objid)` pairs (the 64-bit key splits as `classid` = high 32 bits, `objid` = low 32 bits), confirming distinct derived keys (AC-4, supports NFR-1/AC-12).
    - Both release cleanly (`ReleaseAsync` returns `true`).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should: require no further production change beyond Task 2 — `DeriveLockKey` already differentiates the two inputs. If this test passes immediately against the Task 2 implementation, record that it is a coverage-only (characterisation) test for FR-3/AC-4 and add no new production code.

### Task 4 — stable re-derivation for a fixed key

- [x] **TEST + IMPLEMENT: a fixed lock key acquires the same advisory-lock identity on every acquisition (deterministic derivation)** — coverage-only (characterisation); passes against Task 2 impl, no new production code.
  - **USE COMMAND**: `/test-first a fixed Brighter lock key maps to a stable PostgreSQL advisory-lock identity across acquire, release, and re-acquire`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning"
  - Test file: `When_a_fixed_lock_key_is_acquired_released_and_reacquired_it_should_map_to_the_same_advisory_lock_identity.cs`
  - Test should verify (integration):
    - `AcquireAsync` for a fixed `lockKey`; capture `(classid, objid)` from `pg_locks`; `ReleaseAsync` (returns `true`); `AcquireAsync` again for the same `lockKey`; capture `(classid, objid)` again.
    - The two captured `(classid, objid)` pairs are identical, demonstrating the derivation is stable for a fixed input across repeated acquisitions (AC-6, FR-4). `objsubid = 1` on both (single-arg overload).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should: require no further production change beyond Task 2 — `DeriveLockKey` is a pure deterministic function of `lockKey`. Coverage-only test for FR-4/AC-6.

### Task 5 — non-behavioral: update the class XML-doc remark

This is a tidy/documentation task; no behavior changes, no test.

- [x] **DOC TIDY: update the PostgreSqlAdvisoryLock XML-doc to describe the implemented bigint/SHA-256 scheme**
  - File (existing): `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlAdvisoryLock.cs`
  - Update the class summary (lines 32–35) so it references `pg_try_advisory_lock(bigint)` / `pg_advisory_unlock(bigint)` instead of the `(int4, int4)` overloads.
  - Replace the "Lock-key hashing" `<para>` (lines 42–54): describe the new scheme — composite `"74726:{lockKey}"`, SHA-256, first 8 bytes read big-endian via `BinaryPrimitives.ReadInt64BigEndian` into a signed `long`, passed to the single-arg `bigint` overload; ~1-in-2^64 birthday bound (NFR-1); namespace folded into the hash rather than a SQL arg. Remove the "#4145 follow-up / would push this to 2^64" wording, since this change implements it (the ADR supersedes that inline TODO).
  - Optionally refresh the `BRIGHTER_LOCK_NAMESPACE` comment (lines 58–63) to note it is now folded into the hash input, not a SQL argument.
  - Verification: solution builds; XML doc is accurate. No test.

## Coverage Cross-Reference

### Requirements → Task

| Item | Description | Task(s) |
|------|-------------|---------|
| FR-1 | Acquire uses single-arg `pg_try_advisory_lock(bigint)`, no `hashtext`/`@ns` | Task 2 |
| FR-2 | Release uses single-arg `pg_advisory_unlock(bigint)` with identical key | Task 2 |
| FR-3 | 64-bit key from SHA-256 over namespace + lockKey | Task 2 (impl); Task 3 (distinctness) |
| FR-4 | Determinism — same input → same key | Task 2 (impl: big-endian read); Task 4 (stable re-derivation) |
| FR-5 | Retry/backoff/timeout/TimeProvider/null-guard preserved | Task 2 (preserve control flow verbatim); Task 1 (deadline/TimeProvider regression test migrated and still passing) |
| NFR-1 | 64-bit collision space | Task 2 (impl); Task 3/Task 4 (pg_locks shows derived bigint, not hashtext) |
| NFR-2 | No public API change; `IPostgreSqlAdvisoryLock` unchanged | Task 2 (impl confined to class) |
| NFR-3 | Determinism independently verifiable via integration; `DeriveLockKey` stays private | Task 2 (objsubid=1 oracle); Task 3; Task 4 |
| ADR 0062 decision | Signed 64-bit from SHA-256 of `"74726:{lockKey}"`, first 8 bytes big-endian, single-arg bigint overloads; private `DeriveLockKey`; XML-doc updated | Task 2 (derivation + SQL swap, private helper); Task 5 (XML-doc) |

### Acceptance Criteria → Task notes

- **AC-1** (acquire calls bigint overload) — Task 2 (`objsubid = 1` in `pg_locks`).
- **AC-2** (release calls bigint overload, identical key, returns bool) — Task 2 (release returns `true`, lock gone).
- **AC-3** (acquire/release derive byte-identical keys) — Task 2; structurally guaranteed by both paths calling the single `DeriveLockKey`, and demonstrated by the successful round-trip release.
- **AC-4** (distinct keys → distinct identities, integration) — Task 3.
- **AC-5** (namespace participates in derivation) — Task 2: covered by the implementation forming `"74726:{lockKey}"`; not independently `pg_locks`-observable, so verified by construction within the Task 2 integration acquisition (no separate negative-oracle test), per the spec instruction.
- **AC-6** (determinism across repeated derivation, integration) — Task 4.
- **AC-7** (timeout preserved) — Task 1 (migrated deadline test) + Task 2 (control flow preserved).
- **AC-8** (null-result `InvalidOperationException` guard preserved) — Task 2 (guard kept verbatim). Covered by code preservation; no dedicated new test added (no existing null-result test in the suite; the guard is unchanged FR-5 control flow). See Gaps.
- **AC-9** (backoff + TimeProvider wiring preserved, constructor default) — Task 1 (migrated deadline test exercises the TimeProvider/backoff path) + Task 2 (constructor and loop untouched).
- **AC-10** (acquire + release round-trip against PostgreSQL) — Task 2.
- **AC-11** (interface unchanged) — Task 2 (no edit to `IPostgreSqlAdvisoryLock`; existing call sites compile unchanged).
- **AC-12** (collision-space argument: 64-bit SHA-256 value) — Task 2 (impl) + Task 3/Task 4 (derived bigint visible in `pg_locks`, no `hashtext`).

### Gaps

- **AC-8** has no dedicated new test: it is FR-5 control-flow preservation of an existing guard, with no pre-existing test in the suite and no mock seam to force `pg_try_advisory_lock` to return `null` against a real PostgreSQL (the function never returns null in practice). It is covered by the verbatim preservation of the null-guard in Task 2. Flagged for awareness; adding a unit test would require a seam not in scope (NFR-2 forbids interface change). **No other gaps.**
