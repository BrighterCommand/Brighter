# Lifetimes and Scoping

This guide explains how Brighter's three configured lifetimes — `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` — govern the handlers, mappers and transforms Brighter resolves, and how a `Scoped` pipeline can share a host's own ambient dependency-injection scope instead of creating its own. It is the page every scope-related startup message points you back to.

> **Status of this page.** This covers the lifetime model itself, the `IAmAScope`/`IAmALifetime` distinction, the full adoption truth table, the decision guide for choosing a lifetime triple, and a troubleshooting entry for each of the seven validation messages. A later addition covers the transaction, migration and captive-dependency consequences of adopting `Scoped`, and the request-scope extension's three gestures. If a section you expected is not here yet, it has not landed.

## 1. The get/release cycle, by configured lifetime

Each of the three lifetimes is set independently on `IBrighterOptions` (`HandlerLifetime`, `MapperLifetime`, `TransformerLifetime`), and each governs its own kind of artefact — a handler, a mapper, or a transform. A pipeline's overall behaviour is the union of what its participating factories each do for their own configured lifetime; there is no single "pipeline lifetime".

### Transform pipelines (mappers and transforms)

| Configured lifetime | Does the pipeline take a scope? | Resolution and reclamation |
| --- | --- | --- |
| `Transient` | No | Each resolution gets a fresh, per-resolution DI scope (see *Transient's per-resolution scope*, below), released when that resolution is released. |
| `Scoped` | Yes — one DI scope for the pipeline | The pipeline's mapper and every transform on it resolve from the same scope: one artefact per type per pipeline. Both the artefact and its container-`Scoped` dependencies are disposed when the pipeline is released. |
| `Singleton` | No | Resolves from the root provider: one artefact for the life of the process. |

`MapperLifetime` and `TransformerLifetime` are set independently, so a pipeline can mix them — for example `{Scoped mapper, Transient transformer}`. Where they differ, each factory follows its own row above: a `Scoped` mapper takes a pipeline scope and resolves from it, while a `Transient` transformer on the same pipeline resolves from its own per-resolution scope and ignores the pipeline scope. A pipeline scope existing is therefore not the same as the mapper and its transforms sharing a container-`Scoped` dependency — **only `{Scoped, Scoped}` shares one**, which is why mixing `Transient` and `Scoped` across a pipeline's participants is rejected when you opt in to `ValidatePipelines()`.

### Handler pipelines

| Handler lifetime | Does the pipeline take a scope? | Resolution and reclamation |
| --- | --- | --- |
| `Transient` | No — but the handle it is given is not `null` | Each resolution gets its own inner DI scope (`IsolateTransientHandlerScope`), all drained together when the pipeline ends. |
| `Scoped` | Yes — one DI scope for the pipeline | One artefact per type; disposed when the pipeline ends. |
| `Singleton` | No | Resolves from the root provider: one artefact for the life of the process. |

The handler and transform tables look almost identical because they are the same mechanism applied to two different families of factory: whichever participating factory has a `Scoped` configured lifetime is the one that creates and owns the pipeline's single DI scope, and every other participant either shares it (if it too is `Scoped`) or resolves independently of it (if it is `Transient` or `Singleton`).

### Transient's per-resolution scope

A `Transient` artefact is never resolved from the pipeline scope, whether or not one exists. Instead, each `Transient` resolution gets its own fresh `IServiceScope`, created at the moment of resolution and released when that resolution is released — a mapper resolved twice in the same pipeline gets two independent scopes, not one shared with the pipeline. A `Transient` handler pipeline still holds a handle (so that `IsolateTransientHandlerScope` can govern whether handlers within one pipeline share a scope with each other), but that handle is not a pipeline scope in the sense the tables above use the term, and it never participates in ambient adoption (see §3).

## 2. Affinity applies to `Scoped` only

`DefaultScopeAffinity` — the setting that opts a host in to *joining* an ambient DI scope a host already owns, rather than always creating and owning its own — only has anything to join when at least one participating factory's configured lifetime is `Scoped`. A `Transient` or `Singleton` participant never asks an ambient source for anything, at any affinity setting, because neither one ever takes a pipeline scope to begin with.

This has a direct consequence for configuration: setting `DefaultScopeAffinity = ScopeAffinity.JoinAmbient` while none of `HandlerLifetime`, `MapperLifetime` or `TransformerLifetime` is `Scoped` is an **inert opt-in** — it changes nothing, because nothing in the host will ever ask. If you call `ValidatePipelines()`, this configuration is reported as a startup error, because a host that believes it has opted in to sharing scope, but has not, is exactly the kind of silent misconfiguration validation exists to catch. The opt-in is inert whether every lifetime is `Transient`, every lifetime is `Singleton`, or any mixture of the two — the test is "is none of them `Scoped`", not "are they all `Transient`".

## 3. The adoption truth table

This is the full answer to *what does a `Scoped` pipeline resolve from, in every situation Brighter distinguishes*. It only applies where a `Scoped` participant exists in the pipeline at all (§2) — `Transient` and `Singleton` participants are covered once, below, because their answer never varies.

Three outcomes recur throughout the table:

- **Adopts (BORROWED)** — the pipeline resolves from the ambient DI scope a host already owns. Brighter creates nothing and disposes nothing; the host that owns the ambient scope is responsible for its own teardown.
- **Owns (OWNED)** — Brighter creates a fresh DI scope for the pipeline and disposes it when the pipeline ends, exactly as it does when no ambient source is registered at all.
- **No ask, no pipeline scope** — the pipeline never consults an ambient source at all, because no participant is `Scoped` (§2).

### `Transient` and `Singleton` — the answer never varies

| Configured lifetime | Every call site (`Send`, `Post`, `Publish` subscriber, nested pipeline, consume) |
| --- | --- |
| `Transient` | No ask is ever made. Each resolution gets its own per-resolution scope (§1), whatever the affinity setting and whatever an ambient source offers. |
| `Singleton` | No ask is ever made. Resolves from the root provider, one instance for the process, whatever the affinity setting and whatever an ambient source offers. |

### `Scoped` — by call site, affinity and what is offered

| Call site | No `IAmAScopeProvider` registered | `AlwaysNew` (the default) | `JoinAmbient`, an ambient is offered and usable | `JoinAmbient`, no ambient is offered, or the offered one is unusable |
| --- | --- | --- | --- | --- |
| `Send` | Owns; no ask is made (**AC-14**) | Owns; the ask is still made and carries `AlwaysNew` (**AC-13**, **AC-26**) | **Adopts** the ambient's scope — a handler's dependency is reference-equal to the caller's own instance of it (**AC-15**, **AC-18**, **AC-26**) | Owns, exactly as the `AlwaysNew` column — nothing was there to adopt (**AC-19**), or what was there was stale or otherwise unusable and is declined rather than surfaced (**AC-29**) |
| `Post` | Owns; no ask is made (**AC-14**) | Owns; the ask is still made and carries `AlwaysNew` (**AC-13**) | **Adopts** — two `Post`s in the same ambient scope share one mapper instance, and a mapper and a handler sharing the same ambient scope share a `Scoped` dependency between them (**AC-17**, **AC-34**) | Owns, exactly as the `AlwaysNew` column (**AC-19**, **AC-29**) |
| `Publish` subscriber | Owns; no ask is made | **Always owns — never adopts, whatever the affinity setting.** The ask is still made and always carries `AlwaysNew`, so the decision stays observable even though it never varies (**AC-13**, **AC-39**) | *(same as `AlwaysNew` — a subscriber never reaches this column)* | *(same as `AlwaysNew`)* |
| A pipeline nested inside a subscriber (a `Send`, `Post` or `Publish` the subscriber's own handler issues) | Owns; no ask is made | **Always owns — never adopts**, even where the enclosing subscriber's own pipeline had no `Scoped` participant and took no scope of its own (**AC-47**) | *(same as `AlwaysNew`)* | *(same as `AlwaysNew`)* |
| Consume (a message pump's own pipeline) | Owns; no ask is made | **Always owns — never adopts**, whatever the host's own affinity setting says and whatever flow the `Dispatcher` was started from. Resolution counts, instance identity and disposal are identical whichever affinity the host is configured with, and consuming never records a diagnostic `Warning` about an unusable or absent ambient, because the ask never carries `JoinAmbient` to begin with (**AC-20**) | *(same as `AlwaysNew`)* | *(same as `AlwaysNew`)* |

A few rows need a word beyond the table:

- **A pipeline that mixes a `Scoped` participant with a `Transient` one still asks, and still declines to adopt.** The ask is made by whichever participant is `Scoped`, and it is made even though the affinity computed for a mixed pipeline is always `AlwaysNew` — a pipeline shares dependencies across *all* of its `Scoped` participants or none of them, never half (**AC-46**).
- **`Publish` subscribers, and everything nested inside them, are the one case where the affinity setting is irrelevant.** A subscriber's own pipeline, and any pipeline its handler creates while it runs, always creates and owns a scope — this holds regardless of `DefaultScopeAffinity`, and it is not a limitation of the ambient source; it is enforced by Brighter itself so that [ADR 0039](../adr/0039-scoping-dependencies-inline-with-lifetime-scope.md)'s per-subscriber isolation cannot be silently undone by opting in to ambient adoption. Once a `Publish` call returns, the calling flow is not left suppressed: a `Send` or `Post` the caller issues next adopts exactly as it would have if no `Publish` had happened in between (**AC-39**).
- **On the consumer side, a message's transform pipeline is fully torn down before its handler pipeline begins.** The two are separate pipelines with separate scopes even under `{Scoped, Scoped, Scoped}`, so a container-`Scoped` dependency shared by an inbound transform and the handler for the same message is *not* the same instance, and the transform's instance is already disposed by the time the handler runs (**AC-21**) — contrast the `Post` row above, where a mapper and a handler sharing one ambient scope in the same request *do* share the instance.

### Artefact identity, restated for both affinities

Dependency identity always follows the DI scope a pipeline resolves from — two resolutions from the same scope share the same `Scoped` dependencies, whatever produced that scope. Artefact identity (whether the mapper, transform or handler *itself* is the same instance across two calls) follows the pipeline under `AlwaysNew`, and follows the borrowed DI scope under `JoinAmbient`. `Singleton` sits outside both rules: an artefact configured `Singleton` is one instance for the process, resolved from the root provider, regardless of affinity.

## 4. `IAmAScope` versus `IAmALifetime`

These two names are easy to confuse, and the distinction between them is deliberate rather than incidental:

- **`IAmAScope`** *is* a DI scope: a handle to the scope a transform pipeline, or a handler pipeline, takes for its lifetime. It says nothing about where it came from, who owns it, or how to resolve anything through it — it is `IDisposable` and `IAsyncDisposable`, and nothing more. One `IAmAScope` is created per pipeline by whichever participating factory can offer one, and disposed when the pipeline is released.
- **`IAmALifetime`** *tracks handlers* — the handler instances a handler pipeline has created, so they can be released when the pipeline ends. It also *carries* a handler pipeline's own `IAmAScope` handle, on its `PipelineScope` property, but carrying the handle is not the same as being one: **the lifetime scope holds a scope, it does not become one.**

The short version: if you are asking "what DI scope does this pipeline resolve from", the answer is an `IAmAScope`. If you are asking "which handler instances has this pipeline created, and how do I release them", the answer is an `IAmALifetime` — one of which happens to be carrying an `IAmAScope` as well, for the handler family specifically.

## 5. Choosing a lifetime triple

`HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` are not three independent choices. If you call `ValidatePipelines()`, they are validated as one joint choice: discard whichever of the three is `Singleton`, and whatever remains must be a single, uniform lifetime. That single rule is what makes the two triples below the anchor points for every valid configuration — the table is a consequence of the rule, not a separate decision.

| Triple | Cost | Right when |
| --- | --- | --- |
| `{Transient, Transient, Transient}` | A fresh resolution every time an artefact is needed — nothing is shared, within a pipeline or across pipelines. Heaviest on allocation, safest on isolation: nothing this pipeline does can leak into another one, and nothing it depends on can be a captive dependency (see the captive-dependency hazard, below). | The default, and right for most handlers, mappers and transforms: no pipeline-scoped state needs to be shared across the mapper, the transforms and the handler on one pipeline. |
| `{Scoped, Scoped, Scoped}` | One DI scope per pipeline, shared by the mapper, every transform and the handler on it — so all three resolve the same instance of any container-`Scoped` dependency (a `DbContext`, a unit of work, an ambient transaction). Under `JoinAmbient` (§2), the pipeline can adopt a caller's own scope instead of creating one — but only when all three positions are `Scoped`; a mix defeats the sharing this triple exists for, which is exactly why the joint rule rejects it once you opt in to checking it. | A `DbContext`, transaction, or other pipeline-scoped dependency must be shared across the mapper, transforms and handler on one pipeline — or the pipeline must adopt the caller's ambient scope, as when a `Post` and a `Send` issued from the same web request need to share one unit of work (the case the adoption truth table's `Post` row, §3, exists for). |

**Any of the three positions may independently be `Singleton` instead, in either triple above, and the configuration still validates** — a `Singleton` position is discarded before the remainder is checked for uniformity, so it never breaks either triple's pattern. `{Singleton, Scoped, Scoped}`, `{Scoped, Singleton, Scoped}`, `{Transient, Singleton, Transient}`, `{Singleton, Singleton, Transient}` and every other one- or two-position substitution of `Singleton` into either base triple are all valid destinations on the same terms as the triple they were substituted into. `{Singleton, Singleton, Singleton}` is the one point the two triples' substitutions meet — see the per-kind notes below for when a given kind should be the one that is `Singleton`.

**One more condition applies, and only if you have also set `DefaultScopeAffinity = ScopeAffinity.JoinAmbient` (§2).** A triple with **no** `Scoped` position left at all is then rejected — not because the lifetimes conflict with each other, but because the opt-in itself is inert: nothing in the pipeline will ever ask the ambient source for anything. Every substitution built from `{Transient, Transient, Transient}`, including `{Singleton, Singleton, Singleton}`, falls into this under `JoinAmbient`; every substitution built from `{Scoped, Scoped, Scoped}` that keeps **at least one** `Scoped` position does not. If you are not opting in to `JoinAmbient`, this condition does not apply, and every substitution above is valid without qualification.

### When a kind should be `Singleton`

The same two reasons apply to a handler, a mapper or a transform: it is genuinely **stateless** (nothing it does depends on which pipeline is running, so there is nothing to isolate per pipeline), or it is **deliberately caching across pipelines** by design — the case FR-20 has in mind for a mapper that used to rely on the old process-lifetime `Scoped` caching and is migrating to say so explicitly. Either reason carries the same caveat: a `Singleton` artefact resolves from the DI container's root provider, so a container-`Scoped` dependency on its constructor is captive — held for the life of the process, or an exception if the container's `ValidateScopes` is on. Brighter's own captive-dependency warning, and its limits, are covered where that hazard is documented in full, below.

- **Handler** — a stateless piece of business logic with no per-request dependency, or a handler deliberately holding a shared, cross-request resource (an in-memory counter, a warm cache) on purpose.
- **Mapper** — the common case: most message mappers are pure serialization with no injected state at all, and are `Singleton`-safe by construction. A mapper relying on a cache built once and reused across every message is the other case, and is exactly what FR-20 asks a formerly-`Scoped`, process-cached mapper to become.
- **Transform** — a stateless transform (compression, encryption with no per-message key material) or one deliberately maintaining state across messages, such as a shared connection or buffer pool.

### The three answers, and why they do not contradict each other

It is easy to read "`Scoped` fixes per-pipeline state", "`Singleton` is the migration for cross-pipeline caching" and "`Transient` is the default" as three answers to the same question. They are not — each belongs to a different situation:

- **Choose `Scoped` when a `DbContext`, an ambient transaction, or some other pipeline-scoped dependency must be shared** across the mapper, the transforms and the handler on one pipeline, or when the pipeline must adopt the scope its caller already owns. Because the rule is joint, this is never a one-member change: moving one kind to `Scoped` moves all three lifetimes together, to `Scoped` for every kind that needs the sharing and `Singleton` for every kind that does not.
- **Choose `Singleton` when an artefact is stateless, or when it is deliberately caching across pipelines by design** — the situation FR-20 addresses directly: code that relied on `MapperLifetime.Scoped`'s old process-wide caching migrates to `MapperLifetime.Singleton`, which states the same intent explicitly rather than getting it as an accidental side effect of what `Scoped` used to mean.
- **`Transient` remains the default because it needs no reasoning about sharing at all.** It is the safe starting point precisely because nothing is shared, in either direction: a `Transient` artefact can never be a captive dependency, and it never has state left over from one pipeline to leak into the next. Reach for `Scoped` or `Singleton` only once you have identified the specific dependency, or the specific caching intent, that `Transient` does not give you.

## 6. Troubleshooting validation messages

Every message below is only surfaced if two things are both true: your application calls `ValidatePipelines()`, **and** something actually runs the validation it registers. Calling `ValidatePipelines()` on its own is not enough — it registers the validators and takes a snapshot of the container, but a *hosted service* is what evaluates them at startup and reports the result.

- On a producer host (`AddBrighter` only), `BrighterValidationHostedService` does this automatically — nothing further to register.
- On a consumer host (`AddConsumers`), Brighter itself does not run validation — `AddConsumers` sets a flag that defers to `ServiceActivatorHostedService`, which lives in `Paramore.Brighter.ServiceActivator.Extensions.Hosting` and is **not** registered by `AddConsumers`. If your application never adds that package and registers the service, no message from this section — error or warning — is ever produced, however wrong the configuration is. This is a known, accepted gap, not a bug: if consumer-side validation appears to be silently doing nothing, this is the first thing to check.

**Call `ValidatePipelines()` last, as its own statement — not chained onto `AddBrighter`.** The snapshot it takes covers only what is registered in the container *before* it runs. Hold the `IBrighterBuilder` in a variable and call `ValidatePipelines()` on it after every other registration your host makes:

```csharp
var brighter = services.AddBrighter(options => { ... });
// ...every other registration, including AddBrighterRequestScope and any
// application registration of IBrighterOptions...
brighter.ValidatePipelines();
```

If you instead chain `.ValidatePipelines()` straight onto `AddBrighter(...)` in the natural fluent style, any registration your application makes afterwards — most importantly `services.AddSingleton<IBrighterOptions>(...)` — lands *after* the snapshot. The defeated-opt-in message below exists specifically to catch that registration, and chaining early is the one shape that lets it slip past validation instead of being reported.

Three of the seven messages are **errors** (validation fails startup); four are **warnings** (validation logs and continues). Each entry below gives the cause and a concrete remedy — you should not need to read Brighter's source, the ADRs or this spec's requirements to act on any of them.

### Inert opt-in (FR-22.1, Error)

**Cause.** `DefaultScopeAffinity` is `JoinAmbient`, but none of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` is `Scoped`. `JoinAmbient` only has anything to do for a `Scoped` participant (§2), so the opt-in changes nothing.

**Remedy.** Pick one of two fixes, depending on which one you meant:
- If you want the ambient adoption to actually happen, adopt the `{Scoped, Scoped, Scoped}` triple (or one of its `Singleton` substitutions that keeps at least one `Scoped` position, §5) — set `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` so that at least one is `Scoped` and none mixes `Transient` with `Scoped`.
- If you don't need ambient adoption, remove `DefaultScopeAffinity = ScopeAffinity.JoinAmbient` (or set it to `ScopeAffinity.AlwaysNew`, the default) and leave the lifetimes as they are.

### Mixed `Transient`/`Scoped` (FR-22.2, Error)

**Cause.** After discarding any of the three lifetimes that is `Singleton`, the remainder is not uniform — some of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` are `Transient` and others are `Scoped`. A mixed pair never shares a pipeline-scoped dependency, so the configuration cannot do what setting `Scoped` on only some of them implies.

**Remedy.** Choose one of the two base triples from §5 and apply it uniformly: `{Transient, Transient, Transient}` if nothing needs to be shared across the mapper, transforms and handler on a pipeline, or `{Scoped, Scoped, Scoped}` if something does. Either position may then independently be changed to `Singleton` (§5) without reintroducing the mix.

### Captive dependency (FR-22.3, Warning)

**Cause.** A handler, mapper or transform configured `Singleton` has a constructor parameter registered `Scoped` in the container. Because a `Singleton` artefact resolves once from the root provider, that `Scoped` dependency is held for the life of the process — a captive dependency — rather than being released when the pipeline that created it ends.

**Remedy.** Two fixes, depending on which side should change:
- Move the *artefact* off `Singleton`: give it the same lifetime as the rest of its triple instead — `Transient` if the pipeline is otherwise `{Transient, Transient, Transient}`, or `Scoped` if it is otherwise `{Scoped, Scoped, Scoped}` — so it resolves per-pipeline instead of once from the root.
- Or move the *dependency* off `Scoped` — register it `Singleton` (if it is safe to share across the process) or `Transient` (if a fresh instance per resolution is safe) so a `Singleton` artefact can hold it without capturing anything scoped.

This warning inspects only the artefact's own constructor parameters directly — it does not follow a dependency's own dependencies. The container's `ValidateScopes` option remains the complete check for captivity anywhere in the graph; treat this warning as a targeted early signal, not a substitute for it.

### Defeated opt-in (FR-22.4, Error)

**Cause.** An affinity override (`AddBrighterRequestScope(...)`) is registered, but the `IBrighterOptions` the container will actually resolve — the last unkeyed registration — was supplied by your application (`services.AddSingleton<IBrighterOptions>(...)` or similar), not by Brighter's own `AddBrighter`/`AddConsumers`. Only Brighter's own registration carries the write-through that applies the override, so the affinity you asked for was never applied.

**Remedy.** Remove your application's own `IBrighterOptions` registration, and configure the same options through `AddBrighter(Action<BrighterOptions>)` or `AddConsumers`'s options action instead — set `DefaultScopeAffinity` there, or keep using `AddBrighterRequestScope(...)` once nothing else is registering `IBrighterOptions` directly. Until this is fixed, the override has no effect at all: the affinity in force is whatever your own `IBrighterOptions` object carries — `ScopeAffinity.AlwaysNew` (the option's own default) unless your application set it to something else.

### Duplicate scope provider (FR-24.3, Warning)

**Cause.** More than one distinct `IAmAScopeProvider` implementation is registered unkeyed. The container resolves an unkeyed service to the last-registered descriptor, so only one of them is ever actually asked for an ambient scope — the rest are silently shadowed rather than combined or preferred by type.

**Remedy.** Remove every `IAmAScopeProvider` registration except the one you want in effect. The message names every distinct implementation found and identifies the last-registered one as the one currently effective — until you remove the others, that is the provider being used, and any of the shadowed ones' behaviour is simply never reached.

### Conflicting repeated opt-in (FR-17, Warning)

**Cause.** `AddBrighterRequestScope(...)` was called more than once, with different `ScopeAffinity` values. The last call wins — MS DI resolves `IBrighterOptions` to the last-registered unkeyed descriptor, and that descriptor's write-through carries the last call's affinity — so the earlier call's affinity is silently discarded rather than combined with it.

**Remedy.** Call `AddBrighterRequestScope(...)` exactly once. If you need a specific affinity, pass it as the extension's own argument (`AddBrighterRequestScope(ScopeAffinity.AlwaysNew)` or `AddBrighterRequestScope(ScopeAffinity.JoinAmbient)`) rather than calling the extension again to change it. Until you remove the extra call, the message names every distinct affinity registered and identifies the last call's as the one currently in effect.

### Unreadable override (FR-17, Warning)

**Cause.** An affinity override is registered by factory delegate (`services.AddSingleton(sp => new ScopeAffinityOverride(...))` or similar) rather than as a constructed instance. The override still takes effect — the write-through resolves it normally — but this validator cannot read its value without resolving it, so it cannot detect whether a *later* conflicting registration (the previous message) is present.

**Remedy.** Register the override as a constructed instance instead of a factory delegate — `services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient))`, or simply use `AddBrighterRequestScope(...)`, which registers this way already. Until this is fixed, the override's own affinity still applies as normal; what is lost is only this validator's ability to warn you if a second, conflicting `AddBrighterRequestScope`/override registration is added later.
