---
allowed-tools: Read, Write, Edit, Glob, Bash, AskUserQuestion, Skill
description: Create Architecture Decision Record following Brighter's template
argument-hint: <title-of-decision>
---

# Architecture Decision Record (ADR) Creation

You are helping the user create an Architecture Decision Record following Brighter's ADR template and conventions.

## Decision Title

$ARGUMENTS

## Your Task

Create a new Architecture Decision Record in the `docs/adr/` directory following the project's conventions from [.agent_instructions/documentation.md](../../../.agent_instructions/documentation.md).

### Step 1: Determine Next ADR Number

Find the highest numbered ADR file in `docs/adr/` and take the next sequence number.

```bash
ls -1 docs/adr/*.md | grep -E '^docs/adr/[0-9]+' | sed 's/docs\/adr\/\([0-9]*\).*/\1/' | sort -n | tail -1
```

The next ADR number is the highest number + 1, formatted as a 4-digit number with leading zeros
(e.g., `0037`).

**The number is a non-unique ordering hint, not the ADR's identity.** Numbers were historically
allocated `max+1` per branch without locking, so parallel branches collided — several numbers are
shared by more than one ADR, and we do **not** renumber. The **identity is the `id`/slug = the
filename stem** (`0037-kafka-message-serialization`), which is unique; all tooling keys off that.
So: take `max+1`, but if a concurrent branch later lands the same number, that collision is
**acceptable and not a defect** — do not renumber to "fix" it. See *Identity and numbering* in
[`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md).

### Step 2: Check for Linked Specification

Check if there's a current specification this ADR should be linked to:

```bash
# Check if specs/.current-spec exists
if [ -f specs/.current-spec ]; then
    cat specs/.current-spec
fi
```

If a current spec exists:
- Read `specs/[spec-name]/requirements.md` to understand context
- This ADR should reference the parent requirement
- Add this ADR to `specs/[spec-name]/.adr-list`

### Step 3: Read the ADR Authoring Guidance

**Read [`.agent_instructions/documentation.md`](../../../.agent_instructions/documentation.md)
§ *Architecture Decision Records* before drafting.** It is the single source for the ADR
skeleton, the readability rules, the diagram rules and the pre-commit checks — the same source
`/spec:design` uses, so the two commands cannot drift. The Step 5 template below is that
skeleton; the guidance says what goes in each heading and why.

Then read the worked examples:
- `docs/adr/0001-record-architecture-decisions.md` — the minimal template
- `docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md` — the full shape:
  sequence diagram, assembly flowchart, roles table, contract tables
- `docs/adr/0072-ambient-scope-adoption-seam.md` — the decision-ladder form, for a protocol with
  too many branches to draw as a flowchart

### Step 4: Gather Information from User

**Surface prior art first.** Before gathering new content, find existing ADRs relevant to this
decision so you reuse, avoid contradicting, or deliberately supersede them rather than re-deciding in
ignorance. Use the `read_adr_metadata` skill (`read_adr_metadata`), passing the decision title as the
query and any obvious tags from the taxonomy:

```
read_adr_metadata "{decision title}" --tags "{likely-tags}"
```

It reads only frontmatter (cheap) and skips `Deprecated`/`Superseded` ADRs by default. Show the user
the returned prior-art candidates (`id` + `title` + `summary`), and note any this ADR would supersede
(you will mark that older ADR `Superseded` in Step 5).

Use AskUserQuestion to gather the key information needed for the ADR:

**Question 1: What is the architectural problem or decision to be made?**
- This becomes the Context section
- Should describe the situation requiring a decision
- Include constraints, requirements, and any background

**Question 2: What is your proposed solution or decision?**
- This becomes the Decision section
- Describe WHAT you're doing and WHY
- Include implementation details, diagrams, code examples if helpful

**Question 3 (Optional): What are the main alternatives you considered?**
- This becomes the Alternatives Considered section
- List other options and why they were rejected
- Helps future readers understand the decision rationale

**Question 4 (Optional): What are the key consequences (positive and negative)?**
- This becomes the Consequences section
- Positive outcomes
- Negative outcomes
- Risks and mitigations

### Step 5: Create the ADR File

Create the file at: `docs/adr/[NNNN]-[title].md`

**File naming**:
- Use 4-digit sequence number with leading zeros
- Use dash-case (kebab-case) for the title
- Example: `0037-kafka-message-serialization.md`

**File structure**:

```markdown
# [Number]. [Title]

Date: [YYYY-MM-DD]

## Status

Proposed

## Context

[2-4 sentences in plain language: what exists today, what is wrong with it, why that matters.
Do NOT open by naming four interfaces — a reader cannot hold type names before they know the
problem. Name the defect in terms a user would recognise.]

[If linked to spec]
**Parent Requirement**: [specs/[spec-name]/requirements.md](../../specs/[spec-name]/requirements.md)

**Scope**: This ADR decides one thing — [the decision, in one bold clause]. [If linked to a spec,
the requirement ids it discharges.]

[What this ADR deliberately does NOT decide, and where each of those things is decided instead.]

### Where this ADR sits

[INCLUDE ONLY IF THIS ADR IS ONE OF A SET — a spec's ADRs, or a related sequence. A table
mapping each ADR in the set to the one thing it decides, this one bolded and marked *(this one)*:]

| ADR | Decides |
| --- | --- |
| 00NN | [the one thing it decides] |
| **00NN** *(this one)* | [the one thing THIS ADR decides] |

[Then, if the set shares one, the single sentence that unifies them — stated identically in every
sibling that applies it.]

### [The problem, named as a behaviour rather than as a structure]

[Lead with the artefact that orients — a comparison table or a diagram — then the consequences
that follow from it, and only then the mechanism that produces them.]

### The forces

[Bulleted. Each force is one constraint that narrows the solution space, so the Decision's shape
is legible before it is stated. Includes the constraints and requirements context that older ADRs
put under separate headings.]

## Decision

**[The decision, in one bold sentence. A reader who reads only this sentence should know what was
decided and be able to recognise it in the code.]**

[One short paragraph on the shape that takes, and why. No type signatures, no file paths yet.]

### The mechanism, end to end

[BEHAVIOUR FIRST. What happens, in what order. Lead with a mermaid `sequenceDiagram`, `flowchart`
or decision-ladder table, then two or three sentences reading the load-bearing invariants off it.
This is the section a human reviewer actually reads.]

### Where the pieces live

[STRUCTURE SECOND. A mermaid `flowchart` with one `subgraph` per assembly or package, showing what
is new, what changes, and which way dependencies point.]

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Stereotype | Responsibility |
| --- | --- | --- | --- |
| [role] | [type] ([where it lives]) | **knowing** / **doing** / **deciding** | [what it is answerable for] |

[Then a sentence on the split that matters most.]

#### [Each significant type]

[The signature, then a contract table: Member | Input | Output | Error conditions.]

#### Where each type is touched

[A table: Assembly | Type | Change. Then a paragraph naming what is deliberately UNCHANGED, so an
omission is not read as an oversight.]

### Technology Choices

[Why this mechanism and not the obvious one. Each as a bolded question.]

### Implementation Approach

[The implementor's section, and the ONLY place `file:line` citation density belongs. Numbered
steps in commit order, structural changes separated from behavioural ones per Tidy First.]

## Consequences

### Positive

[Good outcomes from this decision]

### Negative

[Drawbacks or costs from this decision. An ADR with only positive consequences reads as
unreviewed — state the migration cost and who pays it.]

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| [Description of risk] | [How you'll address it, and which test or check is the guard] |

## Alternatives Considered

**1. [Name].** [Description.] **Rejected because** [real rationale, not a strawman]. [State the
do-nothing option when the ADR delivers no behaviour.]

**2. [Name].** [Description.] **Rejected because** [reason].

## References

[If linked to spec]
- Requirements: [specs/[spec-name]/requirements.md](../../specs/[spec-name]/requirements.md)
- Related ADRs:
  - [ADR NNNN: Title](NNNN-title.md) - Brief description of relationship
- External references:
  - [Link to external documentation, articles, etc.]
```

**Add frontmatter**: after writing the body above, stamp the YAML frontmatter block with the
`write_adr_metadata` skill (`write_adr_metadata`). It derives `id`/`title`/`created` from the file,
sets `status: Proposed`, and keeps the frontmatter in sync with the body `## Status`. Provide a
`summary` (1–2 sentences on WHAT was decided — an agent reads this to decide whether to open the ADR)
and 1–4 `tags` from the controlled taxonomy in
[`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md):

```
write_adr_metadata docs/adr/[NNNN]-[title].md init --summary "..." --tags "tag-a,tag-b"
```

Confirm the six checks the skill reports pass (`id` == filename stem, `status` ∈ enum, tags ⊆
taxonomy, valid date, all seven keys, frontmatter status matches body). If this ADR **supersedes** a
prior one you found in Step 4, also mark that older ADR:
`write_adr_metadata docs/adr/[old-id].md supersede --by [NNNN]-[title]`.

**Run the mechanical checks** — see
[`.agent_instructions/documentation.md`](../../../.agent_instructions/documentation.md)
§ *Before an ADR is committed*. Both, every time; they catch defects that survive a careful read.

1. **Render every mermaid diagram.** Roughly one diagram in six fails on first draft, and a
   diagram that does not render is a broken ADR that looked fine in review. Extract each mermaid
   block to a `.mmd` file under the scratchpad and render it:

   ```bash
   npx -y -p @mermaid-js/mermaid-cli@11 mmdc -i diagram.mmd -o diagram.svg
   ```

   A non-zero exit or a missing `.svg` is a failure — read the parse error, fix, re-run. Then
   render the most complex diagram to PNG (`-o diagram.png -w 1600 -b white`) and actually *look*
   at it with Read: a diagram can parse cleanly and still be unreadable, which is the signal to
   convert it to a decision-ladder table. If the check cannot run (no network for `npx`), say so
   rather than reporting the diagrams as verified.

2. **No escaped markdown**, which breaks C# generics and mermaid labels alike:

   ```bash
   grep -c '&lt;\|&gt;\|&amp;' docs/adr/[NNNN]-[title].md   # must be 0
   ```

**Back-fill the sibling map.** If this ADR joined a set — a spec's `.adr-list`, or a related
sequence — every earlier ADR's `### Where this ADR sits` table is now stale. Add the new row to
each, keeping each file's own row bolded. A map that is right for the newest ADR and wrong for the
rest is worse than no map, because a reader who finds it stale once will not check it again.

### Step 6: Regenerate the ADR index

The new frontmatter (and any supersession from Step 5) must be reflected in the derived
`docs/adr/index.md`. Regenerate it — this is the single canonical command (documented in
[`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md)):

```bash
awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md
```

`docs/adr/index.md` is a regenerable cache — never hand-edit it; always regenerate from frontmatter.

### Step 7: Update Spec ADR List (if applicable)

If linked to a spec, append this ADR to the spec's ADR list:

```bash
echo "docs/adr/[NNNN]-[title].md" >> specs/[spec-name]/.adr-list
```

### Step 8: Inform the User

Tell the user:
1. The ADR file path and number
2. Remind them the status is "Proposed" - they should:
   - Review and edit the ADR as needed
   - Commit it (ideally as the first commit on a feature branch)
   - Get team review
   - Use `/spec:approve design [NNNN]` when approved (changes status to "Accepted")


### Step 9: Review against the Design Principles

Read the ADR:

- Read ".agent_instructions/design_principles.md"
- Review the ADR using the design guidelines from "Use Responsibility-Driven Design"
- Check for a design focused on behavior: does the ADR think about *roles* and *responsibilities*.
- Responsibilities are "knowing", "doing", and "deciding"
- Allocate responsibilities into roles, focusing on cohesion.
- Roles are interfaces or abstract types
- A class can implement one or more roles. If it implements multiple roles, they should be related.
- Provide feedback and suggest improvements

**The roles table under `### Key Components` is where this review lands.** If the ADR has no such
table, or its rows are types rather than roles, RDD was applied to the review and not to the
design — send it back rather than accepting prose that mentions responsibilities.

### Step 10. Suggest next steps:

Ask the user for the next step:

   - Create a feature branch if not already on one
   - Commit the ADR: `git commit -m "docs: add ADR for [title]"`
   - Continue with additional ADRs if needed
   - Or proceed to `/spec:tasks` if design is complete

## Important Guidelines

From [.agent_instructions/documentation.md](../../../.agent_instructions/documentation.md):

**ADR Best Practices**:
- One architectural decision per ADR - stay focused
- Focus on WHY, not just WHAT
- Document alternatives considered and why they were rejected
- Cross-reference related ADRs
- First ADR should be first commit on feature branch
- Create draft PR with ADRs for early feedback

**The skeleton, the readability rules, the diagram rules and the pre-commit checks are all in
[`.agent_instructions/documentation.md`](../../../.agent_instructions/documentation.md)
§ *Architecture Decision Records*** — one source, shared with `/spec:design`, so the two commands
cannot drift. Do not restate them here; if a rule needs to change, change it there.

Two that are worth naming because they are the easiest to lose under time pressure:

- **Behaviour before structure.** `### The mechanism, end to end` precedes `### Where the pieces
  live`, which precedes the signatures. Never open a Decision with an interface declaration.
- **Write for a future reader, not for this conversation** (§ *Writing tone for design documents*).
  An ADR that records what a participant said, rather than what was decided and why, has failed
  its only audience.

**Common ADR Topics**:
- Message serialization strategy
- Error handling approach
- Persistence/storage mechanism
- API design and contracts
- Performance optimization strategy
- Security model
- Testing approach

**File Conventions**:
- Place in `docs/adr/` directory
- Use sequence number format: `NNNN-title.md` (4 digits with leading zeros)
- Use dash-case (kebab-case) for titles
- Scan existing ADRs to avoid duplication

## Example Interaction

```
User: /adr kafka message serialization strategy

[Claude scans docs/adr/ and finds highest is 0036]
Next ADR number: 0037

[Claude checks for current spec and finds specs/0001-kafka-dead-letter-queue/]
Found current spec: specs/0001-kafka-dead-letter-queue/
This ADR will be linked to that specification.

[Claude uses AskUserQuestion]
What is the architectural problem or decision to be made?
> We need to decide how to serialize messages when sending them to the Kafka dead letter queue...

What is your proposed solution?
> Use JSON serialization with UTF-8 encoding...

What alternatives did you consider?
> We considered Protocol Buffers and Avro but rejected them because...

What are the key consequences?
> Positive: Human readable, easy debugging. Negative: Larger message size...

[Claude creates docs/adr/0037-kafka-message-serialization-strategy.md]
Created: docs/adr/0037-kafka-message-serialization-strategy.md

Status: Proposed

Next steps:
1. Review and edit the ADR as needed
2. Commit: git commit -m "docs: add ADR for kafka message serialization strategy"
3. Create more ADRs if needed, or approve design with /spec:approve design
4. Status will change to "Accepted" when approved

The ADR has been added to specs/0001-kafka-dead-letter-queue/.adr-list
```

## Notes

- ADRs should be created during the design phase, before implementation
- Multiple focused ADRs are better than one large ADR covering everything
- **An accepted ADR's *decisions* are immutable — supersede with a new ADR rather than editing.**
  That immutability is about the decision, not the prose. Restructuring an accepted ADR for
  readability — reordering sections, adding a diagram or a roles table, moving `file:line`
  citations into `Implementation Approach`, fixing a title — is allowed and is not a supersession,
  provided no decision, fact, citation or requirement reference changes. Say so explicitly when
  you do it, and verify it: no citation present before may be absent after.
- The `/spec:design` command uses this same workflow when working with specifications, and both
  commands read their skeleton and rules from
  [`.agent_instructions/documentation.md`](../../../.agent_instructions/documentation.md)
