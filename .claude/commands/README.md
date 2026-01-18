# Claude Code Skills for Brighter Development

This directory contains Claude Code skills (slash commands) that enforce Brighter's engineering practices and streamline common development workflows.

## Quick Start

Skills are invoked using slash commands in Claude Code:

```bash
/test-first <behavior description>    # TDD with mandatory approval
/adr <title>                          # Create Architecture Decision Record
/tidy-first <change description>      # Separate structural from behavioral changes
```

## Available Skills

### 1. Test-Driven Development

**Command**: `/test-first <behavior description>`

**Purpose**: Enforces TDD workflow with mandatory user approval before implementation.

**When to use**:
- Adding new behavior or functionality
- Fixing bugs with test-first approach
- Want to ensure tests are correct before writing implementation

**Workflow**:
1. 🔴 **RED**: Claude writes a failing test following Brighter conventions
2. ✅ **APPROVAL**: You must approve the test before implementation
3. 🟢 **GREEN**: Claude implements minimum code to pass the test
4. 🔵 **REFACTOR**: Claude suggests design improvements (optional)

**Example**:
```bash
/test-first when an invalid message is received it should be sent to the dead letter queue
```

**Why it matters**: The approval step is MANDATORY per testing.md when working with AI. This skill enforces that requirement, preventing implementation before you validate the test specification.

📖 **Documentation**: [.claude/commands/tdd/README.md](tdd/README.md)

---

### 2. Architecture Decision Records

**Command**: `/adr <title>`

**Purpose**: Automates creation of properly formatted and numbered ADRs.

**When to use**:
- Making significant architectural decisions
- Need to document WHY a design choice was made
- Want to capture alternatives considered
- Required for new capabilities per CONTRIBUTING.md

**What it does**:
1. Scans `docs/adr/` to find next sequence number
2. Checks for current spec and links if applicable
3. Prompts for key ADR content (context, decision, alternatives, consequences)
4. Creates properly named file: `docs/adr/[NNNN]-[title].md`
5. Updates spec's `.adr-list` if part of spec workflow

**Example**:
```bash
/adr kafka message serialization strategy
```

**Output**: Creates `docs/adr/0037-kafka-message-serialization-strategy.md` with proper structure, Status: Proposed.

**Why it matters**: ADRs capture the WHY behind decisions, not just the WHAT. This skill ensures they're created consistently and tracked properly.

📖 **Documentation**: [.claude/commands/adr/README.md](adr/README.md)

---

### 3. Tidy First - Separate Structural from Behavioral Changes

**Command**: `/tidy-first <change description>`

**Purpose**: Enforces Beck's "Tidy First" methodology by separating refactoring from functionality changes into distinct commits.

**When to use**:
- Need to refactor code AND add/change functionality
- Existing code is messy and needs cleanup before modification
- Want cleaner git history and easier code reviews
- Large methods need breaking down before adding features

**Workflow**:
1. **Analysis**: Categorizes changes into structural (refactoring) vs behavioral (functionality)
2. **Plan**: Gets your approval of categorization
3. **Structural Phase**: Makes refactoring changes only
4. **Validate**: Runs tests - all must pass (behavior unchanged)
5. **Commit**: Creates `refactor:` commit
6. **Behavioral Phase**: Makes functionality changes
7. **Validate**: Runs tests with new behavior
8. **Commit**: Creates `feat:`/`fix:`/`perf:` commit

**Example**:
```bash
/tidy-first optimize the message processing in KafkaConsumer
```

**Output**: Two separate commits:
1. `refactor: simplify message processing structure in KafkaConsumer`
2. `feat: add caching and exponential backoff to message processing`

**Why it matters**: Separating structural from behavioral changes makes code reviews easier, git history clearer, and reduces bugs. Required per code_style.md.

📖 **Documentation**: [.claude/commands/refactor/README.md](refactor/README.md)

---

## Skill Categories

### Development Workflow Skills
- **`/test-first`** - TDD with approval gate
- **`/tidy-first`** - Safe refactoring workflow

### Documentation Skills
- **`/adr`** - Architecture Decision Records

