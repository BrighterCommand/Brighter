---
id: 0072-show-me-command-resolution-and-output
title: "Target Resolution and Output Shape for /spec:show-me"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-09-19
summary: "/spec:show-me is implemented as two artefacts with one seam: a measurement script that performs every mechanically countable measurement and emits one JSON object, and the prompt file .claude/commands/spec/show-me.md that consumes it and synthesises. A precondition gate (FR-1/FR-2/FR-3 stops) sits above both writes; the JSON is persisted as the gitignored fact ledger specs/{spec}/.show-me-ledger.json so each remote input is fetched once; reads are budgeted in bytes rather than file count; and show-me.md is emitted in a single Write. The script's implementation language is left open."
tags:
  - "meta"
  - "api-design"
---

# 0072. Target Resolution and Output Shape for `/spec:show-me`

Date: 2026-09-19

## Status

Accepted. Amended 2026-09-20 to track the rescoped requirements: the CI tally, the PR comment fetch,
the review-round decomposition and factors F3/F4 left the command's scope, and FR-8 became a
narrative of deviations rather than a row per declared requirement.

Amended again 2026-09-21 to track the amended `requirements.md` (FR-21, NFR-9, and the rewritten
FR-4/FR-18/NFR-3). The deterministic half moves out of shell pipelines embedded in this document's
prose and into a delivered measurement script emitting JSON — Alternative 1, previously rejected here
and now the Decision; the fact ledger becomes a second, gitignored written file rather than an
in-context table; and NFR-3's read budget is denominated in bytes rather than in a count of files.
The script's implementation language is deliberately left to this phase and is **not yet settled**.

## Context

Spec 0037 asks for a new slash command, `/spec:show-me [spec-id]`, that runs after a spec's
implementation is finished and writes one durable markdown file, `specs/NNNN-name/show-me.md`,
summarising what the spec actually changed and how risky it looks to merge. The material already
exists — ADRs, `requirements.md`, `tasks.md`, the git history and the pull request — but it is
scattered and asymmetric in cost, so "is this safe to merge?" has been an ad hoc judgement
re-derived from scratch in every session. (An earlier revision of this spec also drew on the pull
request's review rounds and CI state. Both left the command's scope: the pull request already
presents them, and `/spec:review code` assesses the code properly. See
[ADR 0073](0073-show-me-advisory-risk-model.md).)

**Parent Requirement**: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)

**Scope**: This ADR covers command invocation, spec/branch/PR resolution, and output file
structure. Two sibling ADRs cover the rest: [ADR 0073](0073-show-me-advisory-risk-model.md) decides
the advisory risk model (FR-11–FR-13), and
[ADR 0077](0077-show-me-visual-explanation.md) decides the visual-explanation capability and the
Explainer role (FR-6's `##### Visual explanation`, FR-14's optional tree).

### Where this ADR sits

| ADR | Decides |
| --- | --- |
| **[0072](0072-show-me-command-resolution-and-output.md)** *(this one)* | What the command is, how it resolves its target, and the shape of the file it writes |
| [0073](0073-show-me-advisory-risk-model.md) | How the advisory risk level is computed, and how it stays advisory |
| [0077](0077-show-me-visual-explanation.md) | When the command draws a diagram, what it may draw, and who is allowed to read code to draw it |

The sentence that unifies all three: **the command states only what it has measured, names what it
measured it from, and changes nothing.**

### The forces

**Some output must be reproducible and the rest cannot be.** NFR-1 draws the line itself. The section
set and order, every `## Blast radius` number, the task total, per-tag counts and commit count,
FR-8's declared-id set and `{total}`, FR-6's trigger measurements and which of them fired, and factor
level F1 must be **identical** between runs on unchanged inputs. FR-7's breaking-change item list,
FR-8's per-requirement statuses and every diagram's content are named as "judgement-derived synthesis
and are not required to be identical between runs". NFR-7 then requires every factual claim to be
attributable to a listed input. Those two requirements together are a responsibility split, and this
ADR's job is to make it structural rather than aspirational — because an artefact that counts and
narrates in the same breath will quietly let the narration do the counting.

### Constraints this ADR inherits

Four of these come from `requirements.md` — the numbered requirements it states, and the `C-n` items
in its *Constraints and Assumptions* section, which record facts about **this** repository that the
design has to survive. The fifth comes from the repository itself. Each is stated here as what it
forbids, because that is how it shapes the decision below.

- **The command may not enumerate specs by splitting on whitespace, and may not assume an id
  identifies one spec.** Three directories are numbered `0002` on `master`
  (`0002-backstop-error-handler`, `0002-sqs-cleanup`, `0002-universal_scheduler_delay`), and one real
  spec directory name contains spaces (`specs/0021-Expose Unacceptable Message Window/`). Any
  matching mechanism must survive both. *(`requirements.md` C-1, required by FR-1.)*
- **The command may never refer to an ADR by its number alone.** ADR numbers are not unique —
  `docs/adr/` carries five files numbered `0037` and four numbered `0057` on `master` — so identity is
  the filename stem, the rule `.agent_instructions/adr_frontmatter.md` states. Bare-number references
  are forbidden anywhere in `show-me.md`. *(`requirements.md` C-9, required by FR-6.)*
- **The command may reach GitHub only to learn a PR's identity and fetch its diff.** Its `gh` surface
  is `gh pr list` and `gh pr diff`; no `gh run`, `gh api` or `gh checks`, and no query for comments,
  reviews or `statusCheckRollup`. This is not merely policy — it is what
  `.claude/settings.json` already grants, whose `gh` allow-list is exactly `Bash(gh pr view:*)`,
  `Bash(gh pr list:*)`, `Bash(gh pr diff:*)`, `Bash(gh issue view:*)`, `Bash(gh issue list:*)`.
  *(`requirements.md` C-10, required by FR-18; widening the allow-list is Out of Scope.)*
- **The command writes its output and nothing else, and a refusal writes nothing at all.** It may not
  touch `requirements.md`, `tasks.md`, any ADR, any approval marker, `specs/.current-spec` or
  `.current-gear`, and may not stage, commit, push, branch, checkout, stash or rebase. A stop must
  leave the repository byte-for-byte unchanged. *(FR-18 and NFR-8.)*
