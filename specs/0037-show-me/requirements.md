# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: N/A — originated from a wrap-up follow-up note in spec 0036 (`specs/0036-scoped-lifetime-per-pipeline/`), not a GitHub issue.

## Problem Statement

As the owner of a finished spec — and as the reviewer or teammate who has to form an opinion about
someone else's finished spec — I would like a single command that writes a durable, plain-language
summary of what the spec actually changed, together with a levelled, advisory read on how risky it
looks to merge, so that I can understand a spec's real shape and its merge risk without reading
seven ADRs, an 82-task `tasks.md` and 363 commit messages, and so that "what did this actually
build, and where should I look?" stops being an ad hoc judgement re-derived from scratch in every
session.

Think of the output as the end-of-sprint demo: *here is what we built, here is why it looks like
that, here is where to look*. It is explicitly **not** a review. Correctness, security, test
discipline, CI state and the outcome of the PR's own review rounds are assessed elsewhere — by
`/spec:review code` and by the review that already ran on the pull request — and this command does
not recount, re-derive or second-guess any of them.

Today the raw material exists but is scattered and asymmetric in cost:

- The **ADRs** (`docs/adr/NNNN-*.md`) hold the decisions but not the outcome — an ADR says what was
  decided, not whether it shipped as decided.
- **`requirements.md`** holds the numbered FRs/ACs the spec promised, but nothing reconciles the
  promise against what landed.
