# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## How to Use This File
This file contains instructions for Claude Code to help it understand the context and requirements of the project. It is not intended to be modified by contributors. Human contributors should follow the guidelines in the [CONTRIBUTING.md](CONTRIBUTING.md) file. These guidelines derive from that document.

## ⛔ TDD Workflow (MANDATORY - NOT OPTIONAL)

When working on implementation tasks in `specs/*/tasks.md`:

- **ALWAYS use `/test-first <behavior>`** for TEST tasks
- **NEVER write tests manually and proceed to implementation**
- **STOP and ASK FOR APPROVAL** after writing each test
- The user will review the test in their IDE before you implement
- Each TEST task in tasks.md specifies the exact `/test-first` command to use
- The skill enforces the approval gate automatically - you cannot bypass it

**Why this is mandatory:**
1. Tests correctly specify desired behavior before implementation
2. Scope control - only code required by tests is written
3. No speculative code
4. User reviews test in IDE, not in CLI output

**If a task says `/test-first when ...`** - YOU MUST USE THAT COMMAND. Do not write the test file manually.

## Spec Workflow

Follow the structured specification workflow: Requirements → ADR Design → Adversarial Review (multiple rounds) → Task Breakdown → Implementation. Never skip review rounds or assume approval - wait for explicit user approval before proceeding to the next phase.

## Change Scope

Do NOT change defaults or make changes beyond what was explicitly requested. When fixing or modifying code, restrict changes to exactly what the user asked for — no additional "improvements" or default value changes.

## Adversarial Reviews

When conducting adversarial reviews, apply strict judgment criteria. A clear violation should result in FAIL, not NEEDS_ATTENTION. Err on the side of strictness rather than leniency when evaluating against guardrails and principles.

## Claude Code Skills (Recommended)

Claude Code skills automate common workflows and enforce mandatory engineering practices. **Use these skills proactively** rather than manually following documented procedures:

- **[Skills Overview](.agent_instructions/skills_overview.md)** - Quick reference for all available skills
- **[Detailed Skills Documentation](.claude/commands/README.md)** - Complete documentation for all skills

### Core Development Skills

- `/test-first <behavior>` - TDD workflow with mandatory approval before implementation ([docs](.claude/commands/tdd/README.md))
- `/tidy-first <change>` - Separate structural (refactoring) from behavioral (feature) changes ([docs](.claude/commands/refactor/README.md))
- `/adr <title>` - Create Architecture Decision Records ([docs](.claude/commands/adr/README.md))

### Specification Workflow Skills

- `/spec:requirements`, `/spec:design`, `/spec:tasks`, `/spec:implement`, `/spec:status` - Complete specification-driven development workflow ([docs](.claude/commands/spec/README.md))

**When to use skills**:
- Use `/test-first` when adding new behavior or fixing bugs
- Use `/tidy-first` when code needs refactoring before/during feature work
- Use `/adr` when documenting architectural decisions
- Use `/spec:*` commands for full feature development from requirements to implementation

## Context Management

When asked to remember learnings or update guidance:
- **Prefer project-owned files** (`.agent_instructions/`, `CLAUDE.md`, `PROMPT.md`) over ephemeral Claude memory (`~/.claude/projects/.../memory/`). Project-owned files are shared, version-controlled, and authoritative.
- Update `.agent_instructions/code_style.md` for coding conventions, `.agent_instructions/testing.md` for test practices, etc.
- Use `PROMPT.md` (if it exists) for temporary state that should persist across conversations.
- Only use Claude memory (`MEMORY.md`) for user-specific preferences that don't belong in the project, or for tracking conversation-spanning work state.

## Git Gotcha — `*.sqlite` ignore pattern matches `Paramore.Brighter.*.Sqlite` directories on macOS

`.gitignore` has `*.sqlite` for SQLite database files. On macOS (case-insensitive filesystem with `core.ignoreCase` enabled), this pattern *also* matches any directory whose name ends in `.Sqlite` — including:

- `src/Paramore.Brighter.BoxProvisioning.Sqlite/`
- `src/Paramore.Brighter.Locking.Sqlite/`
- `src/Paramore.Brighter.Inbox.Sqlite/`
- `src/Paramore.Brighter.Outbox.Sqlite/` *(and similar)*

Test directories like `tests/Paramore.Brighter.Sqlite.Tests/` end in `.Tests` and are **not** affected.

**Practical impact**:
- Modifying *already-tracked* files in those directories — stages normally with `git add`.
- Adding *new* files in those directories — `git add` reports the parent path as ignored and exits 1. Use `git add -f <path>` for new files in those directories.
- The `git add` warning aggregates to the parent directory ("paths are ignored by one of your .gitignore files: src/Paramore.Brighter.BoxProvisioning.Sqlite"). If commits fail with this message after a multi-file `git add`, check whether the failing paths are *new* files in a `.Sqlite` directory.

Don't change `.gitignore` to fix this — the pattern is correct for its purpose; the case-insensitive FS quirk is the issue.

## Detailed Instructions
For comprehensive guidance on working with this codebase, Claude should read the following files as needed:

- [Build and Development Commands](.agent_instructions/build_and_development.md) - Build scripts, test commands, and Docker setup
- [Project Structure](.agent_instructions/project_structure.md) - Organization of the codebase and testing framework
- [Code Style](.agent_instructions/code_style.md) - C# conventions and architectural patterns
- [Design Principles](.agent_instructions/design_principles.md) - Responsibility-Driven Design and architectural guidance
- [Testing](.agent_instructions/testing.md) - TDD practices, test structure, and testing guidelines
- [Generated Tests](.agent_instructions/generated_tests.md) - Test generator templates, configuration, and regeneration workflow
- [Documentation](.agent_instructions/documentation.md) - XML documentation standards and licensing requirements
- [Dependency Management](.agent_instructions/dependency_management.md) - Package management with Directory.Packages.props
