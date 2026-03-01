# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a developer using Brighter's message pump (Reactor or Proactor), I would like a simple way to ensure that any unhandled exception in my handler pipeline causes the message to be rejected and routed to a Dead Letter Queue (DLQ), so that I can monitor failed messages without implementing custom fallback logic.

Currently, if an exception escapes the handler pipeline:
- The message pump logs the error but acknowledges the message
- The message is effectively lost from a processing perspective
- Developers must implement custom fallback handlers or wrap their entire handler in try/catch to achieve DLQ routing

## Proposed Solution

A declarative middleware attribute that acts as a catch-all exception backstop. When any exception occurs in the handler pipeline, the message is rejected and routed to the DLQ (if configured).

## Requirements

### Functional Requirements

1. Provide a middleware attribute for catching unhandled exceptions in the handler pipeline
2. Support both synchronous and asynchronous handlers
3. When an exception is caught:
   - Log the exception (preserving diagnostic information before it's replaced)
   - Reject the message so it routes to DLQ
   - Preserve the original exception details in the rejection
4. Catch all exception types (no filtering) - this is a "give up" backstop
5. Should be usable as the outermost handler in the pipeline (lowest step number)

### Non-functional Requirements

- Minimal performance overhead in the happy path
- Follow existing middleware patterns in the codebase
- Clear documentation recommending placement as outermost backstop

### Constraints and Assumptions

- Only provides value when running within a message pump (Reactor/Proactor)
- Assumes a DLQ is configured for the subscription; if not, the message is simply rejected/discarded

### Out of Scope

- Exception type filtering
- Configurable rejection reasons
- Automatic DLQ configuration or validation

## Acceptance Criteria

1. Applying the attribute to a handler causes exceptions to result in message rejection
2. The original exception message and exception are preserved in the rejection
3. The exception is logged before being converted to a rejection
4. Works with both sync and async handler patterns
5. When used in a message pump, failed messages are routed to DLQ
