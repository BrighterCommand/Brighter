# 0062. PostgreSQL Box-Provisioning Advisory Lock — SHA-256 64-bit Key Derivation

Date: 2026-06-09

## Status

Accepted

## Context

PostgreSQL box-table provisioning serialises concurrent migrations behind a session-level advisory lock implemented by `PostgreSqlAdvisoryLock` (`src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlAdvisoryLock.cs`). Today both paths use the two-`int4` advisory-lock overloads and let PostgreSQL hash the key:

- `AcquireAsync` runs `SELECT pg_try_advisory_lock(@ns, hashtext(@key))` (line 90) inside an exponential-backoff loop, with `@ns = BRIGHTER_LOCK_NAMESPACE` (the `const int BRIGHTER_LOCK_NAMESPACE = 74726` at line 64, "BRIG" as ASCII) and `@key = lockKey`.
- `ReleaseAsync` runs `SELECT pg_advisory_unlock(@ns, hashtext(@key))` (line 118) and returns `bool` (line 123).

PostgreSQL's `hashtext(text)` returns a 32-bit `int4`. The advisory-lock identity for a Brighter migration is therefore effectively a 32-bit value, giving a birthday-bound collision probability of approximately 1-in-2^32 across the distinct lock keys active in a deployment. The collision failure mode is benign — two unrelated migrations needlessly serialise on a shared advisory lock; correctness is preserved, only the concurrency boundary widens — but the collision surface is wider than the MySQL backend, whose long-form fallback derives a key from the first 8 bytes (64 bits) of SHA-256.

This ADR records the single decision of **how `PostgreSqlAdvisoryLock` derives the 64-bit advisory-lock key**, switching to the single-argument `pg_(try_)advisory_(un)lock(bigint)` overloads and computing the `bigint` in C# from SHA-256.

**Parent Requirement**: [specs/0033-pg-advisory-lock-sha256/requirements.md](../../specs/0033-pg-advisory-lock-sha256/requirements.md) — defines FR-1..FR-5, NFR-1..NFR-3, AC-1..AC-12, C-1..C-3, A-1..A-2, OOS-1..OOS-5.

**Scope**: The change is confined to the `PostgreSqlAdvisoryLock` implementation — the SQL command text and the key computation. The `IPostgreSqlAdvisoryLock` interface (`src/Paramore.Brighter.BoxProvisioning.PostgreSql/IPostgreSqlAdvisoryLock.cs`), the retry/backoff/timeout/`TimeProvider` control flow, and all callers are untouched (NFR-2, OOS-1, OOS-2).

**Forces**
- Reduce the collision space from ~1-in-2^32 to ~1-in-2^64 (NFR-1, AC-12).
- Determinism: the same `lockKey` must map to the same `bigint` on every host, OS, culture, and process (FR-4, AC-6).
- The acquire-derived key and the release-derived key for the same `lockKey` must be byte-for-byte identical (FR-2, AC-3).
- The derivation must be unit-testable with no database I/O (NFR-3).
- The existing retry/backoff/timeout/null-guard control flow must be preserved exactly (FR-5, AC-7/AC-8/AC-9).
- Align with the principle the MySQL backend already follows (A-2).

**Constraints**
- C-1: PostgreSQL provides the single-argument `pg_try_advisory_lock(bigint)` / `pg_advisory_unlock(bigint)` overloads in all supported versions.
- C-2: This feature is unreleased (targeted V10.X). There is no deployed population of `hashtext`-derived locks to remain compatible with, so the key derivation may change freely (also OOS-3, OOS-4).
- C-3: `lockKey` arrives as a caller-supplied `string` (typically `BrighterMigration_<schema>.<table>`); the interface contract for `lockKey` is unchanged.

## Decision

`PostgreSqlAdvisoryLock` will derive the advisory-lock key as a signed 64-bit value computed in C# from the SHA-256 digest of a composite of the namespace constant and the `lockKey`, and pass that single value to the single-argument `bigint` advisory-lock overloads.

### Architecture Overview

```
lockKey: "BrighterMigration_public.Outbox"
        │
        ▼
  composite = "74726:BrighterMigration_public.Outbox"      ← namespace folded in (FR-3, AC-5)
        │  UTF-8
        ▼
  digest = SHA256.HashData(bytes)        32 bytes          ← C#, no DB I/O (NFR-3)
        │  first 8 bytes, fixed big-endian
        ▼
  key (long) = BinaryPrimitives.ReadInt64BigEndian(digest) ← deterministic (FR-4, AC-6)
        │
        ▼
  SELECT pg_try_advisory_lock(@key)   /  pg_advisory_unlock(@key)   ← single-arg bigint (FR-1, FR-2)
```

