---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(dotnet:*), Bash(git:*), Read, Write, Edit, Glob, Grep, Agent
description: Unattended TDD implementation from ralph-tasks (no approval gates)
argument-hint: [count=1]
---

## Context

Current spec directory: specs/

**Workflow**: Unattended TDD implementation - no human approval gates.

**TDD Cycle**: 🔴 Red → 🟢 Green → 🔵 Refactor (no approval step)

**AskUserQuestion is deliberately excluded** - this command runs unattended.

**Sub-agent**: Each task's Red→Green→Refactor cycle is delegated to a sub-agent
(`subagent_type: "general-purpose"`, **`model: "sonnet"`** — implementation work). The
sub-agent writes the test + implementation files and RETURNS a structured result. The MAIN
agent owns everything that must stay sequential and authoritative: the STOP-file check,
task selection, the run count, marking the checkbox, **the git commit**, and the summary.
The sub-agent NEVER commits, NEVER pushes, and NEVER edits `ralph-tasks.md`/`tasks.md`. See
`.claude/commands/spec/README.md` → "Sub-agents & model policy".

Tasks are processed **strictly sequentially** — they are dependency-ordered and each gets
its own commit. Do not parallelise.

## Your Task

Parse count from $ARGUMENTS (default: 1 if not provided or empty).

### Step 1: Check STOP File

If a file named `RALPH_STOP` exists at the repository root:
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: Z
Status: STOPPED
=== END RALPH ===
```
Then STOP immediately. Do not proceed.

### Step 2: Gather Context

1. Read `specs/.current-spec` to determine the active specification directory
2. Verify `.tasks-approved` exists in that directory
3. Read `specs/{current-spec}/ralph-tasks.md` to see ralph task list
4. Read `specs/{current-spec}/.adr-list` to see all ADRs

The main agent does **not** read the ADR bodies here — that is deliberate. Its role in this
command is bookkeeping (task selection, commit, marking, count); the per-task TDD context
(including the relevant ADRs) is read by the **sub-agent** from each task's `References`
section. This keeps the main agent's context lean.

### Step 3: Select Next Task

Find the first unchecked `- [ ]` task in `ralph-tasks.md`.

If all tasks are checked (`[x]` or `[!]`):
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: 0
Status: ALL_DONE
=== END RALPH ===
```
Then STOP.

### Step 4: Delegate the TDD Cycle to a Sub-Agent

Launch an `Agent` with `subagent_type: "general-purpose"` and **`model: "sonnet"`**. The
prompt MUST include:

1. The full text of the selected task (Behavior, Test file, Test should verify,
   Implementation files, RALPH-VERIFY command, References).
2. The paths `.agent_instructions/testing.md` and `.agent_instructions/code_style.md`, with
   an instruction to read them before writing code.
3. The TDD cycle instructions, code-style rules, and hard constraints below.
4. The required return format below.

The sub-agent runs unattended — it has full tool access (Read, Write, Edit, Glob, Grep,
Bash) and DOES write the test and implementation source files. It just must not commit,
push, or touch the task files.

#### TDD Cycle for the sub-agent (include in the prompt)

**Before starting**: Read ALL files listed in the task's **References** section, plus
`.agent_instructions/testing.md` and `.agent_instructions/code_style.md`. This provides the
context a fresh session would otherwise get from conversation.

🔴 **RED — Write a Failing Test**
- Test naming: `When_[condition]_should_[expected_behavior]`; one test case per file named
  the same; Arrange/Act/Assert with explicit comments; highlight evident data.
- Test behavior, not implementation — public exports only; no mocks for isolation, use
  `InMemory*` implementations for I/O.
