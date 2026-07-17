---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(mkdir:*), Bash(echo:*), Bash(gh:*), Bash(git:*), Read, Write, Glob, Agent, AskUserQuestion
description: Create or review requirements specification
argument-hint: [issue-number]
---

## Context

Current spec: !`cat specs/.current-spec 2>/dev/null || echo "No active spec"`
Current branch: !`git branch --show-current`

## Your Task

**Workflow**: Issue → **Requirements** → ADR → Tasks → Tests → Code

**Sub-agent**: Drafting the requirements body is delegated to a sub-agent
(`subagent_type: "Plan"`, **`model: "opus"`**). Requirements drafting is read-only by nature
(it returns text), so `Plan` fits — and using it keeps the non-implementation commands
uniform on `Plan` and removes `AskUserQuestion` from the sub-agent's tool set, so it cannot
prompt the user. The sub-agent turns the (clarified) issue / problem description into a
complete `requirements.md` body and RETURNS it as text. Because the sub-agent is one-shot and
cannot ask anything, **all user interaction stays in the main agent**: the main agent clarifies
any ambiguous inputs with the user via `AskUserQuestion` (Step 2.5) *before* launching, and
owns ALL of the bookkeeping — spec-directory setup, `gh`, branch management, writing the file,
and the issue comment. See `.claude/commands/spec/README.md` → "Sub-agents & model policy".

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
3. The issue title + body become the source material for the sub-agent.

If no issue number is provided, take the problem description from the **conversation context**.

If neither an issue number nor a usable problem description from the conversation is
available, **stop** and ask the user (via `AskUserQuestion`) to either supply an issue number
or describe the problem — do NOT launch the sub-agent with empty inputs.

If a `requirements.md` already exists in the spec directory, read it and pass it to the
sub-agent as the current draft to refine (rather than starting from scratch).

### Step 2.5: Clarify Ambiguities Before Launch (MAIN agent)

Requirements gathering is where clarification matters most, and the `Plan` sub-agent is
one-shot — it has **no `AskUserQuestion`** and cannot ask the user anything once launched.
So the **main agent** owns all user interaction: before launching the sub-agent, review the
issue / conversation inputs for ambiguity or open decisions (unstated scope, undefined terms,
conflicting goals, missing acceptance criteria) and resolve them with the user via
`AskUserQuestion`. Then launch the sub-agent with the **clarified** inputs folded in, so it
can draft from a complete, unambiguous brief. Do not defer clarification to the sub-agent or
to a later phase.

### Step 3: Launch Sub-Agent to Draft Requirements

Launch an `Agent` with `subagent_type: "Plan"` and **`model: "opus"`**. The
prompt MUST include:

1. The issue title + body (or the user-provided problem description), **plus any answers the
   user gave during Step 2.5 clarification**, and the existing `requirements.md` text if one
   is being refined.
2. The requirements template below.
3. The quality bar below — the sub-agent should draft requirements that would PASS an
   adversarial `/spec:review requirements`.
4. An explicit instruction: **RETURN the complete `requirements.md` body as markdown text.
   Do NOT write any file. Do NOT ask the user any questions — draft from the inputs provided.**
   (`Plan` has no `AskUserQuestion`, so the sub-agent *cannot* prompt the user; this
   instruction is belt-and-braces and keeps the conversation non-interactive.)

#### Requirements Template (the sub-agent fills this in)

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

#### Quality bar (include in the sub-agent prompt)

Draft requirements that would survive a skeptical reviewer:
- **Testability**: every functional requirement has at least one concrete example with
  specific inputs/outputs; each acceptance criterion can become a test assertion; boundary
  conditions and error scenarios are explicit.
- **Completeness**: problem statement as a user story; numbered functional requirements;
  non-functional requirements; constraints/assumptions; an explicit out-of-scope section.
- **Unambiguity**: consistent, defined terminology; no vague phrases ("handle gracefully",
  "supports multiple" without a number, "works as expected"); two developers could not
  reasonably implement a requirement differently.
- **Boundedness**: scope is clearly bounded; each FR has clear start/end conditions; no
  contradictions between sections.
- **Acceptance criteria**: every FR maps to at least one AC, written in Given/When/Then or
  an equivalent testable format.

The sub-agent may number FRs (FR-1, FR-2, …) and ACs so later phases can cross-reference them.

### Step 4: Validate and Write Requirements

After the sub-agent returns:

1. **Validate** before writing: all template sections present; FRs numbered; every FR has
   at least one AC; no obvious vague phrases. If weak, ask the sub-agent to revise (or fix
   it yourself) before writing.
2. **Write** the validated body to `specs/{current-spec}/requirements.md` using the Write tool.

### Step 5: Determine Next ADR Number

1. Check existing ADRs: `ls docs/adr/ | grep -E "^[0-9]+" | sort | tail -1`
2. Calculate next number (e.g., if last is 0042, next is 0043)
3. Inform user: "Next ADR will be: `docs/adr/{NNNN}-{feature-name}.md`"

### Step 6: Branch Management

1. Check current branch with `git branch --show-current`
2. If on `master` or `main`:
   - Offer to create feature branch: `git checkout -b feature/{issue-number}-{feature-name}` or `feature/{feature-name}`
3. If on another branch:
   - Inform user: "You're on branch `{branch-name}`. You can continue using this branch or create a new one."

### Step 7: Update GitHub Issue (Optional)

If issue number was provided:
1. Ask user if they want to add a comment to the GitHub issue with a link to requirements
2. If yes, use: `gh issue comment $ISSUE_NUMBER --body "Requirements documented in specs/{spec-dir}/requirements.md"`

### Step 8: Next Steps

Remind user:
- Review requirements.md
- Run `/spec:review requirements` for an adversarial review, then `/spec:approve requirements` when ready to proceed to design phase
- Next step: Create ADR in `docs/adr/` with technical design decisions
