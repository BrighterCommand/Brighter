# Specification-Driven Development Commands

This directory contains Claude Code commands that implement a specification-driven development workflow for Brighter contributions. These commands help you follow Brighter's preferred contribution workflow: **Issue → Requirements → ADR(s) → Tasks → Tests → Code**.

## Overview

The spec commands provide a structured approach to designing and implementing features:

1. **Requirements**: Capture user needs and problem statements
2. **Design (ADRs)**: Document architectural decisions (can have multiple ADRs per requirement)
3. **Tasks**: Break down implementation into actionable steps
4. **Implementation**: Follow TDD to write tests and code

## Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                      Specification Workflow                      │
└─────────────────────────────────────────────────────────────────┘

 GitHub Issue
      │
      ▼
 Requirements.md ────────► /spec:requirements [issue-number]
      │                   /spec:approve requirements
      │
      ▼
 ADR(s) in docs/adr/ ───► /spec:design [focus-area]
      │                   /spec:review design [adr-number]
      │                   /spec:approve design [adr-number]
      │                   (Repeat for multiple architectural decisions)
      │
      ▼
 Choose by certainty ────► (prompted at /spec:approve design)
      │
      ├── Attended ──────► /spec:tasks
      │   (review each      /spec:approve tasks
      │    test)            /spec:implement   (sonnet; TDD: Tests → Code)
      │
      └── Unattended ────► /spec:ralph-tasks       (standalone, from approved design)
          (review in        /spec:ralph-implement  (opus + auto mode, self-driving loop)
           batches)
      │
      ▼
 Pull Request
