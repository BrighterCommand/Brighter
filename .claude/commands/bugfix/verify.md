---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(dotnet:*), Bash(git:*), Bash(gh:*), Read, Write, Edit, Glob, Grep, AskUserQuestion
description: Run the suite to confirm the fix holds and no regressions; prepare commit/PR
argument-hint: (no arguments — operates on the active bug)
---

## Context

Current bug: !`cat bugfixes/.current-bug 2>/dev/null || echo "No active bug"`
Issue: !`cat bugfixes/$(cat bugfixes/.current-bug 2>/dev/null)/.issue-number 2>/dev/null || echo "none"`

## Your Task

**Workflow**: Issue → Triage → Confirm (✋ gate) → Test-first (✋ gate) → Fix → **Verify**

Final step: prove the fix holds and broke nothing, then capture the confirmed root cause in the
commit/PR.

### Step 1: Load State

1. Read `bugfixes/.current-bug`. If absent, tell the user to run `/bugfix:triage` and stop.
2. Read `bugfixes/{current-bug}/bugfix.md` — note the **Regression Test** path(s) and the
   **Confirmed Root Cause** (for the commit/PR body).

### Step 2: Run the Suite

1. Run the regression test(s) and confirm they pass:
   `dotnet test {test-project} --filter "FullyQualifiedName~When_{...}"`
2. Run the broader suite for the touched project(s) to check for regressions:
   `dotnet test {test-project}`
   (For transport/integration tests that need infrastructure, use `/test-infra:run-tests` or note
   that the run requires a container runtime.)
3. **Report results faithfully.** If anything fails, say so with the output and stop — do not
   declare the bug fixed. The regression test must stay (never delete or `[Skip]` it).

### Step 3: Mark Verified

Use `Edit` to set Status to `Verified` in `bugfixes/{current-bug}/bugfix.md`.

### Step 4: Suggest Commit and PR (capture root cause, link, don't auto-close)

Propose a `fix:` commit and PR that **capture the confirmed root cause** and **link** the issue so
merging closes it — but do **not** close the issue from this command.

Suggested commit message:
```
fix: {one-line summary of the behavioural fix}

Root cause: {confirmed root cause, 1–2 lines}
{if scope widened: also fixes {additional defect} surfaced during confirmation}

Fixes #{issue-number}

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

If structural cleanup was done via `/tidy-first`, there will already be a separate `refactor:`
commit — keep them distinct.

Suggested PR body should include: the symptom, the **confirmed root cause** and its evidence, the
scope (including any additional defects), and `Fixes #{issue-number}`.

Ask the user (via `AskUserQuestion`) whether to commit / open the PR now, or review first. Do not
push or open a PR without the user's go-ahead.

### Step 5: Done

The bug is verified. Closure happens when the PR merges (via `Fixes #N`) — the command never
closes the issue directly.
