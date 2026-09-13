---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(date:*), Bash(grep:*), Bash(dotnet:*), Bash(git:*), Read, Write, Edit, Glob, Grep, Agent, AskUserQuestion, ScheduleWakeup
description: Unattended TDD implementation from the approved tasks.md (review-after gear + self-driving loop)
argument-hint: [count]
---

## Context

Current spec directory: specs/

**Workflow**: Unattended TDD implementation from the spec's **approved `tasks.md`** — the same task
list `/spec:implement` works from, in the same format. A self-driving loop processes unchecked tasks
one after another until a user-chosen bound, a STOP signal, a downshift of the review gear, or all
tasks are done.

**TDD Cycle**: 🔴 Red → 🟢 Green → 🔵 Refactor (no approval pause)

### This command is the `review-after` driver

There is one task-list format (`tasks.md`) and one gate-state mechanism (the **review gear**, see
[ADR 0071](../../../docs/adr/0071-tdd-review-gear.md) and [`gear.md`](gear.md)). This command does
not have its own format and does not regenerate anything — it is simply the driver that runs
`tasks.md` in the `review-after` gear, in a loop, unattended.

- `/spec:implement` — one task at a time, honours whichever gear is set.
- `/spec:ralph-implement` — a loop, **always `review-after`**, and it writes that gear so the state
  on disk matches what is actually happening.

**`review-after` removes the human approval pause and nothing else.** RED-first, the full regression
suite, the two-commit shape, and every test-authoring convention hold here exactly as they do in
`/spec:implement`. See *What this loop must never drop*, below.

**Runtime model — Opus orchestrator + auto mode (advisory).** This command is designed to run
**unattended**:

- **Run it on Opus.** The orchestrator (this main agent) should be on **opus** — it is the
  required model for **auto mode**, and the policy for the unattended path. If the session is
  not on opus, advise the user to switch (`/model opus`) before a real unattended run.
- **Enable auto mode.** Auto mode is a *permission mode* configured outside this command
  (Claude Code settings, or `CLAUDE_CODE_ENABLE_AUTO_MODE` on cloud providers; Opus-gated). It
  lets the loop run without per-action permission prompts. This command **cannot** toggle it.
  **Advise, then proceed**: if it looks like auto mode is off (you are hitting permission
  prompts), warn the user that the loop will pause for approvals and is not truly unattended —
  but still run. Do not hard-block.

**AskUserQuestion — setup only.** This command may use `AskUserQuestion` **only** in the
up-front setup (Step 0): the auto-mode/Opus advisory, the gear reason, and choosing the run bound.
Once the loop starts it is fully unattended — **never** prompt the user mid-loop.

**Sub-agent (cost control): Sonnet.** Each task's Red→Green→Refactor cycle is delegated to a
sub-agent (`subagent_type: "general-purpose"`, **`model: "sonnet"`**). The orchestrator stays
on opus for cheap bookkeeping; the expensive per-task implementation churn runs on the cheaper
**sonnet** sub-agent and in its own context, keeping it out of the loop's opus context. The
sub-agent writes the test + implementation files and RETURNS a structured result. The MAIN
agent owns everything that must stay sequential and authoritative: the STOP-file check, the gear
check, task selection, the run count, marking the checkbox, **the git commits**, and the summary.
The sub-agent NEVER commits, NEVER pushes, and NEVER edits `tasks.md`. See
`.claude/commands/spec/README.md` → "Sub-agents & model policy".

Tasks are processed **strictly sequentially** — they are dependency-ordered and each gets
its own commits. Do not parallelise.

## What this loop must never drop

Running unattended changes *who reviews when*. It changes nothing else. Every task in this loop:

- **Proves RED first** — the test is run and observed to fail *for the right reason* before any
  production code is written. A task whose test passes on first run is `ALREADY_COMPLETE`, never a
  licence to write the implementation anyway.
- **Runs the full regression suite** for the affected project(s), not just the new test's own
  `--filter`.
- **Produces two commits** — a `feat:`/`test:`/`fix:`/`refactor:` commit for the change, then a
  separate `docs:` commit ticking the task off in `tasks.md`.
