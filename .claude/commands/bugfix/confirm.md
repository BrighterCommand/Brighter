---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Read, Write, Edit, Glob, Grep, Agent, AskUserQuestion
description: Confirm the root-cause hypothesis before any fix (the ✋ Confirm gate)
argument-hint: (no arguments — operates on the active bug)
---

## Context

Current bug: !`cat bugfixes/.current-bug 2>/dev/null || echo "No active bug"`

## Your Task

**Workflow**: Issue → Triage → **Confirm (✋ gate)** → Test-first (✋ gate) → Fix → Verify

This is the step that makes `/bugfix` different from a plain `/test-first`. **Prove the root-cause
hypothesis before writing any fix.** A suggested fix — even an agent-authored one — is unverified
until this passes. Confirming the theory frequently changes the *scope* of the fix (e.g. issue
#4054: confirming the camelCase serialization cause surfaced a *second* defect that the suggested
fix only partially addressed).

**Sub-agent**: The diagnostic proof (tracing the code) is delegated to a sub-agent
(`subagent_type: "Plan"`, **`model: "opus"`**). It is read-only — it traces, it does not fix, and
it RETURNS its findings as text. The **main agent** writes the findings into `bugfix.md` and owns
the user-facing **Confirm gate** (`Plan` has no `AskUserQuestion`, so it cannot run the gate). See
`.claude/commands/bugfix/README.md` → "Sub-agents & model policy".

### Step 1: Load State

1. Read `bugfixes/.current-bug`. If absent, tell the user to run `/bugfix:triage` first and stop.
2. Read `bugfixes/{current-bug}/bugfix.md`. If the Root-Cause Hypothesis is empty, tell the user
   to complete `/bugfix:triage` first and stop.

### Step 2: Launch Sub-Agent to Prove or Refute

Launch an `Agent` with `subagent_type: "Plan"` and **`model: "opus"`**. The prompt MUST include:

1. The full `bugfix.md` text (Symptom, Suspected Location, Root-Cause Hypothesis).
2. The instruction below and the confirmation criteria.
3. An explicit instruction: **RETURN the findings as markdown text. Do NOT write any file. Do NOT
   apply a fix. Do NOT ask the user any questions.**

**Confirmation criteria for the sub-agent:**

- **Trace the code** from the symptom to the suspected cause using `Read`, `Glob`, `Grep`. Cite
  concrete `file:line` evidence at every step. The goal is to either **prove** or **refute** the
  hypothesis — be adversarial, do not rubber-stamp it.
- **Evidence standard (red repro preferred, code-trace allowed):**
  - A documented **code-trace** that proves the cause is sufficient — this is the accepted path
    when an executable reproduction would need live infrastructure (a real broker, database, etc.).
  - If the bug *can* be reproduced cheaply (pure logic, no live infra), describe the **red repro**:
    the exact assertion that fails today and why. The durable executable test is written later by
    `/bugfix:test` — Confirm does not write source.
- **Check the suggested fix** (if the issue/triage proposed one): does the proven cause match it?
  Is it complete, or does it address only a symptom? State explicitly whether it is
  CONFIRMED / PARTIAL / WRONG.
- **Hunt for scope changes**: does the proven cause imply *additional* defects, related call
  sites, or cross-backend parity gaps the suggested fix misses? List them — these change the fix's
  scope.

**Sub-agent output format:**

```markdown
## Verdict
CONFIRMED | REFUTED — one line on whether the hypothesis holds.

## Confirmed Root Cause
The proven cause, in precise terms.

## Evidence
- [x] Code-trace: step-by-step with `file:line` references, OR
- [ ] Red repro: the assertion that fails today and why
(tick whichever applies; a trace alone is valid for infra-bound bugs)

## Suggested-Fix Assessment
CONFIRMED / PARTIAL / WRONG — and why.

## Scope Notes
Additional defects, related sites, or cross-backend parity gaps the fix must also cover (or
"none found"). Be specific with `file:line`.
```

### Step 3: Write Findings Into bugfix.md

After the sub-agent returns, use `Edit` to fill the **Confirmed Root Cause**, **Evidence**, and
**Scope Notes** sections of `bugfixes/{current-bug}/bugfix.md` with the validated findings.

- If the verdict is **REFUTED**: update the Root-Cause Hypothesis to reflect what was learned, set
  Status back to `Triaged`, tell the user the hypothesis did not hold, and recommend re-running
  `/bugfix:triage` (or refining the hypothesis) — do **not** create the approval marker. Stop here.

### Step 4: ✋ Confirm Gate (MAIN agent — CRITICAL)

**You MUST get explicit user approval of the diagnosis before any test or fix.** Present:
- The Confirmed Root Cause and the Evidence
- The Suggested-Fix Assessment
- Any Scope Notes (especially additional defects — these may widen the fix)

Use `AskUserQuestion`:

```
Question: "I've confirmed the root cause: {one-line}. Approve this diagnosis and proceed to write
           the regression test?"
Options:
1. "Yes, the diagnosis is confirmed" — proceed
2. "Revise the diagnosis" — user explains; loop back to Step 2
3. "Cancel" — stop
```

**On approval only:** `touch bugfixes/{current-bug}/.confirm-approved` and set Status to
`Confirmed` in `bugfix.md`.

### Step 5: Next Steps

Remind the user:
- The diagnosis is approved. Run `/bugfix:test` to write the failing regression test (it delegates
  to `/test-first`, which has its own approval gate).
- If the Scope Notes widened the fix, the regression test(s) should cover the additional defects
  too.
