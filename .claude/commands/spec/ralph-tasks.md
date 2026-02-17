---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Read, Write, Glob
description: Create ralph-tasks.md for unattended TDD implementation
---

## Context

Current spec directory: specs/

**Purpose**: Generate `ralph-tasks.md` - a variant of `tasks.md` formatted for **unattended** TDD execution via the Ralph loop. Unlike `tasks.md`, ralph tasks have no approval gates and include all context needed for a fresh Claude session.

## Your Task

### Step 1: Gather Context

1. Read `specs/.current-spec` to determine the active specification directory
2. Verify `.tasks-approved` exists in that directory (ralph-tasks builds on approved task list)
3. Read `specs/{current-spec}/tasks.md` to understand the approved tasks
4. Read `specs/{current-spec}/requirements.md` for requirement context
5. Read `specs/{current-spec}/.adr-list` to see all ADRs
6. Read each ADR from `docs/adr/` to understand design decisions

### Step 2: Verify Prerequisites

- `.tasks-approved` MUST exist (ralph-tasks derives from the approved task list)
- All ADRs MUST have Status "Accepted"

If prerequisites not met, inform user and exit.

### Step 3: Generate ralph-tasks.md

Create `specs/{current-spec}/ralph-tasks.md` with the following structure:

```markdown
# Ralph Tasks: {spec-name}

> Auto-generated from tasks.md for unattended TDD execution.
> Each task is self-contained with all context a fresh Claude session needs.

## Spec Context

- **Spec**: {spec-name}
- **Requirements**: specs/{current-spec}/requirements.md
- **ADRs**: {list of ADR file paths}

## Tasks

{task list}
```

### Step 4: Format Each Task

**CRITICAL**: Each task MUST follow this exact format:

```markdown
- [ ] **[Brief behavior description]**
  - **Behavior**: [Precise behavioral specification - what should happen when X]
  - **Test file**: `tests/[Project]/[When_condition_should_behavior.cs]`
  - **Test should verify**:
    - [Verification point 1]
    - [Verification point 2]
  - **Implementation files**:
    - `src/[Project]/[File.cs]` - [What to add/change]
  - **RALPH-VERIFY**: `dotnet test tests/[Project]/ --filter "FullyQualifiedName~When_condition_should_behavior"`
  - **References**: [ADR numbers, requirement sections, existing code files to read]
```

### Key Differences from tasks.md

| Feature | tasks.md | ralph-tasks.md |
|---------|----------|----------------|
| Approval gates | `⛔ STOP HERE` after each test | None |
| `/test-first` directive | `USE COMMAND: /test-first ...` | None |
| Verification command | None | `RALPH-VERIFY:` with exact dotnet test filter |
| Context references | Assumes human context | `References:` section with files/ADRs to read |
| Atomicity | Grouped by phase | Strictly one behavior per task |

### Quality Rules (MANDATORY)

Apply these rules to EVERY task:

1. **One thing per task**: Single endpoint, component, migration, or behavior. Never combine.
2. **Testable in isolation**: After completing the task, `dotnet build` and the RALPH-VERIFY command must pass.
3. **Completable in one iteration**: ~200 lines of combined test + implementation code maximum.
4. **Ordered by dependency**: If Task B depends on Task A's code, Task A comes first.
5. **Self-contained**: The `References` section includes ALL files/ADRs a fresh Claude session needs to read before starting the task.

### Step 5: Validate Task List

Before writing the file, verify:

- [ ] Every task has a `RALPH-VERIFY` command with a valid test filter
- [ ] Every task has a `References` section
- [ ] No task combines multiple behaviors
- [ ] Tasks are ordered so dependencies come first
- [ ] No task references implementation details from tasks.md's `⛔ STOP HERE` or `/test-first` directives
- [ ] Each task's test name follows `When_[condition]_should_[expected_behavior]` convention

### Step 6: Write the File

Write the completed ralph-tasks.md to `specs/{current-spec}/ralph-tasks.md`.

Print summary:
```
Ralph tasks generated: specs/{current-spec}/ralph-tasks.md
Total tasks: N
Ready for: ./scripts/ralph.sh
```

## Important Notes

- **NEVER modify tasks.md** - ralph-tasks.md is a separate file
- Tasks should map 1:1 to behaviors from tasks.md, but reformatted for unattended execution
- Large tasks from tasks.md should be SPLIT into smaller atomic tasks
- The RALPH-VERIFY command must be copy-pasteable and work from the repo root
- References should include specific file paths, not general descriptions
