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
1–3 tried` and carry these five consequences into Step 6 (FR-16 rows 12–15):

- **`## Blast radius`** states `Spec branch not determinable — no diff measured.` followed by the
  rules tried, and **F1 scores Medium**.
- **The metadata block**'s spec-branch, head-commit-sha and merge-base-sha lines each read the
  single word `undetermined`. The base ref is still resolved and named normally — it does not
  depend on the spec branch.
- **The PR-reference line** reads `none found`, because FR-20's PR discovery needs the branch name.
- **`## Where to look first`** takes its defined fallback with no paths (row 13), and
  **`## Breaking changes`** adds the line stating public-API declaration lines could not be
  inspected while still carrying a count line (row 14) — both built where those sections are
  written, in Phase 3, and cross-referenced here so behaviours stay in step.
- **`## Inputs used`** marks both the `pull request` and `git history` rows `not available: spec
  branch not determinable` — neither was ever measured, so marking either `used` would violate the
  traceability rule. This is distinct from FR-16 rows 1–2's `gh`-specific reasons: here the branch
  itself, not `gh`, is why nothing was measured.

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

### Step 5R — Review-history measurement

Owns: measuring the elected PR's comment history as a **structural skeleton**, never as raw JSON
(ADR 0073 Key Components 1–2) — the inputs Step 5R.7 (Phase 4) and `## How it was built`/F3/F4
(Phases 4–5) are built from. Runs only when Step 4 elected a PR; produces ledger rows in the same
`{input or metric} | {value} | {command} | used | not available: {reason}` shape as Step 5.

```bash
# 5R.1 census — one line per comment
gh pr view {n} --json comments --jq '.comments[] | "\(.createdAt)\t\(.author.login)\t\(.body|length)"'

# 5R.2 structural skeleton — one invocation, boundary/heading/title lines only
gh pr view {n} --json comments \
  --jq '.comments[] | "===\(.createdAt)\t\(.author.login)\t\(.body|length)", (.body|split("\n")[])' \
  | grep -E '^===|^#{1,6}[[:space:]]|^[0-9]+\.[[:space:]]\*\*|^\*\*Fix'

# 5R.3 inline review submission stubs
gh pr view {n} --json reviews --jq '.reviews[] | "\(.submittedAt)\t\(.author.login)\t\(.state)"'

# 5R.6 author date of the commit that first added tasks.md
git log --diff-filter=A --format='%aI' "{base}..{head}" -- "specs/{dir}/tasks.md" | tail -1
```

**The skeleton (5R.2) is a named step with its own projection, never a shortcut through the
census.** On the calibration PR #4282 the raw `--json comments` payload is 68,837 bytes; the
skeleton is 10,151 — an 85% reduction. Reading the raw JSON "just to be safe" is the regression
that restores the case for delegating this measurement to a sub-agent, which ADR 0073 argued away.

Both `--jq` projections are arguments to the allow-listed `gh pr view`, never piped to a standalone
`jq` binary. The skeleton filter's four alternatives capture, in order: the comment boundary with
its timestamp, author and length; any ATX heading at any level; a top-level numbered item whose
title is bold; and the `**Fix N — …**` shape a verdict item uses when it sits outside its own
heading. The patterns use POSIX classes per Step 2's BSD-compatibility rule, and **no rule anywhere
in this procedure keys on heading level** — on the calibration PR, round 2 heads its findings
sequence `### New findings` while round 3 heads the same thing `## New findings`, and their items
are `#### 1.` and `### 1.` respectively; a rule keyed to one level would score one round zero.

5R.4 pulls **bounded body slices**, never a whole comment: only from comments 5R.2 marked as
carrying a findings sequence or a numbered disposition reply, and only between the located heading
and the next heading.

`gh` exiting non-zero at any of 5R.1/5R.2/5R.3 is FR-16 rows 1–2: the ledger row reads `not
available: gh unavailable`, `## How it was built` takes its defined no-PR line, and **F3 and F4
both score Medium**. Check **exit status**, not empty output — a PR with no comments and a failed
`gh` look identical on stdout (NFR-3).

#### Step 5R.7 — Classification (stages A–D)

