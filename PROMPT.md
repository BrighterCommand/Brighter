# Resume State — Spec 0031 test_naming_conventions

**Last updated:** 2026-06-09
**Branch:** `test_naming`  ·  **Spec dir:** `specs/0031-test_naming_conventions/`  ·  **Issue:** #4157

## ✅ STATUS: IMPLEMENTATION + VERIFICATION COMPLETE — nothing pushed yet

Spec 0031 was a **pure rename refactor** (no behaviour/assertion/logic/count changes; `/test-first`
did not apply). All 7 tasks done & committed on `test_naming`:

| Commit | Task |
|---|---|
| `a3b2bc33d` | T2 — BoxProvisioning.Tests (2) |
| `53520bb84` | T3 — Sqlite (22) |
| `d9947c90b` | T4 — MSSQL (33) |
| `e9358acb3` | T5 — MySQL (27) |
| `24c4f168d` | T6 — PostgreSQL (34) |
| `299f18d5c` | spec docs + T1 baseline + T7 sign-off |

**118 non-conforming files renamed** to the convention in `.agent_instructions/testing.md`
(class `[Behavior]Tests`, method `When_..._should_...`). 0 WRONG + 0 MIXED remain. All AC-1..10 PASS;
all 5 projects build clean; count + green/red parity vs baseline (MSSQL keeps its 13 pre-existing
failures — 12 = DateTimeOffset/BST #4161, 1 now under a renamed FQN). 0 file renames needed (names
already conformed), 0 `src/`/analyzer/CI/agent-instruction changes, 0 `Assert.` lines touched.
Full evidence: `specs/0031-test_naming_conventions/.scratch/T7-signoff.md`.

## ▶️ FIRST THING NEXT SESSION — ask before reviewing

This change is **style only** (identifier + comment renames, mechanically verified by build + suite
parity). Before doing more work, **ASK the user whether a `/spec:review code` / code review is even
warranted here**, or whether we should skip straight to opening the PR / merging. A full adversarial
code review is likely overkill for a rename-only diff — surface that and let the user decide.

## Remaining options (pending the user's answer)
- `/spec:review code` (if they want it) → then PR / merge, OR
- open PR for branch `test_naming` directly, OR
- merge to `master`.

## Verify (per project)
```bash
dotnet test tests/Paramore.Brighter.<Project>.Tests --framework net9.0 -q
```
