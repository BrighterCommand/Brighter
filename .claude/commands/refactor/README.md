# Refactoring Commands

This directory contains Claude Code commands for safe, disciplined refactoring following Brighter's code style principles.

## Commands

### `/refactor:tidy-first <description of desired change>`

Implements Kent Beck's "Tidy First" methodology by separating structural changes from behavioral changes into distinct commits.

**Purpose**: Ensures refactoring (structural changes) are separated from functionality changes (behavioral changes), making code reviews easier, git history clearer, and reducing bugs.

**Usage:**
```bash
/refactor:tidy-first optimize the message processing in KafkaConsumer
```

### `/refactor:smells [base-ref | base..head | path...]`

An opt-in review, run after a unit of work completes (a task, a spec section, or a branch) and its
tests are green, that looks for Martin Fowler's code smells (*Refactoring*, 2nd ed.) in the changed
code — for example Long Function, Feature Envy, Data Clumps, Primitive Obsession, Shotgun Surgery,
Speculative Generality and Message Chains — calibrated against Brighter's design principles.

**Purpose**: Turn "this could be tidier" into scheduled, named refactorings. Each finding gives the
smell, its location and evidence, and the refactoring from Fowler's catalogue that removes it, and is
marked `structural` (a `/refactor:tidy-first` job) or `public API change` (needs a decision first).

**It reports and schedules; it does not refactor.** Findings become a `/refactor:tidy-first` run, or
`STRUCTURAL` tasks appended to the active spec's `tasks.md`, as you choose.

**Usage:**
```bash
/refactor:smells                 # current branch vs master, plus uncommitted changes
/refactor:smells HEAD~3          # changes since a ref
/refactor:smells abc123^1..abc123  # changes between two refs, e.g. a merged PR's merge commit
/refactor:smells src/Paramore.Brighter/CommandProcessor.cs
```

### `/refactor:tidy-comments [base-ref | base..head | path...]`

