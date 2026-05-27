# Review: tasks — 0029-multi-tenancy-migrations

## Round 2 (2026-05-27) — **Verdict: PASS** (threshold 60)

All three round-1 blocking findings resolved; both Low nits addressed; no new contradictions introduced. Re-verified against `master`.

| # | Finding | Round-1 score | Status |
|---|---------|--------------|--------|
| 1 | MSSQL `DoesHistoryExistAsync` existence-delegation hardcoded `dbo` | 88 | **Resolved** — S1 "existence-delegation gotcha" bullet + T3 read-side bullet (pass `historySchema` to inner `DoesTableExistAsync` `:85-86` and COUNT `:93`) + ADR Errata #2 |
| 2 | `DetectCurrentVersionAsync` mislabeled history-targeting | 70 | **Resolved** — S1 scopes param to `DoesHistoryExistAsync`/`GetMaxVersionAsync` only; `IAmAVersionDetectingMigrationHelper<,>` unchanged; ADR D4 inline marker + Errata #1 |
| 3 | T2 / legend ownership of PG `SupportsPerSchemaHistory` | 62 | **Resolved** — T1 delivers MSSQL override, T2 delivers PG override, T3/T4 consume; legend + dependencies updated |
| 4 | Spanner `BoxProvisioning/TestDoubles` enumeration | <60 | **Resolved** — S1 enumeration corrected (Spanner excluded; verified on disk) |
| 5 | T11 schema-vs-table identifier framing | <60 | **Resolved** — reworded as a genuinely new schema-identifier negative assertion |

**Findings at or above threshold (60): 0.** Ready for `/spec:approve tasks`.

---

## Round 1 (2026-05-27) — Verdict: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

## Findings

### 1. `DoesHistoryExistAsync`'s inner history-table existence check is hardcoded to the default schema and is excluded from the change scope — PerSchema detection will short-circuit `false` (Score: 88)

S1's "Scope guard (reviewer #5)" states `DoesTableExistAsync` is **unchanged** because it is a "box-table" method. But in the real MSSQL implementation, `DoesHistoryExistAsync` determines whether the *history* table exists by calling `DoesTableExistAsync(connection, "__BrighterMigrationHistory", DefaultSchemaName, ...)` (`MsSqlBoxDetectionHelper.cs:85-86`, hardcoded `DefaultSchemaName = "dbo"`). Under `PerSchema`, the per-schema history table lives in the configured schema, not `dbo`, so this inner existence probe returns `false` and `DoesHistoryExistAsync` short-circuits to `return false` (`:87-88`) before ever reaching the COUNT query at `:93` that the task does change.

This means T3 ("detect ... consistent", "A second detection ... reads the per-schema table and agrees") cannot pass as written: the existence gate looks in the wrong schema. The PG impl avoids this because its existence check is *inlined* with a literal `TABLE_SCHEMA = 'public'` (`PostgreSqlBoxDetectionHelper.cs:122`, which T4/T10 *does* cover) — but MSSQL delegates to `DoesTableExistAsync`, which the plan freezes.

**Evidence**: `MsSqlBoxDetectionHelper.cs:85-86` (`DoesTableExistAsync(..., DefaultSchemaName, ...)`); tasks.md S1 "Scope guard" line ("`DoesTableExistAsync` ... are **unchanged**"); tasks.md T3 implementation bullets (only the `:93` read changed).
**Recommendation**: Inside MSSQL `DoesHistoryExistAsync`, pass the new `historySchema` value (not `DefaultSchemaName`) to the inner existence probe AND use it in the COUNT — the method `DoesTableExistAsync` itself stays unchanged, but its *argument here* must be the resolved history schema. Reword S1's scope guard so it does not imply the history-existence path is wholly inside the COUNT, and make T3's implementation bullets call out the existence-probe argument explicitly. (PG already inlines this; no change to that finding.)

---

### 2. `DetectCurrentVersionAsync` is mislabeled a "history-targeting" method; adding `historySchema` to it produces an unused parameter (Score: 70)

S1 enumerates the methods that gain the `historySchema` param as "the three history-targeting methods (`DoesHistoryExistAsync`, `GetMaxVersionAsync`, `DetectCurrentVersionAsync`)" and contrasts them against `GetTableColumnsAsync` as a "box-table" method. But `DetectCurrentVersionAsync` reads the **box table's columns** — it calls `GetTableColumnsAsHashSetAsync` (`MsSqlBoxDetectionHelper.cs:162`) which queries `INFORMATION_SCHEMA.COLUMNS` of the box table using `schemaName ?? DefaultSchemaName` (`:200-204`). It never touches `__BrighterMigrationHistory`. Adding `historySchema` to it would be dead weight. ADR 0060 D4 (line 100) repeats the same mischaracterization, so this traces from the design, not just the task list.