```

**The certainty fork.** After the design is approved, you pick *one* of two paths — and
`/spec:approve design` prompts you to choose:

- **Attended** (`/spec:tasks` → `/spec:implement`): a strict Red → **user approval** → Green
  → Refactor loop in the main agent on **sonnet**. Every test is reviewed in the IDE before
  implementation. Use when the work is uncertain.
- **Unattended** (`/spec:ralph-tasks` → `/spec:ralph-implement`): `ralph-tasks.md` is
  generated **directly from the approved design** (no `tasks.md`, no per-test gates), then a
  self-driving loop on **opus** under **auto mode** delegates each task to a **sonnet**
  sub-agent. Reviewed in batches rather than per test. Use when the work is well-understood.

## Sub-agents & model policy

Several commands delegate their reasoning-heavy work to a **sub-agent** (launched via the
`Agent` tool) rather than doing it inline. This keeps the main conversation's context clean
and gives the heavy work a focused, single-purpose context.

**The convention** (modelled on `/spec:review`):

1. **The main agent gathers inputs.** A sub-agent starts with a clean context — it only
   knows what is in its prompt. The command reads the needed files (and runs `gh`/`git`)
   first, then passes the text or paths.
2. **Launch `Agent`** with an explicit `subagent_type` and `model`:
   - **Planning commands** (`/spec:design`, `/spec:tasks`, `/spec:ralph-tasks`) use
     `subagent_type: "Plan"`. `Plan` has all tools **except** `Agent`, `ExitPlanMode`,
     `Edit`, `Write`, and `NotebookEdit` — so it can Read/Glob/Grep/Bash/WebFetch/WebSearch
     but has no file-editing tool. That makes it much **harder** for the sub-agent to
     accidentally write the spec file (no `Write`/`Edit`/`NotebookEdit`) than relying on the
     prompt alone, though it is not an absolute lock — it still has `Bash`, so a determined
     `echo >`/`tee`/`sed -i` could write. The prompt still instructs it to return as text;
     `Plan` simply removes the easy, accidental path to writing.
   - **`/spec:requirements`** also uses `subagent_type: "Plan"` — drafting requirements is
     read-only (returns text), so `Plan` fits, and `Plan` has no `AskUserQuestion` so the
     sub-agent cannot prompt the user.
   - **`/spec:review`** uses `subagent_type: "general-purpose"` (adversarial reasoning that
     needs no source mutation).
   - **`/spec:ralph-implement`** uses `subagent_type: "general-purpose"` because its
     sub-agent genuinely *writes* source files.
3. **The main agent owns all user interaction.** A sub-agent is one-shot — once launched it
   runs to completion and returns; it cannot pause to ask the user anything. So before
   launching, the main agent clarifies any ambiguous or under-specified inputs with the user
   via `AskUserQuestion`, then launches the sub-agent with the clarified inputs folded in.
   Every delegated command keeps `AskUserQuestion` in its own `allowed-tools` for this. The
   sub-agents never prompt: the `Plan`-based commands (`requirements`, `design`, `tasks`,
   `ralph-tasks`) have no `AskUserQuestion` so they *structurally* can't, and the
   `general-purpose` `review` sub-agent is explicitly instructed not to. **Exception:**
   `/spec:ralph-implement` runs fully **unattended** — neither its main agent nor its
   sub-agent prompts the user (it has no `AskUserQuestion` at all).
4. **The sub-agent RETURNS its artifact as text** — it does *not* write the spec file. The
   one exception is `/spec:ralph-implement`, where the sub-agent must write the test and
   implementation source files (it still never commits or edits the task list).
5. **The main agent validates** the returned output against a checklist, then writes the
   file and does all bookkeeping (approval markers, `.adr-list`, git, next-steps).

**Model policy** — reasoning vs. implementation:

| Command | Sub-agent (type) | Model | Rationale |
|---------|-----------|-------|-----------|
| `/spec:requirements` | Yes — `Plan` (read-only drafting) | **opus** | Planning / analysis |
| `/spec:design` | Yes — `Plan` (read-only) | **opus** | Architecture / design |
| `/spec:tasks` | Yes — `Plan` (read-only) | **opus** | Planning / coverage mapping |
| `/spec:ralph-tasks` | Yes — `Plan` (read-only) | **opus** | Planning / decomposition |
| `/spec:review` | Yes — `general-purpose` | **opus** | Adversarial reasoning |
| `/spec:ralph-implement` (orchestrator) | — (the loop itself) | **opus** | Cheap bookkeeping + **required for auto mode** |
| `/spec:ralph-implement` (per-task sub-agent) | Yes — `general-purpose` (writes source) | **sonnet** | Mechanical TDD implementation, kept off the opus loop context for cost |
| `/spec:implement` | No | **sonnet** (Step 0 prompts to switch) | Implementation work; runs in the main agent, so set the session model |
| `/spec:new`, `/spec:switch`, `/spec:approve`, `/spec:status` | No | — | Mechanical bookkeeping |

The planning commands use the `Plan` agent so the "return as text, don't write the file"
rule is much harder to violate accidentally (it has no `Write`/`Edit`/`NotebookEdit`; the
prompt still forbids writing via `Bash`). `/spec:ralph-implement` keeps `general-purpose`
because its sub-agent must write source.

`/spec:ralph-implement` runs **two models on purpose**: the orchestrator loop on **opus**
(required for **auto mode**, and the policy for the unattended path) does only cheap
bookkeeping — STOP-file check, task selection, marking checkboxes, committing, counting — while
each task's actual test + implementation is delegated to a **sonnet** sub-agent. That keeps the
expensive per-task churn on the cheaper model and out of the opus loop's context, lowering cost
without taking the orchestrator off opus.

`/spec:implement` is deliberately **not** delegated: its per-behavior
Red → user-approval → Green → Refactor loop is interactive, and the mandatory approval gate
must run in the main agent where it can reach the user. Because there is no sub-agent to
assign a model to, run the command itself on **sonnet** (the session model) — it is
implementation work. **Step 0 of `/spec:implement` actively checks the session model and
prompts you to switch to sonnet if you are on another model** (e.g. opus). This guidance is
repeated at the top of `implement.md` itself, since that is where the user reads it.

## Commands

### `/spec:new <feature-name>`

Create a new specification for a feature.

```bash
/spec:new kafka-dead-letter-queue
```

Creates:
- `specs/NNNN-kafka-dead-letter-queue/` directory
- Updates `specs/.current-spec` to track active spec
- Creates initial README.md in spec directory

---

### `/spec:requirements [issue-number]`

Create or update requirements specification for the current spec.

**With GitHub Issue:**
```bash
/spec:requirements 123
```
- Pulls issue content from GitHub
- Creates `requirements.md` based on issue
- Stores issue number in `.issue-number` file
- Optionally adds comment to issue linking to requirements

**Without Issue:**
```bash
/spec:requirements
```
- Creates template `requirements.md` for you to fill in
- Optionally creates new GitHub issue

**Features:**
- Creates `specs/` directory if it doesn't exist
- Shows next ADR number to use
- Offers to create feature branch (or use current branch)
- Template focuses on user requirements (technical details go in ADRs)

---

### `/spec:design [focus-area]`

Create an Architecture Decision Record (ADR) for a specific architectural decision.

**Multiple ADRs per Requirement:**
You can (and should) create multiple focused ADRs for different aspects of a requirement. Each ADR should address one specific architectural decision.

```bash
# First ADR - message serialization
/spec:design message-serialization