- **Whatever implements the command has to look like the rest of the family.** Every command under
  `.claude/commands/spec/` is a single markdown file whose front matter carries `allowed-tools` and
  `description`, and nine of the eleven carry `argument-hint` — `status.md` and `tasks.md`, the two
  taking no argument, do not. *(NFR-6, verified against the directory.)*

The closest prior art in this repository is
[ADR 0071: Shiftable Review Gear for the TDD Approval Gate](0071-tdd-review-gear.md) — the only other
ADR that designs a `/spec:*` command's own behaviour rather than Brighter's C# runtime. Like this
one, it records a decision about the project's own agent tooling; unlike the library's usual
interface-heavy ADRs, neither has a C# component at all. There is no other prior art for this shape.

## Decision

Implement `/spec:show-me` as **two artefacts with one seam between them**: a *measurement script*
that performs every mechanically countable measurement and emits them as one JSON object, and a
prompt file, `.claude/commands/spec/show-me.md`, that consumes that JSON and synthesises the output
sections from it, then writes `show-me.md` in one `Write` call. The JSON is also persisted, as the
*fact ledger* `specs/{spec}/.show-me-ledger.json`, so a run resolves and fetches each remote input
once.

**The seam is the decision.** Earlier revisions of this ADR put the measurement in shell pipelines
embedded in the prompt file's prose, and rejected a script on the ground that "the pipelines are
deterministic *because they are pipelines*, whoever types them". That ground is false, and the
design review that followed proved it: four of its twenty findings are defects in those very
pipelines — a declared-id `grep` returning `0` where the answer is `28` because a `\|` escape leaked
in from the markdown table cell holding it, a word-count `awk` returning `16` where the answer is
`8` because it does not exclude fenced blocks, an invariant `grep` firing three false positives by
matching `if` inside `diff`, and one stated pattern drifting into three variants across the document.
A pipeline written in prose is not code that runs; it is a description of code, and it acquires the
escaping, the line-wrapping and the drift of the prose around it. None of those four defects is
detectable by reading the file, and none is testable. Moving them into one artefact that executes
makes them both.

The organising principle is therefore a two-stage split with a real boundary. Each name is shorthand
for the rule it holds:

| Stage | Is | Produces | The rule it holds |
|------|------|-----------|------|
| **Measurer** | **The measurement script** — one executable artefact beside the command file | Every value NFR-1 requires to be identical between runs, as JSON on stdout | Counts; never paraphrases, never judges |
| **Synthesiser** | **The executing model**, reading the command file | Every value NFR-1 names as judgement-derived, plus all prose | Reads the JSON and cites it; runs no measurement and counts nothing |

That table answers a question earlier revisions left implicit: *Measurer* and *Synthesiser* are not
roles a reader has to infer from context, they are the script and the model respectively.

Nothing crosses the line in either direction: the Measurer never paraphrases and the Synthesiser
never counts. The difference from earlier revisions is that this is now enforced by where the code
lives rather than by an instruction the model is asked to follow. FR-15's `## Inputs used` table is
written *from* the ledger, which is what makes FR-16's degradation auditable and NFR-7's
traceability checkable rather than merely asserted.

### Architecture Overview

