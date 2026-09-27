---
allowed-tools: Bash(ls:*), Bash(cat:*), Bash(date:*), Bash(head:*), Bash(tail:*), Bash(test:*), Bash(wc:*), Bash(grep:*), Bash(git diff:*), Bash(git log:*), Bash(git ls-files:*), Bash(dotnet run .claude/commands/spec/show_me_facts.cs -- specs/:*), Read, Write
description: Summarise a finished spec and give an advisory merge-risk read
argument-hint: [spec-id]
---

## Available specifications

!`ls -1d specs/*/ 2>/dev/null`

## Your task

Summarise the finished spec named by `$ARGUMENTS` (or the current spec, absent an argument) into
`specs/{spec}/show-me.md`: what changed and why, breaking changes, a requirement reconciliation, how
it was built, blast radius, an advisory Low/Medium/High merge-risk read, where to look first, and a
provenance table. This is not a review — it does not re-check correctness, security, TDD compliance,
CI status or PR review outcomes.

**This command is under active construction (spec 0037).** Steps 1–6 are implemented below.
`## How it was built` and `## Blast radius` are filled in; the other six of Step 5's H2 sections are
still bare headings, with nothing written under them — the tasks that fill each one land later and
will extend Step 5 in place, section by section. Follow each step exactly as written; every step ends
by saying what happens next, so do not improvise past what a step actually says.

### Step 1 — Resolve the target spec directory

Candidates are the directory names in the `## Available specifications` listing above — the last path
segment of each line, trailing slash removed. `specs/README.md`, `specs/dlq-review-findings.md` and
`specs/.current-spec` are not directory entries and never appear in that listing, so they are never
candidates.

Take `$ARGUMENTS` **as a whole** — the entirety of the argument text, leading and trailing whitespace
trimmed, any wrapping quotes removed, internal whitespace preserved verbatim. Do **not** split it on
whitespace: a real spec directory's name contains spaces
(`specs/0021-Expose Unacceptable Message Window/`).

**If `$ARGUMENTS` is non-empty after trimming**, match it against the candidates using this ordered
rule set, stopping at the first rule that yields exactly one match:

1. Exact directory-name match.
2. Exact four-digit id match.
3. Case-insensitive substring match on the directory name.

- If a rule yields **more than one** match, stop. Do not create or modify any file. Print exactly:
  `Ambiguous spec id '{arg}' — matches: {list of matching directory names}. Re-run with the full
  directory name.`
- If **no** rule yields any match, stop. Do not create or modify any file. Print exactly:
  `No spec matches '{arg}'. Run /spec:status to list specs.`
- Otherwise exactly one rule yields exactly one match: the target is that directory. Continue below.

**If `$ARGUMENTS` is empty** (no argument given), read `specs/.current-spec`.

