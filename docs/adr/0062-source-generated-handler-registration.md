# 62. Source-Generated Handler, Mapper and Transform Registration

Date: 2026-06-04

## Status

Proposed

## Context

**Scope**: This ADR covers an opt-in, compile-time alternative to runtime assembly scanning for
registering Brighter handlers, message mappers and message transforms. It does **not** remove or
change assembly scanning; the two mechanisms are additive.

### The Problem

Brighter discovers handlers, mappers and transforms at startup by scanning assemblies — either the
assemblies the host happens to have loaded, or the explicit list passed to
`AutoFromAssemblies([...])`. This has well-known failure modes:

1. **Unloaded assemblies are invisible.** Scanning only sees *loaded* assemblies. An assembly that
   has not yet been loaded when scanning runs contributes no registrations, so the handlers,
   mappers or transforms it contains are silently absent. This is the subject of issue
   [#4159](https://github.com/BrighterCommand/Brighter/issues/4159): for example, using a
   `JustSayingMessageMapper` without also ensuring its assembly (and its dependency
   `JustSayingCompressionTransform`) is scanned leaves the pipeline incompletely registered.

2. **The failure is silent and only surfaces at runtime — destructively.** A missing mapper or
   transform throws `MessageMappingException` while unwrapping an incoming message. Until issue
   [#4160](https://github.com/BrighterCommand/Brighter/issues/4160) (fixed in
   [ADR 0061](0061-reject_mapping_errors.md)), the message pump silently acknowledged — i.e.
   deleted — such messages. Even with the reject-path fix, the operator still gets a message routed
   to the Invalid Message Queue / DLQ rather than a correctly processed one. The registration was
   never wrong in the code; it was lost at scan time.

3. **No compile-time feedback.** Because discovery is reflective and deferred to startup, the
   compiler cannot tell the developer that a handler will not be registered. Mistakes are found by
   running the application, not by building it.

4. **Reflection has a cost.** Assembly scanning at startup is incompatible with trimming / Native
   AOT and adds startup latency proportional to the assembly surface scanned.

### Requirements Context

This is not derived from a `specs/` requirement; it is a standalone architectural decision recorded
retroactively to capture work already implemented on the
`slang25/source-gen-auto-assemblies` branch. Brighter already treats source generation as an
accepted technique for cross-cutting concerns (see [ADR 0026](0026-use-source-generated-logging.md),
source-generated logging) and ships Roslyn analyzers for pipeline validation
([ADR 0037](0037-provide-roslyn-analyzers-for-brighter.md),
[ADR 0054](0054-roslyn-analyzer-extensions-for-pipeline-validation.md)), so a generator that emits
registration code is consistent with the existing direction.

### Constraints

- **Must not break existing users.** Assembly scanning and `AutoFromAssemblies` must keep working
  unchanged; source generation is opt-in and additive.
- **Must register through the existing public surface.** Generated code must go through the same
  registries the framework already uses (`IBrighterBuilder.Handlers`,
  `IBrighterBuilder.MapperRegistry`, and transform registration) so that downstream behaviour —
  including `ValidatePipelines` / `DescribePipelines` — is identical regardless of how a type was
  registered.
- **Must not introduce a binary-breaking change** to `IBrighterBuilder` for downstream
  implementers.
- **Must be incremental.** The generator must participate correctly in Roslyn's incremental
  pipeline (no semantic-model objects escaping transforms; value-equatable models only) so it does
  not regress IDE/build performance.

## Decision

We add a new analyzer-only package, `Paramore.Brighter.SourceGenerators`, containing an
`IIncrementalGenerator` (`BrighterRegistrationsGenerator`) that discovers Brighter types **in the
current compilation** and emits explicit registration code. Registration becomes a compile-time
artefact of the project that owns the types, not a runtime reflection pass over loaded assemblies.

### Roles and responsibilities

The implementation is split so each type has a single, cohesive responsibility, with no Roslyn
semantic-model object crossing a pipeline boundary:

| Role (stereotype) | Type | Responsibility |
|---|---|---|
| Interfacer / information holder | `MarkerSymbols` | *Knowing* the Brighter framework interface symbols (`IHandleRequests<>`, `IHandleRequestsAsync<>`, `IAmAMessageMapper<>`, `IAmAMessageMapperAsync<>`, `IAmAMessageTransform`, `IAmAMessageTransformAsync`) in a given compilation, and whether Brighter is referenced at all. |
| Service provider | `SemanticModelReader` | *Deciding* how a method or class is classified — projecting Roslyn symbols into Roslyn-free, value-equatable records (`MethodCandidate`, `DiscoveredEntry`, `DiagnosticInfo`). |
| Information holder | `Model/*` (`RegistrationModel`, `DiscoveredEntry`, `EquatableArray<T>`, …) | *Knowing* the discovered registrations as pure, equatable data the incremental cache can compare by value. |
| Service provider | `RegistrationWriter` | *Doing* the text generation — turning a `RegistrationModel` into C# source. Holds no Roslyn references, so it is unit-testable without a `Compilation`. |
| Coordinator | `BrighterRegistrationsGenerator` | *Coordinating* the incremental pipeline: wiring the syntax providers, combining streams, and handing models to the writer. |

### What is generated

The developer marks a `static partial` method that returns `IBrighterBuilder` and takes a single
`IBrighterBuilder`, with `[BrighterRegistrations]`. The generator implements that partial method,
emitting calls that register every discovered type:

- **Handlers** via `builder.Handlers(...)` / `builder.AsyncHandlers(...)`. Closed generics use the
  strongly-typed `Register<TRequest, TImpl>()`; open-generic handlers use
  `EnsureHandlerIsRegistered(typeof(...))`.
- **Mappers** via `builder.MapperRegistry(...)` (`Add` / `AddAsync`).
- **Transforms** via a new off-interface `BrighterBuilderExtensions.Transforms(...)` extension
  (added so transform registration is symmetric with handlers/mappers **without** a binary-breaking
  addition to `IBrighterBuilder`).

`[ExcludeFromBrighterRegistration]` opts a single type out. Generic mappers/transforms cannot be
registered as-is and produce diagnostic `BRGEN005` (warning) rather than being silently dropped.
Malformed registration methods produce `BRGEN001`–`BRGEN004` (errors), giving the compile-time
feedback that scanning never could.

### Per-package auto-registration (the referenced-assembly story)

A source generator only sees the syntax of the **current** compilation; types compiled into a
*referenced* package have no syntax tree in the consumer and are therefore invisible to the
consumer's generator. To let a library contribute its own registrations without the consumer
re-scanning it, the package ships a `build/` props file that sets the
`BrighterAutoRegistration` MSBuild property. When true, the generator additionally synthesises an
`internal static BrighterAssemblyRegistrations.AddFromThisAssembly(this IBrighterBuilder)`
extension covering *that compilation's* types. Because the prop is in `build/` (not
`buildTransitive/`), it only applies to a **direct** `PackageReference`, so a library generates its
own registrations at its own compile time and the consumer calls `AddFromThisAssembly()` — rather
than the consumer trying to load and scan the library at runtime.

