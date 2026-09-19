# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: N/A — originated from a wrap-up follow-up note in spec 0036 (`specs/0036-scoped-lifetime-per-pipeline/`), not a GitHub issue.

## Problem Statement

As the owner of a finished spec — and as the reviewer or teammate who has to form an opinion about
someone else's finished spec — I would like a single command that writes a durable, plain-language
summary of what the spec actually changed, together with a levelled, advisory read on how risky it
looks to merge, so that I can understand a spec's real shape and its merge risk without reading
seven ADRs, an 82-task `tasks.md`, 363 commit messages and three rounds of PR review comments, and
so that "is this safe to merge?" stops being an ad hoc judgement re-derived from scratch in every
session.

Today the raw material exists but is scattered and asymmetric in cost:

- The **ADRs** (`docs/adr/NNNN-*.md`) hold the decisions but not the outcome — an ADR says what was
  decided, not whether it shipped as decided.
- **`requirements.md`** holds the numbered FRs/ACs the spec promised, but nothing reconciles the
  promise against what landed.
- **`tasks.md`** holds the ground truth of what was built (spec 0036's is 229 KB, 82 tasks), which
  is precisely the document nobody outside the implementation session will read.
- **`release_notes.md`** holds the breaking-change catalogue, but it is written for a *user* of the
  library, not for a reviewer of the change.
- The **PR review history** (the repo's `claude-review`/`@claude` rounds — three *implementation*
  review rounds on spec 0036, 3 + 5 + 3 findings, of which nine were fixed and two were accepted
  without a code change) exists only as GitHub comments and is re-read from scratch every time.
  Spec 0036's PR also carries an earlier *specification-phase* review and several unrelated agent
  task-completion comments, which a naive count would wrongly fold into the review history — the
  definitions below exist to stop that.

Because nothing synthesises these, merge risk has been assessed implicitly and session-by-session:
each of spec 0036's review-response passes ended in an unstructured "what's left, is it safe?"
read, with no recorded factors, no comparable level, and no artefact anyone else could check.

## Proposed Solution

A new slash command, `/spec:show-me [spec-id]`, that sits alongside the existing `/spec:*` family
and runs **after** a spec's implementation is finished. It reads the spec's own artefacts (ADRs,
`requirements.md`, `tasks.md`), its git history, and — when they exist — its pull request, review
findings and CI results, and writes one markdown file, `specs/NNNN-name/show-me.md`, containing:

1. **What changed and why** — a plain-language narrative a teammate can read cold.
2. **Breaking changes** — what a consumer of the library would have to do differently, or an
   explicit statement that there are none.
3. **Did it ship what it said?** — every numbered requirement reconciled against what landed.
4. **How it was built** — task shape, commit shape, and the review-findings history.
5. **Blast radius** — the numbers: files and lines changed, bucketed by area.
6. **Risk assessment (advisory)** — a **Low / Medium / High** level, the factors that produced it
   with their measured values, and the rationale.
7. **Where to look first** — the handful of files a reviewer should open.
8. **Inputs used** — exactly which inputs were available and which were not.

The risk assessment is **advisory only, never a gate**: it produces a level and a rationale, it says
so in its own output, and nothing about the command's behaviour changes with the level. A human
still decides whether to merge.

The file is a spec deliverable like any other: it lives in the spec directory, is tracked in git,
and is regenerated (overwriting in place) whenever the picture changes — for example after a new
round of review findings lands.

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
| **Blast radius** | Four measured numbers over the spec diff: (a) changed files under `src/`, (b) changed files total, (c) net lines added/removed total, (d) changed **public API declaration lines** — added or removed lines in files under `src/` matching `^[+-]\s*(public\|protected)\b`. |
| **Breaking change item** | One distinct consumer-affecting change, carrying **one or more** classifications from the vocabulary `release_notes.md` already uses: **source**, **binary**, **behavioural**, **compatibility**. Combined markers are normal (`release_notes.md` uses "source and binary" on 5 of spec 0036's 14 items), so the classification is a set, not an exclusive choice. |
| **Finding** | One distinct issue raised against the spec's PR by an implementation review pass, drawn **only** from: an inline PR review comment, or a **numbered** item inside the tracking comment's **findings sequence** — the numbered/lettered list under whichever heading names new findings (e.g. `### New findings`, `## New findings`), or, if the comment names no such heading, the single numbered list that describes defects/risks/requested changes in the code (as opposed to a numbered or titled section that grades the *previous* round's fixes, e.g. `### Verdict on each fix`/`## Fix #1 — …: ✅`, which is evidence for `Resolved`/`Acknowledged` status, never a source of a new finding, even where one such verdict item is qualified or flags a residual concern). A numbered item that is itself a **container** for several distinct sub-issues (e.g. a "smaller items" bucket of five bullets, itself numbered as one item in the findings sequence) counts as one finding per sub-issue, not one. **A trailing titled-but-unnumbered aside after the findings sequence — e.g. a closing "Smaller notes" section — is never itself a source of findings, however many bullets it contains or what they assert**; this is what distinguishes it from a numbered container item, and it exists precisely so a review's own stylistic asides and questions don't inflate the finding count. **When the same review pass posts an issue both as an inline PR review comment and as a restatement inside its own tracking comment, this is one finding, not two — the inline comment is the finding of record**, and its bold title is what `Finding severity` matches against; the tracking-comment restatement is evidence of the same finding, not a second one, and an inline comment's authorship under a different bot login than its round's tracking comment (see **Review round** (a)) does not change this. Issues raised by a *specification-phase* review pass (see **Review round**) are not findings for FR-9/FR-11 purposes. |
| **Review round** | One **implementation** review pass over the spec's PR, identified by **content, not by comment count**. (a) *Grouping*: one continuous sequence of numbered or lettered findings is one round however many comments it spans — two or more comments form one round when they share an author, the later comment's numbering continues the earlier's without restarting (e.g. spec 0036's own "part 1 of 2" ends at finding 5 and "part 2 of 2" begins at finding 6), and they were posted within 15 minutes of each other; a comment whose numbering restarts at 1 begins a new round. **A round's inline PR review comments belong to that round regardless of author login even when it differs from the round's tracking-comment author** — this repository's review workflow posts a round's tracking comment and its inline comments under different bot logins (`claude` vs `claude[bot]`) — and are attributed to whichever round's tracking comment was posted within 15 minutes of them (before or after) and describes the same finding. (b) *Qualification*: a comment is part of a review round only if it contains at least one finding — a numbered item that names a file, symbol, requirement or behaviour in the change under review and asserts a defect, risk or requested change. Comments that report completion of an unrelated agent task, progress or status updates, CI notifications, and discussion replies raising no numbered findings are **not** review rounds, even when posted by the same bot on the same PR and under the same "Claude finished @user's task" preamble that a genuine round's own tracking comment also carries — the discriminator is the presence of numbered findings in the body, never the preamble text. (Spec 0036's PR carries six such finding-free task-completion comments dated 2026-09-16, alongside the three that do carry findings and are rounds; none of the six is a round.) A trailing unnumbered aside is likewise not a source of findings even inside a comment that otherwise is a round (see **Finding**). (c) *Phase exclusion*: a **specification-phase** review — one whose subject is the spec's own planning documents (`requirements.md`, `docs/adr/*`, `tasks.md`) rather than the implemented code — is not a review round. A pass is specification-phase when **any** of: (i) it self-identifies as a requirements/design/tasks review (e.g. produced by `/spec:review requirements\|design\|tasks`, or its title names the phase, as in "design only"); (ii) it was posted before the commit that first adds `specs/{target spec}/tasks.md` to the branch (by author date — a later rebase can move a commit's committer date without changing when the file was actually written, so committer date is not used for this test); (iii) every finding it raises cites only paths under `specs/{target spec}/` or `docs/adr/`, with no finding citing a source or test file in the diff. Otherwise the pass is an implementation review and counts. (Spec 0036's 2026-08-28 "design only" pass is excluded by tests (i) and (iii) — it self-identifies as design-only, and every one of its ten numbered items cites only `specs/`/`docs/adr/` paths; its 2026-09-16 and 2026-09-18 passes all count as implementation reviews.) |
| **Resolved finding** | A finding for which a commit on the spec branch, made after the finding was posted, changes the code or document the finding names; or which the PR thread marks resolved/outdated; or which the author's reply states was addressed by the fix made for another finding in the same round. |
| **Acknowledged finding** | A finding answered in the PR with a reply accepting it as understood but explicitly taking no code change ("acknowledged, no action", "by design", "deliberate, leaving as-is", "won't fix", "accepted for now, will revisit if it's hit", "tracked separately"). Acknowledged is **not** resolved. |
| **Open finding** | A finding that is neither resolved nor acknowledged. |
| **Finding severity** | The severity word the finding states, read **only** from a marker in the finding's **title** — a trailing parenthetical or italicised tag immediately after the title text (e.g. `*(medium)*`, `*(low — a question, not a defect)*`) or a leading bracketed tag (e.g. `[High]`). Matched case-insensitively after stripping markdown emphasis and surrounding punctuation, taking the first word of the marker and ignoring anything after a dash/colon within it (`*(low — a question, not a defect)*` → `low`). **A severity word appearing anywhere else — in the finding's body prose, or restated inside a tracking-comment summary of an inline finding (see `Finding`) — is not matched**; this is deliberate, so that a finding's prose (e.g. "…so low blast radius, but…") is never mistaken for a severity marker. The mapping is: `critical` → Critical; `high` → High; `medium` → Medium; `low` → Low; `nit` → Low. A finding with **no** title marker, or a marker word outside {critical, high, medium, low, nit}, is **unclassified** and is counted as `Medium`. |
| **CI check** | One entry in the `statusCheckRollup` array returned by `gh pr view {n} --json statusCheckRollup` for the PR's head commit. Its state/conclusion drives F4 (FR-11); no other CI source is consulted. |
| **Advisory** | Producing a level and rationale only: no gating, no blocking, no refusal, no label, no comment, no marker file, no change to any approval state, and no behavioural difference between a `Low` result and a `High` result. Whatever the level, the command completes its run and reports per FR-19. |

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
pre-existing `show-me.md` untouched — and prints one of:

- `tasks.md` absent: `Spec {dir} has no tasks.md — /spec:show-me runs only against a finished spec.
  Current phase: run /spec:status.`
- `tasks.md` present but with zero task checkboxes: `Spec {dir}'s tasks.md contains no task
  checkboxes — nothing to summarise.`
- `tasks.md` present with ≥ 1 unchecked checkbox: `Spec {dir} is not finished: {n} of {total} tasks
  are still unchecked. First unfinished: {first three unchecked task titles, one per line}.
  /spec:show-me runs only against a finished spec.`

This precondition check is the **only** circumstance in which the command declines to produce
output. It is a precondition on the *input*, not a judgement about the change (contrast FR-13).

#### Output file

**FR-4 — The command writes exactly one file, `show-me.md`, in the target spec directory, and
overwrites it in place on re-run.** The path is `specs/{target spec directory}/show-me.md`, lower
case, no suffix or timestamp in the name. If the file already exists, the command **replaces its
entire contents**; it does not append, does not create a numbered variant (`show-me-2.md`), does not
write a backup, and does not refuse. Prior versions are recoverable from git history; the command
does not manage them. The command does not `git add` or commit the file.

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

#### Section content

**FR-6 — `## What changed and why` is a synthesised plain-language narrative.**
150–600 words of prose (not a bullet dump, not a copy-paste of ADR *Decision* sections) that states:
what the spec set out to fix, what a user of Brighter can now do that they could not before (or what
now behaves differently), and the one or two decisions that most shaped the result. It must name
**every** ADR listed in the spec's `.adr-list` at least once, each with its title and current Status
from the ADR's own front matter/body.

**ADRs are identified by filename slug (the filename stem), never by bare number.** ADR numbers are
*not* unique in this repository — `docs/adr/` on `master` alone carries fourteen duplicated numbers,
including five files numbered `0037` and four numbered `0057` (C-9) — so a reference of the form
`[ADR 0070]` is ambiguous and is forbidden. Each ADR reference must give its title and link
relatively to the ADR file, with the filename stem visible in the link text, e.g.
`[Per-pipeline DI scope for mapper and transform factories
(0070-per-pipeline-di-scope-for-mapper-and-transform-factories)](../../docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md)`.
The same rule binds every other ADR reference anywhere in `show-me.md` (FR-7's evidence, FR-8's
evidence column, FR-15's `Inputs used` rows).

The narrative must not introduce an internal type name without a short gloss on first use.
*Example (spec 0036 — requires the calibration branch, C-8)*: the narrative must name the seven ADRs
`0070-per-pipeline-di-scope-for-mapper-and-transform-factories` through
`0076-scope-affinity-option-and-write-through` by slug and title, and must say, in ordinary words,
that `Scoped` mappers/handlers/transforms now share one DI scope per pipeline that is disposed when
the pipeline ends, and that an ASP.NET Core host can opt a pipeline in to adopting the ambient
request scope instead. Note that on that branch `docs/adr/` holds *two* files numbered 0070 and
*two* numbered 0071 — which is exactly why the slug, not the number, is the identifier.

**FR-7 — `## Breaking changes` enumerates consumer-affecting changes, each classified, or states
there are none.**
The section derives its content from the spec's **own** artefacts — the ADRs' *Consequences*
sections, `requirements.md`, and the public-API declaration lines in the spec diff. It lists each
*breaking change item* as one bullet: a one-sentence statement of what breaks, its classification —
**one or more** of source / binary / behavioural / compatibility, stated as a set (e.g. "source and
binary", "behavioural, and source and binary") — and the migration in one sentence. It ends with a
count line: `Total breaking-change items: {n}`. If there are none, the section contains exactly the
line `No breaking changes identified for this spec.` and the count line `Total breaking-change
items: 0`.

When `release_notes.md` happens to contain a section for this spec, the command **may** read it to
corroborate the list, and — if the counts disagree — must say so in one line
(`release_notes.md records {m} items; this summary identifies {n}`). `release_notes.md` is never
required, never modified, never a prerequisite, and a link to it is never required (see FR-17 and
Out of Scope).

*Example (spec 0036 — C-8)*: fourteen items, including `MapperLifetime.Scoped` no longer caching for
the life of the process (behavioural); `CreatePipelineScope()` added to six mapper/transformer
factory-and-registry interfaces in one item (source and binary); `IAmAHandlerFactory` gaining
`CreatePipelineScope()` **and**, in the same item, `IAmALifetime` gaining a `PipelineScope` member
(source and binary); and, as a separate item of its own, `IAmALifetime` also implementing
`IAsyncDisposable` (source and binary).

**When a `release_notes.md` section for the spec is read (FR-7 above), its grouping is the tie-break
for what counts as one item** — the command follows the catalogue's own bullet boundaries rather than
re-partitioning them, which is why the calibration example above groups `CreatePipelineScope()` and
`PipelineScope` into one item (the catalogue reports them in one bullet) while keeping
`IAsyncDisposable` separate (the catalogue gives it its own bullet). When no `release_notes.md`
section is read (FR-16 row 5), there is no catalogue to defer to, and the rule falls back to: one
item per distinct public-API declaration change or per ADR *Consequences* bullet describing a
behavioural break.

**FR-8 — `## Did it ship what it said?` reconciles every numbered requirement against what
landed.**
A table with one row per **top-level** numbered requirement in the spec's `requirements.md`, with
columns: requirement id, one-line paraphrase, status, and evidence (a task id from `tasks.md`, a file
path, or an ADR reference by slug).

*Which ids get a row (the counting rule)*: a numbered requirement is an id matching
`\b(FR|NFR)-(\d+)\b` that is **declared** in `requirements.md` — i.e. it appears at the start of a
requirement's heading or bold lead-in (`**FR-7 — …**`), not merely cross-referenced in another
requirement's prose. Coverage is 100%: every declared id gets exactly one row, and none is silently
dropped. Rows are ordered `FR-1 … FR-n`, then `NFR-1 … NFR-n`.

*Sub-numbered requirements*: a sub-numbered clause (`FR-27.3`, `NFR-1.2`) does **not** get its own
row. It is folded into the row for its top-level number (`FR-27`, `NFR-1`), and each sub-clause must
be individually addressed inside that row's paraphrase and evidence. When sub-clauses of one
requirement have different outcomes, the row takes the least-shipped status among them, using the
precedence `Shipped` < `Shipped with deviation` < `Unverifiable` < `Deferred` < `Withdrawn` <
`Dropped`, and the evidence column names which sub-clause differs.

*Status set*: status ∈ {`Shipped`, `Shipped with deviation`, `Deferred`, `Dropped`, `Withdrawn`,
`Unverifiable`}.

- `Withdrawn` — the requirement was explicitly removed or superseded by a **recorded decision** taken
  during the spec's own lifecycle (recorded in `requirements.md` itself, an ADR, a task, or the PR
  review thread), before or during implementation: it was never meant to ship in its stated form. A
  `Withdrawn` row must cite where the withdrawal is recorded.
- `Dropped` — not built, with no recorded decision to withdraw it. (This is the distinction: a
  withdrawal is a decision; a drop is an absence.)

Every row whose status is not `Shipped` must carry a one-sentence reason in the evidence column, and
`Deferred`/`Dropped`/`Withdrawn` rows must state whether a follow-up issue exists and its number, the
requirement that supersedes it, or `no follow-up recorded`. The table is followed by a short
`Shipped beyond the requirements` list naming work present in `tasks.md` that no numbered requirement
covers (or the line `Nothing shipped outside the numbered requirements.`), and a count line:
`Shipped: {a} · Shipped with deviation: {b} · Deferred: {c} · Dropped: {d} · Withdrawn: {w} ·
Unverifiable: {e} (of {total})`, where `{total}` equals the row count.

**FR-9 — `## How it was built` states the task shape, the commit shape and the review history.**
It states: total tasks and the count per task-type tag found in `tasks.md`
(`TEST + IMPLEMENT`, `STRUCTURAL`, `PROJECT`, `DOC`, plus `untagged`); the number of commits on the
spec branch since the merge base; and a review-history line per **implementation** review round (see
the `Review round` definition — specification-phase passes and non-review tracking comments are
excluded):
`Round {i}: {n} findings ({c} Critical, {h} High, {m} Medium, {l} Low, {u} unclassified) — {r}
resolved, {a} acknowledged, {o} open.` It ends with a totals line across rounds, and one further line
recording how many review passes were excluded as specification-phase, so the exclusion is auditable:
`Specification-phase review passes excluded: {n}` (or `none`). If no PR or no implementation review
rounds are found, it states that instead (FR-16).

`{m}` counts only findings whose severity word matched exactly `Medium`; `{u}` counts unclassified
findings (no severity word, or a word outside the defined set) — both are risk-scored as Medium by
F3 (Definitions, `Finding severity`), but are reported in separate slots here so a reader can see how
much of a round's Medium weight came from an actual severity call versus an absence of one.

*Example (spec 0036 — C-8)*: 82 tasks; three implementation review rounds — `Round 1: 3 findings
(0 Critical, 0 High, 0 Medium, 0 Low, 3 unclassified) — 3 resolved, 0 acknowledged, 0 open.` (round
1's three findings are its inline PR review comments, per `Finding`'s dedup rule — none of their bold
titles carries a title marker, even though the tracking comment's own restatement of one uses the
word "low" in body prose, which does not count) / `Round 2: 5 findings (0 Critical, 0 High,
2 Medium, 3 Low, 0 unclassified) — 3 resolved, 2 acknowledged, 0 open.` / `Round 3: 3 findings
(0 Critical, 0 High, 1 Medium, 2 Low, 0 unclassified) — 3 resolved, 0 acknowledged, 0 open.` (its
`*(nit)*` finding counts as Low); totals **11 findings, 9 resolved, 2 acknowledged, 0 open**; and
`Specification-phase review passes excluded: 1` (the 2026-08-28 design-only pass). The six unrelated
`@claude` task-completion comments on the same PR are not rounds and are not reported.

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

The section reports, over the *spec diff*: files changed and net lines (`+a/−b`) for each of the
buckets `src/`, `tests/`, `docs/`, `specs/`, `.github/`, `other` — **all six buckets are always
listed, including any that are zero, and the six file counts must sum to the reported total**; the
totals; the count of changed public API declaration lines; and the commit count.

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

**FR-11 — The risk level is computed from five named factors, each with stated thresholds and its
measured value cited.**
The section contains a table with one row per factor: factor, measured value, factor level
(`Low`/`Medium`/`High`). The factors and thresholds are:

| # | Factor | Low | Medium | High |
|---|--------|-----|--------|------|
| F1 | **Product-code blast radius** — files changed under `src/` | ≤ 10 | 11–50 | > 50 |
| F2 | **Breaking changes** — count of breaking change items (FR-7) | 0 | 1–3 | ≥ 4 |
| F3 | **Review findings** — state of findings across all implementation review rounds | ≥ 1 implementation review round ran, **and** every finding is resolved, **and** no finding is open or acknowledged | no PR found or no implementation review round ran; **or** ≥ 1 acknowledged finding; **or** ≥ 1 open finding of severity Medium or Low (including unclassified) | ≥ 1 **open** finding of severity High or Critical |
| F4 | **CI state** — the PR's `statusCheckRollup` for the head commit | a PR was found, the rollup returned ≥ 1 check, and **every** check's conclusion is `SUCCESS` or `SKIPPED` | no PR found; PR found but the rollup is empty or absent; **or** any check is `PENDING`/`IN_PROGRESS`/`QUEUED`, or concluded `NEUTRAL` or `ACTION_REQUIRED`; **or** any conclusion value this table gives no other rule for (unrecognised → Medium); **or** the result is not determinable (e.g. `gh` unavailable) | any check concluded `FAILURE`, `CANCELLED`, `TIMED_OUT` or `STARTUP_FAILURE` |
| F5 | **Requirement fidelity** — from FR-8's reconciliation | every numbered requirement is `Shipped` | ≥ 1 `Shipped with deviation` or `Unverifiable`; **or** ≥ 1 `Deferred`/`Dropped`/`Withdrawn` row that **does** state a follow-up issue or superseding requirement (i.e. not `no follow-up recorded`) — this includes a `Withdrawn` row that cites both the withdrawal decision and a follow-up/supersession | ≥ 1 `Deferred`, `Dropped`, or `Withdrawn` row stating `no follow-up recorded` |

F4 is defined directly from `gh pr view --json statusCheckRollup` and deliberately does **not** use
GitHub's branch-protection sense of "required check": that is a repository setting the command cannot
read within its allow-list (C-10), and several `ci.yml` jobs are fork-gated and legitimately report
`SKIPPED` on ordinary PRs. Treating `SKIPPED` as acceptable is what makes F4's `Low` column reachable
at all. The rollup is a total mapping: every state/conclusion value lands in exactly one column, with
Medium as the catch-all for anything unrecognised.

F3's `High` column fires only on an **open** High/Critical finding. This repository's review
workflows do not require a severity word, so findings are often unclassified — three of the
calibration case's eleven carry no title marker at all — and count as Medium; but even where a word
is given, `nit`/`low`/`medium` dominate in practice, so F3 reaches `High` rarely regardless. That is a
true reading of the evidence rather than a defect:
the classification rule above is total (every finding lands in exactly one severity bucket), and F3's
`Medium` column is what actually carries the common cases — acknowledged findings and open
unclassified findings.

F5's column mapping is likewise total: **any** row that is not `Shipped` — including a `Withdrawn`
row that cites both its decision and a follow-up/supersession — lands in Medium if it states a
follow-up issue or superseding requirement, alongside `Shipped with deviation`/`Unverifiable`; it is
not High, because High is reserved for a row whose gap is *unrecorded* (`no follow-up recorded`); and
it is not Low, because Low requires **every** row to be `Shipped`, with no exception.
*Example (AC-45)*: `FR-27`'s row is `Withdrawn`, citing both the withdrawal decision and a
superseding requirement — that row alone puts F5 in `Medium`, not `Low` and not `High`.

**When more than one of a factor's three column conditions is satisfied *collectively* by the
evidence that factor measures — a reconciliation table's rows for F5, the findings across *all*
implementation review rounds (not just one round) for F3, or the checks in a `statusCheckRollup` for
F4 — the factor takes the *highest* matching column** (High beats Medium beats Low). For example: one
`Deferred` row with a recorded follow-up alongside a separate `Dropped` row stating `no follow-up
recorded` puts F5 at High; an acknowledged finding in one round alongside a separate open Critical
finding in another round puts F3 at High; a rollup containing one `NEUTRAL` check alongside a separate
`TIMED_OUT` check puts F4 at High (this is the rule AC-48's third case relies on). This one rule is
what makes every factor's mapping total over its full body of evidence, not just against a single
row/finding/check considered on its own.

Each row must cite the value it measured (e.g. `76 files under src/`, `14 items`, `11 findings over
3 implementation review rounds; 9 resolved, 2 acknowledged, 0 open`, `18 checks: 14 SUCCESS, 4
SKIPPED`), not merely the level.

**FR-12 — The overall level is the highest factor level, and may be raised but never lowered.**
The overall level is the maximum of the five factor levels (`Low` < `Medium` < `High`). The command
may state a **higher** overall level than the maximum if it gives an explicit one-sentence reason
naming what the factors miss; it may **never** state a lower one. The section states the level on
its own line in the form `**Overall risk: {Low|Medium|High}**`, followed by 2–5 sentences of
rationale that reference at least the factor(s) that set the level.

*Example (spec 0036 — C-8)*: F1 `High` (76 `src/` files), F2 `High` (14 breaking-change items), F3
`Medium` (11 findings over 3 implementation review rounds — 9 resolved, **2 acknowledged**, 0 open;
`≥ 1 acknowledged finding` puts F3 in the Medium column), F4 as measured from the rollup, F5 as
measured from the reconciliation → **Overall risk: High**, set by F1 and F2 as the maximum. F3 being
`Medium` rather than `Low` does not change the overall level, because the overall level is the
maximum of all five factors, not their average.

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

**FR-15 — `## Inputs used` records provenance for every input, present or absent.**
A table with one row per input — `requirements.md`, `tasks.md`, `.adr-list` (and one row per ADR it
names, identified by slug), `.issue-number`, `release_notes.md` section, git history, pull request,
review comments, CI checks — each marked `used` or `not available: {one-line reason}`. This section
is what makes FR-16's degradation auditable, and it is excluded from the length budget in NFR-2.

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

#### Degradation, provenance and safety

**FR-16 — Every optional input has a defined absence behaviour; no absence fails the command.**
When the FR-3 precondition holds, the command always produces `show-me.md`. Absent inputs are handled
exactly as follows (each row has a matching acceptance criterion):

| # | Absent input | Behaviour |
|---|---|---|
| 1 | No PR found for the spec branch (FR-20) | `How it was built` states `No pull request found for branch {branch} — no external review findings available.`; F3 = Medium, F4 = Medium; blast radius measured from `git diff` (FR-10, FR-20). |
| 2 | `gh` unavailable, unauthenticated, or offline | Same as row 1, with the reason recorded in `Inputs used` as `not available: gh unavailable`; F3 = Medium, F4 = Medium. Blast radius still measured from git. |
| 3 | PR exists but no implementation review round ran | `How it was built` states `Pull request #{n} has no recorded implementation review rounds.` (plus the specification-phase exclusion count, FR-9); F3 = Medium. |
| 4 | No CI checks for the head commit (rollup empty or absent) | `Risk assessment (advisory)` records `CI: no checks found for {sha}` as F4's measured value; F4 = Medium. |
| 5 | No `release_notes.md` section for the spec | `Breaking changes` is derived from ADRs and the diff, and adds the line `No release_notes.md section found for this spec; this list is derived from the ADRs and the diff.` No failure, no modification of `release_notes.md`. |
| 6 | `.adr-list` missing or empty | `What changed and why` states `No ADRs recorded for this spec.` and is synthesised from `requirements.md`, `tasks.md` and the commits. No factor consequence. |
| 7 | An entry in `.adr-list` cannot be resolved to exactly one file in `docs/adr/` | `.adr-list` entries are ordinarily the full filename including its `.md` extension (e.g. `0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md`), which resolves unambiguously on its own. If an entry is instead a bare number (or otherwise doesn't match any file): `What changed and why` names it as `{entry} — ADR file not found in docs/adr/.` If it's a bare number matching more than one file (C-9): `{entry} — ambiguous ADR number, matches: {filenames}; .adr-list should name the full filename instead.` Either way the narrative continues with the remaining ADRs, the entry is marked `not available` in `Inputs used`, and the run succeeds. No factor consequence. |
| 8 | `requirements.md` missing | `Did it ship what it said?` contains exactly `No requirements.md found for this spec — scope reconciliation is not possible.`; F5 = Medium. |
| 9 | `requirements.md` present but declaring zero numbered requirements (FR-8's counting rule) | `Did it ship what it said?` contains exactly `requirements.md declares no numbered requirements — nothing to reconcile.`, with no table and no count line; F5 = Medium. |
| 10 | `.issue-number` missing, empty or whitespace-only | The metadata block's linked issue reads `none`; `Inputs used` marks `.issue-number` as `not available: not present`. No factor consequence. |
| 11 | `PROMPT.md` (or a `PROMPT-*.md` companion) absent | No mention anywhere in the output; its absence is normal and is not recorded in `Inputs used`. |
| 12 | Spec branch not determinable (FR-10) | `Blast radius` states `Spec branch not determinable — no diff measured.` followed by the rules it tried; F1 = Medium. |
| 13 | Spec branch not determinable — effect on `Where to look first` (FR-14) | The section contains exactly: `No diff measured — spec branch not determinable, so no files can be ranked. Start from specs/{spec dir}/tasks.md and the ADRs listed in specs/{spec dir}/.adr-list.` It lists no paths, and FR-14's 3–7-path rule does not apply. |
| 14 | Spec branch not determinable — effect on `Breaking changes` (FR-7) | The item list is derived from the ADRs' *Consequences* sections and `requirements.md` only, and the section adds the line `No diff measured — this list is derived from the ADRs and requirements.md only; public-API declaration lines could not be inspected.` The count line is still present, and F2 is computed from the items found. |
| 15 | Spec branch not determinable — effect on the metadata block (FR-5) | The metadata block's spec-branch, head-sha and merge-base-sha lines are each replaced with the single word `undetermined`. The base ref (FR-10's `origin/master`-or-`master` rule) does **not** depend on the spec branch and is still resolved and named normally. The PR reference reads `none found` — FR-20's PR discovery needs the spec branch's name to query `gh pr list --head`, which is unavailable in this state. Generation date, spec directory and issue are populated normally; the metadata block is still present in full. |

**FR-17 — `PROMPT.md` may inform the narrative but is never cited, and no untracked path is
referenced.**
When a `PROMPT.md`/`PROMPT-*.md` exists it may be read as background, but `show-me.md` — a tracked
file — must not link to it, quote it as a source, or name it as evidence in any table. The rule is
stated in terms of **git tracking, not gitignore status**: `show-me.md` must reference only paths
that are tracked in git (verifiable with `git ls-files --error-unmatch {path}`). This matters because
only literal `PROMPT.md` is gitignored (`.gitignore` carries an exact-match entry, not a `PROMPT*.md`
glob); the companion files (`PROMPT-calls.md`, `PROMPT-history.md`, …) are merely **untracked**. The
conclusion is the same for both — a reference would dangle for every other reader — but the test is
"not tracked in git", which covers both cases. The same rule applies to any other untracked path
(e.g. `specs/**/.current-gear`). Every path, ADR and issue reference written into `show-me.md` must
resolve for a reader who has only the repository.

**FR-18 — The command performs no writes other than `show-me.md`.**
It must not modify `release_notes.md`, `requirements.md`, `tasks.md`, any ADR, any approval marker
(`.requirements-approved`, `.design-approved`, `.tasks-approved`, `.code-approved`),
`specs/.current-spec`, or `.current-gear`. It must not stage, commit, push, branch, checkout, stash
or rebase. It must not post, edit or resolve any GitHub comment, apply or remove a label, or change
PR state.

Its GitHub access is read-only and confined to `gh pr view`, `gh pr list` and `gh pr diff`, which are
exactly the PR entries already present in the repository's `.claude/settings.json` allow-list
(`Bash(gh pr view:*)`, `Bash(gh pr list:*)`, `Bash(gh pr diff:*)`, plus `gh issue view`/`gh issue
list` which this command does not need). In particular, **CI state (F4) is derived from
`gh pr view {n} --json statusCheckRollup`**, which is covered by the existing `gh pr view:*` entry
and returns each check run's name, status and conclusion — enough to evaluate F4 without any
workflow-run query. The command therefore requires **no new allow-list entry**, and no `gh run`,
`gh api` or `gh checks` invocation is permitted. (This is the review's option (a): `statusCheckRollup`
supplies per-check conclusions, so adding a settings entry as part of this spec would be unnecessary
scope.)

**FR-19 — The command reports a short summary to the caller.**
On success it prints, in the session: the path written, whether it created or replaced the file, the
overall risk level, and the one-line reminder that the level is advisory. On any FR-1/FR-2/FR-3 stop
it prints only that stop message.

### Non-functional Requirements

**NFR-1 — Deterministic mechanical fields; judgement-derived fields are named as such.**
For unchanged inputs (same measured ref and head sha, same PR state, same CI rollup), two runs must
produce **identical** values for every *mechanically countable* field:

- the section set and order;
- every number in `## Blast radius` (per-bucket file counts, net lines, public-API declaration-line
  count, commit count) and the named source/refs/shas;
- the task total and per-tag counts;
- the CI check tally;
- the FR-8 **row count** and the set of requirement ids that appear;
- factor levels F1 and F4, which derive purely from those counts.

Three fields are **judgement-derived synthesis and are not required to be identical between runs**:
FR-7's breaking-change item list and its count (and therefore F2); FR-8's per-requirement status
values (and therefore F5); and the review-round decomposition itself (and therefore F3) — deciding
what counts as one distinct finding (the `Finding` definition's container/sub-issue split), whether a
given comment "asserts a defect" (the `Review round` definition's qualification test), and whether a
reply's wording counts as resolving a finding (the `Resolved finding` definition's "states was
addressed by the fix made for another finding" clause) are each a judgement call, not a mechanical
count — round 2 of spec 0036's own calibration history turns on exactly this ("closed for free by
#1's fix" is a reading of prose, not a count). All three fields must stay grounded in — and must
never contradict — the evidence cited in the same row or bullet, but their wording, granularity and
the boundaries between items may vary. Because F2, F3 and F5 feed FR-12's maximum, the **overall
level may in principle vary with them**; this is acknowledged rather than asserted away. The prose
(FR-6 narrative, FR-12 rationale, FR-14 reasons) is likewise not required to be byte-identical between
runs.

**NFR-2 — Readable in one sitting, with a mechanically countable budget.**
The counted body of `show-me.md` must be between **400 and 2,000 words**. The count is defined
mechanically so it can be checked by a script: count whitespace-delimited tokens containing at least
one alphanumeric character, over every line of the file **except** (a) the H1 and the metadata block
above the first H2, (b) any line that begins with `|` after trimming (pipe-table rows, including
separator rows), and (c) every line from the `## Inputs used` heading to the end of the file.
Everything else counts, including H2 headings, prose paragraphs, **and bullet lists** — FR-7's
breaking-change items and FR-14's "where to look" entries count toward the budget.

The upper bound is 2,000 rather than 1,500 because the calibration case's own content volume does not
fit in 1,500: spec 0036 would produce up to 600 narrative words (FR-6), 14 breaking-change bullets,
up to 7 "where to look" bullets with reasons, three review-round lines, and FR-12's rationale — a
plausible 1,600–1,750 counted words with every requirement satisfied. A budget a conforming output
cannot meet is not a budget. The `What changed and why` section must remain understandable by a
contributor who has not read the spec's ADRs.

**NFR-3 — Bounded cost, with a stated degradation when the budget binds.** A single run must complete
without reading the full spec diff into context: it may read summary statistics
(`--numstat`/`--name-only`) for the whole diff, and the full content of at most **25** individual
files. Targeted reads of each ADR named in `.adr-list` — its front matter plus its `Status` and
`Consequences` headings — do **not** count toward the 25, because they are small and bounded by the
ADR list length; this keeps FR-6/FR-7's per-ADR obligations satisfiable for a spec with many ADRs.

When a required artefact is too large to read whole (spec 0036's `tasks.md` is 229 KB), the command
must **degrade by chunking, never by skipping**: it reads the file in bounded chunks, or by targeted
extraction of the constructs it needs (checkbox lines, headings, task-type tags, tables), and must
never silently omit data needed for FR-3's completeness check, FR-8's per-requirement evidence, or
FR-9's task-shape counts. If any required extraction could not be completed, the affected value is
reported as `Unverifiable` (FR-8) or `not available` (FR-15) with the reason — it is never reported
as zero or omitted.

**NFR-4 — Works offline.** With no network and no `gh`, the command must still produce a complete
`show-me.md` from local artefacts and git alone, degrading per FR-16 and recording the reason in
`Inputs used`.

**NFR-5 — No secrets, no leakage.** The command must not write tokens, credentials, absolute
machine-local paths, or the contents of files that are **not tracked in git** into `show-me.md` (the
same tracking test as FR-17, which covers both gitignored and merely untracked paths). All repository
paths written are relative to the repository root.

**NFR-6 — Convention conformance.** The command file follows the existing `/spec:*` conventions
(front matter with `allowed-tools`, `description`, `argument-hint`; `$ARGUMENTS`; documented in
`.claude/commands/spec/README.md` alongside the other commands). `show-me.md` is valid markdown whose
relative links all resolve from its location in the spec directory.

**NFR-7 — Traceable claims.** Every factual claim in `show-me.md` must be attributable to a listed
input: counts to the artefact they were counted from, requirement statuses to a task id / file path /
ADR slug, and findings to the PR. The command must not assert behaviour it has not read evidence for.

**NFR-8 — Safe re-run.** Re-running the command on a spec whose inputs have changed (e.g. a new
review round) must leave the repository in a state differing only in `show-me.md`'s contents, and a
stop under FR-1/FR-2/FR-3 must leave the repository byte-for-byte unchanged.

### Constraints and Assumptions

- **C-1** Spec directories follow `specs/NNNN-name/`, and spec ids are **not unique** — FR-1's
  ambiguity handling is required, not defensive. Spec directory names may also contain spaces
  (`specs/0021-Expose Unacceptable Message Window/`), which is why FR-1 takes the whole argument.
- **C-2** `tasks.md` records completion as markdown checkboxes in the established style
  (`- [x] **TEST + IMPLEMENT: T1.5 — …**`), and task-type tags are one of
  `TEST + IMPLEMENT` / `STRUCTURAL` / `PROJECT` / `DOC`.
- **C-3** The repository has **two** paths by which a Claude review lands on a PR: the `claude-review`
  label workflow (`.github/workflows/claude-code-review.yml`) and the `@claude` mention workflow
  (`.github/workflows/claude.yml`). Every review pass on the calibration PR came through the latter.
  Both post findings as inline comments plus a tracking comment, and **neither prompts for a severity
  word**, which is why an unclassified finding is counted as Medium and why the severity matching in
  Definitions is case-insensitive and maps `nit`. The `@claude` workflow also fires for non-review
  tasks, producing tracking comments that are not review rounds — hence the `Review round`
  definition's qualification rule.
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
  — exist **only on the branch `spec/scoped-lifetime-per-pipeline`**. They are not on `master` and
  not on the branch this command is being built on. Every acceptance criterion marked *(C-8)* in its
  citation (AC-1, AC-2, AC-4, AC-12, AC-17, AC-18, AC-20, AC-47, AC-51) therefore carries a **test
  precondition**: that branch must be checked out, or merged into the branch under test, at the time
  the criterion is exercised — the inline *(C-8)* marker on each such AC is the single source of
  truth for which criteria this covers, not this prose list, which exists only to explain why the
  marker is there. (AC-48 is not among them: its Given is a hypothetical CI rollup that requires no
  particular spec fixture and runs on any branch.) This is a documented fact about
  where the fixture data lives, not a defect to design around: spec 0036 is the intended real-world
  calibration case for this command, and it is retained deliberately. Criteria that must be runnable
  anywhere use fixtures available on `master` (the three `0002-*` directories for ambiguity,
  `specs/0021-Expose Unacceptable Message Window/` for whitespace, `specs/0037-show-me/` for the
  missing-`tasks.md` case).
- **C-9** **ADR numbers are not unique anywhere in this repository** — not merely across branches.
  `docs/adr/` on `master` today carries duplicate numbers 0037 (five files), 0038, 0039, 0040, 0041,
  0042, 0043, 0051, 0053, 0054, 0057 (four files), 0061, 0063 and 0064. Per
  `.agent_instructions/adr_frontmatter.md`, the ADR **number is a non-unique ordering hint** and the
  **identity is the filename stem**; renumbering is explicitly rejected and "a number collision with
  a concurrent branch is acceptable". This is permanent and structural, which is why FR-6 forbids
  bare-number ADR references throughout `show-me.md` and FR-16 row 7 defines what happens when an
  `.adr-list` entry cannot be resolved to exactly one file.
- **C-10** The repository's `.claude/settings.json` `gh` allow-list is exactly `gh pr view`,
  `gh pr list`, `gh pr diff`, `gh issue view`, `gh issue list` — there is **no** run-query or
  `gh api` entry. FR-18 and F4 are defined to live inside that list (`gh pr view --json
  statusCheckRollup`), so this spec introduces no settings change.
- **A-1** Assumption: a finished spec's `tasks.md` is an accurate record of what was built — the
  command reconciles against it rather than re-deriving intent from the diff.
- **A-2** Assumption: the reader of `show-me.md` knows Brighter as a library but has not read this
  spec's ADRs, `tasks.md`, or PR history.

### Out of Scope

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
- **Any change to `.claude/settings.json`'s allow-list.** The command is defined to fit the existing
  entries (C-10, FR-18).
- **Summarising or counting specification-phase reviews** beyond the single excluded-count line
  required by FR-9. `/spec:review` owns those.
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
- **Re-litigating spec content**: the command summarises and assesses; it does not review code,
  raise new findings, or duplicate `/spec:review`.

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

**AC-7** *(FR-3, NFR-8)* **Given** a target spec with an existing `show-me.md` and an unchecked task,
**when** the command is run, **then** the existing `show-me.md` is byte-for-byte unchanged and no
other file in the repository is modified.

**AC-8** *(FR-3)* **Given** `specs/0037-show-me/` has no `tasks.md`, **when** `/spec:show-me
0037-show-me` is run, **then** no file is written and the output says the spec has no `tasks.md`.

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
`## What changed and why` is 150–600 words, names all seven ADRs by filename stem and title with
their current Status, links each with a relative path that resolves from the spec directory, and
contains **no** reference of the bare-number form `ADR 0070` unaccompanied by its filename stem.

**AC-13** *(FR-7)* **Given** a spec that introduced breaking changes, **when** the command runs,
**then** every listed item carries **one or more** of the classifications
source/binary/behavioural/compatibility (a combined value such as "source and binary" is valid) and a
one-sentence migration, and the section ends with `Total breaking-change items: {n}` matching the
number of bullets listed.

**AC-14** *(FR-7, FR-16 row 5)* **Given** a spec with no section in `release_notes.md`, **when** the
command runs, **then** the run succeeds, the section states that no `release_notes.md` section was
found and that the list is derived from the ADRs and the diff, and `release_notes.md` itself is
unmodified.

**AC-15** *(FR-8)* **Given** a `requirements.md` declaring FR-1…FR-12 and NFR-1…NFR-5 — including at
least one sub-numbered clause such as `FR-7.2`, and cross-references to those ids inside other
requirements' prose — **when** the command runs, **then** the reconciliation table has exactly **17**
rows (one per declared top-level id; sub-numbered clauses fold into their parent row and
cross-references create no rows), every row has a status from the allowed set, every non-`Shipped`
row has a reason, every `Deferred`/`Dropped`/`Withdrawn` row states a follow-up issue number, a
superseding requirement, or `no follow-up recorded`, and the count line sums to 17.

**AC-16** *(FR-8, FR-16 row 8)* **Given** a complete spec with no `requirements.md`, **when** the
command runs, **then** the run succeeds, the section contains exactly the defined "not possible"
line, and F5 is `Medium` in the risk table.

**AC-17** *(FR-9, C-8)* **Given** spec 0036's PR (#4282) with **three implementation review rounds**
of 3, 5 and 3 findings — the 2026-09-16 round all fixed, the 2026-09-18 07:44 round with 3 fixed and
2 accepted without a code change, the 2026-09-18 11:39 round all fixed — plus one earlier
design-phase review pass and six unrelated `@claude` task-completion comments on the same PR,
**when** the command runs, **then** `## How it was built` reports **exactly three** rounds with
finding counts 3, 5 and 3, per-round severity splits of (0/0/0/0/3 unclassified), (0/0/2/3/0) and
(0/0/1/2/0) in (Critical/High/Medium/Low/unclassified) order, per-round resolved/acknowledged/open
splits of 3/0/0, 3/2/0 and 3/0/0, a totals line of **11 findings / 9 resolved / 2 acknowledged / 0
open**, the line
`Specification-phase review passes excluded: 1`, no round derived from any task-completion comment,
the 82-task total with its per-tag breakdown, and the branch commit count.

**AC-18** *(FR-10, FR-20, C-5, C-8)* **Given** spec 0036's branch, **when** the command runs, **then**
`## Blast radius` reports all six bucket file counts — `src/` 76, `tests/` 393, `specs/` 24, `docs/`
14, `.github/` 0, `other` 10 — which sum to the reported total of **517**; reports net lines; reports
the changed public-API declaration-line count; and names exactly one measurement source with its
refs and shas (`gh pr diff #4282 (head {sha})` or `git diff {merge-base sha}..{head sha}`).

**AC-19** *(FR-10, FR-16 rows 12 and 15)* **Given** a spec whose branch cannot be resolved by any of
FR-10's three rules, **when** the command runs, **then** the run still succeeds, `## Blast radius`
states the branch is not determinable and lists the rules tried, F1 is `Medium`, the metadata block's
spec-branch, head-sha and merge-base-sha lines each read `undetermined`, its base-ref line still
names the resolved base ref normally, its PR-reference line reads `none found`, and generation date,
spec directory and issue are populated normally.

**AC-20** *(FR-11, C-8)* **Given** spec 0036 (76 `src/` files, 14 breaking-change items, 11 findings
over 3 implementation review rounds with 9 resolved and **2 acknowledged**), **when** the command
runs, **then** the risk table shows F1 `High` citing `76`, F2 `High` citing `14`, and F3 **`Medium`**
citing the `11 findings / 9 resolved / 2 acknowledged / 0 open` tally — `Medium`, not `Low`, because
F3's Low column requires that no finding be acknowledged.

**AC-21** *(FR-11)* **Given** a spec with 4 `src/` files changed, 0 breaking changes, one review round
whose 2 findings are both resolved, a `statusCheckRollup` on the head commit in which every check
concluded `SUCCESS`, and every requirement `Shipped`, **when** the command runs, **then** all five
factors are `Low`.

**AC-22** *(FR-12)* **Given** factor levels F1 `Low`, F2 `Medium`, F3 `Low`, F4 `Low`, F5 `Low`,
**when** the command runs, **then** the output contains `**Overall risk: Medium**` and the rationale
references F2.

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
**when** `## Inputs used` is inspected, **then** it marks the pull request, review comments and CI
checks rows as `not available` with a reason, marks `release_notes.md` as not available, and marks
`tasks.md`, `requirements.md`, `.adr-list` and git history as `used`.

**AC-28** *(FR-16 row 2, NFR-4)* **Given** `gh` is unavailable or unauthenticated, **when** the
command runs against a complete spec, **then** it still writes a complete `show-me.md`, records
`gh unavailable` in `Inputs used`, sets F3 and F4 to `Medium`, and measures blast radius from
`git diff`.

**AC-29** *(FR-17, NFR-5)* **Given** a spec directory containing a `PROMPT.md` and a
`PROMPT-history.md`, **when** the command runs, **then** `show-me.md` contains no occurrence of
either filename, no link to either, and no reference to any path that
`git ls-files --error-unmatch` reports as untracked; every relative link in `show-me.md` resolves to
an existing tracked repository path.

**AC-30** *(FR-18)* **Given** a successful run, **when** `git status --porcelain` is compared before
and after, **then** the only difference is `specs/{spec}/show-me.md`, and no commit, push, branch,
label, comment or PR state change occurred; **and when** the commands the run issued are inspected,
**then** every `gh` invocation is one of `gh pr view`, `gh pr list`, `gh pr diff`, and none is
`gh run`, `gh api` or `gh checks`.

**AC-31** *(FR-19)* **Given** a successful run, **when** the session output is read, **then** it
states the written path, created-vs-replaced, the overall level, and that the level is advisory.

**AC-32** *(NFR-1)* **Given** two runs against an unchanged measured ref, head sha, PR state and CI
rollup, **when** the two `show-me.md` files are compared, **then** every number in `## Blast radius`,
the named source/refs/shas, the task and per-tag counts, the CI check tally, the FR-8 row count and
the set of requirement ids, and factor levels F1 and F4 are identical. The round/finding/severity/
resolution counts (and therefore F3), F2, F5, the overall level and all prose are **not** required to
be identical; the test instead asserts that each run's F3, F2 and F5 values are consistent with the
evidence cited in the same run's own `## How it was built` lines, FR-7 bullets and FR-8 rows
respectively.

**AC-33** *(NFR-2)* **Given** any successful run, **when** the body of `show-me.md` is word-counted by
NFR-2's mechanical rule (excluding the H1/metadata block, all lines beginning with `|`, and everything
from `## Inputs used` onward; including headings and bullet lists), **then** the count is between 400
and 2,000.

**AC-34** *(NFR-7)* **Given** any successful run, **when** each count and status in `show-me.md` is
checked, **then** each is attributable to an input listed in `## Inputs used` and matches that
input's actual content.

**AC-35** *(FR-1)* **Given** `specs/0021-Expose Unacceptable Message Window/` exists on `master`,
**when** `/spec:show-me 0021-Expose Unacceptable Message Window` is run (argument unquoted, four
whitespace-separated words), **then** the whole argument text is taken as the spec id and the target
spec is `specs/0021-Expose Unacceptable Message Window/` — not a failure, and not a match attempted
on `0021-Expose` alone; **and when** `/spec:show-me README.md` is run, **then** no spec matches,
because `specs/README.md` is a file and not a candidate.

**AC-36** *(FR-14, FR-16 row 13)* **Given** a complete spec whose branch is not determinable, so no
diff is measured, **when** the command runs, **then** the run succeeds and
`## Where to look first` contains exactly the FR-16 row 13 fallback line, lists no paths, and FR-14's
3–7-path rule is not applied.

**AC-37** *(FR-7, FR-16 row 14)* **Given** the same no-diff run, **when** `## Breaking changes` is
inspected, **then** its items are derived from the ADRs' *Consequences* sections and
`requirements.md` only, it contains the FR-16 row 14 line stating that public-API declaration lines
could not be inspected, it still ends with `Total breaking-change items: {n}`, and F2 is computed
from the items listed.

**AC-38** *(FR-9, FR-16 row 3)* **Given** a PR exists for the spec branch but no implementation review
round ran on it (for example only specification-phase passes and task-completion comments are
present), **when** the command runs, **then** `## How it was built` states
`Pull request #{n} has no recorded implementation review rounds.`, reports the count of excluded
specification-phase passes, and F3 is `Medium`.

**AC-39** *(FR-11, FR-16 row 4)* **Given** a PR whose `statusCheckRollup` is empty or absent for the
head commit, **when** the command runs, **then** the risk table records `CI: no checks found for
{sha}` as F4's measured value and F4 is `Medium`.

**AC-40** *(FR-6, FR-16 row 6)* **Given** a complete spec whose `.adr-list` is missing or empty,
**when** the command runs, **then** the run succeeds, `## What changed and why` states
`No ADRs recorded for this spec.` and is synthesised from `requirements.md`, `tasks.md` and the
commits, and no risk factor changes because of the absence.

**AC-41** *(FR-16 row 11, FR-17)* **Given** a spec directory with no `PROMPT.md` and no `PROMPT-*.md`,
**when** the command runs, **then** `show-me.md` contains no occurrence of the string `PROMPT` and
`## Inputs used` has no row for it — the absence is reported nowhere.

**AC-42** *(FR-6, FR-16 row 7, C-9)* **Given** an `.adr-list` containing one entry naming a file that
does not exist in `docs/adr/` and one entry given as a bare number that matches more than one file
there (e.g. `0037`, which matches five files on `master`), **when** the command runs, **then** the
run succeeds, `## What changed and why` names the first as `ADR file not found` and the second as
`ambiguous ADR number` listing the matching filenames, the remaining ADRs are still named and linked,
both unresolved entries are marked `not available` in `## Inputs used`, and no risk factor changes.

**AC-43** *(FR-8, FR-16 row 9)* **Given** a complete spec whose `requirements.md` exists but declares
no `FR-n`/`NFR-n` ids, **when** the command runs, **then** the run succeeds,
`## Did it ship what it said?` contains exactly `requirements.md declares no numbered requirements —
nothing to reconcile.` with no table and no count line, and F5 is `Medium`.

**AC-44** *(FR-5, FR-16 row 10)* **Given** a complete spec with no `.issue-number` (or an empty one),
**when** the command runs, **then** the metadata block's linked-issue line reads `none`,
`## Inputs used` marks `.issue-number` as `not available: not present`, the run succeeds, and no risk
factor changes.

**AC-45** *(FR-8, FR-11)* **Given** a `requirements.md` declaring `FR-27` with sub-clauses `FR-27.1`,
`FR-27.2` and `FR-27.3` where `FR-27.1` and `FR-27.2` both shipped and `FR-27.3` was explicitly
withdrawn by a decision recorded in an ADR that names a superseding requirement, **when** the command
runs, **then** the table has a single `FR-27` row (no `FR-27.3` row), that row addresses all three
sub-clauses in its paraphrase and evidence, its status is `Withdrawn` (the least-shipped sub-clause
status, per the stated precedence `Shipped` &lt; … &lt; `Withdrawn` &lt; `Dropped`), its evidence cites where
the withdrawal is recorded and the superseding requirement, the count line includes a `Withdrawn:`
term, and F5 is **`Medium`** for this reconciliation (not `High`) because that row states a
superseding requirement rather than `no follow-up recorded`.

**AC-46** *(FR-20)* **Given** two pull requests whose `headRefName` equals the spec branch name
(numbers #4200 and #4282) and both `gh pr diff` and a local `git diff` are available, **when** the
command runs, **then** PR **#4282** is used (highest number wins), `## Blast radius` records
`2 pull requests found for branch {branch}; using #4282 (highest number).`, the measurement source
line names `gh pr diff #4282` and not a `git diff`, and only one set of blast-radius numbers is
reported.

**AC-47** *(FR-10, C-8)* **Given** both a local branch `spec/scoped-lifetime-per-pipeline` and a
remote-tracking `origin/spec/scoped-lifetime-per-pipeline` exist at **different** shas, **when** the
command runs, **then** the measured ref is the remote-tracking one, the metadata block and
`## Blast radius` name that full ref and its sha (plus the base ref, base sha and merge-base sha), and
`## Blast radius` carries the line stating that the local branch is at a different sha.

**AC-48** *(FR-11)* **Given** a PR whose `statusCheckRollup` contains 14 checks concluded `SUCCESS`
and 4 fork-gated checks concluded `SKIPPED`, **when** the command runs, **then** F4 is `Low` and its
measured value cites the tally including the skipped checks; **and given** a rollup additionally
containing one check concluded `NEUTRAL`, **then** F4 is `Medium`; **and given** one concluded
`TIMED_OUT`, **then** F4 is `High`.

**AC-49** *(Definitions — Finding severity, FR-9, FR-11)* **Given** a review round whose findings are
titled `*(medium)*`, `*(low — a question, not a defect)*`, `*(nit)*`, `*(Blocker)*` and one with no
severity word, **when** the command runs, **then** the round's severity split is 0 Critical, 0 High,
1 Medium, 2 Low (the lowercase `low` and the `nit`), and 2 unclassified counted as Medium (the
unrecognised `Blocker` and the unmarked one), and the matching was case-insensitive and ignored the
parenthetical qualifier.

**AC-50** *(Definitions — Review round, FR-9)* **Given** a PR carrying (a) two comments by the same
author 29 seconds apart titled "part 1 of 2" and "part 2 of 2" with one continuous finding numbering
1–10, (b) six comments reporting completion of unrelated agent tasks with no numbered findings, and
(c) a later comment restarting its numbering at 1 with three findings citing source files, **when**
the command runs, **then** (a) is counted as **one** pass (not two), (b) contributes **zero** rounds,
(c) is one round, and if (a) is specification-phase by any of the three tests it contributes zero
rounds and increments the excluded-passes count instead.

**AC-51** *(FR-6, C-9, C-8)* **Given** a tree in which `docs/adr/` contains two files numbered `0070`
and two numbered `0071`, **when** the command runs against a spec whose `.adr-list` names one of
each, **then** every ADR reference in `show-me.md` carries the filename stem, each relative link
resolves to the file the `.adr-list` entry named, and no reference identifies an ADR by bare number
alone.

**AC-52** *(NFR-3)* **Given** a spec with 15 ADRs in `.adr-list` and a 229 KB `tasks.md`, **when** the
command runs, **then** it completes, it reads no more than 25 files in full (targeted front-matter /
`Status` / `Consequences` reads of the 15 ADRs excluded from that count), `tasks.md` is processed by
targeted extraction or bounded chunks rather than skipped, FR-9's task counts and FR-8's evidence are
complete, and no value is reported as zero or omitted because a read was skipped.

**AC-53** *(NFR-6)* **Given** the delivered command file, **when** it is inspected, **then** it has
front matter with `allowed-tools`, `description` and `argument-hint` and consumes `$ARGUMENTS` in the
style of the other `.claude/commands/spec/*.md` files, `.claude/commands/spec/README.md` lists
`/spec:show-me` in its command catalogue, and every relative link in a generated `show-me.md`
resolves from the spec directory.

## Additional Context

**Origin.** This command was proposed while wrapping up spec 0036
(`specs/0036-scoped-lifetime-per-pipeline/`) as the repo's own "show me" capability, bundled with a
risk-based merge assessment. The risk half exists because spec 0036's three implementation
review-response passes each ended in an ad hoc "what's left, is it safe?" read — a judgement made
three times, recorded zero times. Its sibling follow-up, "switching gears"
([#4357](https://github.com/BrighterCommand/Brighter/issues/4357), now closed — shipped separately
as ADR 0071/`/spec:gear`), is unrelated and explicitly out of scope here.

**Why the output is a file, not chat output.** A chat summary dies with the session. `show-me.md`
sits in the spec directory next to `requirements.md` and `tasks.md`, is reviewable in the PR that
introduces it, and can be regenerated as the picture changes (FR-4).

**Why `release_notes.md` is independent.** `release_notes.md` is curated for users of the library
during the spec's DOC tasks: it is a breaking-change catalogue, not a change summary, and it is
deliberately not derived from anything. `show-me.md` serves a different reader (a reviewer or
teammate wanting the internal picture) and is therefore built from the spec's own artefacts, with
`release_notes.md` used only as optional corroboration (FR-7).

**Worked example — spec 0036 as the calibration case** *(requires the `spec/scoped-lifetime-per-pipeline`
branch, C-8)*. Seven ADRs (`0070-per-pipeline-di-scope-for-mapper-and-transform-factories` through
`0076-scope-affinity-option-and-write-through`, named by stem because that tree also holds a *different*
ADR 0070 and a *different* ADR 0071 — C-9); 82 tasks, all checked; 363 commits since the merge base;
14 breaking-change items in `release_notes.md`, five of them carrying combined classifications; and a
branch whose diff against its merge base touches **517 files**, bucketed as **76 `src/`, 393 `tests/`,
24 `specs/`, 14 `docs/`, 0 `.github/`, 10 `other`** (root config files, `.claude/`,
`.agent_instructions/`) — six buckets summing exactly to 517, which is why FR-10 requires all six and
requires them to sum.

Its review history is **three implementation review rounds** on PR #4282 (2026-09-16 17:43, 3 findings;
2026-09-18 07:44, 5 findings; 2026-09-18 11:39, 3 findings) totalling **11 findings — 9 resolved, 2
acknowledged, 0 open**. Two findings in the middle round were answered without a code change ("a
deliberate mirror of an existing pattern… leaving as-is"; "accepted for now, will revisit if it's
actually hit in practice"), which is exactly the `Acknowledged finding` case. An earlier 2026-08-28
**design-phase** review on the same PR — posted as "part 1 of 2"/"part 2 of 2" 29 seconds apart with
continuous numbering 1–10 — is **not** counted, because it reviewed the spec's planning documents
rather than the implemented code: it self-identifies as design-only, and every one of its ten
numbered items cites only `specs/`/`docs/adr/` paths (it was in fact posted *after* `tasks.md` was
added to the branch — the exclusion rests on tests (i) and (iii), not on timing); six unrelated
`@claude` task-completion comments from 2026-09-16 are not counted either, because they raise no
findings. Those two exclusions, and the fact that the
two-part review is **one** pass rather than two, are the whole reason the `Review round` definition is
written in terms of content rather than comment count.

Under FR-11 this is F1 `High` (76), F2 `High` (14), **F3 `Medium`** (2 acknowledged findings put it in
the Medium column — it is *not* `Low`, because F3's `Low` column requires that nothing be
acknowledged), with F4 and F5 as measured. **Overall risk: High** under FR-12 — set by F1 and F2,
since the overall level is the **maximum** of the five factors and not their average. That is the right
answer, and precisely the answer that was previously only ever reached informally. It is also the case
that shows why the raw 517 must never be the blast-radius number quoted.

**Risk-model rationale.** A maximum-of-factors rule was chosen over weighted scoring because it is
reproducible, explainable in one sentence, and cannot be gamed by averaging a `High` away. Judgement
is preserved in the one safe direction only: the command may raise the level with a stated reason,
never lower it (FR-12). Three of the five factors (F2, F3, F5) rest on synthesis rather than pure
counting, which NFR-1 now says out loud rather than papering over with a determinism claim the
document cannot honour; the two mechanically countable factors (F1, F4) are fully deterministic, and
the calibration case's `High` comes from one of those (F1) plus the judged F2, so the level is not
hostage to the judged factors alone. The narrower question — whether the change should merge —
remains a human's, by construction (FR-13).

**Critical files for the design/implementation phase** (not requirements, kept here for
continuity):

- `.claude/commands/spec/requirements.md` — front-matter and sub-agent conventions the new command
  file must follow.
- `.claude/commands/spec/status.md` — closest prior art: reads spec metadata files,
  `.current-spec`, marker files, reports without mutating.
- `.claude/commands/spec/review.md` — finding severity scale and output-file convention.
- `.claude/commands/spec/README.md` — command catalogue the new `/spec:show-me` entry must be
  added to; sub-agent and model policy.
- `.github/workflows/claude-code-review.yml` — how review findings are posted (inline comments plus
  one tracking comment), grounding the "finding"/"round" definitions.
- `.claude/settings.json` — existing read-only `gh pr view`/`gh pr list` allow-list entries the
  command must stay within.