- If the file is missing, is empty, is whitespace-only, or names a directory that is not one of the
  candidates above, stop. Do not create or modify any file. Print exactly:
  `No spec id given and no usable current spec (specs/.current-spec is {missing|empty|stale: names
  '{value}'}). Pass a spec id (/spec:show-me 0036-scoped-lifetime-per-pipeline) or run /spec:switch
  first.` — using `missing` when the file does not exist, `empty` when it exists but is empty or
  whitespace-only, and `stale: names '{value}'` (with `{value}` the file's trimmed content)
  otherwise.
- Otherwise the target is the directory it names. Continue below.

**On success**, the target is resolved. Continue to Step 2.

### Step 2 — Probe and invoke the measurement script, and map its exit status

The script is `.claude/commands/spec/show_me_facts.cs`. Every stop in this step (all but the
`unchecked`/`zero checkboxes`/`tasks.md absent` messages below) prints exactly:
`/spec:show-me could not run its measurement script ({path}): {state}. No show-me.md was written.
This is a tooling fault, not a fault in spec {dir} — re-run after restoring the script.` — with
`{path}` the script's path and `{state}` as named at each bullet — and creates or modifies no file.

- Run `test -f {path}`. If it fails, stop with `{state}` = `absent`.
- Run `test -r {path}`. If it fails, stop with `{state}` = `unreadable`.
- Otherwise invoke `dotnet run .claude/commands/spec/show_me_facts.cs -- specs/{dir}` — quoting
  `{dir}` after `specs/` whenever it contains a space, the only form the allow-list entry permits
  without a permission prompt. Never pass `--pinned` or `--release-notes`; those are the test
  script's inputs only, never the command's.
- The exit status, and nothing else — standard output is never parsed — decides what happens next:
  - **`0`** — measured successfully; the ledger was written. Print `Measured target: specs/{dir}/.`
    and stop there (Step 3, which reads the ledger, lands in a later task — do not improvise it, and
    do not create or modify `show-me.md` or any other file).
  - **`2`** — FR-3's precondition did not pass. Read the script's standard error and find the last
    line beginning `show-me-gate: `.
    - No such line, or it does not parse as one JSON object: stop with `{state}` = `gate facts were
      not parseable`.
    - Otherwise its `case` field selects exactly one message below, reading `unchecked`, `total` and
      `first_unchecked` from the same record for the third case. Print the selected message (not the
      tooling-fault template above) and stop. Do not create or modify any file.
      - `"tasks.md absent"` → `Spec {dir} has no tasks.md — /spec:show-me runs only against a
        finished spec. Current phase: run /spec:status.`
      - `"zero checkboxes"` → `Spec {dir}'s tasks.md contains no task checkboxes — nothing to
        summarise.`
      - `"unchecked"` → `Spec {dir} is not finished: {unchecked} of {total} tasks are still
        unchecked. First unfinished: {first_unchecked, one per line}. /spec:show-me runs only
        against a finished spec.`
  - **Any other status** — stop with `{state}` = `exited {code}`.

Every value above is copied verbatim from the script's own exit status and standard error — never
computed inline, and never used to write a partial or guessed file.

### Step 3 — Read the ledger, and hold its fields for later steps

Price `specs/{dir}/.show-me-ledger.json` with `wc -c` before opening it — pricing is not a read.
Then read it as **unplanned windows** of at most 25,000 B each, with `tail`/`head`, until the whole
file has been read. Parse the concatenated text once, as a single JSON object.

**If it does not parse as one JSON object**, stop. Do not create or modify any file — the ledger the
script wrote stays exactly as it is. Print exactly:
`/spec:show-me could not run its measurement script (.claude/commands/spec/show_me_facts.cs): ledger
was not a single JSON object. No show-me.md was written. This is a tooling fault, not a fault in
spec {dir} — re-run after restoring the script.`

**Otherwise**, hold every field of the parsed object for the steps below. Never recompute any of
them, and never parse anything from the script's own standard output — the ledger is the only
contract. Every field holding `null` has a matching entry in the ledger's `null_reasons` object; hold
that reason alongside the field too. It becomes one `## Inputs used` row (FR-15, one of FR-16's
absence rows) once Step 8 writes that section — nothing is written yet.

**Which section each field feeds** (the one table this command uses; no other step restates it):

| Section | From the ledger (copied, never recomputed) | From the command's reads (judged) |
| --- | --- | --- |
| Metadata block | branch ref, measured head, base ref, merge base, PR number and URL | the generation date (`date +%F`) and the issue (`.issue-number`) — nothing judged |
| `## What changed and why` | `.adr-list` resolution | the narrative; each ADR's title and Status; the diagram or fallback line |
| `## Breaking changes` | marked-section count and `{m}` | the item list, classifications, migrations, the count `{n}`, and the disagreement line |
| `## Did it ship what it said?` | the declared-id set and `{total}` | each id's status, the tallies `{k}`, and Parts 3–4's content |
| `## How it was built` | task total, per-tag counts, commit count, or the fallback line when the diff fields are null | nothing |
| `## Blast radius` | everything, including the provenance lines | nothing |
| `## Risk assessment (advisory)` | F1's `src/` count and level | the rest |
| `## Where to look first` | — | 3–7 paths, each with a reason |
| `## Inputs used` | `.adr-list` resolution, PR presence and its reason | one row per source read directly, marked from the read log |

**On success**, the ledger's fields are held. Continue to Step 4.

### Step 4 — Price and read the evidence, charged to one read log

Every read from here connects to **the read log**: a table kept only in the model's context for this
run, never written to disk — one row per read, holding its path, how it was read, and the bytes it
charged. Keep a running total against the **general allowance** of 948,576 B (NFR-3's 1,048,576 B
budget less its last 100,000 B, the **reserve**, which only a later step's source reads may spend).
Nothing below may push the running total over the general allowance; a read that would is not issued,
and later steps decide what that leaves unresolved. Never read `PROMPT.md` or a `PROMPT-*.md`
companion — it is not one of the reads below, on purpose.

**How every window is priced and read** (ADR 0072 KC3):

1. **A planned window** — one entry in a ledger window list (`tasks.windows`, `requirements.windows`,
   an entry's own `declarations[].windows`, `src_diff.windows`, one of an ADR's
   `adr_list[].extract[].windows`, or one of `release_notes.sections[].windows`) — is priced at that
   entry's own `bytes`. No extra `wc -c` call is needed for it; the ledger already measured it.
2. **Anything the ledger has not planned** — a whole file read in full (`tasks.md`, `requirements.md`,
   `.issue-number`, `.adr-list`) or a command's output (`git diff --name-only`, the commit-subject
   `git log`) — is priced first by piping it into `wc -c` (`wc -c {path}` for a file; `{command} | wc
   -c` for a command). `wc -c` is a size probe, not a read, and is charged nothing. It is then read in
   windows of at most 25,000 B each, with `tail -n +{first} {path} | head -n {count}` for a file or
   the same command piped into `tail | head`. `.issue-number` and `.adr-list` are always small enough
   to read whole in one call, with `cat {path}`, instead.
3. **An oversize window** (a ledger window flagged `"oversize": true`, or an unplanned read whose
   single remaining line is still over 25,000 B) is not read at all; a value that needed it is
   unresolved, for a later step to report.
4. **A truncated tool output** counts as not read, but is still charged the bytes it brought into
   context.

Every window, whichever rule priced it, is read with `tail -n +{first_line} {path} | head -n {count}`,
where `{count}` is `{last_line} - {first_line} + 1` — except the `src/`-scoped diff, `git diff
--name-only` and the commit-subject `git log`, each of which is read by piping its own command into
the same `tail | head` rather than naming a file path, and `.issue-number`/`.adr-list`, read whole with
`cat`. None of the four is ever re-issued as a fresh, unscoped command.

**Step 4's reads, in KC3's order:**

- **The existing `show-me.md`.** Run `test -f specs/{dir}/show-me.md`. When it exists, price it at
  `wc -c specs/{dir}/show-me.md` plus 8 bytes for every line `wc -l specs/{dir}/show-me.md` counts and
  for one line more, then `Read` it — the only use of the `Read` tool, and only because `Write`
  refuses to replace a file the session has not read. Log it at that price. Never treat its content as
  evidence for any section.
- **`.issue-number`.** Run `test -f specs/{dir}/.issue-number`. When it exists, price it with
  `wc -c specs/{dir}/.issue-number`, then read it whole with `cat specs/{dir}/.issue-number`, and log
  it. When it is missing, empty, or whitespace-only, issue no read.
- **`.adr-list`.** Run `test -f specs/{dir}/.adr-list`. When it exists and its content is non-empty
  after trimming, price it with `wc -c specs/{dir}/.adr-list`, then read it whole with
  `cat specs/{dir}/.adr-list`, and log it. When it is missing or empty, issue no read. Either way,
  `.adr-list`'s *resolution* — which entries resolved, to which files, and each entry's extract — comes
  only from the ledger's `adr_list` (Step 3); this read is never used to recompute it.
- **`tasks.md`.** Price it with `wc -c specs/{dir}/tasks.md`, then read every window in the ledger's
  `tasks.windows`, in order, for as long as the general allowance covers the next one.
- **The `src/`-scoped diff.** When `src_diff` is not null, read every window in `src_diff.windows`, in
  order, piping `src_diff.command` into `tail | head` as above, for as long as the general allowance
  covers the next one.
- **`git diff --name-only`, over the same pair.** Only when `buckets.src.files` is `0` while
  `merge_base` and `measured_head` are both not null (FR-14: a diff was measured but touches nothing
  under `src/`): price `git diff {merge_base}..{measured_head} --name-only` by piping it into `wc -c`,
  then read it the same way, and log it. Otherwise issue no read.
- **Each `.adr-list` entry's extract.** For every entry in `adr_list` whose `extract` is not null, read
  its three parts — `front_matter`, `status`, `consequences` — each by its own `windows`, against that
  entry's own `path`, for as long as the general allowance covers the next one.
- **Marked release-notes section(s).** When `release_notes.count` is `0`, issue no read — there is
  nothing to read, and FR-16 row 5 is a later step's concern. Otherwise price the file with
  `wc -c {release_notes.path}`, then check whether the general allowance remaining covers the sum of
  every entry's `bytes` in `release_notes.sections`. When it does, read every section, in order — each
  entry's own `windows`, against `release_notes.path` — and log them: FR-7 makes this an obligation,
  every marked section for the target or none. When it does not, issue no read at all and hold that
  FR-16 row 5a applies (a present section was not read because the read budget was exhausted), for a
  later step to report — never read some sections and skip others.
- **Commit subjects.** Only when `adr_list` is empty while `merge_base` and `measured_head` are both
  not null (FR-16 row 6, with a diff measured): price
  `git log --format='%h %s' {merge_base}..{measured_head}` by piping it into `wc -c`, then read it the
  same way, and log it. Otherwise issue no read.
- **`requirements.md`.** When `requirements.present` is `true`, price it with
  `wc -c specs/{dir}/requirements.md`. If its `bytes` fits inside what the general allowance has left,
  read it whole: every window in `requirements.windows`, in order — a read *in full*, not a
  degradation. Otherwise read it by declaration instead: every entry in `declarations`, in the ledger's
  order, each by its own `windows`, for as long as the general allowance covers the next one. Any
  declaration whose window cannot be afforded is not read; a status that needed it is `Unverifiable`,
  with that reason, for a later step to report — never zero.

Hold everything read above, and the read log itself, for later steps. The Explainer's own reads
(source files for a diagram) land with a later task's extension of Step 5. Continue to Step 5.

### Step 5 — Assemble `show-me.md`

Build the file's full text in memory — never on disk — in this order. Later tasks extend this step in
place, filling in each H2 section's body; nothing below writes to disk yet.

1. The H1: `# Show me — {dir}` (the bare spec directory name, not the `specs/` prefix).
2. The metadata block, one bullet per line, blank line after the H1 and after the block. Every value
   is copied verbatim from the ledger (Step 3) or computed exactly as named — nothing here is judged:
   - `- **Generated:** {date +%F}`
   - `- **Spec:** specs/{dir}/`
   - `- **Issue:** {the content Step 4 read from .issue-number, trimmed; or, when Step 4 issued no
     read for it, none}`
   - `- **Spec branch:** {spec_branch.ref, when spec_branch is not null; otherwise undetermined}`
   - `- **Measured head:** {the first 9 characters of measured_head.sha, when measured_head is not
     null; otherwise undetermined}`
   - `- **Base ref:** {base.ref}`
   - `- **Merge base:** {the first 9 characters of merge_base, when merge_base is not null; otherwise
     undetermined}`
   - `- **PR:** {#pr.number (pr.url), when pr is not null; otherwise none found}`
3. The eight H2 headings, verbatim and in this order, one blank line between each. `## How it was
   built` and `## Blast radius` are filled per the rules below; the other six are still bare, with
   nothing written under them yet:
   `## What changed and why`, `## Breaking changes`, `## Did it ship what it said?`,
   `## How it was built`, `## Blast radius`, `## Risk assessment (advisory)`,
   `## Where to look first`, `## Inputs used`.

**The Explainer** (ADR 0077, FR-6) runs next, after the headings are written and before any H2
section's content — it decides `## What changed and why`'s diagram-or-line output, which a later
task places. Walk this six-row ladder, stated in ADR 0077's own order; the first row that applies
decides the outcome:

| # | Situation | The Explainer | `## What changed and why` carries |
| --- | --- | --- | --- |
| 1 | The ledger's trigger fields are null, because no spec diff was measured | evaluates nothing, reads nothing | the no-diff line |
| 2 | No test fired, and the Explainer does not raise | reads nothing | the no-trigger line |
| 3 | A test fired, but the evidence does not cohere into one relationship — seen before reading, or found on reading | stands down; reads already made stay charged | the stand-down line |
| 4 | A relationship is elected — because a test fired, or as a raise with its one-sentence reason — and cannot be completed: a file it needs cannot be afforded, or, for a raise, reading shows no relationship after all | abandons the relationship; reads already made stay charged | after a fired test, the budget line; after a raise, the no-trigger line |
| 5 | A test fired, and the only relationship worth drawing is the path tree among three to seven changed files a reviewer should open first | reads, renders the tree, targets `## Where to look first` | the placed-elsewhere line |
| 6 | Any other elected relationship that could be afforded, including every raise | reads, renders the block | the diagram, plus the raise's reason when it was a raise |

The five named lines. Every reference to one of these — in this file, in a commit message, anywhere —
uses its name, never its row number or its position in this list:

- **the no-trigger line**: `No diagram: {a} files changed under src/ across {b} director{y|ies}, {c}
  changed public API declaration lines, {d} ADRs — no structural relationship to draw.`
- **the stand-down line**: `No diagram: {which test(s)} fired, but {one-sentence reason the evidence
  does not cohere into a relationship}.`
- **the placed-elsewhere line**: `No diagram here: the change's shape is drawn as a path tree in ##
  Where to look first.`
- **the budget line**: `No diagram: the read budget was exhausted before the relationship could be
  read accurately.`
- **the no-diff line**: `No diagram: spec branch not determinable, so no change could be drawn.`

**The node-list row shape.** Before rendering a diagram, the Explainer holds one row per node it
draws, naming the node and exactly one licensing source:

| Source | Licenses a node that names |
| --- | --- |
| a path in the spec diff | that file |
| a declaration line in the `src/`-scoped diff | the type or member that line declares |
| an ADR stem from `.adr-list` | a component or type the ADR's extract names |
| a path in the read log, read by the Explainer | that file — marked `(unchanged)` when it is not in the spec diff — or a type or member read in it |

A node with no row cannot be drawn.

**Walking the ladder:**

- When `triggers` is `null` (row 1, FR-16 row 12): hold **the no-diff line** as the Explainer's
  output, and stop. `triggers.d1`, `.d2` and `.d3` are not evaluated at all.
- Otherwise, read `triggers.d1`, `triggers.d2` and `triggers.d3` — already computed in the ledger,
  never re-evaluated.
  - When all three are `false` and the model does not exercise FR-6 (b)'s raise (row 2, non-raise
    half): hold **the no-trigger line** as the Explainer's output, with `{a}` = `buckets.src.files`,
    `{b}` = `src_subdirectory_count` (`directory` when `{b}` is `1`, otherwise `directories`), `{c}` =
    `public_api_lines`, `{d}` = `adr_resolved_count` — every value copied verbatim from the ledger.
    Stop; no reads are issued.
  - Otherwise — a test fired (at least one of `d1`/`d2`/`d3` is `true`), or all three are `false` but
    the model exercises FR-6 (b)'s raise, naming one explicit one-sentence reason for the relationship
    the prose cannot carry — elect one relationship to draw and continue to *Participants and
    reading* below.