Owns: turning 5R.2's skeleton and 5R.4's bounded slices into the Classifier's round/finding rows,
applying the Definitions' `Finding` and `Review round` clauses in order — quoted verbatim below,
never paraphrased, since paraphrasing them is how the review rounds that produced them get undone.
Stages E–H (attach/dedup, phase exclusion, severity, resolution) are later tasks; this stage only
qualifies, locates, splits and groups.

**A — Qualify.** Per `Review round` (b): a comment is part of a round "only if it contains at least
one finding — a numbered item that names a file, symbol, requirement or behaviour in the change
under review and asserts a defect, risk or requested change." Task-completion reports, progress
updates, CI notifications and finding-free discussion replies are **not** rounds, even under the
same `Claude finished @user's task` preamble a genuine round's own tracking comment also carries —
the discriminator is the presence of numbered findings in the body, never the preamble text.
**Judgement point 1** is only the residual case: a comment that *has* numbered items whose
assertions may or may not be defects.

**B — Locate the findings sequence.** In order, stopping at the first that applies:

1. A heading whose text names new findings (e.g. `### New findings`, `## New findings`) —
   **level-agnostic** — its numbered items running to the next heading at the same or higher level.
2. Failing that, the single numbered list describing defects/risks/requested changes.
3. **Excluded: fix-verdict sections.** Per `Finding`: "...a numbered or titled section that grades
   the *previous* round's fixes, e.g. `### Verdict on each fix`/`## Fix #1 — …: ✅`, which is
   evidence for `Resolved`/`Acknowledged` status, never a source of a new finding, even where one
   such verdict item is qualified or flags a residual concern."
4. **Excluded: the trailing unnumbered aside.** Per `Finding`: "A trailing titled-but-unnumbered
   aside after the findings sequence — e.g. a closing `Smaller notes` section — is never itself a
   source of findings, however many bullets it contains or what they assert."

**C — Split container items.** Per `Finding`: "A numbered item that is itself a **container** for
several distinct sub-issues (e.g. a 'smaller items' bucket of five bullets, itself numbered as one
item in the findings sequence) counts as one finding per sub-issue, not one." The discriminator
against B.4 is **numbering, not content**: `### 10. Smaller items` is item 10 of the sequence;
`### Smaller notes` has no number. Deciding whether a bucket's bullets are genuinely distinct
sub-issues is **judgement point 2**.

**D — Group comments into passes.** Three conjunctive tests from `Review round` (a): same author;
the later comment's numbering continues the earlier's without restarting; posted within 15 minutes
of each other. A comment whose numbering restarts at 1 begins a new round.

**E — Attach inline comments and dedup.** Take 5R.3's review-submission stubs (one
`{submittedAt}\t{author}\t{state}` line per submission) and attribute each to "whichever round's
tracking comment was posted within 15 minutes of them (before or after) and describes the same
finding" (`Review round` (a)), comparing **timestamp only**. **The 15-minute window is the whole of
the attachment test — the submission's author field is read off the stub but never compared against
the tracking comment's author.** Per
`Review round` (a): "A round's inline PR review comments belong to that round regardless of author
login even when it differs from the round's tracking-comment author — this repository's review
workflow posts a round's tracking comment and its inline comments under different bot logins
(`claude` vs `claude[bot]`)." **Dedup.** Per `Finding`: "When the same review pass posts an issue
both as an inline PR review comment and as a restatement inside its own tracking comment, this is
one finding, not two — the inline comment is the finding of record"; the tracking-comment
restatement is evidence of the same finding, never a second one.

**Known limitation — recorded as a ledger row, never hidden.** Inline review comment **bodies** are
unreachable inside the allow-list: `gh pr view --json reviews` returns `"body": ""` for every
submission, `gh pr view --comments` renders only issue comments, and only `gh api` — forbidden by
C-10 — would return them. The command therefore reads the tracking comment's **restatement**, and
treats the submission count as a **corroborating cardinality check** on the round's inline finding
count, never a repair: a mismatch between the submission count and the restated-finding count is a
ledger row, not something this procedure resolves on its own.

