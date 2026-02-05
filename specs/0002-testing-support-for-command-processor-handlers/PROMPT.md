# Resume Prompt: Testing Support for Command Processor Handlers

**Spec ID**: 0002-testing-support-for-command-processor-handlers
**Last Updated**: 2026-02-05

## Current Status

```
[x] Requirements - APPROVED
[x] Design (ADRs) - 2 ADRs created, 1 approved, 1 pending
[ ] Tasks - Not yet created
[ ] Implementation - Not started
```

## Completed Work

### Requirements (Approved)
- `specs/0002-testing-support-for-command-processor-handlers/requirements.md`
- Defines the need for testing tools for handlers that depend on `IAmACommandProcessor`
- Two deliverables: new `Paramore.Brighter.Testing` assembly + documentation

### ADRs Created

| ADR | File | Status | Description |
|-----|------|--------|-------------|
| 0037 | `Docs/adr/0037-testing-assembly-structure.md` | **Accepted** | Assembly structure, project layout, key components |
| 0038 | `Docs/adr/0038-spy-command-processor-api.md` | **Proposed** | SpyCommandProcessor public API design (3-layer approach) |

## Key Design Decisions

1. **New Assembly**: `Paramore.Brighter.Testing` in `src/`
2. **Three Types**:
   - `CommandType` enum - tracks which methods were called
   - `RecordedCall` record - captures call details
   - `SpyCommandProcessor` class - main spy implementation
3. **Three-Layer API**:
   - Layer 1: Quick checks (`WasCalled`, `CallCount`, `Observe<T>`)
   - Layer 2: Request inspection (`GetRequests<T>`, `GetCalls`, `Commands`)
   - Layer 3: Full detail (`RecordedCalls`, `DepositedRequests`)
4. **Virtual Methods**: All interface implementations for extensibility

## Next Steps

To resume, run these commands in order:

```bash
# 1. Approve the pending ADR
/spec:approve design 0038

# 2. Create implementation tasks
/spec:tasks

# 3. Review and approve tasks
/spec:approve tasks

# 4. Begin TDD implementation
/spec:implement
```

## Key Files to Reference

| File | Purpose |
|------|---------|
| `specs/0002-.../requirements.md` | Full requirements |
| `Docs/adr/0037-testing-assembly-structure.md` | Assembly design |
| `Docs/adr/0038-spy-command-processor-api.md` | API design |
| `src/Paramore.Brighter/IAmACommandProcessor.cs` | Interface to implement |
| `tests/.../TestDoubles/SpyCommandProcessor.cs` | Reference implementation |

## Context Summary

Users want to unit test handlers that depend on `IAmACommandProcessor`. The solution:

1. **Create `Paramore.Brighter.Testing` NuGet package** with:
   - `SpyCommandProcessor` - records all method calls for verification
   - Supports outbox pattern (DepositPost/ClearOutbox)
   - Virtual methods for custom spy behavior

2. **Create documentation** at `Docs/guides/testing-handlers.md`:
   - Using SpyCommandProcessor
   - Using mocking frameworks (Moq, NSubstitute, FakeItEasy)
   - Using in-memory bus for integration tests

## Quick Resume Command

```
Continue work on spec 0002-testing-support-for-command-processor-handlers.
The requirements and ADR 0037 are approved. ADR 0038 is pending approval.
Next step: approve ADR 0038, then create tasks with /spec:tasks.
```
