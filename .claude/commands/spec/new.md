---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Create a new feature specification
argument-hint: <feature-name>
---

## Current Spec Status

!`ls -la specs/ 2>/dev/null | grep "^d" | wc -l | xargs -I {} echo "Total specs: {}"`

## Your Task

Create a new specification directory for the feature: $ARGUMENTS

1. Determine the next ID number (format: 001, 002, etc.)
2. Create directory: `specs/[ID]-$ARGUMENTS/`
3. Update `specs/.current-spec` with the new spec directory name ([ID]-$ARGUMENTS)
4. Create a README.md in the new directory with:
   - Feature name
   - Creation date
   - Initial status checklist
5. Inform the user about next steps

Use the Bash tool to create directories and files as needed.