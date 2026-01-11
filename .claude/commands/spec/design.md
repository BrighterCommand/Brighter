---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(git:*), Read, Write, Glob
description: Create technical design specification (ADR)
argument-hint: [adr-focus-area]
---

## Context

Architecture Decision Records: [Described by Michael Nygard](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions)
ADR directory: `docs/adr/`

**Workflow**: Issue → Requirements → **ADR(s)** → Tasks → Tests → Code

**Note**: You can create multiple ADRs for the same requirement. Each ADR should focus on a single architectural decision.

## Your Task

### Step 1: Verify Requirements Approved

1. Read `specs/.current-spec` to determine the active specification directory
2. Check for `.requirements-approved` file in the spec directory
3. If not approved:
   - Inform user to complete requirements first using `/spec:approve requirements`
   - Exit

### Step 2: Check Existing ADRs for this Spec

1. Check if `specs/{current-spec}/.adr-list` exists
2. If it exists, read it to see which ADRs are already associated with this spec
3. Show user the existing ADRs to avoid duplication

### Step 3: Determine ADR Number and Focus

1. Check existing ADRs: `ls docs/adr/ | grep -E "^[0-9]{4}-" | sort | tail -1`
2. Calculate next number (format: 0001, 0002, 0003, etc.)
   - Example: if last is `0042-some-feature.md`, next is `0043`
3. Determine the ADR focus:
   - If $ARGUMENTS provided: Use that as the specific focus area (e.g., "message-serialization", "error-handling")
   - If not provided: Ask user what aspect of the requirement this ADR addresses
4. Create ADR filename: `docs/adr/{NNNN}-{focus-area}.md`
   - Use kebab-case for focus area
   - Example: `docs/adr/0043-kafka-dlq-message-serialization.md`
5. Add to tracking: `echo "0043-kafka-dlq-message-serialization.md" >> specs/{current-spec}/.adr-list`

### Step 4: Create ADR Document

Create `docs/adr/{NNNN}-{focus-area}.md` with the following template:

```markdown
# {Number}. {Title}

Date: {YYYY-MM-DD}

## Status

Proposed

## Context

{Describe the specific architectural problem this ADR addresses}

**Parent Requirement**: [specs/{spec-dir}/requirements.md](../../specs/{spec-dir}/requirements.md)

**Scope**: This ADR focuses specifically on {the architectural decision area}. {If there are other ADRs for this requirement, mention them here}

{Describe the problem and context}:
- What specific aspect of the requirement needs an architectural decision?
- What are the forces at play (technical, political, social, project)?
- Why is this decision important?
- What constraints exist?

## Decision

{Describe the specific architectural decision that was made}

- What approach are we taking for this aspect?
- What are the key technical choices?
- What patterns or practices will we follow?

### Architecture Overview

{Describe the architecture for this specific decision}
{Use ASCII art or mermaid diagrams where helpful}

### Key Components

{List and describe the main components affected by this decision}

### Technology Choices

{Document specific technology/library choices for this aspect and why}

### Implementation Approach

{Outline how this specific aspect will be implemented}

## Consequences

### Positive

- {What becomes easier or better with this decision?}
- {Benefits specific to this architectural choice}

### Negative

- {What becomes harder or more complex?}
- {What tradeoffs are we making?}
- {Additional complexity introduced}

### Risks and Mitigations

- {Identify technical risks specific to this decision}
- {Describe how we'll mitigate them}

## Alternatives Considered

{Describe other approaches that were considered for this specific aspect and why they were not chosen}

## References

- Requirements: [specs/{spec-dir}/requirements.md](../../specs/{spec-dir}/requirements.md)
- Related ADRs: {List related ADRs, especially others for the same requirement}
- External references: {Links to relevant documentation, articles, etc.}
```

### Step 5: Additional Guidance

When creating the ADR:
- Focus on **one specific architectural decision** - keep it focused
- Focus on the **why** of decisions, not just the **what**
- Use ASCII art or mermaid diagrams where helpful for architecture
- Consider:
  - Data model and schema changes
  - API design (public interfaces)
  - Security implications
  - Performance considerations
  - Testing strategy
  - Deployment considerations
  - Backward compatibility

### Step 6: Next Steps

1. Remind user to:
   - Review and complete the ADR with technical details
   - Commit the ADR: `git add docs/adr/{NNNN}-{focus-area}.md && git commit -m "docs: add ADR for {focus-area}"`
   - The first ADR should typically be the first commit on the feature branch
   - Can create/update draft PR with ADR for early feedback
2. Multiple ADRs:
   - If requirement needs more architectural decisions, run `/spec:design [another-focus-area]` again
   - Each ADR should address a distinct architectural concern
3. When all ADRs are complete: `/spec:approve design` to proceed to tasks phase

Use the Write tool to create the ADR document in `docs/adr/`.