- **Obeys every standing test-authoring convention**: TestDoubles one class per file; a distinct
  request type per new test double so assembly scans do not collide; new closed generics registered
  with each test project's logging `Initializer.cs`; `When_[condition]_should_[behavior]` naming;
  no mocks for isolation; `InMemory*` for I/O.

## Your Task

### Step 0: Advise Runtime, Set the Gear, Ask the Run Bound

This runs **once**, before the loop starts.

1. **Runtime advisory.** Confirm the orchestrator is on **opus** and that **auto mode** is
   expected for an unattended run. If the session is not on opus, advise `/model opus`. If
   auto mode appears off, warn that the loop will pause for permission prompts. Advise — do not
   block.

2. **Set the review gear.** This loop runs `review-after`, so make the state on disk say so —
   otherwise a session that inspects `/spec:gear` mid-run gets a false answer.
   - Read `specs/{current-spec}/.current-gear` if it exists.
   - If it already reads `gear: review-after`, keep it and note its `scope:` — the loop will honour
     that scope (see Step 3).
   - Otherwise ask the user, with `AskUserQuestion`, for the **reason** for the upshift (and offer
     to scope it to a phase of `tasks.md`), then write the file:
     ```
     # Working state — untracked. Shift with /spec:gear.
     gear: review-after
     scope: {phase heading, or omit for spec-wide}
     driver: ralph
     shifted: {today} — {reason}
     ```
   - Add or update the pointer line in `PROMPT.md` so a fresh session finds the gear (see
     [`gear.md`](gear.md) → *Record it in `PROMPT.md`*).

3. **Choose the bound.** The loop needs a stopping bound. If a numeric `count` was passed in
   `$ARGUMENTS`, take that as the **tasks** bound and skip the prompt. Otherwise use
   `AskUserQuestion` to ask the user which bound to use and its value:

   - **Tasks** — stop after **N tasks complete** this run (a task "completes" on `GREEN` or
     `ALREADY_COMPLETE`; `FAILED` does not count). This is the `count` argument.
   - **Turns** — stop after **N loop iterations** attempted, regardless of outcome (so a run
     of failures still terminates). Use when you want to cap effort, not completions.
   - **Budget** — stop when roughly **N output tokens** have been consumed by the run. Use the
     turn's token budget (a `+Nk`-style directive) when one is set; otherwise approximate and
     stop once the loop has clearly consumed the requested budget.

   Record the chosen bound type and value. Default if the user gives nothing and no `count`
   was passed: **Tasks = 1**.

### Step 1: Check the Stop Conditions

Two independent ways to halt, both checked **before every task**:

1. **STOP file** — a file named `RALPH_STOP` at the repository root. The unattended kill-switch:
   `touch RALPH_STOP` from another terminal.
2. **Gear downshift** — `specs/{current-spec}/.current-gear` reads `gear: review-before` (or the
   file has been deleted, or is unparseable). The user has decided the work needs per-test review
   again. **This is not a failure.** Stop cleanly; nothing already completed is unwound or redone,
   and `/spec:implement` picks up from the next unchecked task under the restored gate.

