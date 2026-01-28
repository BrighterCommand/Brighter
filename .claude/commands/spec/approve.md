---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Read, Write, Edit, Glob
description: Approve a specification phase
argument-hint: requirements|design [adr-number]|tasks
---

## Context

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code

## Your Task

First, read `specs/.current-spec` to determine the active specification directory.

Parse $ARGUMENTS to extract phase and optional ADR number:
- Format: "requirements" or "design" or "design 0043" or "tasks"
- Phase is the first word
- ADR number (if provided) is the second word

### For "requirements" Phase

1. Verify `requirements.md` exists in the spec directory
2. Create approval marker: `touch specs/{current-spec}/.requirements-approved`
3. Inform user about next steps:
   - Next: Create one or more ADRs using `/spec:design [focus-area]`
   - Reminder: First ADR should be first commit on feature branch

### For "design" Phase

1. Verify at least one ADR exists for this spec
2. Read `specs/{current-spec}/.adr-list` to find associated ADRs
3. Determine which ADRs to approve:
   - **If ADR number specified** (e.g., "design 0043"): Approve only that ADR
   - **If no ADR number**: Approve ALL ADRs in the `.adr-list`

4. For each ADR to approve:
   - Read the ADR file from `docs/adr/{adr-filename}`
   - Use Edit tool to update Status from "Proposed" to "Accepted"
   - Find the line containing "## Status" followed by "Proposed"
   - Replace "Proposed" with "Accepted"
   - Example:
     ```
     Old: ## Status\n\nProposed
     New: ## Status\n\nAccepted
     ```

5. Create approval marker: `touch specs/{current-spec}/.design-approved`
6. Show user which ADRs were approved
7. Inform user about next steps:
   - If more ADRs needed: Run `/spec:design [another-focus-area]`
   - If all ADRs complete: Proceed to `/spec:tasks` to create implementation task list
   - Reminder: Commit approved ADRs to git

### For "tasks" Phase

1. Verify `tasks.md` exists in the spec directory
2. Create approval marker: `touch specs/{current-spec}/.tasks-approved`
3. Inform user about next steps:
   - Next: Begin implementation using `/spec:implement`
   - Follow TDD: Write tests first, then code

### Invalid Phase

If phase name is not recognized, show valid options:
- `requirements` - Approve requirements specification
- `design [adr-number]` - Approve ADR(s) and update status to Accepted
- `tasks` - Approve task list

## Examples

```bash
# Approve requirements
/spec:approve requirements

# Approve all ADRs for current spec
/spec:approve design

# Approve specific ADR
/spec:approve design 0043

# Approve tasks
/spec:approve tasks
```

Use Edit tool to update ADR status, touch command for approval markers.