---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Read, Glob
description: Switch the active bugfix
argument-hint: <bug-slug>
---

## Available Bugfixes

!`ls -d bugfixes/*/ 2>/dev/null | sort`

## Your Task

Switch the active bug to: $ARGUMENTS

1. Verify `bugfixes/$ARGUMENTS/` exists. If not, list the available bugs and stop.
2. Update the active bug: `echo $ARGUMENTS > bugfixes/.current-bug`
3. Read `bugfixes/$ARGUMENTS/bugfix.md` and show its current phase and recommended next command
   (see `/bugfix:status` for the phase derivation).

If no argument is provided, list all available bugs.
