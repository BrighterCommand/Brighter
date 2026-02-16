# Resume Prompt: Testing Support for Command Processor Handlers

**Spec ID**: 0003-testing-support-for-command-processor-handlers
**Last Updated**: 2026-02-06

## Current Status

```
[x] Requirements - APPROVED
[x] Design (ADRs) - 2 ADRs, both ACCEPTED
[x] Tasks - APPROVED
[x] Implementation - All 11 Phases COMPLETE
```

## Completed Work

### Requirements (Approved)
- `specs/0003-testing-support-for-command-processor-handlers/requirements.md`

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
| 8 | Complete Interface (Scheduled, Call, Transaction) | ✅ Complete |
| 9 | Extensibility (Virtual methods) | ✅ Complete |
| 10 | Documentation | ✅ Complete |
| 11 | Final Verification | ✅ Complete |

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
├── When_reset_clears_all_state.cs
├── When_scheduled_methods_record_scheduler_types.cs
├── When_call_invoked_records_and_returns_null.cs
├── When_transaction_provider_overloads_record_correctly.cs
└── When_subclass_overrides_executes_custom_behavior.cs

Docs/guides/
└── testing-handlers.md
```

### Test Coverage

- **85 tests passing** across net9.0 and net10.0
- All Phase 1-11 behaviors verified
- 0 build warnings on source assembly

### Git Commits

```
b9d3ceb48 fix: Test classes don't need GWT name
008e6a4c3 feat: implement SpyCommandProcessor Phases 4-7
826604ef7 chore: update PROMPT.md with Phase 1-3 completion status
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

### SpyCommandProcessor - Complete API

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
- Scheduled overloads record `CommandType.Scheduler`/`SchedulerAsync` and return scheduler IDs
- `Call<T, TResponse>()` records `CommandType.Call` and returns null
- Transaction provider overloads (`DepositPost<T, TTransaction>`) work correctly

**Extensibility:**
- All methods are virtual for subclass customization
- `ThrowingSpyCommandProcessor` example demonstrates override pattern

**Documentation:**
- `Docs/guides/testing-handlers.md` - comprehensive usage guide
- Core guide cross-reference added at Testing Strategies section

## Key Files

| File | Purpose |
|------|---------|
| `specs/0003-.../tasks.md` | Full task list with TDD commands |
| `specs/0003-.../requirements.md` | Requirements specification |
| `src/Paramore.Brighter.Testing/SpyCommandProcessor.cs` | Main implementation |
| `src/Paramore.Brighter.Testing/CommandType.cs` | Command type enum |
| `src/Paramore.Brighter.Testing/RecordedCall.cs` | Recorded call record |
| `Docs/adr/0037-testing-assembly-structure.md` | Assembly design |
| `Docs/adr/0038-spy-command-processor-api.md` | API design |
| `Docs/guides/testing-handlers.md` | Usage guide |