If either holds:
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: Z
Status: STOPPED | DOWNSHIFTED
=== END RALPH ===
```
Then STOP immediately. On `DOWNSHIFTED`, also print the next unchecked task and the exact
`/spec:implement` command to resume with.

### Step 2: Gather Context

1. Read `specs/.current-spec` to determine the active specification directory
2. Verify **all three** approval markers exist in that directory: `.requirements-approved`,
   `.design-approved`, `.tasks-approved`. This loop runs an **approved** task list; if
   `.tasks-approved` is missing, tell the user to run `/spec:review tasks` then
   `/spec:approve tasks` first, and exit.
3. Read `specs/{current-spec}/tasks.md` — the task list. There is no `ralph-tasks.md`; do not
   create one.
4. Read `specs/{current-spec}/.adr-list` — the ADR paths to hand to each sub-agent.

The main agent does **not** read the ADR bodies here — that is deliberate. Its role in this
command is bookkeeping (task selection, commits, marking, count); the per-task TDD context
(including the relevant ADRs) is read by the **sub-agent** from the paths the main agent passes.
This keeps the main agent's context lean.

### Step 3: Select the Next Task

Scan `tasks.md` top to bottom for the first task that is:

- unchecked (`- [ ]`) — skip `- [x]` (done) and `- [!]` (failed earlier this run), and
- **within the gear's `scope:`**, if one is set — i.e. under that phase heading. If the next
  unchecked task falls outside the scope, the loop's work is finished: stop with
  `Status: SCOPE_COMPLETE` and name the task it stopped before.

`tasks.md` is a general task list, so tasks come in several shapes. Read the task's own text and
dispatch on it:

| Task shape | Handling |
|------------|----------|
| **`TEST + IMPLEMENT: …`** (carries a `/test-first` command and an approval-gate line) | The normal case — Step 4's Red→Green→Refactor cycle. The task's `⛔` approval-gate line is a `review-before` instruction; under `review-after` it does not fire, and that is the *only* thing the gear changes about the task. |
| **`STRUCTURAL: …`** (tidy-first refactoring) | No new test. Delegate the refactoring, and require the **existing** suite green before and after. Commit as `refactor:`. Behaviour must not change. |
| **`DOC: …`** (documentation only) | No test, no build. Delegate the edit; commit as `docs:`. This is the behaviour commit for that task — the checkbox tick still gets its own second commit. |
| **Anything else** | If you cannot confidently classify the task, do **not** guess: mark it `- [!]` with `RALPH-SKIPPED: unrecognised task shape, needs /spec:implement`, commit the marker, and continue. An unattended loop should skip what it does not understand, not improvise. |

If no unchecked tasks remain:
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: 0
Status: ALL_DONE
=== END RALPH ===
```
Then STOP.

### Step 4: Delegate the Cycle to a Sub-Agent

Launch an `Agent` with `subagent_type: "general-purpose"` and **`model: "sonnet"`**. The
prompt MUST include:

1. The **full text of the selected task** from `tasks.md`, verbatim, including its phase heading.
2. The context the task does not carry itself — because `tasks.md` tasks were written for an
   interactive session, they assume conversation context a fresh sub-agent does not have. Pass:
   - `specs/{current-spec}/requirements.md`
   - every ADR path from `.adr-list`
   - `.agent_instructions/testing.md` and `.agent_instructions/code_style.md`
   with an instruction to **read them before writing code**.
3. The **verify command** to use. `tasks.md` tasks name a test location and test file but not a
   filter command — derive it and state it explicitly in the prompt:
   `dotnet test {test project from the task's test location} --filter "FullyQualifiedName~{test method name}"`
4. The cycle instructions, code-style rules, and hard constraints below.
5. The required return format below.

The sub-agent runs unattended — it has full tool access (Read, Write, Edit, Glob, Grep,
Bash) and DOES write the test and implementation source files. It just must not commit,
push, or touch the task list.

#### TDD Cycle for the sub-agent (include in the prompt)

**Before starting**: Read the requirements, the ADRs, `.agent_instructions/testing.md` and
`.agent_instructions/code_style.md`. This provides the context a fresh session would otherwise get
from conversation.

🔴 **RED — Write a Failing Test**
- Test naming: `When_[condition]_should_[expected_behavior]`; one test case per file named
  the same; Arrange/Act/Assert with explicit comments; highlight evident data.
- Test behavior, not implementation — public exports only; no mocks for isolation, use
  `InMemory*` implementations for I/O.
- TestDoubles: one class per file. Give each new test double its **own distinct request type** so
  assembly scans do not collide with other tests. Register any new closed generic with the test
  project's logging `Initializer.cs`.
- Write the test file at the path the task specifies.
- Run the verify command and confirm the test **FAILS** for the right reason (behavior doesn't
  exist yet). **Do not skip this.** Running unattended does not make this test-after.
- **If the test PASSES with no implementation change**: the behavior already exists. Either
  revise the test to verify something genuinely new, or RETURN status `ALREADY_COMPLETE`.

