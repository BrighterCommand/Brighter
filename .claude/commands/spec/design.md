---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(git:*), Bash(awk:*), Bash(date:*), Bash(basename:*), Read, Write, Edit, Glob, Agent, AskUserQuestion, Skill
description: Create technical design specification (ADR)
argument-hint: [adr-focus-area]
---

## Context

Architecture Decision Records: [Described by Michael Nygard](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions)
ADR directory: `docs/adr/`

**Workflow**: Issue → Requirements → **ADR(s)** → Tasks → Tests → Code

**Note**: You can create multiple ADRs for the same requirement. Each ADR should focus on a single architectural decision.

**Frontmatter**: Every ADR carries a YAML frontmatter block (`id`, `title`, `status`, `author`,
`created`, `summary`, `tags`) — see [`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md).
This command reads existing ADRs' frontmatter to surface prior art (Step 4) and stamps the new ADR's
frontmatter on write (Step 7) via the `read_adr_metadata` / `write_adr_metadata` skills.

**Sub-agent**: The codebase-grounded drafting of the ADR is delegated to a sub-agent
(`subagent_type: "Plan"`, **`model: "opus"`**). `Plan` has no `Write`/`Edit`/`NotebookEdit`,
which makes it much harder to accidentally write the file (the prompt still forbids writing
via `Bash`), while still allowing Read/Glob/Grep to verify references. The sub-agent reads the inputs, verifies references
against the real codebase, and RETURNS the ADR body as text — plus a proposed frontmatter `summary`
and `tags`. The main agent owns numbering, file writing, frontmatter stamping, and `.adr-list`
bookkeeping. See `.claude/commands/spec/README.md` → "Sub-agents & model policy".

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
   - The number is an **ordering hint, not an identity** — the `id`/slug is the identity, and a
     number collision with a concurrent branch is acceptable (see `adr_frontmatter.md`).
3. Determine the ADR focus:
   - If $ARGUMENTS provided: Use that as the specific focus area (e.g., "message-serialization", "error-handling")
   - If not provided: Ask user what aspect of the requirement this ADR addresses
4. Create ADR filename: `docs/adr/{NNNN}-{focus-area}.md`
   - Use kebab-case for focus area
   - Example: `docs/adr/0043-kafka-dlq-message-serialization.md`

   **Do NOT write the ADR file or update `.adr-list` yet** — that happens in Step 7 after
   the sub-agent returns and you have validated its output.

   > **Note**: the number is computed here but only *reserved* (written to `.adr-list`) in
   > Step 7 after the sub-agent returns. Two `/spec:design` runs started concurrently could
   > therefore pick the same number. This is a theoretical race for a single-user dev tool
   > and is accepted; if you ever run designs in parallel, double-check the number in Step 7
   > against `docs/adr/` before writing.

### Step 4: Surface Prior-Art ADRs (before drafting)

Before drafting, find existing decisions relevant to **{focus-area}** so the new ADR reuses them,
avoids contradicting them, or deliberately supersedes them — rather than re-deciding in ignorance.

Use the `read_adr_metadata` skill (`.claude/commands/adr/read_adr_metadata.md`), passing the focus
area as the query plus any obvious tags from the taxonomy:

```
read_adr_metadata "{focus-area}" --tags "{likely-tags}"
```

It reads only frontmatter (cheap) and by default **skips `Deprecated` and `Superseded`** ADRs, so
retired decisions are not offered as live prior art. Keep the returned candidates (`id` + `title` +
`status` + `summary`) — you feed them to the drafting sub-agent in Step 6 so it can cite or
distinguish them, and you flag any that this ADR would supersede.

If no prior art is found, note that and proceed.

### Step 5: Gather Inputs for the Sub-Agent

The sub-agent starts with a clean context — it only knows what you put in its prompt.
Gather (read) the inputs it needs so you can pass their text or paths:

- `specs/{current-spec}/requirements.md` — the parent requirement the ADR must address.
- Each existing ADR listed in `specs/{current-spec}/.adr-list` (read from `docs/adr/`) —
  so the new ADR stays focused on a *distinct* decision and references siblings correctly.
- The **prior-art candidates from Step 4** (`id` + `title` + `summary`) — so the sub-agent
  references related decisions correctly and does not contradict or silently duplicate one.
- `.agent_instructions/design_principles.md` — pass the path; the sub-agent reads it itself.
- `.agent_instructions/adr_frontmatter.md` — pass the path; the sub-agent reads the tag taxonomy
  from it to propose `tags`.

If any required document is missing (e.g. no `requirements.md`), stop and tell the user to
run the appropriate command first. Do NOT launch the sub-agent with missing inputs.

### Step 6: Launch Sub-Agent to Draft the ADR

**Verify inputs with the user before launching (MAIN agent).** The `Plan` sub-agent is
one-shot and has no `AskUserQuestion` — it cannot ask the user anything once launched. So
before launching, review the gathered inputs for ambiguity or open design decisions (which
architectural direction to take, the intended scope of this single ADR, trade-off
preferences, anything under-specified in `requirements.md`) and resolve them with the user
via `AskUserQuestion`. Then launch the sub-agent with the clarified inputs folded in. All
user interaction stays in the main agent — never the sub-agent.

Launch an `Agent` with `subagent_type: "Plan"` and **`model: "opus"`**. The
prompt MUST include all of the following:

1. The full text of `requirements.md` (or its path if too large to inline).
2. The full text of each existing ADR for this spec (or their paths), and an instruction
   that the new ADR addresses a DISTINCT decision: **{focus-area}**.
3. The **prior-art candidates from Step 4** (`id` + `title` + `summary`), with an instruction to
   reference related ones under `Related ADRs`, and to state explicitly if this ADR supersedes any
   of them (so the main agent can mark the old one `Superseded` in Step 7).
4. The proposed number and filename (`{NNNN}-{focus-area}.md`) and today's date — so the
   header and `Related ADRs` references are correct.
5. The ADR template (below) — the sub-agent fills it in.
6. The "Drafting guidance" and "Grounding requirements" blocks below.
7. An explicit instruction: **RETURN the completed ADR as markdown text. Do NOT write any
   file.** Use Read/Glob/Grep to verify references; do not use Write/Edit.
8. An explicit instruction to **also return, clearly separated from the ADR body, a proposed
   frontmatter `SUMMARY:` (1–2 sentences stating WHAT was decided, so a reader can decide whether
   to open the ADR) and `TAGS:` (1–4 tags drawn from the controlled taxonomy in
   `.agent_instructions/adr_frontmatter.md` — do not invent ad-hoc tags).** The main agent uses
   these to stamp the frontmatter in Step 7.

#### ADR Template (the sub-agent fills this in)

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

#### Drafting guidance (include in the sub-agent prompt)

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

**Responsibility-Driven Design** — read `.agent_instructions/design_principles.md` and apply it:
- Check for a design focused on behavior: does the ADR think about *roles* and *responsibilities*?
- Responsibilities are "knowing", "doing", and "deciding".
- Allocate responsibilities into roles, focusing on cohesion.
- Roles are interfaces or abstract types.
- A class can implement one or more roles. If it implements multiple roles, they should be related.

#### Grounding requirements (include in the sub-agent prompt)

The ADR must be grounded in the real codebase — an ADR that references files, classes, or
namespaces that don't exist misleads everyone who reads it. Therefore:
- USE Glob and Grep to verify every file path, class name, type name, and namespace the ADR
  references actually exists. Do not guess from a name.
- Reference real existing patterns in the codebase; if you propose a new pattern, justify
  why the existing ones don't fit.
- Specify interface contracts with inputs, outputs, and error conditions.
- Document consequences honestly (both positive AND negative). A design with only positive
  consequences is suspicious.
- List genuine alternatives with real rationale for rejection (not strawmen).

### Step 7: Validate, Write the ADR, Stamp Frontmatter, and Update Tracking

After the sub-agent returns:

1. **Validate** the returned ADR before writing:
   - It contains all template sections (Status, Context, Decision, Consequences with both
     Positive AND Negative, Alternatives Considered, References).
   - The header number/date and `Parent Requirement` / `Related ADRs` links are correct.
   - The sub-agent returned a `SUMMARY:` and 1–4 `TAGS:` from the taxonomy. If missing or a tag is
     outside the taxonomy, fix it (or ask the sub-agent to revise) before stamping frontmatter.
   - **Re-check the ADR number** (closes the concurrent-run race noted in Step 3): re-run
     `ls docs/adr/` and confirm `{NNNN}-{focus-area}.md` does not already exist. If it does,
     increment to the next free number and update the ADR header before writing.
   - Spot-check at least one codebase reference the sub-agent cited (Glob/Grep) — if a
     referenced file/class doesn't exist, send it back to the sub-agent to fix rather than
     writing a misleading ADR.
   - If anything is missing or wrong, ask the sub-agent to revise, or fix it yourself before writing.

2. **Write** the validated ADR body to `docs/adr/{NNNN}-{focus-area}.md` using the Write tool.

3. **Stamp frontmatter** with the `write_adr_metadata` skill
   (`.claude/commands/adr/write_adr_metadata.md`), passing the sub-agent's proposed summary and tags:

   ```
   write_adr_metadata docs/adr/{NNNN}-{focus-area}.md init --summary "{summary}" --tags "{tags}"
   ```

   This derives `id`/`title`/`created` from the file, sets `status: Proposed`, and keeps the
   frontmatter in sync with the body `## Status`. Confirm the six checks the skill reports pass
   (`id` == filename stem, `status` ∈ enum, tags ⊆ taxonomy, valid date, all seven keys, frontmatter
   status matches body).

   If this ADR **supersedes** a prior ADR you identified in Step 4, also mark that older ADR on disk:

   ```
   write_adr_metadata docs/adr/{old-id}.md supersede --by {NNNN}-{focus-area}
   ```

4. **Update tracking**: `echo "{NNNN}-{focus-area}.md" >> specs/{current-spec}/.adr-list`

### Step 8: Next Steps

1. Remind user to:
   - Review and complete the ADR with technical details
   - Commit the ADR: `git add docs/adr/{NNNN}-{focus-area}.md && git commit -m "docs: add ADR for {focus-area}"`
   - The first ADR should typically be the first commit on the feature branch
   - Can create/update draft PR with ADR for early feedback
2. Multiple ADRs:
   - If requirement needs more architectural decisions, run `/spec:design [another-focus-area]` again
   - Each ADR should address a distinct architectural concern
3. When all ADRs are complete: `/spec:approve design` to proceed to tasks phase
