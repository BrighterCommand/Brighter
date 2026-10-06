---
allowed-tools: Read, Edit, Write, Glob, Bash(git config:*), Bash(date:*), Bash(ls:*), Bash(grep:*), Bash(basename:*), AskUserQuestion
description: Add or update an ADR's YAML frontmatter and keep it in sync with the body Status
argument-hint: <path> init|status <S>|supersede --by <id>|deprecate [options]
---

# Write ADR Metadata

You are adding or updating the YAML **frontmatter** block on a single Architecture Decision Record,
and keeping it in step with the body `## Status`. This is the *write* half of the ADR metadata pair
(the read half is `read_adr_metadata`).

**Source of truth**: the schema, status vocabulary, tag taxonomy, and identity rule live in
[`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md). Read it
before writing. Do **not** duplicate the tag list here — draw tags from that document so the
vocabulary cannot drift.

## Arguments

```
$ARGUMENTS
```

Parse `$ARGUMENTS` as: `<path> <operation> [options]`, where `<path>` is the ADR file (e.g.
`docs/adr/0066-widget-batching.md`) and `<operation>` is one of:

| Operation           | Effect |
|---------------------|--------|
| `init`              | Add a frontmatter block, or update the existing one in place. Default `status: Proposed`. |
| `status <S>`        | Set status to `<S>` in **both** the frontmatter and the body `## Status`. Approval is `status Accepted`. |
| `supersede --by <id>` | Mark this (older) ADR `Superseded` and record `Superseded by <id>` in the body `## Status`. |
| `deprecate`         | Mark this ADR `Deprecated` (retired, with **no** named replacement). |

`init` options (all optional except as noted): `--summary "..."` (**required** if none can be found),
`--tags "a,b,c"`, `--author "Name"` (repeatable), `--status <S>`, `--title "..."`,
`--created YYYY-MM-DD`.

If required information for the requested operation is missing and cannot be derived from the file,
ask for it with `AskUserQuestion` rather than guessing.

## Rules that govern every operation

- **Identity** — `id` is **always** the filename stem (basename without `.md`), e.g.
  `0066-widget-batching`. Never invent or renumber it. Compute it from `<path>`.
- **Status vocabulary** — `Proposed | Accepted | Deprecated | Superseded` only. Reject anything
  else. New ADRs are `Proposed`.
- **Frontmatter mirrors the body** — after any status change the frontmatter `status` and the body
  `## Status` word must be identical. Never change one without the other.
- **Idempotent** — if a frontmatter block already exists (the file starts with `---` and has a
  closing `---`), update the affected fields **in place**; never prepend a second block or duplicate
  keys.
- **Preserve prose** — when a body `## Status` line carries extra text (e.g. an amendment note),
  change only the status *word*; keep the surrounding sentence.
- Do **not** touch anything outside frontmatter and the body `## Status` section.

## Procedure

### Step 1 — Read and classify the file

Read `<path>`. Detect whether a frontmatter block is present: the file's first non-empty line is
`---` and a closing `---` follows. Note the current body `## Status` value and the `Date:` line.

### Step 2 — Perform the operation

**`init`** — build (or reconcile) the seven required fields:

- `id` — filename stem (authoritative; overwrite any wrong value).
- `title` — from `--title`, else the H1 (`# N. Title`) with the leading `N.` number stripped.
- `status` — from `--status`, else the body `## Status` word normalized to the vocabulary, else
  `Proposed`.
- `author` — from `--author` (list form, one entry per author). If absent: default to the git user
  (`git config user.name`); if that is empty, `- "Brighter Team"`.
- `created` — from `--created`, else the body `Date:` line, else today (`date +%Y-%m-%d`).
- `summary` — from `--summary`. If none and none exists in a prior block, **ask** — a summary cannot
  be guessed; it is the load-bearing field.
- `tags` — from `--tags` (comma-separated), 1–4 values, each drawn from the taxonomy in the
  conventions doc. If a needed tag is not in the taxonomy, propose adding it there rather than
  inventing an ad-hoc tag.

If a block already exists, update only the fields the arguments specify (plus `id`, which is always
reconciled to the stem); leave the rest untouched. Otherwise write a new block, as YAML delimited by
`---` fences, at the very top of the file, immediately before the `# N. Title` heading, followed by a
blank line. Emit the fields in schema order: `id, title, status, author, created, summary, tags`.
Quote `title` and `summary`; render `author` and `tags` as YAML lists.

**`status <S>`** — validate `<S>` against the vocabulary. Set the frontmatter `status` to `<S>` and
replace the body `## Status` word with `<S>`, preserving any trailing amendment prose. (Approval is
just `status Accepted`.)

**`supersede --by <id>`** — this operates on the **older** ADR being retired. Set frontmatter
`status: Superseded`. In the body `## Status` section write `Superseded` followed by a reference line
`Superseded by [<id>](<id>.md)` (link relative to `docs/adr/`). Do not modify the superseding ADR
here — the caller records the forward relationship there separately.

**`deprecate`** — set frontmatter `status: Deprecated` and the body `## Status` to `Deprecated`. Use
this only when the decision is retired with **no** specific replacement; if a replacement exists, use
`supersede` instead.

### Step 3 — Validate before finishing

Confirm, and report, that:

1. All seven keys are present: `id, title, status, author, created, summary, tags`.
2. `id` equals the filename stem.
3. `status` ∈ `{Proposed, Accepted, Deprecated, Superseded}`.
4. `created` is a valid `YYYY-MM-DD` date.
5. Every tag is in the controlled taxonomy; there are 1–4 of them.
6. The frontmatter `status` word matches the body `## Status` word.

If any check fails, fix it (or ask) — do not leave the file in an inconsistent state.

### Step 4 — Report

Tell the caller what changed: the file, the operation, the resulting `status`, and (for supersession)
the recorded reference. Keep it to a couple of lines — this skill is usually invoked *by* another
command (`/adr`, `/spec:design`, `/spec:approve`), so the output feeds that caller.

> The derived `docs/adr/index.md` is regenerated separately from frontmatter; this skill does not
> maintain it.

## Examples

```bash
# New ADR: stamp Proposed frontmatter onto a freshly written body
write_adr_metadata docs/adr/0066-widget-batching.md init \
  --summary "Batches outbound widget events into a single envelope to cut broker round-trips, opt-in per publication." \
  --tags "messaging,bulk-messaging,performance"

# Approve: flip Proposed -> Accepted in frontmatter and body
write_adr_metadata docs/adr/0066-widget-batching.md status Accepted

# Supersession: the new ADR 0070 replaces the older 0066
write_adr_metadata docs/adr/0066-widget-batching.md supersede --by 0070-widget-streaming

# Retire with no replacement
write_adr_metadata docs/adr/0031-legacy-thing.md deprecate
```
