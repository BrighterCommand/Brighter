---
id: 0072-show-me-command-resolution-and-output
title: "Target Resolution and Output Shape for /spec:show-me"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-09-19
summary: "/spec:show-me is two artefacts with one seam: a C# file-based measurement script that counts every mechanically countable value and writes it atomically to a gitignored JSON fact ledger, and the prompt file .claude/commands/spec/show-me.md that reads evidence within a per-run byte budget, judges, and writes show-me.md in one Write. The seam runs between counting and reading: the command may read files and pinned-sha diffs, but never computes a mechanically countable value. Every stop is decided before either file is written, and the script's exit status - 0, 2 or other - tells the command which stop, if any, applies."
tags:
  - "meta"
  - "api-design"
---

# 0072. Target Resolution and Output Shape for `/spec:show-me`

Date: 2026-09-19

## Status

Proposed

## Context

Spec 0037 asks for a slash command, `/spec:show-me [spec-id]`, that runs against a finished spec
and writes one durable file, `specs/NNNN-name/show-me.md`: what the spec changed, and how risky it
looks to merge. Half of that file is counted — files, lines, tasks, ids, commits — and must come out
the same on every run. The other half is judged — a narrative, a breaking-change list, a verdict on
each requirement — and cannot. A prompt that does both in one breath lets the judging do the
counting, and nothing then catches a wrong number.

### Terms

- **Measurer** — the measurement script. Key Components 1 states its contract.
- **Synthesiser** — the executing model reading `.claude/commands/spec/show-me.md`, in the stages
  that write prose. Key Components, *The stages*, states its rule.
- **Classifier** — the stage that judges breaking-change items and requirement statuses. Its rule is
  stated in
  [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md).
- **Explainer** — the stage that reads source to draw a diagram. Its rule is stated in
  [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md).
- **Fact ledger**, **measured head**, **merge base**, **spec diff**, **charged bytes** —
  `requirements.md` § *Definitions* states each one.
- **Read log** — the command's in-context record of what it read and what each read cost. Key
  Components 3 states it. It is never written to disk and is not the fact ledger.
- **Reserve** — the last 100,000 bytes of the per-run read budget, which only diagram reads may
  spend. Key Components 3 states its size; which reads spend it is
  [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md)'s.
- **Mechanically countable** — a count, sum or threshold outcome computed over the repository's
  artefacts: the diff, `tasks.md`, `requirements.md`, `.adr-list`, `release_notes.md`. The
  script computes every one. A tally of the command's own judgements — how many items it judged
  breaking, how many ids it judged `Shipped` — is not mechanically countable: NFR-1 names those
  tallies as judgement-derived, and the Classifier produces them. Nor is a byte size taken only to
  decide whether a read is affordable: NFR-3 makes that a free size probe, it is never written into
  `show-me.md`, and the command takes it itself (Key Components 3).

### Scope

**Parent requirement**: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)

**In scope**:

- FR-1, FR-2 — target resolution: the whole argument matched by the model over a pre-executed
  directory listing (Key Components 4).
- FR-3 — the completeness gate, evaluated from the script's exit status and gate record, above both
  writes (Key Components 4).
- FR-4, NFR-8 — exactly two writes; the ledger replaced atomically by the script, `show-me.md`
  replaced by one `Write` (Key Components 2 and 5).
- FR-5, FR-9, FR-10, FR-14's path list, FR-15 — the file's header, and the sections built from the
  ledger and the command's reads (Key Components 5).
- FR-6's narrative, FR-7, FR-8 — the section shapes, their read obligations and their mechanical
  parts. The judgement inside FR-7 and FR-8 is the Classifier's (0073).
- FR-10, FR-20 — spec branch, base ref, PR discovery, measured head and merge base, all resolved by
  the script (Key Components 1).
- FR-16 — every row's effect on a section, except the factor levels rows 8, 9 and 12 force (0073)
  and row 12's diagram line (0077).
- FR-17, NFR-5 — tracked paths only, tested before a path is written (Key Components 5).
- FR-18, C-10 — the writes, the single `gh pr list` query, and the one path-scoped allow-list entry
  (Technology Choices).
- FR-19 — the session report, including the word-count result.
- FR-21 — the measurement script: language, invocation, exit statuses, stderr records, ledger cap,
  atomic write, modes, pinned invocation, and the one failure mode (Key Components 1 and 2).
- NFR-1 — the split between mechanical and judged fields, made structural by the seam.
- NFR-2 — the word count, executed by the script's word-count mode.
- NFR-3 — the byte budget, the `wc -c` probe, the reserve's size, the full-diff ban and the
  degradation rule (Key Components 3). Which reads draw on the reserve is 0077's.
- NFR-4 — offline behaviour: the script exits `0` with the PR fields null.
- NFR-6 — the conventions of the three `/spec:show-me` artefacts.
- NFR-7 — traceability for counts and prose. The diagram half is 0077's.
- NFR-9 — the sibling test script, its fixtures, and the `CONTRIBUTING.md` merge-commit paragraph
  (Key Components 6).

**Out of scope**:

- The risk factors, the overall level, the forced levels and the advisory construction —
  [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md).
- The diagram trigger, drawing, caps, the reserve's spending and the fallback lines —
  [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md).
- The forms the `/spec` family writes (declared ids, task tags, marked release-notes sections) and
  the `/spec:write_release_notes` command — `0078-spec-family-machine-readable-forms`. This ADR owns
  only *reading* those forms.
