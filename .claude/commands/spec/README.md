# Specification-Driven Development Commands

This directory contains Claude Code commands that implement a specification-driven development workflow for Brighter contributions. These commands help you follow Brighter's preferred contribution workflow: **Issue → Requirements → ADR(s) → Tasks → Tests → Code**.

## Overview

The spec commands provide a structured approach to designing and implementing features:

1. **Requirements**: Capture user needs and problem statements
2. **Design (ADRs)**: Document architectural decisions (can have multiple ADRs per requirement)
3. **Tasks**: Break down implementation into actionable steps
4. **Implementation**: Follow TDD to write tests and code, in whichever **review gear** the work
   currently warrants — shifted with `/spec:gear`, armed by default

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
 tasks.md ──────────────► /spec:tasks
      │                   /spec:review tasks
      │                   /spec:approve tasks
      │
      ▼
 Pick a driver, ────────► /spec:implement        (sonnet; one task at a time)
 shift gear as you go     /spec:ralph-implement  (opus + auto mode; unattended loop)
      │                        ▲
      │                        └── /spec:gear  shifts the review gear, either way
      ▼
 Pull Request
```

**One task list, one gear lever.** There is a single task list — `tasks.md` — and both drivers run
it. What changes between "attended" and "unattended" is the **review gear**, and the gear is
shiftable at any point in either direction ([ADR 0071](../../../docs/adr/0071-tdd-review-gear.md)):

| Gear | Gate | Meaning |
|------|------|---------|
| `review-before` | ✅ armed | Every test reviewed in the IDE before implementation. **The default.** |
| `review-after` | ➖ not armed | RED still proved first; the work is reviewed as a batch afterwards. |

**Gears, not paths.** You select a gear for the certainty you have and the blast radius you face.
Early in a spec, while the design is still moving, run `review-before`. Later, on a run of
near-identical tasks whose shape has been approved six times, upshift. The moment the work starts
producing tests you would not have approved, downshift — mid-phase, with nothing unwound.

The two drivers:

- **`/spec:implement`** — one task at a time in the main agent on **sonnet**. Honours whatever gear
  is set.
- **`/spec:ralph-implement`** — a self-driving unattended loop over the **same** `tasks.md`, on
  **opus** under **auto mode**, delegating each task to a **sonnet** sub-agent. Always
  `review-after`; a downshift stops it cleanly.

Neither regenerates a task list, and switching between them costs nothing.

## Sub-agents & model policy

Several commands delegate their reasoning-heavy work to a **sub-agent** (launched via the
`Agent` tool) rather than doing it inline. This keeps the main conversation's context clean
and gives the heavy work a focused, single-purpose context.

**The convention** (modelled on `/spec:review`):

1. **The main agent gathers inputs.** A sub-agent starts with a clean context — it only
   knows what is in its prompt. The command reads the needed files (and runs `gh`/`git`)
   first, then passes the text or paths.
2. **Launch `Agent`** with an explicit `subagent_type` and `model`:
   - **Planning commands** (`/spec:design`, `/spec:tasks`) use
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
   sub-agents never prompt: the `Plan`-based commands (`requirements`, `design`, `tasks`)
   have no `AskUserQuestion` so they *structurally* can't, and the
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
| `/spec:review` | Yes — `general-purpose` | **opus** | Adversarial reasoning |
| `/spec:ralph-implement` (orchestrator) | — (the loop itself) | **opus** | Cheap bookkeeping + **required for auto mode** |
| `/spec:ralph-implement` (per-task sub-agent) | Yes — `general-purpose` (writes source) | **sonnet** | Mechanical TDD implementation, kept off the opus loop context for cost |
| `/spec:implement` | No | **sonnet** (Step 0 prompts to switch) | Implementation work; runs in the main agent, so set the session model |
| `/spec:new`, `/spec:switch`, `/spec:approve`, `/spec:status`, `/spec:gear` | No | — | Mechanical bookkeeping |

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
Red → user-approval → Green → Refactor loop is interactive, and the approval gate — armed
by default — must run in the main agent where it can reach the user. Because there is no sub-agent to
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
- Points you at `/spec:tasks` — there is one task list and one route to it. Attended vs.
  unattended is not a fork here; it is a **gear** you pick (and change) at implementation time
  with `/spec:gear`.

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
- Allows progression to implementation with either driver (`/spec:implement` or
  `/spec:ralph-implement`)
- Freezes the **content** of `tasks.md`. Checkbox state (`[ ]` → `[x]`/`[!]`) is progress
  bookkeeping and both drivers write it; rewording, adding, removing or reordering tasks after
  this point needs a fresh review

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

### `/spec:implement [task-number] [--review-before|--review-after]`

Begin TDD implementation of approved specification, one task at a time.

```bash
# Implement all tasks
/spec:implement

# Implement specific task
/spec:implement 3

