---
id: 0072-show-me-command-resolution-and-output
title: "Target Resolution and Output Shape for /spec:show-me"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-09-19
summary: "/spec:show-me is implemented as a single self-contained prompt file at .claude/commands/spec/show-me.md, structured as a precondition gate (FR-1/FR-2/FR-3 stops, above any write) followed by a bounded measurement phase that builds an in-context fact ledger with git/gh/grep/awk counts, a synthesis phase constrained to cite only ledger entries, and a single Write of show-me.md."
tags:
  - "meta"
  - "api-design"
---

# 0072. Target Resolution and Output Shape for `/spec:show-me`

Date: 2026-09-19

## Status

Accepted

## Context

Spec 0037 asks for a new slash command, `/spec:show-me [spec-id]`, that runs after a spec's
implementation is finished and writes one durable markdown file, `specs/NNNN-name/show-me.md`,
summarising what the spec actually changed and how risky it looks to merge. The material already
exists — ADRs, `requirements.md`, `tasks.md`, the git history, the pull request and its review
rounds — but it is scattered and asymmetric in cost, so "is this safe to merge?" has been an ad hoc
judgement re-derived from scratch in every session.

**Parent Requirement**: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)

**Scope**: This ADR covers command invocation, spec/branch/PR resolution, and output file
structure. A separate ADR (to follow) covers review-history decomposition and the risk-scoring
model (FR-9, FR-11–FR-13).

### What requirements.md already fixed, and what it left open

`requirements.md` went through six rounds of adversarial review and is unusually prescriptive: it
fixes the resolution rule sets (FR-1, FR-2, FR-10, FR-20), the exact section set and order (FR-5),
the verbatim stop messages (FR-1, FR-2, FR-3), the degradation table (FR-16, fifteen rows) and the
budgets (NFR-2's 400–2,000 counted words, NFR-3's 25-full-file cap). Those are not re-decided here.

What it deliberately did **not** decide is the *shape* of the thing that implements them:

- In what artefact does the command live, and what executes it?
- In what order do the steps run, and which of them use targeted shell extraction versus a full
  file read — given that NFR-3 caps full reads at 25 and spec 0036's `tasks.md` alone is 229 KB?
- Where do FR-1/FR-2/FR-3's refusals and FR-16's degradations physically live, in an artefact that
  has no exception handling and no control flow beyond prose?
- Which output content is *counted* and which is *judged*, and how does the command keep the two
  from contaminating each other?

That last question is the load-bearing one. NFR-1 already draws the line: the section set and order,
every `## Blast radius` number, the task total and per-tag counts, the CI tally, FR-8's row count and
the set of requirement ids, and factor levels F1 and F4 must be **identical** between runs on
unchanged inputs; while FR-7's breaking-change item list, FR-8's per-requirement statuses and the
review-round decomposition are named as "judgement-derived synthesis and are not required to be
identical between runs". NFR-7 then requires every factual claim to be attributable to a listed
input. Those two requirements together are a responsibility split, and this ADR's job is to make it
structural rather than aspirational.

### Constraints this ADR inherits

- **C-10 / FR-18** — GitHub access is confined to `gh pr view`, `gh pr list` and `gh pr diff`.
  Verified against `.claude/settings.json`, whose `gh` allow-list is exactly
  `Bash(gh pr view:*)`, `Bash(gh pr list:*)`, `Bash(gh pr diff:*)`, `Bash(gh issue view:*)`,
  `Bash(gh issue list:*)`. No `gh run`, `gh api` or `gh checks`. Changing that allow-list is Out of
  Scope.
- **C-1 / FR-1** — spec ids are not unique (`0002-backstop-error-handler`, `0002-sqs-cleanup`,
  `0002-universal_scheduler_delay` all exist on `master`) and one real spec directory name contains
  spaces (`specs/0021-Expose Unacceptable Message Window/`). Any candidate enumeration or matching
  mechanism must survive both.
- **C-9 / FR-6** — ADR numbers are not unique (`docs/adr/` on `master` carries five files numbered
  `0037` and four numbered `0057`). Identity is the filename stem, per
  `.agent_instructions/adr_frontmatter.md`; bare-number ADR references are forbidden anywhere in
  `show-me.md`.
