---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(mkdir:*), Bash(echo:*), Bash(gh:*), Bash(git:*), Read, Write, Glob
description: Create or review requirements specification
argument-hint: [issue-number]
---

## Context

Current spec: !`cat specs/.current-spec 2>/dev/null || echo "No active spec"`
Current branch: !`git branch --show-current`

## Your Task

**Workflow**: Issue → Requirements → ADR → Tasks → Tests → Code

### Step 1: Setup Spec Directory

1. Create `specs/` directory if it doesn't exist: `mkdir -p specs`
2. If `.current-spec` doesn't exist, determine next spec ID and create spec directory
   - Check existing specs: `ls specs/` to find highest number
   - Format: `specs/NNNN-{feature-name}/` (e.g., `specs/0001-kafka-dlq/`)
   - Update `specs/.current-spec` with the new spec directory name

### Step 2: Pull Existing Issue (if provided)

If $ARGUMENTS provided (issue number):
1. Use `gh issue view $ARGUMENTS --json number,title,body` to pull the existing issue
2. Store issue number in spec directory: `echo $ARGUMENTS > specs/{current-spec}/.issue-number`
3. Use the issue content as the basis for requirements.md

### Step 3: Create/Update Requirements

Create or update `requirements.md` in the current spec directory using this template:

```markdown
# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #ISSUE_NUMBER (if applicable)

## Problem Statement

A clear and concise description of what the problem is:
- Frame it as a user story: "As a [user type] I would like [capability], so that [benefit]..."

## Proposed Solution

Describe the solution you'd like from a user perspective (not implementation details).

## Requirements

### Functional Requirements
- List the key functional requirements

### Non-functional Requirements
- Performance requirements
- Scalability requirements
- Security requirements
- Other quality attributes

### Constraints and Assumptions
- Technical constraints
- Business constraints
- Assumptions being made

### Out of Scope
- Explicitly list what is NOT included in this feature

## Acceptance Criteria

How we'll know this is working correctly:
- Success metrics
- Testing approach
- Definition of done

## Additional Context
Add any other context or screenshots about the feature request here.
```

### Step 4: Determine Next ADR Number

1. Check existing ADRs: `ls docs/adr/ | grep -E "^[0-9]+" | sort | tail -1`
2. Calculate next number (e.g., if last is 0042, next is 0043)
3. Inform user: "Next ADR will be: `docs/adr/{NNNN}-{feature-name}.md`"

### Step 5: Branch Management

1. Check current branch with `git branch --show-current`
2. If on `master` or `main`:
   - Offer to create feature branch: `git checkout -b feature/{issue-number}-{feature-name}` or `feature/{feature-name}`
3. If on another branch:
   - Inform user: "You're on branch `{branch-name}`. You can continue using this branch or create a new one."

### Step 6: Update GitHub Issue (Optional)

If issue number was provided:
1. Ask user if they want to add a comment to the GitHub issue with a link to requirements
2. If yes, use: `gh issue comment $ISSUE_NUMBER --body "Requirements documented in specs/{spec-dir}/requirements.md"`

### Step 7: Next Steps

Remind user:
- Review requirements.md
- Use `/spec:approve requirements` when ready to proceed to design phase
- Next step: Create ADR in `docs/adr/` with technical design decisions

Use Write tool to create/update requirements.md. Use Bash for git/gh operations.
