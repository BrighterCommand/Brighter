# Review: tasks — 0030-primitive_obsession

**Date**: 2026-06-08
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Test bullet `string s = t;` yields CS8600 (not CS8604) — undocumented but harmless (Score: 32)

The Round 3 fix added null-safe notes describing CS8604 at the production `AssertSafe` call sites. However, the `BoxTableName` test bullet (`BoxTableName t = "Outbox"; string s = t;`) is a different nullable scenario: assigning the `string?` operator result to a non-nullable local `string s` produces **CS8600** ("Converting null literal or possible null value to non-nullable type"), not CS8604. This is not a contradiction or a blocker: `tests/Directory.Build.props` does **not** set `TreatWarningsAsErrors` (only `src/Directory.Build.props` does), so in test code CS8600 is a warning only — `string s = t;` compiles and the runtime assertion `s == "Outbox"` holds. The same applies to the FR-1 and FR-5 requirement examples.

**Evidence**: `tasks.md:24` (`string s = t;`); `Id.cs:95` (`implicit operator string?(Id id) => id?.Value`); `src/Directory.Build.props` sets `TreatWarningsAsErrors=true`; `tests/Directory.Build.props` does not.

**Recommendation**: Optional — add a one-line note to the `BoxTableName`/`MigrationDescription` test bullets that `string s = t;` in test code emits a benign CS8600 (warning-only; test project does not treat warnings as errors). Not required for approval.

---

### 2. Type-isolation `BoxTableName == SchemaName` non-compile expectation is comment-only (Score: 30)

Carried from Round 3 (Low). Line 26 states `new BoxTableName("dbo") == new SchemaName("dbo")` "does **not** compile — note as an expectation in the test, not an executable assertion." A non-compiling line cannot live in a passing test, so this is verified only by reviewer inspection of a comment. This is an inherent property of compile-time type isolation and the task handles it correctly by framing it as a commented expectation. Still acceptable.

**Evidence**: `tasks.md:26`, mirroring requirements FR-13 example.

**Recommendation**: Optional — a commented-out line in the test makes the expectation inspectable. Not required.

---

### 3. NFR-4 (allocation overhead) absent from the coverage cross-reference (Score: 22)

Carried from Round 3 (Low). The cross-reference tables map FR-1..FR-13, D1-D7, and AC-1..AC-10, but NFR-4 (no per-row-loop allocations) has no row and no dedicated verification task. NFR-4 is satisfied by design (value types live only on the startup provisioning path per ADR 0061) rather than by a test, so its absence from an explicit task is defensible.

**Evidence**: `requirements.md` NFR-4; no NFR-4 entry in `tasks.md` coverage tables; ADR 0061 covers the rationale.

**Recommendation**: Optional — add an NFR coverage note that NFR-4 is design-satisfied (startup-only path) and not separately tested.

---

### 4. `StubBoxProvisioner` constructor parameter retype not called out (Score: 18)

Carried from Round 3 (Low). The Phase 2 test-doubles task names only the "`BoxTableName` property" for `StubBoxProvisioner.cs`. The file also has a constructor parameter `string boxTableName` — after the property retyping, the ctor param still works via implicit `string → BoxTableName` conversion and needs no change, but this is not stated.

**Evidence**: `StubBoxProvisioner.cs` ctor `string boxTableName` param; `tasks.md:157`.

**Recommendation**: Optional — parenthetical noting ctor param converts implicitly. Not required.

---

## Round 3 fix verification (all confirmed)

1. **All five string-backed types specify `→ string?` with `?.Value`**: `BoxTableName` (line 30), `SchemaName` (line 57), `MigrationDescription` (line 70), `SqlScript` (line 84), `SourceReference` (line 97). All match `Id.cs:95` exactly. ✓
2. **`BoxTableName` null-safe note** — technically accurate: correctly explains `?.Value` guard, `string?` result into non-nullable `AssertSafe` param, CS8604 resolution via `.Value`/`!`. ✓
3. **`MigrationDescription` null-safe note** — present and accurate. ✓
4. **Phase 2 `AssertSafe` bullets** — runner (`:191`/`:194`) and provisioner (`:105`) both correctly describe `→ string?` and give actionable CS8604 guidance. Line numbers verified. ✓
5. **No internal contradiction** — `string s = t;` is valid in test code (CS8600, warning-only). ✓
6. **D-NEW coverage** — `→ string?` operator convention derives from `Id.cs` template (FR-13/D1); D1-D7 table is complete against ADR 0061. No missing ADR decision. ✓

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 4 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0
