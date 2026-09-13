---
id: 0071-tdd-review-gear
title: "Shiftable Review Gear for the TDD Approval Gate"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-09-13
summary: "Makes the /test-first approval gate a shiftable gear (review-before | review-after) held in an untracked, per-spec .current-gear file that defaults to gated, is scopable to one phase of tasks.md, and can be downshifted mid-run; separates gate state from task-list format so /spec:ralph-implement reads the existing tasks.md and /spec:ralph-tasks is removed."
tags:
  - "meta"
  - "testing"
---

# 71. Shiftable Review Gear for the TDD Approval Gate

Date: 2026-09-13

## Status

Accepted

Implements [issue #4357](https://github.com/BrighterCommand/Brighter/issues/4357).
Supersedes no prior ADR. This is a decision about the project's own agent tooling, not its
runtime — see [ADR 0065](0065-add-frontmatter-to-adrs.md) for the precedent.

## Context

**Scope**: how the `/test-first` approval gate is switched on and off during a piece of work, and
where that setting lives.

### The Problem

`/test-first` stops after writing a failing test and waits for the user to review it in their IDE
before any implementation is written. `CLAUDE.md` and `.agent_instructions/testing.md` describe this
gate in absolute terms — "MANDATORY", "you cannot bypass it".

The gate is not, in fact, equally valuable throughout a piece of work. It is at its most valuable
early, when the design is still being pinned down and a wrong test would send the implementation
somewhere expensive to unwind. It is at its least valuable late in a spec, on a run of
near-identical, one-acceptance-criterion tasks whose shape has already been reviewed six times.

This is a **gear**, not a policy. You select it for the certainty you have and the blast radius you
face: low gear (review every test) while you are learning the problem, higher gear (review a batch)
once the shape is settled — and you downshift the moment the work starts producing tests you would
not have approved. The initial gear is a design-time decision; every subsequent shift is an
operational one made mid-run.

The project had no supported way to shift. The only mechanism found in practice was a hand-written
prose waiver in `PROMPT.md` — a gitignored, branch-local scratch file — that **contradicted**
`CLAUDE.md` rather than working with it, was invisible to any other session, was enforced by
nothing, and silently dropped the disciplines that must survive a gear change unless the author
remembered to restate every one of them by hand.

### The orthogonality that was conflated

`/spec:ralph-implement` already ran gate-off, proving unattended execution is viable. But it did so
by **regenerating a whole new task list** (`ralph-tasks.md`) from `requirements.md` and the accepted
ADRs. That welded two orthogonal concerns together:

1. **Which task list format is in play** — `tasks.md` vs `ralph-tasks.md`.
2. **Whether the approval gate fires.**

The cost of the conflation was real: `ralph-tasks.md` regeneration could not read an existing
`tasks.md`, had no notion that some tasks were already done, and could not express task shapes
outside its own TEST+IMPLEMENT+RALPH-VERIFY template — no `STRUCTURAL` tidy-first tasks, no `DOC`
tasks. A spec with a frozen, partially-implemented `tasks.md` therefore had no route to unattended
execution at all.

### Constraints

- **`/test-first` is shared.** It is used by `/spec:implement`, by `/bugfix:test`, and standalone,
  across every spec and branch in the repository. A gear mechanism must never be a global flag that
  disarms the gate for unrelated, concurrently active work.
- **`tasks.md` is frozen by `.tasks-approved`.** A gear encoded *inside* `tasks.md` cannot be
  shifted later without lifting that freeze — which rules out the task list as the gear's home.
- **The gear is operational, not architectural.** It changes several times within one spec, in
  response to how the work is actually going. It is working state, not a spec artifact, and does not
  belong in version control.
- **Shifting down must cost nothing.** A run that starts producing questionable tests must be able
  to return to per-test review mid-phase without unwinding or redoing anything already completed.

## Decision

Make the approval gate a **shiftable gear**, held in untracked per-spec working state, with the gate
armed by default.

### 1. The two gears

| Gear | Gate | Meaning |
|------|------|---------|
| `review-before` | Armed | The user reviews each test in their IDE **before** implementation. The default. |
| `review-after`  | Not armed | The user reviews a **batch** of completed work after the fact. |

The names describe *when the human reviews relative to implementation*, and cannot be read
backwards. (`pre-approve` was rejected precisely because it inverts: "the test is pre-approved"
reads as *approval already granted*, which is the opposite of an armed gate.)

### 2. Where the gear lives: `specs/{spec}/.current-gear`

A small, **gitignored** file inside the active spec's directory:

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
| `scope` | no | A phase heading from `tasks.md`. The gear applies **only** to tasks under that heading; tasks anywhere else fall back to `review-before`. Absent means the whole spec. |
| `driver` | no | `implement` \| `ralph` — which command is expected to be driving. |
| `shifted` | yes | Date and a one-line justification. A gear change is a judgement about certainty; the reason is the interesting part. |

Resolution rules, in order:

1. No `specs/.current-spec`, or no `.current-gear` in that spec → **`review-before`**.
2. File present but unparseable, or `gear:` holds anything other than the two known values →
   **`review-before`**, and say so out loud. Fail safe, never fail open.
3. `scope:` present and the current task is not under that phase heading → **`review-before`**.
4. Otherwise, the gear named in the file.

**Why untracked.** The gear is where the work *currently is*, not what the project decided. Tracking
it would put "we are running ungated right now" into the permanent record of a branch, invite merge
conflicts over a value that changes hourly, and make the answer to "which gear am I in?" depend on
which branch is checked out rather than on what the person at the keyboard last chose.

**Why per-spec rather than at the repo root.** A root-level gear file would be exactly the global
flag the constraints forbid: untracked files survive branch switches, so a root gear set during one
spec would silently disarm the gate for unrelated work checked out afterwards. Inside the spec
directory, the gear cannot reach past the spec that owns it.

**Discoverability.** Because the file is untracked, a fresh session will not learn of it from the
diff. `PROMPT.md` — the project's existing convention for session state that must survive a context
reset — should carry a pointer to the gear file for any spec being run ungated, and `/spec:status`
reports the resolved gear for every spec it lists.

### 3. Who honours the gear

| Caller | Behaviour |
|--------|-----------|
| `/spec:implement` | Resolves the gear for the current spec and task; runs gated or ungated accordingly. |
| `/spec:ralph-implement` | Always `review-after` by definition; re-reads the gear before **each** task so a downshift stops the loop. |
| `/test-first` standalone | **Always gated.** It does not read the gear file. |
| `/bugfix:test` | **Always gated.** Bugfixes have their own Confirm gate and are not part of this mechanism. |

`/test-first` deliberately does not resolve the gear itself. A standalone invocation should not
inherit a mode it did not ask for from whichever spec happens to be current. Instead the spec
commands resolve the gear and pass the resulting instruction into the `/test-first` workflow
explicitly — the caller opts in, the shared skill stays safe by default.

### 4. Shifting: `/spec:gear`

```bash
/spec:gear                                      # report the resolved gear and why
/spec:gear review-after "Phase 5" --because "…" # upshift, scoped to one phase
/spec:gear review-before                        # downshift; takes effect at the next task
```

`/spec:implement --review-before|--review-after` is sugar for the same write, so a shift can be made
in the same gesture that starts the work.

**Downshifting mid-run is the important direction.** `/spec:ralph-implement` re-reads
`.current-gear` at the top of every iteration. Writing `review-before` from another terminal stops
the loop cleanly after the task in flight — nothing already completed is unwound, and
`/spec:implement` picks up from the next unchecked task under the restored gate. This is the same
mechanism as the `RALPH_STOP` kill-switch, but it changes gear rather than halting.

### 5. What a gear change does *not* touch

Shifting to `review-after` removes the **human approval pause** and nothing else. These are
non-negotiable in either gear, and both commands state them explicitly so a gear change cannot
silently drop them:

- **RED first.** The test is written and observed to fail *for the right reason* before any
  production code exists. Ungated does not mean test-after.
- **The full regression suite**, not merely the new test's own `--filter`.
- **The two-commit shape**: a `feat:`/`test:` commit for the behaviour, then a separate `docs:`
  commit ticking the task off in `tasks.md`.
- **Every standing test-authoring convention**: TestDoubles one class per file; a distinct request
  type per new test double so assembly scans do not collide; new closed generics registered with
  each test project's logging `Initializer.cs`; `When_[condition]_should_[behavior]` naming; no
  mocks for isolation; `InMemory*` for I/O.

### 6. Consequence for the ralph commands

Because gear and task-list format are now separated, `/spec:ralph-implement` reads the **existing
`tasks.md`** — skipping tasks already ticked, and handling `STRUCTURAL` and `DOC` task shapes
alongside `TEST + IMPLEMENT`. It gains `.tasks-approved` as a prerequisite, which it previously did
not have.

`/spec:ralph-tasks` is therefore **removed**. Its only purpose was to regenerate a task list in a
format that expressed "no approval gate", and the gear expresses that directly. `tasks.md` becomes
the single task-list format, and the certainty fork at `/spec:approve design` becomes a choice of
*driver* (one task at a time, or a self-driving loop) rather than a fork in the artifacts produced.

### 7. Consequence for `CLAUDE.md`

`CLAUDE.md` and `.agent_instructions/testing.md` stop saying the gate cannot be bypassed. They say
instead that the gate is **armed by default**, that `/spec:gear` is the only supported way to
disarm it, that the disarm is scoped to one spec (and optionally one phase), and that the
disciplines in §5 hold in either gear. The documented behaviour and the actual behaviour agree
again, which the `PROMPT.md` waiver never achieved.

## Consequences

### Positive

- Shifting gear is a one-line, visible, reversible gesture instead of a prose waiver that
  contradicts the project's own documentation.
- A frozen, partially-implemented `tasks.md` can now be finished unattended without regenerating
  anything.
- One task-list format instead of two; one fewer command to maintain and document.
- The disciplines that matter (RED-first, full suite, two-commit shape, conventions) are stated as
  gear-independent, so they cannot be dropped by accident along with the pause.
- The reason for each shift is recorded in `shifted:`, so "why is this running ungated?" has an
  answer at the point of use.

### Negative

- The gear is untracked, so it is invisible in review and in the diff. A reviewer cannot tell from
  the commits alone which tasks were reviewed per-test and which in a batch. Mitigated by the
  `PROMPT.md` pointer and `/spec:status`, but not eliminated — this is the price of keeping
  operational state out of the permanent record.
- One more piece of state a session must resolve before acting, and one more file that can be stale
  (e.g. a `scope:` naming a phase that has since been completed).
- `/spec:ralph-implement` must now cope with the full generality of `tasks.md`, including task
  shapes it never had to parse before.

### Risks and Mitigations

**Risk**: A gear left in `review-after` and forgotten, so later, less certain work runs ungated
without anyone choosing that.
- **Mitigation**: `scope:` narrows the gear to one phase, so it expires naturally as the phase
  completes; resolution rule 3 falls back to `review-before` outside the scope. `/spec:status` and
  the start of every `/spec:implement` run report the resolved gear and the `shifted:` reason.

**Risk**: The gear file is treated as permission to skip RED-first or the regression suite.
- **Mitigation**: §5 is restated verbatim in `test-first.md`, `implement.md` and
  `ralph-implement.md`, adjacent to the gear check itself rather than in a distant document.

**Risk**: Someone reintroduces a global gear (repo root, or a settings flag) for convenience.
- **Mitigation**: This ADR records *why* the gear is per-spec. The resolution rules require a
  `specs/.current-spec` and a spec-local file; there is no root path to find.

## Alternatives Considered

### Alternative 1: A mode line inside `tasks.md`

Encode the gear per phase in the task list itself, e.g. a `Gear: review-after` line under each phase
heading.

**Rejected because**:
- `tasks.md` is frozen once `.tasks-approved` lands. Shifting gear would mean lifting that freeze,
  which is a far heavier gesture than the shift warrants and erodes the meaning of the approval.
- It puts operational state into a reviewed, tracked artifact, so every gear change becomes a commit
  and a potential merge conflict.
- It cannot be changed from another terminal while a loop is running, which kills the mid-run
  downshift.

### Alternative 2: A command argument only, with nothing persisted

`/spec:implement --review-after` per invocation; `/spec:ralph-implement` ungated by definition.

**Rejected because**:
- Nothing survives a context reset or a fresh session, so the gear must be re-stated constantly —
  which is how the prose-waiver problem started.
- There is no way to downshift a running loop; the only lever left is stopping it.
- The reason for the shift has nowhere to live.

### Alternative 3: A tracked marker beside `.tasks-approved`

A committed `.review-mode` file, in the same style as the existing approval markers.

**Rejected because**:
- It is the wrong category. The approval markers record *decisions that were made and reviewed*; the
  gear records *where the work currently is*. Tracking it puts an hourly-changing value into branch
  history and invites conflicts.
- It makes the gear branch-derived rather than operator-chosen, which is not how a gear is selected.

### Alternative 4: Keep `/spec:ralph-implement` as the only ungated path

Leave the gate unconditional in `/spec:implement` and treat "go unattended" as "switch to the ralph
commands".

**Rejected because**:
- It preserves exactly the conflation this ADR removes: to change gate state you must also change
  task-list format, abandoning a frozen `tasks.md` and every task shape it expresses.
- It offers no downshift — returning to gated review means abandoning `ralph-tasks.md` and
  reconciling two task lists by hand.

## References

- Issue: [#4357 — Add a first-class gear-switch for `/test-first`'s approval gate](https://github.com/BrighterCommand/Brighter/issues/4357)
- Skills: [`.claude/commands/tdd/test-first.md`](../../.claude/commands/tdd/test-first.md),
  [`.claude/commands/spec/implement.md`](../../.claude/commands/spec/implement.md),
  [`.claude/commands/spec/gear.md`](../../.claude/commands/spec/gear.md),
  [`.claude/commands/spec/ralph-implement.md`](../../.claude/commands/spec/ralph-implement.md)
- Guidance: [`CLAUDE.md`](../../CLAUDE.md),
  [`.agent_instructions/testing.md`](../../.agent_instructions/testing.md)
- Related ADRs:
  - [ADR 0065: Add Frontmatter to ADRs](0065-add-frontmatter-to-adrs.md) — prior art for an ADR that
    records a decision about the project's own tooling rather than its runtime.