The namespace is no longer a separate SQL argument: the single-argument overload exposes one 64-bit lock space, so the namespace participates only by being part of the hashed input.

### Key Components

- `PostgreSqlAdvisoryLock` — the sole class changed. The namespace constant `BRIGHTER_LOCK_NAMESPACE = 74726` (currently the `int4` SQL argument) is retained as a `const`, but is now folded into the hash input rather than passed to SQL.
- A new **private** key-derivation helper inside `PostgreSqlAdvisoryLock`, e.g. `private static long DeriveLockKey(string lockKey)`. This is a "knowing"/"deciding" responsibility (how a symbolic key maps to a 64-bit lock identity); see *Responsibility-Driven Design* below for why it stays private rather than becoming a separate named type. Both `AcquireAsync` and `ReleaseAsync` call this single helper, which structurally guarantees FR-2/AC-3 (acquire and release derive byte-identical keys).
- `IPostgreSqlAdvisoryLock` — unchanged (NFR-2, OOS-1). Its docstring on `lockKey` already states "the implementation hashes this into the integer space that Postgres advisory locks operate on", which remains accurate.

### Technology Choices

- **`SHA256.HashData(...)` (static)** for the digest. The project (`Paramore.Brighter.BoxProvisioning.PostgreSql.csproj`) multi-targets `net8.0;net9.0;net10.0` (resolved from `BrighterCoreTargetFrameworks` in `src/Directory.Build.props:45`). The static `SHA256.HashData` and `System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian` are both available on every targeted TFM, so **no netstandard2.0 fallback** (`using var sha = SHA256.Create(); sha.ComputeHash(...)`) is required. `BinaryPrimitives` is not currently referenced anywhere under `src/`, but it is a core BCL type in `System.Buffers.Binary` — no new package reference is needed.
- **`System.Text.Encoding.UTF8`** for the composite-to-bytes step, fixing the byte encoding independent of process/host culture (FR-4).
- **`BinaryPrimitives.ReadInt64BigEndian`** rather than `BitConverter.ToInt64`. `BitConverter` is machine-endian-dependent, which would make the derived key differ between little- and big-endian hosts and violate FR-4 cross-platform determinism. Reading a fixed big-endian `Int64` produces an identical value on every host.
- **Signed `long`** maps exactly onto PostgreSQL `bigint` (a signed 64-bit integer), with no range loss; the parameter is passed as a `long`.

### Implementation Approach

```csharp
private const int BRIGHTER_LOCK_NAMESPACE = 74726; // "BRIG" ASCII — now folded into the hash, not a SQL arg

private static long DeriveLockKey(string lockKey)
{
    var composite = $"{BRIGHTER_LOCK_NAMESPACE}:{lockKey}";        // e.g. "74726:BrighterMigration_public.Outbox"
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(composite)); // 32-byte digest
    return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash); // first 8 bytes, fixed big-endian
}
```

`AcquireAsync` keeps its existing structure verbatim — `startTimestamp`/`delayMs` setup, the `while (true)` loop, the null-result `InvalidOperationException` guard, the `GetElapsedTime >= timeout` `TimeoutException` (message naming `lockKey` and `timeout.TotalSeconds`), the `Task.Delay` with `delayMs = Math.Min(delayMs * 2, 1000)`, and the `TimeProvider` defaulting to `TimeProvider.System` in the constructor (FR-5; AC-7/AC-8/AC-9). Only the per-iteration command changes:

```csharp
var key = DeriveLockKey(lockKey);
using var command = connection.CreateCommand();
command.CommandText = "SELECT pg_try_advisory_lock(@key)";
command.Parameters.AddWithValue("@key", key);
```

`ReleaseAsync` mirrors this, retaining its `bool` return:

```csharp
var key = DeriveLockKey(lockKey);
using var command = connection.CreateCommand();
command.CommandText = "SELECT pg_advisory_unlock(@key)";
command.Parameters.AddWithValue("@key", key);
var raw = await command.ExecuteScalarAsync(cancellationToken);
return raw is bool released && released;
```

No call to `hashtext` and no `@ns` parameter remain on either path (FR-1, FR-2, AC-1, AC-2). The class XML-doc remark currently citing issue #4145 as a follow-up (lines 42–54) will be updated to describe the implemented `bigint`/SHA-256 scheme.

### Responsibility-Driven Design

