# 62. Source-Generated Handler, Mapper and Transform Registration

Date: 2026-06-04 (amended 2026-07-12: registration artefact reshaped from generated imperative
method bodies to a `RegistrationCatalog` data type — see Alternative 5 for the original shape and
why it was superseded)

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

5. **Handlers rarely live in the host project.** In any layered solution the handlers, mappers and
   transforms live in domain/application assemblies, while dependency-injection composition happens
   in the host. A registration mechanism that only works inside the composition project — or that
   forces domain assemblies to reference the DI stack (`Paramore.Brighter.Extensions.DependencyInjection`
   brings `Microsoft.Extensions.DependencyInjection` **and Polly** with it) — does not fit how
   Brighter applications are actually structured. Domain assemblies already reference core
   `Paramore.Brighter` (that is where `IHandleRequests<>` lives); they should not need more than
   that to *describe* their registrations.

### Requirements Context

This is not derived from a `specs/` requirement; it is a standalone architectural decision recorded
for work on the `slang25/source-gen-auto-assemblies` branch. Brighter already treats source
generation as an accepted technique for cross-cutting concerns (see
[ADR 0026](0026-use-source-generated-logging.md), source-generated logging) and ships Roslyn
analyzers for pipeline validation ([ADR 0037](0037-provide-roslyn-analyzers-for-brighter.md),
[ADR 0054](0054-roslyn-analyzer-extensions-for-pipeline-validation.md)), so a generator that emits
registration code is consistent with the existing direction.

### Constraints

- **Must not break existing users.** Assembly scanning and `AutoFromAssemblies` must keep working
  unchanged; source generation is opt-in and additive.
- **Must register through the existing public surface.** Generated registrations must flow through
  the same registries the framework already uses (`IBrighterBuilder.Handlers`,
  `IBrighterBuilder.MapperRegistry`, and transform registration) so that downstream behaviour —
  including `ValidatePipelines` / `DescribePipelines` — is identical regardless of how a type was
  registered.
- **Must not introduce a binary-breaking change** to `IBrighterBuilder` for downstream
  implementers.
- **Must be incremental.** The generator must participate correctly in Roslyn's incremental
  pipeline (no semantic-model objects escaping transforms; value-equatable models only) so it does
  not regress IDE/build performance.
- **Must compose across assemblies.** A solution with several handler-owning projects must be able
  to combine their registrations in the host with no name collisions and no per-project DI
  dependency.

## Decision

We introduce a three-layer design: an inert **registration catalog** data type in core
`Paramore.Brighter`, an **applier** extension in the DI package, and an analyzer-only package
`Paramore.Brighter.SourceGenerators` whose `IIncrementalGenerator`
(`BrighterRegistrationsGenerator`) discovers Brighter types **in the current compilation** and
emits catalog-construction code. Registration becomes a compile-time artefact of the project that
owns the types, not a runtime reflection pass over loaded assemblies.

The guiding principle: **the generator emits inert data; all application logic lives in the
library.** Generated code is compiled into consumer assemblies at *their* build time and cannot be
patched by a Brighter release; the less intelligence it contains, the smaller the version-skew
surface between the generator and the runtime libraries.

### Layer 0 — `RegistrationCatalog` (core `Paramore.Brighter`)

A pure data type describing an assembly's contribution: handler, mapper and transform
registrations as `(Type, Type)` pairs plus flags.

```csharp
public sealed class RegistrationCatalog
{
    public IReadOnlyList<HandlerRegistration> Handlers { get; }
    public IReadOnlyList<MapperRegistration> Mappers { get; }
    public IReadOnlyList<Type> Transforms { get; }

    public void AddHandler(Type handlerType, Type? requestType, bool isAsync); // requestType null ⇒ open generic
    public void AddMapper(Type mapperType, Type requestType, bool isAsync);
    public void AddTransform(Type transformType);
}
```

- Lives in core because domain assemblies already reference core; describing registrations must
  not require the DI package (and its `Microsoft.Extensions.DependencyInjection` / Polly graph).
