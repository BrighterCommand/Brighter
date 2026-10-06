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

Follow each step exactly as written; do not improvise past what a step actually says. Read at most
200 lines per `Read` call in every step below, taken from `Grep` line numbers or, for a whole file,
from a `Grep` count of its lines; if a call is refused for size, re-issue it with half the limit.
Nothing is ever staged or committed — this command writes `release_notes.md` and nothing else.

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
    - **Yes**: hold its full extent — from its `###` heading line to the line before the next
      heading of any depth (or end of file) — for Step 6's replacement (row 8). Skip the rest of
      this step.
  - **None marked for the target**: continue below.
- When no section is marked for the target, find every `###` heading **within the first section's
  range from Step 2 only** (row 7 is scoped to the first heading, unlike the marker search above)
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
  - **None**: there is nothing standing in the way of a fresh section (ladder row 9). Continue to
    Step 5.

### Step 5 — Judge the breaking-change items (ADR 0078 KC4)

Items are **judged from prose**, never from a diff or from any prior `release_notes.md` content —
this command has no catalogue of its own to defer to; it is the one that produces one.

- **Resolve `.adr-list`.** Read `specs/{dir}/.adr-list` (if present and non-empty) one entry per
  line. For each entry, apply FR-16 row 7's resolution rule with `Glob` of `docs/adr/`:
  - The entry is a filename (optionally prefixed `docs/adr/`): it resolves when exactly that file
    exists in `docs/adr/`.
  - Otherwise (a bare number or anything else that isn't a filename): `Glob` for
    `docs/adr/{entry}-*.md`. Zero matches — unresolved, contributes no items, held for the closing
    message as `{entry} — ADR file not found in docs/adr/.` More than one match — unresolved,
    contributes no items, held as `{entry} — ambiguous ADR number, matches: {filenames}; .adr-list
    should name the full filename instead.` Exactly one match — resolved.
- **Read each resolved ADR's Consequences.** For each resolved file, find its `## ` heading lines
  and fence-delimiter lines with one `Grep`, discarding any heading between an opening and closing
  fence (the same rule Step 2 applies to `release_notes.md`). Read from the `## Consequences`
  heading to the next `## ` heading (or end of file). Judge one breaking-change item per
  *Consequences* bullet that is **consumer-affecting** — changes an existing public behaviour or
  interface a consumer outside the ADR's own module depends on — never a bullet the ADR's own text
  marks as internal-only or non-consumer-affecting. A bullet naming more than one related change to
  one member is one item; a bullet the ADR gives its own line for is a separate item — follow the
  ADR's own bullet boundaries rather than re-partitioning them.
- **Read `requirements.md`.** When `specs/{dir}/requirements.md` exists, read it in full and judge
  any further breaking-change item its prose states that is not already covered by a resolved ADR's
  Consequences bullet — this is the **only** source when `.adr-list` is missing, empty, or every
  entry is unresolved.
- **Word each item** as one bullet, at most 40 words: a one-sentence statement of what breaks, its
  classification set in italics and parentheses — one or more of *source*, *binary*, *behavioural*,
  *compatibility* (e.g. `*(source and binary)*`) — and the migration in one sentence.
- **If no item is found** — no resolved `.adr-list` entry's Consequences named a consumer-affecting
  break, and `requirements.md` named none or is absent — the list is the single line
  `No breaking changes.` When, additionally, `.adr-list` had entries but **none** resolved and there
  is no `requirements.md`, note for the closing message that the list was derived from no source,
  and name every unresolved entry.
- Hold the finished item list (or `No breaking changes.`) for Step 6.

### Step 6 — Write the section and apply the one exact-match `Edit` (ladder rows 8, 9)

Build the section text:

- **Heading.** `### {title} (spec {NNNN}{, #{issue}})` — the issue part appears only when
  `specs/{dir}/.issue-number` exists and is non-empty (its trimmed content is `{issue}`).
  - **Replacing** (Step 4 held a marked section): keep the **existing** title — the held heading's
    text up to (not including) its first literal ` (spec `, or the whole heading text when it has
    none.
  - **Inserting** (row 9, nothing was held): the title is a short plain-language name for the
    change, written from `requirements.md`'s problem statement (or, when `requirements.md` is
    absent, from `tasks.md`'s own subject matter).
- **Marker line**, the very next line: `<!-- spec: {dir} -->`.
- **Summary paragraph**: one short paragraph for a user of the library, written from the problem
  statement (or `tasks.md`), never copied verbatim from an ADR's implementation detail.
- **`#### Breaking changes`**, followed by Step 5's held item list — one top-level `- ` bullet per
  item — or `No breaking changes.`
- No further `####` subsection is added; KC4's free-text subsections are for a person to add by
  hand and this command never writes one.

Apply the write with **one** `Edit` on `release_notes.md`:

- **Replacing** (row 8): the `old_string` is the held section's full extent from Step 4 (its
  `###` heading line through the line before the next heading), read first with `Read` so the
  `Edit` has content to match against; the `new_string` is the section text above. Every byte
  outside that span is therefore left untouched.
- **Inserting** (row 9): the `old_string` is the first `##` heading's own line, read first with
  `Read`; the `new_string` is that same line, a blank line, then the section text above — so the
  new section lands directly under the first `##` heading, before whatever followed it.
- If the `Edit` is refused because `old_string` is not unique in the file, stop. Do not retry with
  a different anchor. Print exactly:
  `release_notes.md's {replacement anchor|insertion anchor} was not unique — the edit was not
  applied. release_notes.md is unchanged.`

**Closing message.** After a successful `Edit`, print that `release_notes.md` gained or replaced the
section for `{dir}`, then name every `.adr-list` entry Step 5 could not resolve, using its own
wording, or say every entry resolved when none is left unresolved.