- The four stated patterns. They are stated once, in `requirements.md` § *Definitions*, and
  implemented once, in the script. No ADR in this set transcribes one.

### Where this ADR sits

| ADR | Decides |
| --- | --- |
| **[0072-show-me-command-resolution-and-output](0072-show-me-command-resolution-and-output.md)** *(this one)* | What the command is, how it resolves its target, what it measures and reads, and the shape of the file it writes |
| [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md) | How the advisory risk level is computed, and how it stays advisory |
| [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md) | When the command draws a diagram, what it may draw, and which stage may read source to draw it |
| [0078-spec-family-machine-readable-forms](0078-spec-family-machine-readable-forms.md) | The forms the `/spec` family writes so that a tool can read them, and the command that writes release notes in one of them |

The sentence that unifies all four: **the command states only what it has measured, names what it
measured it from, and changes nothing.**

### Counting in prose that nobody runs

A pattern written into a markdown document is a description of code. Nobody executes it, so no test
reaches it, and it carries the escaping of the prose around it. This spec's own design review found
four defects of that kind in earlier drafts of this command:

| Defect | Returned | Correct | Cause |
| --- | --- | --- | --- |
| Declared-id count | 0 | 28 | The pipe had to be escaped to sit in a markdown table cell, and the escape became part of the pattern |
| NFR-2 word count | 16 | 8 | The count did not exclude fenced blocks |
| FR-13 invariant check | 3 hits | 0 | `if` matched inside `diff` |
| One stated pattern | 3 variants | 1 | The same pattern was copied into three places and drifted |

Each defect was invisible to a reader and untestable where it sat. In an executable artefact, each
one is an ordinary bug with an ordinary test. The requirements responded with FR-21: one script
does the counting, and each stated pattern is implemented once, in that script.

### The forces

- **Some output must be identical between runs, and the rest cannot be.** NFR-1 lists the
  mechanical fields and names the judged ones.
- **The command must count nothing it could get wrong silently.** FR-21 puts every countable value
  in one script, with its own test (NFR-9).
- **The command must still read evidence.** FR-7 needs the public-API lines of the diff, FR-14 needs
  the diff's paths, and FR-6's diagram needs source. Those are reads, not counts.
- **One run, one diff, one remote query.** FR-20 pins the merge base and measured head once. FR-18
  allows one `gh pr list` query per run and no `gh pr diff`.
- **A stop writes nothing.** FR-3 and NFR-8 require a stopped run to leave the repository
  byte-for-byte unchanged, but the gate reads counts the script produces.
- **The full diff does not fit.** Spec 0036's full diff is 4,081,673 bytes, which is more than the
  context window. NFR-3 budgets reads in bytes.
- **The script's output streams are not its own.** The toolchain writes diagnostics to standard
  output, and children write errors to standard error (FR-21).
- **No interpreter grant.** Every candidate language can run arbitrary commands, so the allow-list
  entry must name the script's own path (FR-18, C-10).
- **Neither spec ids nor ADR numbers are unique.** Three directories are numbered `0002`, one name
  contains spaces, and five ADRs are numbered `0037` (C-1, C-9).
- **The family has a shape.** Every `/spec:*` command is one markdown file with front matter
  (NFR-6).

## Decision

**Split `/spec:show-me` into a measurement script that counts and a command file that reads and
judges, joined by one JSON ledger file; draw the line between them at counting, not at reading; and
decide every stop before either file is written.**

The script resolves refs, queries the pull request once, counts everything NFR-1 lists, and writes
the ledger atomically. The command reads that ledger, reads the evidence it needs within a byte
budget, judges what NFR-1 names as judged, and writes `show-me.md` in one call. The command may
issue a path-scoped `git diff` over the ledger's two shas, and it may read files. It may never
compute a mechanically countable value.

### The mechanism, end to end

```mermaid
sequenceDiagram
    participant U as User
    participant C as Command file
    participant M as Measurement script
    participant L as Fact ledger
    U->>C: /spec:show-me spec-id
    C->>C: Step 1 - resolve the target over the directory listing
    C->>M: Step 2 - measure the target directory
    alt exit 2 - the spec is not finished
        M-->>C: gate record on stderr, no ledger
        C-->>U: the FR-3 stop message
    else any status other than 0 or 2
        M-->>C: nothing usable
        C-->>U: the FR-21 tooling-fault message
    else exit 0
        M->>L: replace the ledger atomically
        C->>L: Step 3 - read the ledger
        C->>C: Step 4 - evidence reads, charged to the read log
        C->>C: Step 5 - Explainer, Classifier, risk step, Synthesiser
        C->>C: Step 6 - one Write of show-me.md
        C->>M: Step 7 - word-count the written file
        M-->>C: word-count record on stderr
        C-->>U: Step 8 - the FR-19 report
    end
```

The run has four exits and one path to a written file. Four invariants read off the diagram:

- **Nothing is written above the `exit 0` branch.** Step 1's stops happen before the script runs.
  Step 2's two stops happen before the ledger exists, because the script writes the ledger only
  after the gate passes. A stopped run therefore leaves the repository unchanged. The one
  exception is FR-21's fifth failure state, where the script has already replaced the gitignored
  ledger (Key Components 4).
- **The exit status alone decides the branch.** The command never parses standard output. It reads
  standard error only on `2` and at Step 7, and then only the last line carrying the right prefix.
- **Only the script touches the ledger.** The command reads it once, at Step 3, and never writes it.
- **The word count cannot change the outcome.** Step 7 runs after the one `Write`. Its result is
  reported, and `show-me.md` is not revised.