# Second ADR - error handling strategy
/spec:design error-handling

# Third ADR - persistence layer
/spec:design persistence-strategy
```

**Features:**
- Creates ADR in `docs/adr/NNNN-{focus-area}.md`
- Tracks all ADRs in `specs/{current-spec}/.adr-list`
- Shows existing ADRs to avoid duplication
- Links back to requirements specification
- Uses standard ADR template with sections:
  - Status (Proposed/Accepted)
  - Context (the specific architectural problem)
  - Decision (what we're doing and why)
  - Consequences (positive, negative, risks)
  - Alternatives Considered
  - References

**Best Practices:**
- Keep each ADR focused on ONE architectural decision
- Create separate ADRs for different concerns (e.g., data model, API design, error handling)
- First ADR should be the first commit on your feature branch
- Include diagrams (ASCII art or mermaid) where helpful

---

### `/spec:approve <phase> [adr-number]`

Approve a specification phase or specific ADR.

**Approve Requirements:**
```bash
/spec:approve requirements
```
- Creates `.requirements-approved` marker
- Allows progression to design phase

**Approve All ADRs:**
```bash
/spec:approve design
```
- Updates Status from "Proposed" to "Accepted" in ALL ADRs for current spec
- Creates `.design-approved` marker
- **Prompts you to choose the implementation path** (the certainty fork): the **attended**
  path (`/spec:tasks` → `/spec:implement`, review each test) or the **unattended** path
  (`/spec:ralph-tasks` → `/spec:ralph-implement`, review in batches). Either path can start
  straight from the approved design.

**Approve Specific ADR:**
```bash
/spec:approve design 0043
```
- Updates Status to "Accepted" only for ADR 0043
- Useful for incremental approval as ADRs are reviewed
- Once all ADRs approved, can create `.design-approved` marker

**Approve Tasks:**
```bash
/spec:approve tasks
```
- Creates `.tasks-approved` marker
- Allows progression to implementation

---

### `/spec:review [phase] [adr-number]`

Review the current specification phase or specific ADR.

**Auto-detect Current Phase:**
```bash
/spec:review
```
Reviews the first unapproved phase automatically.

**Review Requirements:**
```bash
/spec:review requirements
```
Shows requirements.md with checklist.

**Review All ADRs:**
```bash
/spec:review design
```
- Shows all ADRs for current spec
- Displays status (Proposed/Accepted) for each
- Provides summary: X ADRs total (Y approved, Z proposed)

**Review Specific ADR:**
```bash
/spec:review design 0043
```
Shows only ADR 0043 with detailed checklist.

---

### `/spec:tasks`

Create implementation task list based on approved design.

```bash
/spec:tasks
```

Creates `tasks.md` with:
- Overview and task breakdown
- Phases: Foundation, Core, Testing, Deployment
- Checkboxes for tracking progress
- Task dependencies
- Risk mitigation tasks

**Requirements:**
- Design must be approved (`.design-approved` exists)
- All ADRs should be reviewed and approved

---

### `/spec:status`

Show status of all specifications.

```bash
/spec:status
```

Displays:
- All specification directories
- Current active spec (marked with *)
- Phase completion status for each spec
- ADR approval status (Proposed/Accepted)
- Linked GitHub issues
- Task progress (if applicable)
- Recommended next action

**Example Output:**
```
Specification Status Report
===========================

Active Spec: specs/0001-kafka-dlq/ (marked with *)