```
/spec:show-me [spec-id]
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ Step 0  Pre-flight: !`ls -1d specs/*/`  (candidate list)    │
└─────────────────────────────────────────────────────────────┘
        │
┌───────┴─────────────────────────────────────────────────────┐
│ PRECONDITION GATE — no file is written above this line      │
│  Step 1  Which spec?   one unambiguous match, or stop       │
│  Step 2  Is it done?   every task checked, or stop          │
│          a ledger is neither read nor written here          │
└─────────────────────────────────────────────────────────────┘
        │  (past this line the command ALWAYS writes — FR-16)
        ▼
┌─────────────────────────────────────────────────────────────┐
│ MEASURE — the measurement script, invoked once              │
│  Step 3  Spec branch, base ref, merge base                  │
│  Step 4  PR discovery + diff source election                │
│  Step 5  Bounded extraction, charged in bytes:              │
│            tasks.md · requirements.md ids · .adr-list       │
│            + per-ADR front matter/Status/Consequences       │
│            + .issue-number · release_notes.md section       │
│            + blast-radius stats                             │
│          ⇣ emits ONE JSON object on stdout                  │
│          ⇣ persists it: specs/{spec}/.show-me-ledger.json   │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ SYNTHESISE — JSON in, prose out; the model counts nothing   │
│  Step 6  Sections 1–8 in FR-5's fixed order                 │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
  Step 7  Write specs/{spec}/show-me.md  (one Write call)   FR-4
  Step 8  Budget self-check + FR-19 session report
```

The gate's two steps are labelled by **what they require**, not by the requirement numbers that
impose it. Earlier revisions read `FR-1 / FR-2 → stop msg`, which cannot be understood without
opening `requirements.md` alongside; a reader of an architecture diagram should be able to see what
stops a run without cross-referencing three documents. The codes stay in the prose below, where
there is room to state them with their context.

The gate deliberately sits above **both** writes, not merely above the `Write` call. The measurement
script supplies the task-checkbox counts Step 2 reads, so it necessarily runs first — but it emits
to stdout, and the ledger is written only once the gate has passed. A run that stops here leaves the
repository byte-for-byte unchanged (FR-3, AC-7, AC-71).

### Key Components

#### 1. The command file

One markdown file, `.claude/commands/spec/show-me.md`, plus a catalogue entry in
`.claude/commands/spec/README.md` (required by NFR-6 and asserted by AC-53). It holds the synthesis
procedure and nothing that counts.

Beside it sit the other two delivered artefacts: the **measurement script** (FR-21) and its
**sibling test script** (NFR-9). No library, no solution project, nothing under `src/` or `tests/`,
and no sub-agent.

**On the script's language.** `requirements.md` deliberately does not fix it, constraining only that
there be exactly one such artefact, what it emits, that every stated pattern is implemented in it
exactly once, and that it may be permitted to run only by an allow-list entry naming its own path.
Choosing between a shell script, an awk program, a Python script and a small .NET tool is this
phase's job. The repository's one precedent for an executable artefact in this family,
`.claude/commands/adr/generate_adr_index.awk`, is an argument for awk on grounds of consistency; the
script's actual work — shelling out to `git` and `gh`, parsing their output, and emitting
well-formed JSON with correct escaping — is an argument against it, because awk has no JSON support
and hand-rolled string escaping is precisely the class of defect this seam exists to eliminate.
**This ADR does not settle it**; it records that the choice is open, that it is narrow (the contract
is one JSON object on stdout and an exit code), and that whichever is chosen must be invocable from
a fresh checkout with no build step (NFR-6).

**No sub-agent.** `/spec:requirements`, `/spec:design`, `/spec:tasks` and `/spec:review` all delegate
to a sub-agent; `/spec:status` and `/spec:gear` — the two read-and-report commands — do not.
`/spec:show-me` follows the latter. The README's stated rationale for delegating is that a `Plan`
sub-agent "has no file-editing tool", which makes it harder to do damage; that rationale is empty
here, because the whole command is read-only apart from one `Write`. Against delegation there are two
concrete costs: a sub-agent starts with a clean context, so it would either have to be handed the
entire fact ledger and every extract in its prompt, or re-read the inputs itself — and re-reading
would double the NFR-3 budget that Step 5 is built to respect. The command therefore runs in the main
agent, which is also where FR-19's session report has to be emitted.

#### 2. The precondition gate (Steps 1–2)

Claude Code command files have no exception handling, so FR-1/FR-2/FR-3's refusals are expressed as a
single numbered gate that precedes every step capable of writing, in the same style as
`review.md`'s Step 1 "Error handling" paragraph and `status.md`'s Step 1 checks. The gate's contract
is stated once, in bold, in the command file: **if any check in Steps 1–2 fails, print the exact
message given and stop; do not proceed to Step 3; do not write, create or touch any file.** FR-3 is
explicit that this is the only circumstance in which the command declines to produce output, and
AC-7 requires that a pre-existing `show-me.md` be byte-for-byte unchanged after such a stop — which
is why the gate sits above every write and not merely above the `Write` call.

FR-16's fourteen degradation rows are the mirror image and live *below* the gate, attached to the step
that discovers each absence: each records a ledger row (`not available: {reason}`), sets the
section's defined fallback text, and continues. No absence below the gate stops the run.

#### 3. The fact ledger

The JSON object the measurement script emits, held in context for the run and **also persisted** to
`specs/{spec}/.show-me-ledger.json` (FR-4). Earlier revisions of this ADR described it as "an
in-context table — never a file, since FR-18 permits exactly one write"; FR-18 now permits two, and
the second is this. Persisting it is what lets the script fetch each remote input once instead of
re-running `gh pr diff` for every value derived from it.

It is **working state, not a deliverable**: `.gitignore` carries an exact-match entry for it, so
`git status --porcelain` is unchanged by a run except for `show-me.md` itself (AC-30, AC-74), and it
is therefore untracked — which is exactly why FR-17 and NFR-5 forbid `show-me.md` citing it or
listing it in `## Inputs used`. Every fact it carries is attributed instead to the artefact the
script counted it from. Each entry records `{input or metric} | {value} | {command or path it came
from} | used | not available: {reason}`. The ledger is the single source for:

- FR-5's metadata block (branch ref, head sha, base ref, merge-base sha, PR reference, issue);
- every number in `## Blast radius`;
- FR-15's `## Inputs used` table, which is a projection of the ledger;
- NFR-7's traceability rule, restated in the command file as: **a section may state only values
  present in the ledger, and must name the ledger row it came from.**

FR-17 and NFR-5 are enforced at the point of projection: before any path is written into
`show-me.md`, it is checked with `git ls-files --error-unmatch {path}`. This covers both the
gitignored literal `PROMPT.md` and the merely-untracked `PROMPT-*.md` companions with one test, which
is exactly what FR-17 asks for ("the rule is stated in terms of git tracking, not gitignore status").
`PROMPT.md` may be read as background and may never be cited; per FR-16 row 11 its absence is
recorded nowhere, so it gets **no ledger row at all** — the one input deliberately outside the
ledger, because a row would surface it in `## Inputs used` and break AC-41.

#### 4. The output document

`show-me.md` is assembled in memory in FR-5's fixed order and emitted with a **single `Write` call**,
mirroring how `review.md` Step 6 writes `review-{phase}.md` after validating the content rather than
streaming it out in pieces. FR-4's "replaces its entire contents" falls out of `Write`'s own
semantics; no backup, no numbered variant, no `git add`.

### Technology Choices

**Front matter.** Following the family's shape, with `argument-hint` present because the command
takes an optional argument (AC-53 requires all three fields):

```yaml
---
allowed-tools: Bash(ls:*), Bash(cat:*), Bash(test:*), Bash(wc:*),
  Bash(<the measurement script, named by its own path>:*),
  Bash(git ls-files:*), Read, Write, Glob, Grep
description: Summarise a finished spec and give an advisory merge-risk read
argument-hint: [spec-id]
---
```

**The list shrank, and that is the seam paying for itself.** Every `git` and `gh` verb the earlier
front matter declared — `git log`, `git diff`, `git rev-parse`, `git merge-base`, `git branch`,
`gh pr list`, `gh pr view`, `gh pr diff` — was there to let the *prompt file* measure. Measurement
now happens in the script, so the command file needs none of them. What remains is Step 0's
pre-flight listing, the `wc -c` affordability probe NFR-3 requires before each read, `git ls-files`
for FR-17's tracking test, the script invocation itself, and `Read`/`Write`.

**`Bash(awk:*)` is gone, deliberately.** The earlier revision declared it and defended it on the
ground that sibling commands (`approve.md`, `design.md`) declare it too, so no settings change was
needed. That defence is about *where the grant is written*, not about *how wide it is*: awk provides
`system()` and `"cmd" | getline`, so `Bash(awk:*)` is a general command-execution grant wherever it
appears. FR-18 and C-10 now forbid an interpreter grant for this command in either location, and the
command file no longer needs awk for anything, so it is simply removed.

**The script's own entry is written once the language is chosen** (Key Components 1). Whatever the
language, the entry names the script's **path**, not its interpreter, so that it permits that one
program and nothing else.

**One consequence to accept honestly.** The earlier revision claimed AC-30 — "every `gh` invocation
is one of `gh pr list` or `gh pr diff`" — was "checkable by reading fourteen lines of front matter".
It no longer is: the `gh` calls now live inside the script, where the front matter cannot constrain
them. The check moves to the script's source, which is a smaller and more honest surface to audit
than prose describing pipelines, and NFR-9's test script is the mechanism that keeps it checked. But
it is a move, not a free win, and the command file no longer demonstrates FR-18's confinement on its
own.

**No `git rev-list`, no `sed`, no `sort`.** Commit counts use `git log --oneline {mb}..{head} | wc -l`
rather than `git rev-list --count`, and multi-line extracts use `grep -A`/`awk` rather than `sed`, so
the tool surface stays inside what the repository already grants.

**BSD-compatible regexes.** The repository's primary environment is macOS, whose `grep` does not
support `\b`. Every pattern in the command file uses POSIX classes —
`^[[:space:]]*-[[:space:]]\[[ xX]\]` for a task checkbox (the `Task checkbox` definition),
`^[+-][[:space:]]*(public|protected)[^[:alnum:]_]` for a public API declaration line (the
`Blast radius` definition (d)).

### Implementation Approach

#### Step 0 — Pre-flight

The file opens with the family's pre-executed-shell convention (`switch.md` already does this):

```
## Available specifications

!`ls -1d specs/*/ 2>/dev/null`
```

