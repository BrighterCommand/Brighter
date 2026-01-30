# Current Work Context

## Active Specification

**Spec**: `specs/0002-backstop-error-handler`
**Branch**: `backstop_error_handler`

## Workflow Progress

- [x] Requirements approved (`specs/0002-backstop-error-handler/requirements.md`)
- [x] Design approved (`docs/adr/0037-reject-message-on-error-handler.md`)
- [x] Tasks created (`specs/0002-backstop-error-handler/tasks.md`)
- [ ] Implementation

## Feature Summary

**Goal**: Add a declarative middleware attribute that catches unhandled exceptions in the handler pipeline and converts them to `RejectMessageAction`, routing failed messages to DLQ.

**Components to implement**:
- `RejectMessageOnErrorAttribute` - sync attribute
- `RejectMessageOnErrorAsyncAttribute` - async attribute
- `RejectMessageOnErrorHandler<TRequest>` - sync handler
- `RejectMessageOnErrorHandlerAsync<TRequest>` - async handler

**File locations** (per ADR):
- `src/Paramore.Brighter/RejectMessageOnErrorAttribute.cs`
- `src/Paramore.Brighter/RejectMessageOnErrorAsyncAttribute.cs`
- `src/Paramore.Brighter/Handlers/RejectMessageOnErrorHandler.cs`
- `src/Paramore.Brighter/Handlers/RejectMessageOnErrorHandlerAsync.cs`

## Key Design Decisions

1. Use step 0 (outermost position) in pipeline
2. Catch all exceptions (no filtering)
3. Log exception before converting to `RejectMessageAction`
4. Pass original exception message and exception as inner exception

## Next Step

Run `/spec:implement` to begin TDD implementation.

## Reference Files

- Requirements: `specs/0002-backstop-error-handler/requirements.md`
- ADR: `docs/adr/0037-reject-message-on-error-handler.md`
- Similar pattern: `src/Paramore.Brighter/Policies/Handlers/FallbackPolicyHandler.cs`
- Action class: `src/Paramore.Brighter/Actions/RejectMessageAction.cs`