**Participants and reading** (ADR 0077 KC2). A participant is a file the elected relationship needs: a
changed file in the spec diff, a file an ADR extract names, or a file a participant already read
names. Every participant passes `git ls-files --error-unmatch` before it is read — an untracked path
can never become a node. Spend in this order:

1. **Probe the known set first.** Before any read, run `wc -c` on every participant already named,
   and plan which to read whole and which to extract. Probing is free and never by itself abandons the
   relationship.
2. **Probe each newly found file before reading it.** A participant discovered while reading — a
   caller named in a file already read — is probed the same way before it is opened.
3. **Read whole, extract, or abandon.** The bytes remaining are whatever the general allowance has
   left, plus the 100,000 B reserve — only these reads may spend the reserve, and when a run draws two
   diagrams they share the same one reserve (AC-63). A file that fits is read whole. A file that does
   not fit is read instead by a sized `grep -n -F` for the literal member names the relationship needs
   (never a pattern) — itself an unplanned window, priced and charged before the lines it finds are
   read as windows — recorded as `used (targeted extraction)`. If not even the extract fits, the
   relationship cannot be completed (row 4, below).

Every read here is an unplanned window (sized with `wc -c`, at most 25,000 B, halved until it fits),
charged to the same read log Step 4 uses.

**Deciding among rows 3–6, before reading or once it is under way:**