- Construction is via **additive `Add*` methods, never a growing constructor**: generated code
  compiled into shipped consumer packages binds to this surface, so it must only ever evolve by
  adding new methods (a new registration kind ⇒ a new `Add*` method), keeping old generated code
  binary-compatible.
- `Add*` methods validate their arguments (e.g. `handlerType` implements
  `IHandleRequests<requestType>`), so a defective catalog fails at startup — still far earlier
  than the message-receipt-time failure that scanning produces.
- Being data, a catalog is inspectable: a team can unit-test "our catalog contains a mapper for
  `OrderPlaced`" without building a container.

### Layer 1 — `AddRegistrations` (DI package)

One new extension in `BrighterBuilderExtensions` (off-interface, per the existing pattern, so it
is not a binary-breaking change for `IBrighterBuilder` implementers):

```csharp
public static IBrighterBuilder AddRegistrations(this IBrighterBuilder builder, params RegistrationCatalog[] catalogs)
```

It applies catalogs through the existing public surface:

- **Handlers** via `builder.Handlers(...)` / `builder.AsyncHandlers(...)` using the existing
  non-generic `Add(Type, Type)` on the registries — no `MakeGenericMethod`, so the trimming/AOT
  benefit is preserved. Open-generic handlers use `EnsureHandlerIsRegistered` on
  `ServiceCollectionSubscriberRegistry` — a coupling that now lives **inside the library**, where
  it can evolve with the registry, instead of being frozen into every consumer's generated code.
- **Mappers** via `builder.MapperRegistry(...)` (`Add` / `AddAsync`). `AddRegistrations` always
  makes this call, even for a mapper-less catalog, preserving the default-message-mapper guarantee
  (`EnsureDefaultMessageMapperIsRegistered`) that `AutoFromAssemblies` provides.
- **Transforms** via the off-interface `BrighterBuilderExtensions.Transforms(...)` extension
  (added in this change so transform registration is symmetric with handlers/mappers **without**
  a binary-breaking addition to `IBrighterBuilder`).

### Layer 2 — declaration forms (the generator)

The generator emits exactly one interesting thing — a catalog-construction expression for the
current compilation — wrapped in whichever declaration the consumer chose:

**Declared catalog holder (the primary, multi-assembly form).** The developer declares an empty
`static partial class` and marks it:

```csharp
// Orders.Domain — references core Paramore.Brighter + the generator (PrivateAssets="all") only
[BrighterRegistrations]
public static partial class OrdersRegistrations;
```

The generator adds `public static RegistrationCatalog Catalog { get; }` covering that
compilation's types. The developer controls the name, namespace and visibility, so multiple
assemblies compose without collision. The host composes:

```csharp
services.AddBrighter(...)
    .AddRegistrations(OrdersRegistrations.Catalog, BillingRegistrations.Catalog);
```

A plain `partial class` declaration compiles at C# 2-level `partial` support, so this form works
in netstandard2.0 / net48 libraries at their default LangVersion (7.3) — unlike the superseded
extended-partial-method form, which required C# 9.

