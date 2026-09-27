---
id: 0074-isolate-publish-resilience-context
title: "Exclude caller-owned Polly execution state from publish observers"
status: Proposed
author:
  - "Avtandil Ushikishvili"
created: 2026-09-27
summary: "Publish observers omit caller-owned Polly execution state regardless of observer count, while retaining configured resilience strategies and existing request-context data sharing. Send behavior remains unchanged."
tags:
  - "publish"
  - "resilience"
  - "request-context"
  - "concurrency"
---

# 74. Exclude caller-owned Polly execution state from publish observers

Date: 2026-09-27

## Status

Proposed

## Context

`RequestContext.CreateCopy()` retains the resilience pipeline registry but omits
the Polly `ResilienceContext`. Pipeline construction uses the original request
context for one observer and copies for multiple observers. Consequently,
changing the observer count changes execution metadata and async cancellation
token precedence. This is the inconsistency reported in
[issue #4392](https://github.com/BrighterCommand/Brighter/issues/4392).

Polly execution contexts belong to one execution and must not be shared across
independent concurrent observers. Resilience pipelines, by contrast, are reusable.
Existing single-observer publishing also exposes handler bag writes to the caller;
copying the entire request context would change unrelated behavior.

## Decision

`Publish` and `PublishAsync` omit the caller's Polly execution context for every
observer. Configured typed and untyped resilience pipelines continue to execute.
`PublishAsync` passes its method cancellation token to Polly; the supplied
execution context's token, operation key, and custom properties do not propagate.
The caller retains ownership of its execution context, which is neither cleared
nor returned to the pool by publishing.

Pipeline construction retains its existing one-versus-many data-sharing rules.
Where an observer context contains Polly execution state, an internal
`IRequestContext` view hides that state and delegates the other members. This
preserves single-observer bag updates and routing metadata without mutating the
original context. Subscriber selection still receives the original context.

The existing public pipeline-builder methods and `Send`/`SendAsync` retain their
behavior. Only the command processor's publish paths select exclusion through
internal builder overloads. `CreateCopy` continues to omit execution state,
including for independent outbox confirmation callbacks; no callback logic changes.

## Consequences

Adding an observer no longer changes whether publishing inherits caller-owned
Polly execution state. Retry strategies remain active, and publishing introduces
no execution-context pooling or disposal responsibility.

This changes single-observer publishing for callers supplying a resilience
context. Such handlers receive an `IRequestContext` view, not the original
concrete object; concrete casts and reference-identity assumptions are not
preserved. Shared request data should use the context bag, while cancellation
should use the `PublishAsync` parameter. Bag copying remains shallow and is not
a guarantee that objects stored in it are safe for concurrent access.

## Alternatives Considered

- Share the caller's Polly context: unsafe across independent concurrent executions.
- Clone Polly contexts: requires a separate contract for copying properties, token
  precedence, pool ownership, and callback lifetimes.
- Always copy the request context: breaks caller-visible single-observer bag
  updates and can discard unrelated metadata.
- Temporarily clear and restore the original context: exposes mutation to nested
  or concurrent work and does not preserve caller ownership safely.

## References

- [Expose Request Context](0009-expose-request-context.md): caller-supplied context data.
- [Fix Resilience Pipeline Reuse](0053-fix-resilience-pipeline-reuse.md): reusable typed and untyped strategies.
- [Polly execution-context lifetime](https://www.pollydocs.org/api/Polly.ResilienceContext.html).
