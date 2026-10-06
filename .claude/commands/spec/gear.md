---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(date:*), Bash(grep:*), Bash(rm:*), Read, Write, Edit, Glob, AskUserQuestion
description: Report or shift the TDD review gear for the current spec
argument-hint: [review-before|review-after] ["phase scope"] [--because "reason"]
---

## Context

Current spec: !`cat specs/.current-spec 2>/dev/null || echo "(none — run /spec:switch or /spec:new)"`

**The gear** is which TDD review mode the current spec is being run in. It is *operational* state —
you shift it as the work goes, adjusting for how certain you are and how large the blast radius is.
Early in a spec, when the design is still moving, you want every test reviewed. Later, on a run of
near-identical tasks whose shape has been approved six times already, that pause is friction. Shift
up. The moment the work starts producing tests you would not have approved, shift back down.

| Gear | Gate | Meaning |
|------|------|---------|
| `review-before` | ✅ armed | The user reviews each test in their IDE **before** implementation. **The default.** |
| `review-after` | ➖ not armed | The user reviews a **batch** of completed work after the fact. |

See [ADR 0071](../../../docs/adr/0071-tdd-review-gear.md) for why this exists and why it lives where
it does.

## The gear file

`specs/{current-spec}/.current-gear` — **gitignored**. It is where the work currently *is*, not what
the project decided, so it is never committed.

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
| `scope` | no | A **section** of `tasks.md` the gear applies to — either a heading's text (any depth: `##`–`####`, a phase, a task group, whatever the file uses) or a task range, `tasks N-M`, for flat lists with no useful headings. Tasks outside it fall back to `review-before`. Absent = the whole spec. |
| `driver` | no | `implement` \| `ralph` — which command is expected to be driving. |
| `shifted` | yes | Date + a one-line reason. A gear change is a judgement about certainty; the reason is the interesting part. |

## Resolution rules (the canonical algorithm)

Every command that honours the gear resolves it **this way, in this order**. Copy this, do not
improvise:

1. No `specs/.current-spec`, or no `.current-gear` in that spec directory → **`review-before`**.
2. The file exists but cannot be parsed, or `gear:` holds anything other than the two known
   values → **`review-before`**, and **say so out loud**. Fail safe, never fail open.
3. `scope:` is present and the task about to be worked falls **outside** it → **`review-before`**.
   See *Reading a `scope:`* below — `tasks.md` files are not uniformly structured, so resolve the
   scope against what the file actually contains rather than assuming it has phases.
4. Otherwise → the gear named in the file.

## Reading a `scope:`

`tasks.md` is **not** uniformly structured across this repository, so do not assume a phase layout.
Of the task lists in `specs/`, roughly half use `## Phase N: …` or `### Phase N: …`; the rest use
`## Task N: …`, a single flat `## Tasks` section, or numbered subsections like `#### 13.A.7`.
Heading depth varies from `##` to `####` within the same convention.

A `scope:` value is therefore matched in this order:

1. **`tasks N-M`** (e.g. `tasks 6-11`) — an inclusive range over the task numbering the file itself
   uses. Use this for flat lists. This is the only form that works when a file has no useful
   headings at all.
2. **A heading's text** — match it against every markdown heading in `tasks.md` at any depth,
   comparing the heading text with the leading `#`s, any leading number, and surrounding whitespace
   stripped. The scope covers tasks from that heading until the next heading **of the same or
   shallower depth**.

If a `scope:` matches **nothing** in `tasks.md` — a renamed heading, a typo, a range past the end of
the list — that is resolution rule 2: treat the gear as **`review-before`**, and say the scope did
not match. A scope that silently matches nothing must never read as "no scope, so spec-wide"; that
would widen the gear at the exact moment it stopped being understood.

## Who honours the gear

| Caller | Behaviour |
|--------|-----------|
| `/spec:implement` | Resolves the gear per task and runs gated or ungated accordingly. |
| `/spec:ralph-implement` | Always `review-after`; re-reads the gear before **each** task, so a downshift stops the loop. |
| `/test-first` standalone | **Always gated.** It does not read this file. |
| `/bugfix:test` | **Always gated.** Bugfixes have their own Confirm gate; they are outside this mechanism. |

A standalone `/test-first` must not inherit a mode it did not ask for from whichever spec happens to
be current. The spec commands resolve the gear and pass the result *into* the `/test-first` workflow
explicitly — the caller opts in; the shared skill stays safe by default.

