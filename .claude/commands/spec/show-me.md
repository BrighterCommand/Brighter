---
allowed-tools: Bash(cat:*), Bash(ls:*), Bash(test:*), Bash(grep:*), Bash(head:*), Bash(wc:*), Bash(awk:*), Bash(date:*), Bash(git log:*), Bash(git diff:*), Bash(git rev-parse:*), Bash(git merge-base:*), Bash(git ls-files:*), Bash(git branch:*), Bash(gh pr list:*), Bash(gh pr view:*), Bash(gh pr diff:*), Read, Write, Glob, Grep
description: Summarise a finished spec and give an advisory merge-risk read
argument-hint: [spec-id]
---

## Available specifications

!`ls -1d specs/*/ 2>/dev/null`

## Your Task

Summarise the finished spec named by `$ARGUMENTS` (or the current spec, if none is given) into
`specs/{spec}/show-me.md`: what changed and why, breaking changes, a requirement-by-requirement
reconciliation, how it was built, blast radius, an advisory Low/Medium/High merge-risk read, where
to look first, and a full provenance table.

The procedure runs in the order below, because each step consumes the previous step's fact-ledger
rows: **scaffolding → precondition gate → branch/PR/diff resolution → measurement → section
synthesis → review-history classification → risk scoring → write → budget check → docs.**

## Roles, the fact ledger, and global invariants

*(Referenced by every step below; stated once here rather than repeated.)*

### The three roles

| Role | Stereotype | Owns | Mechanism |
|------|-----------|------|-----------|
| **Measurer** | information holder | Every value NFR-1 requires identical between runs | `git`/`gh`/`grep`/`awk` invocations whose output is a count, a sha, a ref name or a short line list |
| **Classifier** | decider | The review-round/finding/severity/resolution tallies and F1–F5's levels | Applying the Definitions' stated rules to Measurer-produced extracts; emits ledger rows, never prose |
| **Synthesiser** | decider | All prose | Rendering each section's lines from ledger rows alone |

Two crossing prohibitions hold throughout: **the Measurer never paraphrases**, and **the
Synthesiser never counts** — no shell call happens during Step 6. The Classifier may read only
Measurer-produced extracts, never the full comment bodies it derives from, and every row it emits
carries the rule it applied and the line it applied it to, not just a value.

### The fact ledger

An in-context table, built up as the command measures — never a file, since this command permits
exactly one `Write` and a marker file would give some other command something to read as a gate.
Each row has the shape:

`{input or metric} | {value} | {command or path it came from} | used | not available: {reason}`

Because the ledger is in-context rather than on disk, a Classifier row is observable in the
session transcript — which is where several verification steps assert against it.

**Traceability rule**: a section may state only values present in the ledger, and must name the
ledger row it came from.

**The ledger's one deliberate exception**: `PROMPT.md` and its `PROMPT-*.md` companions get no
ledger row at all. A row would surface them in `## Inputs used`, and they must never be cited.

### Global invariants

- **One `Write` only.** The command creates or modifies exactly one file, `specs/{dir}/show-me.md`.
- **No step may branch on the level.** The three levels — Low, Medium, High — are rendered values,
  never conditions any step tests, in `## Risk assessment (advisory)` or the session report.
- **Skeleton-first for PR comments.** A PR's comment history is measured as a structural skeleton
  (heading, numbered-title and reply lines only) before any body text; the full comment JSON is
  never read into context.
- **BSD-compatible POSIX-class regexes only** — macOS `grep` has no `\b`:
  - task checkbox: `^[[:space:]]*-[[:space:]]\[[ xX]\]`
  - public-API declaration line: `^[+-][[:space:]]*(public|protected)[^[:alnum:]_]`

### Why no sub-agent

This command runs entirely in the main agent. The README's stated rationale for delegating to a
sub-agent — that a `Plan` sub-agent has no file-editing tool, so it is harder to let it do damage —
is empty here, because the whole command is read-only apart from one `Write`. Against delegation: a
clean-context sub-agent would have to be handed the entire fact ledger and every extract in its
prompt, or re-read the inputs itself and spend the extraction budget twice; and delegated judgement
is unauditable — the ledger row and the transcript are what make a Classifier tally checkable at
all.