- **Row 3, stand-down.** A test fired, but the evidence does not cohere into one drawable
  relationship — seen before any read (e.g. the fired values describe unrelated, incidental changes
  with no shared call path, flow, hierarchy or component relationship — D2 firing on ten added
  properties across ten otherwise-unconnected DTOs is FR-6 (b)'s own example), or found while reading.
  Hold **the stand-down line**, naming which test(s) fired and one explicit sentence for why the
  evidence does not cohere. Reads already made stay charged; nothing already read is discarded from
  the log, and nothing is rendered — never an invented relationship.
- **Row 4, abandon.** The elected relationship — from a fired test, or from the raise — cannot be
  completed: a file it needs cannot be afforded even as an extract, or, for a raise, reading shows no
  relationship after all. Abandon: reads already made stay charged. Hold **the budget line** when the
  relationship was elected from a fired test, or **the no-trigger line** when it was elected as a
  raise — a raise that does not complete counts as not exercised, which is exactly the no-trigger
  line's condition (no test fired, no raise exercised).
- **Row 5, the path tree.** A test fired, and the only relationship worth drawing is the
  reviewer's-starting-files path tree among three to seven changed files (FR-14). Read, render the
  tree — an ASCII tree in a plain fenced block — and target it at `## Where to look first` instead of
  `## What changed and why`; hand over the tree's three to seven changed paths for FR-14's list, each
  marked `(unchanged)` in the tree when it is not itself in the spec diff. Hold **the placed-elsewhere
  line** for `## What changed and why`.
- **Row 6, draw the block.** Any other elected relationship that can be afforded, including every
  raise that finds one. Read, render the block, and target it at `## What changed and why`:
  - a **sequence, call flow, or state/lifecycle** → a Mermaid fenced block;
  - a **file, type or namespace hierarchy** → an ASCII tree in a plain fenced block;
  - a **component or box-and-arrow sketch** → either, at the model's discretion (judgement, NFR-1).

  When row 6 applies, the Explainer may also elect a second diagram for `## Where to look first` — a
  tree or sketch of how three to seven changed paths relate, by containment or by which file calls
  which — spending from the same remainder as the first (one reserve for the whole run, AC-63) and
  handing over those paths for FR-14's list; not drawing it needs no line. After any other row,
  `## Where to look first` carries no second diagram. After a raise that draws (row 6), hold the
  raise's one-sentence reason alongside the diagram.

**Rendering.** Every fenced block follows `.agent_instructions/documentation.md`'s Mermaid trap list:
no `;` inside a `sequenceDiagram`, no `<` or `>` in a label, no HTML entities, and quoted labels where
a label carries a comma, colon or parenthesis. At most 40 lines including the opening and closing
fences, at most 100 characters per line (FR-6 (d)) — a relationship that will not fit is drawn at too
fine a grain: cut detail, or draw the narrower relationship, rather than stretching past the cap.

Before rendering, hold the node list — one row per drawn node, per *The node-list row shape* above. A
node with no row is not rendered.

**What this task holds, for a later task to place:** for `## What changed and why`, at most one
rendered block or exactly one named line, never both; for `## Where to look first`, at most one
rendered block (row 5's tree, or row 6's optional second diagram) together with its three to seven
changed paths. Nothing is written under either heading yet.

**The Classifier** (ADR 0073, FR-7, FR-8) judges each breaking-change item, each requirement's status,
and each piece of work no requirement covers. Four rules bind it:

- **It reads only what the read log holds.** No read of its own; its inputs are the src/-scoped diff,
  the .adr-list extracts, the marked release-notes section (when Step 4 read one), requirements.md
  or its declarations, and tasks.md — all already charged there.
- **It judges each thing once.** An item list, a status set, and Part 3's list are each produced once
  per run; every later rendering (the sections a later task places, and F2/F5 below) renders that one
  result, never re-derives it.
- **It follows the catalogue's boundary when there is one** — see *Breaking-change items* below.
- **Every item and status carries its evidence** — a catalogue bullet, ADR entry or diff hunk for an
  item; a task id, path or ADR stem for a status.

**Breaking-change items** (FR-7): one bullet each, ≤ 40 words — what breaks, its classification set
(one or more of source/binary/behavioural/compatibility — "source and binary" is normal, not
exclusive), and the migration in one sentence.

- **The tie-break.** A marked release-notes section for the target was read (Step 4) and its
  `#### Breaking changes` list holds a bullet (`release_notes.m` > 0) → each bullet is one item,
  following the catalogue's own grouping, never re-partitioned. Otherwise → one item per distinct
  public-API declaration **removed or modified** in the src/-scoped diff (a purely added declaration
  is additive, not breaking), or per ADR *Consequences* bullet describing a **consumer-affecting**
  behavioural break.
- **Count line.** `Total breaking-change items: {n}`. `{n}` is 0 → the list is exactly `No breaking
  changes identified for this spec.` in its place.
- **Disagreement.** The catalogue was read (`release_notes.m` not null) and `{n}` differs from it →
  hold, additionally: `release_notes.md records {m} items; this summary identifies {n}` —
  `{m}` = `release_notes.m`, copied verbatim, never recomputed.

**Each declared id's status** (FR-8) — one status per id in `declared_ids` (the ledger's set; no other
id is ever assigned one):

