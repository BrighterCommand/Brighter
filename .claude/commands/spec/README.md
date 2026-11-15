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

The diagram below is the `feature` flow, the route a spec follows unless its slip names another (see *The route model*).

```
┌─────────────────────────────────────────────────────────────────┐
│           Specification Workflow (the `feature` flow)           │
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
| `/spec:show-me`, `/spec:write_release_notes` | No | — | Run entirely in the main agent; the former delegates counting to `show_me_facts.cs`, not to a sub-agent |

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

**Working the findings:** fix *issues*, not lines. Name the concept each finding is about, edit
every place the document (or the ADR set) states it, re-sweep the old wording, and run a quick
contradiction-only sub-agent check of the diff before committing. See Step 8 of
[`review.md`](review.md).

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
- The spec's route must have reached its hand-off: every step before `/spec:implement` is approved. The route resolver decides this from the spec's slip, and prints the unapproved steps when it fails (see *The route model*)
- If the slip has a `design` step, every ADR in `.adr-list` must be Status: Accepted

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

Report or shift the **review gear** — whether `/tdd:test-first`'s approval gate is armed for the
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

**Who honours it**: `/spec:implement` and `/spec:ralph-implement`. A standalone `/tdd:test-first` and
`/bugfix:test` are **always gated** — they never read the file, so they cannot inherit a mode they
did not ask for.

**What `review-after` does NOT remove**: RED-first, the full regression suite, the two-commit shape
(`feat:`/`test:` then a separate `docs:` tick), and every test-authoring convention. Only the human
pause goes.

**Discoverability**: because the file is untracked, `/spec:gear` also maintains a pointer line in
`PROMPT.md`, and `/spec:status` reports the resolved gear.

---

### `/spec:route {flow} [reason] [--flows-dir {dir}]`

Move the current spec onto another flow, or repair a tampered or invalid slip, by copying the flow's definition into `.flow`. It acts on the current spec, and uses the resolver's `--current` report to classify it.

```bash
/spec:route light "The issue is already scoped as an ADR; no requirements step is needed"
/spec:route feature "Scope grew; requirements are needed after all"
/spec:route feature                      # asks for the reason
```

- The first word is the target flow. `--flows-dir {dir}` may appear anywhere after it and defaults to `.claude/flows`.
- The rest of the text is the reason. When it is missing, the command asks for one, and a dismissed question changes nothing.
- It stops without writing when the target is not usable, the spec is already on the target with a valid, untampered slip, or the current spec is missing, empty or stale.
- It writes in the order given under [Slip creation and route change](#slip-creation-and-route-change), logs the change in `route-changes.md`, and checks the result with a second resolver run. Markers not on the new slip are kept and reported as stray.
- `allowed-tools`: the resolver, `tr`, `shasum`, `sha256sum`, `date`, `cat`, `test`, `rm`, `Read` and `AskUserQuestion`.

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
- The spec's route must have reached its hand-off: every step before `/spec:implement` is approved, as the route resolver reports it from the spec's slip (see *The route model*). The loop does not check ADR status
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

### `/spec:show-me [spec-id]`

Summarise a **finished** spec — not part of the TDD loop above — into `specs/{spec}/show-me.md`: what
changed and why, breaking changes, a requirements reconciliation, how it was built, blast radius, an
advisory Low/Medium/High merge-risk read, where to look first, and a provenance table.

```bash
/spec:show-me                          # the current spec
/spec:show-me 0036-scoped-lifetime-per-pipeline
```

**Not a review.** It does not re-check correctness, security, TDD compliance, CI status or PR review
outcomes — it explains what the spec did and gives an advisory risk read, nothing more.

**What it reads**: `requirements.md`, `tasks.md`, `.adr-list` and the ADRs it names, `.issue-number`,
a marked `release_notes.md` section (if any), `git`/`gh` history against the spec's branch, and the
diff itself — falling back gracefully, and saying so, wherever a source is unavailable (e.g. a spec
branch that can no longer be resolved).

**Output sections**: `## What changed and why`, `## Breaking changes`, `## Did it ship what it
said?`, `## How it was built`, `## Blast radius`, `## Risk assessment (advisory)`, `## Where to look
first`, `## Inputs used`.

Backed by a measurement script — see *Supporting scripts* below.

---

### `/spec:write_release_notes [spec-id]`

Write, or replace in place, the release-notes section for a spec into `release_notes.md`'s marked
form.

```bash
/spec:write_release_notes                          # the current spec
/spec:write_release_notes 0033-pg-advisory-lock-sha256
```

Locates the section marked for the target spec (or the correctly-formed unmarked heading it should
replace), judges breaking-change items from the spec's resolved ADRs' *Consequences* bullets
(consumer-affecting only) and `requirements.md`, and writes the section — never touching any other
spec's section. **Stops rather than guessing** whenever proceeding would mean picking among several
candidates: no usable `.current-spec`, an ambiguous or unresolvable target, more than one section
already marked for it, and so on.