This seeds the candidate list for free on every invocation. FR-1 restricts candidates to *directory
entries directly under `specs/`*, which `ls -1d specs/*/` already satisfies: `specs/README.md`,
`specs/dlq-review-findings.md` and the dotfile `specs/.current-spec` are all excluded by the trailing
slash and the glob, which is what AC-35's second half asserts.

#### Step 1 — Resolve the target spec (FR-1, FR-2)

`$ARGUMENTS` is taken **whole** — trimmed of leading/trailing whitespace, wrapping quotes removed,
internal whitespace preserved — never split on whitespace, because
`specs/0021-Expose Unacceptable Message Window/` is a real directory (AC-35). This is why the
three-rule match (exact directory name → exact four-digit id → case-insensitive substring, stopping
at the first rule yielding exactly one match) is performed **by the executing model over Step 0's
line list**, not by a shell pipeline: a `for` loop over `specs/*/` with an unquoted variable would
split that name into four tokens, and the quoting needed to avoid it is precisely the kind of detail
that rots. The list is at most a few dozen short lines; matching it is not work worth shelling out.

More than one match → the FR-1 ambiguity message and stop (AC-1a: `0002` must name all three
`0002-*` directories). No match → the FR-1 no-match message and stop (AC-3).

With no argument, `cat specs/.current-spec` (verified format: the bare directory name, e.g.
`0037-show-me`) and `test -d "specs/{value}"`. Missing / empty / whitespace-only / stale each select
the corresponding word in FR-2's single message template, and stop.

#### Step 2 — Completeness check (FR-3)

Three bounded `grep`s against `tasks.md` — never a `Read`, because spec 0036's is 229 KB and NFR-3
forbids spending a full read on it:

```bash
test -f "specs/{dir}/tasks.md"
grep -cE '^[[:space:]]*-[[:space:]]\[[ xX]\]' "specs/{dir}/tasks.md"   # total
grep -cE '^[[:space:]]*-[[:space:]]\[ \]'      "specs/{dir}/tasks.md"   # unchecked
grep -m3 -E '^[[:space:]]*-[[:space:]]\[ \]'   "specs/{dir}/tasks.md"   # first three titles
```

Absent → FR-3's first message. Total 0 → the second. Unchecked > 0 → the third, with `{n} of
{total}` and the first three titles one per line. Stop in all three cases. This is also the mechanism
NFR-3 means by "targeted extraction of the constructs it needs": the same `grep` family, with the
task-type tag added, supplies FR-9's per-tag counts, and `untagged` is computed
as the complement (`grep -vcE '(TEST \+ IMPLEMENT|STRUCTURAL|PROJECT|DOC)'` over the checkbox lines)
so the parts always sum to the total.

#### Step 3 — Branch, base ref, merge base (FR-10)

```bash
name={spec directory name with the leading NNNN- removed}
git rev-parse --verify --quiet "refs/remotes/origin/spec/${name}"   # rule 1, remote-tracking wins
git rev-parse --verify --quiet "refs/heads/spec/${name}"            # rule 1, local fallback
git rev-parse --abbrev-ref HEAD                                     # rule 2, if it contains ${name}
git log --oneline "{base}..HEAD" -- "specs/{dir}/"                  # rule 3, non-empty ⇒ HEAD
```

The remote-tracking ref wins when both exist (FR-10), and when the local branch is at a different sha
the ledger records it so `## Blast radius` can carry FR-10's third line (AC-47). Base ref is
`origin/master` if `git rev-parse --verify --quiet origin/master` succeeds, else `master`. Merge base
is `git merge-base {base} {head}`; its sha is reported, which is why the explicit `merge-base`
invocation is used rather than relying on `git diff`'s three-dot form.

All four rules failing is FR-16 rows 12–15: `Blast radius` states the branch is not determinable and
lists the rules tried, `Where to look first` takes its exact fallback line with no paths,
`Breaking changes` adds its "public-API declaration lines could not be inspected" line but still
carries a count line, and the metadata block's branch/head/merge-base lines each read `undetermined`
while the base ref is still resolved and named normally (AC-19).

#### Step 4 — PR discovery and diff-source election (FR-20)

```bash
gh pr list --head "spec/${name}" --state all --json number,url,headRefName,createdAt
```

The remote prefix is stripped from the branch name before querying, and results are filtered to those
whose `headRefName` equals that name **exactly** — `gh` matches loosely enough that the filter is
load-bearing. One result → that PR. More than one → highest number wins, and the ledger records
`{k} pull requests found for branch {branch}; using #{n} (highest number).` for FR-10's line
(AC-46). Zero results, or a non-zero exit from `gh` (unavailable, unauthenticated, offline) → no PR,
FR-16 rows 1–2.

`.issue-number` is never consulted here. It is a *tracking issue*, not a PR — spec 0036's is 4256
while its PR is #4282 — and it feeds only FR-5's linked-issue line.

Diff source, exactly one, never mixed:

```bash
gh pr diff {n} --name-only        # succeeds ⇒ PR diff is the spec diff
git diff --name-only "{mb}..{head}"   # otherwise
```

#### Step 5 — Blast radius without reading the diff (FR-10, NFR-3)

NFR-3 forbids reading the full diff into context but explicitly permits summary statistics. Every
blast-radius number is therefore a *count* produced by a pipeline whose output is one or two integers:

```bash
# six buckets, from the chosen source's file list — `other` as the complement,
# so the six counts always sum to the total (AC-18)
FILES | grep -cE '^src/'      ; FILES | grep -cE '^tests/'
FILES | grep -cE '^docs/'     ; FILES | grep -cE '^specs/'
FILES | grep -cE '^\.github/' ; FILES | grep -vcE '^(src/|tests/|docs/|specs/|\.github/)'
FILES | wc -l

# net lines — git source
git diff --numstat "{mb}..{head}" | awk '{a+=$1; d+=$2} END {print a+0, d+0}'
# net lines — PR source (gh pr diff has no --numstat)
gh pr diff {n} | grep -cE '^\+([^+]|$)' ; gh pr diff {n} | grep -cE '^-([^-]|$)'

# public API declaration lines, restricted to src/ — git source
git diff -U0 "{mb}..{head}" -- src/ \
  | grep -cE '^[+-][[:space:]]*(public|protected)[^[:alnum:]_]'
# …and PR source, tracking the current file from the +++ header
gh pr diff {n} | awk '/^\+\+\+ b\//{f=substr($2,3)}
                      f ~ /^src\// && /^[+-][ \t]*(public|protected)[^A-Za-z0-9_]/ {c++}
                      END {print c+0}'

# commits since the merge base
git log --oneline "{mb}..{head}" | wc -l
```

Computing `other` as the complement rather than as its own pattern is deliberate: AC-18 requires the
six bucket counts to sum to the reported total (517 = 76 + 393 + 24 + 14 + 0 + 10 for spec 0036), and
a complement makes that an identity rather than something to get right.

#### Step 5 (continued) — the byte budget

NFR-3 now denominates the budget in **bytes**, not files: **1,048,576 charged bytes per run**, of
which the last **100,000** are reserved for the source reads FR-6's diagrams need. The old
"at most 25 individual files" cap is retired, and so is its exemption for ADR reads — measured, the
justification for that exemption ("because they are small") was simply false.

Every figure below is a real `wc -c` against spec 0036 at merge base
`6145913a0..spec/scoped-lifetime-per-pipeline`. Affordability is checked with `wc -c` **before**
anything is opened, and `wc -c` is itself charged nothing.

| Input | Mechanism | Charged |
|---|---|---|
| the measurement script's JSON | read whole; capped by FR-21 | ≤ 65,536 |
| `tasks.md` | read **in full** — the "229 KB scare case" was never the problem | 229,159 |
| the `src/`-scoped diff | permitted; the **full** 517-file diff is not | 303,715 |
| each ADR named in `.adr-list` | front matter + `## Status` + `## Consequences`, **charged, not exempt** | 95,967 for seven (mean 13,710, max 20,709) |
| `requirements.md` | `wc -c` says 273,674 — more than remains, so targeted extraction, not a whole read | extract only |
| `release_notes.md` section | targeted extraction of the spec's section | ≤ 118,145 whole |
| `.adr-list`, `.issue-number`, `.current-spec` | `cat` | negligible |
| files for `## Where to look first` and FR-6's diagrams | `Read` on demand | the 100,000 reserve |

Summing the fixed rows: 65,536 + 229,159 + 303,715 + 95,967 = **694,377**, leaving 254,199 of the
948,576 general allowance and the reserve untouched. **The calibration run fits by construction** —
no file is opened whose measured size the remaining allowance does not cover, so the total cannot
exceed the cap (AC-52, AC-76, AC-78).

**The full diff is banned for a reason worth stating.** At 4,081,673 bytes — roughly 1.24 M tokens —
it **exceeds the context window outright**. A run attempting it would not degrade, it would fail.
Earlier revisions banned it without ever saying why, which invites a reader to treat the ban as
negotiable.

Note that `requirements.md`'s
declared-id grep gives FR-8's *declared-id set* mechanically — which is why NFR-1 can require that
set and `{total}` to be identical between runs while leaving the statuses, and therefore the
partition between FR-8's shipped-as-planned line and its deviation entries, judged — and that
sub-numbered ids (`FR-27.3`) are excluded from the declared-id set by the `[0-9]+` anchor and folded
into their top-level id during synthesis (AC-45).

#### Step 6 — Synthesis, and what each section may claim

The command file states the split per section, because this is where NFR-7 is either enforced or
lost:

| Section | Measured (ledger) | Judged (synthesis) |
|---|---|---|
| `## What changed and why` (FR-6) | the `.adr-list` entry set; each ADR's title and Status from its front matter; the resolved link path | the 150–600-word narrative; which one or two decisions "most shaped the result" |
| `## Breaking changes` (FR-7) | public-API declaration lines; the `release_notes.md` bullet boundaries when a section exists; the final `Total breaking-change items: {n}` | what constitutes one item; its classification set; the one-sentence migration |
| `## Did it ship what it said?` (FR-8) | the declared-id set and `{total}`; Part 1's id-list ordering and range collapsing; Part 4's arithmetic | each id's status — and therefore the partition between Part 1's line and Part 2's deviation entries — plus each entry's paraphrase, reason and evidence, and Part 3's `Shipped beyond the requirements` list |
| `## Blast radius` (FR-10) | **everything** | nothing |
| `## Where to look first` (FR-14) | the candidate path list (must exist in the spec diff) | the ordering, the 3–7 selection, and each ≤ 25-word reason |
| `## Inputs used` (FR-15) | **everything** — a projection of the ledger | nothing |

Two rules make FR-7's item boundaries reproducible enough to be useful without pretending they are
mechanical. When a `release_notes.md` section for the spec was read, **its bullet grouping is the
tie-break** — the command follows the catalogue's boundaries rather than re-partitioning them, which
is why spec 0036's calibration groups `CreatePipelineScope()` and `PipelineScope` into one item while
keeping `IAsyncDisposable` separate. When no such section exists (FR-16 row 5), the fallback is one
item per distinct public-API declaration change or per ADR *Consequences* bullet describing a
behavioural break, and the section says so in the defined line.

ADR references everywhere in the file — FR-6's narrative, FR-7's evidence, FR-8's evidence column,
FR-15's rows — carry the filename stem and a relative link, never a bare number, because
`docs/adr/` holds five files numbered `0037` and four numbered `0057` on `master` alone and two
files numbered `0070` plus two numbered `0071` on the calibration branch (AC-51). FR-16 row 7's
two unresolved-entry cases (`ADR file not found` / `ambiguous ADR number`) are detected with
`ls docs/adr/ | grep -E "^{entry}"` and do not stop the run.