- **Sub-clauses fold already** (`declared_ids` is already folded to top-level ids). When a folded id's
  sub-clauses have different outcomes, its status is the *least-shipped* by this precedence: `Shipped`
  < `Shipped with deviation` < `Unverifiable` < `Deferred` < `Withdrawn` < `Dropped`; when that status
  is not `Shipped`, its deviation entry names which sub-clause differs and addresses each sub-clause.
- **Status set**: `Shipped`, `Shipped with deviation`, `Deferred`, `Dropped`, `Withdrawn`,
  `Unverifiable`. `Withdrawn` needs a recorded decision — cited — that removed or superseded the
  requirement before or during implementation; `Dropped` is the same outcome with no such decision.
- **Evidence and follow-up.** Every status names a task id, path or ADR stem. `Deferred`, `Dropped`
  and `Withdrawn` also name a follow-up: an issue number, a superseding requirement's id, or the
  literal `no follow-up recorded`.
- **Part 1.** `Shipped as planned: {k} of {total} numbered requirements — {id list}.` `{id list}`:
  every `Shipped` id, `FR-…` then `NFR-…`, comma-separated; a maximal run of **three or more**
  integer-adjacent ids (`{prefix}-i`, `{prefix}-j`, `j = i + 1` exactly) collapses to `{first}–{last}`
  — a run of two is written out, never collapsed. `{k}` = 0 → the id list is `none`.
- **Part 2.** One deviation entry per non-`Shipped` id, same id order: the id, a one-line paraphrase
  of what it asked for, its status word in bold, a one-sentence reason, its evidence, its follow-up.
  None → exactly `No deviations: every numbered requirement shipped as stated.`
- **The partition invariant.** Every id in `declared_ids` appears exactly once, in Part 1's list or as
  one Part 2 entry — never both, never neither.
- **Part 3.** Judged, not pattern-matched (NFR-1): from `tasks.md`'s checked tasks, which ones
  describe work no declared id's evidence already covers — using each task's own description,
  whatever form it cites its traces in (a `Traces to:` line, an `AC-N` reference, or none at all) —
  held by task id. None → exactly `Nothing shipped outside the numbered requirements.`
- **Part 4.** `Shipped: {a} · Shipped with deviation: {b} · Deferred: {c} · Dropped: {d} ·
  Withdrawn: {w} · Unverifiable: {e} (of {total})` — `{a}` = Part 1's `{k}`; `{b}+{c}+{d}+{w}+{e}` =
  the number of Part 2 entries; the six terms sum to `{total}`.

**Tallies this stage emits** — the only numbers the Classifier produces; every other number in
`show-me.md` comes from the ledger: `{n}` and the six Part 4 terms. F2 (below) reads `{n}`; F5 (below)
reads the Part 2 entries.

**What this task holds, for a later task to place:** the item list for `## Breaking changes`, the
status list and Parts 1–4 for `## Did it ship what it said?`. Nothing is written under either heading
yet.

<!-- show-me:risk-step:begin -->
**The risk step** (ADR 0073, FR-11–FR-13) computes the three factor levels and the overall level, and
is the only step that may test a level against a threshold. Everything up to and including FR-13's
sentence is decided and written here; only the rest of the rationale (2–5 sentences in all, naming
the factor(s) that set the level) is the Synthesiser's job, after this step ends.

**The forced levels** (FR-16 rows 8, 9 and 12), applied before any threshold:

| FR-16 row | Condition | Forced level | Measured-value cell |
| --- | --- | --- | --- |
| 8 | `requirements.present` is `false` | F5 = `Medium` | `no requirements.md — reconciliation not possible` |
| 9 | `declared_total` is `0` | F5 = `Medium` | `0 declared ids in the bold lead-in form` |
| 12 | `f1_level` is `null` (spec branch not determinable) | F1 = `Medium` | `no diff measured — spec branch not determinable` |

**The mapping procedure**, one procedure for all three factors — F1 only ever reaches step 1:

1. **Forced level.** A row above applies to this factor → take its level, evaluate nothing else.
2. **High.** Otherwise, the factor's `High` condition (FR-11) holds over its whole body of evidence →
   `High`.
