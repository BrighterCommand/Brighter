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

**This command is under active construction (spec 0037).** Only Step 1 is implemented below; Steps 2
onward — the precondition gate, the ledger read, evidence reads, the synthesis stages and the write —
land in later tasks and are not yet part of this command. Follow Step 1 exactly as written, and when
it resolves successfully, **stop there**: print the resolution and do nothing else. Do not improvise
any later step, and do not create or modify `show-me.md`, the fact ledger, or any other file.

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

**On success**, print `Resolved target: specs/{dir}/.` and stop — per the note above, do not go
further.