- **NFR-6** — the command file must follow the existing `/spec:*` conventions. Verified: every
  command under `.claude/commands/spec/` is a single markdown file with YAML front matter carrying
  `allowed-tools` and `description`; nine of the eleven carry `argument-hint` (`status.md` and
  `tasks.md`, the two that take no argument, do not). There is no build step, no script, and no
  compiled helper anywhere in the family.
- **FR-18 / NFR-8** — the command writes exactly one file and mutates nothing else; a stop under
  FR-1/FR-2/FR-3 must leave the repository byte-for-byte unchanged.

The closest prior art in this repository is
[ADR 0071: Shiftable Review Gear for the TDD Approval Gate](0071-tdd-review-gear.md) — the only other
ADR that designs a `/spec:*` command's own behaviour rather than Brighter's C# runtime. Like this
one, it records a decision about the project's own agent tooling; unlike the library's usual
interface-heavy ADRs, neither has a C# component at all. There is no other prior art for this shape.

## Decision

Implement `/spec:show-me` as a **single self-contained prompt file**,
`.claude/commands/spec/show-me.md`, whose procedure is an explicit, ordered sequence of steps that
first **measures** the spec with bounded shell commands into an in-context *fact ledger*, then
**synthesises** the output sections from that ledger alone, then writes `show-me.md` in one `Write`
call.

The organising principle is a two-role split, applied throughout:

| Role | Stereotype | Owns | Mechanism |
|------|-----------|------|-----------|
| **Measurer** | information holder | Every value NFR-1 requires to be identical between runs | `git`/`gh`/`grep`/`awk` invocations whose output is a count, a sha, a ref name or a short line list |
| **Synthesiser** | decider | Every value NFR-1 names as judgement-derived, plus all prose | The executing model's own reading, constrained to cite ledger entries |

Nothing crosses the line in either direction: the Measurer never paraphrases and the Synthesiser
never counts. FR-15's `## Inputs used` table is written *from* the ledger, which is what makes
FR-16's degradation auditable and NFR-7's traceability checkable rather than merely asserted.

### Architecture Overview