Deriving the lock identity is a *knowing*/*deciding* responsibility: how does a symbolic `lockKey` become the integer that PostgreSQL locks on? Today that responsibility is **split** between C# (the namespace constant) and SQL (`hashtext` does the actual hashing and width reduction). That split is the cohesion smell: no single role owns "the lock identity", and the SQL-side half cannot be unit-tested without a database.

This decision **consolidates the responsibility into one cohesive C# unit** — `DeriveLockKey` — making `PostgreSqlAdvisoryLock` the single owner of both the namespace and the hash, and making the derivation pure and testable (NFR-3). The remaining design question is *where* that unit lives:

- **Private static method inside `PostgreSqlAdvisoryLock` (chosen).** There is exactly one caller class, the `IPostgreSqlAdvisoryLock` interface is unchanged (NFR-2/OOS-1), and the derivation is intrinsic to *this* backend's lock primitive. Keeping it private respects "do not add new types without necessity" and keeps interior details free to change (Preserve Flexibility). Both `AcquireAsync` and `ReleaseAsync` route through it, which is what guarantees AC-3.
- **Separate named static helper type (e.g. `PostgreSqlMigrationLockKey`) paralleling `MySqlMigrationLockName` (rejected, but legitimate).** This would give cross-backend symmetry: each relational backend has a named "lock-name/lock-key" type. It is a genuine alternative. It is not chosen because (a) MySQL needs a *public* helper since its key feeds a `string` lock name the caller can observe and reason about under the 64-char `GET_LOCK` limit, whereas the PG key is an opaque `bigint` implementation detail with one internal consumer; (b) extracting a public type would add API surface this spec scopes out; and (c) the cohesion goal (one owner of the derivation) is already met by the private method. Symmetry across backends is desirable but secondary to not minting an unnecessary public type. If a future spec needs the PG key derivation shared (e.g. an external Vault/KMS lock-key path, as `IPostgreSqlAdvisoryLock`'s remark hints), promoting the private method to a named type is a cheap, behaviour-preserving refactor at that point.

## Consequences

### Positive
- Collision space hardened from ~1-in-2^32 to ~1-in-2^64 (NFR-1, AC-12): the value handed to PostgreSQL is now 64 bits drawn from a cryptographic hash with uniform output, not the 32-bit `hashtext` output.
- The derivation is a pure C# function with no database dependency. Verification is integration-based (NFR-3, AC-4, AC-6): `DeriveLockKey` stays `private`, so rather than a no-DB unit test asserting an exact 64-bit literal, the derivation is verified against a real PostgreSQL — acquiring the lock and observing from a separate session that `pg_locks` records the expected single-arg-`bigint` shape (`objsubid = 1`), that a fixed `lockKey` maps to a stable identity, and that distinct keys map to distinct identities. (Decision: integration-only verification was chosen over making the helper `internal` + `InternalsVisibleTo`, keeping the type surface unchanged.)
- Acquire and release derive the key through the same single helper, structurally guaranteeing byte-for-byte equality (FR-2, AC-3) — previously this depended on both SQL strings passing the same `@ns`/`@key` to `hashtext`.
- Aligns the PG backend with the 64-bit-SHA-256 principle the MySQL long-form fallback follows (A-2), without sharing a code path (the two backends keep their distinct shapes).
- No public API change; no caller recompilation (NFR-2, AC-11). Control flow (retry/backoff/timeout/null-guard/`TimeProvider`) is preserved exactly (FR-5).
- Removes reliance on PostgreSQL's `hashtext`, whose hashing algorithm is a database-internal detail; the derivation is now fully owned and versioned in Brighter source.

### Negative
- **The lock-key value changes** versus the old `hashtext`-based scheme. A process running the old derivation and a process running the new one would compute different advisory-lock identities for the same `lockKey` and would not mutually exclude. This is acceptable **only** because the feature is unreleased (C-2, OOS-3): there is no deployed lock population to coordinate with, and no rolling-deploy or upgrade-guide handling is in scope (OOS-3, OOS-4).
- One additional in-process SHA-256 computation (32-byte digest over a short string) per acquire attempt and per release. This is negligible relative to the database round-trip it accompanies, and the per-deployment lock population is small (A-1, typically < 100 box tables).
- The namespace is now **opaque inside a hash** rather than visible as a distinct SQL argument. An operator inspecting `pg_locks` previously saw the `int4` namespace `74726` as the `classid`/`objid` pair; now they see a single derived `bigint` `objid` with no human-readable namespace, making locks slightly harder to eyeball or correlate by hand. (Mitigated: any operator who needs to map a `bigint` back to a `lockKey` can run the deterministic derivation offline.)

### Risks and Mitigations
- **Risk:** an inadvertent endianness or signedness mistake makes the key non-deterministic across architectures (FR-4 violation). **Mitigation:** `BinaryPrimitives.ReadInt64BigEndian` fixes the byte order explicitly and is covered by the no-I/O determinism tests (NFR-3, AC-6), including a culture/locale-varied derivation assertion.
- **Risk:** acquire and release drift apart and derive different keys (FR-2 violation), e.g. if release is later changed to inline its own derivation. **Mitigation:** both paths call the single `DeriveLockKey` helper; AC-3 is asserted directly in tests.
- **Risk:** a future regression silently reintroduces `hashtext` or the two-arg overload. **Mitigation:** AC-1/AC-2 assert the executed SQL invokes the single-arg `bigint` overload and references no `hashtext` and no namespace argument.
- **Risk:** `BinaryPrimitives` reads fewer than 8 bytes if a future change passes a shorter buffer. **Mitigation:** SHA-256 always yields 32 bytes; the read of the first 8 is total and safe.

## Alternatives Considered

**(a) Keep the two-`int4` overload but feed it SHA-256-derived int4s (e.g. split the digest into two 32-bit halves).** This would still widen the key beyond `hashtext` and avoid an overload swap. Rejected: PostgreSQL's two-`int4` advisory-lock space is still a 64-bit space, so this gains nothing over the single `bigint` while keeping two parameters and a more awkward digest-slicing rule; the single-argument `bigint` overload is the direct, intention-revealing expression of "one 64-bit key" (FR-1, FR-2, C-1) and is what the requirement targets.

**(b) `BitConverter.ToInt64(hash, 0)` instead of `BinaryPrimitives.ReadInt64BigEndian`.** Simpler-looking and idiomatic. Rejected: `BitConverter` is machine-endian-dependent, so the derived `bigint` would differ on a big-endian or differing architecture — a direct FR-4 / AC-6 violation. Fixing big-endian via `BinaryPrimitives` makes the value identical on every host.

**(c) Take the last 8 bytes, or a different 8-byte slice, of the digest.** Functionally equivalent for collision resistance (SHA-256 output is uniform, so any fixed 8-byte slice is equidistributed). Rejected for consistency: the MySQL reference (`MySqlMigrationLockName.ShortHashOf`, `Convert.ToHexString(hashBytes, 0, HashHexChars / 2)` = first 8 bytes) takes the *first* 8 bytes. Mirroring "first 8 bytes" keeps the cross-backend principle legible (A-2); an arbitrary alternative slice would be an unexplained divergence.

**(d) Extract a separate public named helper type (`PostgreSqlMigrationLockKey`) paralleling `MySqlMigrationLockName`.** Discussed under *Responsibility-Driven Design*. Rejected for now: it adds public API surface this spec scopes out (OOS-1 covers the interface; minting a new public type is unnecessary per "do not add new types without necessity"), and the PG key is an opaque internal `bigint` with one consumer, unlike MySQL's caller-observable `string` name. Cohesion is already satisfied by the private method; symmetry is secondary. Promotable later if a sharing need arises.

**(e) Keep `hashtext` (do nothing / status quo).** Rejected: leaves the collision space at ~1-in-2^32, keeps the derivation split between C# and SQL (untestable without a database), and ties the key to a PostgreSQL-internal hash. This is exactly what the requirement (FR-3, NFR-1, NFR-3) sets out to change; the existing code already flags the move as a tracked follow-up (issue #4145, `PostgreSqlAdvisoryLock.cs:53`).

## References

- Requirements: [specs/0033-pg-advisory-lock-sha256/requirements.md](../../specs/0033-pg-advisory-lock-sha256/requirements.md) (FR-1..FR-5, NFR-1..NFR-3, AC-1..AC-12, C-1..C-3, A-1..A-2, OOS-1..OOS-5)
- Related ADRs:
  - [ADR 0057 Box Schema Versioning and Migrations](0057-box-schema-versioning-and-migrations.md) — establishes the per-backend advisory-lock abstraction and the PostgreSQL `pg_try_advisory_lock` + `BEGIN` model that `PostgreSqlAdvisoryLock` implements.
  - [ADR 0058 Box Provisioning RDD Role Interfaces](0058-box-provisioning-rdd-role-interfaces.md) — notes the `I*AdvisoryLock` family deliberately does not share a base because the per-backend lock primitives have different return semantics (PG `bool`); this ADR keeps PG's distinct shape.
  - This ADR supersedes the inline follow-up TODO referencing issue #4145 in `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlAdvisoryLock.cs` (lines 51–54).
- MySQL reference (principle mirrored, not shared): `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlMigrationLockName.cs` — `ShortHashOf` (lines 98–102) takes the first 8 bytes of SHA-256 only in the long-form fallback (name > 64 chars); its own docstring cites a ~1-in-2^32 birthday bound over the truncated slice (A-2).
- External references:
  - PostgreSQL advisory-lock functions — `pg_try_advisory_lock(bigint)`, `pg_advisory_unlock(bigint)` (single-argument overloads): https://www.postgresql.org/docs/current/functions-admin.html#FUNCTIONS-ADVISORY-LOCKS-TABLE
  - Issue: https://github.com/BrighterCommand/Brighter/issues/4145
