# Review: requirements — show-me (pass 7)

**Date**: 2026-09-20
**Threshold**: 60
**Verdict**: NEEDS WORK

10 findings at or above threshold 60. Address these before approving.

**Context**: this is the first review round of the post-amendment document (F3/F4 and review-history
cut, FR-8 narrativized, visual-explanation FR-6 added). Prior rounds 1–6 reviewed the original,
broader-scope baseline and are not superseded by this file — see `review-requirements.md` and
`review-requirements-pass{2,3,6}.md` for that history.

## Findings

### 1. FR-17's `.gitignore` claim is factually false — `PROMPT-*.md` **is** a gitignore glob (Score: 90)

FR-17 builds its entire "tracking, not gitignore status" rationale on a specific, checkable claim about this repository's `.gitignore`. That claim is wrong. `.gitignore` carries **two** entries, an exact match *and* the glob:

```
352: # AI agent session state
353: PROMPT.md
354: PROMPT-*.md
```

So `PROMPT-calls.md`, `PROMPT-history.md` et al. are *gitignored*, not "merely untracked". The rule's conclusion (test by `git ls-files --error-unmatch`) survives, but its stated justification is false and the parenthetical is an outright misstatement of a file a developer will open.

**Evidence**: FR-17: "This matters because only literal `PROMPT.md` is gitignored (`.gitignore` carries an exact-match entry, **not a `PROMPT*.md` glob**); the companion files (`PROMPT-calls.md`, `PROMPT-history.md`, …) are merely **untracked**." Verified against `.gitignore` lines 353–354.

Note also a second, related inaccuracy in the same area: `git ls-files` shows a **tracked** `specs/0003-testing-support-for-command-processor-handlers/PROMPT.md`, so "a `PROMPT.md` exists ⇒ it is untracked" is not universally true either — a spec-directory `PROMPT.md` can be a legitimate, citable, tracked path. FR-17's blanket "must not link to it, quote it as a source, or name it as evidence" would forbid citing a tracked file, contradicting its own stated test.

**Recommendation**: Replace the parenthetical with the true state — `.gitignore` carries both `PROMPT.md` and `PROMPT-*.md`, so both the root file and its companions are ignored — and keep the tracking test as the normative rule, adding the caveat that an *already-tracked* `PROMPT.md` inside a spec directory is not excluded by the tracking test.

---

### 2. C-8's central factual claim — that the spec-0036 fixtures are "not on the branch this command is being built on" — is false (Score: 85)

C-8 is the load-bearing constraint for eleven acceptance criteria. Its factual premise does not hold. `spec/scoped-lifetime-per-pipeline` is an **ancestor of HEAD** (`git merge-base --is-ancestor spec/scoped-lifetime-per-pipeline HEAD` → true), and every fixture C-8 names is tracked on the current branch `spec/show-me`:

- `specs/0036-scoped-lifetime-per-pipeline/` with `.adr-list`, `requirements.md` and the 229 KB `tasks.md` — present in `git ls-tree HEAD`
- all seven ADRs `0070-per-pipeline-di-scope-…` through `0076-scope-affinity-option-and-write-through` — present in `git ls-tree HEAD docs/adr/`
- the `release_notes.md` section "Scoped lifetime per pipeline (spec 0036, #4256)" — present in `git show HEAD:release_notes.md`

(The *master* half of the claim is correct: none of these are on `master`.)

**Evidence**: C-8: "…exist **only on the branch `spec/scoped-lifetime-per-pipeline`**. They are not on `master` and **not on the branch this command is being built on**."

**Recommendation**: Restate C-8 accurately: the fixtures are absent from `master` but present on `spec/show-me` because the calibration branch is already merged into it. Then say plainly what the precondition actually is for a future reader (a branch that does not contain `spec/scoped-lifetime-per-pipeline`), rather than asserting a state that is already false on the branch under test.

---

### 3. AC-47 is not exercisable with the fixture C-8 explicitly claims makes it exercisable (Score: 80)

AC-47 requires the local and remote-tracking spec branches to be at **different** shas, and asserts the "Local branch … differs from the measured ref" line. C-8 names this very pair as the reason AC-47 is runnable. They are at the **same** sha:

```
$ git rev-parse spec/scoped-lifetime-per-pipeline origin/spec/scoped-lifetime-per-pipeline
91d549be6f83020943ee06263f3ddb41bac017a9
91d549be6f83020943ee06263f3ddb41bac017a9
```