### Step 0 — Pre-flight

Owns: the candidate spec-directory list, seeded above in `## Available specifications` for free on
every invocation.

### Step 1 — Resolve the target spec

Owns: turning `$ARGUMENTS` (or, absent, `specs/.current-spec`) into exactly one spec directory, or
stopping with an ambiguity/no-match/missing/empty/stale message and writing nothing.

**With an argument.** Take `$ARGUMENTS` **whole**: trim leading/trailing whitespace, strip wrapping
quotes, preserve internal whitespace verbatim. Never split on whitespace —
`specs/0021-Expose Unacceptable Message Window/` is a real directory, and an unquoted shell loop
variable would split it into four tokens. For this reason the match is performed by the executing
model reading Step 0's line list, not by a shell `for` loop.

Candidates are only the directory entries `## Available specifications` lists — never
`specs/README.md`, `specs/dlq-review-findings.md`, or a dotfile such as `specs/.current-spec`.

Apply this ordered rule set, stopping at the first rule that yields exactly one match:

1. Exact directory-name match (`0036-scoped-lifetime-per-pipeline`).
2. Exact four-digit id match (`0036`).
3. Case-insensitive substring match on the directory name (`scoped-lifetime`).

- **More than one match** — stop without writing any file and print exactly:
  `Ambiguous spec id '{arg}' — matches: {list of matching directory names}. Re-run with the full
  directory name.`
- **No match** — stop without writing any file and print exactly:
  `No spec matches '{arg}'. Run /spec:status to list specs.`

**With no argument.** Read `specs/.current-spec` (`cat specs/.current-spec`) and target the spec
directory it names, verified with `test -d "specs/{value}"`. Whitespace-only content is treated as
`empty`. If the file is missing, empty, whitespace-only, or names a directory that does not exist
under `specs/`, stop without writing any file and print exactly:
`No spec id given and no usable current spec (specs/.current-spec is {missing|empty|stale: names
'{value}'}). Pass a spec id (/spec:show-me 0036-scoped-lifetime-per-pipeline) or run /spec:switch
first.`
selecting `missing`, `empty`, or `stale: names '{value}'` for the bracketed word.

### Step 2 — Completeness check

Owns: refusing to summarise a spec whose `tasks.md` is absent, has zero checkboxes, or has any
unchecked box — printing the exact refusal and writing nothing.