# Shift the gear and start in the same gesture (sugar for /spec:gear)
/spec:implement --review-after
```

**Requirements:**
- Tasks must be approved (`.tasks-approved` exists)
- All ADRs must be approved (Status: Accepted)

**Gear-aware.** The command resolves the review gear before every task — so a shift made from
another terminal takes effect at the next task, in either direction. It announces the resolved gear
and the reason it was shifted before starting.

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

**✅ USER APPROVAL - the gate (armed by default):**
- In `review-before` (the default): **MUST get explicit user approval before writing
  implementation**. Uses AskUserQuestion; if changes are requested, modifies the test and asks
  again. **No implementation code written without approval**
- In `review-after`: the pause is skipped — and *only* the pause. RED must still be proved first,
  and the run says so in one line. It still stops and asks if the test looks wrong, needs a design
  decision, or duplicates an existing test

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
✓ Committed: docs: mark task 7 complete
```

**Two commits per task, in both gears**: a `feat:`/`test:`/`fix:`/`refactor:` commit for the change,
then a separate `docs:` commit ticking the task off in `tasks.md`.

---

### `/spec:gear [review-before|review-after] ["phase"] [--because "..."]`

Report or shift the **review gear** — whether `/test-first`'s approval gate is armed for the
current spec. See [ADR 0071](../../../docs/adr/0071-tdd-review-gear.md).

```bash
/spec:gear                                        # what gear am I in, and why?
/spec:gear review-after "Phase 5 — Provider rejection tests" \
    --because "Phase 4's six tests were all approved unchanged; the shape is settled"
/spec:gear review-before --because "the last two tests asserted the wrong thing"
```

**The gear file** — `specs/{current-spec}/.current-gear`, **gitignored**:

```
# Working state — untracked. Shift with /spec:gear.
gear: review-after
scope: Phase 5 — Provider rejection tests
driver: ralph
shifted: 2026-09-13 — Phase 4's six tests were all approved unchanged; the shape is settled
```

| Field | Required | Meaning |
|-------|----------|---------|
| `gear` | yes | `review-before` \| `review-after` |
| `scope` | no | A section of `tasks.md` — a heading's text (any depth) or a `tasks N-M` range for flat lists. Absent = spec-wide. |
| `driver` | no | `implement` \| `ralph` |
| `shifted` | yes | Date + one-line reason |

**Resolution** — absent file, unparseable file, unknown value, a task outside `scope:`, or a
`scope:` that matches nothing in `tasks.md` all resolve to `review-before`. Fail safe, never fail
open. `tasks.md` files are not uniformly structured (about half use `## Phase N`, the rest
`## Task N`, a flat `## Tasks`, or numbered subsections), so a scope is matched against the headings
the file actually has, or by task range.

**Why untracked**: the gear is where the work currently *is*, not what the project decided.
Tracking it would put an hourly-changing value into branch history and make the answer depend on
which branch is checked out rather than on what the operator last chose.

**Why per-spec**: a root-level gear file would survive branch switches and silently disarm the gate
for unrelated work. Inside the spec directory it cannot reach past the spec that owns it.

**Who honours it**: `/spec:implement` and `/spec:ralph-implement`. A standalone `/test-first` and
`/bugfix:test` are **always gated** — they never read the file, so they cannot inherit a mode they
did not ask for.

**What `review-after` does NOT remove**: RED-first, the full regression suite, the two-commit shape
(`feat:`/`test:` then a separate `docs:` tick), and every test-authoring convention. Only the human
pause goes.

**Discoverability**: because the file is untracked, `/spec:gear` also maintains a pointer line in
`PROMPT.md`, and `/spec:status` reports the resolved gear.

---

### `/spec:ralph-implement [count]`

Unattended TDD implementation from the spec's **approved `tasks.md`** — the same task list
`/spec:implement` works from — via a **self-driving loop** in the `review-after` gear. Run it on
**opus** with **auto mode** enabled for a true unattended run.

```bash
# Ask the run bound up front, then loop unattended
/spec:ralph-implement

# Shortcut: pre-set the tasks bound to 3 (skips the bound prompt)
/spec:ralph-implement 3
```

**No separate task list.** This command reads `tasks.md`, skips tasks already ticked, and honours
the gear's `scope:` if one is set. Nothing is regenerated; there is no `ralph-tasks.md`.