Recommended by `/spec:design` (Step 7.8) when the ADR just written records a breaking change, and
flagged by `/spec:review`'s design criteria when one is missing — never run automatically by either.

---

### Supporting scripts

`/spec:show-me` delegates every mechanically countable value to a sibling **measurement script**,
`show_me_facts.cs` — a plain, dependency-free C# file-based app (`dotnet run <path> -- <args>`, no
build, project file or install step). **A user never invokes it directly**; `/spec:show-me`'s own
steps call it via the one whitelisted
`Bash(dotnet run .claude/commands/spec/show_me_facts.cs -- specs/:*)` entry in `.claude/settings.json`.

```bash
# The measurement mode /spec:show-me itself uses
dotnet run .claude/commands/spec/show_me_facts.cs -- specs/0036-scoped-lifetime-per-pipeline
dotnet run .claude/commands/spec/show_me_facts.cs -- specs/0036-scoped-lifetime-per-pipeline --pinned <base-sha> <head-sha>

# A second, unrelated mode: word-counts a single file, no spec target involved
dotnet run .claude/commands/spec/show_me_facts.cs -- some/file.md --word-count
```

Run it from the repository root. It starts the route resolver (`resolve_route.cs`) as a child process
by a path relative to the working directory, and `--flows-dir {path}` names the flow-definition
directory the resolver reads (the shipped `.claude/flows` when it is omitted).

**What it emits**:
- On success (exit `0`): a JSON **ledger** written atomically to `{target}/.show-me-ledger.json`
  (serialised in full, then a single `File.Move` — never a partial write) — ref/diff fields, task and
  requirement-id counts, `.adr-list` resolution, the release-notes section count, and every `git`/`gh`
  command it ran, so `/spec:show-me` never repeats one. Refuses to write at all (exit `1`) rather than
  truncate a ledger over its 65,536 B cap.
- On the completeness gate failing (`tasks.md` absent, zero checkboxes, or any box left unchecked):
  no ledger; one `show-me-gate: {…}` line on stderr naming the case and, for the unchecked case, the
  count and the first unchecked task's own title; exit `2` — the one status that distinguishes this
  stop from a tooling fault.
- In `--word-count` mode (`{file} --word-count`, unrelated to the grammar above — no spec target, no
  ledger): a `show-me-wordcount: {"total": …, "excluded_fence_lines": …, "in_range": …}` line on
  stderr, exit `0`; a missing file exits non-zero with no line at all.
- Any other malformed invocation exits `1` and writes nothing.

Its sibling, `show_me_facts_tests.cs`, is a plain file-based test script — **no test framework, no
project, nothing added under `src/` or `tests/`**, matching this whole family's existing status quo
(the only other executable artefact under `.claude/` is `generate_adr_index.awk`, which also has no
tests). It shells out to the measurement script against the tracked fixtures in
`.claude/test-fixtures/show-me/` (plus the real, pinned calibration spec
`0036-scoped-lifetime-per-pipeline`), asserts on the emitted ledger and stderr records, restores any
ledger it disturbed, and prints `N row(s) passed.` or a `FAIL …` line per failed assertion — exiting
non-zero if any fails.

```bash
dotnet run .claude/commands/spec/show_me_facts_tests.cs
```

**It is not part of CI** — no CI workflow lints or tests anything under `.claude/`, and this script
doesn't change that. Run it by hand after editing either script.

### The route resolver

`resolve_route.cs` is a read-only, dependency-free C# file-based app, run from the repository root as
`dotnet run .claude/commands/spec/resolve_route.cs -- {specs-root} {mode} [--flows-dir {dir}]`. The `--`
is required. Every route-following command runs it, so the one
`Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*)` entry in a command's `allowed-tools` covers every mode.
It writes nothing. Its output is one JSON **route report** on a line beginning `route-report: `; a command that
cannot run it, or cannot parse that line, stops and says so rather than guessing.

| Mode | Arguments | Output |
| --- | --- | --- |
| One spec | `--spec {dir-name}` | one `route-report:` line; exit `1` if `{dir-name}` is not a spec directory |
| Current spec | `--current` | one `route-report:` line, including `current_spec` |
| Every spec | `--all` | one `route-current:` line, then one summary `route-report:` line per spec |
| One definition | `--definition {name}` | one `route-report:` line in flow form, with no spec |
| Anything else | | no output, exit `1` |

