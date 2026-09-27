---
allowed-tools: Bash(ls:*), Bash(cat:*), Bash(date:*), Bash(head:*), Bash(tail:*), Bash(test:*), Bash(wc:*), Bash(grep:*), Bash(git diff:*), Bash(git log:*), Bash(git ls-files:*), Bash(dotnet run .claude/commands/spec/show_me_facts.cs -- specs/:*), Read, Write
description: Summarise a finished spec and give an advisory merge-risk read
argument-hint: [spec-id]
---

## Available specifications

!`ls -1d specs/*/ 2>/dev/null`

## Your task

Summarise the finished spec named by `$ARGUMENTS` (or the current spec, if none is given) into
`specs/{spec}/show-me.md`: what changed and why, breaking changes, a requirement reconciliation, how
it was built, blast radius, an advisory Low/Medium/High merge-risk read, where to look first, and a
provenance table. This is not a review — it does not re-check correctness, security, TDD compliance,
CI status or PR review outcomes.

**This command is under active construction (spec 0037).** Only Steps 1–3 are implemented below;
Steps 4 onward — evidence reads, the synthesis stages and the write — land in later tasks and are not
yet part of this command. Follow Steps 1–3 exactly as written, and when Step 3 finishes, **stop
there**: print what it says to print and do nothing else. Do not improvise any later step, and do not
create or modify `show-me.md`, the fact ledger, or any other file.

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

**On success**, the ledger's fields are held. Stop there — Steps 4 onward, which use this table to
write each section, are not yet part of this command.