🟢 **GREEN — Make the Test Pass**
- Write the MINIMUM code to pass — no speculative code. Follow the task's implementation notes.
- Code style: .NET C# conventions (PascalCase public / camelCase private), ALL_CAPS
  constants, expression-bodied members for simple members, `readonly` where appropriate,
  nullable reference types enabled, Responsibility-Driven Design, avoid primitive obsession.
  XML documentation on new public members; MIT licence header on new files.
- Run the verify command and confirm the test **PASSES**.
- Run the **full suite** for the affected project(s) — `dotnet test {project}`, not just the
  filter — to catch regressions. If a regression appears, fix it before finishing.

🔵 **REFACTOR — Improve the Design**
- Tidy First: structural changes only, no behavior changes. Keep methods small and focused;
  reduce complexity; remove duplication; reveal intent.
- Re-run tests after refactoring to confirm no behavioral change.

#### Hard constraints for the sub-agent (include in the prompt)

- **NEVER** run `git commit`, `git add`, or `git push`.
- **NEVER** edit `tasks.md` or `.current-gear`.
- Only create/modify the test file(s) and implementation source file(s).
- Do not ask the user anything — this is unattended.
- If the task cannot be done as written (it contradicts an ADR, depends on something absent, or
  needs a design decision), RETURN `FAILED` with the reason. Do not improvise a different task.

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

### Step 5: Process the Result, Commit, and Mark (MAIN agent)

Read the sub-agent's returned result and act on its `STATUS`. **Every outcome produces the
two-commit shape**: the change, then the bookkeeping.

**GREEN:**
1. **Sanity-check the file lists first.** A `GREEN` result with **empty** `TEST_FILES` *and*
   `IMPL_FILES` is a contract violation — a passing task should have written source. Do NOT
   commit a `feat:` with no source changes. Send it back to the sub-agent and ask whether it
   meant `ALREADY_COMPLETE` (behavior already existed) before proceeding.
2. **Commit one — the change:**
   ```bash
   git add [TEST_FILES] [IMPL_FILES]
   git commit -m "feat: [DESCRIPTION]

   - Test: When_[condition]_should_[expected_behavior]
   - Implementation: [brief description]
   - Task: [task number]/[total] ([phase heading])

   Co-Authored-By: Claude Opus <noreply@anthropic.com>
   Co-Authored-By: Claude Sonnet <noreply@anthropic.com>"
   ```
   Use `refactor:` for a `STRUCTURAL` task and `docs:` for a `DOC` task.
   (Both models contributed: the main agent on **opus** orchestrated and committed; the
   sub-agent on **sonnet** wrote the test + implementation.)
3. **Commit two — the bookkeeping:** use Edit to change `- [ ]` to `- [x]` in `tasks.md`, then:
   ```bash
   git add specs/{current-spec}/tasks.md
   git commit -m "docs: mark task [N] complete

   Co-Authored-By: Claude Opus <noreply@anthropic.com>"
   ```
4. Count this task toward the run count.

**ALREADY_COMPLETE:**
1. The behavior already existed, so the sub-agent wrote **no** source — there is no change commit.
2. Mark the task complete (`- [ ]` → `- [x]`) and commit the tick **alone** (do NOT re-stage
   `[TEST_FILES]`/`[IMPL_FILES]`, which are empty):
   ```bash
   git add specs/{current-spec}/tasks.md
   git commit -m "docs: mark task [N] complete — behavior already existed

   - Task: [task number]/[total]

   Co-Authored-By: Claude Opus <noreply@anthropic.com>"
   ```
3. Count this task toward the run count.

