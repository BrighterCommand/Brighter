---
id: 0068-deterministic-disposal-finalizer-safety-net
title: "Deterministic disposal; the finalizer is a safety net, not the mechanism"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-07-28
summary: "Pipelines and lifetime scopes release their resources on an explicit Dispose/DisposeAsync that drains every tracked object and surfaces failures together as an AggregateException; the finalizer only re-runs the drain best-effort and swallows, since a finalizer must never throw."
tags:
  - "lifetime"
  - "di"
  - "memory"
---

# 68. Deterministic disposal; the finalizer is a safety net, not the mechanism

Date: 2026-07-28

## Status

Accepted

## Context

**Scope**: how the transform pipeline, the transform lifetime scope and the DI lifetime scope release the mapper/transform resolutions they own.

Closing the per-message scope leak (ADR 0067) moved the release of a mapper/transform from something that used to happen incidentally to something a caller must do at a well-defined point. Two forces then apply at once:

- A **finalizer must never let an exception escape** — an exception out of a finalizer terminates the process. Releasing a resolution through the synchronous path can throw: on `netstandard2.0` the container's synchronous scope `Dispose` throws for a service that implements only `IAsyncDisposable`, and a user `Dispose`/`DisposeAsync` may throw.
- An **explicit** dispose, by contrast, must be deterministic: it should release everything it can *now* and report failures, rather than stop at the first fault and leave the rest to a GC-timed finalizer.

## Decision

Disposal is **deterministic on the explicit path and best-effort on the finalizer**, with the finalizer as a safety net only.

### Explicit dispose drains everything and surfaces failures together

`Dispose`/`DisposeAsync` walks the tracked objects and releases each inside a per-item `try/catch`, collecting failures. After the loop it throws a single `AggregateException` carrying every failure. So one throwing release does not abort the drain and defer the remainder to the finalizer — a full call releases everything and the owner sees every failure.

### The finalizer re-runs the drain best-effort and swallows

The finalizer runs the same synchronous drain inside `try { … } catch { }`. Finalization order is non-deterministic, so an orphaned scope can be finalized before its owner disposes it; the finalizer releases what it can and swallows any failure (including the `AggregateException` above), because it must not throw.

### Idempotent, drain-as-you-go

- A release-once guard (`Interlocked.Exchange`) makes an explicit dispose followed by another dispose, or by the finalizer, run the release body exactly once.
- The drain removes each tracked object *before* releasing it, so a finalizer retry re-runs over the shortened list — a throwing release neither leaves the remainder unreleased nor re-releases an already-released object.
- `GC.SuppressFinalize` runs in a `finally` on the explicit path, so a throwing release still de-registers the object from finalization.

This shape is applied uniformly to `TransformLifetimeScope`/`TransformLifetimeScopeAsync`, `TransformPipeline`/`TransformPipelineAsync`, and `ServiceProviderLifetimeScope.Dispose`.

## Consequences

### Positive

- Teardown does not depend on GC timing; a caller that disposes deterministically frees the resolutions' scopes at once and is told about every failure.
- A misbehaving user `Dispose`/`DisposeAsync`, or the `netstandard2.0` sync-dispose-of-async-only throw, cannot crash the process from a finalizer nor silently orphan the other tracked objects.

### Negative

- An explicit dispose can now throw an `AggregateException` where a single throwing release previously surfaced its own exception type; callers already catch `Exception`, which covers it.

## References

- Related: [ADR 0067: Per-resolution DI scope for transient factory instances](0067-per-resolution-di-scope-for-transient-factory-instances.md), [ADR 0069: Ownership and disposal cascade for mapper/transform factories](0069-factory-registry-ownership-and-disposal-cascade.md)