3. **Medium.** Otherwise, its `Medium` condition holds over its whole body of evidence → `Medium`.
4. **Low.** Otherwise → `Low`.

**F1** (files changed under `src/`): when `f1_level` is not `null`, its value is
`{buckets.src.files} files under src/` and its level is `f1_level`, copied verbatim — no threshold is
applied here, the Measurer already did. Otherwise (row 12) its value is
`no diff measured — spec branch not determinable` and its level is `Medium`.

**F2** (breaking-change items) **and F5** (requirement fidelity): row 8 or row 9 forces F5 to
`Medium` (F2 has no forced row — count `1`, `requirements.present`, `declared_total` from the ledger
first, since either row's condition can hold even when a diff was measured). Outside a forced case,
apply steps 2–4 to the Classifier's own evidence: F2's value is `{n} items` against FR-11's F2
thresholds (0 → `Low`, 1–3 → `Medium`, ≥ 4 → `High`); F5's value is `{count} deviation entries of
{total} requirements: {one clause per status present}` (e.g. `1 Shipped with deviation, 1 Withdrawn
with a superseding requirement`) against FR-11's F5 thresholds (no entries → `Low`; ≥ 1 `Shipped with
deviation`/`Unverifiable` entry, or ≥ 1 `Deferred`/`Dropped`/`Withdrawn` entry stating a follow-up →
`Medium`; ≥ 1 `Deferred`/`Dropped`/`Withdrawn` entry stating `no follow-up recorded` → `High`) —
taking the highest matching column when more than one holds collectively.

**The factor table**, exactly three rows, F1, F2 and F5 in that order — never a row named `F3` or
`F4`:

| Factor | Measured value | Level |
| --- | --- | --- |
| F1 | {F1's value, above} | {F1's level, above} |
| F2 | {F2's value, once available} | {F2's level, once available} |
| F5 | {F5's value, above or once available} | {F5's level, above or once available} |

**The overall level**, over `Low` < `Medium` < `High`: the maximum of the three factor rows above.
The stated level may be **higher** than that maximum, but only paired with one explicit
first-sentence reason naming what the three factors miss — this is the raise (FR-12). The stated
level may **never** be lower than the maximum.

Write, on its own line, in the exact form `**Overall risk: {Low|Medium|High}**` — the stated level
substituted for the placeholder: the maximum, or the raised level.

**Hold, for the Synthesiser:** the factor or factors whose level equals the maximum — a list, not a
line; nothing here writes it out. The Synthesiser names them in the rest of the rationale without
re-deriving the maximum itself.

**FR-13's sentence**, verbatim, a literal — never composed at run time:
`This assessment is advisory only. It is not a merge gate; the merge decision stays with a human
reviewer.`

**Before this step ends**, still inside these markers, check what has just been decided: the stated
level is not below the maximum (AC-23); and, when the stated level is above the maximum, a raising
sentence — naming what the factors miss — was actually produced, not merely claimed (AC-23). Both
checks run over what this step itself computed; nothing outside these markers ever tests a level.
<!-- show-me:risk-step:end -->

**The Synthesiser** writes everything else — including `## How it was built` and `## Blast radius`
below, whose rules are already implemented, and every other H2 section a later task fills — copying
every number from the ledger or from the Classifier's tallies, never computing one itself.

**`## What changed and why`** (FR-6) states, in 150–600 words of prose (fenced diagram lines
excluded, NFR-2 (d)) — never a bullet dump, never a copy-paste of an ADR's *Decision* section — what
the spec set out to fix, what a user of Brighter can now do differently (or what now behaves
differently), and the one or two decisions that most shaped the result. An internal type name is
never introduced without a short gloss on first use.

**Naming the ADRs.** Every entry in the ledger's `adr_list` is named at least once:

- An entry whose `path` resolved: by its filename stem, its title (from the extract's
  `front_matter`) and its current Status (from the extract's `## Status` section), linked relatively
  from the spec directory to the file — never by a bare number (C-9), e.g. `[{title}
  ({stem})](../../docs/adr/{stem}.md)`.
- An entry whose `path` did not resolve (row 7, FR-16): named using its own `reason` — `{entry} —
  ADR file not found in docs/adr/.`, or, when `matches` holds more than one filename, `{entry} —
  ambiguous ADR number, matches: {filenames}; .adr-list should name the full filename instead.` — the
  narrative continues with the remaining, resolved ADRs.
- When `adr_list` is empty (row 6, FR-16): write exactly `No ADRs recorded for this spec.` in place
  of any ADR naming, and synthesise the narrative from `requirements.md`, `tasks.md` and the commits
  instead.

**Placing the Explainer's output** (ADR 0077 IA 3, first part). Place exactly what the Explainer
held earlier in this step: its rendered block, when row 6 of its ladder applied (with the raise's
one-sentence reason immediately before the block, when it was a raise), or exactly one of the five
named lines, when any other row applied — never both, never neither.

**`## Breaking changes`** (FR-7) places the Classifier's bullets (or, when `{n}` is `0`, exactly
`No breaking changes identified for this spec.` in their place), then whichever applies below, then
the count line last:

- Exactly one absence line, checked in this order — never more than one:
  1. `spec_branch` is `null` (row 14, FR-16 — no diff was measured): `No diff measured — this list
     is derived from the ADRs and requirements.md only; public-API declaration lines could not be
     inspected.`
  2. Otherwise, `release_notes.count` is `0` (row 5, FR-16 — no marked section exists): `No
     release_notes.md section found for this spec; this list is derived from the ADRs and the
     diff.`
  3. Otherwise, Step 4 held that FR-16 row 5a applies (a marked section exists but the read budget
     was exhausted before it could be read): `A release_notes.md section exists for this spec but
     was not read: the read budget was exhausted. This list is derived from the ADRs and the diff.`
  4. Otherwise, no absence line.
- The disagreement line, when `release_notes.m` is not `null` and differs from `{n}`:
  `release_notes.md records {m} items; this summary identifies {n}` — `{m}` copied verbatim from
  `release_notes.m`.
- `Total breaking-change items: {n}` — always last, copied verbatim from the Classifier's tally.

**`## Did it ship what it said?`** (FR-8) checks FR-16 rows 8 and 9 first, in this order, and writes
one of their two lines verbatim in place of everything below when either applies; otherwise it
places the Classifier's judgements as Parts 1–4, in order:

- Row 8 (`requirements.present` is `false`): write exactly `No requirements.md found for this spec —
  scope reconciliation is not possible.`
- Otherwise, row 9 (`declared_total` is `0`): write exactly `requirements.md declares no numbered
  requirements in the bold lead-in form (**FR-n — …**) — nothing to reconcile.` No shipped-as-planned
  line, no deviation list, no count line follows.
- Otherwise, Parts 1–4:

  **Part 1 — the shipped-as-planned line.** `Shipped as planned: {k} of {total} numbered
  requirements — {id list}.` `{id list}` names every id the Classifier judged `Shipped`, `FR-…` then
  `NFR-…`, comma-separated, collapsed by FR-8's two rules: two ids are consecutive only when
  integer-adjacent (`{prefix}-i`, `{prefix}-j`, `j = i + 1` exactly — an undeclared id's absence
  never bridges a gap), and a maximal consecutive run collapses to `{first}–{last}` only when it
  spans three or more ids; a run of exactly two is written out, comma-separated, never collapsed.
  `{k}` is `0` → `{id list}` is the single word `none`.

  **Part 2 — the deviations.** One bullet per id the Classifier judged not `Shipped`, in the same id
  order Part 1 uses: the id; a one-line paraphrase of what it asked for; the status word in bold; a
  one-sentence reason; the evidence the Classifier cited; and, for `Deferred`, `Dropped` or
  `Withdrawn`, the follow-up the Classifier cited — a follow-up issue number, the id of a superseding
  requirement, or the literal `no follow-up recorded`. No such ids → exactly `No deviations: every
  numbered requirement shipped as stated.`

  **The partition invariant.** Every declared id appears exactly once, across Part 1's list and Part
  2's entries together — never both, never neither. This step renders the Classifier's own
  partition; it does not recompute it.

  **Part 3 — `Shipped beyond the requirements`.** The Classifier's list of task ids for work no
  declared id's evidence covers, at most 5 entries, in task-id order; when more than 5 qualify, list
  the first 5 and close with one further line, `… and {r} more task(s) not shown.`, `{r}` the
  remaining count. None → exactly `Nothing shipped outside the numbered requirements.`

  **Part 4 — the count line, always last.** `Shipped: {a} · Shipped with deviation: {b} · Deferred:
  {c} · Dropped: {d} · Withdrawn: {w} · Unverifiable: {e} (of {total})`, every term copied verbatim
  from the Classifier's tally.

**`## How it was built`** (FR-9) states the task shape and the commit shape, and nothing else — no
review history, no CI state. Both figures are copied verbatim from the ledger's `tasks` and `commits`
fields — nothing here is judged:

- `- **Tasks:** {tasks.total} total — {tasks.by_tag["TEST + IMPLEMENT"]} \`TEST + IMPLEMENT\`,
  {tasks.by_tag.STRUCTURAL} \`STRUCTURAL\`, {tasks.by_tag.PROJECT} \`PROJECT\`, {tasks.by_tag.DOC}
  \`DOC\`, {tasks.by_tag.untagged} untagged`
- When `commits` is not null: `- **Commits:** {commits} since the merge base`
- When `commits` is null (FR-16 row 12, spec branch not determinable): `Commits: not determinable —
  spec branch not resolved.` verbatim, in place of the commits bullet above.

**`## Blast radius`** (FR-10, FR-20) states every measured number and the exact refs measured
against, copied verbatim from the ledger — nothing here is judged. Every sha in this section is the
ledger's **full** sha, never truncated to the metadata block's 9-character form: this section exists
so two runs that disagree can be diagnosed (FR-10), which needs the exact value.

**When `spec_branch` is `null`** (FR-16 row 12 — the branch is not determinable), write exactly:

- `Spec branch not determinable — no diff measured.`
- a blank line, then `Rules tried:` followed by one bullet per entry of `rules_tried`, verbatim and
  in order.

Nothing else in this section: no bucket table, no bullets below, no zero figures.

**Otherwise** (a diff was measured), in this order:

1. A pipe table, one row per bucket plus a totals row, in `buckets`' own key order (`src`, `tests`,
   `docs`, `specs`, `github` — labelled `.github/` — `other`), then `total`. Excluded from NFR-2's
   word budget, per FR-10:

   | Bucket | Files changed | Net lines |
   | --- | --- | --- |
   | `src/` | {buckets.src.files} | +{buckets.src.added}/−{buckets.src.removed} |
   | `tests/` | {buckets.tests.files} | +{buckets.tests.added}/−{buckets.tests.removed} |
   | `docs/` | {buckets.docs.files} | +{buckets.docs.added}/−{buckets.docs.removed} |
   | `specs/` | {buckets.specs.files} | +{buckets.specs.added}/−{buckets.specs.removed} |
   | `.github/` | {buckets.github.files} | +{buckets.github.added}/−{buckets.github.removed} |
   | `other` | {buckets.other.files} | +{buckets.other.added}/−{buckets.other.removed} |
   | **Total** | {buckets.total.files} | +{buckets.total.added}/−{buckets.total.removed} |

