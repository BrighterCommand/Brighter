---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(dotnet:*), Read, Write, Edit, Glob, Grep, AskUserQuestion
description: Write the failing regression test for a confirmed bug (delegates to /test-first)
argument-hint: (no arguments — operates on the active bug)
---

## Context

Current bug: !`cat bugfixes/.current-bug 2>/dev/null || echo "No active bug"`
Confirm gate (the active bug above must appear in this list): !`ls -1 bugfixes/*/.confirm-approved 2>/dev/null || echo "(none confirmed yet — run /bugfix:confirm first)"`

## Your Task

**Workflow**: Issue → Triage → Confirm (✋ gate) → **Test-first (✋ gate)** → Fix → Verify

Write the **failing regression test** that pins the confirmed bug. This step is a thin wrapper
around `/test-first` — it carries the confirmed diagnosis in, and `/test-first`'s own mandatory
IDE-approval gate governs the test before any implementation.

> **Recommended model: `sonnet`.** This step writes a test in the main agent and runs `dotnet`,
> so it is implementation work — match the model policy for implementation
> (`.claude/commands/bugfix/README.md` → "Sub-agents & model policy"). If your session is on
> opus, consider switching to sonnet first.

### Step 1: Enforce the Confirm Gate

1. Read `bugfixes/.current-bug`. If absent, tell the user to run `/bugfix:triage` and stop.
2. Verify `bugfixes/{current-bug}/.confirm-approved` exists. **If it does not, stop** and tell the
   user to run `/bugfix:confirm` first — a regression test must pin a *confirmed* cause, not an
   assumed one.
3. Read `bugfixes/{current-bug}/bugfix.md` for the Confirmed Root Cause, Evidence, and **Scope
   Notes** (the test set must also cover any additional defects surfaced at Confirm).

### Step 2: Delegate to /test-first

Hand the confirmed behaviour to the `/test-first` workflow. Frame the behaviour from the
**Confirmed Root Cause**, not the raw symptom — e.g. for #4054:
`/test-first when a message with a SessionId header is round-tripped through the ASB
serializer the reserved header is read back case-insensitively and not leaked into
ApplicationProperties`.

Follow `/test-first` exactly:
- 🔴 RED: write the test per `.agent_instructions/testing.md` conventions
  (`When_[condition]_should_[behavior]`, one test per file, Arrange/Act/Assert, Evident Data,
  InMemory* for I/O, public exports only) and run it to confirm it fails for the right reason.
- ✋ **APPROVAL GATE**: `/test-first` requires explicit user approval of the test before any
  implementation. Honour it — do not write the fix here.

If the Scope Notes listed additional defects, write a failing test for each (run `/test-first`
once per behaviour) so the fix can't leave a known defect uncovered.

### Step 3: Record the Test

Use `Edit` to fill the **Regression Test** section of `bugfixes/{current-bug}/bugfix.md` with the
test file path(s), and set Status to `Tested`.

### Step 4: Next Steps

Remind the user:
- The regression test is red and approved. Run `/bugfix:fix` to make it green with the **minimal**
  change scoped to the confirmed cause.
- Do **not** broaden the change beyond what the confirmed cause (and Scope Notes) require.
