# Review: tasks — 0022-Defer-Message-Action-Backstop-Handler

**Date**: 2026-04-17
**Threshold**: 60
**Verdict**: NEEDS WORK

> **Note**: This phase is already approved. Findings are informational — consider whether any warrant re-opening the phase.

1 finding at or above threshold 60. Address these before approving.

## Findings

### 1. Task 7 only tests Proactor path but implementation modifies both Reactor and Proactor (Score: 70)

Task 7 specifies a single test file in `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/` and says the implementation touches both `Proactor.cs` and `Reactor.cs`. However, the task only has one test file covering the Proactor (async) path. There is no corresponding test for the Reactor (sync) path with the delay override.

**Evidence**: Task 7 says:
> "In `Proactor.cs`: Change `catch (DeferMessageAction)` to `catch (DeferMessageAction deferAction)`, extract `deferAction.Delay`, pass to `RequeueMessage`"
> "In `Reactor.cs`: Same change for the sync path"

But only one test file is listed: `When_a_command_handler_throws_a_defer_message_with_delay_Then_message_is_requeued_with_delay.cs` in the `Proactor/` directory.

**Recommendation**: Add a separate test task (or expand Task 7) to include a Reactor-path test: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/When_a_command_handler_throws_a_defer_message_with_delay_Then_message_is_requeued_with_delay.cs`.

---

### 2. Task 7 test verifies only the "with delay" case; the "null delay fallback" case is described but has no separate test file (Score: 55)

Task 7 bullet says to verify two behaviors: (1) when `Delay` is set, pump uses it, and (2) when `Delay` is null, pump falls back to subscription `RequeueDelay`. Only one test file is specified, which presumably covers both cases in a single test class.

**Evidence**: Task 7 lists:
> "When handler throws `DeferMessageAction` with a `Delay` value, the pump passes that delay to `Channel.RequeueAsync`"
> "When handler throws `DeferMessageAction` without a `Delay` (null), the pump falls back to subscription `RequeueDelay`"

But only one test file is named.

**Recommendation**: Consider splitting into two test files or at minimum ensure the single test file clearly has two separate test methods.

---

### 3. Task 1 describes work already completed in codebase (Score: 45)

All tasks appear to be already implemented in the codebase (all four types exist, all test files exist, pump changes are in place), but all checkboxes remain unchecked.

**Evidence**: `DeferMessageAction.cs` already has `TimeSpan? Delay`, all constructors, and all files referenced in Tasks 2-7 exist. All checkboxes are `- [ ]`.

**Recommendation**: Mark completed tasks with `[x]` to reflect actual state.

---

### 4. Branch name in header is stale (Score: 30)

The tasks.md header says `**Branch**: \`error_examples\` (or new feature branch)` but the current branch is `replay_on_seen`.

**Evidence**: Line 6: `**Branch**: \`error_examples\` (or new feature branch)`

**Recommendation**: Update the branch name or remove it.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 4
**Findings at or above threshold (60)**: 1
