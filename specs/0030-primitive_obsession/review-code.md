# Review: code — 0030-primitive_obsession

**Date**: 2026-06-08
**Threshold**: 60
**Verdict**: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Core `Paramore.Brighter` (and ServiceActivator) assemblies modified — direct violation of C-1, ADR scope, and the branch's own approved tasks (Score: 92)

The requirements (C-1), the ADR, and tasks.md all explicitly forbid touching the core assembly, yet this branch modifies 19+ files across out-of-scope assemblies in two commits (`cb3e4ef47`, `6262cc15d`).

- **ADR 0061 line 15**: "It does not touch the core `Paramore.Brighter` assembly…"
- **ADR 0061 line 96**: "Explicitly **unchanged**: … the core `Paramore.Brighter` assembly."
- **requirements.md C-1**: "The core `Paramore.Brighter` assembly is NOT modified to introduce new value types under this issue."
- **tasks.md line 264**: "No task … touches the core `Paramore.Brighter` assembly (C-1)."
- **tasks.md line 203** (Phase 4 scope-guard, marked done): "Confirm the core `Paramore.Brighter` assembly is unmodified" — checked off `[x]` despite being false.

`cb3e4ef47` widened the public implicit-string operators on `Id`, `RoutingKey`, `PartitionKey`, `CloudEventsType`, `SubscriptionName`, `TraceContext`, `ConsumerName`, `HostName`, and `Tenant` from `string` to `string?`. This is a public API signature change to the framework's core value types, completely unrelated to the BoxProvisioning value-type work (the new BoxProvisioning records are standalone and do not depend on these operators). `6262cc15d` then patched 16 core call sites to fix CS8604/CS8601 warnings that `cb3e4ef47` itself introduced — a self-inflicted change loop with no traceability to any FR.

**Evidence**: `git show --stat cb3e4ef47` (9 files, 3 assemblies); `git show --stat 6262cc15d` (16 core files); `tasks.md:203,264`; ADR `docs/adr/0061-box-provisioning-value-types.md:15,96`.

**Recommendation**: Revert both `cb3e4ef47` and `6262cc15d` from this branch. If the core operators genuinely warrant null-safety hardening, that is a separate ADR-level change to the core assembly and must not ride along under spec-0030. The BoxProvisioning task notes (tasks.md:30,57) correctly require `string?` operators only on the *new* BoxProvisioning types — that does not license retyping existing core types.

---

### 2. Spanner provisioners hardcode `null` schema, changing migration-activity telemetry (Score: 72)

`SpannerInboxProvisioner` and `SpannerOutboxProvisioner` previously passed `_configuration.SchemaName` into `MigrateAsync`; the branch hardcodes `null` instead. While the Spanner runner discards `schemaName` for DDL (`_ = schemaName`), it still feeds it to `StartMigrationActivity`, which sets the `db.namespace` OTel tag when non-null. With a non-null configured `SchemaName`, the migration Activity previously carried a `db.namespace` tag and now does not — a telemetry content change.

NFR-3 mandates telemetry "byte-for-byte equivalent," and the `SqlBoxProvisioner` sibling handled the same `string? → SchemaName?` conversion correctly via a ternary cast (`SqlBoxProvisioner.cs:142`). The Spanner side took a behaviour-changing shortcut instead.

**Evidence**: `SpannerInboxProvisioner.cs` / `SpannerOutboxProvisioner.cs` diff: `- _configuration.SchemaName` → `+ null`; `SpannerBoxMigrationRunner.cs:161,209-212` (`if (schemaName is not null) activity.SetTag(DbNamespace, schemaName)`).

**Recommendation**: Mirror the SqlBoxProvisioner pattern: pass `_configuration.SchemaName != null ? (SchemaName)_configuration.SchemaName : null` (or equivalent) so the activity tag content is preserved when a schema is configured.

---

### 3. Operator-widening in core forced semantic rewrites in mappers with no test coverage (Score: 64)

The operator widening (Finding 1) forced non-mechanical rewrites in core that change null handling, none covered by any spec test:

- **`MonitorEventMessageMapper.cs`**: `new RoutingKey(publication.Topic ?? "")` → `publication.Topic ?? RoutingKey.Empty`. A null Topic previously produced `new RoutingKey("")`; it now produces the `RoutingKey.Empty` sentinel. These are not guaranteed equivalent.
- **`CloudEventsTransformer.cs`**: `Type = message.Header.Type` → `Type = message.Header.Type?.Value ?? string.Empty`. Null-`Type` behaviour changed from NPE-on-master to coalesce-to-empty.
- **`JsonMessageMapper.cs`**: `replyTo: publication.ReplyTo ?? RoutingKey.Empty` → `publication.ReplyTo is not null ? new RoutingKey(publication.ReplyTo) : RoutingKey.Empty`.
- **`InternalBus.cs:54`**: `new RoutingKey(message.Header.Topic)` → `message.Header.Topic` (relies on the now-nullable implicit conversion).

These are behavioural edits to core hot paths riding on an out-of-scope refactor, with no accompanying tests demonstrating equivalence.

**Evidence**: Diffs of `MonitorEventMessageMapper.cs`, `CloudEventsTransformer.cs`, `JsonMessageMapper.cs`, `InternalBus.cs` in commit `6262cc15d`.

**Recommendation**: Subsumed by reverting Finding 1. If retained, each requires a characterisation test proving the null path is unchanged.

---

### 4. `MigrationVersion` implicit-to-int operator is not null-safe, asymmetric with string types (Score: 38)

`public static implicit operator int(MigrationVersion v) => v.Value;` dereferences `v` without a null guard, so `int i = (MigrationVersion)null;` throws NRE — whereas `CompareTo` (`other?.Value ?? 0`) and every string type's `operator string?` (`value?.Value`) are null-tolerant. Latent only (no current call site passes a null `MigrationVersion`), and `int` cannot return `int?` to mirror the others, so this is a nit.

**Evidence**: `MigrationVersion.cs:62` vs `:55`; contrast `BoxTableName.cs:65`.

**Recommendation**: Document the non-null contract explicitly in XML docs on the operator, or accept as-is given the `int` return type cannot be null-lifted.

---

### 5. `IsNullOrEmpty` dedicated test file exists only for `BoxTableName`; other four types fold it into round-trip files (Score: 22)

`When_box_table_name_is_null_or_empty_is_called_it_should_report_emptiness.cs` is a standalone AC-2 test file, but `SchemaName`, `MigrationDescription`, `SqlScript`, and `SourceReference` embed their `IsNullOrEmpty` assertions inside their round-trip test files. Coverage exists and AC-2 is satisfied; this is purely an organisational inconsistency.

**Evidence**: `When_box_table_name_is_null_or_empty…cs` (dedicated file); `IsNullOrEmpty` assertions verified present in round-trip files for all other four types.

**Recommendation**: No action required. Optionally split into dedicated files for consistency.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 5
**Findings at or above threshold (60)**: 3

---

*The new BoxProvisioning value types themselves (BoxTableName, SchemaName, MigrationVersion, MigrationDescription, SqlScript, SourceReference) are well-formed: correct `Id.cs` template, standalone records (FR-13, D1), null-safe string operators, full XML docs, netstandard2.0-clean build, the D4 ternary cast present, and AC-1/AC-2/AC-7 test coverage intact. The blocking problem is scope: the branch modified the core `Paramore.Brighter` and `ServiceActivator` assemblies in direct contradiction of C-1, the ADR, and tasks.md — and the Phase 4 scope-creep guard task was checked off despite being false.*
