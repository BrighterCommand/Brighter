# Review: design — 0030-primitive_obsession

**Date**: 2026-06-07
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Context and D3 understate the number of `AssertSafe` call sites (Score: 45)

The Context says `Identifiers.AssertSafe` "is invoked at the user-facing entry points of the runner and provisioner. These calls form the validation boundary." D3 repeats: "Validation logic stays in the provisioner and runner entry points that call `AssertSafe` today." The Architecture Overview diagram shows `AssertSafe` only on `SqlBoxProvisioner / SqlBoxMigrationRunner`.

In reality `AssertSafe` is a defence-in-depth pattern called at ~30 sites across the BoxProvisioning assemblies: all four backend catalogs (`MsSqlOutboxMigrationCatalog.cs:91,102,122,123`), all four backend detection helpers (`MsSqlBoxDetectionHelper.cs:98,131`), the per-backend runners (`MsSqlBoxMigrationRunner.cs:292,482`; `PostgreSqlBoxMigrationRunner.cs:139,155,321`), and the Spanner runner (`SpannerBoxMigrationRunner.cs:153`). The decision in D3 is unaffected by the count, but a reader following Implementation step 3 ("Verify, do not modify, the `AssertSafe` call sites") will find far more than two.

**Evidence**: ADR line 28 ("the user-facing entry points of the runner and provisioner"); diagram. Actual sites span catalogs, detection helpers, and all backend runners — verified via grep.

**Recommendation**: Reword Context/D3 to acknowledge that `AssertSafe` is a defence-in-depth pattern called at multiple chokepoints (provisioner/runner entry points plus catalogs, detection helpers, and per-backend runners), and that the decision is to leave *all* of them untouched rather than relocate any into constructors.

---

### 2. `MigrationVersion.ToString()` "returning `Value`" is loosely worded for the int-backed type (Score: 30)

Line 84 states every type has "an overridden `ToString()` returning `Value`." For the five string-backed types `Value` is a `string`, so this is literally correct. For `MigrationVersion`, `Value` is an `int`, so `ToString()` must return `Value.ToString()`. The NFR-3/AC-7 log-equivalence guarantee for the monotonicity message (`V{prev} … (expected V{prev + 1})`) depends on `MigrationVersion.ToString()` rendering the bare integer.

**Evidence**: ADR line 84 "an overridden `ToString()` returning `Value`"; `MigrationVersion.Value` is `int` per line 88 and the Key Components table.

**Recommendation**: Add a half-sentence noting `MigrationVersion.ToString()` returns `Value.ToString()` (the integer rendered as a string), preserving the existing interpolation output.

---

### 3. D4 labels the ternary error "CS0172 (ambiguous best-common-type)" — number correct, gloss slightly off (Score: 20)

The error number CS0172 is correct (verified by compilation). The parenthetical "ambiguous best-common-type" is a paraphrase; the actual compiler message is "Type of conditional expression cannot be determined because 'int' and 'MigrationVersion' implicitly convert to one another."

**Evidence**: Local repro of the ternary pattern produced CS0172 as stated.

**Recommendation**: Optional — quote the actual CS0172 message text, or drop the parenthetical.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 3 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0

---

### Verifications that passed

- C# overload resolution claim (D4) — **empirically verified correct**: `version == brokenVersion` (`MigrationVersion`/`int`), `curr != prev + 1`, and `v <= detected`/`v <= maxVersion` all compile without casts; only the ternary produces CS0172.
- `RunFreshPathAsync` at `SqlBoxMigrationRunner.cs:418` with `int latestVersion` — confirmed.
- Ternary at `SqlBoxMigrationRunner.cs:285` — confirmed verbatim.
- All 8 `<=` sites confirmed across four backends at cited lines; no `<`/`>`/`>=` against `.Version` exist.
- `==`/`!=` sites confirmed in all four `BrokenMigrationFactory.cs` files and `ValidateMigrationsMonotonic`; no casts present, consistent with "no cast required."
- `Id.cs` template, `Identifiers.AssertSafe(string, string)`, detection helper return types — all confirmed.
- All referenced file paths and ADRs (0053, 0057, 0058, 0059, 0060) exist.
- D7 TFM claim accurate (`$(BrighterTargetFrameworks)` = `netstandard2.0;net8.0;net9.0;net10.0`).
- All 13 FRs and 6 NFRs map to ADR decisions.
