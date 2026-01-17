# Refactoring Commands

This directory contains Claude Code commands for safe, disciplined refactoring following Brighter's code style principles.

## Commands

### `/tidy-first <description of desired change>`

Implements Kent Beck's "Tidy First" methodology by separating structural changes from behavioral changes into distinct commits.

**Purpose**: Ensures refactoring (structural changes) are separated from functionality changes (behavioral changes), making code reviews easier, git history clearer, and reducing bugs.

**Usage:**
```bash
/tidy-first optimize the message processing in KafkaConsumer
```

## The Core Principle

From [docs/agent_instructions/code_style.md](../../../docs/agent_instructions/code_style.md):

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

The `/tidy-first` command guides you through 9 phases:

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
$ /tidy-first optimize the message processing in KafkaConsumer

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
- Pure feature addition (use `/test-first` instead for TDD)
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
- Using `/test-first` for significant new features
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

**With TDD (`/test-first`):**
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
# Consider using /test-first for test-driven approach
```

## Related Documentation

- [Code Style Guide](../../../docs/agent_instructions/code_style.md) - Tidy First principles (lines 74-83)
- [Testing Guidelines](../../../docs/agent_instructions/testing.md) - TDD workflow for behavioral changes
- [Kent Beck's Tidy First Book](https://www.oreilly.com/library/view/tidy-first/9781098151232/) - Source methodology

## Related Commands

- **`/test-first`** - TDD workflow for pure feature additions
- **`/spec:implement`** - Specification-driven implementation (uses TDD)
- **`/commit`** - Standard commit (doesn't enforce separation)
- **`/adr`** - Document significant refactoring decisions
