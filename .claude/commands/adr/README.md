# Architecture Decision Record (ADR) Commands

This directory contains Claude Code commands for creating and managing Architecture Decision Records in the Brighter project.

## Commands

### `/adr <title>`

Creates a new Architecture Decision Record following Brighter's template and conventions.

**Purpose**: Streamlines the process of creating properly formatted and numbered ADRs, ensuring consistency and completeness.

**Usage:**
```bash
/adr kafka message serialization strategy
```

**What it does:**

1. **Auto-numbers**: Scans `docs/adr/` to find the next sequence number
2. **Links to specs**: Automatically links to current specification if one exists
3. **Follows template**: Uses the structure from ADR 0001 and comprehensive examples
4. **Gathers information**: Prompts for key ADR content (context, decision, alternatives, consequences)
5. **Creates file**: Generates properly named file in `docs/adr/[NNNN]-[title].md`
6. **Updates tracking**: Adds to spec's `.adr-list` if applicable

**File Naming Convention:**
- Format: `[NNNN]-[title].md`
- 4-digit sequence number with leading zeros (e.g., `0037`)
- Dash-case (kebab-case) title
- Example: `0037-kafka-message-serialization-strategy.md`

**ADR Structure:**

The command creates ADRs with these sections:
- **Title and Date**: Number, descriptive title, creation date
- **Status**: Starts as "Proposed", changes to "Accepted" when approved
- **Context**: The problem, requirements, constraints
- **Decision**: What you're doing and WHY
- **Consequences**: Positive, negative, risks and mitigations
- **Alternatives Considered**: Other options and why rejected
- **References**: Links to requirements, related ADRs, external docs

## ADR Metadata (frontmatter & index)

Every ADR carries a YAML **frontmatter** block (`id`, `title`, `status`, `author`, `created`,
`summary`, `tags`) so agents can find prior art from cheap metadata instead of grepping full bodies.
The schema, status vocabulary, tag taxonomy, and identity rule are defined once in
[`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md).

Two skills manage it (both are invoked *by* `/adr`, `/spec:design`, and `/spec:approve`, but can be
run directly):

- **`read_adr_metadata`** (`read_adr_metadata.md`) — reads only the frontmatter of each ADR and
  returns ranked prior-art candidates (`id` + `title` + `summary`) filtered by tag/status. Skips
  `Deprecated`/`Superseded` by default. Used before drafting to surface related decisions.
- **`write_adr_metadata`** (`write_adr_metadata.md`) — adds/updates a file's frontmatter and keeps it
  in sync with the body `## Status` (`init` → `Proposed`; `status Accepted` on approval;
  `supersede`/`deprecate` on retirement). Idempotent.

**Identity is the `id`/slug (filename stem), not the number.** ADR numbers are a non-unique ordering
hint — parallel branches have collided on numbers and we do not renumber. Tooling keys off `id`.

**Derived index** — `docs/adr/index.md` is a table generated *from* the frontmatter. It is a
regenerable cache: **never hand-edit it**. `/adr` and `/spec:approve` refresh it automatically; to
regenerate by hand run the single canonical command:

```bash
awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md
```

## Integration with Specification Workflow

The `/adr` command integrates with the [specification workflow](../spec/README.md):

- **Standalone**: Use anytime you need to document an architectural decision
- **With specs**: Automatically links to current spec and updates `.adr-list`
- **Design phase**: Part of the `/spec:design` workflow

When a current spec exists (in `specs/.current-spec`):
- ADR includes link to parent requirement
- ADR is tracked in `specs/[spec]/.adr-list`
- Can be approved with `/spec:approve design [NNNN]`

## Why Use ADRs?

From [.agent_instructions/documentation.md](../../../.agent_instructions/documentation.md):

Architecture Decision Records capture important design decisions that provide context to future reviewers and explorers of the codebase. They answer:

