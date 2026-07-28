---
id: 0069-factory-registry-ownership-and-disposal-cascade
title: "Ownership and disposal cascade for mapper/transform factories"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-28
summary: "The MessageMapperRegistry owns and disposes the factories it was built with; the OutboxProducerMediator and the Dispatcher are IDisposable and cascade disposal into the registry and transform factories they own; a component that did not create a disposable does not dispose it."
tags:
  - "lifetime"
  - "di"
  - "mediator"
  - "dispatcher"
---

# 69. Ownership and disposal cascade for mapper/transform factories

Date: 2026-07-28

## Status

Proposed

## Context

**Scope**: which component disposes the mapper registry and the mapper/transform factories, so the per-resolution scopes of ADR 0067 are actually reclaimed.

A per-resolution scope that a caller fails to release is drained only when its factory is disposed (ADR 0067). For an IoC-backed factory nothing else can reach those factories to dispose them, so an owner must — and must do so exactly once, without disposing a factory or registry that another owner is still using.

## Decision

Disposal follows ownership: **the component that created a disposable disposes it, and disposal cascades from the owner down.**

### The registry owns its factories

`MessageMapperRegistry` is `IDisposable` and disposes the mapper factories it was constructed with — both the synchronous and asynchronous factory. It disposes both even when the first `Dispose` throws (`try/finally`), so a fault draining one factory cannot orphan the scopes retained by the other, and the disposal is idempotent (the disposed flag is claimed with a single atomic exchange) so an owner and the container can both dispose it exactly once.

### Producer and consumer roots cascade

- The `OutboxProducerMediator` (producer side) is `IDisposable` and, on disposal, cascades into the `IAmAMessageMapperRegistry` and both transform factories it was given, disposing each quietly (a failure to dispose one is logged, not allowed to skip the rest). A `ReferenceEquals` guard avoids disposing the same object twice when the sync and async registry are one instance (the DI path).
- The `Dispatcher` (consumer side) is likewise `IDisposable` and disposes the registry and transform factories it constructed and no one else owns.

### A non-creator does not dispose

Components that receive a registry they did not create do not dispose it. The pipeline validation and diagnostics components take a `Func<MessageMapperRegistry>` rather than a registry instance: each invokes the factory once and owns and disposes only the registry it thereby created, so it can never dispose a caller's shared registry.

## Consequences

### Positive

- Every per-resolution scope is reclaimed at the latest when its owning root is disposed, bounding retention to the host lifetime; in the DI path each root gets its own registry and factories, so disposal is airtight.

### Negative

- If a `MessageMapperRegistry` or a transformer factory is **manually** shared between independently-disposed owners (e.g. a `CommandProcessor` external bus and a `Dispatcher`), disposing one owner disposes objects the other still uses, and subsequent resolutions throw `ObjectDisposedException`. This is a runtime break with no compile-time signal; the mitigation is to give each owner its own registry/factories or to defer disposal until all owners are done.

## References

- Related: [ADR 0067: Per-resolution DI scope for transient factory instances](0067-per-resolution-di-scope-for-transient-factory-instances.md), [ADR 0033: Lifetime of Command Processor and Mediator](0033-lifetime-of-command-processor-and-mediator.md)
