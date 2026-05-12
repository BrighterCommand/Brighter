# Review: tasks — 0028-box-provisioning-rdd-role-interfaces (Sub-phase A round 4)

**Date**: 2026-05-12
**Threshold**: 60
**Scope**: Phase 13 (sub-phase A) — round 4 review after addressing round-3 findings
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> **Note**: This file supersedes the round-1, round-2, and round-3 sub-phase A review records (all NEEDS WORK) and the earlier round-2 PASS record for the parent tasks list (Phase 0..12, dated 2026-05-07). The parent record passed PASS / 0 findings ≥60 on 2026-05-07 and remains valid for Phase 0..12. Sub-phase A passed after four iteration rounds:
> - Round 1: 7 findings, 7 ≥60 (High 4, Medium 3)
> - Round 2: 2 new findings, 2 ≥60 (Medium 2 — score 67 and 62)
> - Round 3: 3 new findings, 3 ≥60 (High 2 — score 78, 72; Medium 1 — score 65)
> - Round 4: 0 findings — PASS

## Round-3 resolution verification

**Finding 1 — stale `[InlineData]` token at the 13.A.1 gate bullet (round-3 score 72)**: **RESOLVED**

Verified by grepping Phase 13 (lines 862–1194) for `InlineData` / `[Theory]` / `[Fact]`:
- 13.A.1 gate bullet at line 977 reads: *"All 8 Phase 13.A.1 `[Fact]` methods pass against their respective fake derivatives — Core BoxProvisioning.Tests count is 44/44 per TFM (= 36 pre-13.A.1 floor + 8 new `[Fact]`s — matches the 13.A.0.5 amended NF9 floor exactly)."* — `[Fact]`, not `[InlineData]`.
- Only two `[Theory]` / `[InlineData]` occurrences remain in Phase 13, both in legitimate contrastive context:
  - Line 912: *"matches the Phase 6 precedent... `[Fact]` methods rather than `[Theory] + [InlineData]`"* — contrasts the chosen shape with the rejected alternative.
  - Line 1098: *"Not a `[Theory]` because the two derivations are distinct types — parameterising a generic type on `[InlineData]` is awkward..."* — justifies why 13.B's MySQL test uses two `[Fact]`s instead of a `[Theory]`.
- All `[Fact]` references throughout Phase 13.A.1 slices 1–3 consistently use `[Fact]`. No drift.

**Finding 2 — NF2 mis-targeted as the running floor (round-3 score 78)**: **RESOLVED**

Verified by reading requirements.md:56-65 and grepping the lock-step instruction:
- NF2 (requirements.md:56-65) carries the Phase-0 baseline anchor: MSSQL 54/54, PG 46/46, MySQL 50/50, SQLite 40/40, Spanner 26/26, Core 23/23, Core sub-filter 5/5 — these are pre-spec-0028 counts captured at HEAD `edfa9fc99`. NF2 contains no 36/36, no 44/44, no 61/61.
- Two explicit *"Do NOT touch NF2"* guards in Phase 13 (lines 890, 1115). Plus the "NF2 stays put as the Phase-0 baseline anchor" reminder in the 13.B lock-step note (line 1111).
- Lock-step rule reads correctly: *"three artefacts move together: NF9-parenthetical / baseline.md / AC6"* at line 1111 — three artefacts, not "NF2 / baseline.md / AC6". The 13.A.0.5 artefact-targeting note (line 885) likewise enumerates *"(1) NF9's parenthetical in `requirements.md:211` ... (2) baseline.md ... (3) AC6 in acceptance.md"* with the explicit "NF2 ... must NOT move" reminder.
- NF9 parenthetical updates are explicit: 13.A.0.5 spells out the full before/after text at line 888 (36/36 → 44/44); 13.B spells out the full before/after text at line 1115 (44/44 → 43/43, 61/61 → 63/63).
- The verbatim text instruction at line 888 matches requirements.md:211 character-for-character.
- Validation greps (lines 900, 1117) explicitly check that 36/36, 44/44, +21/+21, 61/61 net9.0-only, and +11 are absent post-amendment and that NF2's 23/23 is preserved.

**Finding 3 — AC6 Δ column not updated (round-3 score 65)**: **RESOLVED**

Δ-column arithmetic verified end-to-end:
- 13.A.0.5 (line 894): *"`44/44 | 44/44 | 23/23 | +21/+21`"* — 44 − 23 = +21 per TFM ✓
- 13.B Core (line 1116): *"`44/44 +21/+21` → `43/43 +20/+20`; Δ recomputed: 43 − 23 = +20"* ✓
- 13.B MySQL (line 1116): *"`61/61 net9.0-only +11` → `63/63 net9.0-only +13`; Δ recomputed: 63 − 50 = +13"* ✓
- Pre-13.B MySQL Δ +11 = 61 − 50 ✓
- Validation grep clauses at line 1117 include `+21/+21` and `+11` as stale tokens that must vanish post-edit.

## Findings (new in round 4)

No new findings introduced by the round-3 fixes. Additional spot-checks all passed:

- **NF9 parenthetical edit instruction matches requirements.md:211 verbatim** — confirmed character-for-character. Both round-3 edits target identical text strings.
- **No remaining mis-references to NF2 or stale floor values outside 13.A.0.5 / 13.B** — Phase 13.A.7 gate (line 1086) correctly references the *"post-13.A.0.5 amended NF9 floor"* (no NF2 confusion). The 13.A.7 gate table (lines 1078–1084) lists 44/44 / 5/5 / 63/63 / 54/54 / 61/61 net9.0-only / 45/45 / 26/26 — matches the post-13.A.0.5 floor exactly. Backend-port validation lines (1014, 1020, 1033, 1040, 1054, 1060) reference "AC6 floor" without NF2 confusion.
- **AC12 NF9 sub-bullet (line 1143)** correctly walks the trajectory: Core `36/36 → 44/44` post-13.A.1, `44/44 → 43/43` post-13.B; MySQL `61/61 → 63/63` post-13.B. Numbers match the running ledger.
- **Phase 13 final gate count table (line 1168)** matches the post-13.B floor: Core BoxProvisioning.Tests 43/43, sub-filter 5/5, MSSQL 63/63, PG 54/54, MySQL 63/63 net9.0-only, SQLite 45/45, Spanner 26/26. Consistent with 13.B's instruction that Core moves to 43/43 and MySQL moves to 63/63 in the F11 commit.

Two minor stylistic notes (well below threshold, not findings):
- The 13.B MySQL row update instruction (line 1116) uses the phrasing *"61/61 net9.0-only +11"* rather than the literal four-column form *"61/61 | n/a | 50/50 | +11"* that AC6 carries (acceptance.md:51). Functionally unambiguous (the "net9.0-only" encodes the `n/a` column), but a literal-row form would tighten fidelity if a future round wants belt-and-braces.
- The AC6 footnote authored in 13.A.0.5 (line 894) forward-references the post-13.B 43/43 + 63/63 numbers — a docs commit recording a planned trajectory. Stylistic, not a defect.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 0 |

**Total findings**: 0
**Findings at or above threshold (60)**: 0

Phase 13 sub-phase A passes round-4 review. All three round-3 findings (the `[InlineData]` token, the NF2 mis-target, and the AC6 Δ column) are sound and fully resolved. The before/after texts the tasks file instructs to edit match the actual requirements.md:211 / baseline.md:71 / acceptance.md:47 strings verbatim. The Δ arithmetic on every floor transition (23→44, 44→43, 50→61, 61→63) is correct. NF2 is properly preserved as the Phase-0 historical anchor, never mutated.