### Specification Workflow Skills (Pre-existing)
- **`/spec:requirements`** - Capture requirements
- **`/spec:design`** - Create design ADRs
- **`/spec:tasks`** - Break down implementation
- **`/spec:implement`** - TDD implementation
- **`/spec:status`** - Show spec status
- **`/spec:approve`** - Approve phases
- **`/spec:review`** - Review phases

📖 **Documentation**: [.claude/commands/spec/README.md](spec/README.md)

---

## When to Use Which Skill

### Decision Tree

```
Do you need to document an architectural decision?
├─ Yes → /adr <title>
└─ No ↓

Are you adding new behavior or fixing a bug?
├─ Yes ↓
│   └─ Does existing code need refactoring first?
│       ├─ Yes → /tidy-first <description>
│       └─ No → /test-first <behavior>
└─ No ↓

Are you just refactoring with no behavior changes?
├─ Yes → /tidy-first <description> (will create single refactor commit)
└─ No → Use standard workflow
```

### Common Scenarios

**Scenario 1: Adding a new feature**
```bash
# If code is clean, use test-first
/test-first when message fails validation it should log detailed error

# If code needs cleanup first, use tidy-first
/tidy-first add validation logging with error details
```

**Scenario 2: Implementing from a specification**
```bash
# Part of spec workflow
/spec:requirements 123
/spec:design message-validation-strategy  # Uses /adr internally
/spec:tasks
/spec:implement  # Uses /test-first approach
```

**Scenario 3: Making architectural decision**
```bash
# Standalone or part of spec
/adr error-handling-strategy-for-kafka-consumer
```

**Scenario 4: Optimizing existing code**
```bash
# Refactor structure, then add optimizations
/tidy-first optimize message batch processing for better throughput
```

---

## Integration with Brighter Practices

These skills enforce practices documented in `.agent_instructions/`:

| Skill | Enforces | Reference |
|-------|----------|-----------|
| `/test-first` | TDD approval workflow | [testing.md](../../.agent_instructions/testing.md) lines 11-26 |
| `/adr` | ADR creation standards | [documentation.md](../../.agent_instructions/documentation.md) lines 49-62 |
| `/tidy-first` | Structural/behavioral separation | [code_style.md](../../.agent_instructions/code_style.md) lines 74-83 |

All three make **mandatory workflows enforceable** rather than just documented.

---

## Benefits

### For Individual Developers

- **Faster workflows**: One command does multi-step processes correctly
- **Less to remember**: Skills encode the practices, you just invoke them
- **Built-in guardrails**: Mandatory approval gates prevent mistakes
- **Better habits**: Using skills reinforces best practices

### For Code Reviews

- **Clearer commits**: Structural and behavioral changes are separated
- **Better context**: ADRs explain WHY decisions were made
- **Correct tests**: Approval gate ensures tests are reviewed before implementation
- **Easier review**: Small, focused commits are easier to review

### For Project Quality

- **Consistent ADRs**: All architectural decisions are documented the same way
- **Better git history**: Commits have single purpose (structure OR behavior)
- **Test coverage**: TDD approach ensures tests are written first
- **Design quality**: Approval and refactoring phases promote better design

---

## Skill Implementation Details

Skills are implemented as Markdown files with frontmatter defining:

```yaml
---
allowed-tools: Read, Write, Edit, Bash, AskUserQuestion, ...
description: Brief description shown in skill list
argument-hint: <what user should provide>
---
```

The body contains instructions for Claude Code to follow when the skill is invoked.

**Location**: `.claude/commands/[category]/[skill-name].md`

---

## Examples of Skills in Action

### Example 1: Test-First Development

```
Developer: /test-first when a message is rejected it should be sent to dead letter queue

Claude:
🔴 RED: Writing test When_message_is_rejected_should_send_to_dead_letter_queue

[Shows test code following BDD naming, Arrange/Act/Assert structure]

Test fails: ✓ "SendToDeadLetterQueue method not found"

✅ APPROVAL REQUIRED: Should I proceed to implement the code to make this test pass?

Developer: Yes

Claude:
🟢 GREEN: Implementing SendToDeadLetterQueue method
[Shows implementation with XML docs]
✓ Test passes
✓ All 47 tests pass

🔵 REFACTOR: Extracting rejection logic into separate method
[Shows refactoring]
✓ All tests still pass

Suggested commit: feat: send rejected messages to dead letter queue
```

