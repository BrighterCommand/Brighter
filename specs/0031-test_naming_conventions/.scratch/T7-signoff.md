# T7 — Whole-spec sign-off (Spec 0031) — 2026-06-09

Pre-work base commit: 51c2d9f29 (a3b2bc33d^). Spec commits: T2 a3b2bc33d · T3 53520bb84 · T4 d9947c90b · T5 e9358acb3 · T6 24c4f168d.

## Acceptance criteria
- **AC-9 (0 WRONG + 0 MIXED)**: all 5 folders — 0 `class When_`, 0 `Should_` test methods. PASS.
- **AC-2 (no Should_ method remains)**: 0 across scope. PASS.
- **AC-1/AC-4 (classes → [Behavior]Tests; multi-case files structured)**: every test class ends in `Tests`
  (sole non-Tests public class = `BoxProvisioningObservabilityCollection`, an xUnit `[CollectionDefinition]`
  fixture — not a test class, untouched). Every `[Fact]`/`[Theory]` method begins with `When_`. PASS.
- **AC-3 (file renames via git mv, history preserved)**: 0 file renames were required — every in-scope
  test file name already conformed to `When_..._should_...`, so files were modified in place (118 × M,
  0 × R). git history is intact (no moves). PASS (vacuously — no rename needed).
- **AC-5 / FR-5 / NFR-1 (CORRECT files untouched)**: exactly 118 files changed (2+22+33+27+34), matching the
  non-conforming set; all 77 CORRECT test files + helpers show no diff. PASS.
- **AC-10 (scope containment)**: (a) 0 files changed outside the 5 in-scope folders; (b) diff contains
  ONLY class/method/constructor declaration identifiers + comment-reference updates — 0 `Assert.` lines
  changed, no arrange/act/assertion logic touched; (c) 0 analyzer / .github CI / .editorconfig /
  agent-instruction / hook / testing.md changes; (d) 0 production (src/) files changed. PASS.
- **AC-6 (compiles, references resolve)**: all 5 projects `dotnet build` net9.0 = 0 Error(s). PASS.
- **AC-8 (count parity)** & **AC-7 (green/red parity)** vs T1 baseline — all containers available, every suite run:

| Project | Baseline Total | After Total | Baseline P/F | After P/F | Verdict |
|---|---|---|---|---|---|
| BoxProvisioning.Tests | 111 | 111 | 111/0 | 111/0 | parity |
| Sqlite.Tests | 127 | 127 | 127/0 | 127/0 | parity |
| MSSQL.Tests | 198 | 198 | 185/13 | 185/13 | parity (13 pre-existing fails preserved; 1 in-scope under renamed FQN `MsSqlRunnerLockResourceSchemaQualificationTests`, 12 = DateTimeOffset/BST #4161) |
| MySQL.Tests | 160 | 160 | 160/0 | 160/0 | parity |
| PostgresSQL.Tests | 191 | 191 | 191/0 | 191/0 | parity |

No "infra unavailable" gaps — MSSQL/Postgres/MySQL Docker all up; Spanner not in scope (no Spanner BoxProvisioning rename folder).

## Conclusion
All FR-1..6, NFR-1..6, AC-1..10 satisfied. Pure rename refactor complete; behaviour, assertions, and test
counts preserved exactly. Cross-file comment references (4 total: 1 Sqlite, 1 MSSQL, 1 MySQL, 2 Postgres-wait=2)
updated to new class names; file-name comments (referencing unchanged .cs files) left intact.
