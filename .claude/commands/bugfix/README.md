# Bugfix Workflow Commands

This directory contains Claude Code commands that implement a **lightweight, diagnosis-first
workflow for fixing bugs**. It is the bug-shaped counterpart to the heavyweight `/spec:*`
workflow: where `/spec` runs Requirements → ADR → adversarial review → tasks → implement (right
for *features*), `/bugfix` runs **Triage → Confirm → Test-first → Fix → Verify** and deliberately
omits the ADR/requirements/review rounds.

## Why a separate workflow?

Bugs differ from features in one important way: **the root cause is a hypothesis until proven.**
Issues often arrive with a suggested fix (sometimes from an automated agent), but a suggested fix
can be wrong, incomplete, or address a *symptom* rather than the cause. `/test-first` jumps
straight to writing a test for an *assumed* behaviour — it has no gate for *confirming the
diagnosis first*.

`/bugfix` is essentially `/test-first` wrapped with an explicit **Confirm** gate up front, plus a
triage step that records the hypothesis.

**Worked example — #4054 (ASB `SessionId` case-sensitivity).** The issue arrived with a plausible
suggested fix. Before writing any code, Confirm proved the root cause by reading
`JsonSerialisationOptions` (`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`) — keys are
camelCased on serialization round-trips — and surfaced a *second* defect the suggested fix only
partially addressed (reserved-header stripping is also case-sensitive, so reserved keys leak into
`ApplicationProperties`). Confirming the theory **changed the scope of the fix.** That is exactly
what the Confirm gate exists to catch.

## Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                         Bugfix Workflow                          │
└─────────────────────────────────────────────────────────────────┘

 GitHub Issue / bug report
      │
      ▼
 Triage ──────────────► /bugfix:triage [issue-number | description]
      │                 (restate symptom, locate code, form hypothesis;
      │                  any suggested fix is UNVERIFIED)
      ▼
 Confirm ─────────────► /bugfix:confirm
      │                 ✋ Confirm gate — prove the hypothesis (code-trace
      │                    and/or red repro) before any fix; .confirm-approved
      │   (refuted → back to triage)
      ▼
 Test-first ──────────► /bugfix:test
      │                 ✋ delegates to /test-first (approve test in IDE)
      ▼
 Fix ─────────────────► /bugfix:fix
      │                 (minimal change to green, scoped to confirmed cause;
      │                  /tidy-first if structural cleanup needed first)
      ▼
 Verify ──────────────► /bugfix:verify
      │                 (run suite; regression test stays; capture root cause,
      │                  Fixes #N — no auto-close)
      ▼
 Pull Request
```

## The two gates

| Gate | Where | What it guarantees |
|------|-------|--------------------|
| ✋ **Confirm** | `/bugfix:confirm` | The root-cause diagnosis is **proven**, not assumed, and the fix's scope (including any extra defects) is agreed before code is written. The one on-disk marker: `.confirm-approved`. |
| ✋ **Test approval** | `/bugfix:test` → `/test-first` | The failing regression test is reviewed in the IDE before any implementation (inherited from `/test-first`). |

### Confirm evidence standard — red repro preferred, code-trace allowed

Confirm passes on **either**:
- an executable **red reproduction** (preferred when the bug reproduces cheaply — pure logic, no
  live infra), **or**
- a documented **code-trace** that proves the cause (the accepted path when a repro would need a
  real broker/database/etc., as in #4054).

The durable, executable regression test is written later by `/bugfix:test` (via `/test-first`) —
Confirm itself stays read-only and writes no source.

## Sub-agents & model policy

Modelled on `/spec` (see `.claude/commands/spec/README.md` → "Sub-agents & model policy"):
reasoning-heavy, read-only steps delegate to a **`Plan`** sub-agent (no file-editing tool, no
`AskUserQuestion`) which RETURNS its findings as text; the **main agent** owns all user
interaction (the Confirm gate) and all bookkeeping (writing `bugfix.md`, markers, `gh`, git).

| Command | Sub-agent (type) | Model | Rationale |
|---------|------------------|-------|-----------|
| `/bugfix:triage`  | Yes — `Plan` (read-only) | **opus** | Diagnostic analysis / locating code |
| `/bugfix:confirm` | Yes — `Plan` (read-only) | **opus** | Adversarial root-cause proof |
| `/bugfix:test`    | No — delegates to `/test-first` | **sonnet** | Implementation (interactive approval gate) |
| `/bugfix:fix`     | No (main agent) | **sonnet** | Implementation work |
| `/bugfix:verify`  | No (main agent) | — | Mechanical: run suite, prepare commit/PR |
| `/bugfix:status`, `/bugfix:switch` | No | — | Bookkeeping |

`triage` and `confirm` use `Plan` so the "return as text, don't write the file" rule is hard to
violate accidentally (no `Write`/`Edit`). `test`/`fix` run in the **main agent** because the
test-approval gate is interactive and the fix writes source — set the session model to **sonnet**
for those, matching the implementation policy.

## State model (lightweight — single record + one marker)

```
bugfixes/
├── .current-bug                     # tracks the active bug (like specs/.current-spec)
└── 0001-asb-sessionid-case/
    ├── .issue-number                # linked GitHub issue (optional)
    ├── .confirm-approved            # the ONE gate marker
    └── bugfix.md                    # symptom / hypothesis / root cause / evidence / fix
```

Unlike `/spec` (three approval markers), `/bugfix` has a **single** marker (`.confirm-approved`).
Every other phase is inferred from the content of `bugfix.md`, keeping the workflow light.

### `bugfix.md` sections (filled progressively)

| Section | Filled by |
|---------|-----------|
| Symptom, Suspected Location, Root-Cause Hypothesis | `/bugfix:triage` |
| Confirmed Root Cause, Evidence, Scope Notes | `/bugfix:confirm` |
| Regression Test | `/bugfix:test` |
| Fix | `/bugfix:fix` |
| Status (Triaged → Confirmed → Tested → Fixed → Verified) | each step |

## Commands

### `/bugfix:triage [issue-number | description]`
Start a bug. Sets up `bugfixes/NNNN-slug/` and `.current-bug`, pulls the issue via `gh`, and (via
a `Plan` sub-agent) restates the symptom, locates the suspect code, and forms a root-cause
hypothesis. Any suggested fix is recorded as **UNVERIFIED**. Offers a `bugfix/N-slug` branch.

### `/bugfix:confirm`
The ✋ Confirm gate. A `Plan` sub-agent proves or refutes the hypothesis by tracing the code
(and/or describing a red repro), citing `file:line` evidence, and hunts for scope changes /
additional defects. The main agent writes the findings into `bugfix.md` and asks the user to
approve the diagnosis; on approval it creates `.confirm-approved`. A refuted hypothesis loops back
to triage.

### `/bugfix:test`
Requires `.confirm-approved`. Delegates to `/test-first` to write the failing regression test for
the confirmed behaviour (and one per additional defect in the Scope Notes). The `/test-first`
IDE-approval gate applies.

### `/bugfix:fix`
Requires `.confirm-approved` + a red test. Makes the test green with the **minimal** change scoped
to the confirmed cause — no speculative edits, no default changes. If structural cleanup is needed
first, defers to `/tidy-first` (separate `refactor:` commit, then the `fix:` commit).

### `/bugfix:verify`
Runs the regression test(s) and the broader suite; the regression test stays. Suggests a `fix:`
commit and PR body that capture the confirmed root cause and link the issue with `Fixes #N` —
without closing the issue directly (merge closes it).

### `/bugfix:status`
Lists all bugs and derives each phase from `bugfix.md` + `.confirm-approved`.

### `/bugfix:switch <bug-slug>`
Switches the active bug (updates `bugfixes/.current-bug`).

## When to use `/bugfix` vs other workflows

- **`/bugfix`** — a defect whose root cause is not yet proven, or that arrived with a suggested fix
  you should verify before trusting. The Confirm gate is the value-add.
- **`/test-first`** — a small change whose behaviour/cause is already obvious; you just need the
  test-first discipline.
- **`/tidy-first`** — the change is (or includes) structural cleanup; keep refactor and behaviour
  in separate commits. `/bugfix:fix` calls this when a fix needs cleanup first.
- **`/spec:*`** — a *feature* or capability needing requirements + ADR design, not a bug fix.

## Related Commands
- **`/test-first`** — `.claude/commands/tdd/test-first.md` (the test step delegates here)
- **`/tidy-first`** — `.claude/commands/refactor/tidy-first.md` (the fix step defers here when needed)
- **`/spec:*`** — `.claude/commands/spec/README.md` (the heavyweight feature workflow this mirrors)