**Evidence**: `MsSqlBoxDetectionHelper.cs:156-180` (`DetectCurrentVersionAsync` → `GetTableColumnsAsHashSetAsync` → `INFORMATION_SCHEMA.COLUMNS`, box table only); tasks.md S1 scope-guard/call-site bullets; ADR 0060 line 100.
**Recommendation**: Drop `DetectCurrentVersionAsync` from the `historySchema` change set (it is a box-table method like `GetTableColumnsAsync`). The correct set is `DoesHistoryExistAsync` and `GetMaxVersionAsync` only — both on `IAmABoxMigrationDetectionHelper`, so `IAmAVersionDetectingMigrationHelper` does not change at all for this feature. Note that ADR 0060 D4 carries the same inaccuracy and should be reconciled (a minor errata to an approved ADR).

---

### 3. T2 (PostgreSQL D3 guard) has a contradictory dependency: legend marks it "independent" but its body depends on T4 (Score: 62)

The dependency legend lists `T1,T2 (D3 guard, AC1a) → independent`. But T2's implementation bullet says: "the base D3 guard from the MSSQL task plus PG's `SupportsPerSchemaHistory => true` (delivered in T4) covers it. If T4 not yet done, this task delivers PG's `SupportsPerSchemaHistory => true` override too." So T2 either depends on T4, or it must itself deliver a production override that overlaps with T4's deliverable — two tasks can each claim ownership of `PostgreSqlBoxMigrationRunner.SupportsPerSchemaHistory => true`. Worse, the D3 guard is gated on `SupportsPerSchemaHistory`; if the override isn't present yet, the guard never fires and the PG D3 test passes for the wrong reason (no exception because `SupportsPerSchemaHistory` is still the inherited `false`) — or the test stays red with no clear owner for the fix.

**Evidence**: tasks.md legend ("independent") vs T2 body ("delivered in T4 ... If T4 not yet done, this task delivers ... too").
**Recommendation**: Make ownership unambiguous. Since the D3 guard cannot be exercised without `SupportsPerSchemaHistory => true`, have **T1 deliver MSSQL's override** and **T2 deliver PG's override** (each guard test drives its own backend's override into existence — clean TDD), and have T3/T4 simply *consume* the existing override. Update the legend so T3 depends on T1 and T4 depends on T2.

---

### 4. S1 references a Spanner `BoxProvisioning/TestDoubles` directory that does not exist (Score: 45)

S1 enumerates test-double call sites including "Spanner `BoxProvisioning` tests". `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning` exists but has no `TestDoubles` subdir, and `SpannerBoxDetectionHelper` implements only the base `IAmABoxMigrationDetectionHelper` (`SpannerBoxDetectionHelper.cs:53-54`), not the version-detecting interface. An implementer will simply find nothing to update there.

**Evidence**: no `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/TestDoubles`; `SpannerBoxDetectionHelper.cs:53-54` implements base interface only.
**Recommendation**: Remove Spanner from the test-doubles enumeration (or note "no test double exists; nothing to change") and note Spanner does not implement `IAmAVersionDetectingMigrationHelper`.

---

### 5. T11 (AC6) frames a novel unsafe-*schema* negative test as "confirm existing flow" (Score: 40)

T11 says "Add validation only where a path was missed" and leans on existing analogs `When_<backend>_migrations_are_built_with_an_unsafe_table_name`. But those analogs validate the *table* name; AC6 here concerns the *schema* identifier on the new per-schema qualified references. The test is genuinely new behaviour; the framing slightly undersells it. Non-blocking.

**Evidence**: analog files exist (`When_mssql_migrations_are_built_with_an_unsafe_table_name_they_should_throw.cs` etc.); T11 implementation bullet.
**Recommendation**: Reword T11 so the schema-identifier negative test is treated as a real new assertion, not just re-confirmation of the table-name path.

---

### 6. Reviewer-feedback table and code anchors all verified accurate (Score: 0 — no action)

Reviewer items #1–#7 each map to the cited task body as claimed. Spot-checked anchors all correct: `BoxProvisioningOptions.cs:74`, `SqlBoxMigrationRunner.cs:54`, `MsSqlBoxDetectionHelper.cs:93` (`[dbo]`), `PostgreSqlBoxDetectionHelper.cs:122/132`, `SqlBoxProvisioner.cs:151/158/166` + discarded-hint `:155-157`, `MsSqlBoxMigrationRunner.cs:140-148/142/281/174-175`, `PgIdentifier.cs:57/77`, `SqlBoxMigrationRunner.cs:165/410`, `ConfigurationException(string)`. No hallucinated anchors. Full AC/FR/NF/D coverage confirmed (every FR1–FR6, NF1–NF5, AC1–AC7, D1–D6 maps to a task; no orphan/scope-creep task).

**Evidence**: cross-reference mapping in review notes.
**Recommendation**: None.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 3 |

**Total findings**: 6
**Findings at or above threshold (60)**: 3

The task list is strong on structure, TDD framing, anchor accuracy, and traceability (every FR/NF/AC/D maps to a task; reviewer items honestly addressed). The blocking issues are two correctness gaps that propagate from ADR 0060 into S1/T3 — the MSSQL history-table **existence** probe stays hardcoded to `dbo` while the plan freezes the path that performs it (Finding 1, High), and `DetectCurrentVersionAsync` is wrongly classed as history-targeting (Finding 2, High) — plus one dependency-ownership contradiction for the PG `SupportsPerSchemaHistory` override (Finding 3, Medium). Fix these (and note the ADR D4 errata) before approving.
