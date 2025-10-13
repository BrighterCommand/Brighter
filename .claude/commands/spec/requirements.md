---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Create or review requirements specification
---

## Context

Current spec: !`cat specs/.current-spec 2>/dev/null || echo "No active spec"`

## Your Task

For the current active specification:

1. Check if requirements.md exists
2. If not, create a comprehensive requirements.md with:
   - Feature overview
   - User stories with acceptance criteria
   - Functional requirements (P0, P1, P2)
   - Non-functional requirements
   - Constraints and assumptions
   - Out of scope items
   - Success metrics
3. If it exists, display current content and suggest improvements
4. Remind user to use `docs/spec:approve requirements` when ready

Use the Write tool to create/update the requirements.md file.