- **`tasks.md`** holds the ground truth of what was built (spec 0036's is 229 KB, 82 tasks), which
  is precisely the document nobody outside the implementation session will read.
- **`release_notes.md`** holds the breaking-change catalogue, but it is written for a *user* of the
  library, not for a reviewer of the change.
- The **structural shape of the change** — what talks to what, which call path moved, which type
  hierarchy is new — exists only in the diff, and a diff is exactly the artefact that hides shape
  behind volume.

The **PR review history and the CI checks tab** are deliberately *not* in that list. They already
live on the pull request, in a form a reviewer can read directly, and `/spec:review code` already
assesses them properly. Re-stating them here would duplicate two tools that own them and would turn
a demo into a second review.

Because nothing synthesises the rest, merge risk has been assessed implicitly and session-by-session:
each of spec 0036's review-response passes ended in an unstructured "what's left, is it safe?"
read, with no recorded factors, no comparable level, and no artefact anyone else could check.

## Proposed Solution

A new slash command, `/spec:show-me [spec-id]`, that sits alongside the existing `/spec:*` family
and runs **after** a spec's implementation is finished. It reads the spec's own artefacts (ADRs,
`requirements.md`, `tasks.md`), its git history, and — when it exists — its pull request's diff, and
writes one markdown file, `specs/NNNN-name/show-me.md`, containing:

1. **What changed and why** — a plain-language narrative a teammate can read cold, carrying a
   diagram when the change has a shape worth drawing.
2. **Breaking changes** — what a consumer of the library would have to do differently, or an
   explicit statement that there are none.
3. **Did it ship what it said?** — a plain statement of what shipped as planned, and every deviation
   named with its evidence.
4. **How it was built** — task shape and commit shape.
5. **Blast radius** — the numbers: files and lines changed, bucketed by area.
6. **Risk assessment (advisory)** — a **Low / Medium / High** level, the factors that produced it
   with their measured values, and the rationale.
7. **Where to look first** — the handful of files a reviewer should open.
8. **Inputs used** — exactly which inputs were available and which were not.

The risk assessment is **advisory only, never a gate**: it produces a level and a rationale, it says
so in its own output, and nothing about the command's behaviour changes with the level. A human
still decides whether to merge.

The file is a spec deliverable like any other: it lives in the spec directory, is tracked in git,
and is regenerated (overwriting in place) whenever the picture changes — for example after more
commits land on the spec branch.

## Requirements

### Definitions

These terms are used with exactly these meanings throughout this document.

| Term | Definition |
|------|------------|
| **Spec directory** | A directory (not a file) directly under `specs/`, named `NNNN-name`, where `NNNN` is a four-digit id. Note that ids are **not unique** in this repository (e.g. `specs/0002-backstop-error-handler/`, `specs/0002-sqs-cleanup/` and `specs/0002-universal_scheduler_delay/` all exist on `master`). Non-directory entries under `specs/` (`specs/README.md`, `specs/dlq-review-findings.md`, `specs/.current-spec`) are never spec directories. |
| **Target spec** | The spec directory the command resolves from its argument (FR-1) or from `specs/.current-spec` (FR-2). |
| **Task checkbox** | A line in `tasks.md` matching `^\s*-\s\[[ xX]\]`. Checked = `[x]` or `[X]`; unchecked = `[ ]`. |
| **Complete spec** | A target spec whose `tasks.md` exists, contains ≥ 1 task checkbox, and contains **zero** unchecked task checkboxes. |
| **Spec branch** | The git ref resolved by FR-10's ordered rules (e.g. spec 0036 → `refs/remotes/origin/spec/scoped-lifetime-per-pipeline`, preferred over the local `spec/scoped-lifetime-per-pipeline` when both exist). |
| **Base ref** | `origin/master` if it resolves, otherwise `master`. The chosen ref and its sha are always named in the output (FR-10). |
| **Merge base** | `git merge-base <base ref> <spec branch tip>`. |
| **Spec diff** | The single diff selected by FR-20's precedence rule: the PR diff (`gh pr diff {n}`) when a PR is discovered and the command succeeds, otherwise `git diff <merge base>..<spec branch tip>`. Exactly one source is used and named; the two are never mixed or averaged. |
| **Blast radius** | Four measured numbers over the spec diff: (a) changed files under `src/`, (b) changed files total, (c) net lines added/removed total, (d) changed **public API declaration lines** — see the row below. |
| **Public API declaration line** | A line of the spec diff, in a file under `src/`, that begins with `+` or `-` and declares a `public` or `protected` member. The exact pattern, its regex dialect and its counting rule are stated in full immediately below this table — they are given outside it because the pattern contains a `|` that a table cell cannot carry literally. |
| **Immediate subdirectory of `src/`** | For a changed path of the form `src/{X}/…` — that is, with at least one path segment after `{X}` — the contributed subdirectory is `{X}`. A changed path of the form `src/{F}` with **no** further segment (a file sitting directly under `src/`) contributes **nothing** to the subdirectory count, while still counting toward "changed files under `src/`". The repository has exactly one such file, `src/Directory.Build.props`, so this is a real case and not a hypothetical: a diff touching `src/Directory.Build.props` plus four files in one project directory changes 5 files under `src/` but spans **1** subdirectory, and FR-6's D1 test therefore does **not** fire. The count is of distinct `{X}` values, so two changed files in the same `{X}` contribute 1. |
| **Breaking change item** | One distinct consumer-affecting change, carrying **one or more** classifications from the vocabulary `release_notes.md` already uses: **source**, **binary**, **behavioural**, **compatibility**. Combined markers are normal (`release_notes.md` uses "source and binary" on 5 of spec 0036's 14 items), so the classification is a set, not an exclusive choice. |
| **Deviation entry** | One bullet in `## Did it ship what it said?` (FR-8) naming one top-level numbered requirement whose status is **not** `Shipped`, together with that status, its reason, its evidence and — for `Deferred`/`Dropped`/`Withdrawn` — its follow-up. Deviation entries are the only per-requirement items the section enumerates; requirements that shipped as planned are accounted for collectively by FR-8's shipped-as-planned line. |
| **Diagram** | One fenced code block inside `show-me.md` that draws a relationship in the change under review — a call or data flow, a component interaction, a lifecycle, or a type/file hierarchy. Either a Mermaid block (```` ```mermaid ````) or a plain fenced block holding an ASCII sketch or tree. Governed entirely by FR-6; permitted only in `## What changed and why` (FR-6) and `## Where to look first` (FR-14). A fenced block that is not drawing such a relationship — a quoted log line, a code excerpt, a command — is not a diagram and is not permitted by this document. |
| **Advisory** | Producing a level and rationale only: no gating, no blocking, no refusal, no label, no comment, no marker file, no change to any approval state, and no behavioural difference between a `Low` result and a `High` result. Whatever the level, the command completes its run and reports per FR-19. |
| **Measurement script** | The single executable artefact, delivered by this spec and living beside the command file at `.claude/commands/spec/show_me_facts.awk`, that performs every mechanically countable measurement this document defines and emits them as one JSON object on stdout (FR-21). It is part of the command, not an input to it: it is never an `## Inputs used` row (FR-15) and never an FR-16 absence row. |
| **Fact ledger** | The file `specs/{target spec directory}/.show-me-ledger.json`, holding the JSON the measurement script emitted for the run that wrote it. It is **working state, not a deliverable**: it is gitignored by an exact-match `.gitignore` entry, is never staged or committed, is never cited from `show-me.md` (FR-17, NFR-5), and is written only after the FR-3 precondition passes (FR-3, FR-4, FR-21). It holds measured values only; it never caches fetched diff text. |
| **Charged bytes** | The number of bytes of file or command output that a read brings into the command's context. A full-content read is charged its whole-file byte count; a targeted or chunked read is charged the byte count of the extract actually brought into context. Bytes that the measurement script reads but does not emit are **not** charged — only its JSON output is. `wc -c` is a size measurement, **not a read**, and is charged nothing. This is the unit NFR-3's budget is denominated in. |

**Public API declaration line — the exact rule.** A line of the spec diff qualifies when it is in a
file under `src/`, begins with `+` or `-`, and matches this **POSIX extended** pattern, evaluated as
`grep -E`:

```
^[+-][[:space:]]*(public|protected)[[:space:]]
```

The dialect is named because the pattern is not portable otherwise. Written for a *basic*-regex
`grep`, the parentheses and an escaped `\|` are read as literal characters and the pattern matches
neither declaration; and `\s`/`\b` are GNU extensions absent from the BSD `grep` that macOS ships.
Implementations must use the extended form above verbatim, inside a fenced block rather than a table
cell, because the alternation `|` cannot appear literally in a markdown table. The pattern is
implemented in exactly one place — FR-21's measurement script — and never transcribed into the
command file, a task, an ADR or a table cell, because a pattern carried in markdown prose acquires
that prose's escaping and a pattern written twice drifts into two patterns.

**Counting is per diff line, not per declaration.** A declaration that is *modified* rather than
added appears in the diff as one `-` line and one `+` line and therefore contributes **2**; a purely
added or purely removed declaration contributes **1**. This is deliberate — the number measures how
much public-surface text the diff moves, not how many distinct members changed — and it is why
FR-6's D2 threshold is expressed as 10 *lines* rather than 10 members. Every consumer of this number
(F1's sibling metric, D2's trigger, FR-10's mandatory report) reads this one definition, so the three
can never disagree.

### Functional Requirements

#### Invocation and target resolution

**FR-1 — The command accepts an optional spec identifier and resolves it deterministically.**
`/spec:show-me [spec-id]`. The `spec-id` is **the entirety of the command's argument text**
(`$ARGUMENTS`) with leading and trailing whitespace trimmed and any wrapping quotes removed — it is
**not** the first whitespace-delimited token, because this repository contains a real spec directory
whose name has spaces (`specs/0021-Expose Unacceptable Message Window/`). Internal whitespace is
preserved verbatim. Candidates for matching are **only directory entries directly under `specs/`**;
files such as `specs/README.md` and `specs/dlq-review-findings.md`, and dotfiles such as
`specs/.current-spec`, are never candidates.

When `spec-id` is given, the command resolves it against the names of the candidate directories
using this ordered rule set, stopping at the first rule that yields exactly one match:

1. Exact directory-name match (`0036-scoped-lifetime-per-pipeline`).
2. Exact four-digit id match (`0036`).
3. Case-insensitive substring match on the directory name (`scoped-lifetime`).

If a rule yields **more than one** match, the command stops without writing any file and prints:
`Ambiguous spec id '{arg}' — matches: {list of matching directory names}. Re-run with the full
directory name.` If no rule yields any match, the command stops without writing any file and prints:
`No spec matches '{arg}'. Run /spec:status to list specs.`
*Example (always available, `master` included)*: `/spec:show-me 0002` must be reported as ambiguous —
it matches `0002-backstop-error-handler`, `0002-sqs-cleanup` and `0002-universal_scheduler_delay`;
`/spec:show-me sqs-cleanup` must resolve to `specs/0002-sqs-cleanup/`. (The spec-0036 pair is a
second ambiguity case, but it only exists when the `spec/scoped-lifetime-per-pipeline` branch is
checked out or merged — see C-8. The `0002-*` example is used here precisely because it does not
depend on any feature branch.)

**FR-2 — With no argument the command targets the current spec, and says so when there isn't one.**
With no argument, the command reads `specs/.current-spec` and targets the spec directory it names.
If `specs/.current-spec` is missing, empty, whitespace-only, or names a directory that does not
exist under `specs/`, the command stops without writing any file and prints:
`No spec id given and no usable current spec (specs/.current-spec is {missing|empty|stale: names
'{value}'}). Pass a spec id (/spec:show-me 0036-scoped-lifetime-per-pipeline) or run /spec:switch
first.`

**FR-3 — The command runs only against a complete spec, and refuses clearly otherwise.**
Before producing any output, the command checks the target spec is a *complete spec* (see
Definitions). If not, it stops **without writing or modifying any file** — including leaving any
pre-existing `show-me.md` and any pre-existing *fact ledger* untouched, and creating neither — and
prints one of:

- `tasks.md` absent: `Spec {dir} has no tasks.md — /spec:show-me runs only against a finished spec.
  Current phase: run /spec:status.`
- `tasks.md` present but with zero task checkboxes: `Spec {dir}'s tasks.md contains no task
  checkboxes — nothing to summarise.`
- `tasks.md` present with ≥ 1 unchecked checkbox: `Spec {dir} is not finished: {n} of {total} tasks
  are still unchecked. First unfinished: {first three unchecked task titles, one per line}.
  /spec:show-me runs only against a finished spec.`

This precondition check is the only circumstance in which the command declines to produce output
**because of the spec it was pointed at**. It is a precondition on the *input*, not a judgement about
the change (contrast FR-13). There is exactly one other stop, and it is a precondition on the
*command's own tooling* rather than on the input: FR-21's measurement-script stop, which fires when
the script is absent, unreadable, exits non-zero, or emits output the command cannot parse. No third
stop exists, and no absent or degraded **input** ever stops the command (FR-16).

**Ordering — the gate runs above both writes.** The measurement script supplies the task-checkbox
counts this gate reads, so the script necessarily runs before the gate is evaluated. That is not a
write: the script emits its JSON to stdout and the *fact ledger* is written only once this gate has
passed. A run that stops here therefore leaves the repository byte-for-byte unchanged (NFR-8), with
no ledger created and no pre-existing ledger touched (AC-7, AC-71).

#### Output file

**FR-4 — The command writes exactly two files — the deliverable `show-me.md` and the gitignored
*fact ledger* — both in the target spec directory, and overwrites each in place on re-run.**

*The deliverable.* The path is `specs/{target spec directory}/show-me.md`, lower
case, no suffix or timestamp in the name. If the file already exists, the command **replaces its
entire contents**; it does not append, does not create a numbered variant (`show-me-2.md`), does not
write a backup, and does not refuse. Prior versions are recoverable from git history; the command
does not manage them. The command does not `git add` or commit the file.

*The fact ledger.* The path is `specs/{target spec directory}/.show-me-ledger.json` (Definitions). It
holds the JSON the measurement script emitted for this run (FR-21) and exists so that one run
resolves and fetches each remote input at most once rather than invoking `gh pr diff` a second time.
It is replaced wholly on re-run under exactly the same rules as the deliverable — no append, no
numbered variant, no backup — and is never staged, committed or pushed. It holds the measured JSON
only: it never caches fetched diff text, so its size stays bounded by FR-21's stdout cap.

*Why two files do not make a third state in git.* `.gitignore` gains the **exact-match** entry
`.show-me-ledger.json` — an exact filename, deliberately not a glob, because this repository already
carries one case-insensitivity gotcha from a wildcard pattern (`*.sqlite`) and a second is not worth
the saving. The entry matches the file at any depth, so every spec directory's ledger is ignored by
one line. The consequence is the one that matters: after a successful run `git status --porcelain`
differs from its before-state **only** by `specs/{spec}/show-me.md` (AC-30, AC-74), and NFR-8's
safe-re-run guarantee needs only the narrow carve-out it now states.

Exactly these two paths are written. No third file, no marker, no temporary file left behind.

**FR-5 — `show-me.md` has a fixed header and a fixed section set, in a fixed order.**
The file begins with an H1 `# Show me — {spec directory name}` followed by a metadata block stating,
each on its own line: generation date (ISO-8601), spec directory path, linked issue (from
`.issue-number`, or `none`), spec branch (the **full ref** actually used, per FR-10), head commit sha
(short), base ref and merge-base sha (short), and PR reference (number and URL, or `none found`).
The body then contains these H2 sections, in this order, with these exact headings, all of them
always present:

1. `## What changed and why` (FR-6)
2. `## Breaking changes` (FR-7)
3. `## Did it ship what it said?` (FR-8)
4. `## How it was built` (FR-9)
5. `## Blast radius` (FR-10, FR-20)
6. `## Risk assessment (advisory)` (FR-11, FR-12, FR-13)
7. `## Where to look first` (FR-14)
8. `## Inputs used` (FR-15)

No section is omitted when its input is missing; instead the section states the absence (FR-16).
There is no section for review history and none for CI state: both are out of scope (Out of Scope,
FR-9).

#### Section content

**FR-6 — `## What changed and why` is a synthesised plain-language narrative, carrying a diagram
when the change has a shape worth drawing.**
150–600 words of prose (not a bullet dump, not a copy-paste of ADR *Decision* sections) that states:
what the spec set out to fix, what a user of Brighter can now do that they could not before (or what
now behaves differently), and the one or two decisions that most shaped the result. It must name
**every** ADR listed in the spec's `.adr-list` at least once, each with its title and current Status
from the ADR's own front matter/body. Lines inside a fenced code block do not count toward the
150–600 range (they are diagram content, sized by (d) below and excluded from NFR-2's budget).

**ADRs are identified by filename slug (the filename stem), never by bare number.** ADR numbers are
*not* unique in this repository — `docs/adr/` on `master` alone carries fourteen duplicated numbers,
including five files numbered `0037` and four numbered `0057` (C-9) — so a reference of the form
`[ADR 0070]` is ambiguous and is forbidden. Each ADR reference must give its title and link
relatively to the ADR file, with the filename stem visible in the link text, e.g.
`[Per-pipeline DI scope for mapper and transform factories
(0070-per-pipeline-di-scope-for-mapper-and-transform-factories)](../../docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md)`.
The same rule binds every other ADR reference anywhere in `show-me.md` (FR-7's evidence, FR-8's
deviation entries, FR-15's `Inputs used` rows).

The narrative must not introduce an internal type name without a short gloss on first use.
*Example (spec 0036 — requires the calibration branch, C-8)*: the narrative must name the seven ADRs
`0070-per-pipeline-di-scope-for-mapper-and-transform-factories` through
`0076-scope-affinity-option-and-write-through` by slug and title, and must say, in ordinary words,
that `Scoped` mappers/handlers/transforms now share one DI scope per pipeline that is disposed when
the pipeline ends, and that an ASP.NET Core host can opt a pipeline in to adopting the ambient
request scope instead. Note that on that branch `docs/adr/` holds *two* files numbered 0070 and
*two* numbered 0071 — which is exactly why the slug, not the number, is the identifier.

##### Visual explanation

Prose is bad at shape. Where a picture explains the change faster than a paragraph, this section
carries a *diagram* (see Definitions). The rules below are the whole specification of that
capability, and they bind `## Where to look first` too (FR-14).

**(a) When a diagram is warranted — the trigger.** The trigger is evaluated only when a spec diff
was measured (FR-10). A diagram is **warranted** when at least one of these holds:

| # | Test | Fires when |
|---|------|------------|
| D1 | Spread of product-code change | The spec diff changes **≥ 5** files under `src/` **and** those files span **≥ 2** distinct immediate subdirectories of `src/` (Definitions — a file directly under `src/`, such as `src/Directory.Build.props`, counts toward the file count but contributes no subdirectory) |
| D2 | New or altered public surface | The spec diff changes **≥ 10** public API declaration lines (Definitions, *Blast radius* (d)) |
| D3 | Multiple shaping decisions | `.adr-list` names **≥ 2** entries that resolved to a file in `docs/adr/` (FR-16 row 7) |

All three values are already measured for `## Blast radius` (D1, D2) and `## Inputs used` (D3), so
deciding the trigger needs no extra measurement and two runs over the same inputs reach the same
decision (NFR-1). When a test fires, the file must carry a diagram — in `## What changed and why`
unless the only relationship worth drawing is the reviewer's-starting-files tree that FR-14 already
permits, in which case placing it in `## Where to look first` instead satisfies the trigger — unless
(e) applies.
*Example (spec 0036 — C-8)*: D1 fires (76 files under `src/`, spanning six immediate subdirectories
of `src/`) and D3 fires (seven ADRs), so a diagram is warranted.

**(b) The raise, and its mirror image, the stand-down.** When **no** test fires, the command **may**
still draw one diagram, provided it gives an explicit one-sentence reason naming the relationship the
prose cannot carry (e.g. "the change reorders three await points inside one method and the order is
the whole fix"). Symmetrically, when a test **does** fire but every value it measured turns out, on
inspection, to describe unrelated or incidental changes with no shared call path, flow, hierarchy or
component relationship to draw — e.g. D2 fired on ten added properties across ten unrelated,
otherwise-unconnected DTOs — the command may stand down, provided it gives an explicit one-sentence
reason naming why the fired test's evidence does not cohere into a drawable relationship. Both the
raise and the stand-down are judgement-derived and NFR-1 says so; what NFR-1 does *not* permit
varying is whether a test fired in the first place (that stays mechanical). A stand-down is never a
silent skip: it is answered by (e)'s **stand-down line**, exactly as a fired-and-answered test is
answered by one of (e)'s other lines.

**(c) What is drawn, and in which format.** A diagram shows a relationship in the change under
review. It never decorates, never restates the blast-radius numbers, and never draws a structure the
command has not read. Format follows the kind of relationship:

- a **sequence, call flow, or state/lifecycle** → a Mermaid fenced block (```` ```mermaid ````),
  which GitHub renders natively in markdown;
- a **file, type or namespace hierarchy** → an ASCII tree in a plain fenced block, which renders
  identically in every viewer;
- a **component or box-and-arrow sketch** → either, at the command's discretion. This one choice is
  judgement-derived (NFR-1); the first two are determined by the kind.

Every symbol, type, file or component named in a diagram must be attributable to a listed input
under NFR-7, exactly as a prose claim is: it appears in the spec diff, in an ADR named in
`.adr-list`, or in a file the command read within NFR-3's budget. A diagram must not name a type the
command has not read. Paths named in a diagram are paths written into `show-me.md` and are bound by
FR-17 (tracked-in-git only) and NFR-5.

**(d) Size and count.** At most **two** diagrams in the whole file: at most one in
`## What changed and why` and at most one in `## Where to look first`. Each fenced block is
**≤ 40 lines** including its opening and closing fences, and **≤ 100 characters** per line. These
are the only size rules that apply to diagram content — fenced-block lines are excluded from NFR-2's
word budget (NFR-2 (d)) and from FR-6's 150–600-word prose range, and a diagram consumes none of
FR-14's 3–7 path slots. A relationship that will not fit in 40 lines is being drawn at too fine a
grain: cut detail, or draw the narrower relationship.

**(e) When no diagram is drawn — defined fallbacks.** `## What changed and why` must carry exactly
one of these lines whenever it contains no diagram (including when the fired trigger's diagram was
placed in `## Where to look first` instead, per (a) — that case uses **the placed-elsewhere line**
below, naming where the diagram actually is). The command never omits both the diagram and the line, and never
draws a guessed diagram in place of one. **Each line below is named, and every cross-reference to
these lines — here, in (b), in (f), in FR-16 and in the acceptance criteria — uses that name, never
an ordinal.** A line's position in this list carries no meaning and must never be cited: inserting a
line would silently invalidate every ordinal reference elsewhere in this document, which is a defect
this naming exists to make impossible.

- **the no-trigger line** — no test fired and the command did not exercise (b)'s raise:
  `No diagram: {a} files changed under src/ across {b} director{y|ies}, {c} changed public API
  declaration lines, {d} ADRs — no structural relationship to draw.` with `{a}`, `{b}`, `{c}`, `{d}`
  the measured values the trigger tested.
- **the stand-down line** — a test fired but the command exercised (b)'s stand-down, because the
  evidence did not cohere into a drawable relationship:
  `No diagram: {which test(s)} fired, but {one-sentence reason the evidence does not cohere into a
  relationship}.`
- **the placed-elsewhere line** — a test fired, the relationship was drawable, and the drawn diagram
  was placed in `## Where to look first` instead (per (a)):
  `No diagram here: the change's shape is drawn as a path tree in ## Where to look first.`
- **the budget line** — a test fired but NFR-3's read budget, including its 100,000-byte reserve for
  these reads, was exhausted before the command could read enough source to draw the relationship
  accurately:
  `No diagram: the read budget was exhausted before the relationship could be read accurately.`
- **the no-diff line** — no spec diff was measured (FR-16 row 12):
  `No diagram: spec branch not determinable, so no change could be drawn.`

`## Where to look first` carries no such line **except** when it is the section actually carrying the
diagram that a `## What changed and why` trigger fired for (**the placed-elsewhere line** above) — otherwise an
absent tree there needs no explanation, since FR-6's line is the file's single statement about
diagrams for the ordinary case.

**(f) Budget.** Any source-code reads needed to draw a diagram accurately are charged to NFR-3's
single whole-run byte budget, and are the only reads permitted to draw on its 100,000-byte reserve.
That budget is **per run, not per diagram**: a run that draws two diagrams shares one reserve between
them and stays inside one 1,048,576-byte total across the entire run. There is no separate diagram
budget and no per-diagram allowance. Before each such read the command checks the file's size with
`wc -c` (NFR-3) and does not open a file the remaining bytes will not cover. Exhausting the budget
produces (e)'s **budget line**, never a partially-read guess.

**FR-7 — `## Breaking changes` enumerates consumer-affecting changes, each classified, or states
there are none.**
The section derives its content from the spec's **own** artefacts — the ADRs' *Consequences*
sections, `requirements.md`, and the public-API declaration lines in the spec diff. It lists each
*breaking change item* as one bullet, **≤ 40 words**: a one-sentence statement of what breaks, its
classification — **one or more** of source / binary / behavioural / compatibility, stated as a set
(e.g. "source and binary", "behavioural, and source and binary") — and the migration in one sentence.
It ends with a count line: `Total breaking-change items: {n}`. If there are none, the section
contains exactly the line `No breaking changes identified for this spec.` and the count line `Total
breaking-change items: 0`.

`release_notes.md` is never *required to exist*, never modified, never a prerequisite, and a link to
it is never required (see FR-17 and Out of Scope). But **when a section for this spec does exist, the
command must read it** — the read is not optional. Presence and reading are therefore the same
state, which is what lets FR-16 row 5, FR-15 and the tie-break below each be written against a single
condition instead of two, and what makes F2 deterministic between runs (NFR-1): two runs over the
same tree cannot reach different item counts by one of them declining to read. Having read it, the
command corroborates the list and — if the counts disagree — must say so in one line
(`release_notes.md records {m} items; this summary identifies {n}`).

The read is bounded like every other: it is a targeted extraction of the spec's section, charged in
bytes against **NFR-3's** budget, with the file's size measured by `wc -c` first. If the bytes
remaining do not cover it when the section is reached, the section is **not** read, and this is the
one case in which a present section goes unread — it is recorded in `## Inputs used` as
`not available: read budget exhausted before release_notes.md section could be read` (a distinct
reason string from row 5's absence case), the FR-16 row 5 line is **not** emitted (the section is
present; saying it was not found would be false, which NFR-7 forbids), and the tie-break below falls
back exactly as it does when no section exists. No other circumstance permits a present section to go
unread.

*Example (spec 0036 — C-8)*: fourteen items, including `MapperLifetime.Scoped` no longer caching for
the life of the process (behavioural); `CreatePipelineScope()` added to six mapper/transformer
factory-and-registry interfaces in one item (source and binary); `IAmAHandlerFactory` gaining
`CreatePipelineScope()` **and**, in the same item, `IAmALifetime` gaining a `PipelineScope` member
(source and binary); and, as a separate item of its own, `IAmALifetime` also implementing
`IAsyncDisposable` (source and binary).

**When a `release_notes.md` section for the spec is read — which, per FR-7 above, is whenever one
exists and the read budget allowed it — its grouping is the tie-break
for what counts as one item** — the command follows the catalogue's own bullet boundaries rather than
re-partitioning them, which is why the calibration example above groups `CreatePipelineScope()` and
`PipelineScope` into one item (the catalogue reports them in one bullet) while keeping
`IAsyncDisposable` separate (the catalogue gives it its own bullet). When no `release_notes.md`
section is read — because none exists (FR-16 row 5) or because the read budget was exhausted
(FR-16 row 5a) — there is no catalogue to defer to, and the rule falls back to: one
item per distinct public-API declaration change or per ADR *Consequences* bullet describing a
behavioural break.

**FR-8 — `## Did it ship what it said?` states what shipped as planned and enumerates every
deviation.**
The section is prose and bullets, not a per-requirement audit table: a reader wants the exceptions,
not twenty-eight rows confirming that the ordinary happened. It has exactly four parts, in this
order.

*Which ids are in scope (the counting rule, unchanged)*: a numbered requirement is an id matching
`\b(FR|NFR)-(\d+)\b` that is **declared** in the spec's `requirements.md` — i.e. it appears at the
start of a requirement's heading or bold lead-in (`**FR-7 — …**`), not merely cross-referenced in
another requirement's prose. Call the resulting set the *declared ids* and its size `{total}`.

*Sub-numbered requirements*: a sub-numbered clause (`FR-27.3`, `NFR-1.2`) is not in scope on its own.
It is folded into its top-level number (`FR-27`, `NFR-1`). When sub-clauses of one requirement have
different outcomes, the top-level id takes the least-shipped status among them, using the precedence
`Shipped` < `Shipped with deviation` < `Unverifiable` < `Deferred` < `Withdrawn` < `Dropped`, and —
if that status is not `Shipped` — its deviation entry must name which sub-clause differs and address
each sub-clause in its reason and evidence.

*Status set*: status ∈ {`Shipped`, `Shipped with deviation`, `Deferred`, `Dropped`, `Withdrawn`,
`Unverifiable`}.

- `Withdrawn` — the requirement was explicitly removed or superseded by a **recorded decision** taken
  during the spec's own lifecycle (recorded in `requirements.md` itself, an ADR, a task, or the PR
  review thread), before or during implementation: it was never meant to ship in its stated form. A
  `Withdrawn` entry must cite where the withdrawal is recorded.
- `Dropped` — not built, with no recorded decision to withdraw it. (This is the distinction: a
  withdrawal is a decision; a drop is an absence.)

**Part 1 — the shipped-as-planned line.** Exactly one line, in this form:
`Shipped as planned: {k} of {total} numbered requirements — {id list}.`
`{id list}` names every declared id whose status is `Shipped`, in the order `FR-1 … FR-n` then
`NFR-1 … NFR-n`, comma-separated, with collapsing governed by these two rules: (i) two ids
`{prefix}-i` and `{prefix}-j` in the list are **consecutive** only when `j = i + 1` **exactly** —
this is pure integer adjacency and does not look at whether any intervening number is declared,
so it never invents a reference to an id absent from the declared-id set (e.g. declared
`{FR-1, FR-2, FR-3, FR-5}`, `FR-4` never declared, all four `Shipped`: `FR-3` and `FR-5` are **not**
consecutive, because `5 ≠ 3 + 1`, so the run breaks there regardless of `FR-4`'s absence); (ii) a
maximal run of consecutive ids is written as `{first}–{last}` **only when it spans three or more**
ids — a run of exactly two consecutive ids is written out in full, comma-separated, and is never
collapsed to a range. When `{k}` is 0 the list is the single word `none`.
*Examples*: declared `{FR-1, FR-2, FR-3, FR-5}`, `FR-4` never declared, all `Shipped` →
`FR-1–FR-3, FR-5` (the run stops at `FR-3` because `FR-4` is not in the list, per rule (i); `FR-5` is
reported on its own because a run of one is never a range). Declared
`{FR-1, FR-2, FR-3, FR-5, FR-6}`, all `Shipped` → `FR-1–FR-3, FR-5, FR-6` (the trailing run of two,
`FR-5, FR-6`, is written out rather than collapsed, per rule (ii)'s three-id minimum).
This line is what "stating what shipped as planned" means: it is one line, it is complete, and it is
mechanically expandable back to exactly the declared ids that are `Shipped` — never to an id absent
from the declared-id set — which is what makes the partition invariant below a property a test can
actually assert.

**Part 2 — the deviations.** One *deviation entry* (see Definitions) per declared id whose status is
not `Shipped`, in the same id order, each a single bullet stating: the id; a one-line paraphrase of
what it asked for; the status word in bold; a one-sentence reason for the deviation; evidence (a task
id from `tasks.md`, a file path, or an ADR reference by slug); and, for `Deferred`/`Dropped`/
`Withdrawn`, either a follow-up issue number, the id of the requirement that supersedes it, or the
literal `no follow-up recorded`. If there are no deviations, Part 2 is exactly the line
`No deviations: every numbered requirement shipped as stated.`

**The partition invariant.** Every declared id appears **exactly once** across Parts 1 and 2 — either
in the shipped-as-planned line's id list or as one deviation entry, never both and never neither.
This replaces the old table's 100%-coverage rule and is the property a test asserts.

**Part 3 — `Shipped beyond the requirements`.** A short list naming work present in `tasks.md` that
no numbered requirement covers, or the line `Nothing shipped outside the numbered requirements.`

**Part 4 — the count line.**
`Shipped: {a} · Shipped with deviation: {b} · Deferred: {c} · Dropped: {d} · Withdrawn: {w} ·
Unverifiable: {e} (of {total})`, where `{a}` equals Part 1's `{k}`, `{b}+{c}+{d}+{w}+{e}` equals the
number of deviation entries, and the six terms sum to `{total}`.

**FR-9 — `## How it was built` states the task shape and the commit shape.**
It states two things and nothing else: total tasks and the count per task-type tag found in
`tasks.md` (`TEST + IMPLEMENT`, `STRUCTURAL`, `PROJECT`, `DOC`, plus `untagged`); and the number of
commits on the spec branch since the merge base. Both are mechanically counted (NFR-1). They are
here as churn context — how much work, in how many pieces — not as an assessment of it.

When the spec branch is not determinable (FR-16 row 12) there is no merge base to count from: the
commit count is reported as the line `Commits: not determinable — spec branch not resolved.` and the
task shape is still reported normally.

**This section reports no review history and no CI state.** Findings, severities, resolution state,
review rounds, and check conclusions are not summarised here or anywhere else in `show-me.md`. The
pull request is the record of its own review, the checks tab is the record of its own CI, and
`/spec:review code` is the tool that assesses the code. Recounting any of them here would duplicate
two tools that already own them (Out of Scope).

*Example (spec 0036 — C-8)*: 82 tasks with their per-tag breakdown, and 363 commits since the merge
base.

**FR-10 — `## Blast radius` reports measured numbers, bucketed, with the exact refs it measured
against.**
The command resolves the spec branch using this ordered rule set, stopping at the first that
succeeds: (1) a branch named `spec/{spec directory name with the leading `NNNN-` removed}` —
**when both a remote-tracking and a local branch of that name exist, the remote-tracking branch
(`refs/remotes/origin/spec/…`) wins**, because it is the last-pushed state a PR reviewer sees and the
state a discovered PR's diff reflects; (2) the currently checked-out branch, if its name contains
that same name part; (3) `HEAD`, if any commit reachable from `HEAD` but not from the base ref touches
`specs/{spec directory}/`; otherwise the branch is **not determinable** and FR-16 applies. The base
ref follows the same preference for the pushed state: `origin/master` when it resolves, otherwise
`master`.

The section renders its bucket breakdown as a **pipe table** — one row per bucket
(`src/`, `tests/`, `docs/`, `specs/`, `.github/`, `other`), columns for files changed and net lines
(`+a/−b`) — **all six buckets are always listed, including any that are zero, and the six file
counts must sum to the reported total**, with a totals row. Table rows are excluded from NFR-2's word
budget (NFR-2 (b)), which is deliberate: these are measured numbers, not prose, and rendering them as
a table keeps them out of the section's word count exactly as `## Risk assessment (advisory)`'s
factor table (FR-11) already does. Outside the table, as plain prose lines (which **do** count toward
NFR-2), the section states: the count of changed public API declaration lines; the number of distinct
immediate subdirectories of `src/` the diff touches (Definitions, *Immediate subdirectory of
`src/`* — a file directly under `src/` contributes to neither this count nor D1's second clause),
because FR-6's D1 test is defined over it and
every value a trigger tests must be visible to a reader checking the trigger; and the commit count
is **not** restated here — it is reported once, in `## How it was built` (FR-9), and this section
does not duplicate it.

It also states, on one line each, exactly what was measured, so two runs that disagree can be
diagnosed:

- `Measured from {gh pr diff #N (head {sha}) | git diff {merge-base sha}..{head sha}}` (one source
  only, per FR-20).
- `Ref used: {full ref, e.g. refs/remotes/origin/spec/scoped-lifetime-per-pipeline} at {sha}; base
  ref {origin/master|master} at {sha}; merge base {sha}.`
- When rule 1 selected a remote-tracking branch and a local branch of the same name exists at a
  different sha: `Local branch {name} is at {sha} and differs from the measured ref.`

The bucket breakdown is mandatory rather than a single total, because a spec's own paperwork
dominates the raw total: spec 0036's branch shows 517 files changed, of which only 76 are under
`src/`.

**FR-11 — The risk level is computed from three named factors, each with stated thresholds and its
measured value cited.**
The section contains a table with one row per factor: factor, measured value, factor level
(`Low`/`Medium`/`High`). The factors and thresholds are:

| # | Factor | Low | Medium | High |
|---|--------|-----|--------|------|
| F1 | **Product-code blast radius** — files changed under `src/` | ≤ 10 | 11–50 | > 50 |
| F2 | **Breaking changes** — count of breaking change items (FR-7) | 0 | 1–3 | ≥ 4 |
| F5 | **Requirement fidelity** — from FR-8's reconciliation | no deviation entries (every declared id is `Shipped`) | ≥ 1 `Shipped with deviation` or `Unverifiable` entry; **or** ≥ 1 `Deferred`/`Dropped`/`Withdrawn` entry that **does** state a follow-up issue or superseding requirement (i.e. not `no follow-up recorded`) — this includes a `Withdrawn` entry that cites both the withdrawal decision and a follow-up/supersession | ≥ 1 `Deferred`, `Dropped`, or `Withdrawn` entry stating `no follow-up recorded` |

**The numbering is deliberately non-contiguous.** Two earlier factors were removed when review
history and CI state left this command's scope: **F3** (state of PR review findings) and **F4** (the
PR's CI rollup). Their identifiers are **retired and must not be reused**, so that `F5` keeps the one
meaning it has ever had in this document and in the implementation built against it. Any
`show-me.md` containing an `F3` or `F4` row is non-conforming.

F5's column mapping is total: **any** id that is not `Shipped` — including a `Withdrawn` entry that
cites both its decision and a follow-up/supersession — lands in Medium if it states a follow-up issue
or superseding requirement, alongside `Shipped with deviation`/`Unverifiable`; it is not High,
because High is reserved for a gap that is *unrecorded* (`no follow-up recorded`); and it is not Low,
because Low requires the deviation list to be empty, with no exception.
*Example (AC-45)*: `FR-27`'s entry is `Withdrawn`, citing both the withdrawal decision and a
superseding requirement — that entry alone puts F5 in `Medium`, not `Low` and not `High`.

**When more than one of a factor's three column conditions is satisfied *collectively* by the
evidence that factor measures — the full set of deviation entries for F5, the full item list for F2 —
the factor takes the *highest* matching column** (High beats Medium beats Low). For example: one
`Deferred` entry with a recorded follow-up alongside a separate `Dropped` entry stating
`no follow-up recorded` puts F5 at High. This one rule is what makes every factor's mapping total
over its full body of evidence, not just against a single entry considered on its own.

Each row must cite the value it measured (e.g. `76 files under src/`, `14 items`, `2 deviation
entries of 28 requirements: 1 Shipped with deviation, 1 Withdrawn with a superseding requirement`),
not merely the level.

**FR-12 — The overall level is the highest factor level, and may be raised but never lowered.**
The overall level is the maximum of the three factor levels (`Low` < `Medium` < `High`). The command
may state a **higher** overall level than the maximum if it gives an explicit one-sentence reason
naming what the factors miss; it may **never** state a lower one. The section states the level on
its own line in the form `**Overall risk: {Low|Medium|High}**`, followed by 2–5 sentences of
rationale that reference at least the factor(s) that set the level.

*Example (spec 0036 — C-8)*: F1 `High` (76 `src/` files), F2 `High` (14 breaking-change items), F5 as
measured from the reconciliation → **Overall risk: High**, set by F1 and F2 as the maximum. Whatever
F5 measures cannot lower that, because the overall level is the maximum of the three factors, not
their average.

**FR-13 — The risk assessment is advisory, says so in the output, and changes nothing about the
command's behaviour.**
The `## Risk assessment (advisory)` section must contain this sentence verbatim:
`This assessment is advisory only. It is not a merge gate; the merge decision stays with a human
reviewer.`
The command's behaviour must be identical regardless of the level: once the FR-3 precondition passes
it always writes the file, always completes its run and reports per FR-19 (path written, created or
replaced, level, advisory reminder) with **no error message and no refusal**, and never blocks,
warns-and-stops, sets a marker file, applies a label, posts a comment, requests changes, or alters
any approval state. A `High` result and a `Low` result differ only in the text written.

**FR-14 — `## Where to look first` names the files a reviewer should open.**
When a spec diff was measured: three to seven paths from the spec diff, ordered most-important first,
each with a one-line reason (≤ 25 words) for why it matters. Every path listed must exist in the spec
diff. If the spec diff touches no files under `src/`, the list is drawn from whatever the diff does
touch and says so. When **no** diff was measured (spec branch not determinable, FR-10), the section
takes FR-16's fallback text instead of a path list.

*Optional tree.* This section may additionally carry **one** diagram under FR-6's visual-explanation
rules — in practice an ASCII tree or sketch showing how the listed paths relate to each other (which
file calls which, or where each sits in a type or namespace hierarchy). It is bound by FR-6 (c)'s
format and attribution rules, (d)'s caps (≤ 40 lines, ≤ 100 columns, and the whole-file maximum of
two diagrams), and (f)'s per-run read budget. It **does not consume a path slot**: the 3–7-path rule
is counted over the path entries only. A node in the tree that is not in the spec diff may appear for
context and must be marked `(unchanged)`, which is the one exception to this requirement's
"every path listed must exist in the spec diff" rule and exists so a diagram can show the caller that
did not move. When this section carries no diagram it says nothing about that — FR-6 (e)'s line is
the file's single statement on the subject.

**FR-15 — `## Inputs used` records provenance for every input, present or absent.**
A table with one row per input — `requirements.md`, `tasks.md`, `.adr-list` (and one row per ADR it
names, identified by slug), `.issue-number`, `release_notes.md` section, git history, pull request,
and one row per source file read in full for a diagram (FR-6) — each marked `used` or
`not available: {one-line reason}`. "Present but not read" is not a third state: per FR-7 a present
`release_notes.md` section is always read unless the read budget was exhausted, and that one case is
a `not available:` reason like any other (FR-16 row 5a), so every row resolves to exactly one of the
two marks. There is no row for review comments and no row for CI checks:
neither is an input to this command. There is likewise **no row for the measurement script and no row
for the fact ledger** (Definitions): both are parts of the command itself rather than inputs to it,
the ledger is untracked by design, and FR-17 forbids `show-me.md` naming an untracked path at all —
so a ledger row could not be written without making the file dangle for every other reader. This
section is what makes FR-16's degradation auditable, and it
is excluded from the length budget in NFR-2.

#### Source discovery

**FR-20 — The spec's pull request is discovered by a stated rule, and exactly one diff source is
chosen and named.** *(Numbered FR-20 to keep the existing FR-1…FR-19 ids stable; it belongs
logically with FR-10.)*

*PR discovery*: the command runs `gh pr list --head {spec branch name with any remote prefix
stripped} --state all --json number,url,headRefName,createdAt` and keeps only results whose
`headRefName` equals that branch name exactly.

- Exactly one result → that is the spec's PR.
- More than one result → the PR with the **highest number** wins (PR numbers are monotonic with
  creation, so this is the most recently opened), and `## Blast radius` records
  `{k} pull requests found for branch {branch}; using #{n} (highest number).`
- No result, or `gh` unavailable/unauthenticated/offline → no PR; FR-16's corresponding row applies.

`.issue-number` is **never** used for PR discovery: it names the spec's tracking *issue*, not its PR
(spec 0036's `.issue-number` is 4256 while its PR is #4282). It feeds only the metadata block's
linked-issue line (FR-5).

*Diff source precedence*: when a PR is discovered **and** `gh pr diff {n}` succeeds, the PR diff is
the spec diff. Otherwise the spec diff is `git diff {merge base}..{head sha}`. The PR diff wins
because it is stable while local `master` advances, and it is the diff a reviewer is actually
looking at. Exactly one source is used; the command never reports two sets of numbers, and
`## Blast radius` must name the chosen source and its parameters per FR-10.

The PR is used for two things only: its number and URL in the metadata block, and its diff. Nothing
else on the PR — comments, reviews, labels, checks — is read (FR-18).

#### Measurement

**FR-21 — Every mechanically countable value is produced by one delivered script that emits JSON,
and the command consumes that JSON rather than recomputing anything.** *(Numbered FR-21 as the next
free id after FR-20; it belongs logically with FR-10 and NFR-1.)*

*The split.* The script **measures**; the command file **synthesises**. Nothing mechanically
countable is computed in the command file, and nothing judgement-derived is computed in the script.

*The artefact.* One awk program, `.claude/commands/spec/show_me_facts.awk`, invoked
`awk -f .claude/commands/spec/show_me_facts.awk <spec directory>` from the repository root — the same
language and the same invocation shape as this repository's existing executable artefact in this
family, `.claude/commands/adr/generate_adr_index.awk` (NFR-6, C-10).

*What the script owns.* Exactly the fields NFR-1 lists as mechanically countable, plus the inputs
those fields depend on: spec-branch and base-ref resolution and their shas including FR-10's
local-versus-remote-tracking divergence line; PR discovery under FR-20 and the chosen diff source;
`## Blast radius`'s six bucket file counts and net lines and their total; the distinct-immediate-
subdirectory-of-`src/` count; the changed public API declaration line count; the commit count since
the merge base; `tasks.md`'s checkbox total, checked count, unchecked count, first three unchecked
titles and per-tag counts; FR-8's *declared id* set and `{total}`; `.adr-list` entry resolution; and
whether a `release_notes.md` section for the spec exists.

*One implementation of every stated pattern.* The **Public API declaration line** rule's POSIX
extended pattern (Definitions) and FR-8's declared-id regex are each implemented **once**, in this
script, and nowhere else in the delivered artefacts. This is the whole point of the requirement: a
pattern written into markdown prose acquires the prose's escaping, and a pattern written more than
once drifts. Neither failure is detectable by reading the command file.

*The contract.* The script writes **one JSON object to stdout and nothing else** — no progress text,
no warnings interleaved — carrying at minimum a `schema_version` integer and one named field per
value above, with a null (never a zero, never an omission) for any value that is not determinable.
Its stdout must be **≤ 65,536 bytes**, which is what makes it affordable to read under NFR-3 without
a pre-check. **This figure is a chosen cap, not a measurement**: no ledger has yet been generated, so
there is no observed size to calibrate against, and the cap is set roughly an order of magnitude
above the expected low-single-digit-KB object precisely so that it binds on a defect rather than on
normal output. It exits `0` when it produced a parseable object and non-zero otherwise.

*The ledger.* Having emitted that object, and **only once the FR-3 precondition has passed**, the
same object is written to the *fact ledger* (FR-4). This is what lets one run resolve and fetch each
remote input at most once (FR-18): `gh pr list` and `gh pr diff` are each invoked at most once per
run. The ledger holds these measured values only and never caches fetched diff text. A stop under
FR-1, FR-2 or FR-3 writes no ledger and modifies no existing one.

*Modes.* The script is invoked at most twice per run: once before the deliverable is written, to
produce the facts above; and once after it is written, to apply NFR-2's mechanical word-count rule to
the generated `show-me.md` and report the counted total, the number of excluded fenced-block lines,
and whether the total is inside the 400–2,000 range. The second invocation reads and writes nothing
else and does not touch the ledger.

*Offline.* With no network and no `gh` the script still exits `0` and still emits a complete object,
with the PR fields null and a stated reason, so NFR-4's guarantee is unaffected.

*The one failure mode, with defined text.* If the script is absent, is unreadable, exits non-zero, or
emits output the command cannot parse as a single JSON object, the command **stops without writing or
modifying any file** — no `show-me.md`, no ledger — and prints exactly:
`/spec:show-me could not run its measurement script ({path}): {absent|unreadable|exited {code}|output was not a single JSON object}. No file was written. This is a tooling fault, not a fault in spec {dir} — re-run after restoring the script.`
The command **must not** fall back to computing these values inline, and **must not** write a
partial, guessed or zero-filled `show-me.md`. A missing measurer is not a missing input: FR-16's
"no absence fails the command" governs the spec's inputs, and the script is part of the command, so
it has no FR-16 row and no `## Inputs used` row (FR-15).

#### Degradation, provenance and safety

**FR-16 — Every optional input has a defined absence behaviour; no absence fails the command.**
When the FR-3 precondition holds, the command always produces `show-me.md`. Absent inputs are handled
exactly as follows (each row has a matching acceptance criterion):

| # | Absent input | Behaviour |
|---|---|---|
| 1 | No PR found for the spec branch (FR-20) | The metadata block's PR reference reads `none found`; `Inputs used` marks the pull-request row `not available: no PR found for branch {branch}`; blast radius is measured from `git diff` (FR-10, FR-20). No factor consequence. |
| 2 | `gh` unavailable, unauthenticated, or offline | Same as row 1, with the reason recorded in `Inputs used` as `not available: gh unavailable`. Blast radius still measured from git. No factor consequence. |
| 5 | No `release_notes.md` section for the spec — the section is **absent**, not merely unread (FR-7) | `Breaking changes` is derived from ADRs and the diff, and adds the line `No release_notes.md section found for this spec; this list is derived from the ADRs and the diff.` No failure, no modification of `release_notes.md`. |
| 5a | A `release_notes.md` section for the spec **exists** but NFR-3's read budget was exhausted before it could be read (FR-7 — the only case in which a present section goes unread) | `Breaking changes` is derived from ADRs and the diff, and adds the line `A release_notes.md section exists for this spec but was not read: the read budget was exhausted. This list is derived from the ADRs and the diff.` Row 5's "not found" line is **not** emitted. `Inputs used` marks the `release_notes.md` row `not available: read budget exhausted before release_notes.md section could be read`. FR-7's tie-break falls back to its no-catalogue rule. No failure, no modification of `release_notes.md`. |
| 6 | `.adr-list` missing or empty | `What changed and why` states `No ADRs recorded for this spec.` and is synthesised from `requirements.md`, `tasks.md` and the commits. D3 (FR-6) cannot fire; D1 and D2 are evaluated normally. No factor consequence. |
| 7 | An entry in `.adr-list` cannot be resolved to exactly one file in `docs/adr/` | `.adr-list` entries are ordinarily the full filename including its `.md` extension (e.g. `0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md`), which resolves unambiguously on its own. If an entry is instead a bare number (or otherwise doesn't match any file): `What changed and why` names it as `{entry} — ADR file not found in docs/adr/.` If it's a bare number matching more than one file (C-9): `{entry} — ambiguous ADR number, matches: {filenames}; .adr-list should name the full filename instead.` Either way the narrative continues with the remaining ADRs, the entry is marked `not available` in `Inputs used`, it does not count toward D3, and the run succeeds. No factor consequence. |
| 8 | `requirements.md` missing | `Did it ship what it said?` contains exactly `No requirements.md found for this spec — scope reconciliation is not possible.`; F5 = Medium. |
| 9 | `requirements.md` present but declaring zero numbered requirements (FR-8's counting rule) | `Did it ship what it said?` contains exactly `requirements.md declares no numbered requirements — nothing to reconcile.`, with no shipped-as-planned line, no deviation list and no count line; F5 = Medium. |
| 10 | `.issue-number` missing, empty or whitespace-only | The metadata block's linked issue reads `none`; `Inputs used` marks `.issue-number` as `not available: not present`. No factor consequence. |
| 11 | `PROMPT.md` (or a `PROMPT-*.md` companion) absent | No mention anywhere in the output; its absence is normal and is not recorded in `Inputs used`. |
| 12 | Spec branch not determinable (FR-10) | `Blast radius` states `Spec branch not determinable — no diff measured.` followed by the rules it tried; `How it was built` reports the commit count per FR-9's fallback; `What changed and why` carries FR-6 (e)'s **no-diff line**; F1 = Medium. |
| 13 | Spec branch not determinable — effect on `Where to look first` (FR-14) | The section contains exactly: `No diff measured — spec branch not determinable, so no files can be ranked. Start from specs/{spec dir}/tasks.md and the ADRs listed in specs/{spec dir}/.adr-list.` It lists no paths and no diagram, and FR-14's 3–7-path rule does not apply. |
| 14 | Spec branch not determinable — effect on `Breaking changes` (FR-7) | The item list is derived from the ADRs' *Consequences* sections and `requirements.md` only, and the section adds the line `No diff measured — this list is derived from the ADRs and requirements.md only; public-API declaration lines could not be inspected.` The count line is still present, and F2 is computed from the items found. |
| 15 | Spec branch not determinable — effect on the metadata block (FR-5) | The metadata block's spec-branch, head-sha and merge-base-sha lines are each replaced with the single word `undetermined`. The base ref (FR-10's `origin/master`-or-`master` rule) does **not** depend on the spec branch and is still resolved and named normally. The PR reference reads `none found` — FR-20's PR discovery needs the spec branch's name to query `gh pr list --head`, which is unavailable in this state. Generation date, spec directory and issue are populated normally; the metadata block is still present in full. |

**Rows 3 and 4 are retired.** They defined the absence behaviour for "PR exists but no implementation
review round ran" and "no CI checks for the head commit". Both left the document when review history
and CI state left this command's scope. Their numbers are **not reused**, so every citation elsewhere
in this document to rows 5–15 remains valid.

**FR-17 — `PROMPT.md` may inform the narrative but is never cited, and no untracked path is
referenced.**
When a `PROMPT.md`/`PROMPT-*.md` exists it may be read as background, but `show-me.md` — a tracked
file — must not link to it, quote it as a source, or name it as evidence in any table, **unless
that specific path is itself tracked in git** (a small number of spec directories, e.g.
`specs/0003-testing-support-for-command-processor-handlers/`, commit a `PROMPT.md` deliberately; a
tracked one is an ordinary citable file, not an instance of this rule). The rule is stated in terms
of **git tracking, not gitignore status**: `show-me.md` must reference only paths that are tracked
in git (verifiable with `git ls-files --error-unmatch {path}`). `.gitignore` carries both an
exact-match entry for `PROMPT.md` and the glob `PROMPT-*.md`, so in the common case both the root
file and its companions (`PROMPT-calls.md`, `PROMPT-history.md`, …) are gitignored outright; either
way the test that matters is "not tracked in git", which is what actually determines whether a
reference would dangle for every other reader, regardless of which mechanism (gitignore or simple
absence from the index) made a given path untracked. The same rule applies to any other untracked
path (e.g. `specs/**/.current-gear`), and to paths named inside a diagram (FR-6 (c)). It applies with
no exception to this command's **own** *fact ledger*, `specs/{spec}/.show-me-ledger.json`: the ledger
is gitignored by design (FR-4), so `show-me.md` must not name it, link to it, cite it as evidence, or
list it in `## Inputs used` (FR-15) — every fact the ledger carries is attributed instead to the
artefact the measurement script counted it from, which is what NFR-7 already requires. The
measurement script's own path is tracked and would therefore be citable, but it is not an input
either and is not cited. Every path,
ADR and issue reference written into `show-me.md` must resolve for a reader who has only the
repository.

**FR-18 — The command performs no writes other than its own two files, and reads nothing on GitHub
but the PR's identity and its diff.**
Its only writes are the two FR-4 names: `specs/{spec}/show-me.md` and the gitignored *fact ledger*
`specs/{spec}/.show-me-ledger.json`. It must not modify `release_notes.md`, `requirements.md`,
`tasks.md`, any ADR, any approval marker (`.requirements-approved`, `.design-approved`,
`.tasks-approved`, `.code-approved`), `specs/.current-spec`, or `.current-gear`. It must not stage,
commit, push, branch, checkout, stash or rebase. It must not post, edit or resolve any GitHub
comment, apply or remove a label, or change PR state. Both writes are made by the command or by
FR-21's measurement script acting as part of it; no other process is invoked that writes anything.

Its GitHub access is read-only and confined to **`gh pr list`** (FR-20's discovery query) and
**`gh pr diff`** (the spec diff), both already present in the repository's `.claude/settings.json`
allow-list (`Bash(gh pr list:*)`, `Bash(gh pr diff:*)`). `gh pr view` remains permitted by that
allow-list but is **not required** by this command: PR number and URL come from the `gh pr list`
query. No `gh run`, `gh api` or `gh checks` invocation is permitted, and **no query that retrieves PR
comments, reviews or `statusCheckRollup` is permitted** — review history and CI state are out of
scope (FR-9, Out of Scope), so the command must not fetch them even incidentally. Each of those two
`gh` commands is invoked **at most once per run**, which is what the fact ledger exists to make
possible (FR-4, FR-21).

**One new allow-list entry is required, and exactly one.** FR-21 delivers a measurement script and
the allow-list carries no entry that would permit invoking it: there is no `Bash(awk:*)`, no
`Bash(bash:*)`, no `Bash(sh:*)`, no `Bash(jq:*)` and no path-scoped script entry of any kind. This
spec adds the single entry

```
Bash(awk -f .claude/commands/spec/show_me_facts.awk:*)
```

which permits **that one program** to be run in the invocation shape the repository's existing
executable artefact already uses (`awk -f <path> <args>`, as `generate_adr_index.awk` is invoked).
It deliberately does **not** grant `Bash(awk:*)`. The distinction is not cosmetic: POSIX awk provides
`system()` and `"cmd" | getline`, so a general awk grant would confer arbitrary command execution and
would make the `deny` list's `curl`/`wget`/`ssh` entries bypassable. A path-scoped entry confers
neither.

Two things this entry is **not** needed for, both verified against the current allow-list: `wc -c` —
the affordability probe NFR-3 requires before every read — is already covered by the existing
`Bash(wc:*)` entry, and every `git` command this document names is already covered by the existing
`git status`/`log`/`diff`/`show`/`branch`/`rev-parse`/`ls-files` entries. No other addition, removal
or widening of the allow-list is in scope.

**FR-19 — The command reports a short summary to the caller.**
On success it prints, in the session: the path written — `specs/{spec}/show-me.md` only; the *fact
ledger* is working state and is never reported — whether it created or replaced that file, the
overall risk level, and the one-line reminder that the level is advisory. On any FR-1/FR-2/FR-3 stop,
or on FR-21's measurement-script stop, it prints only that stop message.

### Non-functional Requirements

**NFR-1 — Deterministic mechanical fields; judgement-derived fields are named as such.**
For unchanged inputs (same measured ref and head sha, same PR state), two runs must produce
**identical** values for every *mechanically countable* field:

- the section set and order;
- every number in `## Blast radius` (per-bucket file counts, net lines, public-API declaration-line
  count, distinct `src/` subdirectory count) and the named source/refs/shas;
- the task total and per-tag counts, and the commit count (both `## How it was built`, FR-9);
- the *declared id* set and `{total}` in FR-8 — the ids are extracted by FR-8's stated regex over
  `requirements.md`, which is a match, not a judgement;
- FR-6's trigger measurements and their outcome: the values behind D1, D2 and D3, and which of the
  three fired;
- factor level F1, which derives purely from those counts.

The remaining fields are **judgement-derived synthesis and are not required to be identical between
runs**:

- FR-7's breaking-change item list and its count (and therefore F2);
- FR-8's per-requirement statuses — hence which ids land in the shipped-as-planned line versus the
  deviation list, `{k}`, the deviation count, and F5. Note what is and is not deterministic here: the
  *denominator* and the *id set* are mechanical, the *partition* is judged. The old row-per-id table
  made this look more deterministic than it was; the narrative shape does not;
- any diagram's content, its choice of relationship, and — for a box-and-arrow sketch — its format
  (FR-6 (c)); and whether FR-6 (b)'s raise was exercised when no trigger test fired. Whether a test
  *fired* is deterministic; whether the command chose to draw anyway is not;
- the prose (FR-6 narrative, FR-12 rationale, FR-14 reasons).

Every judgement-derived field must stay grounded in — and must never contradict — the evidence cited
in the same entry, bullet or diagram, but its wording, granularity and the boundaries between items
may vary. Because F2 and F5 feed FR-12's maximum, the **overall level may in principle vary with
them**; this is acknowledged rather than asserted away.

**Every field in the first list above is produced by FR-21's measurement script and copied — not
recomputed — into `show-me.md`.** That is what makes this requirement checkable rather than
aspirational: determinism is a property of running one implementation twice, not a property a
document can assert about prose that describes a shell pipeline.

**NFR-2 — Readable in one sitting, with a mechanically countable budget.**
The counted body of `show-me.md` must be between **400 and 2,000 words**. The count is defined
mechanically so it can be checked by a script, and **FR-21's measurement script is that script** — it
applies this rule to the generated file in its word-count mode and reports the total, so the check is
executed rather than merely defined. The rule: count whitespace-delimited tokens containing at least
one alphanumeric character, over every line of the file **except** (a) the H1 and the metadata block
above the first H2, (b) any line that begins with `|` after trimming (pipe-table rows, including
separator rows — this now includes `## Blast radius`'s bucket table, FR-10), (c) every line from the
`## Inputs used` heading to the end of the file, and (d) every line from an opening ``` fence through
its matching closing fence, inclusive. Everything else counts, including H2 headings, prose
paragraphs, **and bullet lists** — FR-7's breaking-change items, FR-8's deviation entries and FR-14's
"where to look" entries count toward the budget.

Exclusion (d) exists because diagram content is not prose: box-drawing characters, Mermaid arrow
syntax and tree glyphs tokenise into nonsense word counts, and diagrams are already bounded by
FR-6 (d)'s explicit caps (at most two blocks, each ≤ 40 lines and ≤ 100 columns). Those caps are the
**only** size rule for diagram content anywhere in this document; nothing else constrains it.

The upper bound is 2,000. Counting every section that contributes prose to the budget for the
calibration case, at each item's own stated cap: up to 600 narrative words (FR-6) plus its `No
diagram:`/stand-down line when applicable (≤ 40); 14 breaking-change bullets at ≤ 40 words each
(FR-7's cap, added by this revision precisely so this arithmetic holds) = ≤ 560, plus the count line
(≤ 10); FR-8's shipped-as-planned line (≤ 40) and its deviation entries (uncapped in count but, for
28 declared ids with at most a handful of deviations in practice, ≤ 150), Part 3 (≤ 5 entries × ≤ 20
words = ≤ 100, per FR-8's stated cap) and the count line (≤ 15); `## How it was built`'s two lines
(≤ 30); `## Blast radius`'s non-table prose lines (the public-API count, the subdirectory count, and
the three "measured from/ref used" lines — ≤ 70, with the six-bucket table itself excluded by (b));
`## Risk assessment (advisory)`'s table (excluded by (b)) plus FR-13's verbatim sentence (22) and
FR-12's 2–5-sentence rationale (≤ 120); up to 7 "where to look" bullets at ≤ 25 words each (FR-14's
existing cap) = ≤ 175; and the eight H2 headings (≤ 24). Summing the ceilings: 600 + 40 + 560 + 10 +
40 + 150 + 100 + 15 + 30 + 70 + 22 + 120 + 175 + 24 ≈ **1,956** — under 2,000 with the caps this
revision added (FR-7's ≤ 40 words/item, FR-8 Part 3's ≤ 5 entries), which is why those caps exist:
without them the ceiling was not provably reachable, which is the same defect as a floor a conforming
minimal output cannot meet.

The lower bound is **400**. A minimal conforming spec (a 150-word narrative at FR-6's floor, no
breaking changes, no deviations, two `## How it was built` lines, `## Blast radius`'s non-table
prose, a three-factor table whose rows are pipe-excluded plus a short rationale, and three "where to
look" bullets) lands at roughly 520 counted words by the same per-section accounting above — safely
above 400, so 400 remains the correct, reachable floor and was not lowered by this revision. The
`What changed and why` section must remain understandable by a contributor who has not read the
spec's ADRs.

**NFR-3 — Bounded cost, measured in bytes, with a stated degradation when the budget binds.**

**The budget.** A single run may bring at most **1,048,576 *charged bytes*** (Definitions) into
context. The unit is bytes because the unit is what the old cap got wrong: across spec 0036's 76
changed `src/` files the mean is 8,226 B, the median 4,035 B, the p90 11,666 B and the max 88,957 B,
so a cap counting *files* priced a 4 KB file and an 89 KB file identically and could not bound a run
at all. 1,048,576 B is ~300 K tokens at the ~3.5 B/token rate these measurements derive from — about
30% of a 1 M context window, leaving the synthesis itself room to run.

**The file-count cap is retired.** The rule "the full content of at most 25 individual files" and
every citation of a *25-file read budget* are withdrawn and replaced by this byte budget. The number
25 no longer has any meaning in this document as a read budget; where it still appears it is FR-14's
≤ 25-word reason cap, which is unrelated and unchanged.

**The ADR-read exemption is retired too.** It was justified "because they are small". Measured, that
is false: spec 0036's seven `.adr-list` ADRs total **702,087 B** whole (mean ~100 KB, max 138,105 B).
Their *targeted* reads — front matter plus `## Status` plus `## Consequences` — total **95,967 B**
(mean 13,710 B, max 20,709 B), which is affordable but is not nothing, so it is charged like every
other read rather than exempted. FR-6/FR-7's per-ADR obligations stay satisfiable: 95,967 B is 9% of
the budget for a seven-ADR spec, and 15 ADRs at the measured mean is 205,650 B, or 20%.

**Affordability is checked before anything is opened.** Before every full-content read the command
determines the file's size with `wc -c`. **`wc -c` is a size measurement, not a read**: it costs no
charged bytes, requires no new allow-list entry (`Bash(wc:*)` is already present) and may be applied
to any path, including one the run will never open. If the measured size exceeds the bytes remaining,
that file is **not** read in full; it is read by targeted extraction or bounded chunks instead
(see *Degradation* below). No read is ever issued blind.

**A reserve is held for FR-6's source reads.** Of the 1,048,576 B, the last **100,000 B** may be
spent only on source files read to draw a diagram accurately (FR-6 (f)). Everything else — the
script's JSON, `tasks.md`, the diff, ADR extracts, `requirements.md`, the `release_notes.md` section
— draws on the remaining 948,576 B. The reserve is sized from the measured spread: 100,000 B is ≥ 8
files at the p90 of 11,666 B, ≥ 24 at the median of 4,035 B, and ≥ 1 even at the max of 88,957 B.
The reserve exists to make diagram affordability a **guarantee rather than an artefact of read
order**: without it, whether a diagram can be drawn would depend on whether `requirements.md`
happened to be read whole or extracted first, and this document specifies no read order. That
order-dependence would make AC-56 and AC-78 unassertable and would let the visual-explanation
capability fail silently on the very change it was designed for.

**What is affordable, measured on spec 0036 at merge base `6145913a0..spec/scoped-lifetime-per-pipeline`.**
`tasks.md` may be read **in full** — 229,159 B, 22% of the budget; the "229 KB scare case" was never
the problem. The **`src/`-scoped diff is permitted** — 303,715 B, 29%. Summary statistics for the
whole diff (`--numstat`/`--name-only`/`--stat`) are permitted — `git diff --stat` alone is 31,929 B,
3%.

**The full diff is banned, and not as a policy choice.** No `git diff` or `gh pr diff` may be issued
without either a pathspec restricting it or a summary-only flag. Spec 0036's full 517-file diff is
**4,081,673 B ≈ 1.24 M tokens** — it **exceeds the context window outright**. This is a hard wall: a
run that attempted it would not degrade, it would fail. The ban is stated here rather than left
implicit because the previous revision banned the full diff without ever saying why, and a reader who
does not know the number will reasonably assume the ban is negotiable.

**Worst case, measured — the calibration run fits by construction.** Against the 948,576 B general
allowance: the script's JSON at its FR-21 cap (≤ 65,536) + `tasks.md` in full (229,159) + the
`src/`-scoped diff (303,715) + seven targeted ADR extracts (95,967) = **694,377 B**, leaving
254,199 B. `wc -c` then shows `requirements.md` at 273,674 B — more than remains — so it is read by
targeted extraction rather than in full, exactly as *Degradation* requires; `release_notes.md` at
118,145 B whole still fits. The 100,000 B reserve is untouched and available to FR-6. The total can
never exceed 1,048,576 B, because no file is opened whose measured size the remaining allowance does
not cover.

**The budget is per run, not per artefact and not per diagram.** Source files read to draw a diagram
(FR-6) are charged to this same budget. A run drawing two diagrams has one reserve between them.
There is no second budget and no per-diagram allowance. When the budget is exhausted before a
diagram's relationship can be read accurately, the command emits FR-6 (e)'s **budget line** and draws
nothing — it never infers a call flow from file names, diff statistics or a partial read.

**Degradation — chunk, never skip.** When a required artefact does not fit the bytes remaining, the
command reads it in bounded chunks, or by targeted extraction of the constructs it needs (checkbox
lines, headings, task-type tags, tables, a named `release_notes.md` section, a named requirement's
paragraph), and must never silently omit data needed for FR-3's completeness check, FR-8's
per-requirement evidence, or FR-9's task-shape counts. If any required extraction could not be
completed, the affected value is reported as `Unverifiable` (FR-8) or `not available` (FR-15) with
the reason — it is never reported as zero or omitted.

**NFR-4 — Works offline.** With no network and no `gh`, the command must still produce a complete
`show-me.md` from local artefacts and git alone, degrading per FR-16 and recording the reason in
`Inputs used`.

**NFR-5 — No secrets, no leakage.** The command must not write tokens, credentials, absolute
machine-local paths, or the contents of files that are **not tracked in git** into `show-me.md` (the
same tracking test as FR-17, which covers both gitignored and merely untracked paths, and which binds
diagram content as well as prose). All repository paths written are relative to the repository root.

**NFR-6 — Convention conformance.** This spec delivers three artefacts and all three follow the
conventions of the family they join.

- *The command file*, `.claude/commands/spec/show-me.md`, follows the existing `/spec:*` conventions
  (front matter with `allowed-tools`, `description`, `argument-hint`; `$ARGUMENTS`; documented in
  `.claude/commands/spec/README.md` alongside the other commands).
- *The measurement script*, `.claude/commands/spec/show_me_facts.awk` (FR-21), sits beside the
  command file and follows the repository's one existing precedent for an executable artefact in this
  family, `.claude/commands/adr/generate_adr_index.awk`, in every respect: it is an awk program, it
  carries the same `#!/usr/bin/awk -f` shebang line, it is committed **non-executable (mode
  `100644`)** because `awk -f` reads it rather than executing it, and it is invoked
  `awk -f <path> <args>` from the repository root. `.claude/commands/spec/README.md` documents it
  alongside `/spec:show-me`, naming what it emits and that it is not invoked directly by a user.
- *Its test script*, `.claude/commands/spec/show_me_facts_test.sh` (NFR-9), also sits beside the
  command file. Unlike the awk program it **is** executed directly, so it carries a `#!/usr/bin/env sh`
  shebang and is committed executable (mode `100755`).
- *The generated `show-me.md`* is valid markdown whose relative links all resolve from its location
  in the spec directory, and whose fenced blocks are closed.

**NFR-7 — Traceable claims.** Every factual claim in `show-me.md` must be attributable to a listed
input: counts to the artefact they were counted from, requirement statuses to a task id / file path /
ADR slug, and every symbol, type, file or component drawn in a diagram to the diff, an ADR, or a file
the command read. The command must not assert behaviour it has not read evidence for, and must not
draw structure it has not read evidence for.

**NFR-8 — Safe re-run.** Re-running the command on a spec whose inputs have changed (e.g. new commits
on the branch) must leave the repository in a state differing only in `show-me.md`'s contents and, as
the single narrow carve-out, in the contents of the gitignored *fact ledger*
`specs/{spec}/.show-me-ledger.json` (FR-4). Because the ledger is gitignored, that carve-out is
invisible to `git status --porcelain`, which must be byte-identical before and after a successful run
except for `show-me.md` (AC-30, AC-74). A stop under FR-1, FR-2, FR-3 or FR-21 must leave the
repository byte-for-byte unchanged, ledger included — no ledger is created and no existing ledger is
touched on a stop.

**NFR-9 — The measurement script is covered by an executable test that runs against real fixtures.**
FR-21's script is the single point at which every mechanically countable value in this document is
computed, so an undetected defect in it silently falsifies `show-me.md` rather than failing loudly.
It is therefore delivered with a **sibling test script**,
`.claude/commands/spec/show_me_facts_test.sh`, beside the program it tests.

*What it is.* A plain executable shell script that invokes the awk program under test. **No test
framework, no new solution project, and nothing added under `src/` or `tests/`** — this spec has no
C# component and that boundary is unchanged. This is a deliberate choice against the family's status
quo, not an oversight in it: the only executable artefact anywhere in `.claude/` today is
`generate_adr_index.awk`, it has no tests at all, there is no shell-test framework anywhere in this
repository, every project under `tests/` is C#/xUnit, and no CI workflow lints or tests anything
under `.claude/`. "Nothing runs it automatically" is the pre-existing condition of this whole family;
this requirement leaves a regression net behind where there was none.

*What it does.* It invokes the measurement script against **named, tracked fixtures**, asserts on
fields of the emitted JSON, prints which assertion failed, and **exits non-zero if any assertion
fails**. The fixtures and the facts asserted are, at minimum:

| Fixture | Asserted |
|---|---|
| `specs/9999-show-me-fixture/` | 3 declared ids; 2 task checkboxes, 0 unchecked |
| `specs/0033-pg-advisory-lock-sha256/` | 8 declared ids; 5 task checkboxes, 0 unchecked |
| `specs/0002-sqs-cleanup/` | **0** declared ids — the FR-16 row 9 case — with 6 checkboxes, 0 unchecked; the run must report zero, not fail |
| `specs/0036-scoped-lifetime-per-pipeline/` *(C-8)* | 37 declared ids; 82 task checkboxes, 0 unchecked |

Every figure in that table was measured against the real fixture directories on 2026-09-21, not
estimated.

*Two regressions it must specifically pin,* because both were observed live and both are the reason
FR-21 exists: the declared-id count must be non-zero for every fixture that declares ids (the pattern
once returned **0** where the answer was **28**, an escaped `\|` having leaked in from a markdown
table cell); and the NFR-2 word-count mode must exclude fenced blocks (it once returned **16** where
the answer was **8**, over-counting roughly 2x by counting inside fences).

*One invariant it checks over the command file itself*: the delivered
`.claude/commands/spec/show-me.md` contains no branch on the risk level (FR-13), tested in a way that
cannot match the letters `if` inside an unrelated word such as `diff` — a false-positive class
already observed three times.

*What it is not.* It is not a gate, not wired into CI by this spec, and not a prerequisite for
running `/spec:show-me`. It is a script a person or a task runs.

### Constraints and Assumptions

- **C-1** Spec directories follow `specs/NNNN-name/`, and spec ids are **not unique** — FR-1's
  ambiguity handling is required, not defensive. Spec directory names may also contain spaces
  (`specs/0021-Expose Unacceptable Message Window/`), which is why FR-1 takes the whole argument.
- **C-2** `tasks.md` records completion as markdown checkboxes in the established style
  (`- [x] **TEST + IMPLEMENT: T1.5 — …**`), and task-type tags are one of
  `TEST + IMPLEMENT` / `STRUCTURAL` / `PROJECT` / `DOC`.
- **C-3** *(retired — it described the repository's two Claude-review GitHub Action paths and existed
  only to ground the finding/severity/review-round definitions, all of which have left this document
  with review history. The identifier is not reused, so citations to C-4…C-10 elsewhere remain
  valid.)*
- **C-4** Spec branches are conventionally named `spec/<name>` (e.g.
  `spec/scoped-lifetime-per-pipeline`) and target `master`; FR-10's fallback rules exist because the
  convention is a convention, not a guarantee. Both a local and a remote-tracking form of a spec
  branch commonly exist and can diverge, which is why FR-10 states a precedence and names the ref.
- **C-5** A spec's raw diff totals are dominated by its own paperwork (spec 0036: 517 files total,
  76 under `src/`), so bucketing (FR-10) is mandatory and F1 counts only `src/`.
- **C-6** The command runs inside Claude Code with the repository checked out; `gh` may or may not be
  available and authenticated (NFR-4).
- **C-7** `show-me.md` is a tracked spec deliverable; git provides its history, so the command does
  not version, archive or timestamp its own output file (FR-4).
- **C-8** **Test-fixture branch dependency.** The spec-0036 calibration fixtures —
  `specs/0036-scoped-lifetime-per-pipeline/`, its seven ADRs (`0070-per-pipeline-di-scope-…` through
  `0076-scope-affinity-option-and-write-through`), its `tasks.md`, and its `release_notes.md` section
  — exist **only on the branch `spec/scoped-lifetime-per-pipeline` and on branches it has been
  merged into**. They are not on `master`. (At the time this document was written, that branch was
  already merged into the branch this command is itself being built on, so the fixtures are present
  there too — this is a fact about *this* branch's history, not a guarantee for every future branch
  this spec's tests might run on.) Every acceptance criterion marked *(C-8)* in its citation (AC-1,
  AC-2, AC-4, AC-12, AC-17, AC-18, AC-20, AC-47, AC-51, AC-56) therefore carries a **test
  precondition**: that branch must be checked out, or merged into the branch under test, at the time
  the criterion is exercised — the inline *(C-8)* marker on each such AC is the single source of
  truth for which criteria this covers, not this prose list, which exists only to explain why the
  marker is there. This is a documented fact about where the fixture data lives, not a defect to
  design around: spec 0036 is the intended real-world calibration case for this command, and it is
  retained deliberately. **A fixture cited by any criterion must resolve under FR-10 on the branch the
  criterion is run on** — spec 0036 does, which is why every worked example in this document uses it
  rather than a spec directory that has no branch. **AC-8 deliberately carries no *(C-8)* marker**, despite an earlier revision of this
  document listing it here: its fixture, `specs/0005-defer-message-on-error/`, is one of the
  fixtures below that must run anywhere, on `master` included, so it has no calibration-branch
  precondition to carry. Criteria that must be runnable anywhere use
  fixtures available on `master`: the three `0002-*` directories for ambiguity,
  `specs/0021-Expose Unacceptable Message Window/` for whitespace, and
  `specs/0005-defer-message-on-error/` — which contains a `requirements.md` and nothing else — for
  the missing-`tasks.md` case. **AC-47 carries the C-8 precondition *and* one more on top.** Its
  local-vs-remote-tracking divergence case is **not** demonstrable on the calibration branch's
  current fixture — `git rev-parse spec/scoped-lifetime-per-pipeline
  origin/spec/scoped-lifetime-per-pipeline` returns the same sha for both — so having the branch
  present is necessary but not sufficient. AC-47's own text therefore specifies the additional setup
  it needs (a deliberately moved local branch tip); it is the one *(C-8)* criterion that uses the
  calibration branch as a starting point to be modified rather than as-is.
- **C-9** **ADR numbers are not unique anywhere in this repository** — not merely across branches.
  `docs/adr/` on `master` today carries duplicate numbers 0037 (five files), 0038, 0039, 0040, 0041,
  0042, 0043, 0051, 0053, 0054, 0057 (four files), 0061, 0063 and 0064. Per
  `.agent_instructions/adr_frontmatter.md`, the ADR **number is a non-unique ordering hint** and the
  **identity is the filename stem**; renumbering is explicitly rejected and "a number collision with
  a concurrent branch is acceptable". This is permanent and structural, which is why FR-6 forbids
  bare-number ADR references throughout `show-me.md` and FR-16 row 7 defines what happens when an
  `.adr-list` entry cannot be resolved to exactly one file.
- **C-10** The repository's `.claude/settings.json` **`gh`** allow-list is exactly `gh pr view`,
  `gh pr list`, `gh pr diff`, `gh issue view`, `gh issue list` — there is **no** run-query and **no**
  `gh api` entry, and this spec adds neither. FR-18 confines the command's GitHub access to
  `gh pr list` and `gh pr diff`, both already inside that list, so **no `gh` entry changes**.
  The wider allow-list is a different matter. It permits `Read`, `Glob`, `Grep` and a fixed set of
  `Bash(…)` prefixes — among them `Bash(wc:*)`, which already covers NFR-3's affordability probe, and
  the `git` read commands this document names — but it contains **no `Bash(awk:*)`, no
  `Bash(bash:*)`, no `Bash(sh:*)`, no `Bash(jq:*)` and no path-scoped script entry**. FR-21's
  measurement script therefore cannot be invoked under the allow-list as it stands, and **this spec
  adds exactly one entry**, `Bash(awk -f .claude/commands/spec/show_me_facts.awk:*)`, permitting that
  one program in the `awk -f <path>` shape the precedent artefact already uses. A general
  `Bash(awk:*)` grant is deliberately **not** requested: awk provides `system()` and
  `"cmd" | getline`, so it would confer arbitrary command execution and make the `deny` list's
  `curl`/`wget`/`ssh` entries bypassable. The precedent artefact,
  `.claude/commands/adr/generate_adr_index.awk`, has no entry at all and runs by per-invocation
  approval from five documented call sites; this spec chooses the narrow entry instead, because
  `/spec:show-me` is meant to complete in one turn without a permission prompt in the middle of a
  measurement. That single addition is the whole settings change this spec makes (FR-18, Out of
  Scope).
- **A-1** Assumption: a finished spec's `tasks.md` is an accurate record of what was built — the
  command reconciles against it rather than re-deriving intent from the diff.
- **A-2** Assumption: the reader of `show-me.md` knows Brighter as a library but has not read this
  spec's ADRs, `tasks.md`, or PR history.
- **A-3** Assumption: the reviewer reading `show-me.md` has the pull request in front of them. Review
  findings and CI results are one click away there, which is why this command neither copies nor
  summarises them.

### Out of Scope

- **Re-reviewing the change.** `show-me.md` does not assess correctness, security, performance, test
  quality or TDD compliance, and raises no findings of its own. `/spec:review code` owns that, and
  the review that already ran on the pull request owns its own output.
- **Summarising PR review history in any form**: no finding counts, no severity tallies, no
  resolved/acknowledged/open state, no per-round breakdown, no excluded-pass count, for
  implementation *or* specification-phase passes. The pull request is the record of its own review.
- **Reporting CI state**: no check tally, no rollup, no pass/fail summary, no risk factor derived
  from any of them. The checks tab is the record of its own CI.
- **Any mid-implementation or "running status" variant.** `/spec:show-me` runs only against a
  complete spec (FR-3). Progress reporting during implementation is a separate concept and is not
  addressed here; `/spec:status` remains the place for in-flight state.
- **A `--force`/override flag** to run against an incomplete spec. There is no way to bypass FR-3.
- **Any change to `release_notes.md`** — neither its content, format, ownership, nor the DOC tasks
  that curate it. `show-me.md` neither generates it, derives from it as a required input, nor
  requires a cross-reference to it.
- **Gating merge in any form**: blocking, required checks, CI integration, PR labels, requesting
  changes, or any marker file that another command could read as a gate (FR-13).
- **Posting to GitHub**: no PR comments, no PR description updates, no issue comments (FR-18).
- **Any change to `.claude/settings.json` beyond the single path-scoped entry FR-21's measurement
  script needs.** One entry is added, `Bash(awk -f .claude/commands/spec/show_me_facts.awk:*)`,
  because no existing entry covers it (C-10). Everything else about the file is out of scope: no `gh`
  entry is added, removed or widened; **no general interpreter grant** (`Bash(awk:*)`, `bash`, `sh`,
  `jq`) is requested; the `deny` list is untouched; and the command's GitHub access stays inside the
  `gh pr list` / `gh pr diff` entries that already exist (FR-18).
- **Any change to `.gitignore` beyond the single exact-match entry `.show-me-ledger.json`.** That one
  line is added so the *fact ledger* leaves `git status --porcelain` clean (FR-4, NFR-8, AC-30). No
  existing pattern is edited, and no wildcard form is used.
- **Rendered images.** Diagrams are fenced text — ASCII or Mermaid (FR-6 (c)). No PNG, SVG, image
  file, external rendering service, or generated asset of any kind is produced or linked.
- **Diagram-only output.** A diagram never replaces the prose it illustrates; FR-6's 150–600-word
  narrative obligation is independent of whether a diagram is drawn.
- **PR-description formatting.** `show-me.md` is written for a reader, not as paste-ready PR body
  text. A human may paste from it, but no requirement constrains it to PR-description shape.
- **The "switching gears" follow-up** ([#4357](https://github.com/BrighterCommand/Brighter/issues/4357),
  now closed — it shipped as ADR 0071 and `/spec:gear`). Unrelated to this spec either way.
- **Multi-spec roll-ups**: no "show me the last five specs", no cross-spec dashboard, no aggregate
  risk report.
- **Re-running or changing the review workflow**, its severities, its model, or its triggers.
- **Renumbering or de-duplicating ADRs** (C-9). The command adapts to non-unique numbers; it does not
  fix them.
- **Automatically committing `show-me.md`** or opening a PR for it.
- **Re-litigating spec content**: the command summarises and assesses shape; it does not review code
  or duplicate `/spec:review`.

## Acceptance Criteria

**AC-1** *(FR-1, C-8)* **Given** the branch `spec/scoped-lifetime-per-pipeline` is checked out or
merged, so that `specs/0036-generator-universal-rejection-tests/` and
`specs/0036-scoped-lifetime-per-pipeline/` both exist, **when** `/spec:show-me 0036` is run, **then**
no file is written and the output names both directories and asks for the full directory name. (The
branch-independent equivalent of this behaviour is exercised by AC-1a below using the three
`0002-*` directories on `master`.)

**AC-1a** *(FR-1)* **Given** `master` (no feature branch required), on which
`specs/0002-backstop-error-handler/`, `specs/0002-sqs-cleanup/` and
`specs/0002-universal_scheduler_delay/` all exist, **when** `/spec:show-me 0002` is run, **then** no
file is written and the output names all three directories and asks for the full directory name;
**and when** `/spec:show-me sqs-cleanup` is run, **then** the target spec is `specs/0002-sqs-cleanup/`.

**AC-2** *(FR-1, C-8)* **Given** the calibration branch is checked out so both 0036 directories
exist, **when** `/spec:show-me scoped-lifetime` is run, **then** the target spec is
`specs/0036-scoped-lifetime-per-pipeline/`.

**AC-3** *(FR-1)* **Given** no spec directory contains the text `kafka-widget`, **when**
`/spec:show-me kafka-widget` is run, **then** no file is written and the output says no spec matches
and points at `/spec:status`.

**AC-4** *(FR-2, C-8)* **Given** the calibration branch is checked out or merged (so that
`specs/0036-scoped-lifetime-per-pipeline/` exists) and `specs/.current-spec` contains
`0036-scoped-lifetime-per-pipeline`,
**when** `/spec:show-me` is run with no argument, **then** that spec is the target.

**AC-5** *(FR-2)* **Given** `specs/.current-spec` is absent (or names a directory that no longer
exists), **when** `/spec:show-me` is run with no argument, **then** no file is written and the output
states which of missing/empty/stale applies and offers both remedies (pass an id, or `/spec:switch`).

**AC-6** *(FR-3)* **Given** a target spec whose `tasks.md` has 80 checked and 2 unchecked checkboxes,
**when** the command is run, **then** no file is written, and the output states `2 of 82` and lists
the first three (here: both) unchecked task titles.

**AC-7** *(FR-3, FR-21, NFR-8)* **Given** a target spec with an existing `show-me.md` and an unchecked
task, **when** the command is run, **then** the existing `show-me.md` is byte-for-byte unchanged and
no other file in the repository is created or modified — including, explicitly,
`specs/{spec}/.show-me-ledger.json`, which is not created if it was absent and is byte-for-byte
unchanged if it was present, because the FR-3 gate runs above both writes (FR-3, FR-21).

**AC-8** *(FR-3)* **Given** `specs/0005-defer-message-on-error/` — a spec directory present on
`master` that contains a `requirements.md` and no `tasks.md` — **when**
`/spec:show-me 0005-defer-message-on-error` is run, **then** no file is written and the output says
the spec has no `tasks.md`.

**AC-9** *(FR-4)* **Given** a complete target spec with no existing `show-me.md`, **when** the command
is run, **then** `specs/{spec}/show-me.md` exists afterwards, the session output says it was created,
and `git status` shows it as the only new/modified path (it is not staged or committed).

**AC-10** *(FR-4)* **Given** a complete target spec whose `show-me.md` already exists, **when** the
command is run again, **then** that file's contents are wholly replaced, and no `show-me-2.md`,
`show-me.md.bak` or similarly named file is created.

**AC-11** *(FR-5)* **Given** any successful run **in which a spec branch was resolved**, **when**
`show-me.md` is inspected, **then** it contains the eight required H2 headings with the exact
spellings given in FR-5, in that order, and a metadata block naming generation date, spec directory,
issue, the full spec-branch ref, head sha, base ref and merge-base sha, and PR reference.

**AC-12** *(FR-6, C-8)* **Given** spec 0036 on its calibration branch (whose `.adr-list` names the
seven ADR filenames `0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md` …
`0076-scope-affinity-option-and-write-through.md`), **when** the command runs, **then**
`## What changed and why` is 150–600 words of prose (fenced-block lines excluded), names all seven
ADRs by filename stem and title with their current Status, links each with a relative path that
resolves from the spec directory, and contains **no** reference of the bare-number form `ADR 0070`
unaccompanied by its filename stem.

**AC-13** *(FR-7)* **Given** a spec that introduced breaking changes, **when** the command runs,
**then** every listed item is ≤ 40 words and carries **one or more** of the classifications
source/binary/behavioural/compatibility (a combined value such as "source and binary" is valid) and a
one-sentence migration, and the section ends with `Total breaking-change items: {n}` matching the
number of bullets listed.

**AC-14** *(FR-7, FR-16 row 5)* **Given** a spec with no section in `release_notes.md`, **when** the
command runs, **then** the run succeeds, the section states that no `release_notes.md` section was
found and that the list is derived from the ADRs and the diff, and `release_notes.md` itself is
unmodified.

**AC-15** *(FR-8)* **Given** a `requirements.md` declaring FR-1…FR-12 and NFR-1…NFR-5 — including at
least one sub-numbered clause such as `FR-7.2`, and cross-references to those ids inside other
requirements' prose — **when** the command runs, **then** `{total}` is **17** (one per declared
top-level id; sub-numbered clauses fold into their parent and cross-references declare nothing), the
shipped-as-planned line and the deviation entries between them name each of those 17 ids exactly
once, no id appears in both, no id is absent from both, every deviation entry carries a status from
the allowed set and a reason, every `Deferred`/`Dropped`/`Withdrawn` entry states a follow-up issue
number, a superseding requirement, or `no follow-up recorded`, and the count line's six terms sum
to 17.

**AC-16** *(FR-8, FR-16 row 8)* **Given** a complete spec with no `requirements.md`, **when** the
command runs, **then** the run succeeds, the section contains exactly the defined "not possible"
line, and F5 is `Medium` in the risk table.

**AC-17** *(FR-9, C-8)* **Given** spec 0036's calibration branch with its 82-task `tasks.md`, **when**
the command runs, **then** `## How it was built` reports the 82-task total with its per-tag
breakdown and the branch's commit count since the merge base, **and** contains no finding count, no
severity tally, no resolved/acknowledged/open state, no review-round line, no excluded-pass count and
no CI check tally.

**AC-18** *(FR-10, FR-20, C-5, C-8)* **Given** spec 0036's branch, **when** the command runs, **then**
`## Blast radius` reports all six bucket file counts — `src/` 76, `tests/` 393, `specs/` 24, `docs/`
14, `.github/` 0, `other` 10 — which sum to the reported total of **517**; reports net lines; reports
the changed public-API declaration-line count; reports that the `src/` changes span **6** distinct
immediate subdirectories of `src/`; and names exactly one measurement source with its refs and shas
(`gh pr diff #4282 (head {sha})` or `git diff {merge-base sha}..{head sha}`).

**AC-19** *(FR-10, FR-16 rows 12 and 15)* **Given** a spec whose branch cannot be resolved by any of
FR-10's three rules, **when** the command runs, **then** the run still succeeds, `## Blast radius`
states the branch is not determinable and lists the rules tried, F1 is `Medium`, `## How it was
built` reports `Commits: not determinable — spec branch not resolved.` while still reporting the task
shape, the metadata block's spec-branch, head-sha and merge-base-sha lines each read `undetermined`,
its base-ref line still names the resolved base ref normally, its PR-reference line reads
`none found`, and generation date, spec directory and issue are populated normally.

**AC-20** *(FR-11, C-8)* **Given** spec 0036 (76 `src/` files, 14 breaking-change items), **when** the
command runs, **then** the risk table has exactly three rows, showing F1 `High` citing `76` and F2
`High` citing `14`, plus an F5 row citing its deviation-entry tally, **and** the table contains no row
labelled `F3` and no row labelled `F4`.

**AC-21** *(FR-11)* **Given** a spec with 4 `src/` files changed, 0 breaking changes, and every
declared requirement `Shipped` (so the deviation list is the `No deviations:` line), **when** the
command runs, **then** all three factors are `Low`.

**AC-22** *(FR-12)* **Given** factor levels F1 `Low`, F2 `Medium`, F5 `Low`, **when** the command
runs, **then** the output contains `**Overall risk: Medium**` and the rationale references F2.

**AC-23** *(FR-12)* **Given** any set of factor levels, **when** the command runs, **then** the stated
overall level is never lower than the maximum factor level; if it is higher, the rationale contains
an explicit sentence giving the reason for raising it.

**AC-24** *(FR-13)* **Given** a run whose overall level is `High`, **when** the command finishes,
**then** `show-me.md` was still written, the session output contains the FR-19 report (path written,
created-or-replaced, level, advisory reminder) and **no error message and no refusal**, no marker
file, label or comment was produced, and `## Risk assessment (advisory)` contains verbatim:
`This assessment is advisory only. It is not a merge gate; the merge decision stays with a human
reviewer.`

**AC-25** *(FR-13)* **Given** two otherwise identical runs producing `Low` and `High` respectively,
**when** their side effects are compared, **then** the only difference is the text inside
`show-me.md`.

**AC-26** *(FR-14)* **Given** any successful run **in which a spec diff was measured**, **when**
`## Where to look first` is inspected, **then** it lists 3–7 paths, every path appears in the spec
diff, and each carries a reason of ≤ 25 words.

**AC-27** *(FR-15, FR-16 rows 1 and 5)* **Given** a run with no PR and no `release_notes.md` section,
**when** `## Inputs used` is inspected, **then** it marks the pull-request row `not available` with a
reason, marks `release_notes.md` as not available, marks `tasks.md`, `requirements.md`, `.adr-list`
and git history as `used`, and contains **no row for review comments and no row for CI checks**.

**AC-28** *(FR-16 row 2, NFR-4)* **Given** `gh` is unavailable or unauthenticated, **when** the
command runs against a complete spec, **then** it still writes a complete `show-me.md`, records
`gh unavailable` in `Inputs used`, measures blast radius from `git diff`, and **no factor level
changes because of the absence** (F1 is computed from the git-measured `src/` count as normal).

**AC-29** *(FR-17, NFR-5)* **Given** a spec directory containing a `PROMPT.md` and a
`PROMPT-history.md`, **when** the command runs, **then** `show-me.md` contains no occurrence of
either filename, no link to either, and no reference to any path that
`git ls-files --error-unmatch` reports as untracked — including inside any diagram; every relative
link in `show-me.md` resolves to an existing tracked repository path.

**AC-30** *(FR-18, FR-4)* **Given** a successful run, **when** `git status --porcelain` is compared
before and after, **then** the only difference is `specs/{spec}/show-me.md` — the *fact ledger* that
the same run wrote to `specs/{spec}/.show-me-ledger.json` does **not** appear, because `.gitignore`
carries the exact-match entry for it, which `git check-ignore -v specs/{spec}/.show-me-ledger.json`
confirms — and no commit, push, branch, label, comment or PR state change occurred; **and when** the
commands the run issued are inspected, **then** every `gh` invocation is one of `gh pr list` or
`gh pr diff`, none is `gh run`, `gh api` or `gh checks`, none requests PR comments, reviews or
`statusCheckRollup`, and `gh pr diff` was invoked **at most once**.

**AC-31** *(FR-19)* **Given** a successful run, **when** the session output is read, **then** it
states the written path, created-vs-replaced, the overall level, and that the level is advisory.

**AC-32** *(NFR-1)* **Given** two runs against an unchanged measured ref, head sha and PR state,
**when** the two `show-me.md` files are compared, **then** every number in `## Blast radius`
(including the distinct-`src/`-subdirectory count), the named source/refs/shas, the task total and
per-tag counts, the commit count, FR-8's `{total}` and its set of declared requirement ids, the
measured values behind D1/D2/D3 and which of them fired, and factor level F1 are identical. F2, F5,
FR-8's statuses and `{k}`, the overall level, any diagram's content and format, whether FR-6 (b)'s
raise was exercised, and all prose are **not** required to be identical; the test instead asserts
that each run's F2 and F5 values are consistent with the evidence cited in that same run's own FR-7
bullets and FR-8 deviation entries respectively.

**AC-33** *(NFR-2)* **Given** any successful run, **when** the body of `show-me.md` is word-counted by
NFR-2's mechanical rule (excluding the H1/metadata block, all lines beginning with `|`, everything
from `## Inputs used` onward, and every line inside a fenced code block; including headings and
bullet lists), **then** the count is between 400 and 2,000.

**AC-34** *(NFR-7)* **Given** any successful run, **when** each count, status and diagram node in
`show-me.md` is checked, **then** each is attributable to an input listed in `## Inputs used` and
matches that input's actual content.

**AC-35** *(FR-1)* **Given** `specs/0021-Expose Unacceptable Message Window/` exists on `master`,
**when** `/spec:show-me 0021-Expose Unacceptable Message Window` is run (argument unquoted, four
whitespace-separated words), **then** the whole argument text is taken as the spec id and the target
spec is `specs/0021-Expose Unacceptable Message Window/` — not a failure, and not a match attempted
on `0021-Expose` alone; **and when** `/spec:show-me README.md` is run, **then** no spec matches,
because `specs/README.md` is a file and not a candidate.

**AC-36** *(FR-14, FR-16 row 13)* **Given** a complete spec whose branch is not determinable, so no
diff is measured, **when** the command runs, **then** the run succeeds and
`## Where to look first` contains exactly the FR-16 row 13 fallback line, lists no paths and no
diagram, and FR-14's 3–7-path rule is not applied.

**AC-37** *(FR-7, FR-16 row 14)* **Given** the same no-diff run, **when** `## Breaking changes` is
inspected, **then** its items are derived from the ADRs' *Consequences* sections and
`requirements.md` only, it contains the FR-16 row 14 line stating that public-API declaration lines
could not be inspected, it still ends with `Total breaking-change items: {n}`, and F2 is computed
from the items listed.

**AC-38** *(retired with FR-16 row 3 — review-round absence is no longer a behaviour of this command;
the number is not reused.)*

**AC-39** *(retired with FR-16 row 4 — CI rollup absence is no longer a behaviour of this command;
the number is not reused.)*

**AC-40** *(FR-6, FR-16 row 6)* **Given** a complete spec whose `.adr-list` is missing or empty,
**when** the command runs, **then** the run succeeds, `## What changed and why` states
`No ADRs recorded for this spec.` and is synthesised from `requirements.md`, `tasks.md` and the
commits, D3 does not fire while D1 and D2 are still evaluated, and no risk factor changes because of
the absence.

**AC-41** *(FR-16 row 11, FR-17)* **Given** a spec directory with no `PROMPT.md` and no `PROMPT-*.md`,
**when** the command runs, **then** `show-me.md` contains no occurrence of the string `PROMPT` and
`## Inputs used` has no row for it — the absence is reported nowhere.

**AC-42** *(FR-6, FR-16 row 7, C-9)* **Given** an `.adr-list` containing one entry naming a file that
does not exist in `docs/adr/` and one entry given as a bare number that matches more than one file
there (e.g. `0037`, which matches five files on `master`), **when** the command runs, **then** the
run succeeds, `## What changed and why` names the first as `ADR file not found` and the second as
`ambiguous ADR number` listing the matching filenames, the remaining ADRs are still named and linked,
both unresolved entries are marked `not available` in `## Inputs used`, neither counts toward D3, and
no risk factor changes.

**AC-43** *(FR-8, FR-16 row 9)* **Given** a complete spec whose `requirements.md` exists but declares
no `FR-n`/`NFR-n` ids, **when** the command runs, **then** the run succeeds,
`## Did it ship what it said?` contains exactly `requirements.md declares no numbered requirements —
nothing to reconcile.` with no shipped-as-planned line, no deviation list and no count line, and F5
is `Medium`.

**AC-44** *(FR-5, FR-16 row 10)* **Given** a complete spec with no `.issue-number` (or an empty one),
**when** the command runs, **then** the metadata block's linked-issue line reads `none`,
`## Inputs used` marks `.issue-number` as `not available: not present`, the run succeeds, and no risk
factor changes.

**AC-45** *(FR-8, FR-11)* **Given** a `requirements.md` declaring `FR-27` with sub-clauses `FR-27.1`,
`FR-27.2` and `FR-27.3` where `FR-27.1` and `FR-27.2` both shipped and `FR-27.3` was explicitly
withdrawn by a decision recorded in an ADR that names a superseding requirement, **when** the command
runs, **then** `FR-27` appears as exactly one deviation entry (no separate `FR-27.3` entry, and
`FR-27` does not appear in the shipped-as-planned line), that entry addresses all three sub-clauses
in its paraphrase and evidence and names `FR-27.3` as the differing sub-clause, its status is
`Withdrawn` (the least-shipped sub-clause status, per the stated precedence `Shipped` < … <
`Withdrawn` < `Dropped`), its evidence cites where the withdrawal is recorded and the superseding
requirement, the count line includes a `Withdrawn:` term, and F5 is **`Medium`** for this
reconciliation (not `High`) because that entry states a superseding requirement rather than
`no follow-up recorded`.

**AC-46** *(FR-20)* **Given** two pull requests whose `headRefName` equals the spec branch name
(numbers #4200 and #4282) and both `gh pr diff` and a local `git diff` are available, **when** the
command runs, **then** PR **#4282** is used (highest number wins), `## Blast radius` records
`2 pull requests found for branch {branch}; using #4282 (highest number).`, the measurement source
line names `gh pr diff #4282` and not a `git diff`, and only one set of blast-radius numbers is
reported.

**AC-47** *(FR-10, C-8)* **Given** the calibration branch `spec/scoped-lifetime-per-pipeline` is
checked out or merged (C-8) and its **local** branch tip is deliberately moved one commit behind
`origin/spec/scoped-lifetime-per-pipeline` (e.g. `git branch -f spec/scoped-lifetime-per-pipeline
spec/scoped-lifetime-per-pipeline~1` in a disposable clone, so the local and remote-tracking refs
resolve to **different** shas — this is a constructed setup step for the criterion, not an ambient
repository state to assume), **when** the command runs, **then** the measured ref is the
remote-tracking one, the metadata block and `## Blast radius` name that full ref and its sha (plus
the base ref, base sha and merge-base sha), and `## Blast radius` carries the line stating that the
local branch is at a different sha.

**AC-48** *(retired with F4 — CI state is no longer a risk factor; the number is not reused.)*

**AC-49** *(retired with the `Finding severity` definition — severity classification is no longer part
of this command; the number is not reused.)*

**AC-50** *(retired with the `Review round` definition — review-round decomposition is no longer part
of this command; the number is not reused.)*

**AC-51** *(FR-6, C-9, C-8)* **Given** a tree in which `docs/adr/` contains two files numbered `0070`
and two numbered `0071`, **when** the command runs against a spec whose `.adr-list` names one of
each, **then** every ADR reference in `show-me.md` carries the filename stem, each relative link
resolves to the file the `.adr-list` entry named, and no reference identifies an ADR by bare number
alone.

**AC-52** *(NFR-3)* **Given** a spec with 15 ADRs in `.adr-list` and a 229,159-byte `tasks.md`,
**when** the command runs, **then** it completes; its total *charged bytes* are ≤ 1,048,576; every
full-content read was preceded by a `wc -c` of that path and no read was issued whose measured size
exceeded the bytes then remaining; `tasks.md` is read **in full** (229,159 B, 22% of the budget)
rather than chunked or skipped; the 15 targeted ADR extracts are charged to the budget rather than
exempted from it; FR-9's task counts and FR-8's evidence are complete; and no value is reported as
zero or omitted because a read was skipped.

**AC-53** *(NFR-6)* **Given** the three delivered artefacts, **when** they are inspected, **then** the
command file has front matter with `allowed-tools`, `description` and `argument-hint` and consumes
`$ARGUMENTS` in the style of the other `.claude/commands/spec/*.md` files; the measurement script and
its test script both sit in `.claude/commands/spec/`; `git ls-files --stage` shows the awk program at
mode `100644` with a `#!/usr/bin/awk -f` first line, matching
`.claude/commands/adr/generate_adr_index.awk`, and the test script at mode `100755`;
`.claude/commands/spec/README.md` lists `/spec:show-me` in its command catalogue **and** documents the
measurement script; and every relative link in a generated `show-me.md` resolves from the spec
directory.

**AC-54** *(FR-8)* **Given** a complete spec declaring 28 top-level ids of which 26 are judged
`Shipped`, one is `Shipped with deviation` and one is `Deferred` with a recorded follow-up issue,
**when** the command runs, **then** `## Did it ship what it said?` contains exactly one
`Shipped as planned: 26 of 28 numbered requirements — {id list}.` line whose list names those 26 ids
in FR-then-NFR order with runs of three or more consecutive same-prefix ids collapsed to
`{first}–{last}`, exactly **two** deviation entries (the two non-`Shipped` ids, in id order, each
with status, reason and evidence, the `Deferred` one naming its follow-up issue number), **no**
per-requirement row or entry for any of the 26 shipped ids, and a count line reading
`Shipped: 26 · Shipped with deviation: 1 · Deferred: 1 · Dropped: 0 · Withdrawn: 0 · Unverifiable: 0
(of 28)`, whose six terms sum to 28.

**AC-55** *(FR-8, FR-11)* **Given** a complete spec in which every declared id is judged `Shipped`,
**when** the command runs, **then** the deviation part contains exactly the line
`No deviations: every numbered requirement shipped as stated.`, the shipped-as-planned line's `{k}`
equals `{total}`, the count line's `Shipped:` term equals `{total}` and every other term is 0, and F5
is `Low`.

**AC-56** *(FR-6, C-8)* **Given** spec 0036 on its calibration branch — 76 files changed under `src/`
spanning six immediate subdirectories of `src/`, and seven resolved ADRs in `.adr-list` — **when** the
command runs, **then** `## What changed and why` carries exactly one diagram; the diagram is a fenced
block of ≤ 40 lines with no line exceeding 100 characters; it is a ```` ```mermaid ```` block if it
draws a sequence, call flow or lifecycle and a plain fenced block if it draws a file or type
hierarchy; every symbol, type, file and component it names appears in the spec diff, in one of the
seven ADRs, or in a file recorded as read in `## Inputs used`; and the section contains no
`No diagram:` line.

**AC-57** *(FR-6)* **Given** a complete spec whose measured diff changes 2 files under `src/` in a
single immediate subdirectory, changes 0 public API declaration lines, and whose `.adr-list` resolves
to 1 ADR — so none of D1, D2 or D3 fires — **when** the command runs **and** it does not exercise
FR-6 (b)'s raise, **then** `## What changed and why` contains no fenced block at all and contains the
`No diagram:` line naming all four measured values (`2` files, `1` directory, `0` public API
declaration lines, `1` ADR); **and when** a run instead does exercise the raise, **then** it carries
one diagram and an explicit one-sentence reason for drawing it, and no `No diagram:` line.

**AC-58** *(FR-6, NFR-3)* **Given** a run in which D1 fires but NFR-3's read budget — including its
100,000-byte FR-6 reserve — is exhausted before the command can read the types the relationship
needs, **when** the command runs, **then** the
run still succeeds, `## What changed and why` contains no fenced block, it contains exactly the line
`No diagram: the read budget was exhausted before the relationship could be read accurately.`, and no
diagram names any type the command did not read.

**AC-59** *(NFR-2, FR-6)* **Given** a successful run that produced two diagrams totalling 70 fenced
lines of box-drawing and Mermaid syntax, **when** `show-me.md` is checked, **then** the NFR-2 word
count (which excludes every fenced-block line) is still between 400 and 2,000, neither fenced block
exceeds 40 lines or 100 columns, there are no more than two fenced blocks in the file, and FR-6's
prose still counts 150–600 words with the fenced lines excluded.

**AC-60** *(FR-14, FR-6)* **Given** a successful run in which `## Where to look first` carries a tree
diagram, **when** the section is inspected, **then** it still lists 3–7 path entries with their
reasons, the diagram consumes no path slot, every node in the tree that does not appear in the spec
diff is marked `(unchanged)`, every node resolves to a path tracked in git, and the section contains
no `No diagram:` line.

**AC-61** *(FR-6, FR-16 row 12)* **Given** a complete spec whose branch is not determinable, so no
diff is measured, **when** the command runs, **then** `## What changed and why` contains no fenced
block and contains exactly the line
`No diagram: spec branch not determinable, so no change could be drawn.`, and the trigger tests are
not evaluated.

**AC-62** *(FR-9, FR-11, FR-15, FR-18, Out of Scope)* **Given** a complete spec whose PR carries three
review rounds with findings and a full `statusCheckRollup`, **when** the command runs, **then**
`show-me.md` contains no finding count, no severity word attributed to a review, no
resolved/acknowledged/open tally, no round heading or line, no excluded-specification-phase count, no
CI check tally and no `F3`/`F4` row; `## Inputs used` has no review-comments row and no CI-checks
row; and no `gh` invocation in the run requested PR comments, reviews or `statusCheckRollup`.

**AC-63** *(NFR-3, FR-6)* **Given** a run that draws one diagram in `## What changed and why` and one
in `## Where to look first`, **when** the run's reads are totalled in *charged bytes* — including
every source file read for either diagram and every targeted ADR extract, and excluding only the
`wc -c` probes, which are not reads — **then** the total is ≤ 1,048,576; the two diagrams drew on one
shared 100,000-byte reserve rather than one each; and no per-diagram allowance was applied.

**AC-64** *(FR-6)* **Given** a run in which D2 fires (the spec diff changes 10 public API declaration
lines) but those ten lines are ten unrelated one-line property additions spread across ten otherwise
disconnected DTOs with no shared call path, flow, or hierarchy, **when** the command runs and
exercises (b)'s stand-down, **then** `## What changed and why` contains no fenced block, contains
exactly the stand-down fallback line naming which test fired and a one-sentence reason the evidence
does not cohere, and no diagram anywhere in the file names an invented relationship among those ten
types.

**AC-65** *(FR-6, FR-14)* **Given** a run in which a trigger test fires and the only relationship
worth drawing is the path-tree relationship among the paths `## Where to look first` already lists,
**when** the command runs, **then** `## Where to look first` carries that diagram under FR-14's
optional-tree rule, `## What changed and why` contains no fenced block and contains exactly the
FR-6 (e) **placed-elsewhere line** naming that the diagram is in `## Where to look first`, and the diagram
still satisfies FR-6 (c)'s attribution rule and (d)'s size caps.

**AC-66** *(Definitions — Public API declaration line, FR-10, FR-6 D2)* **Given** a spec diff
containing, in files under `src/`, one purely added declaration (`+    public void Foo()`), one
purely removed declaration (`-    protected int Bar;`) and one *modified* declaration rendered as the
pair `-    public void Create(Type t)` / `+    public void Create(Type t, string name)`, **when** the
command measures blast radius, **then** the reported count of changed public API declaration lines is
**4** (the modified declaration contributing 2, not 1), and the same number is the one D2 tests and
the one F1's sibling metric reads — the three never disagree.

**AC-67** *(Definitions — Immediate subdirectory of `src/`, FR-6 D1, FR-10, NFR-1)* **Given** a spec
diff that changes `src/Directory.Build.props` plus four files all inside
`src/Paramore.Brighter/`, **when** the command runs, **then** `## Blast radius` reports **5** changed
files under `src/` and **1** distinct immediate subdirectory of `src/`, D1 does **not** fire (its
second clause requires ≥ 2), and a second run over the same tree reports the identical pair.

**AC-68** *(FR-7, FR-16 row 5a, FR-15, NFR-3, NFR-7)* **Given** a spec for which `release_notes.md`
**does** contain a section, but NFR-3's read budget is exhausted before that section is reached,
**when**
the command runs, **then** `## Breaking changes` carries the row 5a line stating that a section
exists but was not read because the budget was exhausted, does **not** carry row 5's "no
release_notes.md section found" line, `## Inputs used` marks the `release_notes.md` row
`not available: read budget exhausted before release_notes.md section could be read`, FR-7's
item-grouping tie-break uses its no-catalogue fallback, the run succeeds, and `release_notes.md` is
byte-identical before and after.

**AC-69** *(FR-7, FR-15)* **Given** a spec for which `release_notes.md` contains a section and the
read budget is intact, **when** the command runs, **then** the section **is** read (the command has
no discretion to skip it), `## Inputs used` marks the `release_notes.md` row `used`, and neither
FR-16 row 5's nor row 5a's line appears anywhere in the output.

**AC-70** *(FR-21, NFR-1)* **Given** the delivered measurement script and the tracked fixture
`specs/9999-show-me-fixture/`, **when** it is invoked as
`awk -f .claude/commands/spec/show_me_facts.awk specs/9999-show-me-fixture` from the repository root,
**then** its stdout is a **single** well-formed JSON object and nothing else, it carries a
`schema_version` field and one named field per value FR-21 lists, its declared-id total is **3** and
its task fields report **2** checkboxes with **0** unchecked, its total size is ≤ 65,536 bytes, and
its exit code is `0`; **and when** the delivered command file is searched, **then** it contains no
shell pipeline computing any of those values itself.

**AC-71** *(FR-21, FR-3, NFR-8)* **Given** a target spec with an unchecked task and no pre-existing
`specs/{spec}/.show-me-ledger.json`, **when** the command is run, **then** it prints the FR-3 stop
message, `specs/{spec}/show-me.md` is not created, `specs/{spec}/.show-me-ledger.json` **does not
exist afterwards**, and `git status --porcelain` is byte-identical before and after; **and given**
the same spec with a pre-existing ledger, **when** the command is run, **then** that ledger is
byte-for-byte unchanged.

**AC-72** *(FR-21)* **Given** each of four states of the measurement script in turn — absent, present
but unreadable, present and readable but exiting non-zero, and present and exiting `0` but emitting
output that is not a single JSON object — **when** the command is run against an otherwise complete
spec, **then** in every case it prints FR-21's measurement-script stop message naming which of the
four applies, writes no `show-me.md` and no ledger, leaves the repository byte-for-byte unchanged,
and **does not** compute any measured value inline or emit a partial, guessed or zero-filled
`show-me.md`.

**AC-73** *(FR-21, FR-18, FR-4)* **Given** a successful run against a spec whose PR is discovered,
**when** the commands the run issued are inspected in order, **then** `gh pr list` was invoked at most
once and `gh pr diff` was invoked **at most once**, and `specs/{spec}/.show-me-ledger.json` afterwards
parses as JSON, contains the same PR number, diff source, bucket counts and public-API declaration
line count that `show-me.md` reports, and contains **no** raw diff text.

**AC-74** *(FR-4, NFR-8)* **Given** a successful run, **when** the repository is inspected afterwards,
**then** `specs/{spec}/.show-me-ledger.json` exists, `git check-ignore -v` reports it ignored by the
exact-match `.gitignore` entry `.show-me-ledger.json`, `git ls-files --error-unmatch` on it fails
(it is untracked), and `git status --porcelain` differs from its before-state only by
`specs/{spec}/show-me.md`; **and when** the command is run a second time, **then** the ledger's
contents are wholly replaced and no `.show-me-ledger-2.json` or `.show-me-ledger.json.bak` exists.

**AC-75** *(FR-17, NFR-5, FR-15)* **Given** a successful run, **when** `show-me.md` is searched,
**then** it contains no occurrence of the string `.show-me-ledger.json`, no occurrence of the
measurement script's path, and `## Inputs used` has **no row** for either — while every count it
reports is still attributed to the artefact it was counted from (NFR-7).

**AC-76** *(NFR-3)* **Given** a successful run, **when** the commands the run issued are inspected in
order, **then** every full-content read of a file was preceded by a `wc -c` of that same path; no
read was issued whose `wc -c`-measured size exceeded the bytes then remaining; the `wc -c` probes
themselves are charged nothing; and the run's total *charged bytes* are ≤ 1,048,576.

**AC-77** *(NFR-3, FR-10, FR-20)* **Given** a spec whose diff touches 517 files and whose full diff is
4,081,673 bytes, **when** the command runs, **then** no `git diff` or `gh pr diff` was issued without
either a pathspec restricting it or a summary-only flag (`--numstat`, `--name-only`, `--stat`); the
`src/`-scoped diff (303,715 bytes) **was** read; the run completed; and its total charged bytes are
≤ 1,048,576.

**AC-78** *(NFR-3, FR-6, C-8)* **Given** spec 0036 on its calibration branch, **when** the command
runs, **then** the run completes; `tasks.md` is read **in full** (229,159 bytes); the seven ADR
targeted extracts are charged to the budget (95,967 bytes) rather than exempted; at the point FR-6's
source reads begin, at least **100,000 bytes** remain available to them; `## What changed and why`
carries a diagram and **not** FR-6 (e)'s budget line; and the run's total charged bytes are
≤ 1,048,576.

**AC-79** *(NFR-9)* **Given** `.claude/commands/spec/show_me_facts_test.sh`, **when** it is run from
the repository root with the calibration branch present (C-8), **then** it invokes the measurement
script against `specs/9999-show-me-fixture/`, `specs/0033-pg-advisory-lock-sha256/`,
`specs/0002-sqs-cleanup/` and `specs/0036-scoped-lifetime-per-pipeline/`; asserts the declared-id
totals **3, 8, 0 and 37** and the checkbox totals **2, 5, 6 and 82** with zero unchecked in each;
exits `0` when every assertion holds; and, **when** any single asserted value is perturbed, prints
which assertion failed and exits **non-zero**; **and when** the script is inspected, **then** it
invokes no test framework, no `dotnet` command, and no file under `src/` or `tests/`.

**AC-80** *(NFR-9, NFR-2, FR-21)* **Given** a `show-me.md` whose counted body holds exactly **8**
tokens outside its fenced blocks and whose single fenced block holds **8** further tokens, **when**
the measurement script's word-count mode is run over it, **then** it reports **8**, not 16 — fenced
lines, `|`-leading lines, the H1/metadata block and everything from `## Inputs used` onward all
excluded per NFR-2; **and when** the same file is grown to a conforming body, **then** the script
reports a total inside 400–2,000 and says so.

**AC-81** *(NFR-9, FR-13)* **Given** the delivered command file, **when** the test script's FR-13
invariant check runs over it, **then** the check reports **no** branch on the risk level, and it does
so without matching the letters `if` inside an unrelated word — demonstrated by a fixture line
containing `git diff` and `gh pr diff` and no conditional, over which the check must report zero
matches.

**AC-82** *(FR-18, C-10, Out of Scope)* **Given** `.claude/settings.json` after this spec has shipped,
**when** it is inspected, **then** its `allow` array contains **exactly one** entry that was not
present before — `Bash(awk -f .claude/commands/spec/show_me_facts.awk:*)`; it contains no
`Bash(awk:*)`, `Bash(bash:*)`, `Bash(sh:*)` or `Bash(jq:*)` entry; its `gh` entries are unchanged and
still contain no `gh run` and no `gh api` entry; its `deny` array is unchanged; **and given**
`.gitignore`, **then** it contains exactly one added line, the exact-match `.show-me-ledger.json`,
with no wildcard form and no existing pattern edited.

## Additional Context

**Origin.** This command was proposed while wrapping up spec 0036
(`specs/0036-scoped-lifetime-per-pipeline/`) as the repo's own "show me" capability, bundled with a
risk-based merge assessment. The risk half exists because spec 0036's three implementation
review-response passes each ended in an ad hoc "what's left, is it safe?" read — a judgement made
three times, recorded zero times. Its sibling follow-up, "switching gears"
([#4357](https://github.com/BrighterCommand/Brighter/issues/4357), now closed — shipped separately
as ADR 0071/`/spec:gear`), is unrelated and explicitly out of scope here.

**Why review history and CI left this command's scope.** An earlier version of this document had
`## How it was built` recount the PR's review rounds — findings per round, severity splits,
resolved/acknowledged/open state — and made two of five risk factors out of review findings (F3) and
the CI rollup (F4). That was duplication of two tools that already own the material: the pull request
carries its own review comments and its own checks tab, and `/spec:review code` assesses the code
properly rather than by counting comments. The command's job is the end-of-sprint demo — *what did we
build, why does it look like this, where should you look* — and it doubles as documentation-seed
material for whoever writes the guide entry later. It is not a review, and a summary that recounts
findings reads as one. Cutting F3 and F4 also removed an entire apparatus that existed only to feed
them (the `Finding`, `Review round`, `Resolved`/`Acknowledged`/`Open finding`, `Finding severity` and
`CI check` definitions, and C-3's description of the two review workflows), which is why this document
is meaningfully shorter than the version it amends. The retired identifiers — factors F3 and F4,
FR-16 rows 3 and 4, C-3, and AC-38/39/48/49/50 — are deliberately **not reused**, so that every
surviving cross-reference in this document and in the implementation built against it keeps its
meaning.

**Why the output is a file, not chat output.** A chat summary dies with the session. `show-me.md`
sits in the spec directory next to `requirements.md` and `tasks.md`, is reviewable in the PR that
introduces it, and can be regenerated as the picture changes (FR-4).

**Why `release_notes.md` is independent.** `release_notes.md` is curated for users of the library
during the spec's DOC tasks: it is a breaking-change catalogue, not a change summary, and it is
deliberately not derived from anything. `show-me.md` serves a different reader (a reviewer or
teammate wanting the internal picture) and is therefore built from the spec's own artefacts, with
`release_notes.md` used only as optional corroboration (FR-7).

**Why the reconciliation is a narrative and not a table.** The old FR-8 rendered one table row per
declared requirement id. For a spec of this size that is roughly thirty rows, of which typically
twenty-eight say some variant of "yes, as planned" — a mechanical audit that buries the two rows a
reader actually needs. The narrative shape keeps the same status vocabulary, the same sub-clause
precedence and the same follow-up obligations, but renders the ordinary collectively (one
`Shipped as planned:` line, complete and checkable against the declared-id set) and the exceptional
individually. The partition invariant — every declared id appears exactly once, in the line or in the
list — is what keeps it as checkable as a table was, and it is the reason F5's thresholds can still
be read off the section deterministically once the statuses are settled. What changed honestly is
NFR-1's claim: the old document asserted the *row count* and *id set* were deterministic, which was
true but flattering, since the row count is just the id count and the statuses inside every row were
always judgement. The narrative form makes that split visible rather than hiding it behind uniform
rows.

**Why the diagram trigger is mechanical.** "Draw one if it would help" is not testable and two runs
would disagree. The three tests (D1, D2, D3) are computed from values `## Blast radius` and
`## Inputs used` already report, so deciding whether a diagram is warranted costs nothing extra and
is reproducible. The thresholds are deliberately generous rather than clever: five product files
across two projects, ten changed public declaration lines, or two ADRs are all signals that the
change has a *shape*, and a change with a shape is the case where prose is worst. Everything
downstream of the trigger — which relationship to draw, how to draw it, and whether to draw one
anyway when nothing fired — stays judgement, and NFR-1 says so rather than pretending otherwise.

**Open design question for the ADR amendment — where the diagram's reads happen.** FR-6's visual
explanation is the one part of `show-me.md` that may need to read source code the rest of the command
never opens: a genuine call-flow or component sketch cannot be re-derived from diff statistics, ADR
prose and requirement ids. `docs/adr/0072-show-me-command-resolution-and-output.md` currently splits
the command into a Measurer (which runs bounded shell commands into a fact ledger) and a Synthesiser
(which writes prose "from that ledger alone", with no new shell calls), under the rule that "nothing
crosses the line in either direction". A diagram sits across that line: the reading is measurement,
the drawing is synthesis. **This requirements document deliberately does not resolve it and does not
name any internal role.** FR-6 states only *what must be true* — the reads are charged to NFR-3's
single per-run byte budget and are the only reads permitted to draw on its 100,000-byte reserve,
everything named is attributable to a listed input (NFR-7), and a run that
cannot read enough emits FR-6 (e)'s budget line rather than guessing. Whether that is satisfied by
extending the Measurer's extraction step, by granting the Synthesiser a bounded read, or by some
other arrangement is a decision for the ADR amendment that follows this one. Nothing in FR-6 requires
or forbids any particular role split, and an implementation that keeps ADR 0072's current split
unchanged must still satisfy FR-6 or explain, in that ADR, why it cannot.

**Worked example — spec 0036 as the calibration case** *(requires the `spec/scoped-lifetime-per-pipeline`
branch, C-8; both a local and a remote-tracking form of that branch exist, so FR-10 resolves it)*.
Seven ADRs (`0070-per-pipeline-di-scope-for-mapper-and-transform-factories` through
`0076-scope-affinity-option-and-write-through`, named by stem because that tree also holds a *different*
ADR 0070 and a *different* ADR 0071 — C-9); 82 tasks, all checked; 363 commits since the merge base;
14 breaking-change items in `release_notes.md`, five of them carrying combined classifications; and a
branch whose diff against its merge base touches **517 files**, bucketed as **76 `src/`, 393 `tests/`,
24 `specs/`, 14 `docs/`, 0 `.github/`, 10 `other`** (root config files, `.claude/`,
`.agent_instructions/`) — six buckets summing exactly to 517, which is why FR-10 requires all six and
requires them to sum. Those 76 `src/` files span **six** immediate subdirectories of `src/`
(`Paramore.Brighter`, `Paramore.Brighter.Extensions.AspNetCore`,
`Paramore.Brighter.Extensions.DependencyInjection`, `Paramore.Brighter.ServiceActivator`,
`Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection`,
`Paramore.Brighter.ServiceActivator.Extensions.Hosting`), so FR-6's D1 test fires — and D3 fires too,
on seven ADRs. This is exactly the change a reviewer cannot hold in their head from prose: a scope
created in one project, adopted in another, disposed in a third. It is the case the visual
explanation exists for.

Under FR-11 this is F1 `High` (76) and F2 `High` (14), with F5 as measured from the reconciliation.
**Overall risk: High** under FR-12 — set by F1 and F2, since the overall level is the **maximum** of
the three factors and not their average. That is the right answer, and precisely the answer that was
previously only ever reached informally. It is also the case that shows why the raw 517 must never be
the blast-radius number quoted. Its PR's review history — three implementation rounds, eleven
findings, nine fixed and two accepted without a code change — is real and worth a reviewer's time,
and it is on the PR, which is where this command leaves it.

**Risk-model rationale.** A maximum-of-factors rule was chosen over weighted scoring because it is
reproducible, explainable in one sentence, and cannot be gamed by averaging a `High` away. Judgement
is preserved in the one safe direction only: the command may raise the level with a stated reason,
never lower it (FR-12). Two of the three factors (F2, F5) rest on synthesis rather than pure
counting, which NFR-1 says out loud rather than papering over with a determinism claim the document
cannot honour; the one mechanically countable factor (F1) is fully deterministic, and the calibration
case's `High` comes from that factor plus the judged F2, so the level is not hostage to the judged
factors alone. Dropping the two review/CI factors deliberately narrows what the level claims: it is a
read on the *shape* of the change — how much product code moved, what it breaks, and whether it
delivered what it promised — not on whether the code is correct or whether the build is green.
Those questions have owners. The narrowest question of all — whether the change should merge —
remains a human's, by construction (FR-13).

**Critical files for the design/implementation phase** (not requirements, kept here for
continuity):

- `.claude/commands/spec/requirements.md` — front-matter and sub-agent conventions the new command
  file must follow.
- `.claude/commands/spec/status.md` — closest prior art: reads spec metadata files,
  `.current-spec`, marker files, reports without mutating.
- `.claude/commands/spec/README.md` — command catalogue the new `/spec:show-me` entry must be
  added to; sub-agent and model policy.
- `docs/adr/0072-show-me-command-resolution-and-output.md` — the Measurer/Synthesiser split and the
  Step 5 extraction budget that FR-6's diagram reads have to fit inside.
- `.claude/settings.json` — the read-only `gh pr list`/`gh pr diff` entries the command must stay
  within, the existing `Bash(wc:*)` entry that already covers NFR-3's affordability probe, and the
  **one new path-scoped entry this spec adds** for FR-21's measurement script (FR-18, C-10).
