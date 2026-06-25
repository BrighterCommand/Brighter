---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(dotnet:*), Bash(git:*), Read, Write, Edit, Glob, Grep, AskUserQuestion
description: Make the regression test green with the minimal change scoped to the confirmed cause
argument-hint: (no arguments — operates on the active bug)
---

## Context

Current bug: !`cat bugfixes/.current-bug 2>/dev/null || echo "No active bug"`
Confirm gate: !`test -f bugfixes/$(cat bugfixes/.current-bug 2>/dev/null)/.confirm-approved && echo "✓ approved" || echo "✗ NOT approved — run /bugfix:confirm first"`

## Your Task

**Workflow**: Issue → Triage → Confirm (✋ gate) → Test-first (✋ gate) → **Fix** → Verify

Make the red regression test pass with the **minimum** change required by the confirmed cause —
nothing more. This is the GREEN phase from `/test-first`, scoped by the confirmed diagnosis.

> **Recommended model: `sonnet`.** This is implementation work in the main agent (writes source,
> runs `dotnet`). See `.claude/commands/bugfix/README.md` → "Sub-agents & model policy".

### Step 1: Enforce Prerequisites

1. Read `bugfixes/.current-bug`. If absent, tell the user to run `/bugfix:triage` and stop.
2. Verify `bugfixes/{current-bug}/.confirm-approved` exists. If not, stop and point to
   `/bugfix:confirm`.
3. Read `bugfixes/{current-bug}/bugfix.md`. Confirm the **Regression Test** section names a test
   (a red test must exist). If not, stop and point to `/bugfix:test`.

### Step 2: Decide — Straight Fix or Tidy-First

Read the suspect code (Confirmed Root Cause + Suspected Location).

- **If the fix is a clean, localized change**: proceed to Step 3 (a single `fix:` commit).
- **If the code needs structural cleanup *before* the fix is safe** (e.g. a large method must be
  broken up, duplicated logic consolidated): do the cleanup via **`/tidy-first`** so the
  structural change lands in its own `refactor:` commit first, *then* the behavioural `fix:`
  commit. Never mix structural and behavioural changes in one commit
  (`.agent_instructions/code_style.md`).

### Step 3: GREEN — Minimal Fix

1. Write the **minimum** code to make the regression test(s) pass. Scope is bounded by the
   **Confirmed Root Cause** and the **Scope Notes** — no speculative edits, no unrequested
   "improvements", no default changes (CLAUDE.md → "Change Scope").
2. Follow `.agent_instructions/code_style.md` (naming, RDD, nullable, XML docs on public members,
   MIT header on new files) and `.agent_instructions/documentation.md`.
3. If the Scope Notes listed additional defects, fix those too — they share the confirmed cause
   and each has its own red test from `/bugfix:test`.
4. **Cross-backend parity**: if the cause touches one backend (MSSQL/PostgreSQL/MySQL/SQLite/
   Spanner/Dynamo) and the Scope Notes flagged parity, apply the equivalent fix to the others.

### Step 4: Run the Targeted Test

Run the regression test(s):
`dotnet test {test-project} --filter "FullyQualifiedName~When_{...}"`
Confirm they now pass (green). Report the result faithfully — if still red, iterate; do not
declare success.

### Step 5: Record the Fix

Use `Edit` to fill the **Fix** section of `bugfixes/{current-bug}/bugfix.md` with the files
changed and a one-line summary, and set Status to `Fixed`.

### Step 6: Next Steps

Remind the user to run `/bugfix:verify` to run the full suite (no regressions) before
committing/PR.