`--flows-dir {dir}` replaces the default `.claude/flows` (and that directory's `approvals.md`); tests use it to
point at fixture flows. The mode is `--definition`, not `--flow`: no command accepts a `--flow` option.

### The route model

A **flow** is a named route to an approved `tasks.md`. Each flow has one **flow definition**, a Markdown file
`.claude/flows/{name}.flow` that holds the **slip**: an ordered table of steps, one `hand-off:` line (always
`/spec:implement`), and optional `detour:` lines. This repository ships one flow, `feature`:

```
# flow: feature

| Step | Kind | Producer | Artefacts | Marker | Marker-writer |
| --- | --- | --- | --- | --- | --- |
| requirements | requirements | /spec:requirements | requirements.md | .requirements-approved | /spec:approve requirements |
| design | design | /spec:design | .adr-list | .design-approved | /spec:approve design |
| tasks | tasks | /spec:tasks | tasks.md | .tasks-approved | /spec:approve tasks |

hand-off: /spec:implement
```

- **`.flow` and the `feature` default.** A spec holds its own copy of its slip as `.flow`, written when the spec is
  created and changed only with `/spec:route`. A **legacy spec**, one with no `.flow` and no creation record
  (every spec that existed before routing), is resolved against `feature.flow` and behaves as it always did.
  Nothing needs migrating.
- **Steps, artefacts and kinds.** A step names its producing command, its artefact files, its approval marker and the
  command that writes that marker. Each step declares an **artefact kind**. Three kinds are *known* (`requirements`,
  `design` with its `.adr-list`, and `tasks`) because `/spec:*` commands read or review them. Any other kind is
  *flow-specific*: only that flow's own commands touch its artefacts. A command that needs another step's output looks it
  up by kind, never by step name, so a flow with no `requirements` step makes the commands that read requirements skip them.
- **Approval markers.** A step is approved when its marker file exists. An **intent marker** is a marker some flow
  definition in the directory names (`.requirements-approved`, `.design-approved`, `.tasks-approved`). A **stray
  marker** is an intent marker in a spec whose slip does not use it (say `.requirements-approved` in a spec that skips
  requirements). It is reported and never counts as approval. An **ignored marker** is any other `.*-approved` file, such
  as `.code-approved` from the construction phase: it belongs to no route, and the resolver neither reports it nor lets it
  affect a decision. An approval out of slip order (a step approved while an earlier one is not) is reported as out of
  order.
- **Gates.** A command that approves one step and produces the next is a **gate**. In one run it approves step N, then
  asks before producing step N+1, and before replacing any artefact that already exists.
- **Detours.** A slip may attach an optional command (with a return point) to a step or to the hand-off. The next-step
  line offers it beside the required step. A detour never blocks or satisfies a precondition.
- **The approval table.** `.claude/flows/approvals.md` is the one table in a flow-definition directory, keyed by artefact
  kind. Each row's **approval sub-flow** is an ordered list of commands that run, idempotently, when a step of that kind
  is approved (`(empty)` for none). The shipped table gives `design` the four `/adr:*` commands and `requirements` and
  `tasks` nothing. `/spec:approve` holds no kind-specific actions: it reads the step's row and runs it. A kind with no row
  makes the slip invalid, and two rows for one kind make every slip invalid.
- **Adding a flow.** Add `.claude/flows/{name}.flow`. If it uses a new artefact kind, add that kind's row to
  `approvals.md`. No command file changes: the resolver validates the slip against its validity rules and every route-following
  command asks it where to go next. A starter command (the way `/spec:new` is for `feature`) creates specs on the new flow.
- **Route-following commands.** `/spec:requirements`, `/spec:design`, `/spec:tasks`, `/spec:approve`, `/spec:review`,
  `/spec:implement`, `/spec:ralph-implement` and `/adr:adr` run the resolver first and stop, creating nothing, when the
  slip is invalid or tampered or when `.current-spec` is missing, empty or stale (`/adr:adr` instead writes a standalone
  ADR). `/spec:status` and `/spec:switch` report the route; `/spec:route` repairs it.

### Routing texts

The route instructions, closing instructions and message templates that the `/spec:*` and `/adr:adr` commands carry are written here once. Each command carries an identical copy, and `spec_text_tests.cs` compares every copy with the canonical block below ([ADR 0089](../../../docs/adr/0089-slips-resolver-and-routing.md), [ADR 0092](../../../docs/adr/0092-routing-fixtures-and-test-scripts.md)). `{state}` in the route instructions is one of `exited {code}`, `no route-report line` or `route-report line does not parse`. `{command}` in a template is the command printing it.

Route instructions, carried by `requirements`, `design`, `tasks`, `approve`, `review`, `implement`, `ralph-implement` and `/adr:adr`:

<!-- routing-text:route-instructions:begin -->
Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current`. If the exit status is not `0`, or standard output has no line beginning `route-report: `, or the JSON after that prefix on the last such line does not parse, stop. Print `{command} could not run the route resolver (.claude/commands/spec/resolve_route.cs): {state}. This is a tooling fault, not a fault in the spec — re-run after restoring the script.` Create or modify no file. Otherwise hold that JSON object as the route report. If `stop` is not `null`, print `stop.message` and stop, creating or modifying no file.
<!-- routing-text:route-instructions:end -->

Closing instructions, the last step of `requirements`, `design`, `tasks`, `approve` and `/adr:adr`:

<!-- routing-text:closing-instructions:begin -->
After your last write, run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current` again, and apply the same test for a tooling fault as above. If it passes, print the next-step line, built from `next` in this second report. If it fails, print the tooling-fault message in place of the next-step line. Undo no write.
<!-- routing-text:closing-instructions:end -->

Message templates:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |
| Not on the route | the producing commands | ``{command} is not on flow `{flow}`'s route.`` followed by the next-step line |
| Another producer | the producing commands | ``Step `{step}` on flow `{flow}` is produced by `{producer}`, not {command}. Run `{producer}`.`` |
| Step not on the slip | `/spec:approve` | ``Step `{arg}` is not on flow `{flow}`'s route. Approvable steps: {approve_steps joined with ", ", or "none"}.`` For a missing argument the text is ``/spec:approve needs a step. Approvable steps on flow `{flow}`: {approve_steps joined with ", ", or "none"}.`` |
| Another marker-writer | `/spec:approve` | ``Step `{step}` on flow `{flow}` is approved by `{marker_writer}`, not /spec:approve. Run `{marker_writer}`.`` |
| Phase not on the route | `/spec:review` | ``Phase `{phase}` has no step on flow `{flow}`'s route.`` followed by the next-step line |
| Step with no review criteria | `/spec:review`, with no phase | ``Next step `{next.step}` on flow `{flow}` has no review criteria. Run its marker-writer, `{marker_writer}`.`` |
| No requirements step | `/spec:review`, as a note before the review | ``Flow `{flow}` has no requirements step, so no FR criteria apply.`` |
| No design step | `/adr:adr`, with a linked spec | ``Flow `{flow}` has no design step, so `/adr:adr` has no `.adr-list` to update for this spec.`` followed by the next-step line. The command writes nothing |
| Ambiguous spec id | `/spec:switch` | ``Ambiguous spec id '{arg}' — matches: {list of matching directory names}. Re-run with the full directory name.`` |
| No spec matches | `/spec:switch` | ``No spec matches '{arg}'. Run /spec:status to list specs.`` |
| Artefacts missing | `/spec:approve` | ``Step `{step}` cannot be approved: its artefacts ({steps[step].artefacts joined with ", "}) are not all present. Run `{producer}` first.`` |
| Flow not usable | `/spec:new` | ``Cannot start a spec: flow `feature` is not usable. {violations[0].message} Fix `.claude/flows/feature.flow` or `.claude/flows/approvals.md` first.`` |

| Template | Printed by | Text |
| --- | --- | --- |
| No usable entry | every approver | ``Step `{step}` cannot be approved: kind `{steps[step].kind}` has no usable entry in the approval table. Nothing was written.`` |
| Sub-flow failed, questions | every approver | ``{approver} stopped: `{command}` failed in its questions pass: {reason}. {steps[step].marker} was not written, and this sub-flow wrote nothing.`` |
| Sub-flow failed, actions | every approver | ``{approver} stopped: `{command}` failed in its actions pass: {reason}. {steps[step].marker} was not written. Writes made by earlier sub-flow commands stay. Re-run {approver} to run the sub-flow again from `{approval_subflow[0]}`.`` |
| Step approved | every approver | ``Approved step `{step}` on flow `{flow}`: {steps[step].marker} gained a provenance line.`` |

| Template | Printed by | Text |
| --- | --- | --- |
| Gate, first question | a gate | ``Approve step `{N}` ({steps[N].artefacts joined with ", "}) and go on to produce step `{N+1}`?`` Options `Approve`, `Cancel` |
| Gate, declined | a gate | ``{approver} stopped: step `{N}` was not approved. Nothing was written.`` |
| Gate, existing artefact | a gate | ``Step `{N+1}`'s artefacts ({steps[N+1].artefacts joined with ", "}) already exist. What should {approver} do?`` Options `Approve as is` (or `Keep as is`), `Regenerate`, `Cancel` |
| Gate, second question | a gate | ``Approve step `{N+1}` ({steps[N+1].artefacts joined with ", "})?`` Options `Approve`, `Not now` |

| Template | Printed by | Text |
| --- | --- | --- |
| Route usage | `/spec:route` | ``/spec:route needs a target flow name and, if given, a --flows-dir without $, `, " or \. Usage: /spec:route {flow} [reason]. Nothing was changed.`` |
| Route spec name | `/spec:route` | ``Cannot change the route of `{spec}`: its directory name holds $, `, " or \. Nothing was changed.`` |
| Route target not usable | `/spec:route` | ``Cannot change the route to `{target}`: {violations[0].message} Nothing was changed.`` |
| Already on target | `/spec:route` | ``Spec `{spec}` is already on flow `{target}`, and its slip is valid and not tampered. Nothing was changed.`` |
| No reason | `/spec:route` | ``/spec:route needs a reason for the route change. Nothing was changed.`` |
| Route changed | `/spec:route` | ``Spec `{spec}` moved from flow `{from}` to `{target}`. Reason recorded in route-changes.md: {reason}`` then, when `stray_markers` is non-empty, ``Kept as stray markers: {stray_markers joined with ", "}.`` then the next-step line |
| Post-check | `/spec:route`, `/spec:new` | ``{command} wrote the slip, but the resolver reports flow `{flow.name, or "none"}`, valid {valid}, tampered {tampered.case, or "no"}. Re-run /spec:route {target} <reason> to repair.`` |
| Installation failed | `/spec:route` | ``{command} stopped: {step} failed. Written so far: {files}. Re-run {command}.`` |
| New spec failed | `/spec:new`, starters | ``{command} stopped: {step} failed. `specs/{dir}/` was created and holds: {files}. Delete it and run {command} {name} again.`` |
| Start failed | `/spec:new`, starters | ``{command} stopped: {step} failed. Nothing was created.`` |
| Spec name | `/spec:new`, starters | ``{command} needs a spec name that is not empty and has none of / \ $ ` ". Nothing was created.`` |
| Spec exists | `/spec:new`, starters | ``Cannot start a spec: `specs/{dir}/` already exists. Nothing was created. Re-run {command} to take the next number.`` |
| Spec ignored | `/spec:new`, starters | ``Cannot start a spec named `{name}`: git would ignore `specs/{dir}/.flow`. Nothing was created. Choose a name that does not match a .gitignore rule.`` |

<!-- routing-text:templates:end -->

In the approver templates, `{approver}` is the approver's own invocation as typed, `{pass}` is `questions` or `actions`, and `{reason}` comes from the `sub-flow-failed` line, or is `no sub-flow-done line`. `{command}` is the sub-flow command named in the `sub-flow-failed` line, or the command that printed no `sub-flow-done` line. A gate also prints the artefacts-missing template, with `{step}` as `{N}` and `{producer}` as `steps[N].producer`. In a gate, `{N}` is `steps[i].gate.approves` and `{N+1}` is `steps[i].name`, for the step `i` that the gate produces.

### Slip creation and route change

A starter command, `/spec:new` and `/spec:route` install a slip into a spec directory with the same four shell steps, and record the change with the formats below ([ADR 0091](../../../docs/adr/0091-slip-creation-tamper-detection-and-route-change.md)). Each step is run on its own from the repository root. `{D}` is `specs/{dir}`, `{S}` is `{flows-dir}/{flow}.flow`, and each is double-quoted. The creation commands refuse a name holding `$`, a backtick, `"` or `\`, and `/spec:route` refuses a `--flows-dir` or a spec directory that holds one, so the double quotes are enough.

<!-- routing-text:installation-steps:begin -->
1. `tr -d '\r' < "{S}" > "{D}/.flow"`
2. `shasum -a 256 < "{D}/.flow"`. If the command is not found, use `sha256sum < "{D}/.flow"`. The slip hash is the first field of the output, and it must match `^[0-9a-f]{64}$`.
3. `date +%Y-%m-%d`
4. Write the record with a quoted here-document, so that nothing is expanded:
   `cat > "{D}/.flow.record" <<'FLOW_RECORD'` followed by the three lines, then `FLOW_RECORD`.

If any step fails, stop and print a failure template. `/spec:new` and the starters carry the steps in this order. `/spec:route` carries the same four steps verbatim, then runs them in the order its write order sets.
<!-- routing-text:installation-steps:end -->

`/spec:route` runs the steps in this order:

<!-- routing-text:write-order:begin -->
| # | Step | State if the run stops after this step |
| --- | --- | --- |
| W0 | installation step 3 (`date`, read-only). Its value dates both the log heading and the record | unchanged |
| W1 | `rm -f "{D}/.flow.record"` | case (c) if `.flow` exists; otherwise legacy-looking |
| W2 | append the log entry with `test -f` and `cat >> "{D}/route-changes.md" <<'ROUTE_CHANGE'`, writing the `# Route changes` header first if the file is new | as W1, with the entry logged |
| W3 | installation step 1 (the shell creates `.flow` before `tr` runs) | case (c) |
| W4 | installation step 2 (read-only) | case (c) |
| W5 | installation step 4 | complete |
<!-- routing-text:write-order:end -->

The previous flow reaches the log at W2, before `.flow` is replaced, so no interruption loses it. After W1, every state with a `.flow` reads as case (c). Only `/spec:route` repairs case (c), and re-running it repairs any of these states. A spec with no `.flow` that stops after W1 or W2 (legacy, or case (b)) reads as legacy. Its log may then name a change that did not finish, and `/spec:route` to the flow it is on records the abandonment. A re-run appends another entry in each case. Nothing else is deleted: markers and artefacts stay where they are, and markers not on the new slip become stray markers.

**`.flow.record`**, tracked, holds exactly three lines, each ending in LF:

```
flow: feature
sha256: 3b0c9f…(64 lowercase hex digits)
date: 2026-11-02
```

- **Parsing.** CRLF becomes LF, and trailing spaces and tabs are ignored. Trailing blank lines are allowed. The patterns are: `flow: [a-z][a-z0-9-]*`, `sha256: [0-9a-f]{64}`, `date: [0-9]{4}-[0-9]{2}-[0-9]{2}`. Anything else makes the record **unreadable**.
- **Normalisation for the slip hash.** Read `.flow` as bytes, replace each CR LF pair with LF, and hash the result with SHA-256. Nothing else is normalised. A byte-order mark, trailing whitespace, a lone CR or a missing final newline all change the hash.
- **The commands write a CR-free `.flow`.** The installation steps copy with `tr -d '\r'`, so the bytes the command hashes are the bytes the resolver hashes after normalisation. A later CRLF checkout normalises back to them.
- **Only `sha256` decides tampering.** `date` is for people to read. `flow` is reported as `tampered.recorded_flow`, and names the route the spec was set to.

**`route-changes.md`**, tracked, is created by the first route change. Each change appends one entry, and earlier entries are never rewritten:

```
# Route changes

## 2026-11-02: feature -> light

Reason: The issue is already scoped as an ADR; no requirements step is needed.

## 2026-11-09: light -> feature

Reason: Scope grew; requirements are needed after all.
```

- Heading pattern: `^## ([0-9]{4}-[0-9]{2}-[0-9]{2}): ([a-z][a-z0-9-]*) -> ([a-z][a-z0-9-]*)$`. Reason line: `^Reason: (.+)$`. CRLF is tolerated.
- **`from`** is read from the `--current` report: `tampered.recorded_flow` in case (b), where there is no `.flow` to read, and `flow.name` otherwise. When that is `null`, it is `route_change.to`, then `unknown`. A legacy spec therefore reads `feature`.
- **Re-runs** append a further entry. The earlier entry keeps the true change, and `route_change` reports the latest. A re-copy of the same flow reads `light -> light`.
- The reason is the user's text on one line, with line breaks replaced by spaces. It may not be empty.

**Tamper recoveries.** The resolver compares the SHA-256 of `.flow` with `.flow.record`, and a spec that does not match stops every route-following command:

| Case | `.flow` and `.flow.record` | Recovery |
| --- | --- | --- |
| a | `.flow` differs from the recorded hash | Restore `.flow` from version control (`git restore --source=HEAD -- "{spec}/.flow"`), or re-copy the flow with `/spec:route {flow} <reason>` |
| b | `.flow` is missing but `.flow.record` exists | the same as (a) |
| c | `.flow` has no readable `.flow.record` | Re-copy the flow with `/spec:route {flow} <reason>` |

The stop message is ``Spec `{spec}` has a tampered slip: {what}, so it does not match what was recorded when its route was set. {recovery}`` with `{what}` as `` `.flow` differs from the content recorded in `.flow.record` ``, `` `.flow` is missing but `.flow.record` exists `` or `` `.flow` has no readable `.flow.record` ``. `{flow}` is `tampered.recorded_flow` when it is not `null`, then `flow.name`, then the literal `<flow>`. A slip that is invalid names `/spec:route {flow.name} <reason>` as the recovery, or `/spec:route <flow> <reason>` when `flow.name` is `null`.

### Running an approval sub-flow

Every approver (`/spec:approve`, a gate, or a flow's own marker-writing command) carries an identical copy of the sub-flow instructions, which it runs after the route instructions and its step checks ([ADR 0090](../../../docs/adr/0090-approval-sub-flows.md)).

<!-- routing-text:sub-flow-instructions:begin -->
1. Read `steps[{step}].approval_subflow`. If it is `null`, print the no-usable-entry message and stop.
2. If it is empty, go to step 7.
3. Build the common arguments: `"{spec}" {step} {artefacts}`, then `--select {word}` for each of the approver's selector words, in order. `{spec}` is the report's `spec`, and `{artefacts}` is `steps[{step}].artefacts` joined with `,`. `/spec:approve` takes its selector words from the words after the step name. A gate or a flow's own approver passes none, unless its own command defines some.
4. Questions pass. For each command in table order, run it through the Skill tool (its name without the leading `/`), with arguments `questions`, the common arguments, and `--answer {key}={value}` for every answer kept from the commands before it. Keep every `answer:` line it prints, in order.
5. Actions pass. For each command in table order, run it with `actions`, the common arguments, and `--answer {key}={value}` for every kept answer.
6. After each run in steps 4 and 5, the command has completed only when the last protocol line it printed is `sub-flow-done: {command} {pass}`. Otherwise print the sub-flow-failed message for that pass and stop. Write no marker.
7. Append the provenance line to `steps[{step}].marker`, and print the step-approved message.
<!-- routing-text:sub-flow-instructions:end -->

Then `/spec:approve` runs the closing instructions, and a gate runs them at the last step of its ladder. With an empty sub-flow, any selector words are ignored.

A sub-flow command prints these protocol lines, each alone on a line:

| Line | Printed in | Meaning |
| --- | --- | --- |
| `answer: {key}={value}` | the questions pass | One answer, whether the user gave it or the command derived it |
| `approved-artefacts: {paths joined with ","}` | the actions pass, optional | Every file this approval covers, including any the command found already approved and skipped. The approver uses the last such line |
| `sub-flow-done: {command} {pass}` | the last line of each pass | The pass completed |
| `sub-flow-failed: {command} {pass}: {reason}` | instead of `sub-flow-done` | The pass failed. A question the user dismissed has the reason `question not answered` |

### The provenance line

One line is appended to the marker per approval:

```
approved: step={step} artefacts={approved} sha={full HEAD sha, or unavailable}
```

- `{approved}`, the value of `artefacts=`, comes from the last `approved-artefacts:` line, or from `steps[{step}].artefacts` joined with `,` when no command printed one.
- `artefacts=` holds paths as the step's artefacts or the `approved-artefacts:` line give them: spec-relative names such as `tasks.md`, or repository paths such as `docs/adr/0089-slips-resolver-and-routing.md`.
- Existing content is kept. If the marker is non-empty and lacks a final newline, a newline is added first, so the line starts a line.
- Re-approving appends a further line. The resolver never reads the content.

The approver writes the line with these commands, each run on its own from the repository root. `{m}` is `steps[{step}].marker` joined to `{spec}`, in double quotes:

<!-- routing-text:provenance-commands:begin -->
1. `git rev-parse --verify --quiet HEAD`. It prints the full sha. With no commits it exits `1` silently, and outside a git work tree it exits `128`. In both cases the sha is `unavailable`.
2. `test -s {m}`. When it succeeds, `tail -c 1 {m}`. When the byte it prints is not a newline (the output looks empty when the file ends in one), the marker lacks a final newline, so run `echo >> {m}`.
3. `echo "approved: step={step} artefacts={approved} sha={sha}" >> {m}`.

Worked examples, at HEAD `0691e67beb1ce7242c734bcd0a9a5960610d432d`:

```
approved: step=tasks artefacts=tasks.md sha=0691e67beb1ce7242c734bcd0a9a5960610d432d
approved: step=design artefacts=docs/adr/0089-slips-resolver-and-routing.md,docs/adr/0090-approval-sub-flows.md sha=0691e67beb1ce7242c734bcd0a9a5960610d432d
approved: step=requirements artefacts=requirements.md sha=unavailable
```
<!-- routing-text:provenance-commands:end -->

`grep '^approved: ' {marker}` lists every approval. `awk '/^approved: /{print $2, $NF}' {marker}` prints each step and sha. `test -f` still decides approval.

### The gate run contract

A gate approves step N and produces step N+1. It finds its step as the one whose `gate.command` is itself, and reads `steps[N].approved`, `steps[N+1].artefacts_present`, `steps[N+1].approved` and `gate.also_approves_produced`.

<!-- routing-text:gate-run-contract:begin -->
| # | Starting state | Interrupted state | What the gate does |
| --- | --- | --- | --- |
| 1 | N unapproved, N+1 absent and unapproved | A, when N's sub-flow failed before | First question, approve N, produce N+1, then second question |
| 2 | N unapproved, N+1 present and unapproved | A, when N's sub-flow failed before | First question, approve N, existing-artefact question |
| 3 | N approved, N+1 absent and unapproved | B: stopped after N's marker | Produce N+1, then second question |
| 4 | N approved, N+1 present and unapproved | C: stopped after N+1's artefact | Existing-artefact question |
| 5 | N+1 approved, its artefacts present or absent | — | First question if N is unapproved. Never touches N+1 |

Rows 1 to 4 need N+1 unapproved and row 5 needs it approved, so exactly one row matches. The gate applies the row in this order:

1. When N is unapproved and `steps[N].artefacts_present` is false, print the artefacts-missing template and stop. Nothing is written.
2. Ask the first question in rows 1, 2 and 5 when N is unapproved. On `Cancel`, print the declined message and stop. Nothing is written.
3. In rows 1 and 2, and in row 5 when N is unapproved, approve N with the sub-flow instructions. A failure stops the gate with no marker for N, and a re-run lands on the same row.
4. In rows 1 to 4, run the resolver again and read `steps[N+1].precondition`. When `holds` is false, print its `message` and go to step 8, leaving N approved.
5. In rows 1 and 3, produce N+1. In rows 2 and 4, ask the existing-artefact question. `Regenerate` produces N+1 again. `Cancel` leaves the state of row 4 and goes to step 8.
6. When `also_approves_produced` is true, ask the second question after a fresh production or `Regenerate`. Choosing `Approve as is` at the existing-artefact question counts as approval.
7. On approval, run the sub-flow instructions for N+1. `Not now` leaves the state of row 4 and goes to step 8.
8. Run the closing instructions.
<!-- routing-text:gate-run-contract:end -->

A gate produces N+1 only in rows 1 and 3, or after `Regenerate`, so it never overwrites an artefact without asking. A gate that is not N+1's marker-writer offers `Keep as is`, and goes to step 8 after it. Every path ends at step 8, except the stops in steps 1 to 3, which write no marker, so the user sees the next step.

### Starter commands

A starter command starts a spec on a flow other than `feature` (for example `/modules:suggest`). It is well formed when it meets the contract below. Each item is answered yes or no, and each names one clause of the contract and one way of checking it:

- **Fixture**: the flow ships a resolver fixture test in the layout of [ADR 0092](../../../docs/adr/0092-routing-fixtures-and-test-scripts.md).
- **Statement**: the flow's command text carries an identical copy of the named README text.

An item that can be checked both ways is split into two rows, so every row names one method. Gates and flow approvers carry copies of the route instructions and closing instructions, outside the counts for the `/spec:*` commands ([ADR 0090](../../../docs/adr/0090-approval-sub-flows.md)).

The contract has nine clauses:

1. Stops before creating anything if its definition is missing or invalid.
2. Creates the spec directory by the spec numbering rule, stopping if the whole name exists, and updates `specs/.current-spec`.
3. Copies its slip into `.flow` and writes a creation record, so the resolver reports the flow, valid and not tampered.
4. Writes the slip's markers with a provenance line.
5. Produces artefacts whose kinds and filenames match the slip.
6. Ensures every gate on its slip meets the gate run contract.
7. Takes routing decisions from the resolver and stops on an invalid or tampered slip.
8. Adds an approval-table entry for every flow-specific kind its slip uses, and runs a step's approval sub-flow before writing the marker.
9. Ships resolver fixture tests that cover its slip at each step's approved and unapproved state.

**Rule: a starter edits no `/spec:*` file.** A reviewer checks it with `git diff --stat -- .claude/commands/spec/`. It is a rule above the table, not an item in it.

| # | Check | Yes when | FR-19 clause | Checked by |
| --- | --- | --- | --- | --- |
| C1a | Stops on a missing or invalid definition | The starter runs `--definition {name}` first and stops when `valid` is false | 1 | Statement |
| C1b | Stops on a missing or invalid definition | `--definition {name}` reports the definition valid | 1 | Fixture |
| C2 | Names, numbers and creates safely | The starter carries the name rule, the spec numbering rule, the ignore check, `mkdir` with no `-p`, the spec-name, spec-exists, spec-ignored, start-failed and new-spec-failed templates, and writes `specs/.current-spec` last | 2 | Statement |
| C3a | Installs `.flow` and the record | The starter carries the installation steps | 3 | Statement |
| C3b | Installs `.flow` and the record | A fixture spec on the flow with `.flow` and `.flow.record` reports `{name}`, valid, not tampered | 3 | Fixture |
| C4 | Provenance lines | Every marker-writing command of the flow carries the provenance commands | 4 | Statement |
| C5a | Artefacts match the slip | Each producing command writes exactly the files its step's Artefacts cell names | 5 | Statement |
| C5b | Artefacts match the slip | The fixtures at each step name those files | 5 | Fixture |
| C6 | Gate run contract | Every gate on the slip carries the gate run contract text from this file | 6 | Statement |
| C7 | Takes routing from the resolver | Every producing and marker-writing command of the flow carries the route instructions, and also the closing instructions when its write changes `next` (every gate and flow approver does). The starter ends with a post-check like `/spec:new`'s | 7 | Statement |
| C8a | Approval-table entry for every flow-specific kind | `approvals.md` has an entry, possibly `(empty)`, for every flow-specific kind on the slip, so a fixture slip is valid | 8 | Fixture |
| C8b | Sub-flow before marker | Every approving command carries the sub-flow instructions, with the marker written by their step 7 only | 8 | Statement |
| C9 | Ships fixture tests | Fixtures cover every step approved and unapproved | 9 | Fixture |
| C10 | Step checks before the sub-flow | Before its sub-flow, a flow's own marker-writing command checks that the step is on the slip, that `steps[N].marker_writer` is itself, and that step N's artefacts are present, using the artefacts-missing template | 8 | Statement |

C10 covers the approval-table and sub-flow clause (8): it holds the checks a flow's own approver makes before it runs the sub-flow. C4, C6, C8b and C10 point at the texts under [The provenance line](#the-provenance-line), [The gate run contract](#the-gate-run-contract) and [Running an approval sub-flow](#running-an-approval-sub-flow).

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
│       ├── .flow                          # The spec's routing slip (absent on legacy specs)
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