## Your Task

Parse `$ARGUMENTS`:

- **empty** → report (below)
- **`review-before`** → downshift
- **`review-after`** → upshift
- an optional quoted **phase scope** as the second positional argument
- an optional **`--because "reason"`**

### Report (no arguments)

1. Read `specs/.current-spec`. If absent, say so and stop.
2. Resolve the gear with the rules above and print:

```
Spec:   specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/
Gear:   review-after  (➖ approval gate NOT armed)
Scope:  Phase 5 — Provider rejection tests   (tasks outside this phase run review-before)
Driver: ralph
Since:  2026-09-13 — Phase 4's six tests were all approved unchanged; the shape is settled

Shift down with:  /spec:gear review-before
```

If there is no gear file, report the default plainly — `Gear: review-before (✅ approval gate armed —
default; no gear file)` — and do **not** create one. Absence is a valid, correct state.

### Upshift — `/spec:gear review-after ["phase"] [--because "..."]`

1. Verify `specs/.current-spec` exists and that spec directory is present. If not, stop.
2. **Require a reason.** If `--because` was not given, ask for one with `AskUserQuestion` — offer
   the common cases (e.g. "the phase's task shape has been reviewed and accepted repeatedly", "these
   are mechanical, one-acceptance-criterion tasks", "re-running a phase that was already reviewed")
   plus free text. Do not write the file without a reason; an unexplained ungated run is exactly the
   invisible waiver this mechanism replaces.
3. **Encourage a scope.** If none was given, read `tasks.md` and offer what that file actually
   provides — its section headings if it has meaningful ones, otherwise a task-number range —
   and ask whether to scope the gear or leave it spec-wide. A scoped gear expires naturally as
   its section completes; a spec-wide one does not. Do not insist: a genuinely flat, short task
   list is fine to run spec-wide.
4. Write `specs/{current-spec}/.current-gear` with the fields above, stamping `shifted:` with
   today's date and the reason.
5. Confirm what changed, and state the non-negotiables (below) so it is on the record that the
   pause is the *only* thing being removed.

### Downshift — `/spec:gear review-before`

1. Write the file with `gear: review-before` (keep the `scope:`/`driver:` if present, restamp
   `shifted:` with the reason), or simply delete the file — both resolve to the default. Prefer
   **rewriting** it when a loop may be running, so the running loop reads an explicit
   `review-before` rather than racing a deletion.
2. Tell the user it takes effect at the **next task**:
   - `/spec:implement` picks it up on its next task.
   - A running `/spec:ralph-implement` finishes the task in flight, then stops cleanly and hands
     back. **Nothing already completed is unwound or redone.**

### Record it in `PROMPT.md`

Because the gear file is untracked, a fresh session will not learn of it from the diff. Whenever the
gear is set to `review-after`, add or update a line in `PROMPT.md` (the project's convention for
session state that must survive a context reset) pointing at it:

```markdown
## Review gear
`specs/0027-…/.current-gear` → **review-after**, scoped to Phase 5. Downshift with `/spec:gear review-before`.
```

Remove that line on downshift. If `PROMPT.md` does not exist, offer to create it rather than
creating it unasked.

## What a gear change does NOT touch

Shifting to `review-after` removes the **human approval pause** and nothing else. State these
explicitly whenever you shift:

- **RED first.** The test is written and observed to fail *for the right reason* before any
  production code exists. Ungated is not test-after.
- **The full regression suite**, not just the new test's own `--filter`.
- **The two-commit shape**: a `feat:`/`test:` commit for the behaviour, then a separate `docs:`
  commit ticking the task off in `tasks.md`.
- **Every standing test-authoring convention**: TestDoubles one class per file; a distinct request
  type per new test double so assembly scans do not collide; new closed generics registered with
  each test project's logging `Initializer.cs`; `When_[condition]_should_[behavior]` naming; no mocks
  for isolation; `InMemory*` for I/O.

If a run in `review-after` is dropping any of these, that is a defect in the run, not a consequence
of the gear.

## Examples

```bash
/spec:gear                                        # what gear am I in, and why?
/spec:gear review-after "Phase 5 — Provider rejection tests" \
    --because "Phase 4's six tests were all approved unchanged; the shape is settled"
/spec:gear review-before --because "the last two tests asserted the wrong thing"
```
