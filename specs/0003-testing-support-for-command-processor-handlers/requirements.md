# Requirements: Testing Support for Command Processor Handlers

**Spec:** 0003-testing-support-for-command-processor-handlers
**Created:** 2026-02-05
**Status:** Draft

---

## User Story

As a **Brighter user writing handlers**,
I want **tools and documentation to test handlers that depend on IAmACommandProcessor**,
So that **I can verify my handlers correctly raise requests, publish events, and use the outbox pattern**.

---

## Problem Statement

### The Challenge

Users write handlers that depend on `IAmACommandProcessor` to:
- Send commands to other handlers (`Send`, `SendAsync`)
- Publish events (`Publish`, `PublishAsync`)
- Post messages to external bus (`Post`, `PostAsync`)
- Use the outbox pattern (`DepositPost`, `ClearOutbox`)

When unit testing these handlers, users need to verify these interactions occurred correctly.

### Current State

1. **SpyCommandProcessor exists internally** at `tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/SpyCommandProcessor.cs`
   - Tracks command types via `Commands` property
   - Has `Observe<T>()` method to retrieve queued requests
   - Has specialized subclasses for error simulation
   - **Not publicly available** - users cannot reference it

2. **Public in-memory infrastructure exists** in `Paramore.Brighter`:
   - `InternalBus` - In-memory message bus
   - `SimpleHandlerFactory`, `SimpleHandlerFactorySync`, `SimpleHandlerFactoryAsync`
   - `InMemoryMessageProducer`, `InMemoryMessageConsumer`
   - `InMemoryOutbox`, `InMemoryInbox`
   - **Well documented for internal use, but testing patterns not documented for users**

3. **Documentation gap**:
   - Core guide has testing section (lines 926-1279) but doesn't cover IAmACommandProcessor dependency testing
   - No guidance on which approach to use when

---

## Requirements

### R1: Testing Assembly

Create `Paramore.Brighter.Testing` NuGet package containing:

1. **SpyCommandProcessor** - Public spy implementation
   - Track all method calls (Send, Publish, Post, DepositPost, ClearOutbox, etc.)
   - Provide `Observe<T>()` to retrieve captured requests
   - Provide `GetCalls<T>(methodName)` to get all calls of a type
   - Provide `ContainsCommand(CommandType)` to check if method was called
   - Provide `Reset()` to clear captured state
   - Virtual methods to allow test-specific subclassing

2. **CommandType enum** - Track which methods were called:
   - Send, SendAsync
   - Publish, PublishAsync
   - Post, PostAsync
   - Deposit, DepositAsync
   - Clear, ClearAsync
   - Call
   - Scheduler, SchedulerAsync

3. **RecordedCall record** - Capture call details:
   - Method type (CommandType)
   - Request object
   - Timestamp

### R2: Documentation

Create `Docs/guides/testing-handlers.md` covering:

1. **Introduction**
   - The testing challenge explained
   - Overview of two approaches
   - When to use which approach

2. **Approach 1: SpyCommandProcessor** (from new package)
   - Installation instructions
   - Basic usage with xUnit/NUnit
   - Verifying Send/Publish/Post calls
   - Verifying outbox pattern (DepositPost + ClearOutbox)
   - Asserting on request content

3. **Approach 2: Mocking Frameworks**
   - When mocking is appropriate
   - Example with Moq
   - Example with NSubstitute
   - Example with FakeItEasy

4. **Approach 3: In-Memory Bus Integration**
   - When to use (message serialization verification)
   - Components overview
   - Complete integration test example

5. **Sample Handler Under Test**
   - Handler using outbox pattern
   - Handler publishing events

6. **Best Practices**
   - Choosing between approaches
   - Test isolation
   - Async handler testing

### R3: Core Guide Update

Update `Docs/guides/paramore_brighter_core_guide.md`:
- Add cross-reference to new testing guide in "Testing Strategies" section (line ~953)

---

## Acceptance Criteria

### AC1: Testing Assembly
- [ ] `Paramore.Brighter.Testing` project created in `src/`
- [ ] Project builds successfully
- [ ] Added to `Brighter.slnx` solution file
- [ ] `SpyCommandProcessor` implements full `IAmACommandProcessor` interface
- [ ] Unit tests verify spy functionality

### AC2: Documentation
- [ ] `testing-handlers.md` created with all sections
- [ ] Code examples compile (syntax verified)
- [ ] Examples follow existing documentation style
- [ ] Cross-reference added to core guide

### AC3: Integration
- [ ] New package can be installed via NuGet
- [ ] Documentation discoverable from main guide

---

## Out of Scope

- Changes to `IAmACommandProcessor` interface
- Changes to existing in-memory implementations
- Test framework integrations (xUnit extensions, etc.)
- Performance testing utilities

---

## Technical Notes

### Key Source Files

| File | Purpose |
|------|---------|
| `tests/.../TestDoubles/SpyCommandProcessor.cs` | Reference implementation |
| `src/Paramore.Brighter/IAmACommandProcessor.cs` | Interface to implement |
| `src/Paramore.Brighter/InternalBus.cs` | In-memory bus for integration approach |
| `src/Paramore.Brighter/SimpleHandlerFactory.cs` | Factory for test setup |
| `Docs/guides/paramore_brighter_core_guide.md` | Existing docs to update |

### ADR 0012 Alignment

From ADR 0012 (provide-in-memory-and-simple-implementations):
- In-memory implementations preferred over test doubles for production-like behavior
- Test doubles (spies) still useful for isolated unit testing
- Both approaches have valid use cases

---

## References

- [IAmACommandProcessor interface](../../src/Paramore.Brighter/IAmACommandProcessor.cs)
- [Existing SpyCommandProcessor](../../tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/SpyCommandProcessor.cs)
- [ADR 0012: In-memory implementations](../../Docs/adr/0012-provide-in-memory-and-simple-implementations.md)
- [Core Guide Testing Section](../../Docs/guides/paramore_brighter_core_guide.md#testing-and-observability)