### Example 2: Architecture Decision Record

```
Developer: /adr kafka consumer error handling strategy

Claude:
Next ADR number: 0037
Found current spec: specs/0001-kafka-dead-letter-queue/

What is the architectural problem or decision to be made?
Developer: We need to decide how to handle different types of errors in the Kafka consumer...

What is your proposed solution?
Developer: Use different exception types to route to DLQ vs invalid message topics...

What alternatives did you consider?
Developer: Single error topic - rejected because can't differentiate error types...

✓ Created: docs/adr/0037-kafka-consumer-error-handling-strategy.md
✓ Added to: specs/0001-kafka-dead-letter-queue/.adr-list

Status: Proposed

Next: Review and commit: git commit -m "docs: add ADR for kafka consumer error handling"
```

### Example 3: Tidy First Refactoring

```
Developer: /tidy-first optimize KafkaConsumer message processing

Claude:
Analyzing KafkaConsumer.cs...

STRUCTURAL changes (refactoring):
- Extract IsValidMessage() method
- Rename 'msg' to 'message'
- Reduce nesting in ProcessBatch
- Move magic number to constant

BEHAVIORAL changes (new functionality):
- Add message caching
- Implement early exit for empty batches
- Change to exponential backoff

Proceed with structural changes first?
Developer: Yes

Claude:
🔧 STRUCTURAL: Making refactoring changes...
[Shows refactoring]

Running tests... ✓ All 47 tests pass (no behavior changed)

✓ Committed: refactor: simplify message processing structure

Proceed with behavioral changes?
Developer: Yes

Claude:
✨ BEHAVIORAL: Adding optimizations...
[Shows new features]

Running tests... ✓ All 47 tests pass

✓ Committed: feat: add caching and exponential backoff to message processing

Complete! Two commits created for easier review.
```

---

## Tips for Using Skills

### Best Practices

1. **Use skills proactively**: Don't wait until you're stuck - use them from the start
2. **Trust the process**: The approval gates and validations are there for good reasons
3. **Combine skills**: Use `/adr` to document, `/test-first` to implement
4. **Review skill output**: Always review what the skill produces before accepting
5. **Iterate**: Skills support iteration - if categorization is wrong, adjust and continue

### Common Patterns

**Pattern 1: Feature Development**
```bash
/adr <design decision>        # Document the approach
/test-first <behavior>        # Implement with TDD
# Repeat test-first for each behavior
```

**Pattern 2: Refactoring + Feature**
```bash
/tidy-first <optimization>    # Clean up + add feature
# Results in two commits: refactor + feat
```

**Pattern 3: Specification-Driven**
```bash
/spec:requirements <issue>    # Capture requirements
/spec:design <focus>          # Uses /adr internally
/spec:tasks                   # Break down work
/spec:implement               # Uses /test-first approach
```

---

## Getting Help

- **Skill documentation**: Each skill has a README.md in its directory
- **Brighter guidelines**: See `.agent_instructions/` for full practices
- **Issues**: Report skill issues at https://github.com/anthropics/claude-code/issues
- **Contributing guidelines**: See [CONTRIBUTING.md](../CONTRIBUTING.md)

---

## Summary

Three new skills enforce Brighter's mandatory engineering practices:

| Skill | Enforces | Creates |
|-------|----------|---------|
| `/test-first` | TDD with approval | Tests → Implementation → Refactoring |
| `/adr` | Documented decisions | Numbered ADR files |
| `/tidy-first` | Structural/behavioral separation | Two commits: refactor + feat |

**Key insight**: These skills make the **correct approach the easy path** by automating multi-step workflows and enforcing approval gates.

**Try them**: Start with `/test-first` for your next feature or `/tidy-first` for your next optimization.
