# Resume Prompt: Testing Support for Command Processor Handlers

**Spec ID**: 0002-testing-support-for-command-processor-handlers
**Last Updated**: 2026-02-06

## Current Status

```
[x] Requirements - APPROVED
[x] Design (ADRs) - 2 ADRs, both ACCEPTED
[x] Tasks - APPROVED
[~] Implementation - Phases 1-3 COMPLETE, Phase 4+ remaining
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
| 4 | Layer 2 API (GetRequests, GetCalls, Commands) | ⏳ Next |
| 5 | Layer 3 API (RecordedCalls, DepositedRequests) | ⏳ Pending |
| 6 | Outbox Pattern Support | ⏳ Pending |
| 7 | State Management (Reset) | ⏳ Pending |
| 8 | Complete Interface (Scheduled, Call, Transaction) | ⏳ Pending |
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
└── When_observe_dequeues_requests_in_order.cs
```

### Test Coverage

- **22 tests passing** across net9.0 and net10.0
- All Phase 1-3 behaviors verified

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
- `Commands` - IReadOnlyList<CommandType> of calls in order
- `RecordedCalls` - IReadOnlyList<RecordedCall> with full details
- `WasCalled(CommandType)` - bool check if method was called
- `CallCount(CommandType)` - int count of calls
- `Observe<T>()` - dequeue next request of type T (FIFO)
- All `IAmACommandProcessor` interface methods (virtual)

## Next Steps

To resume implementation, run:

```bash
/spec:implement Phase 4 to Phase 7
```

### Phase 4 Tasks (Layer 2 API)
- [ ] `GetRequests<T>()` - returns all requests of type without consuming
- [ ] `GetCalls(CommandType)` - returns all RecordedCalls for a type
- [ ] `Commands` property already implemented

### Phase 5 Tasks (Layer 3 API)
- [ ] `RecordedCalls` property already implemented
- [ ] `DepositedRequests` - track outbox deposits by Id

### Phase 6 Tasks (Outbox Support)
- [ ] `ClearOutbox` moves deposited requests to observation queue
- [ ] Batch `DepositPost` overloads

### Phase 7 Tasks (State Management)
- [ ] `Reset()` clears all recorded state

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
Please read specs/0002-testing-support-for-command-processor-handlers/PROMPT.md to resume work on the Testing assembly. Phases 1-3 are complete. Continue with Phase 4 (Layer 2 API).
```