Run exactly these three bounded `grep`s against `specs/{dir}/tasks.md` — never `Read` the file
(spec 0036's is 229 KB) — and check each command's **exit status**, not just its output, so a
failed extraction is never reported as a real zero:

```bash
test -f "specs/{dir}/tasks.md"
grep -cE '^[[:space:]]*-[[:space:]]\[[ xX]\]' "specs/{dir}/tasks.md"   # total
grep -cE '^[[:space:]]*-[[:space:]]\[ \]'      "specs/{dir}/tasks.md"   # unchecked
grep -m3 -E '^[[:space:]]*-[[:space:]]\[ \]'   "specs/{dir}/tasks.md"   # first three titles
```

- **`tasks.md` absent** — stop and print exactly:
  `Spec {dir} has no tasks.md — /spec:show-me runs only against a finished spec. Current phase:
  run /spec:status.`
- **`tasks.md` present, zero checkboxes** — stop and print exactly:
  `Spec {dir}'s tasks.md contains no task checkboxes — nothing to summarise.`
- **`tasks.md` present, ≥ 1 unchecked checkbox** — stop and print exactly:
  `Spec {dir} is not finished: {n} of {total} tasks are still unchecked. First unfinished: {first
  three unchecked task titles, one per line}. /spec:show-me runs only against a finished spec.`

**The precondition gate's contract, stated once: if any check in Steps 1–2 fails, print the exact
message given and stop; do not proceed to Step 3; do not write, create or touch any file.** This is
the only circumstance in which the command declines to produce output — every absence discovered
from Step 3 onward is a degradation (FR-16), not a refusal.

### Step 3 — Spec branch, base ref, merge base

Owns: resolving the spec's git branch (or recording it as not determinable), the base ref, and the
merge-base sha — the coordinates every later measurement is taken against.

Let `name` = the spec directory name with the leading `NNNN-` removed. Try, in order, stopping at
the first that succeeds:

```bash
git rev-parse --verify --quiet "refs/remotes/origin/spec/${name}"   # rule 1, remote-tracking wins
git rev-parse --verify --quiet "refs/heads/spec/${name}"            # rule 1, local fallback
git rev-parse --abbrev-ref HEAD                                     # rule 2, if it contains ${name}
git log --oneline "{base}..HEAD" -- "specs/{dir}/"                  # rule 3, non-empty ⇒ HEAD
```

When both a remote-tracking and a local branch of that name exist, the **remote-tracking branch
wins** — it is the last-pushed state a PR reviewer sees and the state a discovered PR's diff
reflects.

Resolve the base ref the same way: `origin/master` when `git rev-parse --verify --quiet
origin/master` succeeds, else `master`. Take the merge base with an explicit `git merge-base {base}
{head}` — not `git diff`'s three-dot form — because the sha itself must be reported.

Emit, verbatim except for the bracketed values:
`Ref used: {full ref} at {sha}; base ref {base ref} at {sha}; merge base {sha}.`
and, only when rule 1 selected the remote-tracking ref while a local branch of the same name sits at
a **different** sha:
`Local branch {name} is at {sha} and differs from the measured ref.`
— omitted entirely when the two shas are equal.

If all three rules fail, the branch is **not determinable**. This does not stop the run — no
absence discovered from here on does. Record the ledger row `spec branch | not determinable | rules
1–3 tried` and carry these four consequences into Step 6 (FR-16 rows 12–15):

- **`## Blast radius`** states `Spec branch not determinable — no diff measured.` followed by the
  rules tried, and **F1 scores Medium**.
- **The metadata block**'s spec-branch, head-commit-sha and merge-base-sha lines each read the
  single word `undetermined`. The base ref is still resolved and named normally — it does not
  depend on the spec branch.
- **The PR-reference line** reads `none found`, because FR-20's PR discovery needs the branch name.
- **`## Where to look first`** takes its defined fallback with no paths (row 13), and
  **`## Breaking changes`** adds the line stating public-API declaration lines could not be
  inspected while still carrying a count line (row 14) — both built where those sections are
  written, in Phase 3, and cross-referenced here so the four behaviours stay in step.

### Step 4 — PR discovery and diff-source election

Owns: finding the one pull request for the spec branch (if any) and electing exactly one diff
source — the PR diff or a local `git diff` — never both.

Strip any remote prefix from the resolved branch name, then:

```bash
gh pr list --head "spec/${name}" --state all --json number,url,headRefName,createdAt
```

Keep **only** results whose `headRefName` equals `spec/${name}` exactly — `gh`'s own matching is
loose, so this filter is load-bearing.

- **Exactly one result** — that is the spec's PR.
- **More than one result** — the **highest number** wins (PR numbers are monotonic with creation),
  and the ledger records `{k} pull requests found for branch {branch}; using #{n} (highest
  number).` for `## Blast radius`.
- **Zero results, or a non-zero exit from `gh`** (unavailable, unauthenticated, offline) — no PR
  (FR-16 rows 1–2). `## How it was built` then states `No pull request found for branch {branch}
  — no external review findings available.` (for the `gh`-failure variant, the ledger additionally
  records `not available: gh unavailable`); **F3 and F4 both score Medium**; the branch may still
  be resolvable, so blast radius is still measured — from `git diff`, never from a PR that does not
  exist.

`.issue-number` is **never** consulted here — it names the spec's tracking *issue*, not its PR
(spec 0036's `.issue-number` is 4256 while its PR is #4282), and feeds only the metadata block's
linked-issue line.

Elect **exactly one** diff source, never mixed or averaged:

```bash
gh pr diff {n} --name-only      # a PR was found and this succeeds ⇒ the PR diff is the spec diff
git diff --name-only "{mb}..{head}"   # otherwise
```

Name the chosen source (its ref/PR number and shas) — this is what `## Blast radius`'s `Measured
from …` line reports.

### Step 5 — Bounded extraction

Owns: every counted value in the fact ledger — blast-radius buckets, net lines, public-API
declaration lines, commit count, task/requirement/ADR extraction — each produced by a bounded
`git`/`gh`/`grep`/`awk` pipeline, never by reading `tasks.md` or the diff in full.

#### Blast radius, without reading the diff

`FILES` is the elected source's file list (`gh pr diff {n} --name-only` or `git diff --name-only
"{mb}..{head}"`). Six buckets, always all listed even when zero, with `other` as the **complement**
so the six counts sum to the total by identity rather than by care:

```bash
FILES | grep -cE '^src/'      ; FILES | grep -cE '^tests/'
FILES | grep -cE '^docs/'     ; FILES | grep -cE '^specs/'
FILES | grep -cE '^\.github/' ; FILES | grep -vcE '^(src/|tests/|docs/|specs/|\.github/)'
FILES | wc -l
```

Net lines (`+a/−b`) per bucket:

```bash
# git source
git diff --numstat "{mb}..{head}" | awk '{a+=$1; d+=$2} END {print a+0, d+0}'
# PR source — gh pr diff has no --numstat, so the patch is fetched a second time for this
gh pr diff {n} | grep -cE '^\+([^+]|$)' ; gh pr diff {n} | grep -cE '^-([^-]|$)'
```

Public-API declaration lines, restricted to `src/`:

```bash
# git source
git diff -U0 "{mb}..{head}" -- src/ \
  | grep -cE '^[+-][[:space:]]*(public|protected)[^[:alnum:]_]'
# PR source — track the current file from the +++ header
gh pr diff {n} | awk '/^\+\+\+ b\//{f=substr($2,3)}
                      f ~ /^src\// && /^[+-][ \t]*(public|protected)[^A-Za-z0-9_]/ {c++}
                      END {print c+0}'
```

Commit count, never `git rev-list --count` (keeps the tool surface inside what the repo grants):

```bash
git log --oneline "{mb}..{head}" | wc -l
```

Emit `## Blast radius`'s `Measured from {gh pr diff #N (head {sha}) | git diff {merge-base
sha}..{head sha}}` line, naming exactly one source.

#### The 25-full-read budget

| Input | Mechanism | Counts toward 25? |
|---|---|---|
| `tasks.md` | `grep -c` / `grep -m3` extraction only | no |
| `requirements.md` | declared-id `grep`s, then **one** full `Read` for paraphrases | 1 |
| `.adr-list`, `.issue-number`, `.current-spec` | `cat` | no |
| each ADR named in `.adr-list` | `head -14` (front matter) + `grep -A 25 '^## Consequences'` | **excluded** |
| `release_notes.md` section | `grep -n` for the spec's heading, then `grep -A` for its bullets | no |
| files for `## Where to look first` | `Read` on demand, only where the diff path list is not self-explanatory | the remainder |

