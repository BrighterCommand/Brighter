# Spec 0033: PG Box Provisioning Advisory Lock — SHA-256 64-bit Key

**Created:** 2026-06-09
**Branch:** `pg_advisory_lock_sha256`
**Tracking issue:** [#4145](https://github.com/BrighterCommand/Brighter/issues/4145)

## Summary

Upgrade `PostgreSqlAdvisoryLock` from the 32-bit `pg_try_advisory_lock(int4, hashtext(text))`
key derivation to the single-argument `pg_try_advisory_lock(bigint)` overload, computing the
bigint key as the first 8 bytes of `SHA-256(BRIGHTER_LOCK_NAMESPACE || lockKey)`. This matches
the MySQL convention (`MySqlMigrationLockName`) and reduces the birthday-collision space from
~1-in-2^32 to ~1-in-2^64.

This feature is **not yet released** (V10.X), so no release notes / migration coordination are
required — the lock-key derivation can change freely.

## Status

- [ ] Requirements (`/spec:requirements`)
- [ ] Design / ADR (`/spec:design`)
- [ ] Adversarial Review (`/spec:review`)
- [ ] Task Breakdown (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`)