So the one criterion covering FR-10's divergence line has no working fixture, and the constraint asserting otherwise is wrong. FR-10's third "states what was measured" bullet is therefore untested.

**Evidence**: AC-47: "**Given** both a local branch `spec/scoped-lifetime-per-pipeline` and a remote-tracking `origin/spec/scoped-lifetime-per-pipeline` exist at **different** shas…". C-8: "spec 0036 does (both a local … and a remote-tracking … exist, **which is also what makes AC-47 exercisable**)."

**Recommendation**: Either drop the C-8 justification for AC-47 and specify a constructed fixture (e.g. reset a local branch one commit back, or create a throwaway `spec/<name>` pair), or state AC-47's setup step explicitly. Do not claim an existing repository state satisfies it when it does not.

---

### 4. C-8's AC list is incomplete, and AC-8's `(C-8)` marker contradicts C-8's own text (Score: 75)

C-8 declares that "the inline *(C-8)* marker on each such AC is the **single source of truth** for which criteria this covers". The actual markers are on **eleven** criteria: AC-1, AC-2, AC-4, **AC-8**, AC-12, AC-17, AC-18, AC-20, AC-47, AC-51, AC-56. C-8's prose list names **ten** — it omits AC-8.

Worse, the omission is not a typo but a contradiction. AC-8's fixture is `specs/0005-defer-message-on-error/`, which C-8 itself lists among the fixtures used by "criteria that must be runnable **anywhere**" on `master` (verified: it is on `master`, containing only `requirements.md`). By C-8's own single-source-of-truth rule, the marker on AC-8 imposes the calibration-branch precondition on a criterion C-8 says is branch-independent.

**Evidence**: `**AC-8** *(FR-3, C-8)* **Given** `specs/0005-defer-message-on-error/` — a spec directory present on `master`…`, versus C-8: "Every acceptance criterion marked *(C-8)* … (AC-1, AC-2, AC-4, AC-12, AC-17, AC-18, AC-20, AC-47, AC-51, AC-56)" and "Criteria that must be runnable anywhere use fixtures available on `master`: … `specs/0005-defer-message-on-error/` … for the missing-`tasks.md` case."

**Recommendation**: Remove the `(C-8)` marker from AC-8 (it is a `master` fixture), and re-derive C-8's prose list from the markers so the two agree.

---

### 5. FR-6's diagram trigger has an uncovered state: a test fires, the budget is intact, and there is nothing truthful to draw (Score: 70)

FR-6 (a) states: "When a test fires, the section must carry a diagram unless (e) applies." FR-6 (e) offers exactly three escapes: no test fired, budget exhausted, no diff measured. FR-6 (b) closes the door explicitly: "a fired test is never quietly skipped, only answered by (e)'s budget or no-diff line."

That leaves a real, reachable state with no conforming output:

- **D2 fires mechanically on unrelated work.** Ten changed public declaration lines can be twelve added properties on twelve unrelated DTOs. There is no call flow, no lifecycle, no hierarchy. FR-6 (c) forbids decoration ("It never decorates") and NFR-7 forbids drawing unevidenced structure. The command must draw, but has nothing to draw. Two developers will resolve this differently: one emits a padded box diagram, one emits a fourth, unspecified `No diagram:` line.
- **The single worthwhile diagram belongs in `## Where to look first`.** FR-6 (d) caps `## What changed and why` at one diagram and `## Where to look first` at one. If a test fires and the only relationship worth drawing is the file tree, `## What changed and why` ends up with no diagram and no applicable (e) line — so the command must manufacture a second diagram to conform.

There is no AC for either state. AC-56 covers "fires and draws"; AC-57 covers "none fires"; AC-58 covers budget exhaustion; AC-61 covers no-diff.

**Evidence**: FR-6 (a) "When a test fires, the section must carry a diagram unless (e) applies"; FR-6 (b) "a fired test is never quietly skipped, only answered by (e)'s budget or no-diff line"; FR-6 (e) three enumerated lines.