The ADR exclusion is what keeps a 15-ADR spec inside the cap.

Derive FR-9's per-tag task counts from the same checkbox `grep` family, with `untagged` as the
**complement** so the parts always sum to the total:

```bash
CHECKBOXES='^[[:space:]]*-[[:space:]]\[[ xX]\]'
grep -E "$CHECKBOXES" "specs/{dir}/tasks.md" | grep -cE '(TEST \+ IMPLEMENT|STRUCTURAL|PROJECT|DOC)'   # per tag, one grep each
grep -E "$CHECKBOXES" "specs/{dir}/tasks.md" | grep -vcE '(TEST \+ IMPLEMENT|STRUCTURAL|PROJECT|DOC)'  # untagged, the complement
```

**NFR-3's degradation rule.** Read in bounded chunks or by targeted extraction, never the full file
or the full diff. If an extraction could not be completed, report the value as `Unverifiable`
(FR-8) or `not available` (FR-15) **with the reason** — never as zero, never omitted. Check exit
status, not just empty output, so a failed extraction is never indistinguishable from a real zero.

#### `.adr-list`, `.issue-number`, and the `release_notes.md` section

`cat specs/{dir}/.adr-list`. **Normalise each entry first by stripping a leading `docs/adr/`**,
then resolve with `ls docs/adr/ | grep -E "^{normalised entry}"` — the strip is load-bearing,
because `ls docs/adr/` emits bare filenames and a path-prefixed entry can never match without it.
Tolerate both entry shapes present in this repository — a bare filename
(`0062-pg-advisory-lock-sha256.md`) and a path-prefixed entry
(`docs/adr/0040-asyncapi-document-generation.md`) — so the **shape** is never the reason an entry
fails; whether it resolves is purely a question of whether the named file exists. For each ADR that
resolves, record one ledger row with its resolved path, title and Status, read from the ADR's own
front matter/body by `head -14` plus `grep`.

