# Review: requirements — 0030-primitive_obsession

**Date**: 2026-06-02
**Threshold**: 60
**Verdict**: NEEDS WORK

5 findings at or above threshold 60. Address these before approving.

## Findings

### 1. FR-8/FR-9/NFR-1/AC-4 mischaracterize the `LogicalColumns` change — real catalogs build `HashSet<string>`, not collection initializers (Score: 92)

The requirements repeatedly frame the only non-trivial call-site change as element-level implicit conversion failing inside a `new[] { "a", "b" }` collection initializer (FR-8 example, the "Additional Context" note, AC-4's "only `LogicalColumns` may require a localized mechanical change"). But no relational catalog actually constructs `LogicalColumns` from a string-literal array. Every catalog passes `LogicalColumns: Cumulative(n)`, where `Cumulative` returns `IReadOnlyCollection<string>` built from a `HashSet<string>(StringComparer.OrdinalIgnoreCase)` that `UnionWith`'s `static readonly string[] s_vNColumns` arrays.

Retyping the property to `IReadOnlyCollection<LogicalColumnName>` forces retyping `Cumulative`'s return, the `HashSet<string>` it builds, and/or the `s_vNColumns` constant arrays — across all four backends × inbox/outbox (8 catalogs). That is a structural change, not "a localized mechanical edit limited to the `LogicalColumns` argument." The requirement's stated scope of the change is factually wrong.

**Evidence**: `MsSqlOutboxMigrationCatalog.cs` — `var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);` returned as `IReadOnlyCollection<string>`; `s_v2AddedColumns = ["Dispatched"]` etc. are `string[]`. FR-8's example assumes `new LogicalColumnName[] { "MessageId", "Source" }`, which does not appear in the codebase.

**Recommendation**: Re-survey the catalogs and rewrite FR-8/FR-9/AC-4. Either (a) keep `LogicalColumns` as `IReadOnlyCollection<string>` (it is a logical-column set consumed by case-insensitive `HashSet` superset matching, arguably not the same "identifier" abstraction as a box table name), or (b) explicitly enumerate the `Cumulative`/`HashSet`/`s_vNColumns` retyping work as in-scope, and state how the backend `StringComparer` semantics are preserved.

---

### 2. FR-13/NFR-3 ignore that `LogicalColumns` is consumed via `HashSet<string>.IsSupersetOf(...)` — a compile break and a behaviour risk (Score: 90)

Version detection runs `actualColumns.IsSupersetOf(migrations[i].LogicalColumns)` where `actualColumns` is `HashSet<string>(StringComparer.OrdinalIgnoreCase)`. `HashSet<string>.IsSupersetOf` takes `IEnumerable<string>`. If `LogicalColumns` becomes `IReadOnlyCollection<LogicalColumnName>`, that argument is `IEnumerable<LogicalColumnName>`, which does NOT implicitly convert to `IEnumerable<string>` (C# has no variance through user-defined element conversions). This breaks compilation in every detection helper, contradicting FR-13's blanket claim that backends "continue to compile … relying on implicit conversions."

More subtly, any fix that projects to strings (`.Select(c => c.Value)`) must preserve the exact `StringComparer` used by the membership test, or version detection silently changes — directly threatening NFR-3 ("byte-for-byte equivalent … migration path selection").

**Evidence**: Detection helpers call `actualColumns.IsSupersetOf(migrations[i].LogicalColumns)` using `HashSet<string>(StringComparer.OrdinalIgnoreCase)`. FR-13 lists only `LockResourceFor`, detection-helper `tableName`, and DDL interpolation as the affected helpers — it omits the superset consumption of `LogicalColumns` entirely.

**Recommendation**: Add an explicit FR covering how `LogicalColumns` element values reach the `HashSet<string>` superset test without changing the comparer semantics, and remove the false "everything converts implicitly" assurance in FR-13. Cite ADR 0057 §1 Ordinal-vs-OrdinalIgnoreCase as a behaviour invariant.

---

### 3. FR-4/AC-7 quote the monotonicity error message incorrectly as the full "message form" (Score: 70)

FR-4 and AC-7 state the failing list "throws `ConfigurationException` with the existing message form `V1 followed by V3 (expected V2)`" / "identical to current behaviour." The actual message is a longer sentence: `Migration list for '{qualified}' is not contiguous and ascending: V1 followed by V3 (expected V2).` An implementer writing `ex.Message == "V1 followed by V3 (expected V2)"` — which "message form" invites — would write a failing test. The correct assertion is `Contains`, but the requirement doesn't say that.

**Evidence**: `SqlBoxMigrationRunner.cs` produces: `$"Migration list for '{qualified}' is not contiguous and ascending: " + $"V{prev} followed by V{curr} (expected V{prev + 1})."` — the quoted string is only a trailing substring.

**Recommendation**: State the assertion mode explicitly — "message contains the substring `V1 followed by V3 (expected V2)`" — consistent with FR-12/AC-6's wording ("message contains …").

---

### 4. `MigrationVersion` arithmetic impact on the runner is unspecified — `var` inference and `prev + 1` are ambiguous (Score: 66)

FR-4 requires implicit `MigrationVersion → int` and successor comparison, with examples only for assignment and the monotonicity outcome. It does not address that the runner uses `var` at load-bearing sites: `var latestVersion = migrations[...].Version` (passed to `ExecuteFreshInstallAsync(string, int latestVersion, …)`) and `var prev = migrations[i-1].Version; … prev + 1`. After retyping, `latestVersion`/`prev`/`curr` infer `MigrationVersion`, and `prev + 1`, `curr != prev + 1`, and `V{prev + 1}` interpolation now depend specifically on the implicit-to-`int` operator (or a defined arithmetic operator). The requirement leaves "via `IComparable<MigrationVersion>` or by comparing `Value`" as an either/or — the two choices behave differently for `prev + 1` arithmetic.

**Evidence**: `SqlBoxMigrationRunner.cs` — `var latestVersion = … .Version;` passed to an `int latestVersion` parameter; `var prev/curr` used in `prev + 1` arithmetic and string interpolation.

**Recommendation**: Add an example/assertion that `var prev = migration.Version; prev + 1` evaluates to `int` 2 for V1, and that `latestVersion` round-trips into an `int` parameter. State explicitly whether arithmetic operators (not just `IComparable`) are required.

---

### 5. Public constructor omitted from required member list; FR-1's `==` example may not compile as written (Score: 62)

FR-14 lists required members — `Value`, implicit operators, `ToString()`, equality, `IsNullOrEmpty` — but omits "public constructor." Yet FR-1's example `new BoxTableName("Outbox") == (BoxTableName)"Outbox"` and other examples throughout rely on `new BoxTableName(...)`. If an implementer uses only implicit operators and a factory method (no public ctor), the `new …(...)` examples won't compile. `Id.cs` exposes a public constructor; that should be explicit in the spec.

**Evidence**: FR-1 example: `new BoxTableName("Outbox") == (BoxTableName)"Outbox"`. FR-14 member list does not include "public constructor accepting the underlying primitive." `Id.cs` has `public Id(string value)`.

**Recommendation**: Add "a public constructor accepting the underlying primitive" to FR-14's required-member list. Also clarify that same-type-only equality applies (types are not to share a common base record that would conflate equality across distinct types).

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 2 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 3 |

**Total findings**: 8
**Findings at or above threshold (60)**: 5

### Low findings (informational)

- (Score: 45) FR-3's "named consistently with `Id.IsNullOrEmpty`" is slightly vague on signature; FR-14 pins `[NotNullWhen(false)]`, so this is minor.
- (Score: 40) AC-9 asserts span display name `box.migration {table}` renders unchanged but the requirements never cite where `{table}` is fed from in the retyped surface; covered by NFR-3 in spirit.
- (Score: 35) FR-6 makes `UpScript` a non-null `SqlScript` while `IdempotencyCheckSql` is `SqlScript?`; the nullability contract for the shared type is undocumented (does the ctor reject null? would conflict with `SqlScript?`). ADR-level.
