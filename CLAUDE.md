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