### Where the pieces live

```mermaid
flowchart LR
    subgraph CMDS[".claude/commands/spec/"]
        CF["show-me.md<br/>the command file"]
        MS["show_me_facts.cs<br/>the measurement script"]
        TS["show_me_facts_tests.cs<br/>the test script"]
    end
    subgraph FIX[".claude/test-fixtures/show-me/"]
        FX["fixture directories<br/>and fixture files"]
    end
    subgraph SPEC["specs/NNNN-name/"]
        OUT["show-me.md<br/>tracked deliverable"]
        LED[".show-me-ledger.json<br/>gitignored"]
    end
    subgraph REPO["repository root"]
        GI[".gitignore<br/>one exact-match line"]
        ST[".claude/settings.json<br/>one path-scoped entry"]
        CO["CONTRIBUTING.md<br/>merge-commit paragraph"]
    end
    CF -->|invokes| MS
    MS -->|writes| LED
    CF -->|reads| LED
    CF -->|writes| OUT
    TS -->|invokes| MS
    TS -->|reads| FX
    ST -->|permits| MS
    GI -->|ignores| LED
```

In a `/spec:show-me` run, every write into the spec directory comes from the command or the
script. The test script reads the fixtures, and its only writes are to remove or restore a ledger
its own runs touched. Nothing is added under `src/` or `tests/`, and none of the three
`.claude/commands/spec/` artefacts is part of the product build.

### Key Components

#### The stages, and the rule each one holds

The command file is one ordered procedure, not a program, so these are stages, not types. Each name
is shorthand for the rule the stage holds.

| Stage | Is | Decided in | The rule it holds |
| --- | --- | --- | --- |
| Measurer | the measurement script | this ADR | Counts every value NFR-1 lists; never paraphrases, never judges |
| Explainer | the model, at Step 5 | [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md) | The only stage that reads source files; draws nothing it has not read |
| Classifier | the model, at Step 5 | [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md) | Judges each breaking-change item and requirement status once, and tallies its own judgements |
| Risk step | the model, at Step 5, inside the command file's risk-step markers | [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md) | Computes the factor levels and the overall level; the only step that may test a level |
| Synthesiser | the model, at Step 5 | this ADR | Writes all prose; every number it writes comes from the ledger or from the Classifier's tallies |

**The seam runs between counting and reading.** A stage on the model's side may read a file or a
diff, and every read is charged in bytes. No stage on the model's side may compute a mechanically
countable value (*Terms*), whether or not the ledger carries it. The test for the seam is AC-70:
the command file contains no pipeline that computes such a value.

#### 1. The measurement script

`.claude/commands/spec/show_me_facts.cs`, a C# file-based app, run from the repository root with
`dotnet run`. It takes a target directory and, for NFR-9 only, two optional test inputs: a
pinned pair and a release-notes path. Either may be given without the other (FR-21).

| Mode | Invocation | Writes | Exit status |
| --- | --- | --- | --- |
| Measuring | `dotnet run .claude/commands/spec/show_me_facts.cs {target directory}` | the ledger, beside the target, only on `0` | `0` — gate passed, complete ledger written. `2` — gate not passed, gate record on stderr, no ledger. Other — tooling fault |
| Measuring, with test inputs | the same, plus a pinned pair (a merge-base sha and a head sha), a release-notes path, or both | the ledger, only on `0` | as above; a pinned sha that is not a local commit is a tooling fault |
| Word count | the same, with a word-count switch and the path of the file to count | nothing | `0` — counted, word-count record on stderr. Other — not counted |

What the script measures is FR-21's list, grouped into ref fields, diff fields and file fields. What
each kind of run records in each group is FR-21's *kinds of run* table, which is normative; the
script implements that table and this ADR does not restate it. Six implementation rules sit
beside it:

- **Every stated pattern is implemented once, here.** `requirements.md` states each pattern in POSIX
  extended form. .NET's `Regex` does not support POSIX bracket classes, so each pattern is
  translated. The translation sits next to the POSIX original, quoted verbatim as a comment, and
  NFR-9's fixtures prove it. The public-API figure of 131 over the calibration pair is the proof for
  the pattern that matters most.
- **Diffs are read with a pathspec or a summary flag, never whole.** Net lines come from
  `--numstat`, bucket counts from `--name-only`, and the public-API count from the `src/`-scoped
  diff. NFR-3's full-diff ban binds the script as it binds the command.
- **Children's standard error is captured, not passed through.** `git` and `gh` run as child
  processes with both streams redirected, so a child's message can never carry a record prefix.
  A non-zero `gh` exit means no PR; FR-16 rows 1 and 2 apply, and the script still exits `0`
  (NFR-4).
- **The script also emits the two values NFR-1 derives from its counts**: factor F1's level, and
  whether each of D1, D2 and D3 fired. F1's thresholds are FR-11's and the tests are FR-6 (a)'s;
  both are implemented once, here. Both are diff fields, so both are null when no spec diff was
  measured, which is when FR-6 (a) says the trigger is not evaluated. F1's level in that case is
  FR-16 row 12's, applied as
  [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md) decides.
- **The script locates what the command must read.** For each marked section for the target it
  emits the section's first and last line numbers and its byte size, and for the `src/`-scoped diff
  it emits its byte size. Section recognition — headings, fences, extent — is therefore
  implemented once, in the script, and the command reads a section by its line range without
  re-recognising it. The line ranges are inputs the `{m}` field depends on. The byte sizes let the
  command price two reads it could otherwise size only by running them. FR-21 fixes the ledger's
  fields as a minimum, so neither addition changes its contract. The section fields are file
  fields and the diff size is a diff field.