* specs/0001-kafka-dlq/
  Issue: #123

  Requirements: ✓ Approved

  Design (ADRs):
    - docs/adr/0042-kafka-dlq-message-serialization.md [Accepted]
    - docs/adr/0043-kafka-dlq-error-handling.md [Proposed]
    - docs/adr/0044-kafka-dlq-persistence.md [Accepted]
  Status: ⏳ In Progress (1 ADR pending approval)

  Tasks: ✓ Approved
    Progress: 15/30 tasks complete (50%)

  Next Action: Approve ADR 0043 or begin implementation
```

---

### `/spec:switch <spec-name>`

Switch to a different specification.

```bash
/spec:switch 0002-another-feature
```

Updates `specs/.current-spec` to the specified spec directory.

---

### `/spec:implement [task-number]`

Begin TDD implementation of approved specification.

```bash
# Implement all tasks
/spec:implement

# Implement specific task
/spec:implement 3
```

**Requirements:**
- Tasks must be approved (`.tasks-approved` exists)
- All ADRs must be approved (Status: Accepted)

**Strict TDD Workflow with Approval Gates:**

The implement command follows a rigorous Red-Green-Refactor cycle:

**🔴 RED Phase - Write Failing Test:**
1. Identifies the next behavior to implement
2. Writes a failing test following [.agent_instructions/testing.md](../../../.agent_instructions/testing.md):
   - Test naming: `When_[condition]_should_[expected_behavior]`
   - File per test: `When_[condition]_should_[expected_behavior].cs`
   - Arrange/Act/Assert structure with explicit comments
   - Evident Data pattern
   - Tests public exports only (no private/internal methods)
   - Uses InMemory* implementations for I/O (no mocks for isolation)
3. Runs test to verify it fails correctly
4. Shows test to user with explanation

**✅ USER APPROVAL - Critical Gate:**
- **MUST get explicit user approval before writing implementation**
- Uses AskUserQuestion to request approval
- If changes requested, modifies test and asks again
- **No implementation code written without approval**

**🟢 GREEN Phase - Make Test Pass:**
1. Writes minimum code to make test pass
2. Follows [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md):
   - .NET C# naming conventions
   - Responsibility Driven Design
   - Avoid primitive obsession
   - Keep methods small and focused
3. Runs test to verify it passes
4. Runs all tests to ensure no regressions
5. Shows implementation and results

**🔵 REFACTOR Phase - Improve Design:**
1. Reviews code for design improvements
2. Applies "Tidy First" principles (structural changes only)
3. Keeps methods small, single responsibility
4. Uses expressive types instead of primitives
5. Runs all tests after each refactoring
6. Shows refactored code with explanation

**After Each Cycle:**
- Commits changes with descriptive message
- Updates tasks.md to check off completed task
- Asks user: continue to next test or review?

**Example Session:**
```
🔴 RED: Writing test When_message_is_invalid_should_send_to_dead_letter_queue
      [Shows test] Test fails: "Method not found" ✓

✅ APPROVAL: Should I proceed to make this test pass?
      User: Yes

🟢 GREEN: Adding SendToDeadLetterQueue method
      [Shows code] Test passes ✓ All tests pass ✓

🔵 REFACTOR: Extracting validation logic
      [Shows refactoring] All tests still pass ✓

✓ Committed: feat: add dead letter queue for invalid messages
```

---

### `/spec:ralph-tasks`

Generate `ralph-tasks.md` for unattended TDD implementation. **Standalone** — the unattended
peer of `/spec:tasks`, derived **directly from the approved design** (requirements + ADRs). It
does **not** require `tasks.md` or `.tasks-approved`.

```bash
/spec:ralph-tasks
```

Creates `specs/{current-spec}/ralph-tasks.md`, formatted for unattended execution:

- **No approval gates**: No `⛔ STOP HERE` or `/test-first` directives
- **RALPH-VERIFY**: Each task includes an exact `dotnet test --filter` command
- **References**: Each task lists files/ADRs to read (self-contained for fresh context)
- **Strict atomicity**: One behavior per task, ~200 lines max, ordered by dependency

**Requirements:**
- Design must be approved (`.design-approved` exists)
- All ADRs must be approved (Status: Accepted)

> Pick *either* `/spec:tasks` (attended) *or* `/spec:ralph-tasks` (unattended) after design
> approval — they are alternative branches, not sequential steps.

**Ralph task format:**
```markdown
- [ ] **[Brief behavior description]**
  - **Behavior**: [Precise behavioral specification]
  - **Test file**: `tests/[Project]/[When_condition_should_behavior.cs]`
  - **Test should verify**:
    - [Point 1]
    - [Point 2]
  - **Implementation files**:
    - `src/[Project]/[File.cs]` - [What to add/change]
  - **RALPH-VERIFY**: `dotnet test tests/[Project]/ --filter "FullyQualifiedName~When_condition_should_behavior"`
  - **References**: [ADR numbers, requirement sections, existing code files]