#### Step 7 — Write (FR-4, FR-5)

One `Write` to `specs/{dir}/show-me.md` with the fully assembled content: H1, metadata block, then
the eight H2 sections in FR-5's exact spellings and order, every one present even when its input was
absent (FR-16). Before writing, each relative link is checked with
`git ls-files --error-unmatch {path}` (FR-17, NFR-5, AC-29), and all paths are repository-relative.

#### Step 8 — Budget self-check and report (NFR-2, FR-19)

NFR-2's word count is defined mechanically "so it can be checked by a script", so the command checks
it rather than estimating:

```bash
awk 'BEGIN{b=0}
     /^## /{h=1}
     /^## Inputs used/{exit}
     h==1 && !/^[[:space:]]*\|/ {
       for (i=1;i<=NF;i++) if ($i ~ /[[:alnum:]]/) b++
     }
     END{print b}' "specs/{dir}/show-me.md"
```

Outside 400–2,000 → revise and re-`Write` the same path. Re-writing one file the command already owns
keeps NFR-8's "differing only in `show-me.md`'s contents" true.

FR-19's session report then prints the path written, created-or-replaced, the overall level and the
one-line advisory reminder — with no error and no refusal whatever the level, per FR-13.

## Consequences

### Positive

- **The command is a sibling, not an alien.** One markdown file with the same front-matter shape as
  its nine neighbours, added to the same README catalogue, executed by the same runtime. Nothing new
  to build, install, version or keep in sync (NFR-6, AC-53).
- **NFR-1's determinism claim is structural, and now testable.** Because the Measurer owns every
  field NFR-1 lists and the Synthesiser is forbidden to produce a number, "these values are identical
  between runs" is a property of where the values come from. The seam is what upgrades that from an
  instruction to a guarantee: determinism is a property of running one implementation twice, which
  is something a script can have and a prose description of a pipeline cannot. NFR-9's test script
  pins it against real fixtures, including the two defects that motivated the split.
- **NFR-7 and FR-15 are the same mechanism.** The `## Inputs used` table is a projection of the
  ledger that also feeds every other section, so a claim with no ledger row has nowhere to appear and
  an input with no row cannot be silently used.
- **NFR-3's budget is met by arithmetic, not by restraint.** Every read is priced in bytes and
  checked with `wc -c` before it is issued, so the cap cannot be exceeded by a run that behaves
  correctly — not because large inputs are avoided, but because the fixed costs sum to 694,377 of a
  948,576 general allowance with the diagram reserve untouched (AC-52, AC-76, AC-78). This replaces
  an earlier claim that `tasks.md` and the diff were "never read — only counted": both are now read,
  and the budget accommodates them.
- **The refusal surface is still one block, and the second write did not widen it.** The stops that
  depend on *the spec the command was pointed at* all sit in Steps 1–2 above both writes, so AC-7's
  "byte-for-byte unchanged" remains a property of step ordering. The script runs above the gate — it
  supplies the counts the gate reads — but it only emits to stdout there; the ledger is written below
  the gate, with the deliverable. FR-21 adds a fourth stop, for the script being absent, unreadable,
  failing or emitting unparseable output, and it writes nothing either (AC-71, AC-72).
- **The arithmetic that acceptance tests check is identity, not effort.** `other` as a bucket
  complement, `untagged` as a tag complement, and the count lines derived from the rows they count
  all make the sums correct by construction (AC-18, AC-15).
- **The command file's tool surface shrank to almost nothing.** With measurement in the script, the
  front matter no longer declares a single `git log`, `git diff`, `git rev-parse`, `git branch` or
  `gh` verb, and no longer declares `Bash(awk:*)`. What is granted to the prompt is now close to the
  minimum a document-writing command needs. The trade is stated plainly under Technology Choices:
  FR-18's confinement is no longer demonstrable from the front matter, because the `gh` calls moved
  into the script, and AC-30 is checked against the script's source instead.

### Negative

- **The fact ledger is a second written file, with everything that implies.** Earlier revisions of
  this ADR listed the opposite as the cost — "nothing is cached… because FR-18 permits exactly one
  written file and there is therefore nowhere to put a cache" — and accepted re-running the whole
  `git`/`gh` sequence every time. FR-18 now permits two writes and the ledger is the second, so that
  cost is gone: `gh pr list` and `gh pr diff` are each invoked at most once per run (AC-73). What
  replaces it is a smaller but real cost. A file now appears in every spec directory a run touches;
  it is kept out of `git status` only by a `.gitignore` entry, so the guarantee is one line away from
  being lost; and because it is untracked, FR-17 and NFR-5 forbid `show-me.md` ever citing it, which
  means a reader cannot follow a number back to the ledger it came from — only to the artefact the
  script counted it from. The staleness hazard the old bullet feared is handled by the ledger being
  replaced wholly on every run rather than merged into, so a stale ledger is not a state the design
  admits.
- **The judged sections are not byte-reproducible, and the overall level can move with them.** NFR-1
  says this out loud: FR-7's item list, FR-8's statuses and every diagram's content may vary between
  runs, and because F2 and F5 feed FR-12's maximum, the headline level may vary too. The ledger
  constrains these to stay grounded in cited evidence; it does not make them deterministic, and this
  ADR does not claim otherwise.
- **The budgets can still bind on a large spec.** NFR-2's 2,000 counted words are estimated against
  post-rescope spec 0036 at roughly **1,350** — more headroom than the 1,600–1,750 an earlier
  revision assumed, because FR-8 dropped from a row per requirement to one line plus deviations. That
  figure is an **estimate, not a measurement**: no complete `show-me.md` has ever been generated, so
  the first real run is also the first real test of it. A spec with 25 breaking-change items and 40
  numbered requirements would still force the narrative toward FR-6's 150-word floor, and Step 8's
  self-check would surface that as a revise-and-rewrite rather than as a clean failure. The byte
  budget has its own binding case: a spec whose `requirements.md`, `tasks.md` and diff are each
  larger than spec 0036's would push `requirements.md` and the `release_notes.md` section into
  targeted extraction, which is the designed degradation but is still a loss of context the
  synthesis would otherwise have had.
- **Shell extraction is brittle against format drift.** The checkbox regex, the task-type tags, the
  `+++ b/` header shape and the `release_notes.md` heading pattern are all conventions, not
  contracts. When `tasks.md` starts using a shape the regex misses, NFR-3's rule applies — report
  `Unverifiable`/`not available` with a reason, never zero — but the command will not notice the
  drift on its own.
