---
description: Create a new feature specification
---

## Current Spec Status

Check existing specs: `ls -la specs/ 2>/dev/null | grep "^d" | wc -l`

## Your Task

Create a new specification directory for the feature: $ARGUMENTS

1. Determine the next ID number (format: 0001, 0002, etc.)
2. Create directory: `specs/[ID]-$ARGUMENTS/`
3. Update `specs/.current-spec` with the new spec directory name ([ID]-$ARGUMENTS)
4. Create a README.md in the new directory with:
   - Feature name
   - Creation date
   - Initial status checklist
5. Inform the user about next steps

Use the Bash tool to create directories and files as needed.