- **The script decides nothing a reader could judge.** It emits the marked-section count and `{m}`,
  not a verdict on what the release notes say. It emits the declared-id set, not a status for any id.

The stderr records are one line each: `show-me-gate: ` or `show-me-wordcount: `, then a single-line
JSON object. The gate record carries which of FR-3's three cases applies and, for the unchecked
case, `{n}`, `{total}` and the first three unchecked titles. The word-count record carries the
counted total, the excluded fenced-block line count, and whether the total is inside 400–2,000. The
command reads only the last line with the right prefix.

#### 2. The fact ledger

One JSON object at `{target directory}/.show-me-ledger.json`, serialised with `System.Text.Json`. It
carries a `schema_version` integer and one named field per FR-21 value. A value that is not
determinable is `null` with a sibling reason field, never `0` and never absent. It carries the
command line of every `gh` invocation the script made, so AC-30 and AC-73 can observe the run's
GitHub access. It carries no diff text and no file list, which keeps it far below the 65,536-byte
cap.

**The write is atomic.** The script serialises the whole object in memory, writes it to a temporary
file in the same directory, and replaces the ledger with `File.Move(…, overwrite: true)`. A
same-directory move is a rename, so a reader sees either the old ledger or the new one. The temporary
file is deleted in a `finally` block. A failure before the move therefore leaves no ledger where
none existed and leaves a previous ledger unchanged (AC-83).

The ledger is working state. The exact-match `.gitignore` line `.show-me-ledger.json` ignores it at
any depth, so `git status --porcelain` differs after a run only by `show-me.md` (AC-30, AC-74).
Because it is untracked, `show-me.md` never names it, and every value it carries is attributed to
the artefact the script counted it from (FR-17, NFR-7).

#### 3. The command's reads, and the read log

Every read the command makes is charged against NFR-3's budget of 1,048,576 bytes, and recorded in
the read log with its path, how it was read, and the bytes charged. No full-content read is issued
before its size is known, and knowing a size costs nothing:

- a file's size comes from `wc -c` on its path, which is a size probe, not a read;
- the `src/`-scoped diff's size and each marked section's size come from the ledger;
- the size of any other `git diff` output comes from the same command piped into `wc -c`, which
  brings only the byte count into context.

A file whose size exceeds the bytes remaining is not read whole; it is read by targeted extraction
or in bounded chunks. An extraction or a chunk is bounded before it is issued — a line range, or a
chunk no larger than the bytes remaining — and is charged the bytes it brings in.

| Read | How | Budget |
| --- | --- | --- |
| The ledger | whole | general allowance; at most 65,536 B |
| `tasks.md` | whole | general allowance |
| The `src/`-scoped diff over the ledger's merge base and measured head | whole, as one `git diff` with a `src/` pathspec | general allowance |
| `git diff --name-only` over the same pair | only when the spec diff touches nothing under `src/` (FR-14) | general allowance |
| Each ADR in `.adr-list` | front matter, `## Status` and `## Consequences` only | general allowance |
| `requirements.md` | whole if it fits; otherwise the declarations and the paragraphs the Classifier needs | general allowance |
| Marked release-notes section(s) for the target | each by the line range the ledger gives; all together or none | general allowance |
| `.issue-number`, `.adr-list`, `specs/.current-spec` | whole | general allowance; negligible |
| Source files for a diagram | as [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md) decides | whatever the general allowance has left, plus the reserve |

The reserve is the last 100,000 bytes of the budget, and only the Explainer's reads may spend it.
Everything else draws on the remaining 948,576 bytes. On spec 0036 the fixed reads cost 694,377
bytes — the ledger at its cap, `tasks.md` whole, the `src/`-scoped diff and seven ADR extracts —
which leaves 254,199 bytes for `requirements.md`. At 273,674 bytes it does not fit, so it is read by
extraction. The reserve is untouched when the Explainer starts (AC-78).

**Two reads are obligations, not options.** When a marked section exists for the target, the command
reads it, unless the bytes remaining cannot cover every marked section; then it reads none and
FR-16 row 5a applies. When evidence FR-7 or FR-8 needs cannot be extracted, the affected status is
`Unverifiable` or the affected input `not available`, with its reason, never zero (NFR-3). FR-3's and
FR-9's values come from the ledger and are never extracted by the command.

**The full diff is never read.** At 4,081,673 bytes it exceeds the context window, so a run that
tried it would fail rather than degrade. Every `git diff` the command issues names the ledger's two
shas and either a pathspec or a summary flag (AC-73, AC-77).

The read log is the source of FR-15's rows for everything the command read, and the evidence for
AC-52, AC-63 and AC-76. It lives in the model's context for one run, and nothing writes it to disk.

#### 4. The precondition gate and the four stops

| Stop | Decided at | Evidence | Message |
| --- | --- | --- | --- |
| FR-1 — ambiguous or no match | Step 1 | the pre-executed `ls -1d specs/*/` listing | FR-1's two messages |
| FR-2 — no usable current spec | Step 1 | `specs/.current-spec` and `test -d` | FR-2's message |
| FR-3 — spec not finished | Step 2 | exit `2` and the gate record | FR-3's three messages |
| FR-21 — tooling fault | Step 2, or Step 3 when the ledger does not parse | the exit status, or the unparseable record or ledger | FR-21's one message, naming which of the five states applies |

