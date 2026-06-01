---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob, Agent, AskUserQuestion
description: Create implementation task list
---

## Context

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → **Tasks** → Tests → Code

**Sub-agent**: Drafting the task list is delegated to a sub-agent
(`subagent_type: "Plan"`, **`model: "opus"`**). `Plan` has no `Write`/`Edit`/`NotebookEdit`,
which makes it much harder to accidentally write the file (the prompt still forbids writing
via `Bash`), while still allowing Read/Glob/Grep to verify file paths. The sub-agent reads the requirements and ADRs and
RETURNS the task list as text. The main agent validates coverage and writes `tasks.md`. See
`.claude/commands/spec/README.md` → "Sub-agents & model policy".

## Your Task

### Step 1: Gather Context

1. Read `specs/.current-spec` to determine the active specification directory.
2. Verify design is approved: check for `.design-approved` in the spec directory. If
   missing, tell the user to run `/spec:approve design` first and exit.
3. Read `specs/{current-spec}/requirements.md` (the FRs/ACs the tasks must cover).
4. Read `specs/{current-spec}/.adr-list` and each ADR from `docs/adr/` (the design
   decisions the tasks must implement).

If `requirements.md` or any listed ADR is missing, stop and tell the user. Do NOT launch
the sub-agent with missing inputs.

### Step 2: Launch Sub-Agent to Draft the Task List

**Verify inputs with the user before launching (MAIN agent).** The `Plan` sub-agent is
one-shot and has no `AskUserQuestion` — it cannot ask the user anything once launched. So
before launching, review the requirements and ADRs for ambiguity or open decisions that
affect decomposition (how granular the tasks should be, sequencing/dependencies, scope
boundaries between what's in and out of this spec) and resolve them with the user via
`AskUserQuestion`. Then launch the sub-agent with the clarified inputs folded in. All user
interaction stays in the main agent — never the sub-agent.

Launch an `Agent` with `subagent_type: "Plan"` and **`model: "opus"`**. The
prompt MUST include:

1. The full text of `requirements.md`.
2. The full text of each ADR (or their paths to read).
3. The task-drafting rules and the **mandatory TDD task template** below.
4. An explicit instruction: **RETURN the complete task list as markdown text. Do NOT write
   any file.** The sub-agent may use Read/Glob/Grep to verify only the **existing** paths it
   cites — code/classes and ADRs in a task's `References` section. The **new** test and
   implementation files a task plans to create do NOT exist yet and MUST NOT be Glob-verified
   or flagged as missing; they are expected to be absent.

#### Task-drafting rules (include in the sub-agent prompt)

- Produce `tasks.md` content with:
  - A detailed task list with markdown checkboxes (`- [ ] Task description`)
  - Explicit task dependencies
  - Risk-mitigation tasks
- Each task MUST be specific and actionable.
- A task MUST represent implementing a **behavior**, NOT an implementation detail.
- Organize tasks to enable incremental development and testing.
- Order tasks so dependencies come first; structural/tidy tasks before behavioral ones.

#### CRITICAL: TDD Task Format (include verbatim in the sub-agent prompt)

**MANDATORY**: When creating TEST tasks, you MUST format them to enforce `/test-first` skill usage.

##### Task Template

```markdown
- [ ] **TEST + IMPLEMENT: [Behavior description]**
  - **USE COMMAND**: `/test-first [behavior description for command]`
  - Test location: "[test directory path]"
  - Test file: `[When_condition_should_behavior.cs]`
  - Test should verify:
    - [verification point 1]
    - [verification point 2]
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - [implementation point 1 with specific file/line numbers where applicable]
    - [implementation point 2]
```

##### Example Task

```markdown
- [ ] **TEST + IMPLEMENT: Rejection with no channels configured acknowledges message**
  - **USE COMMAND**: `/test-first when rejecting message with no channels configured should acknowledge and log`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`
  - Test should verify:
    - Consumer created without DLQ or invalid message routing keys
    - Message rejected with DeliveryError reason
    - Message acknowledged (can consume next message)
    - Warning logged
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In KafkaMessageConsumer.Reject() check if both producers are null
    - Log warning via NoChannelsConfiguredForRejection
    - Acknowledge message anyway to prevent reprocessing
    - Return true
```

##### Why This Format?

1. **Visible command**: The `/test-first` command is prominently displayed
2. **Stop sign**: The ⛔ emoji and "STOP HERE" makes the approval gate unmissable
3. **Single task**: Combines TEST + IMPLEMENT so workflow is clear
4. **Complete context**: All details needed for test and implementation
5. **IDE review**: Explicitly states user will review in IDE, not CLI

##### DO NOT Format Tasks Like This

❌ **BAD - Separates test and implementation:**
```markdown
- [ ] **TEST: Rejection with no channels configured**
  - Write test...
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: No channels configured behavior**
  - Handle null producers...
```

This format allows Claude to skip the approval by treating them as independent tasks.

#### Coverage cross-reference (include in the sub-agent prompt)

- Map EVERY functional requirement (FR-N) from `requirements.md` to at least one task.
  List any FR with no task.
- Map EVERY ADR decision to at least one task. List any decision with no task.
- Flag any task that does not trace back to a requirement or ADR decision (scope creep).

### Step 3: Validate Coverage and Write the File

After the sub-agent returns:

1. **Validate** before writing. The sub-agent owns the coverage mapping (its "Coverage
   cross-reference" output); the main agent **sanity-checks** that report rather than
   re-deriving it:
   - The sub-agent's coverage report lists every FR and every ADR decision against a task,
     with no gaps flagged. Spot-check a couple of entries against `requirements.md` or the
     ADRs rather than re-mapping the whole set; if the report flags any uncovered FR/decision
     or is missing, send it back to the sub-agent. This spot-check is deliberately a
     **sampling check, not full verification** — it is not expected to catch every
     inconsistency or hallucinated coverage claim. The exhaustive adversarial coverage review
     is `/spec:review tasks` (Step 4), which the user is prompted to run before approval; that
     is where any remaining gaps are caught.
   - Each behavioral task uses the `TEST + IMPLEMENT` template with `/test-first` and the
     `⛔ STOP HERE` gate — none are split into separate TEST/IMPLEMENT tasks.
   - No task is an implementation detail rather than a behavior.
   - If coverage is incomplete or a task is malformed, ask the sub-agent to revise (or fix
     it yourself) before writing.

2. **Write** the validated task list to `specs/{current-spec}/tasks.md` using the Write tool.

### Step 4: Next Steps

Remind the user to:
- Review `tasks.md`
- Run `/spec:review tasks` for an adversarial coverage review, then
- `/spec:approve tasks` when ready to begin implementation with `/spec:implement`.
