---
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, AskUserQuestion, TodoWrite
description: Separate structural and behavioral changes following Beck's Tidy First
argument-hint: <description of desired change>
---

# Tidy First - Separate Structural and Behavioral Changes

You are guiding the user through Beck's "Tidy First" methodology, which requires separating structural changes from behavioral changes into distinct commits.

## The Desired Change

$ARGUMENTS

## Core Principle

From [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md):

> **Never mix structural and behavioral changes in the same commit**

- **STRUCTURAL CHANGES**: Rearranging code without changing behavior (renaming, extracting methods, moving code)
- **BEHAVIORAL CHANGES**: Adding or modifying actual functionality

This separation makes code reviews easier, reduces bugs, and creates clearer git history.

## Your Task Workflow

### Phase 1: Analysis - Understand the Change

First, understand what the user wants to accomplish and identify the files involved.

**Steps:**
1. Read the relevant code files to understand current state
2. Analyze what needs to change to accomplish the goal
3. Categorize changes into STRUCTURAL vs BEHAVIORAL

Use TodoWrite to track the changes you identify:
```
- Analyze current code (in_progress)
- Identify structural changes needed (pending)
- Identify behavioral changes needed (pending)
- Make structural changes (pending)
- Validate with tests (pending)
- Commit structural changes (pending)
- Make behavioral changes (pending)
- Validate with tests (pending)
- Commit behavioral changes (pending)
```

**Categorization Guide:**

**STRUCTURAL (refactoring) - Does NOT change behavior:**
- Renaming variables, methods, classes
- Extracting methods to reduce complexity
- Moving code to different files/classes
- Simplifying nested conditionals (same logic, clearer structure)
- Reducing indentation levels
- Breaking up large methods
- Replacing magic numbers with named constants
- Converting primitives to expressive types (if behavior identical)

**BEHAVIORAL - DOES change functionality:**
- Adding new features
- Changing algorithms or logic
- Modifying error handling
- Adding validation
- Changing I/O operations
- Adding caching
- Modifying retry logic
- Changing data transformations

### Phase 2: Plan - Present Analysis to User

Use AskUserQuestion to confirm your analysis:

**Question**: "I've analyzed the changes needed. Does this categorization look correct?"

**Show the user:**
```
STRUCTURAL changes (to be done first):
- [List structural changes you identified]

BEHAVIORAL changes (to be done after):
- [List behavioral changes you identified]
```

**Options:**
1. "Yes, proceed with structural changes first" - Continue to Phase 3
2. "Adjust the categorization" - User explains adjustments needed
3. "Skip structural changes, do behavioral only" - Jump to Phase 5

If user adjusts, update your todo list and ask for confirmation again.

### Phase 3: Structural Changes - Refactor Without Changing Behavior

**CRITICAL RULE**: Do NOT change behavior in this phase. Only change structure.

**Steps:**
1. Make the structural changes you identified
2. Follow code style guidelines from [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md)
3. Update XML documentation if public API structure changed
4. Do NOT add new functionality
5. Do NOT change logic or algorithms

**Code Style Guidelines to Apply:**
- Keep methods small and focused (one level of indentation max)
- Use expressive names that reveal intention
- Extract methods to express intent instead of comments
- Avoid primitive obsession - use expressive types
- Follow .NET C# naming conventions
- Maintain documentation for public APIs

**After making changes:**
- Update your todo list (mark structural changes as completed)
- Show the user what you changed and why

### Phase 4: Validate - Ensure No Behavior Changed

**CRITICAL STEP**: Prove that structural changes didn't alter behavior.

**Steps:**
1. Run the test suite to ensure all tests still pass
2. If tests fail, you broke behavior - fix it immediately
3. If tests pass, structural changes are validated

```bash
dotnet test
```

**If tests fail:**
- This means you accidentally changed behavior
- Revert the breaking change
- Fix it to be truly structural
- Run tests again until all pass

**If tests pass:**
- Update todo list (mark validation as completed)
- Proceed to commit

### Phase 5: Commit Structural Changes

**Only commit if all tests pass.**

**Commit Message Format:**
```
refactor: [brief description of structural changes]

[Optional: More detailed explanation of what was refactored and why]
```

**Examples:**
- `refactor: extract method to simplify message processing logic`
- `refactor: rename variables for clarity in KafkaConsumer`
- `refactor: reduce nesting in error handling code`

**Steps:**
1. Stage the structural changes: `git add [files]`
2. Create commit with message above
3. Update todo list (mark commit as completed)
4. Inform user structural phase is complete

**Ask user:**
"Structural changes committed. Should I proceed with behavioral changes?"

Options:
1. "Yes" - Continue to Phase 6
2. "No, stop here" - End workflow
3. "Review the changes first" - Pause for user review

### Phase 6: Behavioral Changes - Add or Modify Functionality

**Now you can change behavior.**