## Consequences

### Positive

- **Eliminates the #4159 root cause for first-party types.** Registration is derived from the
  compilation, so the "assembly not loaded at scan time" failure mode cannot occur for types in the
  project (or in a package that adopts this generator). Handlers, mappers **and** transforms are all
  covered — including the transform case (`JustSayingCompressionTransform`) that #4159 calls out.
- **Failures move from runtime to build time.** Misconfigured registration methods fail compilation
  (`BRGEN001`–`BRGEN004`); unsupported generic mappers/transforms warn (`BRGEN005`).
- **Reduces the destructive variant of `MessageMappingException`.** Reliable compile-time
  registration means fewer messages reach the [ADR 0061](0061-reject_mapping_errors.md) reject path
  because a mapper/transform was missing.
- **Trimming / AOT friendly and lower startup cost** for projects that rely on generation instead
  of scanning.
- **Behaviourally identical downstream.** Generated registrations flow through the same registries,
  so `ValidatePipelines` / `DescribePipelines` behave the same.

### Negative

- **Referenced-package types are only covered if the package adopts the generator.** A third-party
  package that merely ships compiled mappers/transforms (and does not use this generator + props) is
  still invisible to the consumer's generator. Those users must register such types explicitly or
  continue to use `AutoFromAssemblies`. Source generation does not make scanning obsolete.
