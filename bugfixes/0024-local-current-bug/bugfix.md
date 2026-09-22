# Bugfix: Active bug selection is tracked as shared state

**Linked Issue**: #4314
**Status**: Verified

## Symptom

Selecting a bug changes the tracked `bugfixes/.current-bug` file. Unrelated bugfix
branches carry competing selections, causing checkout conflicts and merge noise.

## Suspected Location

- `bugfixes/.current-bug:1` — the active selection, tracked before the fix.
- `.gitignore` — no rule for the active selection before the fix.
- `.claude/commands/bugfix/triage.md:37` — creates the local pointer.
- `.claude/commands/bugfix/switch.md:20` — overwrites the pointer when selecting a bug.

## Root-Cause Hypothesis

The pointer is local working state accidentally tracked as shared project data.
Ignoring the exact path and removing it from the index should prevent selection
changes from appearing in commits while retaining the durable per-bug records.

## Confirmed Root Cause

**CONFIRMED** on upstream `535c5c3ee727dd9e4c444253c90b826792cba004`.
`git ls-files --stage bugfixes/.current-bug` returned a tracked entry, while
`git check-ignore --no-index -v bugfixes/.current-bug` returned no match.
Both triage and switch write this same path. An ignore rule alone cannot stop
changes to an already tracked file; untracking alone would allow accidental re-addition.

## Evidence

- `.claude/commands/bugfix/triage.md:30` creates the parent directory; `:37` writes
  the pointer.
- `.claude/commands/bugfix/switch.md:18` validates an existing bug directory before
  writing the pointer at `:20`.
- `.claude/commands/bugfix/status.md:17` treats the active selection as optional;
  `:18` enumerates the per-bug records independently.
- Confirm, test, fix, and verify already direct users to triage when the pointer is
  absent (`confirm.md:29`, `test.md:33`, `fix.md:24`, and `verify.md:21` in the same directory).

## Scope Notes

Only the local pointer is untracked. Per-bug `bugfix.md`, `.issue-number`, and
`.confirm-approved` files remain versioned. No command logic or runtime code changes.

## Regression Test

No automated regression test was added. Git checks cover this repository bookkeeping
change; no .NET test suite was run.

- Before the fix, the pointer was tracked and had no matching ignore rule.
- After the fix, `git ls-files --error-unmatch bugfixes/.current-bug` exited 1 and
  `git check-ignore -v bugfixes/.current-bug` identified the exact ignore rule.
- Removing the index entry preserved the local pointer and its contents.
- An isolated export of the relevant upstream files with the fix applied had no
  pointer; the command context expression reported `No active bug`.
- In a temporary Git repository, the pointer was recreated for an existing bug,
  then changed to another existing bug. After `git add .` on both selections,
  the pointer remained ignored and absent from the index, with no ordinary status entry.
- Representative per-bug records remained in the temporary index and did not
  match an ignore rule.

These checks exercise Git state and the documented shell behaviour, not an
end-to-end invocation of Claude Code slash commands.

## Fix

- Add `/bugfixes/.current-bug` to `.gitignore`.
- Remove `bugfixes/.current-bug` from Git's index, preserving the local file.
- Document local-only state and fresh-checkout selection in the bugfix README.