**Recommendation**: Add a fourth defined fallback line for "a test fired but the change has no structural relationship to draw" (mirroring (b)'s raise as a symmetric, reasoned *lower*), with its own AC — or explicitly state that a fired test may be answered by a one-sentence stated reason, and make the FR-6 (b) asymmetry claim consistent with that.

---

### 6. FR-8 Part 1's range-collapsing rule is ambiguous at two boundaries, undermining the partition invariant it is meant to make checkable (Score: 65)

FR-8 claims the partition invariant "is the property a test asserts". A test must expand `{first}–{last}` ranges to check it, and the rule does not define expansion in two reachable cases:

1. **Gaps of undeclared numbers.** "Consecutive" is not defined as *consecutive within the declared set* or *numerically consecutive*. Take declared ids `{FR-1, FR-2, FR-3, FR-5}` (FR-4 never declared) all `Shipped`. Is the list `FR-1–FR-3, FR-5` or `FR-1–FR-5`? Under the second, naive expansion yields `FR-4`, which is not a declared id — the partition test then reports a spurious violation. This is not hypothetical: FR-16's own row numbering skips 3 and 4, so the document itself demonstrates that identifier sequences here have retired gaps.
2. **Runs of exactly two.** The rule is written as a positive obligation ("any run of **three or more** … written as `{first}–{last}`") and never forbids collapsing a run of two. `FR-1, FR-2` and `FR-1–FR-2` both conform as written. AC-54 repeats the same positive form and adds no prohibition. A test asserting "no range spans fewer than three ids" would fail a conforming implementation.

**Evidence**: FR-8 Part 1: "…comma-separated, with any run of **three or more** consecutive ids sharing a prefix written as `{first}–{last}` (e.g. `FR-1–FR-6, FR-9, FR-20, NFR-1–NFR-8`)." No example shows a run of exactly two, and no example shows a declared-set gap.

**Recommendation**: Define "consecutive" as *numerically consecutive **and** both declared, with every intervening number also declared and Shipped*, state that runs of exactly two **must not** be collapsed, and add an example covering both (e.g. declared `{FR-1,FR-2,FR-3,FR-5,FR-6}` all shipped → `FR-1–FR-3, FR-5, FR-6`).

---

### 7. NFR-2's 2,000-word ceiling justification omits four whole sections from its own maximal enumeration (Score: 65)

NFR-2 defends the raised ceiling with an itemised estimate. That estimate enumerates only FR-6's narrative, FR-7's bullets, FR-14's bullets, FR-8's line and entries, and FR-12's rationale. It silently omits everything else that NFR-2's own counting rule says **counts**:

| Omitted from the estimate | Counted by NFR-2? | Approx words |
|---|---|---|
| `## Blast radius` prose + six bucket lines + three "measured from / ref used" lines (FR-10) | yes, unless rendered as a pipe table (see finding 16) | 100–130 |
| `## How it was built` two lines (FR-9) | yes | ~30 |
| FR-13's verbatim advisory sentence | yes | 22 |
| FR-8 Part 3 line + Part 4 count line | yes | ~50 |
| Eight H2 headings (NFR-2 says "including H2 headings") | yes | ~24 |

Recount for the calibration case with 14 breaking-change bullets at a realistic 45–60 words each (a statement sentence, a classification, and a migration sentence): 600 narrative + 630–840 bullets + ~200 where-to-look + ~180 FR-8 + ~120 FR-12 + ~250 omitted above = **~1,980 to ~2,190**. The upper half of that range **breaks the 2,000 ceiling** for an output that satisfies every requirement — which is precisely the defect NFR-2 says it exists to prevent ("A budget a conforming output cannot meet is not a budget").

**Evidence**: NFR-2: "spec 0036 would produce up to 600 narrative words (FR-6), 14 breaking-change bullets, up to 7 'where to look' bullets with reasons, FR-8's shipped-as-planned line and its deviation entries, and FR-12's rationale — a plausible 1,600–1,750 counted words with every requirement satisfied."

**Recommendation**: Redo the arithmetic against the full counted section set, and either raise the ceiling to accommodate it or add an explicit per-bullet word cap to FR-7 (as FR-14 already has "≤ 25 words") so the ceiling is provably reachable.

---

### 8. D1's "distinct immediate subdirectories of `src/`" is undefined for files that sit directly under `src/` (Score: 62)

D1's second clause and FR-10's mandatory reported count both depend on a file's "immediate subdirectory of `src/`". This repository has a real file with no such subdirectory: `src/Directory.Build.props` (verified via `git ls-tree master src/` — the only non-tree entry).

A diff that touches `src/Directory.Build.props` plus four files in one project directory changes 5 files under `src/`. Does it span 1 subdirectory (D1 does not fire) or 2 (D1 fires, and a diagram becomes mandatory)? The document gives no rule. This directly changes whether a diagram is required, and NFR-1 asserts the D1 measurement and outcome are **deterministic and identical between runs** — a claim the document cannot honour while the base measurement is undefined.

**Evidence**: FR-6 (a) D1: "changes **≥ 5** files under `src/` **and** those files span **≥ 2** distinct immediate subdirectories of `src/`". FR-10: "It additionally reports the number of distinct immediate subdirectories of `src/` the diff touches." NFR-1: "FR-6's trigger measurements and their outcome … are identical."

**Recommendation**: State the rule for path depth explicitly — e.g. "a changed path `src/X/…` contributes subdirectory `X`; a changed path `src/X` with no further segment contributes nothing to the subdirectory count while still counting toward the file count" — and note `src/Directory.Build.props` as the worked instance.

---

### 9. The public-API-declaration-line definition is not implementable as written, and double-counts modifications (Score: 62)

Blast radius (d) — which feeds F1's sibling metric, D2's trigger, and FR-10's mandatory report — is defined by a regex that is valid in neither grep dialect:

`^[+-]\s*(public\|protected)\b`

In BRE, `(` and `)` are literals and `\|` is alternation, so this matches the literal text `(public` or `protected)`. In ERE, `(…)` groups but `\|` is a literal pipe, so it matches the literal `public|protected`. Neither reading is what is intended. `\s` and `\b` are also GNU extensions, unavailable in BSD `grep` — and the environment here is macOS.

Separately, the counting semantics are wrong at the boundary that matters. "added or removed lines … matching" means a single *modified* declaration (e.g. adding a parameter to `public void Create(Type t)`) appears in the diff as one `-` line and one `+` line and contributes **2**. D2's threshold of "≥ 10 changed public API declaration lines" is therefore really "≥ 5 modified declarations, or 10 purely-added ones" — two very different thresholds depending on change style, with no statement of which is meant.

**Evidence**: Definitions, *Blast radius*: "(d) changed **public API declaration lines** — added or removed lines in files under `src/` matching `^[+-]\s*(public\|protected)\b`." FR-6 (a) D2: "changes **≥ 10** public API declaration lines".

**Recommendation**: Give one unambiguous, dialect-named pattern (e.g. `grep -E '^[+-][[:space:]]*(public|protected)[[:space:]]'`), and state explicitly whether a `-`/`+` pair for one modified declaration counts as 1 or 2.

---

### 10. FR-7 leaves "`release_notes.md` section exists but the command chose not to read it" undefined (Score: 60)

FR-7 makes reading `release_notes.md` optional ("the command **may** read it"), but every downstream rule is written as if presence and reading were the same thing:

- **FR-16 row 5** fires on "No `release_notes.md` section for the spec" and prescribes the line `No release_notes.md section found for this spec; …`. If a section exists and the command simply did not read it, emitting that line writes a **false statement** into a tracked file, and NFR-7 forbids unattributable claims.
- **FR-7's tie-break** says "**When a `release_notes.md` section for the spec is read** … its grouping is the tie-break … **When no `release_notes.md` section is read (FR-16 row 5)**" — conflating "not read" with row 5, which is defined as "not present".
- **FR-15** must mark the `release_notes.md` row `used` or `not available: {one-line reason}`. "Present but not read" is neither.

No AC covers it: AC-14 is "Given a spec with **no section**".

**Evidence**: FR-7: "the command **may** read it to corroborate the list"; FR-16 row 5 header "No `release_notes.md` section for the spec"; FR-15: "each marked `used` or `not available: {one-line reason}`".

**Recommendation**: Either make the read mandatory when a section exists (simplest, and it removes a determinism hazard on F2), or define a third `Inputs used` state and a distinct line for "present, not read", with an AC.

---

### 11. FR-11's factor table does not acknowledge FR-16's overrides, so FR-11 read alone yields the wrong level (Score: 58)

FR-16 rows 8, 9 and 12 set factor levels by fiat: row 8 "F5 = Medium", row 9 "F5 = Medium", row 12 "F1 = Medium". FR-11's table says nothing about them. Read alone:

- Rows 8/9: there are no deviation entries at all, so F5's **Low** column ("no deviation entries") matches — contradicting FR-16's Medium.
- Row 12: no diff measured means 0 files under `src/`, so F1's **Low** column (`≤ 10`) matches — contradicting FR-16's Medium.

FR-11 even provides a "highest matching column" tie-break rule for *within*-factor collisions, which makes the silence about cross-requirement overrides more conspicuous. An implementer working from FR-11 will write Low; AC-16, AC-19 and AC-43 assert Medium.

**Evidence**: FR-11's F5 Low column: "no deviation entries (every declared id is `Shipped`)"; FR-11's F1 Low column: "≤ 10"; FR-16 rows 8/9 "F5 = Medium"; FR-16 row 12 "F1 = Medium".

**Recommendation**: Add a sentence under FR-11's table: "FR-16 rows 8, 9 and 12 override the table for the degraded states they define (F5 = Medium, F5 = Medium, F1 = Medium respectively); the table applies only when the corresponding input was available."

---

### 12. The "open design question" leaks in both directions — the Critical-files bullet presupposes the answer, and FR-6 is given an escape hatch (Score: 58)

The Additional Context section correctly keeps the Measurer/Synthesiser split out of FR-6 (verified: no FR names an internal role). But two other passages break that discipline:

1. **The escape hatch.** "an implementation that keeps ADR 0072's current split unchanged must still satisfy FR-6 **or explain, in that ADR, why it cannot**." A requirements document must not offer an implementation a documented way to *not satisfy* a functional requirement. As written, an ADR paragraph is sufficient to waive FR-6's diagram capability entirely — which would silently void AC-56, AC-57, AC-59, AC-60 and AC-63.
2. **The presupposed answer.** The Critical-files bullet reads: "`docs/adr/0072-…` — the Measurer/Synthesiser split and **the Step 5 extraction budget that FR-6's diagram reads have to fit inside**." That asserts the Measurer-extension resolution, in direct tension with the same section's claim that "Whether that is satisfied by extending the Measurer's extraction step, by granting the Synthesiser a bounded read, or by some other arrangement is a decision for the ADR amendment that follows this one."

**Evidence**: Additional Context, "Open design question…" paragraph; Additional Context, "Critical files for the design/implementation phase" bullet 4.

**Recommendation**: Delete "or explain, in that ADR, why it cannot" — FR-6 is a requirement, not a preference. Reword the Critical-files bullet to "the Measurer/Synthesiser split and the Step 5 extraction budget that the ADR amendment must reconcile with FR-6's diagram reads".

---

### 13. AC-60 asserts more than FR-14 requires, so a conforming implementation can fail it (Score: 58)

FR-14 explicitly permits the optional tree to be a *type or namespace* hierarchy: "an ASCII tree or sketch showing how the listed paths relate to each other (which file calls which, **or where each sits in a type or namespace hierarchy**)". FR-6 (c) binds only *paths* to FR-17's tracking test: "**Paths** named in a diagram are paths written into `show-me.md` and are bound by FR-17".

AC-60 then asserts, without qualification, "**every node** resolves to a path tracked in git". A conforming type-hierarchy tree whose nodes are `IAmAHandlerFactory`, `IAmALifetime`, `IAmAScope` has no node that resolves to a path at all — it fails AC-60 while satisfying FR-14, FR-6 and FR-17. The same over-reach affects the `(unchanged)` marker rule, which is defined for *paths* in the diff but applied by AC-60 to every node.

**Evidence**: AC-60: "…every node in the tree that does not appear in the spec diff is marked `(unchanged)`, **every node resolves to a path tracked in git**…"

**Recommendation**: Rewrite AC-60's clause as "every node **that is a path** resolves to a path tracked in git, and every such path not in the spec diff is marked `(unchanged)`", and add a second criterion covering the type-hierarchy variant under NFR-7's attribution rule.

---

### 14. FR-6's gloss and prose-style obligations are untestable and have no AC (Score: 55)

FR-6 contains two normative sentences that no criterion checks and that no two developers would judge the same way:

- "The narrative **must not** introduce an internal type name without a short gloss on first use." What is an "internal type name" (`IAmALifetime`? `ServiceProvider`? `IServiceScope`?), and what makes a gloss "short"? AC-12 checks word count, ADR naming, link resolution and the absence of bare-number references — nothing about glosses.
- "150–600 words of prose (**not a bullet dump, not a copy-paste of ADR *Decision* sections**)." Neither prohibition has an operational test; a 600-word section of full sentences arranged as twelve bullets satisfies every checkable clause.

**Evidence**: FR-6, paragraphs 1 and 4; AC-12's assertions.

**Recommendation**: Either give the gloss rule a mechanical shape (e.g. "the first occurrence of any identifier matching `\b(I?[A-Z][A-Za-z]*[a-z][A-Za-z]*)\b` that appears in the spec diff must be followed within the same sentence by a parenthetical or appositive gloss") with a matching AC, or demote both sentences to non-normative guidance in Additional Context.

---

### 15. NFR-2's 350-word floor is justified by a non-sequitur (Score: 55)

The stated reasoning does not support the number chosen. NFR-2 says the floor moved from 400 to 350 because a minimal conforming output "**now lands just above 400**" — but an output above 400 *passes* a 400 floor. The premise argues for keeping 400, not lowering it.

My own recount of the minimal case supports the premise, not the conclusion: FR-6's 150-word prose minimum alone, plus FR-7's two fixed lines (~11), FR-8's four parts (~47), FR-9's two lines (~25), FR-10's bucket and ref lines (~80), FR-12's rationale (~60), FR-13's verbatim sentence (22), three FR-14 bullets (~75), eight H2 headings (~24) and FR-6 (e)'s `No diagram:` line (~25) floors at roughly **520 counted words**. The 350 bound is therefore never binding in either direction and the rationale attached to it is incorrect.

**Evidence**: NFR-2: "The lower bound is **350** rather than 400 because … a minimal spec … now lands just above 400, and a floor that a conforming minimal output can fail is the same defect as a ceiling a conforming maximal output cannot meet."

**Recommendation**: Either show a conforming minimal output that actually falls below 400 (and keep 350), or restore 400 and correct the rationale to say the margin was judged too thin. As written the paragraph asserts a fact that contradicts the change it justifies.

---

### 16. FR-10 does not fix the rendering of the bucket breakdown, and NFR-2's count depends on it (Score: 52)

NFR-2 exclusion (b) drops "any line that begins with `|` after trimming (pipe-table rows)". FR-10 requires `## Blast radius` to report six bucket lines, totals, a public-API count, a subdirectory count, a commit count and three "what was measured" lines — but never says whether these are a pipe table or plain lines. Rendered as a table, ~100–130 words vanish from the NFR-2 count; rendered as lines, they count.

NFR-2's own floor rationale silently assumes the *line* rendering ("six bucket lines"), while FR-11 explicitly says "a table" for the risk factors — showing the document knows how to specify this when it wants to. Two implementations will produce word counts differing by well over 5% of the budget, and NFR-2 is asserted as a mechanically checkable AC (AC-33).

**Evidence**: FR-10 "The section reports … for each of the buckets"; NFR-2 (b); NFR-2's floor rationale "six bucket lines"; FR-11 "The section contains a table with one row per factor".

**Recommendation**: State the rendering for `## Blast radius` explicitly (a pipe table is the natural choice and keeps the numbers out of the prose budget), and align NFR-2's floor arithmetic with whichever is chosen.

---

### 17. ADR 0073 — named in this spec's own `.adr-list`, and titled for the material this amendment removes — is never mentioned (Score: 52)

`specs/0037-show-me/.adr-list` contains exactly two entries:

```
0072-show-me-command-resolution-and-output.md
0073-show-me-review-history-and-risk-model.md
```

The amendment removes review history entirely and cuts the risk model from five factors to three — i.e. it invalidates most of ADR 0073 by title and by content. Yet the document discusses only ADR 0072: the "Open design question" paragraph speaks of "the ADR amendment that follows this one" (singular), and the Critical-files list names 0072, `requirements.md`, `status.md`, `README.md` and `settings.json` — not 0073.

This is a real continuity gap for the design phase, and it is self-referentially awkward: FR-6 requires `show-me.md` to name **every** ADR in `.adr-list` with its current Status, so this spec's own eventual `show-me.md` will have to name an ADR about review history that the command no longer implements.

**Evidence**: `specs/0037-show-me/.adr-list` (verified); Additional Context "Open design question for the ADR amendment"; Additional Context "Critical files for the design/implementation phase".

**Recommendation**: Name `docs/adr/0073-show-me-review-history-and-risk-model.md` in the Critical-files list and state what must happen to it (superseded, retitled, or amended to the three-factor model), so the design phase does not leave a stale ADR in the spec's own `.adr-list`.

---

### 18. FR-16 rows 8 and 9 silently delete FR-8's Part 3, which is still derivable (Score: 50)

FR-8 states the section "has exactly four parts". FR-16 rows 8 and 9 say `## Did it ship what it said?` "contains **exactly**" a single line — which removes Part 3 (`Shipped beyond the requirements`) along with Parts 1, 2 and 4. But Part 3 is derived from `tasks.md`, not from `requirements.md`: when `requirements.md` is missing or declares no ids, *everything* shipped is outside the numbered requirements, and Part 3 is the only part that still carries information. FR-8 never notes the exception, and AC-16/AC-43 lock in the deletion.

**Evidence**: FR-8 "It has exactly four parts, in this order"; FR-16 row 8 "contains exactly `No requirements.md found for this spec — scope reconciliation is not possible.`"; row 9 "contains exactly `requirements.md declares no numbered requirements — nothing to reconcile.`, with no shipped-as-planned line, no deviation list and no count line".

**Recommendation**: Either keep Part 3 in rows 8 and 9 (and say so in FR-8), or add one sentence to FR-8 acknowledging that rows 8/9 replace all four parts.

---

### 19. FR-8 Part 3's "short list" is unbounded and unspecified (Score: 45)

Part 3 is "A short list naming work present in `tasks.md` that no numbered requirement covers". No cap, no format, no per-entry word limit, and no evidence obligation — unlike Part 2's deviation entries, which specify id, paraphrase, status, reason, evidence and follow-up. It counts toward NFR-2's budget (only pipe rows, `## Inputs used` and fenced blocks are excluded), so an unbounded Part 3 is a live risk to the ceiling flagged in finding 7. No AC constrains it.

**Evidence**: FR-8 Part 3, one sentence, with no example and no AC citing it.

**Recommendation**: Bound it (e.g. "at most five entries, each ≤ 20 words, each citing a task id"), and add it to an existing FR-8 criterion.

---

### 20. The commit count is required in two sections with no consistency obligation (Score: 42)

FR-9 says `## How it was built` "states two things **and nothing else**": task shape **and** "the number of commits on the spec branch since the merge base". FR-10 independently requires `## Blast radius` to report "the commit count". The same number therefore appears twice, with no requirement that the two agree and no AC comparing them — while NFR-1 lists "commit count" once as a deterministic field.

**Evidence**: FR-9 paragraph 1; FR-10 "…the count of changed public API declaration lines; **and the commit count**"; NFR-1 bullet 2.

**Recommendation**: Pick one owning section (FR-9 reads more naturally), remove it from the other, or state explicitly that the two must be the same value.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 13 |
| 0-49 (Low) | 2 |

**Total findings**: 20
**Findings at or above threshold (60)**: 10

## Verification notes

Claims checked and found **correct** (not findings): the three `specs/0002-*` directories,
`specs/0021-Expose Unacceptable Message Window/`, and `specs/0005-defer-message-on-error/`
(containing only `requirements.md`) all exist on `master`; `docs/adr/` on `master` carries exactly
14 duplicated numbers with 0037 ×5 and 0057 ×4, matching C-9's list item-for-item; the calibration
branch and HEAD both carry two 0070s and two 0071s; spec 0036's diff is 517 files bucketed
76/393/24/14/0/10 summing to 517, spanning exactly the six named `src/` subdirectories, with 363
commits, an 82-task/229 KB `tasks.md` with zero unchecked boxes, `.issue-number` 4256, PR #4282, and
14 `release_notes.md` items of which 5 carry combined classifications; `.claude/settings.json`'s `gh`
allow-list is exactly the five entries C-10 names; `.agent_instructions/adr_frontmatter.md` does say
the number is a non-unique ordering hint and renumbering is rejected; issue #4357 is CLOSED.
AC-54's arithmetic (26+1+1+0+0+0 = 28) and AC-15's (12 FR + 5 NFR = 17) are both internally
consistent, and 28 also happens to be this document's own declared-id count. Every FR and NFR maps
to at least one AC, every surviving FR-16 row has a matching AC, and no retired identifier (F3, F4,
FR-16 rows 3/4, C-3, AC-38/39/48/49/50) is reused anywhere.