**FAILED:**
1. Mark the task as failed: change `- [ ]` to `- [!]` in `tasks.md`.
2. Append a comment to the task line: ` <!-- RALPH-FAILED: [FAILURE_REASON] -->`
3. Commit the failure marker so the next iteration skips it:
   ```bash
   git add specs/{current-spec}/tasks.md
   git commit -m "chore: mark task [N] failed — [short reason]"
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

> **On editing an approved `tasks.md`.** `.tasks-approved` freezes the *content* of the task list —
> what the tasks are. Checkbox state (`[ ]` → `[x]` / `[!]`) is progress bookkeeping, not content,
> and both `/spec:implement` and this loop write it. Never reword, add, remove or reorder a task
> here; if a task is wrong, mark it `- [!]` and let the user decide.
>
> **Never stage `.current-gear`** — it is gitignored working state, not part of the change.

### Step 6: Check Continuation (self-driving loop)

This is the loop. After each task, check ALL of these stop conditions — if **any** holds, go
to Step 7 and stop; otherwise continue the loop:

1. **STOP file**: `RALPH_STOP` exists at repo root → stop (`STOPPED`). The unattended kill-switch.
2. **Gear downshift**: `.current-gear` now reads `review-before`, is gone, or is unparseable →
   stop (`DOWNSHIFTED`). Re-read the file **every** iteration; this is how the user takes back
   per-test review mid-phase without losing the work already done.
3. **Bound reached** (the bound chosen in Step 0) → stop (`BOUND_REACHED`):
   - **Tasks**: completed tasks this run (GREEN + ALREADY_COMPLETE) ≥ N
   - **Turns**: iterations attempted this run (including FAILED) ≥ N
   - **Budget**: output tokens consumed this run have reached ~N
4. **Scope exhausted**: the gear has a `scope:` and the next unchecked task is outside it → stop
   (`SCOPE_COMPLETE`).
5. **All done**: no more `- [ ]` tasks remain in `tasks.md` → stop (`ALL_DONE`).

If none hold, **continue to the next task**: return to **Step 1** with a **fresh sub-agent**
(fresh context). Drive this yourself — do not wait for the user.

**Self-pacing across context windows.** For a long run that would outgrow a single context
window, use `ScheduleWakeup` to re-enter the loop later (carry the same bound and the run
counters in the wake-up prompt) — this is the in-session equivalent of the old bash runner.
For a routine short run, just loop inline. Either way, the loop is unattended once Step 0 is
done.

**Interactive cancel.** Independent of `RALPH_STOP`, the user can press **Esc** while a
self-paced wake-up is pending to cancel the loop. Three mechanisms stop it: `RALPH_STOP` for a
fully unattended kill, `/spec:gear review-before` for a deliberate return to gated review, and Esc
for an at-the-keyboard cancel.

### Step 7: Print Summary

**ALWAYS** print this structured summary at the end, regardless of how you stopped:

```
=== RALPH SUMMARY ===
Spec: specs/{current-spec}/
Gear: review-after [scoped to "{phase}"]
Bound: tasks=N | turns=N | budget=N
Iterations this run: I
Tasks completed this run: N
Total tasks complete: X/Y
Tasks remaining: Z
Status: BOUND_REACHED | STOPPED | DOWNSHIFTED | SCOPE_COMPLETE | ALL_DONE
Next task: [task N — one-line description, or "none"]
=== END RALPH ===
```

Status meanings:
- `BOUND_REACHED`: Hit the chosen bound (tasks / turns / budget); more tasks may remain
- `STOPPED`: Halted due to `RALPH_STOP` file (or user Esc)
- `DOWNSHIFTED`: The gear was shifted to `review-before` — resume with `/spec:implement`; nothing
  completed was unwound
- `SCOPE_COMPLETE`: Every task under the gear's scoped phase is done; widen or move the scope with
  `/spec:gear` to continue
- `ALL_DONE`: No more tasks in `tasks.md`

## Important Reminders

- **AskUserQuestion is for Step 0 setup only** — never prompt the user once the loop is running
- **Run on opus with auto mode** for a true unattended run; the per-task sub-agent stays sonnet
- **One task list**: `tasks.md`. There is no `ralph-tasks.md` and nothing regenerates a task list
- **`.tasks-approved` is required** — this loop runs an approved list
- **Re-read the gear every iteration** — the downshift is the user's way back in
- **NEVER push to remote** — the human decides when to push
- **Two commits per task** — the change, then the checkbox tick
- **Never stage `.current-gear`** — gitignored working state
- **The gear removes the pause, not the discipline** — RED-first, full suite, conventions all hold
- Follow ALL guidelines in .agent_instructions/testing.md and .agent_instructions/code_style.md