**F — Exclude specification-phase passes.** Applied to the **pass**, after grouping (stage D), so a
two-part pass is included or excluded as one unit — **D before F**. Per `Review round` (c), any of
three **disjunctive** tests suffices: (i) self-identification as a requirements/design/tasks review
(e.g. produced by `/spec:review requirements|design|tasks`, or its title names the phase, as in
"design only"); (ii) posted before the commit that first adds `specs/{target spec}/tasks.md` to the
branch, **by author date** (5R.6) — "a later rebase can move a commit's committer date without
changing when the file was actually written, so committer date is not used for this test"; (iii)
every finding the pass raises cites only paths under `specs/{target spec}/` or `docs/adr/`, with no
finding citing a source or test file in the diff. An excluded pass contributes **zero** rounds and
**zero** findings, and increments the counter FR-9 renders as `Specification-phase review passes
excluded: {n}`.

**Number surviving passes.** Passes that pass stage F are numbered `Round 1`..`Round k` in
chronological order — **F before numbering**, so an excluded pass never consumes a round number.

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

#### `## Did it ship what it said?` — row set (mechanical half)

Measured: which rows exist, their order, and the count line's arithmetic — this is exactly NFR-1's
determinism claim for this section, and exactly this much of it: the statuses that fill the rows are
judged and defined separately.

Build the row set from three declaration-shape `grep`s against `requirements.md`, never a full
`Read` for this part:

```bash
grep -nE '^\*\*(FR|NFR)-[0-9]+'                              "specs/{dir}/requirements.md"
grep -nE '^#{1,6}.*(FR|NFR)-[0-9]+'                           "specs/{dir}/requirements.md"
grep -nE '^[[:space:]]*[-*][[:space:]]*\*\*(FR|NFR)-[0-9]+'   "specs/{dir}/requirements.md"
```

Take the union of the three, extracting each match's `(FR|NFR)-[0-9]+` id. The `[0-9]+` anchor stops
at a literal `.`, so a sub-numbered clause (`FR-27.3`) is captured as its parent id (`FR-27`) and
never creates a row of its own — deduplicating to the unique id set is what folds it in. That unique
set **is** the row set: coverage is 100% by construction, since all three declaration shapes feed
the same union and nothing merely cross-referenced in prose is captured.

Order the rows `FR-1 … FR-n`, then `NFR-1 … NFR-n`, numerically — never by first-appearance order in
the file.

`requirements.md` missing (FR-16 row 8): the section contains exactly `No requirements.md found for
this spec — scope reconciliation is not possible.`; **F5 scores Medium**.

Zero declared ids (FR-16 row 9): the section contains exactly `requirements.md declares no numbered
requirements — nothing to reconcile.`, with no table and no count line; **F5 scores Medium**.

Otherwise, emit the table — columns requirement id, one-line paraphrase, status, evidence (the
values themselves are judged and defined separately) — and end with the count line: `Shipped: {a} ·
Shipped with deviation: {b} · Deferred: {c} · Dropped: {d} · Withdrawn: {w} · Unverifiable: {e} (of
{total})`, where `{total}` equals the row count and the six terms sum to it by construction.

#### `## Did it ship what it said?` — statuses and evidence (judged half)

Judged: every row's status, one-line paraphrase, evidence and (for non-`Shipped` rows) its reason
and follow-up; the `Shipped beyond the requirements` list.

For each row, read the requirement's own text in `requirements.md` together with `tasks.md`, the
spec diff, and any resolved ADRs to decide one status from the set `Shipped` / `Shipped with
deviation` / `Deferred` / `Dropped` / `Withdrawn` / `Unverifiable`:

- `Shipped` — built as declared; no reason required.
- `Shipped with deviation` — built, but materially different from what was declared (a narrower
  scope, a different mechanism, a documented trade-off) — state the deviation in one sentence.
- `Deferred` — not yet built, with a stated intention to build it later.
- `Withdrawn` — explicitly removed or superseded by a **recorded decision** taken during the spec's
  own lifecycle (in `requirements.md`, an ADR, a task, or the PR review thread) — cite where the
  decision is recorded.
- `Dropped` — not built, with no recorded decision to withdraw it — an absence, not a decision.
- `Unverifiable` — the evidence needed to judge it (a task, a diff line, a review comment) could not
  be found or read within the run's budget.

Give every row whose status is not `Shipped` a one-sentence reason in the evidence column, and every
`Deferred`/`Dropped`/`Withdrawn` row a follow-up: a GitHub issue number, the requirement that
supersedes it, or exactly `no follow-up recorded`.