```
/spec:show-me [spec-id]
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ Step 0  Pre-flight: !`ls -1d specs/*/`  (candidate list)     │
└─────────────────────────────────────────────────────────────┘
        │
┌───────┴─────────────────────────────────────────────────────┐
│ PRECONDITION GATE — no file is written above this line       │
│  Step 1  Resolve target spec       FR-1 / FR-2  → stop msg   │
│  Step 2  Completeness check        FR-3         → stop msg   │
└─────────────────────────────────────────────────────────────┘
        │  (past this point the command ALWAYS writes a file — FR-16)
        ▼
┌─────────────────────────────────────────────────────────────┐
│ MEASURE — appends to the fact ledger, one row per value      │
│  Step 3  Spec branch, base ref, merge base       FR-10       │
│  Step 4  PR discovery + diff source election     FR-20       │
│  Step 5  Bounded extraction:                     NFR-3       │
│            tasks.md · requirements.md ids · .adr-list        │
│            + per-ADR front matter/Status/Consequences        │
│            + .issue-number · release_notes.md section        │
│            + blast-radius stats · CI rollup · PR comments    │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ SYNTHESISE — ledger in, prose out; no new shell calls        │
│  Step 6  Sections 1–8 in FR-5's fixed order                  │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
  Step 7  Write specs/{spec}/show-me.md  (one Write call)   FR-4
  Step 8  Budget self-check + FR-19 session report
```

### Key Components

#### 1. The command file

One markdown file, `.claude/commands/spec/show-me.md`, plus a catalogue entry in
`.claude/commands/spec/README.md` (required by NFR-6 and asserted by AC-53). No script, no binary,
no library, no sub-agent.

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

FR-16's fifteen degradation rows are the mirror image and live *below* the gate, attached to the step
that discovers each absence: each records a ledger row (`not available: {reason}`), sets the
section's defined fallback text, and continues. No absence below the gate stops the run.

#### 3. The fact ledger

An in-context table the command builds as it measures — never a file, since FR-18 permits exactly one
write. Each row is `{input or metric} | {value} | {command or path it came from} | used | not
available: {reason}`. The ledger is the single source for:

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
allowed-tools: Bash(cat:*), Bash(ls:*), Bash(test:*), Bash(grep:*), Bash(head:*), Bash(wc:*),
  Bash(awk:*), Bash(date:*), Bash(git log:*), Bash(git diff:*), Bash(git rev-parse:*),
  Bash(git merge-base:*), Bash(git ls-files:*), Bash(git branch:*), Bash(gh pr list:*),
  Bash(gh pr view:*), Bash(gh pr diff:*), Read, Write, Glob, Grep
description: Summarise a finished spec and give an advisory merge-risk read
argument-hint: [spec-id]
---
```

Two notes on that list. `Bash(git merge-base:*)` and `Bash(awk:*)` are **not** in
`.claude/settings.json`'s repo-wide allow-list, but both already appear in sibling commands' own
front matter (`review.md` declares `git merge-base`; `approve.md` and `design.md` declare `awk`), so
declaring them here needs no settings change — which keeps the Out of Scope rule "no change to
`.claude/settings.json`'s allow-list" intact. The `gh` entries are deliberately the three narrow
verbs rather than `Bash(gh:*)` (which `requirements.md` uses), so that FR-18's confinement is visible
in the file itself and AC-30's "every `gh` invocation is one of `gh pr view`, `gh pr list`,
`gh pr diff`" is checkable by reading the front matter.

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
task-type tag added, supplies FR-9's per-tag counts for the sibling ADR, and `untagged` is computed
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

#### Step 5 (continued) — the 25-read budget

The budget is spent as follows, and the command file states the allocation explicitly:

| Input | Mechanism | Counts toward 25? |
|---|---|---|
| `tasks.md` | `grep -c` / `grep -m3` extraction only | no read |
| `requirements.md` | `grep -nE '^\*\*(FR\|NFR)-[0-9]+'` and `grep -nE '^#{1,6}.*(FR\|NFR)-[0-9]+'` for declared ids, then a full `Read` for paraphrases | 1 |
| `.adr-list`, `.issue-number`, `.current-spec` | `cat` | no read |
| each ADR named in `.adr-list` | `head -14` (front matter) + `grep -A 25 '^## Consequences'` | **excluded by NFR-3** |
| `release_notes.md` section | `grep -n` for the spec's heading, then `grep -A` for its bullets | no read |
| files for `## Where to look first` | `Read` on demand, only where the diff path list is not self-explanatory | the remainder |

The ADR exclusion is what makes a 15-ADR spec tractable (AC-52). Note that `requirements.md`'s
declared-id grep gives FR-8's *row set* mechanically — which is why NFR-1 can require the row count
and id set to be identical between runs while leaving the statuses judged — and that sub-numbered
ids (`FR-27.3`) are excluded from the row set by the `[0-9]+` anchor and folded into their parent row
during synthesis (AC-45).

#### Step 6 — Synthesis, and what each section may claim

The command file states the split per section, because this is where NFR-7 is either enforced or
lost:

| Section | Measured (ledger) | Judged (synthesis) |
|---|---|---|
| `## What changed and why` (FR-6) | the `.adr-list` entry set; each ADR's title and Status from its front matter; the resolved link path | the 150–600-word narrative; which one or two decisions "most shaped the result" |
| `## Breaking changes` (FR-7) | public-API declaration lines; the `release_notes.md` bullet boundaries when a section exists; the final `Total breaking-change items: {n}` | what constitutes one item; its classification set; the one-sentence migration |
| `## Did it ship what it said?` (FR-8) | the row set and row count; the count line's arithmetic | each row's status, paraphrase, evidence, and the `Shipped beyond the requirements` list |
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
- **NFR-1's determinism claim is structural.** Because the Measurer owns every field NFR-1 lists and
  the Synthesiser is forbidden to produce a number, "these values are identical between runs" is a
  property of where the values come from, not a hope about model behaviour.
- **NFR-7 and FR-15 are the same mechanism.** The `## Inputs used` table is a projection of the
  ledger that also feeds every other section, so a claim with no ledger row has nowhere to appear and
  an input with no row cannot be silently used.
- **NFR-3's budget is met by construction, not by restraint.** `tasks.md` and the diff are never
  read — only counted — so the 229 KB file and the 517-file diff cost a handful of integers each, and
  the ADR exclusion keeps a 15-ADR spec inside the cap (AC-52).
- **The refusal surface is one block.** All three stop conditions sit in Steps 1–2 above every write,
  so AC-7's "byte-for-byte unchanged" is a property of step ordering rather than of remembering not
  to write.
- **The arithmetic that acceptance tests check is identity, not effort.** `other` as a bucket
  complement, `untagged` as a tag complement, and the count lines derived from the rows they count
  all make the sums correct by construction (AC-18, AC-15).
- **FR-18's confinement is visible in the file.** Declaring the three narrow `gh` verbs rather than
  `Bash(gh:*)` means AC-30 can be checked by reading fourteen lines of front matter.

### Negative

- **Nothing is cached.** Every run re-executes the whole `git`/`gh` sequence — branch resolution, PR
  list, PR diff, rollup, comment fetch — because FR-18 permits exactly one written file and there is
  therefore nowhere to put a cache. Re-running after a new review round costs the same as the first
  run, and on a 517-file PR the `gh pr diff` pipelines are the slow part. This is accepted: a cache
  would be a second write, and a stale cache would silently break NFR-1's "same inputs, same numbers"
  contract in the one direction that matters.
- **The judged sections are not byte-reproducible, and the overall level can move with them.** NFR-1
  says this out loud: FR-7's item list, FR-8's statuses and the review-round decomposition may vary
  between runs, and because F2, F3 and F5 feed FR-12's maximum, the headline level may vary too. The
  ledger constrains these to stay grounded in cited evidence; it does not make them deterministic,
  and this ADR does not claim otherwise.
- **The budgets can still bind on a large spec.** NFR-2's 2,000 counted words were calibrated against
  spec 0036 at a plausible 1,600–1,750 — real headroom, but not much. A spec with 25 breaking-change
  items and 40 numbered requirements would force the narrative toward FR-6's 150-word floor, and
  Step 8's self-check would surface that as a revise-and-rewrite rather than as a clean failure. The
  25-read cap has similar slack only because `tasks.md`, the diff and the ADRs are all excluded from
  it; a spec whose `## Where to look first` genuinely needed 20 file reads would leave almost nothing
  for `requirements.md`.
- **Shell extraction is brittle against format drift.** The checkbox regex, the task-type tags, the
  `+++ b/` header shape and the `release_notes.md` heading pattern are all conventions, not
  contracts. When `tasks.md` starts using a shape the regex misses, NFR-3's rule applies — report
  `Unverifiable`/`not available` with a reason, never zero — but the command will not notice the
  drift on its own.
- **Two `gh pr diff` invocations for the PR path.** Added and removed lines need separate `grep -c`
  passes over the same patch, so the PR is fetched more than once per run. Deduplicating would mean
  holding the patch, which NFR-3 forbids.

### Risks and Mitigations

**Risk**: The Synthesiser quietly recomputes a number the Measurer already owns — counting
breaking-change bullets by eye, or estimating a file count — and NFR-1's determinism guarantee
silently lapses.
- **Mitigation**: The command file states the split per section (Step 6's table) and forbids shell
  calls during Step 6. AC-32 tests exactly this by comparing two runs' mechanical fields; AC-34 tests
  that every count is attributable to a listed input.

**Risk**: A future edit adds a second write — a cache, a marker, a `.last-run` file — and breaks
FR-18/NFR-8 and, worse, gives some other command something to read as a gate.
- **Mitigation**: FR-13 and the Out of Scope list already forbid any marker file another command
  could read as a gate; this ADR records *why* the ledger is in-context rather than on disk. AC-30
  compares `git status --porcelain` before and after.

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

### Alternative 1: A helper script or compiled tool invoked by the command

Put the deterministic half — branch resolution, PR discovery, bucket counting, checkbox counting —
in a shell or Python script (or a small .NET tool) under `.claude/`, and have the command file call
it and synthesise around its JSON output.

**Rejected because**:
- There is no precedent in the `/spec:*` family. All eleven files under `.claude/commands/spec/` are
  single markdown files; the only executable artefact anywhere nearby is
  `generate_adr_index.awk`, a one-purpose generator invoked from documented one-liners. A script
  would be a new category for a single command.
- It adds a maintenance and review surface — a script with no test project in a repository whose
  entire test discipline is C#, plus a new allow-list entry to execute it — for a feature that
  produces one markdown file.
- The determinism it would buy is already available: the pipelines in Step 5 are deterministic
  *because they are pipelines*, whoever types them. Wrapping them in a script moves them, it does
  not make them more reliable.
- It would not touch the genuinely variable half. FR-7's item boundaries and FR-8's statuses are
  judgement by NFR-1's own admission; no script helps there.

### Alternative 2: Push more of the output into `gh`/`git` one-liners, with minimal synthesis

Maximise NFR-1's determinism by replacing the judged sections with mechanical proxies: derive
breaking changes purely from `^[+-]\s*(public|protected)` hunks, derive requirement status from
whether an FR id appears in a commit message, and drop FR-6's narrative in favour of
`git log --oneline`.

**Partially accepted, and rejected for the rest.** It is accepted wherever a mechanical answer is the
*right* answer — which is why `## Blast radius` and `## Inputs used` have no judged content at all,
and why FR-8's row set and count are mechanical even though its statuses are not. It is rejected for
the three sections NFR-1 names, for a concrete reason each:

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

**Rejected because**:
- Same absence of precedent as Alternative 1, with a larger surface.
- The narrative slots are most of the point. FR-6 (150–600 words), FR-7's migrations, FR-12's
  rationale and FR-14's reasons are the sections a reader actually reads; templating around them
  produces a document whose interesting parts are still model output but whose provenance is now
  split across two artefacts.
- It would make FR-16's fifteen degradation rows a program's error paths rather than prose next to
  the step that discovers each absence, which is where they are cheapest to keep correct.

### Alternative 4: Delegate synthesis to a `general-purpose` sub-agent

Measure in the main agent, then hand the ledger to a sub-agent (per the README's model policy) that
returns the eight sections as text for the main agent to write — the shape `/spec:review` uses.

**Rejected for this ADR's scope, and explicitly left open for the sibling.** The README's stated
reason for delegating — a sub-agent with no file-editing tool is harder to let do damage — does not
apply to a read-only command. Against it, a clean-context sub-agent must either receive the whole
ledger plus every extract in its prompt, or re-read the inputs and spend the NFR-3 budget twice; and
FR-19's report has to come from the main agent regardless. The balance may change for the sibling
ADR: reading a PR's full comment history to decompose review rounds is the most context-hungry part
of the whole command, and it is a self-contained input-to-counts transformation — a good candidate
for delegation if that ADR finds it needs one.

### Alternative 5: Read `tasks.md` and the diff in full and let the model count

Simply `Read` the 229 KB `tasks.md` and the 517-file diff, and count in-context.

**Rejected because**: NFR-3 forbids it outright ("without reading the full spec diff into context",
"at most 25 individual files"), and it would make every count a model judgement — collapsing NFR-1's
mechanical/judged split, which is the structural idea this ADR rests on.

## References

- Requirements: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)
- Related ADRs:
  - [ADR 0071: Shiftable Review Gear for the TDD Approval Gate](0071-tdd-review-gear.md) — tonal and
    structural prior art: the only other ADR recording a decision about a `/spec:*` command's own
    behaviour rather than Brighter's runtime.
  - **Forthcoming sibling ADR** — review-history decomposition (the `Finding` / `Review round` /
    `Finding severity` definitions and FR-9) and the five-factor risk-scoring model (FR-11, FR-12,
    FR-13, NFR-1's judged half). It consumes this ADR's outputs: the resolved PR from Step 4, the
    fact ledger from Step 5, FR-7's item count (F2) and FR-8's reconciliation (F5), and writes into
    the `## How it was built` and `## Risk assessment (advisory)` slots that FR-5's fixed order
    already reserves.
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
  - [`.agent_instructions/design_principles.md`](../../.agent_instructions/design_principles.md) —
    Responsibility-Driven Design; the Measurer/Synthesiser split is this document's "knowing" and
    "deciding" stereotypes applied to a command procedure rather than to classes.
