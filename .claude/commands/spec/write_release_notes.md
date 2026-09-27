---
allowed-tools: Bash(ls:*), Read, Grep, Glob, Edit
description: Write or replace a spec's marked release-notes section
argument-hint: [spec-id]
---

## Available specifications

!`ls -1d specs/*/ 2>/dev/null`

## Your task

Write, or replace in place, the release-notes section for the spec named by `$ARGUMENTS` (or the
current spec, absent an argument) into `release_notes.md`'s marked form, and stop without writing
whenever proceeding would mean guessing whose section is whose.

**This command is under active construction (spec 0037).** Steps 1–4 are implemented below — every
stop from ladder rows 1 to 7. The write itself — ladder rows 8 and 9, and the section's own form —
lands with a later task and is not implemented yet. Follow each step exactly as written; do not
improvise past what a step actually says.

### Step 1 — Resolve the target spec directory

Candidates are the directory names in the `## Available specifications` listing above — the last
path segment of each line, trailing slash removed. `specs/README.md`, `specs/dlq-review-findings.md`
and `specs/.current-spec` are not directory entries and never appear in that listing, so they are
never candidates.

Take `$ARGUMENTS` **as a whole** — the entirety of the argument text, leading and trailing whitespace
trimmed, any wrapping quotes removed, internal whitespace preserved verbatim. Do **not** split it on
whitespace.

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
  '{value}'}). Pass a spec id (/spec:write_release_notes 0036-scoped-lifetime-per-pipeline) or run
  /spec:switch first.` — using `missing` when the file does not exist, `empty` when it exists but is
  empty or whitespace-only, and `stale: names '{value}'` (with `{value}` the file's trimmed content)
  otherwise. Note the command names **itself** here, `/spec:write_release_notes`, in the example —
  never `/spec:show-me`.
- Otherwise the target is the directory it names. Continue below.

**On success**, the target is resolved. Hold two things for the steps below: `{dir}`, the full
target directory name, and `{NNNN}`, its leading four-digit id (the digits up to the first `-`).
There is **no FR-3 precondition check** here — unlike `/spec:show-me`, this command runs at design
time, long before a spec is finished, so an incomplete spec is not a stop. Continue to Step 2.

### Step 2 — Locate `release_notes.md`'s headings and fences, and its first `##` heading (ladder rows 2, 3)

- Run `test -f release_notes.md`. If it fails, stop. Do not create the file. Print exactly:
  `release_notes.md does not exist — /spec:write_release_notes never creates it.`
- Otherwise, find every heading line and every fence-delimiter line in `release_notes.md`, with line
  numbers, in one `Grep` call. A *heading* is a line beginning with one to six `#` followed by a
  space; a fence-delimiter line begins with three backticks. Discard any heading line that falls
  between an opening and a closing fence-delimiter line — it is inside a fenced block, not a real
  heading (Definitions, *Marked release-notes section*).
- From the remaining headings, find the first `##` heading (depth exactly two). If there is none,
  stop. Do not modify the file. Print exactly:
  `release_notes.md has no ## heading — /spec:write_release_notes has nowhere to write under.`
- Otherwise hold that heading's line number and text, and the line number of the **next** `##`
  heading after it (or end-of-file if there is none) — this range is the *first section*, the only
  place this command ever writes. Continue to Step 3.

### Step 3 — Check the target has something to derive items from (ladder row 4)

Check `specs/{dir}/.adr-list` and `specs/{dir}/requirements.md`.

- If `.adr-list` is missing or empty, **and** `requirements.md` is also missing, stop. Do not modify
  `release_notes.md`. Print exactly:
  `Spec {dir} has neither a non-empty .adr-list nor a requirements.md — nothing to derive
  release-notes items from.`
- Otherwise there is at least one source to derive items from. Continue to Step 4.

### Step 4 — Find marker lines and unmarked same-id headings under the first `##` heading (ladder rows 5–7)

- Find every marker line **in the whole file**, not only the first section — a line whose trimmed
  text is exactly `<!-- spec: {name} -->` for some `{name}` — with a `Grep` searching for the
  literal text `<!-- spec: `, returning line numbers. A `###` heading is *marked* when a marker
  line is the line directly after it, and *marked for the target* when that line, trimmed, equals
  `<!-- spec: {dir} -->` exactly (a literal comparison, never a substring match). Searching the
  whole file matters here: a section marked for the target could sit under any `##` heading, not
  only the first, and that is exactly ladder row 6's case below — scoping this search to the first
  section would make such a section invisible and the command would wrongly fall through to writing
  a duplicate.
- Count the `###` headings marked for the target, anywhere in the file.
  - **More than one**: stop. Do not modify the file. Print exactly:
    `release_notes.md has {n} sections marked for {dir} — delete all but one, then re-run: {list of
    each marked heading's line number and text}.`
  - **Exactly one**: check whether that heading's line number falls inside the first section's range
    from Step 2.
    - **No** (it sits under a later `##` heading than the first): stop. Do not modify the file.
      Print exactly:
      `The section marked for {dir} sits under a later ## heading than the first ({first heading's
      text}) — released notes are not rewritten. Move the section under the first ## heading by
      hand, or delete it, and re-run.`
    - **Yes**: hold it for the write (T15.5's job — not implemented yet). Skip the rest of this
      step.
  - **None marked for the target**: continue below.
- When no section is marked for the target, find every `###` heading **within the first section's
  range from Step 2 only** — row 7 is scoped to the first heading, unlike the marker search above
  that is **not** marked for *any* directory (no marker line directly beneath it) and whose text
  contains the literal substring `(spec {NNNN}`, using the target's own `{NNNN}` from Step 1. A
  heading followed by a marker line naming some *other* directory is not a candidate here — it
  already belongs to that other spec and is left alone.
  - **One or more such candidates**: stop. Do not modify the file. Print exactly:
    `release_notes.md has an unmarked heading naming spec {NNNN} under its first ## heading, with no
    marker line beneath it: "{heading text}" (line {n}). Delete that section, or add the line
    <!-- spec: {the directory this section actually belongs to} --> beneath its heading and re-run —
    marking it hands the section's body to this command, which replaces everything but its title on
    every future run.` — naming every such heading found, one per line, when there is more than one.
  - **None**: there is nothing standing in the way of a fresh section (ladder row 9). Continue below
    — the write itself is T15.5's job, not implemented yet.

Nothing beyond this point is implemented. Do not write, replace or insert anything into
`release_notes.md`, and do not judge any breaking-change item, until a later task extends this step.
