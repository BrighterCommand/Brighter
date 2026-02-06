# Resume Prompt: Testing Support for Command Processor Handlers

**Spec ID**: 0002-testing-support-for-command-processor-handlers
**Last Updated**: 2026-02-06

## Current Status

```
[x] Requirements - APPROVED
[x] Design (ADRs) - 2 ADRs, both ACCEPTED
[x] Tasks - APPROVED
[~] Implementation - Phases 1-7 COMPLETE, Phase 8+ remaining
```

## Completed Work

### Requirements (Approved)
- `specs/0002-testing-support-for-command-processor-handlers/requirements.md`

### ADRs (Both Accepted)

| ADR | File | Status |
|-----|------|--------|
| 0037 | `Docs/adr/0037-testing-assembly-structure.md` | **Accepted** |
| 0038 | `Docs/adr/0038-spy-command-processor-api.md` | **Accepted** |

### Implementation Progress

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Project Setup | ✅ Complete |
| 2 | Core Types (CommandType, RecordedCall) | ✅ Complete |
| 3 | Layer 1 API (WasCalled, CallCount, Observe) | ✅ Complete |
| 4 | Layer 2 API (GetRequests, GetCalls, Commands) | ✅ Complete |
| 5 | Layer 3 API (RecordedCalls, DepositedRequests) | ✅ Complete |
| 6 | Outbox Pattern Support | ✅ Complete |
| 7 | State Management (Reset) | ✅ Complete |
| 8 | Complete Interface (Scheduled, Call, Transaction) | ⏳ Next |
| 9 | Extensibility (Virtual methods) | ⏳ Pending |
| 10 | Documentation | ⏳ Pending |
| 11 | Final Verification | ⏳ Pending |

### Files Created

```
src/Paramore.Brighter.Testing/
├── Paramore.Brighter.Testing.csproj
├── CommandType.cs
├── RecordedCall.cs
└── SpyCommandProcessor.cs

tests/Paramore.Brighter.Testing.Tests/
├── Paramore.Brighter.Testing.Tests.csproj
├── When_send_is_called_should_record_command_type.cs
├── When_async_methods_called_should_record_async_command_types.cs
├── When_method_called_should_capture_request_details.cs
├── When_was_called_checks_for_invoked_method.cs
├── When_call_count_returns_invocation_count.cs
├── When_observe_dequeues_requests_in_order.cs
├── When_get_requests_returns_matching_requests.cs
├── When_get_calls_returns_matching_recorded_calls.cs
├── When_deposit_post_called_tracks_in_deposited_requests.cs
├── When_clear_outbox_moves_requests_to_queue.cs
└── When_reset_clears_all_state.cs
```

### Test Coverage

- **46 tests passing** across net9.0 and net10.0
- All Phase 1-7 behaviors verified

### Git Commits

```
bcce195a3 feat: implement Paramore.Brighter.Testing assembly (Phases 1-3)
9acef4547 chore: add implementation tasks for testing support spec
```

## What's Implemented

### CommandType Enum
All 13 command types: Send, SendAsync, Publish, PublishAsync, Post, PostAsync, Deposit, DepositAsync, Clear, ClearAsync, Call, Scheduler, SchedulerAsync

### RecordedCall Record
```csharp
public record RecordedCall(CommandType Type, IRequest Request, DateTime Timestamp, RequestContext? Context = null);
```

### SpyCommandProcessor - Implemented API

**Layer 1 (Quick Checks):**
- `WasCalled(CommandType)` - bool check if method was called
- `CallCount(CommandType)` - int count of calls
- `Observe<T>()` - dequeue next request of type T (FIFO)

**Layer 2 (Request Inspection):**
- `GetRequests<T>()` - returns all requests of type without consuming
- `GetCalls(CommandType)` - returns all RecordedCalls for a type
- `Commands` - IReadOnlyList<CommandType> of calls in order

**Layer 3 (Full Details):**
- `RecordedCalls` - IReadOnlyList<RecordedCall> with full details
- `DepositedRequests` - IReadOnlyDictionary<Id, IRequest> for outbox tracking

**Outbox Pattern:**
- `DepositPost<T>()` / `DepositPostAsync<T>()` - deposits to DepositedRequests dictionary
- `ClearOutbox()` / `ClearOutboxAsync()` - moves deposited requests to observation queue

**State Management:**
- `Reset()` - clears all recorded state (calls, queue, deposits)

**Interface Methods:**
- All `IAmACommandProcessor` interface methods implemented (virtual)

## Next Steps

To resume implementation, run:

```bash
/spec:implement Phase 8 to Phase 11
```

### Phase 8 Tasks (Complete Interface)
- [ ] Verify scheduled Send/Publish/Post methods record `CommandType.Scheduler`
- [ ] Verify `Call<T, TResponse>()` records `CommandType.Call` and returns null
- [ ] Verify transaction provider overloads work correctly

### Phase 9 Tasks (Extensibility)
- [ ] Verify all methods are virtual for subclass customization
- [ ] Create example `ThrowingSpyCommandProcessor` in tests

### Phase 10 Tasks (Documentation)
- [ ] Create `Docs/guides/testing-handlers.md` guide
- [ ] Update core guide with cross-reference

### Phase 11 Tasks (Verification)
- [ ] Full solution builds without warnings
- [ ] All tests pass
- [ ] Update PROMPT.md to reflect completed status

## Key Files to Reference

| File | Purpose |
|------|---------|
| `specs/0002-.../tasks.md` | Full task list with TDD commands |
| `src/Paramore.Brighter.Testing/SpyCommandProcessor.cs` | Current implementation |
| `Docs/adr/0037-testing-assembly-structure.md` | Assembly design |
| `Docs/adr/0038-spy-command-processor-api.md` | API design |

## Code Style Notes

- Prefer primary constructors for simple classes
- Use Shouldly for assertions (not FluentAssertions)
- Test file naming: `When_[condition]_should_[behavior].cs`
- Use `/test-first` command for TDD workflow

## Quick Resume Command

```
Please read specs/0002-testing-support-for-command-processor-handlers/PROMPT.md to resume work on the Testing assembly. Phases 1-7 are complete. Continue with Phase 8 (Complete Interface).
```