2. `- **Public API declaration lines changed:** {public_api_lines}`
3. `- **Distinct \`src/\` subdirectories touched:** {src_subdirectory_count}`
4. When `pr_count` is not `null` and greater than `1` (FR-20): `- {pr_count} pull requests found for
   branch {branch}; using #{pr.number} (highest number).` — `{branch}` is `spec_branch.ref` with any
   leading `refs/remotes/origin/` or `refs/heads/` prefix removed (FR-20's own stripping rule; used
   as-is when the resolved ref is the literal `HEAD`).
5. When `measured_head.source` is `"pr_head"`: `- Measured from git diff
   {merge_base}..{measured_head.sha} (PR #{pr.number} head)`. Otherwise (`measured_head.source` is
   `"branch_tip"`): `- Measured from git diff {merge_base}..{measured_head.sha} (spec branch tip)`.
6. When `measured_head.source` is `"pr_head"` and `pr.head_sha` is not equal to `spec_branch.sha`:
   `- Spec branch tip {spec_branch.sha} differs from PR #{pr.number} head {pr.head_sha}; measured the
   PR head.`
7. When `pr` is not `null` and `pr.head_present` is `false` (FR-16 row 16): `- PR #{pr.number} head
   {pr.head_sha} is not present locally; measured the spec branch tip {measured_head.sha} instead.
   Fetch the branch and re-run to measure the PR head.` (Mutually exclusive with line 6 —
   `head_present` is `true` there, `false` here.)