```

---

### `/spec:ralph-implement [count]`

Unattended TDD implementation from `ralph-tasks.md` via a **self-driving loop**. Run it on
**opus** with **auto mode** enabled for a true unattended run.

```bash
# Ask the run bound up front, then loop unattended
/spec:ralph-implement

# Shortcut: pre-set the tasks bound to 3 (skips the bound prompt)
/spec:ralph-implement 3
```

**Up-front setup (Step 0, the only interactive part):**
- Advises that the orchestrator should be on **opus** and that **auto mode** should be on
  (auto mode is a permission mode set in Claude Code settings / `CLAUDE_CODE_ENABLE_AUTO_MODE`,
  Opus-gated — the command can't toggle it; it advises and proceeds).
- Asks (via `AskUserQuestion`) which **run bound** to use — *unless* a `count` was passed,
  which sets the tasks bound directly:
  - **Tasks** — stop after N tasks complete (the `count` argument)
  - **Turns** — stop after N loop iterations attempted (failures included)
  - **Budget** — stop after ~N output tokens consumed

**Two models on purpose:** the **opus** orchestrator does only bookkeeping; each task's test +
implementation is delegated to a **sonnet** sub-agent (cheaper, and kept off the opus loop
context). The sub-agent never commits, pushes, or edits the task files.

**Loop per task:** check `RALPH_STOP` → select next `- [ ]` task → delegate 🔴 Red → 🟢 Green
→ 🔵 Refactor to a sonnet sub-agent → orchestrator marks the checkbox and commits → check
continuation → repeat. Long runs can self-pace across context windows with `ScheduleWakeup`.

**Stop mechanisms:**
- The chosen **bound** (tasks / turns / budget)
- `RALPH_STOP` file at repo root — the unattended kill-switch (`touch RALPH_STOP` from another
  terminal); halts after the current task
- **Esc** — cancel a pending self-paced wake-up at the keyboard
- Automatically stops when all tasks complete

**Error handling:** Failed tasks are marked `- [!]` with an explanation and skipped.

**Requirements:**
- Design must be approved (`.design-approved` exists)
- `ralph-tasks.md` must exist (run `/spec:ralph-tasks` first)
- Recommended: session on **opus** with **auto mode** enabled

---

### Running the unattended loop

The loop runs **in-session** — no external bash runner. Built-in **auto mode** removes the
per-action permission prompts and the self-driving loop (optionally self-paced with
`ScheduleWakeup`) replaces the old `scripts/ralph.sh` overnight runner.

```bash
# 1. Spec workflow up to an APPROVED DESIGN (no tasks step needed for this path)
/spec:requirements 123
/spec:approve requirements
/spec:design message-serialization
/spec:approve design          # ← prompts you to pick the unattended path

# 2. Generate ralph-tasks directly from the approved design
/spec:ralph-tasks

# 3. Review ralph-tasks.md in your IDE

# 4. Switch to opus and enable auto mode, then run the loop
/model opus
/spec:ralph-implement         # choose tasks / turns / budget when prompted

# 5. Stop the loop (if needed)
touch RALPH_STOP              # unattended kill-switch (halts after current task)
#   …or press Esc to cancel a pending self-paced wake-up
```

Optionally drive it with the built-in `/loop` instead, e.g. `/loop /spec:ralph-implement`
(self-paced) — the same command, repeated by `/loop`.

---

## Complete Example

Here's a complete workflow for adding a new feature:

```bash
# 1. Create new spec or link to existing issue
/spec:requirements 123

# Review and edit requirements.md as needed
# Then approve
/spec:approve requirements

