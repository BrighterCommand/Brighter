---
id: 0073-show-me-review-history-and-risk-model
title: "Review-History Decomposition and the Five-Factor Risk Model for /spec:show-me"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-09-19
summary: "The PR comment history is measured as a structural skeleton (heading, numbered-title and reply lines only — 10 KB rather than 69 KB on the calibration PR) and decomposed by an ordered qualify/locate/split/group/attach/exclude/severity/resolution procedure run inline in the main agent, with exactly three named judgement points; the five risk factors are then mapped by one shared evaluate-downward-from-High procedure whose maximum is the overall level, computed after the sections F2 and F5 project from, and read by no conditional in the command."
tags:
  - "meta"
  - "api-design"
---

# 0073. Review-History Decomposition and the Five-Factor Risk Model for `/spec:show-me`

Date: 2026-09-19

## Status

Accepted

## Context

Spec 0037's `/spec:show-me` writes one file whose last two substantive sections are the ones that
cannot be produced by counting lines in the repository: `## How it was built`, which must state a
review-history line per implementation review round, and `## Risk assessment (advisory)`, which must
state five factor levels and an overall level derived from them. Everything upstream of those two
sections is settled.

**Parent Requirement**: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)

**Scope**: This ADR covers review-history decomposition (Definitions: Finding, Review round,
Finding severity; FR-9) and the five-factor risk-scoring model (FR-11, FR-12, FR-13). It extends
[ADR 0072](0072-show-me-command-resolution-and-output.md)'s fact-ledger and Measurer/Synthesiser
vocabulary rather than replacing it; command invocation, target/branch/PR resolution, and the
overall output-file structure (including FR-6/FR-7/FR-8/FR-14's content) are that ADR's scope, not
this one's.

### Why review history needs this much structure

The `Finding`, `Review round` and `Finding severity` definitions in `requirements.md` are unusually
long for definitions. That length is not fastidiousness: it is six rounds of adversarial review, each
checking a claim against PR #4282's actual comment history, each finding one more way a plausible
count comes out wrong. The whole list is reproducible today with `gh pr view 4282 --json comments`,
and every case below was re-verified against the live PR while drafting this ADR:

- **A pass can span two comments.** `2026-08-28T15:24:52Z` is titled `## Review — Spec 0036 (design
  only), part 1 of 2` and ends at numbered item 5; `2026-08-28T15:25:21Z` is titled `part 2 of 2` and
  begins at item 6. Twenty-nine seconds apart, same author, continuous numbering. One pass, not two.
- **Most comments by the review bot are not reviews.** Six comments dated `2026-09-16` (13:23, 13:45,
  14:25, 17:03, 17:16, 17:35) carry no numbered item at all. They sit under the same
  `**Claude finished @iancooper's task…**` preamble that the genuine rounds' tracking comments also
  carry, so the preamble is useless as a discriminator.
- **A trailing aside is not a finding source, whatever it says.** Round 2's tracking comment
  (`2026-09-18T07:44:11Z`) ends with `### Smaller notes` and three bullets — one of which
  (`ReleaseUnmanagedResources` releases only *managed* resources) is a defect assertion in plain
  English. Counting it would make that round eight findings instead of five.
- **…but a *numbered* bucket is.** The design pass's `### 10. Smaller items` holds five bullets and is
  item 10 of a 1–10 sequence. Same reviewer, same "bucket of small things" genre, one numbered and one
  not — which is precisely the line the `Finding` definition draws, and why that pass raises fourteen
  findings rather than ten.
- **A fix-verdict section is never a new finding, even when it flags something.** Round 2's
  `### Verdict on each fix` carries `**Fix 3 — fd77dcec, unreadable override: ⚠️ removes the crash,
  but replaces it with silence about the actual misconfiguration.** Details below — this is the one
  finding I'd want revisited.` That concern is real, and it is counted — once, as `#### 1.` of the
  same comment's `### New findings`. Counting the verdict item too would give six.
- **The same finding can appear twice.** Round 1's tracking comment (`2026-09-16T17:43:48Z`) heads its
  sequence `### Findings (posted inline, most severe first)`, and inline review comments were posted
  separately at `17:53:32`, `17:53:39` and `17:53:45`. Three inline comments and three restatements are
  three findings, not six.
- **A specification-phase pass is not a review round at all.** The 2026-08-28 pass reviewed
  `requirements.md` and the ADRs, not the code. It is excluded entirely and reported only as a count.

There is a seventh case the definitions warn about that is visible verbatim in the data: round 1's
third restated finding ends `…so low blast radius, but could produce flaky test failures if hit.`
That `low` is body prose. `Finding severity` says it must not match, and the three findings of that
round are unclassified.

None of these is an arbitrary rule. Each is a wrong answer someone actually produced, against this
PR, and then corrected.

### What this ADR inherits and what it must add

[ADR 0072](0072-show-me-command-resolution-and-output.md) settled the artefact (one prompt file), the
Step 0–8 procedure skeleton, the precondition gate, the fact ledger, the single-`Write` output and the
front matter. Its Step 5 extraction list names `CI rollup · PR comments` as inputs and stops there; its
Step 6 table has no row for FR-9 or FR-11. Its Alternative 4 says, in as many words, that reading a
PR's full comment history "is the most context-hungry part of the whole command, and it is a
self-contained input-to-counts transformation — a good candidate for delegation if that ADR finds it
needs one," and leaves the decision here.

So this ADR must decide four things ADR 0072 did not:

1. Where the decomposition runs — main agent or sub-agent.
2. The decomposition procedure itself, precisely enough to follow by hand against comment JSON.
3. How F1–F5 are computed and who owns each input, including F4, which ADR 0072 mentions but does not
   specify.
4. How FR-11's general tie-break clause is implemented across the five factors.

### The tension this ADR has to resolve, not paper over

NFR-1 names the round decomposition as judgement:

> the review-round decomposition itself (and therefore F3) — deciding what counts as one distinct
> finding (the `Finding` definition's container/sub-issue split), whether a given comment "asserts a
> defect" (the `Review round` definition's qualification test), and whether a reply's wording counts as
> resolving a finding (the `Resolved finding` definition's "states was addressed by the fix made for
> another finding" clause) are each a judgement call, not a mechanical count

Yet the definitions it points at are the most precise prose in the document — fifteen-minute windows,
first-word-before-the-dash severity parsing, three disjunctive phase tests. Precise rules and
judgement look contradictory. They are not: a precise rule still has to be *applied* to messy real
text, and the application is where the judgement lives. The useful observation is that NFR-1 already
enumerates *exactly which three* applications are judged. Everything else in the decomposition — which
lines are headings, which carry numbers, which timestamps fall inside fifteen minutes, which token a
severity marker yields — is arithmetic over text the command already has. This ADR makes that
enumeration structural rather than incidental.

### Constraints this ADR inherits

- **C-10 / FR-18** — `gh` access is confined to `gh pr view`, `gh pr list`, `gh pr diff`. Re-verified:
  `.claude/settings.json`'s `gh` allow-list is exactly `Bash(gh pr view:*)`, `Bash(gh pr list:*)`,
  `Bash(gh pr diff:*)`, `Bash(gh issue view:*)`, `Bash(gh issue list:*)`. Both new JSON fields this
  ADR needs (`reviews`, `statusCheckRollup`) are `gh pr view` fields, so **this ADR proposes no
  front-matter change to ADR 0072's `allowed-tools` and no settings change.**
- **NFR-3** — bounded cost, degrade by chunking never by skipping, and never report a failed
  extraction as zero.
- **NFR-7** — every factual claim attributable to a listed input; AC-32 tests F2/F3/F5 for consistency
  with the evidence cited in the same run's own output rather than for byte-equality.
- **FR-13 / `Advisory`** — "no behavioural difference between a `Low` result and a `High` result."

There is no prior art in this repository for either half of this ADR. A grep of `docs/adr/*.md` for
`risk assessment`, `risk scor`, `review round` and `statusCheckRollup` returns only ADR 0072 itself
and one unrelated prose hit; nothing in `docs/adr/` has previously decomposed GitHub review comments
into counts or scored anything for risk.
[ADR 0071](0071-tdd-review-gear.md) remains the tonal precedent for an ADR about the project's own
tooling rather than Brighter's runtime.

## Decision

Decompose the review history **inline in the main agent**, from a *structural skeleton* of the comment
history rather than its full text, by an ordered eight-stage procedure with exactly **three named
judgement points**; then compute the five factors by **one shared mapping procedure evaluated downward
from High**, take their maximum as the overall level, and consume that level in exactly two places,
neither of them a conditional.

Two additions to ADR 0072's vocabulary, and no replacements:

| Role | Stereotype | Owns | Mechanism |
|------|-----------|------|-----------|
| **Measurer** (ADR 0072) | information holder | every value NFR-1 requires identical between runs | `git`/`gh`/`grep`/`awk` whose output is a count, sha, ref or short line list |
| **Classifier** (new) | decider | the round/finding/severity/resolution tallies, and F1–F5's levels | applying the Definitions' stated rules to Measurer-produced extracts; emits ledger rows, never prose |
| **Synthesiser** (ADR 0072) | decider | prose | renders FR-9's and FR-12's lines from Classifier rows |

The Classifier is the seam ADR 0072's binary split leaves open. Its output is numbers, which looks like
Measurer work; its method is reading prose, which looks like Synthesiser work. Rather than bend either
role, name the third and give it a contract:

- **It may read only Measurer-produced extracts.** It never re-fetches, and never has the full comment
  bodies at its disposal.
- **Every row it emits carries the rule it applied and the line it applied it to**, not just a value.
  A round row is not `5 findings`; it is `Round 2 | 5 findings (0/0/2/3/0); 3 resolved, 2 acknowledged,
  0 open | comment 2026-09-18T07:44:11Z, heading "### New findings", items 1–5; replies in comment
  2026-09-18T10:13:14Z | used`. That is what AC-32's "consistent with the evidence cited in the same
  run's own `## How it was built` lines" is checked against.
- **The Synthesiser still may not count.** FR-9's line is a rendering of a Classifier row.

### Architecture Overview

This extends ADR 0072's Steps 5 and 6; it does not restart the skeleton.

```
  … ADR 0072 Steps 0–4 (gate, branch, PR discovery, diff source) …
        │
        ▼
┌──────────────────────────────────────────────────────────────────────┐
│ MEASURE — ADR 0072 Step 5, plus this ADR's Step 5R                    │
│   5R.1  Comment census      gh pr view --json comments  (3 fields)    │
│   5R.2  Structural skeleton same call, heading/title lines only       │
│   5R.3  Review stubs        gh pr view --json reviews                 │
│   5R.4  Bounded body slices only for comments 5R.2 marked interesting │
│   5R.5  CI rollup tally     gh pr view --json statusCheckRollup       │
│   5R.6  tasks.md first-add  git log --diff-filter=A --format=%aI      │
└──────────────────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────────────────┐
│ CLASSIFY — Step 5R.7, ledger rows in, ledger rows out, no shell       │
│   A Qualify      ◆ judgement point 1: "asserts a defect"              │
│   B Locate sequence  (exclude verdict sections, exclude trailing aside)│
│   C Split containers ◆ judgement point 2: "container of sub-issues"   │
│   D Group comments into passes (author · numbering · 15 min)          │
│   E Attach inline comments, dedup against restatements                │
│   F Exclude specification-phase passes; number the survivors 1..k     │
│   G Severity per finding, from the title marker only                  │
│   H Resolution ◆ judgement point 3: "addressed by another fix"        │
└──────────────────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────────────────┐
│ SYNTHESISE — ADR 0072 Step 6, ordered so F2/F5 read finished text     │
│   6.a  Sections 1–5   (FR-6, FR-7, FR-8, FR-9, FR-10)                 │
│   6.b  Section 6      F1..F5 by the shared mapping, then max (FR-11/12)│
│   6.c  Sections 7–8   (FR-14, FR-15)                                  │
└──────────────────────────────────────────────────────────────────────┘
        │
        ▼
  ADR 0072 Steps 7–8 (one Write; budget self-check; FR-19 report)
```

### Key Components

#### 1. No sub-agent — decided here on its own evidence, not inherited

ADR 0072 declined delegation for its scope and left this step open. Re-examined for this step, the
answer is the same, for a different and stronger reason.

The case *for* delegating looked good on the raw numbers: `gh pr view 4282 --json comments` returns
**68,837 bytes** (~17k tokens), of which the two re-review tracking comments alone are 14,914 and
14,815 bytes, while the output needed is a dozen integers. That asymmetry is exactly what delegation
is for.

The asymmetry disappears once NFR-3's own discipline is applied to the input. ADR 0072 never reads
`tasks.md` or the diff — it counts them. The same move works here. The structural skeleton of
`5R.2` — comment boundary lines plus lines matching a heading or a numbered-item title — is
**10,151 bytes** on this PR, an 85% reduction, and it contains every discriminator the Definitions
need: the two-part titles, the six finding-free task-completion comments (one heading line each, no
numbered items), `### Verdict on each fix`, `### New findings`, all five severity markers, `###
Smaller notes`, `### 10. Smaller items`, and the author's numbered disposition replies. Nothing that
was dropped — `### Areas checked with no solid defect found`, `### Re-checked with nothing new
found`, `### Test coverage` — contributes a finding by definition.

Against delegation, three arguments that survive:

- **The prompt would be as large as the input.** A sub-agent starts clean, so it must be handed the
  `Finding`, `Review round`, `Finding severity`, `Resolved finding`, `Acknowledged finding` and `Open
  finding` definitions verbatim — roughly 1,500 words — *plus* the skeleton, or else re-fetch the
  68 KB itself. Handing it both costs about what running inline costs; re-fetching costs four times
  more and, as ADR 0072 notes for the NFR-3 budget, spends the same measurement twice.
- **The output is not a handful of integers.** NFR-7 and AC-32 require each tally to cite the comment
  and heading it came from. A sub-agent returning bare counts breaks the ledger's provenance chain;
  a sub-agent returning counts *with* their evidence is returning most of the skeleton back.
- **Delegated judgement is unauditable judgement.** The three judgement points are the least
  reproducible part of the whole command. Inline, the main agent's own reasoning is in the transcript
  and the ledger row names the line it read. Behind a sub-agent boundary, a wrong finding count
  arrives as an assertion with a plausible citation and no way to see which rule was misapplied. The
  README's stated rationale for delegating — a `Plan` sub-agent "has no file-editing tool" — is empty
  for a read-only step, so there is nothing on the other side of the scale.

Decision: **run inline.** The command file states the skeleton-first rule explicitly, because the
delegation question returns the moment someone reads the raw comment JSON into context.

#### 2. The structural skeleton (Step 5R.2)

One invocation, covered by the existing `Bash(gh pr view:*)` entry:

```bash
gh pr view {n} --json comments \
  --jq '.comments[] | "===\(.createdAt)\t\(.author.login)\t\(.body|length)", (.body|split("\n")[])' \
  | grep -E '^===|^#{1,6}[[:space:]]|^[0-9]+\.[[:space:]]\*\*|^\*\*Fix'
```

`--jq` is part of the allowed `gh pr view` invocation, not a separate `jq` binary. The patterns are
POSIX classes per ADR 0072's BSD-compatibility rule. The four alternatives capture, in order: the
comment boundary with its timestamp, author and length; any ATX heading at any level; a top-level
numbered item whose title is bold; and the `**Fix N — …**` shape a verdict section uses when it is not
under its own heading.

Heading level is deliberately not fixed anywhere in the procedure. Verified on this PR: round 2 heads
its sequence `### New findings` while round 3 heads its `## New findings`, and their items are `####
1.` and `### 1.` respectively. Any rule keyed on level would have counted one of the two rounds as
zero findings.

#### 3. The decomposition procedure (Step 5R.7)

**A — Qualify.** Per `Review round` (b): a comment is part of a round "only if it contains at least one
finding — a numbered item that names a file, symbol, requirement or behaviour in the change under
review and asserts a defect, risk or requested change." The definition then says what the
discriminator is *not*: comments reporting completion of an unrelated agent task, progress updates, CI
notifications and finding-free discussion replies are not rounds "even when posted by the same bot on
the same PR and under the same `Claude finished @user's task` preamble that a genuine round's own
tracking comment also carries — the discriminator is the presence of numbered findings in the body,
never the preamble text."

In the skeleton this is visible without reading a word of body prose: the six 2026-09-16
task-completion comments collapse to a comment-boundary line with no numbered item beneath. **Judgement
point 1** is only the residual case — a comment that *has* numbered items whose assertions may or may
not be defects.

**B — Locate the findings sequence.** Per `Finding`, a finding is drawn **only** from an inline review
comment or "a **numbered** item inside the tracking comment's **findings sequence** — the numbered/
lettered list under whichever heading names new findings (e.g. `### New findings`, `## New findings`),
or, if the comment names no such heading, the single numbered list that describes defects/risks/
requested changes in the code". In order:

1. A heading whose text names new findings, level-agnostic. The sequence is its numbered items up to
   the next heading at the same or higher level.
2. Failing that, the single numbered list describing defects. Round 1's is under `### Findings (posted
   inline, most severe first)`.
3. **Excluded: fix-verdict sections.** The definition names both observed shapes — "a numbered or
   titled section that grades the *previous* round's fixes, e.g. `### Verdict on each fix`/`## Fix #1 —
   …: ✅`, which is evidence for `Resolved`/`Acknowledged` status, never a source of a new finding,
   even where one such verdict item is qualified or flags a residual concern."
4. **Excluded: the trailing unnumbered aside.** "A trailing titled-but-unnumbered aside after the
   findings sequence — e.g. a closing `Smaller notes` section — is never itself a source of findings,
   however many bullets it contains or what they assert."

**C — Split container items.** "A numbered item that is itself a **container** for several distinct
sub-issues (e.g. a 'smaller items' bucket of five bullets, itself numbered as one item in the findings
sequence) counts as one finding per sub-issue, not one." The discriminator against B.4 is **numbering,
not content**: `### 10. Smaller items` is item 10 of the sequence; `### Smaller notes` has no number.
Deciding whether a bucket's bullets are genuinely distinct sub-issues is **judgement point 2**.

**D — Group comments into passes.** Three conjunctive tests from `Review round` (a): same author; the
later comment's numbering continues the earlier's without restarting; posted within 15 minutes.
"A comment whose numbering restarts at 1 begins a new round." Verified: `part 1 of 2` → `part 2 of 2`
is 29 seconds, same author, item 5 → item 6; the three implementation tracking comments each restart at
1 and are three passes.

**E — Attach inline comments and dedup.** Review submissions are enumerated via
`gh pr view {n} --json reviews --jq '.reviews[] | "\(.submittedAt)\t\(.author.login)\t\(.state)"'`.
Each is attributed to "whichever round's tracking comment was posted within 15 minutes of them (before
or after) and describes the same finding" — verified: submissions at 17:53:32/39/45, tracking at
17:43:48, ten minutes *after*. Author login is explicitly not a test: "A round's inline PR review
comments belong to that round regardless of author login even when it differs from the round's
tracking-comment author." Dedup then applies: "When the same review pass posts an issue both as an
inline PR review comment and as a restatement inside its own tracking comment, this is one finding,
not two — the inline comment is the finding of record." Three submissions plus three restatements is
three findings.

**Operative consequence, and the one real limitation in this design.** Inline review comment
*bodies* are not reachable inside the allow-list. Verified: `gh pr view 4282 --json reviews` returns
all three submissions with `"body": ""` and `bodyLen: 0`, and `gh pr view 4282 --comments` renders
exactly fifteen `author:` blocks — the fifteen issue comments, no inline threads. Only `gh api` would
return them, and that is forbidden by C-10 and by Out of Scope. So the command reads the
*restatement*, and treats the submission count as a corroborating cardinality check on the round's
inline finding count. The definitions already anticipate this: `Finding severity` says a severity word
"restated inside a tracking-comment summary of an inline finding … is not matched" — which reads as a
defensive footnote but is in fact the operative rule, because the restatement is the only text there
is. Round 1's restated titles carry no marker (`**[`HandlerLifetimeScope.cs:121`](…)**`) and its three
findings are unclassified, notwithstanding the `low blast radius` in item 3's body.

**F — Exclude specification-phase passes.** Applied to the **pass**, after grouping, so a two-part pass
is included or excluded as one unit. Any of three tests suffices: (i) self-identification as a
requirements/design/tasks review; (ii) posted before the commit that first adds
`specs/{target spec}/tasks.md`, **by author date** — `requirements.md` is explicit that "a later rebase
can move a commit's committer date without changing when the file was actually written, so committer
date is not used for this test", which fixes the measurement as
`git log --diff-filter=A --format=%aI -- "specs/{dir}/tasks.md" | tail -1`; (iii) every finding cites
only paths under `specs/{target spec}/` or `docs/adr/`. Verified: the 2026-08-28 pass is excluded by
(i) — its title reads `(design only)` — and by (iii) — its ten item titles cite `requirements.md`,
ADRs 0070/0072, and FR/NFR/AC ids only. Test (ii) is *false* for it, which is why all three tests are
disjunctive. The pass contributes zero rounds and zero findings and increments the counter FR-9 renders
as `Specification-phase review passes excluded: 1`. Surviving passes are then numbered 1..k in
chronological order.

**G — Severity.** Read "**only** from a marker in the finding's **title** — a trailing parenthetical or
italicised tag immediately after the title text … or a leading bracketed tag … Matched
case-insensitively after stripping markdown emphasis and surrounding punctuation, taking the first word
of the marker and ignoring anything after a dash/colon within it". `critical`→Critical, `high`→High,
`medium`→Medium, `low`→Low, `nit`→Low; no marker or an out-of-set word → unclassified, risk-scored as
Medium by F3 but reported in FR-9's own `{u}` slot. Verified end to end on the live data:

| Round | Titles as posted | Split (C/H/M/L/u) |
|---|---|---|
| 1 (09-16 17:43) | three bold file links, no markers | 0/0/0/0/3 |
| 2 (09-18 07:44) | `*(medium)*`, `*(low)*`, `*(medium)*`, `*(low — a question, not a defect)*`, `*(low)*` | 0/0/2/3/0 |
| 3 (09-18 11:39) | `*(medium — NFR-10)*`, `*(low)*`, `*(nit)*` | 0/0/1/2/0 |

which is AC-17's assertion exactly. AC-49's `*(Blocker)*` lands out-of-set → unclassified → scored
Medium.

**H — Resolution.** Per finding, in this precedence: an author reply's per-item disposition; a commit
on the spec branch made after the finding was posted that changes the code or document the finding
names (`git log --format='%H %aI' {mb}..{head} -- {path}`); the PR thread marking it resolved/outdated.
Verified against the 2026-09-18T10:13:14Z reply to round 2:

| Item | Reply text | Status | Clause |
|---|---|---|---|
| 1 | "**fixed** in `ac46633de`" | Resolved | commit changes the named file |
| 2 | "**closed for free** by #1's fix" | Resolved | "addressed by the fix made for another finding in the same round" |
| 3 | "**fixed** in `ec60be748`" | Resolved | commit |
| 4 | "**acknowledged, no change.** … Leaving as-is." | Acknowledged | "by design", "leaving as-is" |
| 5 | "**accepted for now.** … Will revisit if … actually hit in practice." | Acknowledged | verbatim in the definition |

Item 2 is **judgement point 3**, and NFR-1 cites this exact line as its example. Rounds 1 and 3 are
3/0/0 each — round 1 by the 07:43:57 re-review request naming `fd77dcece`, `dbe70e0b0`, `d4ac64b8e`
and round 2's own three ✅ verdicts; round 3 by the 14:46:58 reply. Totals: **11 findings, 9 resolved,
2 acknowledged, 0 open**, reproducing requirements.md's verified figures.

Note that step B.3 *excludes* the verdict section as a source of findings while step H *admits* it as
evidence of resolution. That is not an inconsistency; it is one input read twice with two different
questions, which is what the `Finding` definition's "evidence for `Resolved`/`Acknowledged` status,
never a source of a new finding" means operationally.

#### 4. Factor inputs, and who owns each

| Factor | Input | Computed by | Consumed here as |
|---|---|---|---|
| F1 | files changed under `src/` | ADR 0072 Step 5 (blast-radius bucket counts) | the `src/` bucket integer |
| F2 | breaking-change item count | ADR 0072 Step 6 (FR-7 synthesis) | the emitted `Total breaking-change items: {n}` line |
| F3 | round/finding tallies | **this ADR**, Step 5R.7 | the Classifier's round rows |
| F4 | `statusCheckRollup` | **this ADR**, Step 5R.5 | the per-entry state tally |
| F5 | requirement statuses | ADR 0072 Step 6 (FR-8 synthesis) | the emitted `Shipped: … (of {total})` count line, plus each non-`Shipped` row's follow-up text |

**The handoff rule, stated once: a factor mapping never re-derives its own input.** F2 reads the count
line FR-7 already wrote; F5 reads the count line and follow-up cells FR-8 already wrote. Re-partitioning
breaking-change items or re-judging a requirement status inside the risk table would judge the same
evidence twice and could disagree with the section printed two pages above it — which AC-34 would catch
as a claim not matching its input's actual content. The consequence is a sequencing constraint: **the
risk table is synthesised after the sections it projects from**, even though FR-5 places it sixth.
Step 6 therefore runs 6.a (sections 1–5), 6.b (section 6), 6.c (sections 7–8), and assembly into FR-5's
fixed order happens at Step 7, which ADR 0072 already defines as a single in-memory `Write`.

F4 is claimed by this ADR because ADR 0072 lists `CI rollup` as a Step 5 input without specifying the
fetch, and the fetch has a hazard worth recording.

#### 5. F4 and the heterogeneous rollup

`statusCheckRollup` is not a uniform array. Verified on PR #4282, which returns 28 entries: 26 are
`CheckRun` (`name`, `status`, `conclusion` populated, `state` null) and 2 are `StatusContext`
(`state` populated, `name`/`status`/`conclusion` all **null** — one such entry is `license/cla` with
`state: "SUCCESS"`). Reading `.conclusion` alone classifies those two as unrecognised → Medium under
FR-11's catch-all, when their actual state is `SUCCESS`. A `CheckRun` still running has a null
`conclusion` too, and its live value is in `.status`. The projection is therefore a three-way
fallback, one token per entry:

```bash
gh pr view {n} --json statusCheckRollup \
  --jq '.statusCheckRollup[] | (.conclusion // .state // .status // "UNKNOWN")'
```

Counted with `grep -c` per value and a complement bucket for everything else, following ADR 0072's
blast-radius idiom so the tally sums to `.statusCheckRollup | length` by identity rather than by care.
No `sort`, no `uniq`, per ADR 0072's tool-surface rule. `CI check` in the Definitions fixes the source:
"no other CI source is consulted."

#### 6. The shared factor-mapping procedure

FR-11's general clause is the load-bearing one:

> When more than one of a factor's three column conditions is satisfied *collectively* by the evidence
> that factor measures — a reconciliation table's rows for F5, the findings across *all* implementation
> review rounds (not just one round) for F3, or the checks in a `statusCheckRollup` for F4 — the factor
> takes the *highest* matching column (High beats Medium beats Low).

Implemented as **one procedure, five uses, no per-factor passes**:

> For factor F with evidence set E: evaluate F's **High** condition over the whole of E; if it holds,
> F is High and no further column is evaluated. Otherwise evaluate **Medium** over the whole of E;
> if it holds, F is Medium. Otherwise F is **Low**.

Evaluating downward from High and stopping at the first hit is equivalent to evaluating all three and
taking the maximum — FR-11's mapping is total by construction, so at least one column always holds —
but it is one pass, it cannot produce "no column matched", and it makes the "highest wins" rule a
property of evaluation order rather than a post-hoc comparison someone has to remember to perform.
The three worked examples requirements.md gives all fall out unchanged: a `Deferred` row with a
follow-up alongside a `Dropped` row stating `no follow-up recorded` → F5 High; an acknowledged finding
in one round alongside an open Critical in another → F3 High; a `NEUTRAL` check alongside a `TIMED_OUT`
→ F4 High (AC-48's third case).

F1 and F2 are scalar counts over disjoint ranges, so the downward evaluation is vacuous for them. They
still go through it. One rule with two vacuous applications is cheaper to keep correct than one rule
with two exceptions.

#### 7. The overall level, and advisory-by-construction

FR-12's maximum is computed mechanically from the five Classifier-emitted levels over the ordering
`Low < Medium < High`. The Synthesiser may then raise it with an explicit one-sentence reason naming
what the factors miss, and may never lower it. Before Step 7's `Write`, two assertions run over the
assembled text: the stated level is ≥ the computed maximum, and if it is strictly greater, a raising
sentence is present (AC-23).

FR-13 is made structural rather than promised. The level is written into exactly two places — the
`**Overall risk: {…}**` line in `show-me.md`, and FR-19's session report — and the command file states
the invariant: **no step in the procedure may branch on the level.** `High` and `Low` are values that
get rendered, never conditions that get tested. That is what makes AC-25's "the only difference is the
text inside `show-me.md`" a property of the procedure's shape rather than a behaviour to remember, and
it is the same construction ADR 0072 used to make AC-7's byte-for-byte guarantee a property of step
ordering.

The verbatim sentence FR-13 requires —
`This assessment is advisory only. It is not a merge gate; the merge decision stays with a human
reviewer.` — is a literal in the command file, not something composed at run time.

### Technology Choices

**No new tools, no new allow-list entry.** `--json reviews` and `--json statusCheckRollup` are
`gh pr view` fields, covered by ADR 0072's declared `Bash(gh pr view:*)`. The `git log` forms for the
`tasks.md` first-add date and for commit-based resolution are covered by its `Bash(git log:*)`. The
skeleton uses `grep -E` with POSIX classes, already declared. This ADR therefore changes nothing in
`.claude/commands/spec/show-me.md`'s front matter and nothing in `.claude/settings.json`, keeping
Out of Scope's "no change to `.claude/settings.json`'s allow-list" intact.

**`--jq` rather than a shell filter.** The projections in 5R.2, 5R.3 and 5R.5 are expressed as `--jq`
arguments to `gh pr view` because that is one allow-listed invocation, whereas piping to a standalone
`jq` would need an allow-list entry the repository does not have.

**Counts by `grep -c` with a complement bucket**, not by `sort | uniq -c`: ADR 0072 already rules out
`sort`, and a complement makes the rollup tally sum to the rollup length by identity.

**Body slices, never whole comments.** 5R.4 pulls bounded slices only from comments the skeleton marked
as carrying a findings sequence or a numbered disposition reply, and only between the located heading
and the next heading. On the calibration PR that is three finding sequences and two reply comments; the
two 14 KB "re-checked, nothing found" tails are never read.

### Implementation Approach

#### Step 5R — measurement, appended to ADR 0072's ledger

Run in order; each emits ledger rows in ADR 0072's `{input or metric} | {value} | {command} | used |
not available: {reason}` shape.

```bash
# 5R.1 census — one line per comment
gh pr view {n} --json comments --jq '.comments[] | "\(.createdAt)\t\(.author.login)\t\(.body|length)"'

# 5R.2 structural skeleton (see Key Components 2)

# 5R.3 inline review submission stubs
gh pr view {n} --json reviews --jq '.reviews[] | "\(.submittedAt)\t\(.author.login)\t\(.state)"'

# 5R.5 CI rollup, one token per entry, plus its length
gh pr view {n} --json statusCheckRollup --jq '.statusCheckRollup[] | (.conclusion // .state // .status // "UNKNOWN")'
gh pr view {n} --json statusCheckRollup --jq '.statusCheckRollup | length'

# 5R.6 author date of the commit that first added tasks.md (phase test (ii))
git log --diff-filter=A --format='%aI' "{base}..{head}" -- "specs/{dir}/tasks.md" | tail -1
```

`gh` exiting non-zero at any of these is FR-16 rows 1–2: the ledger records `not available: gh
unavailable`, `## How it was built` takes its defined line, and F3 and F4 are both Medium via the
shared mapping's Medium columns, which name exactly this case. NFR-3's rule applies throughout —
a failed extraction is `not available` with a reason, never zero, and the command checks exit status
rather than empty output, because a PR with no comments and a `gh` that failed look identical on
stdout.

#### Step 5R.7 — classification

Stages A–H as specified in Key Components 3, in that order. D before F matters (group first, then
exclude, so a two-part pass is excluded whole); F before the 1..k numbering matters (FR-9's `Round {i}`
counts only surviving implementation rounds, which is why AC-17 expects `Round 1/2/3` and not
`Round 2/3/4`); G and H are per-finding and order-independent.

The command file carries the Definitions' three exclusion clauses verbatim next to stages B.3, B.4 and
C, because paraphrasing them is how the six review rounds that produced them get undone.

#### Step 6.a — `## How it was built` (FR-9)

Rendered from Classifier rows. One line per surviving round in the exact shape FR-9 fixes:
`Round {i}: {n} findings ({c} Critical, {h} High, {m} Medium, {l} Low, {u} unclassified) — {r}
resolved, {a} acknowledged, {o} open.` Then a totals line across rounds, then
`Specification-phase review passes excluded: {n}` (or `none`) — required by FR-9 so the exclusion is
auditable, and bounded by Out of Scope, which forbids summarising those passes beyond the count.

`{m}` and `{u}` stay in separate slots even though F3 scores both as Medium, because FR-9 says why:
"so a reader can see how much of a round's Medium weight came from an actual severity call versus an
absence of one." FR-16 row 3's `Pull request #{n} has no recorded implementation review rounds.` is
the branch where D held but F excluded everything — still with the exclusion count, which is the case
AC-38 tests.

The task and commit shape in the same section is ADR 0072's Step 2/Step 5 material: its `grep -c`
family over `tasks.md` checkbox lines supplies the per-tag counts with `untagged` as the complement,
and `git log --oneline {mb}..{head} | wc -l` the commit count.

#### Step 6.b — `## Risk assessment (advisory)` (FR-11, FR-12, FR-13)

One table row per factor: factor, measured value, level. FR-11 requires the value, not just the level —
`76 files under src/`, `14 items`, `11 findings over 3 implementation review rounds; 9 resolved, 2
acknowledged, 0 open`, `28 checks: 26 CheckRun, 2 StatusContext; conclusions/states: 5 SUCCESS, 22
SKIPPED, 1 FAILURE`, and F5's count line. Each cell is a Classifier row's evidence field, so NFR-7
holds by projection rather than by assertion, exactly as ADR 0072 made `## Inputs used` a projection
of the ledger.

Then the shared mapping (Key Components 6) per factor, then the maximum, then
`**Overall risk: {Low|Medium|High}**` on its own line, then 2–5 sentences of rationale referencing at
least the factor(s) that set the level, then FR-13's verbatim sentence.

On the calibration case that is F1 High (76), F2 High (14), F3 Medium (the two acknowledged findings
put it in Medium's `≥ 1 acknowledged finding` column — AC-20's point that Medium, not Low, is correct
because F3's Low column requires that nothing be acknowledged), F4 as measured, F5 as measured →
**Overall risk: High**, set by F1 and F2.

#### Step 8 — no change

ADR 0072's budget self-check and FR-19 report are unchanged. FR-9's round lines and FR-12's rationale
count toward NFR-2's 400–2,000 words (the factor *table* does not — NFR-2 excludes lines beginning with
`|`), which is part of why NFR-2's ceiling is 2,000 rather than 1,500.

## Consequences

### Positive

- **The most context-hungry input becomes the cheapest one.** 68,837 bytes of comment JSON project to
  10,151 bytes of skeleton on the calibration PR — an 85% reduction — with every discriminator the
  Definitions need still present. The six finding-free task-completion comments collapse to one line
  each, so `Review round` (b)'s qualification test is something a reader *sees* rather than something
  the model is trusted to have applied.
- **Exactly three judgement points, named.** NFR-1 says the decomposition is judgement-derived; this
  ADR says *which three predicates* are judged and asserts that everything else — heading detection,
  numbering continuity, the 15-minute windows, severity tokenisation, the phase tests, the tallies — is
  Measurer-mechanical and reproducible. That is a stronger and more checkable claim than the
  requirement makes, and it gives a reviewer of a surprising F3 three specific places to look.
- **The Classifier makes the judged counts auditable without pretending they are deterministic.**
  Every tally arrives with the rule and the quoted line it came from, which is what AC-32 actually
  tests — consistency with the run's own cited evidence, not byte-equality with another run.
- **One tie-break procedure, five uses.** The "highest matching column" rule is evaluation order, not a
  comparison step, so it cannot be forgotten on the factor where it matters and cannot fall through to
  "no column matched."
- **Advisory is structural.** The level is rendered in two places and tested in none; AC-25 follows from
  the procedure's shape, the same way AC-7 follows from ADR 0072's step ordering.
- **F4's entry-type hazard is fixed before it ships.** A live rollup in this repository today contains
  two `StatusContext` entries whose `conclusion` is null and whose `state` is `SUCCESS`. The
  `.conclusion // .state // .status` projection is the difference between reporting them as
  unrecognised and reporting them correctly.
- **The handoff rule keeps the risk table honest.** Because F2 and F5 read the emitted section text,
  the table cannot quietly contradict the section above it — and the sequencing that makes this true
  is written down rather than assumed.
- **Nothing new to allow.** No sub-agent, no front-matter change, no settings change, no new tool.

### Negative

- **Inline review comment bodies are unreachable, and the command cannot fully tell.** Verified:
  `gh pr view --json reviews` returns empty bodies (confirmed against the live PR: `bodyLen: 0` for
  all three submissions), and `gh pr view --comments` renders only the fifteen issue comments, no
  inline threads. The command reads inline findings through the tracking comment's restatement. A
  round that posted inline comments and did *not* restate them would be counted as zero findings, and
  that is indistinguishable on the available evidence from a genuinely finding-free comment — the
  review-submission count is recorded as a corroborating check and a mismatch is a ledger row, but it
  is not a repair. Closing this needs `gh api`, which C-10 and Out of Scope both forbid. This is the
  one place where the allow-list costs the command real fidelity, and it should be stated rather than
  hidden behind the calibration case happening to work.
- **A review submission is not an inline comment.** The submission count counts review envelopes. On
  PR #4282 the `@claude` workflow posted three submissions carrying one comment each, so 3 = 3; a
  workflow that batched five inline comments into one submission would report 1. The corroboration is
  therefore weaker than it looks.
- **The skeleton is regex-shaped and inherits ADR 0072's format-drift exposure.** Heading levels already
  vary between rounds on the same PR (`### New findings` vs `## New findings`), item prefixes vary
  (`#### 1.`, `### 1.`, `1. **`), and a round that listed its findings as unnumbered bullets would fail
  the qualification test and be reported as zero rounds. NFR-3's "never report zero for a failed
  extraction" applies, but the command cannot detect this particular drift, because a review with no
  numbered items and a non-review comment look the same by design.
- **Running inline keeps the skeleton in context for the rest of the run.** ~10 KB on the calibration PR,
  on top of ADR 0072's Step 5 extracts, and it grows roughly linearly with review rounds: a PR with ten
  implementation rounds would be around three times this. Bounded, not free, and the point at which the
  delegation decision should be revisited is a PR whose skeleton alone approaches the size of the
  synthesis budget.
- **The three judgement points are genuinely unstable, and F3 moves with them.** "Closed for free by
  #1's fix" read as Open instead of Resolved would leave F3 at Medium here only by luck (an open `low`
  finding is also Medium); a differently-worded reply on a High-severity finding would move F3 between
  Medium and High, and with it FR-12's maximum. NFR-1 says the overall level may vary; this ADR narrows
  *where* it can vary without eliminating it.
- **F2 and F5 inherit their sections' errors silently.** Reading the emitted count lines is what stops
  the table contradicting the sections, but it also means a wrong `Total breaking-change items: {n}`
  produces a confidently wrong F2 with no second opinion anywhere in the procedure.
- **The command reads the fix-verdict section twice.** Once to exclude it (stage B.3) and once to mine
  it (stage H). That is correct but non-obvious, and it is exactly the kind of subtlety a later
  simplifying edit removes.

### Risks and Mitigations

**Risk**: A future edit reads `gh pr view --json comments` straight into context "just to be safe",
restoring the 69 KB payload and, with it, the case for delegation this ADR argued away.
- **Mitigation**: The command file states the skeleton-first rule as a named step with its own
  projection command, and this ADR records both byte figures so the regression is visible as a number
  rather than as a feeling. NFR-3's budget prose is the requirement that backs it.

**Risk**: Someone simplifies the round count to "distinct tracking comments by the review bot", which
reads as obviously equivalent and is not.
- **Mitigation**: Alternative 2 below records the arithmetic — that heuristic returns 9 on PR #4282,
  and its refined form returns 4 — and AC-50 tests all three failure modes (two-part grouping, zero
  rounds from task-completion comments, phase exclusion) as one criterion.

**Risk**: A severity word in body prose is matched and a round's split silently shifts. The live data
contains the trap: `…so low blast radius, but could produce flaky test failures if hit.`
- **Mitigation**: `Finding severity`'s title-only rule is quoted verbatim in the command file next to
  stage G, and AC-17's expectation of three *unclassified* findings in round 1 is the test that fails
  if body prose is ever matched.

**Risk**: The verdict section's residual concern (`⚠️ … replaces it with silence`) is counted as a new
finding, and round 2 reports six findings, changing FR-9's totals and possibly F3.
- **Mitigation**: `Finding`'s clause covers this explicitly — "never a source of a new finding, even
  where one such verdict item is qualified or flags a residual concern" — and it is quoted at stage
  B.3. The concrete check is that the concern reappears as `#### 1.` of the same comment's `New
  findings`, so counting both double-counts one issue.

**Risk**: F4 is read as `.conclusion` alone by a later edit, and `StatusContext` entries silently become
"unrecognised → Medium", masking a genuinely green rollup or a genuinely failing one.
- **Mitigation**: The three-way fallback is written into the command file with the reason beside it, and
  the rollup tally is required to sum to `.statusCheckRollup | length`, which an all-null projection
  would not.

**Risk**: A `High` level leaks into behaviour — a warning, an extra prompt, a different exit path —
because it reads as the responsible thing to do.
- **Mitigation**: FR-13, the `Advisory` definition and AC-25 all forbid it; this ADR adds the structural
  form of the rule (**no step may branch on the level**) so the prohibition is checkable by grepping the
  command file for a conditional mentioning the level, rather than by reasoning about intent.

## Alternatives Considered

### Alternative 1: Delegate the decomposition to a sub-agent

Hand the `Finding`/`Review round`/`Finding severity`/`Resolved`/`Acknowledged`/`Open` definitions
verbatim, plus the PR number, to a `general-purpose` sub-agent (the shape `/spec:review` uses) and have
it return the round tallies as text — the delegation ADR 0072 explicitly left open for this step.

**Rejected, on its own evidence rather than by inheriting ADR 0072's stance.** The premise was an
input/output asymmetry: 68,837 bytes in, a dozen integers out. Measured, the asymmetry does not survive
NFR-3's own discipline — the structural projection is 10,151 bytes, and once the input is that size the
sub-agent's *prompt* (roughly 1,500 words of definitions plus the skeleton) costs about what running
inline costs. The alternative shape, letting the sub-agent fetch for itself, spends the 69 KB
measurement twice, which is the same objection ADR 0072 raised against re-reading the NFR-3 budget.
Two further objections are specific to this step: NFR-7 and AC-32 require each tally to cite the
comment and heading it came from, so the return value is not "a handful of integers" but counts plus
most of their evidence; and the three judgement points are the least reproducible part of the command,
which is the worst possible thing to put behind a boundary where a wrong answer arrives as an assertion
with no visible reasoning. The README's stated benefit of delegation — a `Plan` sub-agent has no
file-editing tool — is empty for a read-only step, so nothing balances those costs.

### Alternative 2: Count review rounds by distinct tracking-comment timestamps

Treat each comment by the review bot as a round and each numbered item in it as a finding. Simple, fast,
entirely mechanical, and it would make F3 deterministic in NFR-1's sense.

**Rejected — and this is the alternative with the most concrete evidence against it, because it is what
the requirements review actually produced before six rounds of checking against the live PR corrected
it.** Run against PR #4282 today it returns **nine** rounds (nine comments authored by `claude`), because
six task-completion comments raise no findings and are not rounds. Filter to comments whose heading
contains "Review" and it returns **five**; group the two-part pass and it returns **four**; exclude the
specification-phase pass and only then does it return the correct **three**. Its finding count fails the
same way: round 2 becomes six (the verdict item) or eight (the `Smaller notes` bullets) instead of five,
and the design pass becomes ten instead of fourteen (the `### 10. Smaller items` container). Every one of
those wrong numbers was produced at some point during the requirements review and corrected against `gh`
output; the Definitions are the correction, and a heuristic that reproduces the errors is not a
simplification of them.

### Alternative 3: A weighted or averaged risk score instead of max-of-factors

Give the five factors weights, map Low/Medium/High to 1/2/3, and report a composite.

**Rejected, and this ADR owns the rejection rather than merely citing FR-12's.** Requirements states the
reason — "reproducible, explainable in one sentence, and cannot be gamed by averaging a `High` away" —
and the calibration case shows the shape of the failure: F1 High, F2 High, F3 Medium, with F4 and F5 as
measured, averages to something defensible-looking and materially below High, which is the wrong answer
for a 76-file change to `src/` carrying fourteen breaking-change items. Two arguments of this ADR's own:
the factors are **not commensurable** — "76 files under `src/`" and "one open Critical finding" are not
quantities on a shared scale, so any weighting invents an exchange rate that nothing in the requirements
justifies and that NFR-1 would then have to defend for stability across runs on top of everything else.
And **three of the five factors are already judged** (F2, F3, F5 per NFR-1); multiplying judged values by
invented weights compounds variance in the one direction — downward — that FR-12 forbids. The maximum
needs no exchange rate: it needs only the ordering `Low < Medium < High` that the factor table already
defines, and it preserves judgement in the single safe direction, raising with a stated reason.

### Alternative 4: Treat all findings equally and drop the severity split

Report `Round {i}: {n} findings — {r} resolved, {a} acknowledged, {o} open.` and drop the five-way
severity slot, which would remove stage G, the title-marker parsing and the body-prose trap entirely.

**Rejected because F3's High column is defined on severity.** "≥ 1 **open** finding of severity High or
Critical" is the only route to F3 High; without the split, F3 collapses to a two-level factor and an
open Critical finding scores the same as an open nit. It would also lose the `{m}`/`{u}` distinction
that FR-9 requires deliberately — "so a reader can see how much of a round's Medium weight came from an
actual severity call versus an absence of one" — which matters precisely because C-3 records that
neither of this repository's review workflows prompts for a severity word, so unclassified is the common
case rather than the exception. The cost being avoided is small in any event: the marker grammar is a
handful of shapes, all of them present and verified in the calibration data.

### Alternative 5: Derive resolution from commits alone, ignoring reply prose

Mark a finding Resolved when a commit after its posting date touches a file the finding names, and Open
otherwise. Fully mechanical; removes judgement point 3.

**Rejected because it cannot express `Acknowledged`, which is the distinction F3's Low/Medium boundary
turns on.** An acknowledged finding and an open one look identical from the commit log — neither has a
fixing commit. The calibration case is exactly this: round 2's findings 4 and 5 were answered "leaving
as-is" and "accepted for now, will revisit if it's actually hit in practice", and it is those two, not
any open finding, that put F3 in Medium rather than Low (AC-20). A commit-only rule would report them as
Open, which happens to give Medium here for the wrong reason and would give the wrong level on a PR
where an acknowledged finding carried a High marker. It would also miss round 2's finding 2, resolved by
another finding's fix with no commit of its own — the case `Resolved finding` names explicitly and NFR-1
cites as its worked example of judgement.

### Alternative 6: Count specification-phase passes as rounds and subtract them afterwards

Decompose every pass uniformly, then present implementation rounds and note the excluded ones as a
subtraction.

**Rejected** because FR-9 requires only a count (`Specification-phase review passes excluded: {n}`) and
Out of Scope forbids more: "Summarising or counting specification-phase reviews beyond the single
excluded-count line required by FR-9. `/spec:review` owns those." Decomposing a pass in order to discard
it also costs the most expensive part of the work — on PR #4282 the design pass is the fourteen-finding
one — for output nobody reads, and it puts specification-phase findings into the same ledger rows as
implementation findings, where a later edit can trivially let them leak into F3.

## References

- Requirements: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)
- Related ADRs:
  - [ADR 0072: Target Resolution and Output Shape for /spec:show-me](0072-show-me-command-resolution-and-output.md) — the sibling ADR this one extends. Its Steps 5 and 6 are the ones extended here; its Alternative 4 left this ADR's sub-agent question open, and Key Components 1 answers it.
  - [ADR 0071: Shiftable Review Gear for the TDD Approval Gate](0071-tdd-review-gear.md) — the only other ADR recording a decision about a `/spec:*` command's own behaviour rather than Brighter's runtime.