**Steps:**
1. Make the behavioral changes you identified in Phase 1
2. Follow code style and documentation guidelines
3. Add proper error handling
4. Update or add XML documentation for new/changed public APIs
5. Follow test-first if adding new behavior (consider using `/test-first` for this)

**If adding significant new behavior:**
- Suggest using `/test-first` command instead
- Tidy First is best for changes with both refactoring and behavior modifications
- Pure feature additions work better with TDD workflow

**After making changes:**
- Update todo list (mark behavioral changes as completed)
- Show user what you changed

### Phase 7: Validate - Ensure Changes Work Correctly

Run the full test suite again:

```bash
dotnet test
```

**If tests fail:**
- Fix the failures
- These are legitimate behavior changes, so failures might be expected
- If you're adding new behavior, you might need to write new tests first

**If tests pass:**
- Update todo list (mark validation as completed)
- Proceed to commit

### Phase 8: Commit Behavioral Changes

**Only commit if tests pass (or new tests are added for new behavior).**

**Commit Message Format:**
```
[type]: [brief description of behavioral change]

[Optional: More detailed explanation]
```

**Commit Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `perf:` - Performance improvement
- `refactor:` - If you ended up with only structural changes

**Examples:**
- `feat: add exponential backoff to retry logic`
- `fix: correct offset acknowledgment in Kafka consumer`
- `perf: implement caching for message deserialization`

**Steps:**
1. Stage the behavioral changes: `git add [files]`
2. Create commit with message above
3. Update todo list (mark all remaining as completed)
4. Inform user that workflow is complete

### Phase 9: Summary and Review

Show the user:
1. Summary of both commits created
2. Offer to push to remote if on a feature branch
3. Suggest next steps (create PR, continue with more changes, etc.)

## Important Notes

### When to Use Tidy First

**Good fit:**
- You need to refactor code AND add/change functionality
- Existing code is hard to understand/modify
- Changes touch multiple areas requiring cleanup
- Large methods need breaking down before adding features

**Not a good fit:**
- Pure refactoring (no behavioral changes planned)
- Pure feature addition (use `/test-first` instead)
- Trivial changes that don't need structural cleanup

### Handling Edge Cases

**What if no structural changes are needed?**
- Skip to behavioral changes
- Still separate into clean commits
- Consider if any "while you're here" improvements would help

**What if no behavioral changes are needed?**
- This is pure refactoring
- Make changes, validate with tests, commit as `refactor:`
- No need for second commit

**What if tests don't exist?**
- Warn user that you cannot validate structural changes didn't break behavior
- Suggest writing tests first
- Proceed with caution if user approves

**What if structural changes require interface changes?**
- If public API must change structurally, include in structural commit
- Document clearly why API changed
- Update all call sites in same commit to keep things working

## Example Session

```
User: /tidy-first optimize the message processing in KafkaConsumer

[Claude reads KafkaConsumer.cs and analyzes]

I've analyzed the changes needed for optimizing message processing:

STRUCTURAL changes (to be done first):
- Extract nested conditional logic into IsValidMessage() method
- Rename 'msg' variable to 'message' for clarity
- Reduce indentation in ProcessBatch method
- Move magic number 1000 to constant MAX_BATCH_SIZE

BEHAVIORAL changes (to be done after):
- Add caching for deserialized messages
- Implement early exit for empty batches
- Change retry backoff from linear to exponential

Does this categorization look correct?
> Yes, proceed

🔧 STRUCTURAL: Making refactoring changes...

[Shows code changes]
- Extracted IsValidMessage() method
- Renamed variables
- Reduced nesting
- Added constant

Running tests to validate no behavior changed...
✓ All 47 tests pass - no behavior changed

Committing structural changes...
✓ Committed: refactor: simplify message processing structure in KafkaConsumer

Should I proceed with behavioral changes?
> Yes

✨ BEHAVIORAL: Adding caching and optimizations...

[Shows code changes]
- Added message cache using MemoryCache
- Early exit for empty batches
- Exponential backoff implementation

Running tests...
✓ All 47 tests pass

Committing behavioral changes...
✓ Committed: feat: add caching and exponential backoff to message processing

Complete! Two commits created:
1. refactor: simplify message processing structure in KafkaConsumer
2. feat: add caching and exponential backoff to message processing

This separation makes code review easier and git history clearer.
```

## Benefits of This Approach

From [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md):

- **Easier Code Review**: Reviewers can approve structural changes quickly, focus on behavioral logic
- **Clearer Git History**: Each commit has single purpose (structure OR behavior)
- **Safer Refactoring**: Tests validate structural changes didn't break anything
- **Better Debugging**: If behavioral change causes bug, structural changes aren't suspected
- **Disciplined Development**: Forces thinking about "how code looks" vs "what code does"

## Related Commands

- **`/test-first`** - Use for pure feature addition with TDD
- **`/spec:implement`** - Uses TDD workflow, could benefit from tidy-first approach
- **`/commit`** - Standard commit workflow (doesn't separate structural/behavioral)
