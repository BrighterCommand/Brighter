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

We add validation as an **optional, additive concern**. The provider-agnostic
abstractions live in the **core** `Paramore.Brighter` assembly (they depend only
on the core and carry no third-party reference, exactly like the built-in
`RequestLogging`/`UseInbox` attribute+handler pairs), and each validation
framework ships as **its own provider package**, so users only take the dependency
they need.

**Core — `Paramore.Brighter`, under a `RequestValidation/` sub-folder**:

- `ValidateRequestAttribute` / `ValidateRequestAsyncAttribute`
  (namespace `Paramore.Brighter.RequestValidation.Attributes`) — extend
  `RequestHandlerAttribute`; `GetHandlerType()` points at the **abstract** base
  handlers below, mirroring `RequestLoggingAttribute`.
- `ValidateRequestHandler<TRequest>` / `ValidateRequestHandlerAsync<TRequest>`
  (namespace `Paramore.Brighter.RequestValidation.Handlers`) — **abstract** base
  handlers extending `RequestHandler<TRequest>` / `RequestHandlerAsync<TRequest>`.
  They own the shared pipeline behaviour (null-guard the request, ask the provider
  for failures, throw on any failure, otherwise call `base.Handle`/`base.HandleAsync`)
  and expose one abstract method, `Validate` / `ValidateAsync`, that returns the
  failures.
- `RequestValidationException` and `RequestValidationError`
  (namespace `Paramore.Brighter.RequestValidation`) — the exception carries a
  framework-agnostic `IReadOnlyCollection<RequestValidationError>` (property,
  message, attempted value, error code), so a caller catches **one** exception type
  regardless of provider. `RequestValidationError` is named to avoid a clash with
  the pre-existing `Paramore.Brighter.ValidationError`, which reports findings from
  Brighter's *startup pipeline validation* (ADR 0053) — a different concern.

**Provider packages** (separate assemblies, this PR):

- `Paramore.Brighter.Validation.FluentValidation` — derives from the abstract base
  handlers and implements `Validate`/`ValidateAsync` by resolving a FluentValidation
  `IValidator<TRequest>` from the container and mapping its failures onto
  `RequestValidationError`. `UseFluentValidation()` maps the abstract handler types
  to the FluentValidation implementations on the container.
- `Paramore.Brighter.Validation.DataAnnotations` — validates with
  `System.ComponentModel.DataAnnotations`; `UseDataAnnotations()`.
- `Paramore.Brighter.Validation.Specification` — validates with Brighter's own
  Specification pattern (ADR 0040); `UseSpecification()`.

Because the attribute targets the abstract handler and each provider package maps
it to a concrete implementation, **one `[ValidateRequest]` attribute works for
every framework**: adding a further provider is purely additive — a new concrete
handler plus a `UseX()` registration — with no change to the abstractions or to
the existing provider packages.

**Missing validator** is treated as a configuration error: if a request is marked
with `[ValidateRequest]` but no validator is registered for it, the provider handler
throws `ConfigurationException` (Brighter's existing misconfiguration type),
fail-fast, rather than silently skipping validation.

## Consequences

- Validation is opt-in per request via one attribute; the framework is chosen by
  which provider package is registered. The core gains no third-party dependency —
  only a small, dependency-free abstraction, consistent with the existing built-in
  middleware.
- A single `RequestValidationException` / `RequestValidationError` model is shared
  by all current and future providers, so edge code (e.g. an API mapping to HTTP
  422) catches one type.
- Adding further frameworks is additive and leaves the shipped code untouched.

### Resolution of the open questions (maintainer feedback on PR #4183)

- **Naming.** Renamed `[ValidateQuery]` → `[ValidateRequest]` (and the async
  variant). "Query" was a holdover from the Darker write-up of the issue; Brighter
  has requests, not queries. No alias is kept.
- **Placement.** The abstractions are folded into the core `Paramore.Brighter`
  assembly (under `RequestValidation/`) rather than a separate
  `Paramore.Brighter.Validation` package, following the same pattern as the other
  built-in attribute+handler pairs. Providers remain separate packages.
- **Issue tracking.** This PR delivers FluentValidation, DataAnnotations and
  Specification providers; it "Contributes to" #4175.
- **Darker.** The same requirement exists for Darker V5; it carries a lot of
  structural change for V5 and is intentionally out of scope here, to follow as
  separate work.