Brings the comments in a unit of work into line with
[documentation.md § Comments in Code](../../../.agent_instructions/documentation.md#comments-in-code):
XML docs on public members only, at most a comment above the signature elsewhere, and none inside
method bodies.

**Purpose**: Remove comments that narrate the code, record its history, cite issues or specs, or talk
to a reviewer; replace comments that explain a block or expression with Extract Method, Extract
Variable, a named constant or a rename. Comments that were written *instead of* a refactoring
("must be called first", "can be null when…") are reported and scheduled, as `/refactor:smells` does.

**It changes code, structurally only**: a comment-only `refactor:` commit, then an extraction
`refactor:` commit, with tests green and no public signature changed. Reviewer rationale missing from
the ADR is shown to you rather than deleted.

Recurring cases (issue links in XML docs, AAA markers carrying text, XML docs on test doubles, wrong
ADR section references, magic literals in tests, …) are settled in the skill's *Settled Decisions*
section and applied without asking. A large range is worked in slices, one PR each, tracked in
`PROMPT.md`.

**Usage:**
```bash
/refactor:tidy-comments                 # comments this branch added or changed
/refactor:tidy-comments HEAD~3          # comments changed since a ref
/refactor:tidy-comments src/Paramore.Brighter/CommandProcessor.cs  # every comment in a file with no changes
/refactor:tidy-comments 83e0e5e47^1..83e0e5e47 src/Paramore.Brighter.BoxProvisioning  # one slice of a merged range
```

## The Core Principle

From [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md):

> **Never mix structural and behavioral changes in the same commit**

**Structural Changes** (refactoring):
- Renaming for clarity
- Extracting methods
- Reducing nesting/indentation
- Moving code between files
- Converting primitives to expressive types
- Simplifying conditionals (same logic, clearer structure)

**Behavioral Changes** (functionality):
- Adding features
- Changing algorithms
- Modifying error handling
- Adding validation
- Changing I/O or caching
- Modifying business logic

## Workflow

The `/refactor:tidy-first` command guides you through 9 phases:

### 1. **Analysis**
Reads code and categorizes changes into structural vs behavioral

### 2. **Plan**
Presents categorization to user for approval

### 3. **Structural Changes**
Makes refactoring changes WITHOUT altering behavior

### 4. **Validate Structural**
Runs tests to prove behavior unchanged

### 5. **Commit Structural**
Creates commit: `refactor: [description]`

### 6. **Behavioral Changes**
Makes functionality changes

### 7. **Validate Behavioral**
Runs tests to ensure changes work

### 8. **Commit Behavioral**
Creates commit: `feat:` or `fix:` or `perf:` [description]

### 9. **Summary**
Shows both commits and suggests next steps

## Why Separate Structural and Behavioral?

**Benefits:**

- **Easier Code Review**:
  - Reviewers can quickly approve structural changes (just refactoring)
  - Can focus deep attention on behavioral logic changes

- **Clearer Git History**:
  - Each commit has single purpose
  - Easy to see when behavior actually changed
  - Bisecting bugs is easier

- **Safer Refactoring**:
  - Tests validate structural changes didn't break anything
  - If behavior breaks, you know it was the behavioral commit

- **Better Debugging**:
  - When behavioral change causes bug, structural changes aren't suspected
  - Can revert behavioral commit without losing structural improvements

- **Disciplined Development**:
  - Forces thinking about "how the code looks" vs "what the code does"
  - Prevents "while I'm here" scope creep

## Example: Optimizing Message Processing

```bash
$ /refactor:tidy-first optimize the message processing in KafkaConsumer

Analyzing KafkaConsumer.cs...

I've identified these changes:

STRUCTURAL (refactoring):
- Extract nested conditional into IsValidMessage() method
- Rename 'msg' variable to 'message'
- Reduce indentation in ProcessBatch method
- Move magic number 1000 to constant MAX_BATCH_SIZE

BEHAVIORAL (new functionality):
- Add caching for deserialized messages
- Implement early exit for empty batches
- Change retry from linear to exponential backoff

Proceed with structural changes first?
> Yes

🔧 Making structural changes...
[Shows refactoring]

Running tests... ✓ All 47 tests pass (behavior unchanged)

✓ Committed: refactor: simplify message processing structure

Proceed with behavioral changes?
> Yes

✨ Adding functionality...
[Shows feature additions]

Running tests... ✓ All 47 tests pass

✓ Committed: feat: add caching and exponential backoff

Done! Two commits created:
1. refactor: simplify message processing structure
2. feat: add caching and exponential backoff
```

## When to Use Tidy First

**Good Fit:**
- Need to refactor code AND add/change functionality
- Existing code is hard to understand before adding feature
- Changes touch messy code that needs cleanup
- Large methods need breaking down before modification

**Not a Good Fit:**
- Pure refactoring with no functionality changes (just do it and commit as `refactor:`)
- Pure feature addition (use `/tdd:test-first` instead for TDD)
- Trivial changes that don't need structural cleanup

## Best Practices

### Structural Phase (Refactoring)

**Do:**
- Extract methods to express intent
- Rename for clarity
- Reduce nesting and indentation
- Replace magic numbers with constants
- Convert primitives to expressive types
- Simplify conditional logic structure

**Don't:**
- Change algorithms or logic
- Add new features
- Modify error handling
- Add validation
- Change I/O behavior
- Add caching or optimization

**Validate:**
- ALL tests must pass after structural changes
- If tests fail, you changed behavior - fix it

### Behavioral Phase (Functionality)

**Do:**
- Add new features
- Change algorithms
- Modify error handling
- Add validation
- Optimize performance
- Add caching

**Consider:**
- Using `/tdd:test-first` for significant new features
- Writing tests before behavioral changes (TDD)
- Breaking large behavioral changes into smaller commits

**Validate:**
- Tests should pass (or new tests added for new behavior)
- May need to write new tests for new functionality

## Categorization Guide

Sometimes it's unclear if a change is structural or behavioral. Here's guidance:

**Structural (if behavior stays EXACTLY the same):**
```csharp
// Before
if (msg != null && msg.IsValid == true && msg.Size < 1000)

// After (structural - same logic, clearer)
if (IsValidMessage(message))
```

**Behavioral (if behavior changes):**
```csharp
// Before
if (message.Size < 1000)

// After (behavioral - different validation logic)
if (message.Size < GetMaxSizeForTopic(message.Topic))
```

**When in doubt:** If tests could potentially fail from the change, it's behavioral.

## Integration with Other Workflows

**With TDD (`/tdd:test-first`):**
- Use tidy-first when refactoring existing code before adding features
- Use test-first when adding pure new functionality
- Can combine: tidy-first to clean up, then test-first to add features

**With Specifications (`/spec:implement`):**
- Spec implementation may need tidy-first approach
- Clean up existing code (structural) before implementing new spec (behavioral)

**With ADRs (`/adr`):**
- Significant refactoring may warrant an ADR
- Document WHY structural changes were made
- Reference ADR in refactor commit message

## Git Commit Messages

### Structural Commits

Format: `refactor: [brief description]`

Examples:
- `refactor: extract method to simplify error handling`
- `refactor: rename variables for clarity in KafkaConsumer`
- `refactor: reduce nesting in message processing loop`
- `refactor: convert magic numbers to named constants`

### Behavioral Commits

Format: `[type]: [brief description]`

Types and examples:
- `feat: add exponential backoff to retry logic`
- `fix: correct offset acknowledgment timing`
- `perf: implement caching for message deserialization`
- `refactor:` (if structural changes only, after all)

## Validation Strategy

**Structural validation:**
```bash
# All existing tests must pass
dotnet test

# No new tests needed (behavior unchanged)
# If tests fail, you broke behavior - fix it
```

**Behavioral validation:**
```bash
# Tests should pass with new behavior
dotnet test

# May need to write new tests for new functionality
# Consider using /tdd:test-first for test-driven approach
```

## Related Documentation

- [Code Style Guide](../../../.agent_instructions/code_style.md) - Tidy First principles (lines 74-83)
- [Testing Guidelines](../../../.agent_instructions/testing.md) - TDD workflow for behavioral changes
- [Kent Beck's Tidy First Book](https://www.oreilly.com/library/view/tidy-first/9781098151232/) - Source methodology

## Related Commands

- **`/refactor:smells`** - Find code smells in a completed unit of work and schedule refactorings
- **`/tdd:test-first`** - TDD workflow for pure feature additions
- **`/spec:implement`** - Specification-driven implementation (uses TDD)
- **`/commit`** - Standard commit (doesn't enforce separation)
- **`/adr`** - Document significant refactoring decisions