8. `- Ref used: {spec_branch.ref} at {spec_branch.sha}; base ref {base.ref} at {base.sha}; merge base
   {merge_base}.`
9. When `local_divergence` is not `null`: `- Local branch {local_divergence.name} is at
   {local_divergence.sha} and differs from the measured ref.`

**`## Where to look first`** (FR-14) states the files a reviewer should open first, or, when no diff
was measured, row 13's fallback line in their place.

- **When `spec_branch` is `null`** (row 13, FR-16 row 12): write exactly `No diff measured — spec
  branch not determinable, so no files can be ranked. Start from specs/{dir}/tasks.md and the ADRs
  listed in specs/{dir}/.adr-list.` No paths, no diagram; FR-14's path-count rule does not apply.
- **Otherwise, the path source**: the `src/`-scoped diff's paths, when `buckets.src.files` is not
  `0`; otherwise the paths `git diff --name-only` over `{merge_base}..{measured_head}` found (Step
  4's own read, issued for exactly this case).
- **The Explainer's handover.** When the Explainer drew a diagram targeting this section (row 5 of
  its ladder, or row 6's optional second diagram), place that diagram here and use its three to seven
  handed-over paths as the path list below — never re-derive a separate set; each node the diagram
  carries that is not itself in the spec diff stays marked `(unchanged)`, per the Explainer's own
  rule. After any other row, this section carries no diagram.
- **The path list.** Three to seven paths from the path source, most-important first (judgement,
  NFR-1, when the source holds more than seven), each with a reason of at most 25 words; every listed
  path exists in the path source. When the path source holds fewer than three paths, list every one
  of them, each with its reason — a list shorter than three is then complete, not a shortfall
  (FR-14). A diagram drawn here consumes no path slot.

**`## Inputs used`** (FR-15) is a two-column table, `| Input | Status |`, in exactly this row order —
never a row beyond this set:

1. `requirements.md` — `used` when `requirements.present` is `true` (a whole read or a
   by-declaration read both count as `used`; a declaration a status could not afford is
   `Unverifiable` there, not here); otherwise `not available: not present`.
2. `tasks.md` — always `used` (FR-3's gate already required it).
3. `.adr-list` — `used` when `adr_list` holds at least one entry; otherwise `not available: .adr-list
   missing or empty`.
4. **One row per `adr_list` entry**, identified by its slug (a resolved entry's filename stem) or, for
   an unresolved entry, its own `.adr-list` text (row 7, FR-16): `used` when its extract was actually
   read; `not available: {its own `reason`}` (`ADR file not found` or `ambiguous ADR number`) when it
   did not resolve; `not available: read budget exhausted before {entry}'s extract could be read` on
   the unexercised case where it resolved but the general allowance ran out before its extract's turn.
5. `.issue-number` — `used` when Step 4 issued its read; otherwise `not available: not present` (row
   10, FR-16).
6. `release_notes.md` section — `used` when Step 4 read it (T10.2's obligation: every marked section
   or none); `not available: read budget exhausted before release_notes.md section could be read`
   when row 5a applied; otherwise (no marked section exists at all, row 5) `not available: no marked
   release_notes.md section for this spec`.
7. `git history` — `used` when a spec diff was measured (`merge_base` and `measured_head` both not
   `null`); otherwise `not available: spec branch not determinable` (row 12, FR-16; ADR 0072 KC5).
8. `pull request` — `used` when `pr` is not `null` (including row 16, FR-16, where its head commit
   is not present locally — the PR was still found); otherwise `not available: {null_reasons.pr}`
   (rows 1 and 2, FR-16 — already the exact reason text, copied verbatim).
9. **One row per source file the Explainer read** (ADR 0077 IA 4), identified by its path: `used`
   for a whole read, `used (targeted extraction)` for a read by `grep -n -F` extraction — whether or
   not a diagram was actually drawn from it (row 3's stand-down and row 4's abandon still charge
   their reads). No row for a file that was only probed with `wc -c` and never opened.

**Never a row for**: the fact ledger or the measurement script (FR-15 — both are parts of the command,
not inputs to it); `specs/.current-spec` (it only chooses the target); the existing `show-me.md`
(Step 4 reads it only because `Write` requires that, never as evidence); `PROMPT.md` or a
`PROMPT-*.md` companion (row 11, FR-16 — neither is read at all); review comments; CI checks (FR-9,
FR-15 — neither is an input to this command).

Before the assembled text names any repository path — a link, a `## Where to look first` entry, or a
diagram node — run `git ls-files --error-unmatch {path}` on it and drop it rather than write it if
the check fails (FR-17, NFR-5).

### Step 6 — Write the file once

Step 4 already ran `test -f specs/{dir}/show-me.md` and, when it existed, read it — never as evidence
for any section, only because `Write` refuses to replace a file the session has not read. Write Step
5's assembled text to `specs/{dir}/show-me.md` in one `Write` call, which replaces the file's entire
contents when it already existed. Never `git add`, stage, commit, or create any other file.

Print `specs/{dir}/show-me.md created.` when Step 4's `test -f` failed, or
`specs/{dir}/show-me.md replaced.` when it succeeded, and stop there. FR-19's fuller summary — the
overall risk level, its advisory reminder, and the word-count result — lands once the sections and the
risk step that produce them exist, in later tasks; do not improvise them now.