- **`.adr-list` missing or empty** — ledger row for FR-16 row 6; `## What changed and why` states
  `No ADRs recorded for this spec.` and is synthesised from `requirements.md`, `tasks.md` and the
  commits instead. No factor consequence.
- **An entry does not resolve to exactly one file** (FR-16 row 7) — a bare number matching no file:
  `{entry} — ADR file not found in docs/adr/.` A bare number matching more than one file (C-9):
  `{entry} — ambiguous ADR number, matches: {filenames}; .adr-list should name the full filename
  instead.` Either way the narrative continues with the remaining ADRs, the entry is marked `not
  available` in `## Inputs used`, and the run succeeds.

`cat specs/{dir}/.issue-number`. Missing, empty or whitespace-only ⇒ the metadata block's linked
issue reads `none` and `## Inputs used` marks it `not available: not present` (FR-16 row 10). No
factor consequence.

Locate a `release_notes.md` section for the spec by `grep -n` against the file's own heading
convention (`### Title (spec NNNN)`), then `grep -A` for its bullets. Absence ⇒ FR-16 row 5: `##
Breaking changes` adds the line `No release_notes.md section found for this spec; this list is
derived from the ADRs and the diff.` `release_notes.md` is **never** modified and never a
prerequisite for the run.

### Step 6 — Section synthesis

Owns: assembling the eight `## ` sections from the fact ledger alone. No shell call happens in this
step; the ledger is read, not re-derived.

#### Header and metadata block

Emit `# Show me — {spec directory name}` (the directory name only, not its full path), then, each on
its own line:

```
Generated: {today's date, ISO-8601}
Spec: specs/{dir}/
Issue: {`.issue-number` value, or `none`}
Spec branch: {the full ref Step 3 resolved, or `undetermined` — FR-16 row 15}
Head commit: {short sha, or `undetermined`}
Base ref: {base ref} at {short sha}
Merge base: {short sha, or `undetermined`}
Pull request: {`#{n} {url}`, or `none found`}
```

Every value is the matching ledger row (FR-5); none is recomputed here. When Step 3 recorded the
spec branch as not determinable, the spec-branch, head-commit and merge-base lines each read the
single word `undetermined` (FR-16 row 15) — the base ref is still resolved and named normally, and
the rest of the block is populated as usual.

#### The eight H2 sections

Emit these headings, with these exact spellings, in this exact order, **all always present** — a
section with no content to report states the absence instead of being omitted (FR-16):

1. `## What changed and why`
2. `## Breaking changes`
3. `## Did it ship what it said?`
4. `## How it was built`
5. `## Blast radius`
6. `## Risk assessment (advisory)`
7. `## Where to look first`
8. `## Inputs used`

Each section's synthesis rule is defined below, under its own heading.

#### `## What changed and why`

Measured: the `.adr-list` entry set; each resolved ADR's title and Status, read from its own front
matter/body; the resolved relative link path. Judged: 150–600 words of prose (NFR-2's
whitespace-token count) stating what the spec set out to fix, what a Brighter user can now do or
what now behaves differently, and the one or two decisions that most shaped the result — never a
bullet dump, never a copy-paste of an ADR's *Decision* section.