- **Two registration paths to understand.** Scanning and source generation now coexist; teams must
  know which they are using (and that they are additive).
- **Generic mappers/transforms are unsupported** (by design) and require a closed type, a
  non-generic wrapper, or an explicit exclude.

### Risks and Mitigations

**Risk**: Users assume source generation replaces scanning and stop adding required assemblies,
silently losing third-party package registrations.
- **Mitigation**: Document the referenced-package boundary explicitly; keep `AutoFromAssemblies`
  fully supported as an additive fallback; consider a future analyzer warning when a referenced
  Brighter-using assembly is neither generated nor scanned.

**Risk**: `ValidatePipelines` evolves (per #4159) to warn that required assemblies are missing from
`AutoFromAssemblies`, producing false positives for apps that register via source generation and
have no `AutoFromAssemblies` list.
- **Mitigation**: Any such validation must treat source-generated registrations as satisfying the
  requirement. Because generated code registers through the same registries, validation that checks
  *what is registered* (rather than *which assemblies were listed*) remains correct.

**Risk**: A semantic-model object escaping a transform would break incremental caching and regress
IDE performance.
- **Mitigation**: Every value crossing the pipeline is a value-equatable record; this is covered by
  the incremental-caching tests.

## Alternatives Considered

### Alternative 1: Keep runtime assembly scanning only

Continue to rely on reflection over loaded / listed assemblies.

**Rejected because**:
- Does not address #4159 (unloaded assemblies) or give compile-time feedback.
- Incompatible with trimming / Native AOT.

### Alternative 2: Generator scans referenced assembly *metadata* (symbols), not just syntax

Have the generator enumerate `IAssemblySymbol` references and classify their types, so package types
are discovered without the package adopting the generator.

**Rejected because**:
- Walking referenced metadata on every keystroke is the kind of work incremental generators are
  designed to avoid; it would hurt IDE performance and caching.
- It re-creates reflective ambiguity (which referenced assemblies are "ours"?) at build time.
- The per-package `AddFromThisAssembly` approach gives each library ownership of its own
  registrations at its own compile time, which is cleaner and composes.

### Alternative 3: Make registration a runtime source-generated reflection cache

Generate a static map but still resolve it reflectively at startup.

**Rejected because**:
- Retains a startup reflection step and trimming/AOT hazards for no real gain over emitting direct
  registration calls.

### Alternative 4: Add `Transforms(...)` to `IBrighterBuilder`

Put transform registration directly on the interface for symmetry with `Handlers` / `MapperRegistry`.

**Rejected because**:
- It is a binary-breaking change for downstream implementers of `IBrighterBuilder`. An off-interface
  extension method (`BrighterBuilderExtensions.Transforms`) achieves the same ergonomics without
  breaking the contract.

## References

- Related issues:
  - [#4159: ValidatePipeline should check all required assemblies will be scanned](https://github.com/BrighterCommand/Brighter/issues/4159) — the root cause this generator structurally addresses for first-party types.
  - [#4160: Message pump silently Acks on MessageMappingException](https://github.com/BrighterCommand/Brighter/issues/4160) — the downstream symptom of a missing registration.
- Related ADRs:
  - [ADR 0061: Reject Mapping Errors](0061-reject_mapping_errors.md) — routes failed mappings to IMQ/DLQ; complementary runtime safety net that source generation reduces reliance on.
  - [ADR 0026: Use Source-Generated Logging](0026-use-source-generated-logging.md) — precedent for source generation in the codebase.
  - [ADR 0037: Provide Roslyn Analyzers for Brighter](0037-provide-roslyn-analyzers-for-brighter.md) and [ADR 0054: Roslyn Analyzer Extensions for Pipeline Validation](0054-roslyn-analyzer-extensions-for-pipeline-validation.md) — existing compile-time tooling direction.
  - [ADR 0053: Pipeline Validation at Startup](0053-pipeline-validation-at-startup.md) — the `ValidatePipelines` mechanism that must remain correct under source-generated registration.
- External references:
  - [Incremental Generators design](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
