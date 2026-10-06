# Agentic Coding with Brighter

This guide explains how to use a coding agent to contribute to Brighter. We have focused on [Claude Code](https://claude.com/claude-code), but most of what is here (our instructions, our workflow, the reasoning behind our gears) applies to other agents too.

We welcome code authored with an agent. **You remain responsible for the code you submit.** Read it, understand it, and make sure it follows our [contribution guidelines](CONTRIBUTING.md). The workflows below exist to make that practical.

## Table of Contents

- [What We Provide](#what-we-provide)
- [Choosing a Workflow](#choosing-a-workflow)
- [The Spec Workflow](#the-spec-workflow)
  - [Design: Requirements, ADRs and Tasks](#design-requirements-adrs-and-tasks)
  - [Implementation: Driving in Gears](#implementation-driving-in-gears)
  - [Choosing a Driver: Step or Loop](#choosing-a-driver-step-or-loop)
  - [Shifting Gear with /spec:gear](#shifting-gear-with-specgear)
  - [Worked Example](#worked-example)
- [The Bugfix Workflow](#the-bugfix-workflow)
- [Working Across Sessions: PROMPT.md](#working-across-sessions-promptmd)
- [Smaller Changes: /test-first and /tidy-first](#smaller-changes-test-first-and-tidy-first)
- [Reviewing Agent-Authored Pull Requests](#reviewing-agent-authored-pull-requests)

---

## What We Provide

| Location | Purpose |
| --- | --- |
| `CLAUDE.md` | Entry point for Claude Code. Points at `.agent_instructions/` and sets the non-negotiables, such as the TDD approval gate. |
| `AGENTS.md` | The same entry point for other agents. |
| `.github/copilot-instructions.md` | The same entry point for GitHub Copilot. |
| `.agent_instructions/` | An agent-oriented version of our contribution guidelines: build, code style, design principles, testing, documentation and dependency management. |
| `.claude/commands/` | Slash commands that drive our preferred workflows: `/spec:*`, `/bugfix:*`, `/test-first`, `/tidy-first`, `/refactor:smells` and `/adr`. See the [commands README](.claude/commands/README.md). |
| `PROMPT.md` | Not provided: you create it. A gitignored file of session state that lets work continue across agent sessions. See [Working Across Sessions](#working-across-sessions-promptmd). |
| `.slopwatch/` | Configuration and a legacy baseline for [SlopWatch](https://github.com/Aaronontheweb/dotnet-slopwatch), which we are trialing to catch agents reward-hacking (for example, weakening a test to make it pass). |

Because our instructions live in plain Markdown under `.agent_instructions/`, any agent can use them.

## Choosing a Workflow

| You are... | Use | Why |
| --- | --- | --- |
| Building a feature or new capability | `/spec:*` | Requirements, ADR design, adversarial review, and a reviewed task list before any code. |
| Fixing a bug whose cause is not yet proven, or that arrived with a suggested fix | `/bugfix:*` | Proves the root cause before any fix is written. |
| Making a small change whose behavior is already obvious | `/test-first` | Test-first discipline without the ceremony. |
| Restructuring code | `/tidy-first` | Keeps structural and behavioral changes in separate commits. |
| Looking for what to tidy once a piece of work is finished and green | `/refactor:smells` | Finds Fowler's code smells in the change and schedules the refactorings, without changing code. |

---

## The Spec Workflow

The `/spec` commands implement Brighter's contribution workflow: **Issue → Requirements → ADR(s) → Tasks → Tests → Code**. Every phase ends with your explicit approval; the agent does not move on until you approve. The full command reference is in the [spec README](.claude/commands/spec/README.md).

```
 GitHub Issue
      │
      ▼
 requirements.md ───► /spec:requirements [issue]   → /spec:review → /spec:approve requirements
      │
      ▼
 docs/adr/NNNN-*.md ► /spec:design [focus-area]     → /spec:review → /spec:approve design
      │               (one ADR per architectural decision; repeat as needed)
      ▼
 tasks.md ──────────► /spec:tasks                   → /spec:review → /spec:approve tasks
      │
      ▼
 Implement ─────────► /spec:implement        one task at a time, in the main session
      │               /spec:ralph-implement  an unattended loop over the same tasks.md
      │                    ▲
      │                    └── /spec:gear  shifts the review gear at any point
      ▼
 Pull Request
```

### Design: Requirements, ADRs and Tasks

| Command | What it does |
| --- | --- |
| `/spec:new <name>` | Creates `specs/NNNN-name/` and makes it the current spec. |
| `/spec:requirements [issue]` | Drafts `requirements.md`, from a GitHub issue if one is given. Requirements say *what* users need, not *how*. |
| `/spec:design [focus-area]` | Drafts an ADR in `docs/adr/`. Write one ADR per architectural decision; a feature often needs several. |
| `/spec:review [phase]` | Runs an **adversarial review** of the current phase in a separate agent. Run it more than once: fix the findings, then review again. |
| `/spec:approve <phase>` | Records your approval and unlocks the next phase. Approving `tasks` freezes the *content* of `tasks.md`; ticking checkboxes is still allowed. |
| `/spec:tasks` | Breaks the approved design into a test-first task list. |
| `/spec:status`, `/spec:switch` | Show every spec and its phase (including its current gear), or change the active spec. |

The design phases are where you and the agent build a shared *theory* of the change. Reviewing and approving them yourself, rather than waving them through, makes the implementation phase cheap to review.

### Implementation: Driving in Gears

How much should the agent do before you look at its work? One test? One task? The whole feature?

Our answer comes from [Coding Agents: Driving In Gears](https://ian-cooper.writeas.com/coding-agents-and-driving-in-gears). In brief:

- **The code is where the theory lives.** Natural language is how we talk to the agent about the model; the code *is* the model. Reading it lets us check that the agent shares our theory, gives us fresh insights, and prevents **cognitive debt**: a team that cannot whiteboard how its own code works.
- **What is scarce is human understanding, not approvals.** An agent writes code far faster than we can build a theory of it, and it copies whatever idioms it finds, good or bad. Without oversight it amplifies poor quality as quickly as good.
- **"Human in the loop or not" is the wrong question.** The right one is *how certain am I about this theory right now?* The less certain you are, the smaller the step you take before you review. The more certain, the larger.
- **Blast radius is the second dial.** Certainty is how likely you are to be wrong; blast radius is how much being wrong would cost. A boring change to something with a high cost of failure, such as delivery guarantees or a public API, still calls for a lower gear.

Kent Beck's *driving in gears* metaphor from *TDD By Example* turns this into practice:

| | Low gear | Medium gear | High gear |
| --- | --- | --- | --- |
| **You are…** | Unsure of the theory. The problem, technology or domain is new to you. You want the agent to be a researcher, not a delegate. | Confident you'll find a workable theory quickly because you've solved something similar. You want to explore the solution with the agent. | Holding the theory, and bored by the details. The work is well-trodden boilerplate. |
| **Design** | Several rounds of dialogue. Use the agent to research options and spike unfamiliar tools before you agree a design. | A couple of rounds of back-and-forth on behaviors, responsibilities and key abstractions. | Agree the design, have it adversarially reviewed, and skim it for surprises. |
| **You review…** | Every test, *before* it is implemented, then each implementation as it goes green. You guide the refactoring. | After each task, or every two or three similar tasks. You look for code smells and insights to feed back into the design. | The finished pull request, relying on adversarial agent reviews along the way. |
| **Gear setting** | `review-before` (the default) | `review-after` | `review-after` |
| **Driver** | `/spec:implement` | `/spec:ralph-implement 1`, rising to `2` as the design settles (or `/spec:implement`) | `/spec:ralph-implement 3`, or more, up to all remaining tasks |

#### When to shift

You will shift within a single spec, sometimes within an afternoon.

- **Shift up on boredom and confidence.** The design has stopped moving, reviews no longer surface fresh insights, and the agent has picked up the code style from the work it has already done. Boredom is an honest signal that you hold the theory and what remains is labor.
- **Shift down on opacity and reverts.** You don't recognize the code the agent just wrote, you keep reverting tasks, you keep correcting the same kind of mistake, or the tests assert things you would not have approved.
- **Impatience is not a signal.** The economics always push toward high gear because it is faster and cheaper. Upshift because you have earned it, not because you want to be done.

Spend extra effort in the first few tasks of any spec. The agent learns how this code should be written from the code it has already written, so early review pays off for the rest of the run.

### Choosing a Driver: Step or Loop

There is one task list, `tasks.md`, and two ways to run it. Switching between them costs nothing; each picks up from the next unticked task.

| | `/spec:implement` | `/spec:ralph-implement` |
| --- | --- | --- |
| **Shape** | One task at a time, in your session. | A self-driving loop that runs tasks in sequence until a bound is reached. |
| **Gear** | Honours whatever gear is set, task by task. | Always `review-after`. It sets the gear when it starts, asking you for a reason. |
| **Model** | Run the session on Sonnet; the command will prompt you to switch. | Run the orchestrator on Opus with **auto mode** enabled so it is not stopped by permission prompts. Each task runs in a fresh Sonnet sub-agent. |
| **Use when** | Low gear, or medium gear when you want to stay at the keyboard. | Medium and high gear. |

**Setting the loop's bound.** `/spec:ralph-implement` asks how far to go before it stops and hands back to you:

- **Tasks:** stop after *N* tasks complete. `/spec:ralph-implement 3` sets this directly. This is the gear lever in practice: `1` for medium gear, `2` as the design settles, `3` or more for high gear.
- **Turns:** stop after *N* attempts, successful or not, which caps effort.
- **Budget:** stop after roughly *N* output tokens.

**Context stays small.** Each task's red-green-refactor cycle runs in its own sub-agent, so only the orchestrator builds up context across the run. If the orchestrator's context grows too large, stop it and restart; it resumes from the first unfinished task. The loop is deliberately sequential because it assumes you, the reviewer, are the bottleneck.

**Stopping the loop.** The loop always finishes the task in flight, and nothing already completed is undone.

- `/spec:gear review-before` (from another terminal) stops it cleanly so you can carry on with `/spec:implement` under the restored gate.
- `touch RALPH_STOP` at the repository root is the kill switch.
- It also stops when the bound is reached, when the gear's scope is exhausted, or when no tasks remain. A task that fails is marked `- [!]` and skipped for you to look at.

### Shifting Gear with /spec:gear

There are two gear settings. The low/medium/high distinction above comes from combining a setting with a driver and a bound.

| Setting | Approval gate | Meaning |
| --- | --- | --- |
| `review-before` | ✅ armed | You approve each test in your IDE before the agent implements it. **The default.** |
| `review-after` | ➖ not armed | The agent proves each test fails, implements it, and you review the batch afterwards. |

```bash
/spec:gear                                         # which gear am I in, and why?

/spec:gear review-after "Phase 5 — Provider rejection tests" \
    --because "Phase 4's six tests were all approved unchanged"

/spec:gear review-before --because "the last two tests asserted the wrong thing"
```

- **A reason is required.** A gear change is a judgement about certainty; the reason records it.
- **Scope an upshift where you can.** Name a heading in `tasks.md` (or a range such as `tasks 6-11`) and the gear applies only there; tasks outside it, and the tasks after it, fall back to `review-before`. A scoped upshift expires on its own. This is why task phases with clear, stable names are worth having.
- **The gear is working state, not a decision record.** It lives in `specs/{spec}/.current-gear`, which is gitignored, and it applies only to that spec. `/spec:gear` adds a pointer to [`PROMPT.md`](#working-across-sessions-promptmd) so a fresh session can find it. If the file is missing, can't be read, or names a scope that doesn't match, the gear is `review-before`; it fails safe.
- **`/test-first` and `/bugfix:test` are always gated.** Only the spec drivers read the gear.

**What `review-after` does *not* remove.** It removes the human pause and nothing else. In either gear the agent must:

- write the test and watch it **fail for the right reason** before writing any production code;
- run the **full regression suite**, not just the new test;
- make **two commits** per task: the change (`feat:`, `test:`, `fix:` or `refactor:`), then a separate `docs:` commit ticking the task in `tasks.md`;
- follow every test-authoring convention in [testing.md](.agent_instructions/testing.md).

Even in `review-after`, the agent stops and asks if a test asserts something the task did not ask for, needs a design decision the ADRs don't cover, or duplicates an existing test. The reasoning behind this mechanism is in [ADR 0071](docs/adr/0071-tdd-review-gear.md).

### Worked Example

```bash
# Design: build the theory with the agent, reviewing each phase
/spec:requirements 123
/spec:review requirements
/spec:approve requirements
/spec:design message-serialization
/spec:review design
/spec:approve design
/spec:tasks
/spec:review tasks
/spec:approve tasks

# Low gear: the early tasks set the shape and the style, so review every test
/spec:implement

# Medium gear: the shape has settled; review each task as it lands
/spec:gear review-after "Phase 3 — Mapper tests" --because "the test shape is settled"
/model opus
/spec:ralph-implement 1

# High gear: the remaining tasks are boilerplate; review the batch
/spec:gear review-after "Phase 4 — Provider registrations" --because "repetitive and low blast radius"
/spec:ralph-implement 5

# Something looks wrong: downshift, and carry on one test at a time
/spec:gear review-before --because "the last test asserted the wrong thing"
/model sonnet
/spec:implement
```

When the spec is finished, `/spec:show-me` writes a summary for reviewers (what changed and why, blast radius, and where to look first), and `/spec:write_release_notes` drafts its section of `release_notes.md`.

---

## The Bugfix Workflow

Bugs differ from features in one important way: **the root cause is a hypothesis until it is proven.** Issues often arrive with a suggested fix, sometimes from another agent, that is wrong, incomplete, or treats a symptom. The `/bugfix` commands are a lighter workflow than `/spec`, with no requirements or ADR phases, built around a gate that proves the diagnosis first. The full reference is in the [bugfix README](.claude/commands/bugfix/README.md).

```
 Issue ──► /bugfix:triage [issue]   restate the symptom, locate the code, form a hypothesis
             │                       (any suggested fix is recorded as UNVERIFIED)
             ▼
           /bugfix:confirm           ✋ prove or refute the hypothesis before any fix
             │                       (refuted → back to triage)
             ▼
           /bugfix:test              ✋ write the failing regression test (via /test-first)
             ▼
           /bugfix:fix               the minimal change to go green, scoped to the confirmed cause
             ▼
           /bugfix:verify            run the suite; prepare a fix: commit and a "Fixes #N" PR
```

- **Confirm is the point of the workflow.** It accepts either a failing reproduction (preferred, when the bug reproduces cheaply) or a code trace with `file:line` evidence (when a reproduction would need a real broker or database). Confirming the cause often changes the scope of the fix, for example by revealing a second defect the suggested fix missed.
- **The regression test is always gated.** `/bugfix:test` delegates to `/test-first` and always waits for you to approve the test. Bugfixes do not use gears.
- **Fixes stay minimal.** If the fix needs restructuring first, `/bugfix:fix` hands that to `/tidy-first`, giving a separate `refactor:` commit before the `fix:` commit.
- **State lives in `bugfixes/NNNN-slug/bugfix.md`**, filled in as you go: symptom, hypothesis, confirmed cause, evidence, test and fix. `/bugfix:status` lists open bugs; `/bugfix:switch` changes the active one.

---

## Working Across Sessions: PROMPT.md

A spec or a bugfix rarely fits in one agent session. The context window fills up, compaction drops detail, you stop for the day, or you `/clear` to give the agent a clean start on the next phase. The repository already holds the durable state: `requirements.md`, the ADRs, the ticks in `tasks.md`, and `bugfix.md`. It does not hold the *working* state: what you were in the middle of, what you agreed in conversation but have not yet written down, and what the agent should leave alone. We keep that in `PROMPT.md`.

**What it is.** `PROMPT.md` is a Markdown file at the root of your checkout, or of your worktree if you use one. It is gitignored, along with any `PROMPT-*.md` companions, so it is never committed or reviewed, and each worktree has its own. It belongs to you. `CLAUDE.md` tells the agent to keep cross-session state here rather than in its private memory, where you cannot see or edit it.

**The loop.**

```
 work ──► "update PROMPT.md" ──► /clear, compaction, or tomorrow ──► "read PROMPT.md and continue"
   ▲                                                                              │
   └──────────────────────────────────────────────────────────────────────────────┘
```

- **Update it at natural breaks**: a phase approved, a review round finished, a PR opened, or just before you `/clear`. Ask the agent to do it; it knows what it has been doing.
- **Start the next session with "read PROMPT.md and continue".** The agent should tell you where it thinks it is before it does anything. Check that it matches your understanding.
- **Rewrite it; don't append to it.** It is a snapshot of where the work stands now, not a log. When the work ships, cut that work down to a line or two.

**What goes in it.** Keep it short, because the agent reads it at the start of every session.

| Section | Holds |
| --- | --- |
| Where we are | Branch, worktree, current spec or bugfix, phase, PR number. |
| Done | What is complete, with commit SHAs, so the agent does not redo it. |
| Decisions | Choices made in conversation that are not yet written down anywhere else, and instructions such as "the report is approved; do not regenerate it". |
| Review gear | A pointer to `specs/{spec}/.current-gear` when a spec runs in `review-after`. `/spec:gear` writes and removes this line for you. |
| Leave alone | Untracked or unrelated files in the checkout that the agent must not touch or commit. |
| Next | The next step, or "the user has a new task". |

If one topic needs more room, such as a long hand-over, put it in a `PROMPT-<topic>.md` companion and link to it from `PROMPT.md`.

**What does not go in it.**

- **Anything that should outlive the work.** A decision that shapes the code belongs in the requirements, an ADR or `bugfix.md`, where reviewers will see it. If you find `PROMPT.md` holding a design decision, move it there.
- **Progress the repository already records.** The ticks in `tasks.md` (and `/spec:status`) are the authority for which tasks are done, and `bugfix.md` is the authority for a bugfix's phase. `PROMPT.md` points at them rather than duplicating them. If they disagree, the repository wins.
- **Waivers.** Never use `PROMPT.md` to switch off the approval gate or any other rule in `CLAUDE.md`. Before `/spec:gear` existed, a prose waiver in `PROMPT.md` was how gears were shifted, and nothing enforced it or showed it to anyone else ([ADR 0071](docs/adr/0071-tdd-review-gear.md)). Now `PROMPT.md` only points at the gear file.
- **Links from durable artifacts.** Never cite `PROMPT.md` in code comments, ADRs, commit messages or pull request descriptions; nobody else can read it ([documentation.md](.agent_instructions/documentation.md)). `/spec:review` flags such references, and `/spec:show-me` deliberately never reads `PROMPT.md`.

Nothing here is specific to Claude Code: any agent that can read a file can work this way.

---

## Smaller Changes: /test-first and /tidy-first

- **`/test-first <behavior>`** runs one red-green-refactor cycle. It always stops after writing the failing test so you can review it in your IDE before any implementation. See the [TDD README](.claude/commands/tdd/README.md).
- **`/tidy-first <change>`** separates structural changes (refactoring) from behavioral ones, following Kent Beck's *Tidy First*, so each lands in its own commit. See the [refactoring README](.claude/commands/refactor/README.md).
- **`/refactor:smells [base-ref | base..head | path...]`** reviews finished, green work for the code smells in Martin Fowler's *Refactoring*, calibrated to our design principles. By default it looks at your branch's changes against `master`; pass a git ref, a `base..head` range (for example, to review a PR that has already merged) or paths to narrow it. Each finding names the smell, where it is, and the refactoring that removes it. It changes no code: you choose which findings to act on, as `/tidy-first` runs or as `STRUCTURAL` tasks in the current spec's `tasks.md`. It is opt-in, and agents will not run it unless you ask. See the [refactoring README](.claude/commands/refactor/README.md).
- **`/adr <title>`** creates a correctly numbered ADR outside the spec workflow. See the [ADR README](.claude/commands/adr/README.md).

---

## Reviewing Agent-Authored Pull Requests

Whichever gear you drove in, review the pull request yourself before asking anyone else to. An agent's green test suite tells you the code does what its tests say. It does not tell you that the tests say the right thing, or that you could explain the design without looking at the code.

Maintainers can request an AI review of a pull request; see [Requesting an AI Code Review](CONTRIBUTING.md#requesting-an-ai-code-review). Treat it like any other review: useful, occasionally wrong, and never a substitute for a human reviewer signing off.
