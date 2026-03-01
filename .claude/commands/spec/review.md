---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Read, Write, Glob
description: Review current specification phase
argument-hint: [requirements|design [adr-number]|tasks]
---

## Current Spec Status

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code

## Your Task

First, read `specs/.current-spec` to determine the active specification directory.

Parse $ARGUMENTS to determine what to review:
- If empty: Auto-detect current phase (first unapproved phase)
- If "requirements": Review requirements.md
- If "design": Review all ADRs or specific ADR
- If "design [adr-number]": Review specific ADR only
- If "tasks": Review tasks.md

### Step 1: Determine Current Phase

Check approval markers in the spec directory:
- `.requirements-approved` exists? → Requirements approved
- `.design-approved` exists? → Design approved
- `.tasks-approved` exists? → Tasks approved

If no argument provided, review the first unapproved phase:
- No requirements approval → Review requirements
- No design approval → Review design (all ADRs)
- No tasks approval → Review tasks

### Step 2: Review Requirements

If reviewing requirements:
1. Read `specs/{current-spec}/requirements.md`
2. Display the content
3. Check if linked issue exists (check `.issue-number` file)
4. Provide review checklist:
   - [ ] Problem statement is clear and includes user story
   - [ ] Proposed solution describes user-facing behavior
   - [ ] Functional requirements are listed
   - [ ] Non-functional requirements are specified
   - [ ] Constraints and assumptions are documented
   - [ ] Out of scope items are explicit
   - [ ] Acceptance criteria are measurable
5. Remind: Use `/spec:approve requirements` when ready

### Step 3: Review Design (ADRs)

If reviewing design:
1. Read `specs/{current-spec}/.adr-list` to find all ADRs
2. If specific ADR number provided, review only that one
3. If no ADR specified, review all ADRs in the list
4. For each ADR:
   - Read from `docs/adr/{adr-filename}`
   - Display current Status (Proposed/Accepted)
   - Display key sections: Context, Decision, Consequences
5. Provide review checklist for each ADR:
   - [ ] Context clearly describes the specific architectural problem
   - [ ] Decision is explicit and detailed
   - [ ] Consequences (positive and negative) are documented
   - [ ] Alternatives considered are listed with rationale
   - [ ] References back to requirements
   - [ ] Uses diagrams where appropriate
   - [ ] Focuses on one architectural decision
6. Show summary:
   - Total ADRs for this spec: {count}
   - Approved: {count with Status: Accepted}
   - Proposed: {count with Status: Proposed}
7. Remind:
   - Use `/spec:approve design [adr-number]` to approve specific ADR
   - Use `/spec:approve design` to approve all ADRs
   - Use `/spec:design [focus-area]` to add more ADRs if needed

### Step 4: Review Tasks

If reviewing tasks:
1. Read `specs/{current-spec}/tasks.md`
2. Display the content
3. Provide review checklist:
   - [ ] Tasks are specific and actionable
   - [ ] Tasks follow TDD approach (tests before code)
   - [ ] Task breakdown enables incremental development
   - [ ] Dependencies are identified
   - [ ] Risk mitigation tasks are included
4. Remind: Use `/spec:approve tasks` when ready

### Step 5: Summary

Display overall spec status:
- Spec directory: `specs/{current-spec}`
- Requirements: ✓ Approved / ⏳ In Progress
- Design: {X} ADRs ({Y} approved, {Z} proposed)
- Tasks: ✓ Approved / ⏳ In Progress / ⏹ Not Started

Use Read tool to display documents and Bash to check approval markers.