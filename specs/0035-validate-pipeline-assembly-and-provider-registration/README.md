# Spec 0035 — Validate Pipeline Assembly & Provider Registration

**Issue:** [#4159](https://github.com/BrighterCommand/Brighter/issues/4159) — *ValidatePipelines should check all required assemblies will be scanned*
**Created:** 2026-06-16
**Status:** Requirements (in progress)

## Summary

Extend `ValidatePipelines()` with two related startup checks (both `Warning`,
non-blocking) that surface mis-configuration the current validation cannot catch.

A discovery pass against ADR 0053 + the codebase reshaped the original #4159 ask:
subscription **handler-missing is already detected** (`ConsumerValidationRules.HandlerRegistered`,
Error) and **mapper-missing is undetectable** on the DI path (a default `JsonMessageMapper<>`
always resolves). So the new, reliably-detectable checks are:

- **(A) Missing-transform detection (residual #4159).** Warn when a
  `Publication`'s resolved mapper declares a **wrap** transform — or a
  `Subscription`'s resolved mapper declares an **unwrap** transform — whose
  transformer type (the attribute's `GetHandlerType()` result, e.g.
  `CompressPayloadTransformer`) is **not** resolvable from the transformer
  factory. A reliable, low-false-positive signal the transform's assembly was
  not scanned (the issue's own `JustSayingCompressionTransform` example).

- **(B) Missing validation-provider detection (Ian's add-on).** Warn when
  `[ValidateRequest]` / `[ValidateRequestAsync]` is present on a registered
  handler but **no** validation provider (`UseFluentValidation` /
  `UseDataAnnotations` / `UseSpecification`) is registered, so the abstract
  handler has nothing concrete to map to.

(B) is the deferred "Ian #4" item from the request-validation PR (#4183);
Ian asked for it on #4159 and has no separate issue for it, so it folds in here.
Mapper-missing and handler-missing are out of scope (OOS-7/OOS-8) — see requirements.md.

## Status Checklist

- [ ] Requirements (`/spec:requirements`)
- [ ] Design / ADR (`/spec:design`)
- [ ] Adversarial Review (`/spec:review`)
- [ ] Tasks (`/spec:tasks`)
- [ ] Implementation (TDD via `/spec:implement`)

## Open Design Questions (Ian left these to us)

1. **Severity** — warning (dev-time heads-up; `ValidatePipelines` may be off
   in Prod) vs. hard error gated behind `throwOnError`?
2. **Naming the missing assembly** — heuristic message
   ("no mapper registered for X — is its assembly in `AutoFromAssemblies()`?")
   vs. a precise scanned-assembly-set comparison?

## Key References

- `src/Paramore.Brighter/Validation/PipelineValidator.cs` — existing rule host
- `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs` — `ValidatePipelines()` entry point
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionBrighterBuilder.cs` — `AutoFromAssemblies` scan
- `specs/0023-Pipeline-Validation-At-Startup/` — original ValidatePipelines spec (ADR 0053/0054)
