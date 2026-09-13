# Lifetimes and Scoping

This guide explains how Brighter's three configured lifetimes — `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` — govern the handlers, mappers and transforms Brighter resolves, and how a `Scoped` pipeline can share a host's own ambient dependency-injection scope instead of creating its own. It is the page every scope-related startup message points you back to.

> **Status of this page.** This is part 1 of the guide: the lifetime model itself (this section), the `IAmAScope`/`IAmALifetime` distinction, and the full adoption truth table. Later additions cover the decision guide for choosing a lifetime triple, a troubleshooting entry for each validation message, and the transaction, migration and captive-dependency consequences of adopting `Scoped`. If a section you expected is not here yet, it has not landed.

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