**Sub-numbered clauses**: address each sub-clause (`FR-27.1`, `FR-27.2`, …) individually inside its
parent row's paraphrase and evidence — never a separate row for the clause. When sub-clauses have
different outcomes, the row takes the **least-shipped** status among them under the precedence
`Shipped` < `Shipped with deviation` < `Unverifiable` < `Deferred` < `Withdrawn` < `Dropped`, and the
evidence column names which sub-clause differs.

Follow the table with a `Shipped beyond the requirements` list — one bullet per piece of work found
in `tasks.md` that no numbered requirement covers — or, if none, the line `Nothing shipped outside
the numbered requirements.`

#### `## Blast radius`

Purely a rendering of ledger rows Steps 3 and 5 already produced — no new measurement or judgement
happens here. Render, in order: the `Measured from …` line (Step 5); the six-bucket file-count table
with `other` as the stated complement; the per-bucket net-lines counts; the public-API declaration
line count restricted to `src/`; the commit count. When Step 3 recorded the spec branch as not
determinable, replace all of the above with its FR-16 row 12 text — `Spec branch not determinable —
no diff measured.` followed by the three rules tried — and still score F1 Medium, per Step 3.

#### `## Where to look first`

Measured: the spec diff's file list (already produced by Step 5) — the candidate set this section
selects from. Judged: the 3–7 paths chosen, their order, and each one's reason.

When a spec diff was measured, list **3–7 paths** drawn from the spec diff's file list, most-
important first, each with a one-line reason of **≤ 25 words**. Every listed path must exist in the
spec diff — never a path invented from general repository knowledge. If the diff touches no files
under `src/`, draw the list from whatever it does touch and say so in one sentence before the list.
`Read` a candidate file only where its path is not self-explanatory (e.g. a test fixture whose name
does not indicate its purpose), and charge that read against the NFR-3 remainder from the 25-full-
read budget.

**FR-16 row 13** (no diff measured — spec branch not determinable, carried over from Step 3): the
section contains exactly `No diff measured — spec branch not determinable, so no files can be
ranked. Start from specs/{spec dir}/tasks.md and the ADRs listed in specs/{spec dir}/.adr-list.` —
no paths are listed, and FR-14's 3–7-path rule does not apply.

#### `## Inputs used`

Wholly measured — a projection of the ledger, with no judged content: one row per input, `used` or
`not available: {one-line reason}`, and nothing else. This section is excluded from NFR-2's word
budget (everything from this heading to end of file).

Emit one row for each of: `requirements.md`, `tasks.md`, `.adr-list` (**plus one further row per ADR
it names, identified by slug** — not folded into the `.adr-list` row), `.issue-number`,
`release_notes.md` section, git history, pull request, review comments, CI checks. Mark each `used`
when the ledger recorded a value from it, or `not available: {reason}` using the ledger's own
recorded reason verbatim (e.g. `not available: gh unavailable`, `not available: not present`) —
never a re-worded paraphrase.

**The git-tracking test, applied before any path is written anywhere in the file, not only here**:
`git ls-files --error-unmatch {path}`. A path that fails it is dropped or replaced — this is one test
that catches both the gitignored literal `PROMPT.md` and the merely-untracked `PROMPT-*.md`
companions (FR-17), since the rule is "not tracked in git", not "not gitignored". Every repository
path written anywhere in `show-me.md` is relative to the repository root (NFR-5); no token,
credential, absolute machine-local path, or untracked file's contents is ever written.

**`PROMPT.md`/`PROMPT-*.md` get no ledger row and no mention anywhere in the file** (FR-16 row 11,
FR-17) — their absence is normal and unremarkable, not a degradation to report. They may inform the
Synthesiser's own reading of the spec's background, but never appear as a citation, a link, or an
`Inputs used` row.

### Step 7 — Write

Owns: the single `Write` of `specs/{spec}/show-me.md` — the only file this command ever creates or
modifies.

### Step 8 — Budget self-check and session report

Owns: checking the written file's word count against its budget (revising and re-writing if
outside it) and printing the session report: path written, created-or-replaced, overall risk
level, and the advisory reminder.
