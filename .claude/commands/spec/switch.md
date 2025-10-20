---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Switch to a different specification
argument-hint: <spec-id>
---

## Available Specifications

!`ls -d specs/*/ 2>/dev/null | sort`

## Your Task

Switch the active specification to: $ARGUMENTS

1. Verify the docs/spec directory exists
2. Update specs/.current-spec with the new spec directory name ([ID]-$ARGUMENTS)
3. Show the status of the newly active spec
4. Display next recommended action

If no argument provided, list all available specs.