**Auto registration (the zero-config, single-project form).** The package ships a `build/` props
file setting the `BrighterAutoRegistration` MSBuild property. When true — and only for a
**direct** `PackageReference`, because the props file is in `build/` not `buildTransitive/` — the
generator synthesises an `internal static class BrighterAssemblyRegistrations` exposing the same
generated `Catalog` plus an `AddFromThisAssembly(this IBrighterBuilder)` sugar whose body is one
`AddRegistrations(Catalog)` call. This form is emitted only when the DI package is referenced
(the sugar's parameter type must resolve) and is suppressed — with info diagnostic `BRGEN007` —
when the compilation also declares a `[BrighterRegistrations]` holder, so the two forms cannot
double-register the same types. Override per-project with
`<BrighterAutoRegistration>false</BrighterAutoRegistration>`.

`[ExcludeFromBrighterRegistration]` opts a single type out of discovery under either form.

### Roles and responsibilities

The implementation is split so each type has a single, cohesive responsibility, with no Roslyn
semantic-model object crossing a pipeline boundary:

| Role (stereotype) | Type (package) | Responsibility |
|---|---|---|
| Information holder | `RegistrationCatalog` (`Paramore.Brighter`) | *Knowing* an assembly's handler/mapper/transform registrations as inert, inspectable data. |
| Service provider | `BrighterBuilderExtensions.AddRegistrations` (`…Extensions.DependencyInjection`) | *Doing* the application of catalogs to the builder: non-generic registry adds, the open-generic path, the default-mapper guarantee. |
| Interfacer / information holder | `MarkerSymbols` (generator) | *Knowing* the Brighter framework interface symbols (`IHandleRequests<>`, `IHandleRequestsAsync<>`, `IAmAMessageMapper<>`, `IAmAMessageMapperAsync<>`, `IAmAMessageTransform`, `IAmAMessageTransformAsync`) in a given compilation, and whether Brighter (and the DI package, for the auto sugar) is referenced at all. |
| Service provider | `SemanticModelReader` (generator) | *Deciding* how a declaration is classified — projecting Roslyn symbols into Roslyn-free, value-equatable records (holder candidates, `DiscoveredEntry`, `DiagnosticInfo`). |
| Information holder | `Model/*` (generator) | *Knowing* the discovered registrations as pure, equatable data the incremental cache can compare by value. |
| Service provider | `RegistrationWriter` (generator) | *Doing* the text generation — turning a `RegistrationModel` into the catalog-construction source. Holds no Roslyn references, so it is unit-testable without a `Compilation`. |
| Coordinator | `BrighterRegistrationsGenerator` (generator) | *Coordinating* the incremental pipeline: wiring the syntax providers, combining streams, and handing models to the writer. |

### Diagnostics

Malformed holder declarations produce errors in the `BRGEN001`–`BRGEN004` range (not `partial`,
not `static`, generic, or nested in a generic type), giving the compile-time feedback that
scanning never could. Generic mappers/transforms cannot be registered as-is and produce
`BRGEN005` (warning) rather than being silently dropped. A Brighter type declared inside an *open
generic* type cannot be named with concrete type arguments, so it is reported as `BRGEN006`
(warning) instead of emitting code that would not compile. `BRGEN007` (info) reports the
suppressed auto form, as above.

Discovery covers both `class` and `record` declarations: handlers must derive from
`RequestHandler<T>` (so are always classes), but mappers and transforms implement interfaces only
and may legitimately be records. A type is reachable when it (and any containing type) is `public`
**or** `internal` — see the accessibility note under Consequences.

## Consequences

### Positive

- **Eliminates the #4159 root cause for first-party types.** Registration is derived from the
  compilation, so the "assembly not loaded at scan time" failure mode cannot occur for types in the
  project (or in a package that adopts this generator). Handlers, mappers **and** transforms are all
  covered — including the transform case (`JustSayingCompressionTransform`) that #4159 calls out.
- **Fits layered solutions.** A domain assembly describes its registrations with no dependency
  beyond core `Paramore.Brighter`; the host composes any number of catalogs with one
  `AddRegistrations` call. No name collisions, no DI/Polly reference in domain projects.
- **Generated code is inert data.** The fragile parts — the open-generic registry cast, the
  default-mapper rule, lifetime decisions — live in `AddRegistrations`, versioned and patchable
  with Brighter itself, rather than frozen into consumer assemblies at their compile time.
- **Catalogs are inspectable and testable.** Registration coverage can be asserted in a unit test
  ("the catalog contains a mapper for `OrderPlaced`") without building a container.
- **Failures move from runtime to build time.** Misdeclared holders fail compilation
  (`BRGEN001`–`BRGEN004`); unsupported generic mappers/transforms warn (`BRGEN005`).
- **Reduces the destructive variant of `MessageMappingException`.** Reliable compile-time
  registration means fewer messages reach the [ADR 0061](0061-reject_mapping_errors.md) reject path
  because a mapper/transform was missing.
- **Trimming / AOT friendly and lower startup cost.** Catalogs are `typeof`-based data; the
  applier uses the registries' existing non-generic `Add(Type, Type)` surface, so no
  `MakeGenericMethod` is introduced.
- **Works at old LangVersions.** The declared-holder form is a plain `partial class`, usable from
  netstandard2.0/net48 libraries at default LangVersion 7.3.
- **Broad SDK compatibility.** The generator assembly is built against Roslyn 4.8.0 (the first
  .NET 8 Roslyn, the oldest in-support modern .NET) via a `VersionOverride`, so it loads on that SDK
  and newer rather than requiring the repo-wide (latest) Roslyn.
- **Behaviourally identical downstream.** Applied registrations flow through the same registries,
  so `ValidatePipelines` / `DescribePipelines` behave the same.

### Negative

- **Referenced-package types are only covered if the package adopts the generator.** A third-party
  package that merely ships compiled mappers/transforms (and does not expose a catalog) is still
  invisible to the consumer's generator. Those users must register such types explicitly or
  continue to use `AutoFromAssemblies`. Source generation does not make scanning obsolete.
- **A new public core type with a compatibility contract.** `RegistrationCatalog` is public API in
  core Brighter and — because shipped consumer packages contain generated code bound to it — may
  only evolve additively.
- **Weaker compile-time typing than generic registration calls.** The superseded design emitted
  `Register<TRequest, THandler>()`, which would fail *compilation* on a generator bug that paired
  the wrong types; a `typeof`-based catalog defers that class of defect to catalog construction at
  startup. Mitigated by argument validation in the `Add*` methods (startup-time, still pre-message)
  and by the generator's golden-source test suite.
- **Two registration paths to understand.** Scanning and source generation now coexist; teams must
  know which they are using (and that they are additive).
- **Generic mappers/transforms are unsupported** (by design) and require a closed type, a
  non-generic wrapper, or an explicit exclude.
- **Accessibility asymmetry with `AutoFromAssemblies`.** The reflection scanner registers only
  `public` (or nested-public) handlers; the generator also registers `internal` types, since the
  generated catalog lives in the same assembly and `internal` is genuinely reachable. This is
  arguably better, but a team switching mechanisms may see `internal` handlers appear/disappear.
  Note that an `internal` handler in a *library* catalog must still be constructable by the host's
  container; the applier registers it in the `IServiceCollection` by `Type`, which does not require
  the host to reference it in source.
- **Open-generic registration remains coupled to `ServiceCollectionSubscriberRegistry`** — but the
  coupling now lives inside `AddRegistrations` in the same package that owns the registry, so the
  two can only drift apart by a change within one package.
- **Handler inheritance can register a request type twice.** For `class B : A` where `A :
  RequestHandler<Cmd>`, both `A` and `B` report `IHandleRequests<Cmd>`, so both are registered. This
  matches the reflection scanner's behaviour rather than introducing a new asymmetry.

### Deferred follow-ups

These are accepted gaps, tracked rather than fixed in this change:

- **The branch currently implements the superseded shape (Alternative 5).** PR
  [#4138](https://github.com/BrighterCommand/Brighter/pull/4138) emits imperative
  `IBrighterBuilder` method bodies; migrating the writer/pipeline to catalog emission, adding
  `RegistrationCatalog` to core and `AddRegistrations` to the DI package is the outstanding work.
  The discovery pipeline (`SemanticModelReader.ReadClass`, `DiscoveredEntry`, the incremental
  caching design and its tests) carries over unchanged.
- **Packaging is not yet wired up.** `IsPackable=false` and there is no companion `*.Package`
  project, so `dotnet pack` produces nothing and the `build/`-props direct-vs-transitive story is
  not yet exercised by a real package consumer — only the in-repo `ProjectReference` /
  `OutputItemType="Analyzer"` path (used by the sample). Shipping should follow the existing
  `Paramore.Brighter.Analyzer.Package` pattern.
- **Public surface of the generator assembly.** The reader/writer/model types are `public` for
  testability; for a dev-dependency generator they could be `internal` + `InternalsVisibleTo`.
  Harmless either way.
- **Possible future: one-call composition across referenced projects.** Each generated holder
  could also emit `[assembly: BrighterRegistrationsProvider(typeof(OrdersRegistrations))]`, letting
  a host-side generator scan only the *assembly-level attributes* of direct references
  (O(references), no type-walking, incremental-friendly) and synthesise an
  `AddFromReferencedAssemblies()`. Deliberately not built now: explicit composition in `Program.cs`
  is reviewable, and the catalog design leaves this door open.

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
  requirement. Because catalogs are applied through the same registries, validation that checks
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
are discovered without the package adopting the generator (the approach taken by
martinothamar/Mediator).

**Rejected because**:
- Walking referenced metadata on every keystroke is the kind of work incremental generators are
  designed to avoid; it would hurt IDE performance and caching.
- It re-creates reflective ambiguity (which referenced assemblies are "ours"?) at build time.
- Per-assembly catalogs give each library ownership of its own registrations at its own compile
  time, which is cleaner and composes. (The assembly-level-attribute hybrid under Deferred
  follow-ups recovers the one-call UX without the metadata walk, if ever wanted.)

### Alternative 3: Make registration a runtime source-generated reflection cache

Generate a static map but still resolve it reflectively at startup.

**Rejected because**:
- Retains a startup reflection step and trimming/AOT hazards for no real gain over emitting catalog
  data applied through the registries.

### Alternative 4: Add `Transforms(...)` / `AddRegistrations(...)` to `IBrighterBuilder`

Put the new registration surface directly on the interface for symmetry with `Handlers` /
`MapperRegistry`.

**Rejected because**:
- It is a binary-breaking change for downstream implementers of `IBrighterBuilder`. Off-interface
  extension methods (`BrighterBuilderExtensions.Transforms`, `.AddRegistrations`) achieve the same
  ergonomics without breaking the contract.

### Alternative 5: Emit imperative registration method bodies (the superseded original design)

The first implementation on this branch generated *behaviour* rather than data: an `internal
static BrighterAssemblyRegistrations.AddFromThisAssembly(this IBrighterBuilder)` extension under
the auto path, and a user-declared `[BrighterRegistrations] static partial IBrighterBuilder
Method(this IBrighterBuilder)` whose body the generator filled with direct
`Handlers`/`MapperRegistry`/`Transforms` calls.

**Superseded because**:
- **It had no multi-assembly story.** The auto method is `internal`, so a domain assembly that
  generated it produced a method its host could not call; composition across several
  handler-owning projects was impossible without hand-written wrappers.
- **The manual form forced the DI stack onto domain assemblies.** Its signature is
  `IBrighterBuilder → IBrighterBuilder`, so any assembly exposing registrations had to reference
  `Paramore.Brighter.Extensions.DependencyInjection` — and transitively
  `Microsoft.Extensions.DependencyInjection` and Polly (which appears on the interface).
- **Application logic was frozen into consumer-generated code.** The
  `ServiceCollectionSubscriberRegistry` cast for open generics and the always-emit-`MapperRegistry`
  default-mapper rule were compiled into every consumer at their build time, unpatchable by a
  Brighter release.
- **Extended partial methods require C# 9**, excluding netstandard2.0/net48 libraries at their
  default LangVersion.

The strongly-typed `Register<TRequest, THandler>()` calls it emitted did give compile-time
verification of generator output; that loss is recorded under Consequences.

### Alternative 6: Self-applying module (behaviour object over a registrar abstraction)

Define `IBrighterRegistrar` in core (generic `Handler<TReq, TImpl>()`, `Mapper(...)`,
`Transform(...)`); the generator fills a user-declared type implementing
`void Register(IBrighterRegistrar r)`; the DI package supplies the registrar and a
`builder.AddModule(...)` extension.

**Rejected because**:
- The result is opaque: registrations cannot be inspected or asserted on without a spy registrar.
- The registrar abstraction in core must mirror everything the DI layer can do, and evolving an
  interface is breaking for implementers (including test doubles).
- Its one advantage — generic instantiations baked at compile time — is unnecessary, since the
  registries already expose a non-generic `Add(Type, Type)` surface that is trimming/AOT-safe.

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
  - [System.Text.Json source generation (`JsonSerializerContext`)](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation) — precedent for the user-declared-partial-holder pattern.