- Write the test file at the path the task specifies.
- Run the task's `RALPH-VERIFY` command and confirm the test **FAILS** for the right reason
  (behavior doesn't exist yet).
- **If the test PASSES with no implementation change**: the behavior already exists. Either
  revise the test to verify something genuinely new, or RETURN status `ALREADY_COMPLETE`.

🟢 **GREEN — Make the Test Pass**
- Write the MINIMUM code to pass — no speculative code. Follow the task's
  **Implementation files** guidance.
- Code style: .NET C# conventions (PascalCase public / camelCase private), ALL_CAPS
  constants, expression-bodied members for simple members, `readonly` where appropriate,
  nullable reference types enabled, Responsibility-Driven Design, avoid primitive obsession.
- Run the `RALPH-VERIFY` command and confirm the test **PASSES**.
- Run broader tests (`dotnet test` for the relevant project[s]) to catch regressions. If a
  regression appears, fix it before finishing.

🔵 **REFACTOR — Improve the Design**
- Tidy First: structural changes only, no behavior changes. Keep methods small and focused;
  reduce complexity; remove duplication; reveal intent.
- Re-run tests after refactoring to confirm no behavioral change.

#### Hard constraints for the sub-agent (include in the prompt)

- **NEVER** run `git commit`, `git add`, or `git push`.
- **NEVER** edit `ralph-tasks.md` or `tasks.md`.
- Only create/modify the test file(s) and implementation source file(s).
- Do not ask the user anything — this is unattended.

#### Required return format (the sub-agent RETURNS this as text)

```
STATUS: GREEN | FAILED | ALREADY_COMPLETE
TEST_FILES:
  <one path per line, indented; empty if none>
IMPL_FILES:
  <one path per line, indented; empty if none>
DESCRIPTION: <one-line behavior description for the commit message>
REGRESSIONS: <none | description of any regression and how it was resolved>
FAILURE_REASON: <empty unless STATUS is FAILED — explain what went wrong>
```

Paths MUST be **one per line** (so they tokenise unambiguously regardless of spaces in a
path — no quoting or escaping needed). List nothing under `TEST_FILES:`/`IMPL_FILES:` if the
sub-agent created/modified no files of that kind. Do not use commas, JSON arrays, or a single
space-separated line.

**Main-agent parsing rules (apply before any `git` command):**
- Collect the indented lines under `TEST_FILES:` and `IMPL_FILES:` into one path list.
- If the sub-agent ignored the contract and returned a comma-separated, space-separated, or
  JSON-array value, normalise it to a clean list yourself rather than feeding the raw string
  to `git` — a mis-tokenised list silently stages the wrong files.
- **If the combined path list is empty, skip the `git` command entirely.** Never run a bare
  `git add` / `git checkout --` with no paths: `git checkout --` with no positional args
  errors ("Nothing specified"), and a blanket form would touch unrelated working-tree files.

### Step 5: Process the Result, Mark, and Commit (MAIN agent)

Read the sub-agent's returned result and act on its `STATUS`:

**GREEN or ALREADY_COMPLETE:**
1. Mark the task complete: use Edit to change `- [ ]` to `- [x]` in `ralph-tasks.md`.
2. Stage and commit (the MAIN agent owns this):
   ```bash
   git add [TEST_FILES] [IMPL_FILES] specs/{current-spec}/ralph-tasks.md
   git commit -m "feat: [DESCRIPTION]

   - Test: When_[condition]_should_[expected_behavior]
   - Implementation: [brief description]
   - Ralph task: [task number]/[total]

   Co-Authored-By: Claude Opus <noreply@anthropic.com>
   Co-Authored-By: Claude Sonnet <noreply@anthropic.com>"
   ```
   (Both models contributed: the main agent on **opus** orchestrated and committed; the
   sub-agent on **sonnet** wrote the test + implementation.)
   (For `ALREADY_COMPLETE`, commit the checkbox tick alone with a `docs:`-style message
   noting the behavior already existed.)
3. Count this task toward the run count.

**FAILED:**
1. Mark the task as failed: change `- [ ]` to `- [!]` in `ralph-tasks.md`.
2. Append a comment to the task line: ` <!-- RALPH-FAILED: [FAILURE_REASON] -->`
3. Commit the failure marker so the next iteration skips it:
   ```bash
   git add specs/{current-spec}/ralph-tasks.md
   git commit -m "chore: mark ralph task [N] failed — [short reason]"
   ```
   If the sub-agent left partial source edits, the default is to **discard them** so only
   the failure marker is committed. Discard them with a **scoped** checkout limited to the
   exact files the sub-agent reported — never a blanket `git checkout --`, which would also
   throw away unrelated working-tree changes:
   ```bash
   git checkout -- [TEST_FILES] [IMPL_FILES]
   ```
   If `STATUS: FAILED` but the sub-agent reported **no** files (it failed before writing
   anything), the path list is empty — **skip this checkout entirely** (a bare
   `git checkout --` errors with "Nothing specified") and just commit the failure marker.
   Only KEEP the partial edits (and add them to the marker commit) if BOTH hold:
   `dotnet build` of the affected project[s] succeeds, AND the edits are confined to the
   reported `TEST_FILES`/`IMPL_FILES` (no stray changes elsewhere). If either is in doubt,
   discard.
4. Count it toward the run count and proceed to the next task — do NOT get stuck.

### Step 6: Check Continuation

Before starting the next task, check ALL of these:

1. **STOP file**: If `RALPH_STOP` exists at repo root → stop
2. **Count limit**: If completed tasks this run >= count from $ARGUMENTS → stop
3. **All done**: If no more `- [ ]` tasks remain → stop

If none of these conditions are met, return to **Step 3** for the next task (a fresh
sub-agent, fresh context).

### Step 7: Print Summary

**ALWAYS** print this structured summary at the end, regardless of how you stopped:

```
=== RALPH SUMMARY ===
Tasks completed this run: N
Total tasks complete: X/Y
Tasks remaining: Z
Status: COMPLETED | COUNT_REACHED | STOPPED | ALL_DONE
=== END RALPH ===
```

Status meanings:
- `COMPLETED`: Finished all tasks requested by count
- `COUNT_REACHED`: Completed the requested number of tasks, more remain
- `STOPPED`: Halted due to RALPH_STOP file
- `ALL_DONE`: No more tasks in ralph-tasks.md

## Important Reminders

- **NEVER use AskUserQuestion** - this runs unattended
- **NEVER push to remote** - the human decides when to push
- **NEVER modify tasks.md** - only modify ralph-tasks.md
- **Always commit after each task** - each task gets its own commit, and the MAIN agent (not
  the sub-agent) makes that commit
- **The sub-agent reads its task's References first** - every task carries the context a
  fresh session needs
- Follow ALL guidelines in .agent_instructions/testing.md and .agent_instructions/code_style.md
