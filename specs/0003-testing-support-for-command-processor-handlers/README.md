# Testing Support for Command Processor Handlers

**Created:** 2026-02-05
**Status:** Design Phase (ADRs in progress)

## Overview

Provide testing infrastructure and documentation to help Brighter users write unit tests for handlers that depend on `IAmACommandProcessor`.

## Problem Statement

Users want to unit test their handlers that take a dependency on `IAmACommandProcessor`. When a handler raises requests via Send, Publish, Post, or the outbox pattern (DepositPost/ClearOutbox), users need ways to verify these interactions in tests.

## Proposed Solution

Two complementary approaches:

1. **Test Double Approach**: Create a new `Paramore.Brighter.Testing` assembly with `SpyCommandProcessor` that captures calls for assertion
2. **In-Memory Bus Approach**: Document how to use existing `InternalBus`, `InMemoryMessageProducer`, and `InMemoryOutbox` to verify actual messages

## Deliverables

- [ ] New `Paramore.Brighter.Testing` NuGet package with `SpyCommandProcessor`
- [ ] Comprehensive testing documentation (`Docs/guides/testing-handlers.md`)
- [ ] Code examples for mocking frameworks (Moq, NSubstitute, FakeItEasy)
- [ ] Code examples for in-memory bus integration testing
- [ ] Update to core guide with cross-references

## Status Checklist

- [x] Requirements approved
- [ ] Design (ADRs) approved (1 of 2 approved)
- [ ] Tasks approved
- [ ] Implementation complete

## Architecture Decision Records

| ADR | Title | Status |
|-----|-------|--------|
| [0049](../../docs/adr/0049-testing-assembly-structure.md) | Testing Assembly Structure | Accepted |
| [0050](../../docs/adr/0050-spy-command-processor-api.md) | Spy Command Processor API | Proposed |

## Next Steps

1. Approve ADR 0050: `/spec:approve design 0050`
2. Create tasks: `/spec:tasks`
3. Begin implementation: `/spec:implement`

## Resume

See [PROMPT.md](PROMPT.md) for full context to resume this work.