- **The script is a new artefact to maintain, in a family that had almost none.** This is the cost
  the earlier revision rejected the script to avoid, and it is real: a second delivered file, a third
  counting its test, a language choice to defend, and an allow-list entry to keep narrow. It is
  accepted because the alternative was proved worse — four defects in prose pipelines that no reader
  could see and no test could catch — and because NFR-9 gives the new artefact the regression net the
  old one never had. *(An earlier revision listed "two `gh pr diff` invocations for the PR path" here,
  because added and removed lines needed separate passes over the same patch. The ledger retires that
  cost: the patch is fetched once.)*

### Risks and Mitigations

**Risk**: The Synthesiser quietly recomputes a number the Measurer already owns — counting
breaking-change bullets by eye, or estimating a file count — and NFR-1's determinism guarantee
silently lapses.
- **Mitigation**: The split is now enforced by where the code lives, not only by instruction — the
  command file has no `git`, `gh` or `awk` grant left to recompute anything with (Technology Choices),
  which is a stronger guarantee than the earlier revision's "forbids shell calls during Step 6".
  AC-32 tests it by comparing two runs' mechanical fields; AC-34 tests that every count is
  attributable to a listed input; AC-70 tests that the command file contains no pipeline computing a
  value the script owns.

**Risk**: The second write becomes a third, or the ledger acquires a meaning it was not given. An
earlier revision of this ADR listed the hazard as "a future edit adds a second write — a cache, a
marker, a `.last-run` file"; that edit has now been made deliberately, so the guard rail has to move
rather than simply be restated. The specific danger is that a persisted JSON file sitting in a spec
directory is exactly the shape of thing another command could start reading as a gate, or that a
later change starts merging into it instead of replacing it and reintroduces staleness.
- **Mitigation**: FR-4 fixes the count at exactly two writes and names both. The ledger is replaced
  wholly on every run, never appended to or merged into, so a stale ledger is not a reachable state.
  FR-13 and the Out of Scope list still forbid any marker file another command could read as a gate,
  and the ledger qualifies — nothing in this spec reads it except the run that wrote it. It is
  gitignored, so it cannot travel to another checkout and be mistaken for shared state. AC-30
  compares `git status --porcelain` before and after, AC-74 asserts the wholesale replacement and the
  absence of any backup or numbered variant, and AC-75 asserts `show-me.md` never names it.

**Risk**: The argument is split on whitespace by a later convenience edit, breaking
`specs/0021-Expose Unacceptable Message Window/`.
- **Mitigation**: FR-1 states the whole-argument rule, AC-35 tests it with the unquoted four-word
  form, and this ADR records the shell-quoting reason the match is performed over a line list rather
  than in a loop.

**Risk**: `gh pr diff` succeeds but returns a patch for a head commit that has moved since
`gh pr list` ran, so the ledger's head sha and the measured numbers disagree.
- **Mitigation**: FR-10 requires the measurement source, ref, head sha, base ref and merge-base sha
  to be named on their own lines precisely so "two runs that disagree can be diagnosed". The command
  records the sha it queried, not the sha it assumed.

**Risk**: A `grep` that legitimately finds nothing is indistinguishable from a `grep` that failed,
and a real zero gets reported where "could not measure" is the truth.
- **Mitigation**: NFR-3 already requires an incomplete extraction to be reported as `Unverifiable`
  (FR-8) or `not available` (FR-15) with the reason, never as zero. The command checks exit status,
  not just output, and the ledger row carries the distinction.

## Alternatives Considered

### Alternative 1: A helper script or compiled tool invoked by the command — **ACCEPTED**

Put the deterministic half — branch resolution, PR discovery, bucket counting, checkbox counting —
in a script or small program under `.claude/`, and have the command file call it and synthesise
around its JSON output.

**This is now the Decision.** It is kept here, rather than deleted, because an earlier revision of
this ADR rejected it, and the four grounds it gave are worth recording as falsified — three of them
by evidence that arrived afterwards, and one that was never sound.

- *"There is no precedent in the `/spec:*` family… the only executable artefact anywhere nearby is
  `generate_adr_index.awk`."* **Self-refuting as written.** It names the precedent in the sentence
  that denies one exists. `generate_adr_index.awk` is an executable artefact in this very family,
  invoked from five documented call sites across `adr.md`, `design.md`, `approve.md`, the ADR README
  and `adr_frontmatter.md`. "A new category" was the wrong description; "a second instance" was the
  right one.
- *"It adds a maintenance and review surface — a script with no test project in a repository whose
  entire test discipline is C#, plus a new allow-list entry."* **Half true, and the half that was
  true is now addressed.** NFR-9 delivers a sibling test script, so the artefact is not untested; the
  observation that the repository has no shell-test harness was correct, and the answer is a script
  that needs none rather than a C# project that would breach this spec's own no-`src/`-no-`tests/`
  boundary. The allow-list entry is real and is accepted, narrowed to the script's own path.
- *"The determinism it would buy is already available: the pipelines in Step 5 are deterministic
  because they are pipelines, whoever types them."* **False, and demonstrably so.** The design review
  that followed found four defects in exactly those pipelines: a declared-id `grep` returning `0`
  against a correct answer of `28`, because the markdown table cell holding it required a `\|`
  escape that became part of the pattern; a word-count `awk` returning `16` against a correct answer
  of `8`, because it did not exclude fenced blocks; an invariant `grep` firing three false positives
  by matching `if` inside `diff`; and one stated pattern drifting into three variants across the
  document. A pipeline in prose is not code that runs — it is a description of code, subject to the
  escaping and line-wrapping of the prose around it, verifiable by no one and testable by nothing.
- *"It would not touch the genuinely variable half."* **True, and never the claim.** FR-7's item
  boundaries and FR-8's statuses are judgement by NFR-1's own admission. The seam is not proposed to
  make judgement deterministic; it is proposed to make the *mechanical* half actually mechanical, so
  that NFR-1's split means something.

**What this ADR does not settle** is the script's language. `requirements.md` deliberately leaves it
open, and the trade-offs are recorded under Key Components 1: awk has precedent on its side, and no
JSON support against it.

### Alternative 2: Push more of the output into `gh`/`git` one-liners, with minimal synthesis