- Conventions and prior art in this repository:
  - [`.claude/commands/spec/README.md`](../../.claude/commands/spec/README.md) — the sub-agent and model policy weighed in Key Components 1; its stated rationale for delegating (`Plan` has no file-editing tool) and its rule that "the main agent gathers inputs."
  - [`.claude/settings.json`](../../.claude/settings.json) — the `gh` allow-list, re-verified as exactly `gh pr view`, `gh pr list`, `gh pr diff`, `gh issue view`, `gh issue list`; both JSON fields this ADR adds are `gh pr view` fields.
  - [`.agent_instructions/design_principles.md`](../../.agent_instructions/design_principles.md) — Responsibility-Driven Design; the Classifier is the "deciding" stereotype separated from ADR 0072's "knowing" Measurer, added because neither existing role could hold a responsibility that produces numbers by reading prose.
  - [`.agent_instructions/adr_frontmatter.md`](../../.agent_instructions/adr_frontmatter.md) — tag taxonomy and the slug-is-identity rule.
- External references: PR [#4282](https://github.com/BrighterCommand/Brighter/pull/4282) — spec 0036's pull request, the calibration case. Every figure in this ADR (the 68,837-byte comment payload and its 10,151-byte skeleton; the three-round / 3+5+3-finding / 9-resolved / 2-acknowledged / 0-open decomposition; the excluded 2026-08-28 design-only pass and its fourteen findings; the six finding-free task-completion comments; the 28-entry rollup with two `StatusContext` entries) was re-verified against it with `gh pr view` while drafting.
