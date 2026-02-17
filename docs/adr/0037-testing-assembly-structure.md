# 37. Testing Assembly Structure

Date: 2026-02-05

## Status

Accepted

## Context

**Parent Requirement**: [specs/0002-testing-support-for-command-processor-handlers/requirements.md](../../specs/0002-testing-support-for-command-processor-handlers/requirements.md)

**Scope**: This ADR focuses specifically on the structure and public API of the new `Paramore.Brighter.Testing` assembly.

### Problem

Users of Brighter write handlers that depend on `IAmACommandProcessor` to raise requests (Send, Publish, Post) and use the outbox pattern (DepositPost, ClearOutbox). When unit testing these handlers, users need to verify these interactions occurred correctly.

Currently:
- An internal `SpyCommandProcessor` exists in our test suite but is not publicly available
- Users must either create their own spy implementations or use mocking frameworks
- No official testing utilities are provided by Brighter

### Forces

1. **Discoverability**: Users expect testing utilities to be easily discoverable
2. **Simplicity**: The API should be simple to use without deep Brighter knowledge
3. **Flexibility**: Users have different testing needs (simple verification vs. detailed inspection)
4. **Maintainability**: The testing assembly must stay synchronized with `IAmACommandProcessor` changes
5. **Alignment with ADR 0012**: Prefer in-memory implementations, but test doubles have valid use cases

### Constraints

- Must implement the full `IAmACommandProcessor` interface
- Should not introduce dependencies beyond `Paramore.Brighter`
- Must support both sync and async testing patterns

## Decision

Create a new `Paramore.Brighter.Testing` NuGet package with a focused public API for testing handlers that depend on `IAmACommandProcessor`.

### Architecture Overview

```
Paramore.Brighter.Testing/
├── SpyCommandProcessor.cs      # Main spy implementation
├── CommandType.cs              # Enum tracking method calls
└── RecordedCall.cs             # Record capturing call details
```

### Key Components

#### 1. CommandType Enum

**Role**: Information Holder - knows which command processor method was invoked

```csharp
/// <summary>
/// Identifies which IAmACommandProcessor method was called
/// </summary>
public enum CommandType
{
    Send,
    SendAsync,
    Publish,
    PublishAsync,
    Post,
    PostAsync,
    Deposit,
    DepositAsync,
    Clear,
    ClearAsync,
    Call,
    Scheduler,
    SchedulerAsync
}
```

#### 2. RecordedCall Record

**Role**: Information Holder - knows the details of a single method invocation

```csharp
/// <summary>
/// Captures details of a single IAmACommandProcessor method call
/// </summary>
public record RecordedCall(
    CommandType Type,
    IRequest Request,
    DateTime Timestamp,
    RequestContext? Context = null
);
```

#### 3. SpyCommandProcessor Class

**Role**: Service Provider (recording) + Interfacer (implements IAmACommandProcessor)

**Responsibilities**:
- **Doing**: Record all method invocations
- **Knowing**: Store recorded calls and captured requests
- **Deciding**: N/A (simple recording, no decisions)