Maximise NFR-1's determinism by replacing the judged sections with mechanical proxies: derive
breaking changes purely from `^[+-]\s*(public|protected)` hunks, derive requirement status from
whether an FR id appears in a commit message, and drop FR-6's narrative in favour of
`git log --oneline`.

**Partially accepted, and rejected for the rest.** It is accepted wherever a mechanical answer is the
*right* answer — which is why `## Blast radius` and `## Inputs used` have no judged content at all,
and why FR-8's declared-id set and `{total}` are mechanical even though its statuses are not. It is
rejected for the three sections NFR-1 names, for a concrete reason each:

- A public-API declaration-line diff cannot tell an added overload from a breaking signature change,
  and cannot see the behavioural break that FR-7's calibration case leads with —
  `MapperLifetime.Scoped` no longer caching for the life of the process changes no declaration line
  at all.
- "Did it ship what it said?" is a comparison between a promise written in prose and an outcome
  spread across tasks, ADRs and a diff. An id appearing in a commit message is not evidence that the
  requirement shipped; treating it as such would produce a table that is reproducible and wrong.
- FR-6 explicitly asks for prose that is "not a bullet dump, not a copy-paste of ADR *Decision*
  sections", readable by a contributor who has not read the ADRs (NFR-2, A-2). A commit log is
  precisely the artefact the spec exists to replace.

The honest position is the one NFR-1 takes: name which fields are mechanical, guarantee those, and
say plainly that the rest are judgement — rather than buy reproducibility by answering a different,
easier question.

### Alternative 3: A Python or shell wrapper that owns the whole command

Implement `/spec:show-me` as a thin prompt that shells out to a program which does everything,
including generating the markdown, with the model used only to fill in narrative slots.

**Rejected because** — note that this is *not* the seam Alternative 1 proposes, and is rejected on
grounds that survive its acceptance. Alternative 1 gives the script the measuring and leaves every
judged section to the model; this alternative gives the program the document.

- The narrative slots are most of the point. FR-6 (150–600 words), FR-7's migrations, FR-12's
  rationale and FR-14's reasons are the sections a reader actually reads; templating around them
  produces a document whose interesting parts are still model output but whose provenance is now
  split across two artefacts.
- It would make FR-16's fourteen degradation rows a program's error paths rather than prose next to
  the step that discovers each absence, which is where they are cheapest to keep correct.

### Alternative 4: Delegate synthesis to a `general-purpose` sub-agent

Measure in the main agent, then hand the ledger to a sub-agent (per the README's model policy) that
returns the eight sections as text for the main agent to write — the shape `/spec:review` uses.

**Rejected.** The README's stated reason for delegating — a sub-agent with no file-editing tool is
harder to let do damage — does not apply to a read-only command. Against it, a clean-context
sub-agent must either receive the whole ledger plus every extract in its prompt, or re-read the
inputs and spend the NFR-3 budget twice; and FR-19's report has to come from the main agent
regardless. [ADR 0077](0077-show-me-visual-explanation.md) reaches the same conclusion for the one
stage that reads source, and for an additional reason that applies only there: the read set is a
shared per-run budget, and a sub-agent reports a count where the run needs a set.

### Alternative 5: Read `tasks.md` and the diff in full and let the model count

Simply `Read` the 229 KB `tasks.md` and the 517-file diff, and count in-context.

**Rejected because**: the full diff does not fit. At 4,081,673 bytes — about 1.24 M tokens — spec
0036's 517-file diff **exceeds the context window**, so this is a hard wall rather than a budget
choice, and NFR-3 bans it accordingly. `tasks.md` is a different matter: at 229,159 bytes it is now
read **in full**, so the half of this alternative that proposed reading it was right and the current
design does it. What stays rejected is letting the model *count* — that would collapse NFR-1's
mechanical/judged split, which is the structural idea this ADR rests on, and FR-21 puts every count
in the script precisely so no count is a model judgement.

*(An earlier revision rejected this by quoting NFR-3's "at most 25 individual files". That cap is
retired; the byte budget replaced it.)*

## References

- Requirements: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)
- Related ADRs:
  - [ADR 0071: Shiftable Review Gear for the TDD Approval Gate](0071-tdd-review-gear.md) — tonal and
    structural prior art: the only other ADR recording a decision about a `/spec:*` command's own
    behaviour rather than Brighter's runtime.
  - [ADR 0073: The Advisory Risk Model for `/spec:show-me`](0073-show-me-advisory-risk-model.md) —
    the three-factor risk model (FR-11, FR-12, FR-13). It consumes this ADR's outputs: the fact
    ledger from Step 5, FR-7's item count (F2) and FR-8's deviation entries (F5), and writes into
    the `## Risk assessment (advisory)` slot that FR-5's fixed order already reserves.
  - [ADR 0077: Visual Explanation in `/spec:show-me`](0077-show-me-visual-explanation.md) — when the
    command draws a diagram and which stage may read source to draw it (FR-6's
    `##### Visual explanation`, FR-14's optional tree). It consumes this ADR's Step 5 blast-radius
    counts as its trigger values and spends the read budget this ADR allocates.
- Conventions and prior art in this repository:
  - [`.claude/commands/spec/status.md`](../../.claude/commands/spec/status.md) — read-and-report
    command with no sub-agent; Step 1's spec-metadata gathering pattern.
  - [`.claude/commands/spec/review.md`](../../.claude/commands/spec/review.md) — Step 1 precondition
    checks, the git-inspection front matter (including `Bash(git merge-base:*)`), and the
    write-the-artefact-after-validating pattern for `review-{phase}.md`.
  - [`.claude/commands/spec/switch.md`](../../.claude/commands/spec/switch.md) — the pre-executed
    `!` shell block and whole-`$ARGUMENTS` handling.
  - [`.claude/commands/spec/README.md`](../../.claude/commands/spec/README.md) — command catalogue
    and the sub-agent/model policy.
  - [`.claude/settings.json`](../../.claude/settings.json) — the read-only allow-list; the `gh`
    entries are exactly `gh pr view`, `gh pr list`, `gh pr diff`, `gh issue view`, `gh issue list`.
  - [`.agent_instructions/adr_frontmatter.md`](../../.agent_instructions/adr_frontmatter.md) — "the
    number is a non-unique ordering hint; identity is the filename stem", which is what FR-6's
    slug rule implements.