# 2. Create multiple focused ADRs
/spec:design message-serialization
# Edit the ADR with architectural decisions
# Review and approve
/spec:review design 0042
/spec:approve design 0042

/spec:design error-handling-strategy
# Edit the ADR
/spec:review design 0043
/spec:approve design 0043

/spec:design persistence-layer
# Edit the ADR
/spec:review design 0044
/spec:approve design 0044

# Approve all remaining ADRs (if any)
/spec:approve design

# 3. Create and approve tasks
/spec:tasks
/spec:review tasks
/spec:approve tasks

# 4. Check overall status
/spec:status

# 5. Begin implementation
/spec:implement
```

## File Structure

```
Brighter/
├── specs/
│   ├── .current-spec                      # Tracks active spec
│   └── 0001-feature-name/
│       ├── .issue-number                  # GitHub issue number
│       ├── .requirements-approved         # Approval marker
│       ├── .design-approved               # Approval marker
│       ├── .tasks-approved                # Approval marker
│       ├── .adr-list                      # List of associated ADRs
│       ├── requirements.md                # User requirements
│       ├── tasks.md                       # Implementation tasks
│       ├── ralph-tasks.md                 # Unattended TDD tasks (optional)
│       └── README.md                      # Spec overview
├── docs/
│   └── adr/
│       ├── 0001-record-architecture-decisions.md
│       ├── 0042-feature-aspect-one.md     # Focused ADR
│       ├── 0043-feature-aspect-two.md     # Focused ADR
│       └── 0044-feature-aspect-three.md   # Focused ADR
```

## Best Practices

### Requirements
- Frame problem as user story: "As a [user] I want [capability] so that [benefit]"
- Focus on WHAT users need, not HOW to implement
- Keep it concise - technical details go in ADRs
- Link to GitHub issue for traceability

### ADRs (Architecture Decision Records)
- **One architectural decision per ADR** - stay focused
- Common ADR topics:
  - Message serialization strategy
  - Error handling approach
  - Persistence/storage mechanism
  - API design and contracts
  - Performance optimization strategy
  - Security model
  - Testing approach
- Focus on WHY, not just WHAT
- Document alternatives considered and why they were rejected
- Use diagrams to illustrate architecture
- Cross-reference related ADRs
- First ADR should be first commit on feature branch
- Create draft PR with ADRs for early feedback

### Tasks
- Break down into small, testable increments
- Follow TDD: write tests before implementation
- Identify dependencies between tasks
- Include risk mitigation tasks

### Git Workflow
1. Create feature branch (or use existing)
2. Commit first ADR: `git commit -m "docs: add ADR for message serialization"`
3. Commit subsequent ADRs: `git commit -m "docs: add ADR for error handling"`
4. Create draft PR with ADRs for review
5. Implement incrementally with TDD
6. Update ADR status to "Accepted" when approved

## Why Multiple ADRs?

A single requirement might involve several distinct architectural decisions:

**Example: Kafka Dead Letter Queue Feature**

Instead of one large ADR covering everything, create focused ADRs:

1. **ADR 0042: Message Serialization** - How to serialize/deserialize messages for DLQ
2. **ADR 0043: Error Classification** - How to determine which errors send to DLQ
3. **ADR 0044: Persistence Strategy** - How to store DLQ metadata
4. **ADR 0045: Retry Mechanism** - How to handle retries from DLQ

**Benefits:**
- Each ADR is focused and easier to review
- Can approve ADRs independently as they're ready
- Easier to reference specific decisions later
- Can supersede individual decisions without affecting others
- Better traceability of architectural evolution

## Contributing to Brighter

These commands align with Brighter's contribution guidelines (see [CONTRIBUTING.md](../../../CONTRIBUTING.md)):

- ADRs are required for new capabilities
- ADRs should focus on WHY over implementation details
- First commit should include ADR(s)
- Draft PR with ADRs allows early feedback
- Follow TDD practices
- Maintain code quality standards

## Support

For questions or issues with these commands:
- Check [CONTRIBUTING.md](../../../CONTRIBUTING.md) for contribution guidelines
- Review existing ADRs in [docs/adr/](../../../docs/adr/) for examples
- See [Brighter Documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation/)