```csharp
/// <summary>
/// A spy implementation of IAmACommandProcessor for testing.
/// Records all method calls for later verification.
/// </summary>
public class SpyCommandProcessor : IAmACommandProcessor
{
    // Recorded state
    private readonly Queue<IRequest> _requests = new();
    private readonly List<RecordedCall> _recordedCalls = new();
    private readonly Dictionary<string, IRequest> _depositedRequests = new();

    // Public inspection API
    public IReadOnlyList<RecordedCall> RecordedCalls => _recordedCalls.AsReadOnly();
    public IReadOnlyList<CommandType> Commands => _recordedCalls.Select(c => c.Type).ToList();

    // Query methods
    public T Observe<T>() where T : class, IRequest;
    public IEnumerable<T> GetRequests<T>() where T : class, IRequest;
    public IEnumerable<RecordedCall> GetCalls(CommandType type);
    public bool WasCalled(CommandType type);
    public int CallCount(CommandType type);

    // Reset state
    public void Reset();

    // IAmACommandProcessor implementation (all virtual for subclassing)
    public virtual void Send<T>(T command, RequestContext? context = null);
    public virtual Task SendAsync<T>(T command, ...);
    public virtual void Publish<T>(T @event, RequestContext? context = null);
    public virtual Task PublishAsync<T>(T @event, ...);
    public virtual void Post<T>(T request, ...);
    public virtual Task PostAsync<T>(T request, ...);
    public virtual Id DepositPost<T>(T request, ...);
    public virtual Task<Id> DepositPostAsync<T>(T request, ...);
    public virtual void ClearOutbox(Id[] posts, ...);
    public virtual Task ClearOutboxAsync(IEnumerable<Id> posts, ...);
    // ... remaining interface methods
}
```

### Design Decisions

1. **Virtual Methods**: All interface implementations are `virtual` to allow users to create specialized spies (e.g., one that throws exceptions)

2. **Dual Access Patterns**:
   - `RecordedCalls` - Full detail access for complex assertions
   - `Commands` - Simple list for quick "was this called?" checks
   - `Observe<T>()` - Dequeue pattern for sequential verification

3. **Reset Capability**: `Reset()` method allows reusing spy across multiple test cases

4. **No Dependencies**: Only depends on `Paramore.Brighter` core assembly

### Project Structure

```xml
<!-- Paramore.Brighter.Testing.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$(BrighterTargetFrameworks)</TargetFrameworks>
    <Description>Testing utilities for Paramore.Brighter</Description>
    <PackageTags>Brighter;Testing;Spy;Mock;Unit Test</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Paramore.Brighter\Paramore.Brighter.csproj" />
  </ItemGroup>
</Project>
```

## Consequences

### Positive

- **Simple API**: Users can quickly verify handler interactions with minimal setup
- **Discoverable**: Standard NuGet package naming convention (`*.Testing`)
- **Flexible**: Virtual methods allow customization for edge cases
- **Maintained**: Part of the official Brighter solution, stays in sync with interface changes
- **No external dependencies**: Lightweight addition to test projects

### Negative

- **Maintenance burden**: New assembly requires versioning and release management
- **Interface coupling**: Must be updated when `IAmACommandProcessor` changes
- **Limited scope**: Only covers command processor testing, not other Brighter components

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Interface changes break spy | Include spy in CI build; changes to interface immediately surface |
| Users expect more utilities | Clear documentation on scope; can expand later based on feedback |
| Version drift | Release testing package alongside core package |

## Alternatives Considered

### 1. Document Pattern Only (No Package)

Provide documentation showing users how to write their own spy or use mocking frameworks.

**Rejected because**:
- More work for every user
- Easy to get wrong
- No official maintained implementation

### 2. Add Spy to Core Package

Include `SpyCommandProcessor` in `Paramore.Brighter` main package.

**Rejected because**:
- Pollutes production assembly with test utilities
- Violates separation of concerns
- Users who don't need it still get it

### 3. Comprehensive Testing Framework

Create a full testing framework with assertion extensions, builders, etc.

**Rejected because**:
- Over-engineering for current need
- Competes with existing test frameworks (xUnit, NUnit)
- Can be added later if demand exists

## References

- Requirements: [specs/0002-testing-support-for-command-processor-handlers/requirements.md](../../specs/0002-testing-support-for-command-processor-handlers/requirements.md)
- Related ADRs: [ADR 0012: In-memory implementations](0012-provide-in-memory-and-simple-implementations.md)
- Existing internal spy: [tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/SpyCommandProcessor.cs](../../tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/SpyCommandProcessor.cs)
- IAmACommandProcessor interface: [src/Paramore.Brighter/IAmACommandProcessor.cs](../../src/Paramore.Brighter/IAmACommandProcessor.cs)
