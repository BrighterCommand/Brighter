# 63. Request validation handler

Date: 2026-06-15

## Status

Proposed

## Context

Commands and queries typically need their input validated before the business
handler runs. Bad input can cause exceptions deep in the pipeline, SQL injection
risks, or unnecessary database load. MediatR has a well-established pattern for
this: a pipeline behavior that resolves an `IValidator<TRequest>` and validates
the request before calling `next`.

Brighter already expresses cross-cutting concerns as attribute + handler pairs in
the request pipeline (for example `RequestLoggingAttribute` +
`RequestLoggingHandler<TRequest>`, and `UseInboxAttribute` +
`UseInboxHandler<TRequest>`). A validation concern fits the same shape: an
attribute marks the target `Handle`/`HandleAsync` method, and a decorator handler
runs before the business handler.

Issue [#4175](https://github.com/BrighterCommand/Brighter/issues/4175) proposes:

- a `[ValidateQuery]` attribute and a corresponding `ValidateRequestHandler`;
- the decorator resolves `IValidator<TRequest>` from the DI container and
  validates before calling `next`;
- on failure, throw a `RequestValidationException` with structured error details;
- support for three frameworks over time — Brighter's Specification pattern,
  FluentValidation, and `System.ComponentModel.DataAnnotations`.

The maintainer noted that "FluentValidation might be a good starting PR".

## Decision

We add validation as an **optional, additive concern split across two assemblies**,
so a single attribute serves every framework while users only take the dependency
they need (consistent with Brighter's rule that optional behaviours live in their
own NuGet package).

**`Paramore.Brighter.Validation`** (provider-agnostic abstractions, depends only on
the core):

- `ValidateQueryAttribute` / `ValidateQueryAsyncAttribute` — extend
  `RequestHandlerAttribute`; `GetHandlerType()` points at the **abstract** base
  handlers below, mirroring `RequestLoggingAttribute`.
- `ValidateRequestHandler<TRequest>` / `ValidateRequestHandlerAsync<TRequest>` —
  **abstract** base handlers extending `RequestHandler<TRequest>` /
  `RequestHandlerAsync<TRequest>`. They own the shared pipeline behaviour
  (null-guard the request, ask the provider for failures, throw on any failure,
  otherwise call `base.Handle`/`base.HandleAsync`) and expose one abstract method,
  `Validate` / `ValidateAsync`, that returns the failures.
- `RequestValidationException` — carries a framework-agnostic
  `IReadOnlyCollection<ValidationError>` (property, message, attempted value,
  error code), so a caller catches **one** exception type regardless of provider.

**`Paramore.Brighter.Validation.FluentValidation`** (the first provider, this PR):

- `FluentValidationRequestHandler<TRequest>` /
  `FluentValidationRequestHandlerAsync<TRequest>` — derive from the abstract base
  handlers and implement `Validate`/`ValidateAsync` by resolving a FluentValidation
  `IValidator<TRequest>` from the container and mapping its failures onto
  `ValidationError`.
- `UseFluentValidation()` — registers the abstract handler types against the
  FluentValidation implementations on the container, so Brighter's handler factory
  resolves the concrete handler for `[ValidateQuery]`. The core is not modified.

Because the attribute targets the abstract handler and the provider package maps it
to a concrete implementation, **one `[ValidateQuery]` attribute works for every
framework**: a future `Paramore.Brighter.Validation.DataAnnotations` or
`Paramore.Brighter.Validation.Specification` package is purely additive — a new
concrete handler plus a `UseX()` registration — with no change to the abstractions
or to this FluentValidation package.

**Missing validator** is treated as a configuration error: if a request is marked
with `[ValidateQuery]` but no validator is registered for it, the provider handler
throws `ConfigurationException` (Brighter's existing misconfiguration type),
fail-fast, rather than silently skipping validation.

## Consequences

- Validation is opt-in per request via one attribute; the framework is chosen by
  which provider package is registered. The core gains no dependency.
- A single `RequestValidationException` / `ValidationError` model is shared by all
  current and future providers, so edge code (e.g. an API mapping to HTTP 422)
  catches one type.
- Adding the remaining frameworks from the issue is additive and leaves the shipped
  code untouched.
- Open questions deferred to the PR discussion:
  - **Naming.** The attribute is named `[ValidateQuery]` per the issue, but the
    concern validates commands as well as queries. Should it be `[ValidateRequest]`
    (with `[ValidateQuery]` kept as an alias)?
  - **Placement.** The abstractions live in a new `Paramore.Brighter.Validation`
    package. Would you prefer them in the core assembly instead? They depend only on
    the core, so either home works.
  - **Darker.** The same requirement exists for Darker V5 and is intentionally out
    of scope for this PR; it can follow as separate work.