The command resolves the target by matching the **whole** trimmed argument over the listing, in
FR-1's three-rule order, in the model rather than in a shell loop. The listing is a few dozen short
lines, and a shell loop over `specs/*/` splits `specs/0021-Expose Unacceptable Message Window/` into
four words unless every expansion is quoted — the kind of detail that rots. The listing's trailing
slash excludes `specs/README.md` and `specs/.current-spec` by construction (AC-35).

There is no fifth stop. No absent or degraded input stops the run; each FR-16 row sets its
section's defined text and the run continues. A run that exits `0` at Step 2 and parses at Step 3
always writes `show-me.md`, whatever the risk level (FR-13).

In FR-21's fifth failure state the script exits `0` and the ledger does not parse. The script has
already replaced the ledger atomically, so that ledger stays in place. The command stops without
reading it as fact, and the next successful run replaces it (NFR-8's stated exception).

#### 5. The output document

`show-me.md` is assembled in memory in FR-5's order and written with one `Write`, which replaces
the whole file. Before any repository path is written into it — a link, a path in `## Where to look
first`, a node in a diagram — the command runs `git ls-files --error-unmatch` on that path, so an
untracked path such as `PROMPT.md` cannot appear (FR-17, NFR-5). Every ADR reference carries the
filename stem and a relative link, never a bare number (C-9).

| Section | From the ledger (copied, never recomputed) | From the command's reads (judged) |
| --- | --- | --- |
| Metadata block (FR-5) | branch ref, measured head, base ref, merge base, PR number and URL | — (issue from `.issue-number`) |
| `## What changed and why` (FR-6) | `.adr-list` resolution | the 150–600-word narrative; each ADR's title and Status from its extract. The diagram or fallback line is 0077's |
| `## Breaking changes` (FR-7) | marked-section count and `{m}` | the item list, classifications, migrations and the count `{n}`, from the Classifier; the disagreement line when `{m}` is not null and differs from `{n}` |
| `## Did it ship what it said?` (FR-8) | the declared-id set and `{total}` | each id's status, and the tallies `{k}` and Part 4's terms, from the Classifier; the Synthesiser writes Part 1's id list from those statuses by FR-8's collapse rules |
| `## How it was built` (FR-9) | task total, per-tag counts, commit count — or FR-9's fallback line when the diff fields are null | nothing |
| `## Blast radius` (FR-10) | everything, including the provenance lines | nothing |
| `## Risk assessment (advisory)` (FR-11–FR-13) | F1's `src/` count and F1's level | see [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md) |
| `## Where to look first` (FR-14) | — | 3–7 paths from the diff reads, ordered, each with a reason of at most 25 words. The optional tree is 0077's |
| `## Inputs used` (FR-15) | resolution of `.adr-list` entries, PR presence and its reason | one row per input the read log records, marked `used`, `used (targeted extraction)` or `not available: {reason}` |

`## How it was built` states two things and nothing else: task shape and commit count. It carries
no review history and no CI state, and no `## Inputs used` row names either (FR-9, AC-17, AC-62).

`## Inputs used` has no row for the measurement script and no row for the ledger. Both are part of
the command, not inputs to it, and the ledger is untracked (FR-15, AC-75). An untracked `PROMPT.md`
or `PROMPT-*.md` may be read as background, but it is never cited and gets no row; an absent one is
recorded nowhere (FR-16 row 11). A tracked `PROMPT.md` is an ordinary input like any other file
(FR-17).

#### 6. The test script and its fixtures

`.claude/commands/spec/show_me_facts_tests.cs`, a second C# file-based app, run by a person or a task
as `dotnet run .claude/commands/spec/show_me_facts_tests.cs` from the repository root. It invokes
the measurement script as a child process for each row of NFR-9's table, asserts on the exit status,
the ledger's fields and the stderr record, prints each failed assertion, and exits non-zero if any
fails. It uses no test framework and touches nothing under `src/` or `tests/`. Like the
measurement script, it is committed at mode `100644` and carries no first-line marker.

- **Fixtures** live under `.claude/test-fixtures/show-me/`. That keeps fixture `.md` files out of
  `.claude/commands/`, where each would register as a slash command, and out of `specs/`, where each
  would appear in `/spec:status`.
- **Ledgers are restored.** Before each run, the test script saves any ledger already in the target
  directory. Afterwards it deletes a ledger its run created, and restores a saved one byte-for-byte.
- **Residue is checked.** After all runs, the test script asserts that no temporary file remains in
  any fixture directory (AC-83).
- **The FR-13 invariant check** runs over `.claude/commands/spec/show-me.md`. Its assertion is
  0073's to define. The test script matches whole words only, and it proves this on a literal line
  held in the test script itself (AC-81).

The calibration row is pinned to `6145913a0` and `91d549be6`, so it survives PR #4282's merge as
long as both commits stay reachable. `CONTRIBUTING.md` gains one paragraph asking that pull
requests are merged with a merge commit, and says why: tooling pins commit shas from a merged
branch's history, and only a merge commit keeps them reachable (AC-94).

#### Where each artefact is touched

| Path | Change |
| --- | --- |
| `.claude/commands/spec/show-me.md` | Rewritten: gate, reads, stages and write, with no counting pipeline |
| `.claude/commands/spec/show_me_facts.cs` | New: the measurement script |
| `.claude/commands/spec/show_me_facts_tests.cs` | New: the test script |
| `.claude/test-fixtures/show-me/` | New: NFR-9's fixture directories and files |
| `.claude/commands/spec/README.md` | `/spec:show-me` catalogued, and both scripts documented |
| `.claude/settings.json` | One entry: `Bash(dotnet run .claude/commands/spec/show_me_facts.cs:*)` |
| `.gitignore` | One line: `.show-me-ledger.json` |
| `CONTRIBUTING.md` | One paragraph: merge commits, and why |

Deliberately unchanged: every file under `src/` and `tests/`, the CI workflows, the `gh` entries and
the `deny` list in `.claude/settings.json`, and `release_notes.md`. The amendments to
`/spec:requirements`, `/spec:tasks`, `/spec:design` and `/spec:review` belong to
`0078-spec-family-machine-readable-forms`.

### Technology Choices

#### Why the script is a C# file-based app

- **Every contributor has the toolchain.** Brighter cannot be built without the .NET SDK. CI installs
  the 10.0 SDK, and no `global.json` pins a lower one, so file-based apps add nothing to anyone's
  machine. Python is not comparable: the repository has no `.py` files, and it ships both `.sh` and
  `.ps1` scripts because `CONTRIBUTING.md` documents Linux, macOS and Windows paths.
- **It has a real JSON serialiser.** The defect class this seam removes is hand-made escaping.
  `System.Text.Json` escapes quotes, backslashes and non-ASCII text without being asked.
- **It needs no build step.** `dotnet run {file}.cs` needs no project, restore or build command. It
  costs about 7.8 s on the first run, 1.3 s after an edit and 0.2 s warm, measured on this
  repository. The root `Directory.Build.props` does not disturb it.
- **It is committed at mode `100644`.** `dotnet run` reads the file, as `awk -f` reads
  `generate_adr_index.awk`, so the file is not executed by path. A C# file-based app needs no
  first-line marker when it is run through `dotnet run`, so it carries none (NFR-6, AC-53).

#### Why the payload travels in a file

Measured on this repository: when a file-based app is recompiled, `dotnet run` writes the compiler's
warnings to standard output, ahead of the program's own output. A warm run is clean. A contract on
standard output would therefore pass every test and fail on a fresh clone, in CI, or after an edit.
The ledger is a file the script writes itself, so no toolchain can interleave with it.

The two payloads that must reach the command without a ledger — the gate facts and the word count —
are small and travel on standard error. Standard error is not clean either, so each is one line with
its own prefix, and the command reads only the last line carrying that prefix.

#### Why the one allow-list entry names the script

`Bash(dotnet run:*)` would let the command compile and run any C# file, which would make the `deny`
list's `curl`, `wget` and `ssh` entries bypassable. `Bash(dotnet run .claude/commands/spec/show_me_facts.cs:*)`
permits that one program. Removing the script leaves the entry permitting nothing, and a second C#
file in the same directory is not covered (AC-82). Arguments after the path cannot widen it: measured
on this repository, `dotnet run {file}.cs --project {other}` and `--file {other}` still run
`{file}.cs`, passing the rest to it as arguments. The command's own `git` reads and its `wc -c`
probes are already covered by existing entries. The script's `git` and `gh` children need no entry.

The command file's `allowed-tools` lists what the command itself runs: `ls`, `cat`, `test`, `wc`,
`git diff`, `git ls-files`, the script entry above, `Read`, `Grep` and `Write`. It lists no `gh` verb, no
`git merge-base` and no interpreter (AC-30).

#### Why the command reads diffs itself

The alternative was a script that extracts the evidence — the `src/`-scoped diff, the ADR extracts —
into files for the command to read. FR-4 forbids diff text in the ledger, and a second file would
break FR-4's two-write rule. More basically, reading is not where the defects were. The four defects
above were all counts. A read of a pinned diff returns the same bytes every time; a count written in
prose does not reliably return the same number. The seam therefore runs between counting and
reading, and the command reads under the byte budget.

#### Why there is no sub-agent

`/spec:status` and `/spec:gear`, the read-and-report commands, run in the main agent, and so does
this one. A sub-agent starts with a clean context, so it would either receive every extract in its
prompt or read the inputs a second time, which would double the budget. FR-19's report must come
from the main agent anyway.

#### Why the test script is also C#

It inherits every reason the measurement script has, and one more: it parses the ledger as JSON and
asserts on fields, which C# does with the same serialiser the script writes with. A shell test script
would need a `.ps1` twin for Windows, and would compare JSON as text.

### Implementation Approach

Numbered in commit order. Each behavioural step is test-first: the test script's row comes before
the script code that satisfies it.

1. **Structural.** Add the `.gitignore` line and the `.claude/settings.json` entry.
2. **Structural.** Create the fixture tree under `.claude/test-fixtures/show-me/` from NFR-9's table.
3. **Behavioural.** The test script's harness: invoke the script, parse the ledger, save and restore
   any pre-existing ledger, report failures, set the exit code.
4. **Behavioural.** Script file fields: task checkboxes and tags, declared ids, `.adr-list`
   resolution, the gate and exit `2` with its record. Fixtures: *declared*, *zero-id*, *no-tasks*,
   *unfinished*.
5. **Behavioural.** Marked-section recognition and `{m}`, reading the form
   `0078-spec-family-machine-readable-forms` defines. Fixture: the release-notes file.
6. **Behavioural.** Pinned diff fields: buckets, net lines, `src/` subdirectories, public-API lines,
   commit count. Rows: the calibration spec and the pinned *declared* fixture.
7. **Behavioural.** Unpinned ref fields: spec branch, base ref, PR discovery, measured head, merge
   base, the `gh` record, and FR-10's divergence and merged-candidate lines. The kinds-of-run nulls.
8. **Behavioural.** The atomic write, the 65,536-byte cap and the residue check.
9. **Behavioural.** Word-count mode. Fixtures: the two word-count files.
10. **Behavioural.** The command file: the pre-executed directory listing; Step 1's resolution; Step 2's exit-status
    handling and the four stop messages; Step 3's ledger read; Step 4's reads and read log; Step 5's
    stages in the order *Explainer, Classifier, risk step, Synthesiser*; Step 6's tracked-path check and one
    `Write`; Step 7's word count; Step 8's report.
11. **Structural.** The README catalogue entries and the `CONTRIBUTING.md` paragraph.

## Consequences

### Positive

- **The mechanical fields are deterministic by construction.** One program produces them, and a
  program run twice on the same inputs gives the same answer. NFR-1 becomes testable, and NFR-9
  tests it on real fixtures, including the two defects that motivated the split.
- **A stop writes nothing, by step order.** The script writes the ledger only after the gate passes,
  and the command writes `show-me.md` only after that. AC-7 and AC-71 follow from the order.
- **The exit status is the whole protocol.** `0`, `2` and anything else map to three branches. No
  output is parsed to decide which one applies, so toolchain noise cannot change a run's outcome.
- **Every diff in a run describes the same change.** The ledger pins two shas once, and every read
  names them. Two sets of numbers cannot appear in one `show-me.md` (FR-20).
- **The budget is met by arithmetic.** Every read is priced before it is issued, so a correct run
  cannot exceed 1,048,576 bytes, and spec 0036's fixed reads leave the reserve untouched.
- **`## Inputs used` is a projection.** It is built from the ledger and the read log, so an input
  that was used has a row, and a claim with no source has nowhere to come from (NFR-7).
- **The command's tool surface is small.** No `gh` verb and no interpreter appear in its front
  matter; the one new allow-list entry permits one program.

### Negative

- **The .NET 10 SDK becomes a hard requirement** for anyone running `/spec:show-me`, and the first
  run after an edit pays a compile of several seconds.
- **FR-18's confinement moved into code.** The `gh` call is in the script, so the front matter no
  longer shows it. AC-30 is checked against the ledger's `gh` record and the script's source.
- **The ledger is a second written file.** It is kept out of `git status` by one `.gitignore` line,
  and a reader of `show-me.md` cannot follow a number to it — only to the artefact the script
  counted it from.
- **The judged sections are not reproducible, and the level can move with them.** FR-7's items and
  FR-8's statuses may differ between runs, and they feed F2 and F5. NFR-1 says so, and so does this
  ADR.
- **The byte budget can still bind.** A spec whose `requirements.md`, `tasks.md` and diff are all
  larger than spec 0036's pushes more reads into extraction. The degradation is designed, but it is
  still context the synthesis does not get.
- **Format drift is not detected by this command.** A `tasks.md` or `requirements.md` in a form the
  patterns do not recognise yields `untagged` tasks or FR-16 row 9. The forms are prescribed and
  reviewed by `0078-spec-family-machine-readable-forms`, not policed here.
- **Two new artefacts to maintain**, in a family that had one executable file.

### Risks and Mitigations

- **Risk: a stage on the model's side computes a mechanically countable value** — estimating a file
  count, or counting declaration lines in the diff it read.
  *Mitigation*: the command file names the ledger field for every counted value it writes. AC-70
  checks that the command file contains no counting pipeline, and AC-32 compares two runs'
  mechanical fields.
- **Risk: a killed script leaves its temporary file behind.** The `finally` block does not run when
  the process is killed, and the temporary file's name is not covered by the exact-match
  `.gitignore` line.
  *Mitigation*: the temporary file carries a fixed name in the target directory, so the next run
  overwrites it and removes it. The residue check in NFR-9's test catches a leak in any path the
  script can exit by.
- **Risk: a compiler warning or a `gh` error carries a record prefix.**
  *Mitigation*: the script captures its children's standard error, and the prefixes are
  `show-me-`-qualified strings that no toolchain emits. The command reads only the last prefixed
  line.
- **Risk: the argument is split on whitespace by a later edit.**
  *Mitigation*: FR-1 states the whole-argument rule, AC-35 tests it with the unquoted four-word
  name, and Key Components 4 records why the match runs over a line list.
- **Risk: the calibration commits become unreachable** if PR #4282 is squashed or rebased.
  *Mitigation*: the `CONTRIBUTING.md` paragraph (AC-94). Enforcing the merge method in GitHub's
  settings is out of scope, so this stays a risk.

## Alternatives Considered

### Alternative 1: Keep measuring in shell pipelines written into the command file

Leave the counting where earlier drafts had it: `git`, `gh`, `grep` and `awk` pipelines quoted in
the prompt.

**Rejected because the reproducibility is illusory.** A pipeline that runs is deterministic. A
pipeline transcribed into markdown is a description that no one executes and no test reaches, and it
carries the markdown's escaping. The four defects in *Counting in prose that nobody runs* are that
failure, observed. The claim that a script would be "a new category" in this family does not hold
either: `.claude/commands/adr/generate_adr_index.awk` is an executable artefact in it already.

### Alternative 2: Replace the judged sections with mechanical proxies

Maximise determinism by deriving breaking changes from declaration lines, requirement status from
commit messages, and the narrative from `git log --oneline`.

**Partially accepted, and rejected for the rest.** It is accepted wherever a mechanical answer is the
right answer, which is why `## Blast radius` and `## How it was built` have no judged content, and
why FR-8's id set is mechanical. It is rejected for the judged sections:

- A declaration-line diff cannot tell an added overload from a breaking signature change, and cannot
  see a behavioural break. `MapperLifetime.Scoped` no longer caching for the life of the process
  changes no declaration line.
- An id in a commit message is not evidence that a requirement shipped.
- FR-6 asks for prose a contributor can read without the ADRs. A commit log is the artefact this
  command exists to replace.

### Alternative 3: A program that owns the whole command

A thin prompt that runs a program which generates the markdown, with the model filling narrative
slots.

**Rejected because the narrative slots are most of the point.** FR-6's narrative, FR-7's migrations,
FR-12's rationale and FR-14's reasons are what a reader reads. Templating around them splits their
provenance across two artefacts. FR-16's degradation rows would also become a program's error paths,
away from the step that discovers each absence.

### Alternative 4: Delegate synthesis to a sub-agent

Measure and read in the main agent, then hand everything to a sub-agent that returns the eight
sections.

**Rejected.** A sub-agent with no file-editing tool is safer, which is the family's reason to
delegate, but this command is read-only apart from one `Write`. A clean-context sub-agent must either
receive every extract in its prompt or spend the budget twice, and FR-19's report must come from the
main agent regardless.
[0077-show-me-visual-explanation](0077-show-me-visual-explanation.md) reaches the same conclusion for
the Explainer, for a reason of its own.

### Alternative 5: Write the measurement script in awk, Python or shell

- **awk** is the family's one precedent and needs nothing installed. **Rejected because it has no
  JSON support.** Hand-writing the escaping of quotes, backslashes and non-ASCII text rebuilds the
  hazard this seam removes. `"cmd" | getline` also hides a child's exit status, and the script must
  tell "`gh` found nothing" from "`gh` failed".
- **Python** fits best technically: a probe emitted valid JSON on a clean standard output and ran in
  0.05 s. **Rejected on portability within this repository.** Python 3 is not present by default on
  Windows, so it would add a prerequisite or need a second script, and FR-21 allows one. CI would
  not catch the gap, because every job runs on `ubuntu-latest`.
- **A POSIX shell script** needs nothing installed. **Rejected for the JSON reason, more acutely**, and
  because it would need a `.ps1` twin.

### Alternative 6: Pre-extract the evidence in the script

Let the script write the `src/`-scoped diff and the ADR extracts to files, so the command runs no
shell command after the ledger is read.

**Rejected.** FR-4 allows two written files and forbids diff text in the ledger, so the extracts
would need a third file that FR-4 does not allow. The rule it would protect — no shell call during
synthesis — guards against the wrong failure: the observed defects were counts, not reads. A
path-scoped `git diff` over two pinned shas returns the same bytes on every run.

### Alternative 7: Read `tasks.md` and the full diff and let the model count

**Rejected.** Spec 0036's full diff is 4,081,673 bytes, about 1.24 M tokens, which exceeds the
context window, so the run would fail rather than degrade. `tasks.md` is read whole, because at
229,159 bytes it fits the budget. What stays rejected is letting the model count, which would
collapse NFR-1's split.

## References

- Requirements: [specs/0037-show-me/requirements.md](../../specs/0037-show-me/requirements.md)
- Related ADRs:
  - [0073-show-me-advisory-risk-model](0073-show-me-advisory-risk-model.md) — the risk level, its
    factors and its advisory construction. It reads F1's input from the ledger and F2's and F5's
    from the Classifier.
  - [0077-show-me-visual-explanation](0077-show-me-visual-explanation.md) — the diagram trigger, the
    Explainer and the reserve's spending. It reads its trigger values from the ledger.
  - [0078-spec-family-machine-readable-forms](0078-spec-family-machine-readable-forms.md) — the forms
    the script's patterns recognise, and the command that writes marked release-notes sections.
  - [0071-tdd-review-gear](0071-tdd-review-gear.md) — the other ADR that decides a `/spec:*`
    command's own behaviour rather than Brighter's runtime.
- Conventions and prior art in this repository:
  - [`.claude/commands/spec/status.md`](../../.claude/commands/spec/status.md) — a read-and-report
    command with no sub-agent.
  - [`.claude/commands/spec/switch.md`](../../.claude/commands/spec/switch.md) — the pre-executed `!`
    shell block and whole-`$ARGUMENTS` handling.
  - [`.claude/commands/spec/README.md`](../../.claude/commands/spec/README.md) — the command
    catalogue and the sub-agent policy.
  - [`.claude/commands/adr/generate_adr_index.awk`](../../.claude/commands/adr/generate_adr_index.awk)
    — the family's first executable artefact, committed at mode `100644`.
  - [`.claude/settings.json`](../../.claude/settings.json) — the allow-list this ADR adds one entry to.
  - [`.agent_instructions/adr_frontmatter.md`](../../.agent_instructions/adr_frontmatter.md) — "the
    number is a non-unique ordering hint; identity is the filename stem".
- External references: PR [#4282](https://github.com/BrighterCommand/Brighter/pull/4282) — spec 0036's
  pull request, the calibration case. Its figures (517 files, 76 under `src/`, 131 public-API lines,
  the byte sizes) are `requirements.md`'s, measured over `6145913a0..91d549be6`.
