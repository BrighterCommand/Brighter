---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(ls:*), Bash(echo:*), Read, Glob
description: Show all bugfixes and their phase
argument-hint: (no arguments)
---

## Gather Status Information

Bug tracking directory: bugfixes/

**Workflow**: Issue → Triage → Confirm (✋ gate) → Test-first (✋ gate) → Fix → Verify

## Your Task

### Step 1: Gather Information

1. Read `bugfixes/.current-bug` to determine the active bug (if any).
2. List all bug directories: `ls -d bugfixes/*/ 2>/dev/null`. If none, report "No bugfixes yet —
   start one with `/bugfix:triage [issue-number | description]`" and stop.
3. For each bug directory, check:
   - `.issue-number` (linked GitHub issue, if any)
   - `.confirm-approved` (the one approval marker)
   - `bugfix.md` — read the `**Status**` line and which sections are filled

### Step 2: Derive Each Phase

Infer the phase for each bug from its `bugfix.md` + marker (lightweight — only one marker exists):

- **Triaged** — `bugfix.md` exists with a Root-Cause Hypothesis, no `.confirm-approved`
- **Confirmed** — `.confirm-approved` exists (Confirmed Root Cause + Evidence filled)
- **Tested** — Regression Test section names a test
- **Fixed** — Fix section names changed files
- **Verified** — Status line reads `Verified`

### Step 3: Present the Report

```
Bugfix Status Report
====================

Active bug: bugfixes/{current-bug}/ (marked with *)

* bugfixes/0001-asb-sessionid-case/
  Issue: #4054
  Phase: Confirmed ✓   (Confirm gate: ✓ approved)
  Next: /bugfix:test

  bugfixes/0002-...
  Issue: (none)
  Phase: Triaged
  Next: /bugfix:confirm
```

### Step 4: Legend and Next Action

```
Phases:  Triaged → Confirmed → Tested → Fixed → Verified
Gate:    ✋ Confirm gate (.confirm-approved) must pass before Test-first
* Active bug
```

Finish with the single recommended next command for the active bug.