**Up-front setup (Step 0, the only interactive part):**
- Advises that the orchestrator should be on **opus** and that **auto mode** should be on
  (auto mode is a permission mode set in Claude Code settings / `CLAUDE_CODE_ENABLE_AUTO_MODE`,
  Opus-gated — the command can't toggle it; it advises and proceeds).
- **Sets the gear** to `review-after`, asking for the reason (and optionally a phase scope) if the
  spec is not already in that gear, so `/spec:gear` and `/spec:status` tell the truth mid-run.
- Asks (via `AskUserQuestion`) which **run bound** to use — *unless* a `count` was passed,
  which sets the tasks bound directly:
  - **Tasks** — stop after N tasks complete (the `count` argument)
  - **Turns** — stop after N loop iterations attempted (failures included)
  - **Budget** — stop after ~N output tokens consumed

**Two models on purpose:** the **opus** orchestrator does only bookkeeping; each task's test +
implementation is delegated to a **sonnet** sub-agent (cheaper, and kept off the opus loop
context). The sub-agent never commits, pushes, or edits the task list.

**Task shapes.** `tasks.md` is a general list and the labels in use vary, so the loop matches the
task's leading label case-insensitively and treats synonyms as one shape:

| Shape | Labels | Commit |
|-------|--------|--------|
| Behavioural | `TEST + IMPLEMENT` | `feat:` / `fix:` |
| Test only | `TEST`, `TEST (RED)` | `test:` |
| Implementation only | `IMPLEMENT` | `feat:` — **only** when the specifying test already exists; otherwise skipped |
| Structural | `TIDY FIRST`, `TIDY`, `TIDY-FIRST`, `STRUCTURAL` | `refactor:` |
| Documentation | `DOC`, `DOCUMENT` | `docs:` |
| Scaffolding | `SETUP` | `chore:` |
| Checkpoint | `VERIFY`, `VALIDATION` | bookkeeping tick only |

Anything else it marks `- [!]` and skips rather than improvising. The labels come from the ~750
tasks currently in `specs/`; an unlisted label is skipped for a human to decide, never mapped to the
nearest row by guesswork.

**Loop per task:** check `RALPH_STOP` and the gear → select next in-scope `- [ ]` task → delegate
🔴 Red → 🟢 Green → 🔵 Refactor to a sonnet sub-agent → orchestrator commits the change, then
commits the checkbox tick → check continuation → repeat. Long runs can self-pace across context
windows with `ScheduleWakeup`.

**Stop mechanisms:**
- The chosen **bound** (tasks / turns / budget)
- `/spec:gear review-before` — **downshift**: the loop finishes the task in flight, then stops
  cleanly with `DOWNSHIFTED`. Nothing completed is unwound; `/spec:implement` resumes from the next
  unchecked task under the restored gate. This is the way back into per-test review mid-phase
- `RALPH_STOP` file at repo root — the unattended kill-switch (`touch RALPH_STOP` from another
  terminal); halts after the current task
- **Esc** — cancel a pending self-paced wake-up at the keyboard
- **Scope exhausted** — the next unchecked task falls outside the gear's `scope:`
- Automatically stops when all tasks complete

**Error handling:** Failed tasks are marked `- [!]` with an explanation and skipped.

**What it does not drop:** RED is still proved before any production code, the full regression suite
still runs, each task still produces the two-commit shape, and every test-authoring convention still
applies. `review-after` removes the human pause and nothing else.

**Requirements:**
- Requirements, design **and tasks** approved (`.requirements-approved`, `.design-approved`,
  `.tasks-approved`)
- All ADRs must be approved (Status: Accepted)
- Recommended: session on **opus** with **auto mode** enabled

---

### Running the unattended loop

The loop runs **in-session** — no external bash runner. Built-in **auto mode** removes the
per-action permission prompts and the self-driving loop (optionally self-paced with
`ScheduleWakeup`) replaces the old `scripts/ralph.sh` overnight runner.

```bash
# 1. The normal spec workflow, through to an APPROVED TASK LIST
/spec:requirements 123
/spec:approve requirements
/spec:design message-serialization
/spec:approve design
/spec:tasks
/spec:review tasks
/spec:approve tasks

# 2. Work the early, uncertain tasks attended — the gate is armed by default
/spec:implement

# 3. Once the task shape is settled, upshift and hand the rest to the loop
/spec:gear review-after "Phase 5 — Provider rejection tests" \
    --because "Phase 4's six tests were all approved unchanged"
/model opus
/spec:ralph-implement         # choose tasks / turns / budget when prompted

# 4. Take back per-test review at any point, from another terminal
/spec:gear review-before      # loop stops after the current task; nothing is unwound
/spec:implement               # resumes from the next unchecked task, gated

# 5. Or stop the loop outright
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

# 4. Check overall status (including the current review gear)
/spec:status

# 5. Begin implementation — the approval gate is armed by default
/spec:implement

# 6. Once a phase's task shape is settled, upshift and let the loop finish it
/spec:gear review-after "Phase 5 — Provider rejection tests" \
    --because "Phase 4's six tests were all approved unchanged"
/model opus
/spec:ralph-implement 8

# 7. Downshift the moment the tests stop looking right — nothing is unwound
/spec:gear review-before --because "the last two tests asserted the wrong thing"
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
│       ├── tasks.md                       # Implementation tasks (the single task list)
│       ├── .current-gear                  # Review gear — GITIGNORED working state
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
- Give phases meaningful, stable headings — `/spec:gear` can scope a gear to a single phase, and a
  well-named phase is what makes a narrow, self-expiring gear shift possible
- Do **not** try to encode the gear in `tasks.md`; it is frozen by `.tasks-approved`, and the gear
  lives in the untracked `.current-gear` file so it can still be shifted afterwards

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