- **What** decision was made
- **Why** it was made (most important!)
- **What alternatives** were considered
- **What consequences** resulted
- **What context** influenced the decision

## Best Practices

**One Decision Per ADR**:
- Keep ADRs focused on a single architectural decision
- Better to have multiple small ADRs than one large one
- Makes them easier to review, reference, and supersede

**Common ADR Topics**:
- Message serialization strategy
- Error handling approach
- Persistence/storage mechanism
- API design and contracts
- Performance optimization strategy
- Security model
- Testing approach
- Concurrency model

**Workflow**:
1. Create feature branch
2. Write ADR(s) - use `/adr` command
3. Commit ADR as first commit: `git commit -m "docs: add ADR for [title]"`
4. Create draft PR for early feedback
5. Iterate based on feedback
6. Approve with `/spec:approve design [NNNN]` (changes status to "Accepted")
7. Proceed with implementation

**Multiple ADRs Example**:

A single feature might need several ADRs:

```bash
# Feature: Kafka Dead Letter Queue
/adr message serialization strategy       # ADR 0037
/adr error classification approach        # ADR 0038
/adr dlq persistence mechanism            # ADR 0039
/adr retry and requeue strategy           # ADR 0040
```

Each ADR is:
- Focused on one decision
- Can be reviewed independently
- Can be referenced separately
- Can be superseded individually

## Example Session

```bash
$ /adr kafka message serialization strategy

Scanning docs/adr/ for next sequence number...
Next ADR: 0037

Found current spec: specs/0001-kafka-dead-letter-queue/
This ADR will link to that specification.

What is the architectural problem or decision to be made?
> We need to decide how to serialize messages when sending them to
> the Kafka dead letter queue. The messages need to be readable for
> debugging but also preserve all original data.

What is your proposed solution?
> Use JSON serialization with UTF-8 encoding. Store the original
> message body as a base64-encoded string within the JSON structure...

What alternatives did you consider?
> Protocol Buffers - rejected because not human-readable
> Avro - rejected because adds schema registry dependency
> Raw bytes - rejected because not debuggable

What are the key consequences?
> Positive: Human readable, easy debugging, no additional dependencies
> Negative: Larger message size, potential encoding issues

✓ Created: docs/adr/0037-kafka-message-serialization-strategy.md
✓ Added to: specs/0001-kafka-dead-letter-queue/.adr-list

Status: Proposed

Next steps:
1. Review the ADR and edit as needed
2. Commit: git commit -m "docs: add ADR for kafka message serialization strategy"
3. Create more ADRs for other decisions in this feature
4. Approve with: /spec:approve design 0037
```

## Approving ADRs

To change status from "Proposed" to "Accepted":

```bash
# Approve specific ADR
/spec:approve design 0037

# Approve all ADRs for current spec
/spec:approve design
```

This updates the Status field in the ADR from "Proposed" to "Accepted".

## File Locations

```
Brighter/
├── docs/
│   └── adr/
│       ├── index.md                                # Derived index (generated; do not hand-edit)
│       ├── 0001-record-architecture-decisions.md  # Template
│       ├── 0047-message-rejection-routing-strategy.md
│       └── 0037-kafka-message-serialization-strategy.md
└── specs/
    └── 0001-kafka-dead-letter-queue/
        └── .adr-list  # Tracks ADRs for this spec
```

## Related Commands

- **`/spec:design [focus-area]`** - Same as `/adr` but within spec workflow context
- **`/spec:review design [NNNN]`** - Review a specific ADR
- **`/spec:approve design [NNNN]`** - Approve an ADR (changes status to "Accepted")
- **`/spec:status`** - Shows all specs and their ADR status

## References

- [Documentation Standards](../../../.agent_instructions/documentation.md)
- [ADR Template](../../../docs/adr/0001-record-architecture-decisions.md)
- [Specification Workflow](../spec/README.md)
- [Michael Nygard's ADR article](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions)