Name **every** ADR resolved from `.adr-list` at least once, each with its title and current Status,
linked relatively with the filename stem visible in the link text:
`[{title} ({stem})](../../docs/adr/{stem}.md)`. A bare-number reference (`ADR 0070`) is forbidden
anywhere in the file — `docs/adr/` carries duplicate numbers across specs (C-9), so a number alone
does not identify a file. Gloss any internal type name on first use (A-2).

`.adr-list` missing or empty (FR-16 row 6): state exactly `No ADRs recorded for this spec.` and
synthesise the narrative from `requirements.md`, `tasks.md` and the commits instead — no factor is
affected by the absence.

An individual `.adr-list` entry that Step 5 could not resolve to exactly one file (FR-16 row 7) is
named inline, using Step 5's own ledger text verbatim — `{entry} — ADR file not found in
docs/adr/.` for zero matches, or `{entry} — ambiguous ADR number, matches: {filenames}; .adr-list
should name the full filename instead.` for more than one (a bare number colliding across specs,
C-9). The narrative continues normally with whatever ADRs did resolve; the unresolved entry carries
no factor consequence and is marked `not available` for `## Inputs used`.

#### `## Breaking changes`

Measured: public-API declaration lines in the spec diff (`src/` only); the `release_notes.md`
bullet boundaries when a section was read; the final `Total breaking-change items: {n}` count line.
Judged: what constitutes one item, its classification set, and its one-sentence migration.

Derive items from the spec's own artefacts: the ADRs' *Consequences* sections, `requirements.md`,
and the public-API declaration lines in the spec diff. Emit one bullet per item — a one-sentence
statement of what breaks, its classification as a **set** of one or more of source / binary /
behavioural / compatibility (e.g. "source and binary"), and the migration in one sentence. End with
`Total breaking-change items: {n}` matching the bullet count.

When a `release_notes.md` section for the spec was read, follow **the catalogue's own bullet
boundaries** as the tie-break for what counts as one item, rather than re-partitioning them; when
the counts disagree, add `release_notes.md records {m} items; this summary identifies {n}`.

**FR-16 row 5** (no `release_notes.md` section): add the line `No release_notes.md section found for
this spec; this list is derived from the ADRs and the diff.` — **unless row 14 is also active** (the
spec branch is not determinable, so no diff exists at all): row 14's own line already states the
fallback accurately, and stating "…and the diff" would be false when there is none, so row 14 wins
and row 5's line is not additionally emitted.

**FR-16 row 14** (branch not determinable, carried over from Step 3): derive the item list from the
ADRs' *Consequences* sections and `requirements.md` only, add the line `No diff measured — this
list is derived from the ADRs and requirements.md only; public-API declaration lines could not be
inspected.`, and keep the count line present — F2 is still computed from whatever items were found,
not suppressed by the degradation.

Zero items: the section contains exactly `No breaking changes identified for this spec.` and `Total
breaking-change items: 0` — no absence line is added even if one would otherwise apply, since there
is nothing for it to qualify.

#### `## Blast radius`

Purely a rendering of ledger rows Steps 3 and 5 already produced — no new measurement or judgement
happens here. Render, in order: the `Measured from …` line (Step 5); the six-bucket file-count table
with `other` as the stated complement; the per-bucket net-lines counts; the public-API declaration
line count restricted to `src/`; the commit count. When Step 3 recorded the spec branch as not
determinable, replace all of the above with its FR-16 row 12 text — `Spec branch not determinable —
no diff measured.` followed by the three rules tried — and still score F1 Medium, per Step 3.

### Step 7 — Write

Owns: the single `Write` of `specs/{spec}/show-me.md` — the only file this command ever creates or
modifies.

### Step 8 — Budget self-check and session report

Owns: checking the written file's word count against its budget (revising and re-writing if
outside it) and printing the session report: path written, created-or-replaced, overall risk
level, and the advisory reminder.
