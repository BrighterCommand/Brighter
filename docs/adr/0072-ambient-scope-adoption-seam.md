---
id: 0072-ambient-scope-adoption-seam
title: "Adopting an ambient DI scope — the resolution-source hand-off"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-08-02
summary: "An ambient DI scope offers its resolution source through IAmAServiceProviderScope, a role interface declared in the container package rather than in core. A container-backed factory asks that source once, with an affinity computed over the pipeline's whole participating set, and then either borrows the source without owning it or creates and owns a scope exactly as today."
tags:
  - "di"
  - "lifetime"
  - "pipeline"
---

# 72. Adopting an ambient DI scope — the resolution-source hand-off

Date: 2026-08-02

## Status

Proposed

## Context

Brighter resolves a pipeline's mapper, transform and handler instances from a DI scope of its own making. An application that has already opened a scope — an ASP.NET Core request — therefore ends up with two, its own and Brighter's, and a dependency the two share is two instances rather than one. ADRs 0070 and 0071 gave every pipeline a single place to ask for its scope, which is all the machinery adoption needs. What no ADR has decided is how a pipeline discovers a scope the host already owns, and how it gets at the resolution source behind it.

### Terms

ADR 0067's `Terms` block defines *configured lifetime* and *registration lifetime*, and keeps `IServiceScope`, `ServiceProviderLifetimeScope` and `IAmALifetime` distinct from one another. This ADR uses those definitions and does not restate them. Four further words carry weight below.

- **Ambient** — a DI scope the host already owns, discoverable on the current logical flow. An ASP.NET Core request scope is the case this ADR is written for. *An ambient* is the `IAmAScope` a provider offers; *adopting* it means resolving from the source behind it instead of from a scope Brighter created.
- **Artefact** — the mapper, transform or handler instance Brighter resolves, as against the dependencies the container constructs it with. The configured lifetime governs the artefact; the registration lifetime governs its dependencies (ADR 0067).
- **Seam** — the pair of role interfaces by which an ambient reaches a container-backed factory. `IAmAScopeProvider` sits in core and answers whether there is an ambient; `IAmAServiceProviderScope` sits in the DI package and names the `IServiceProvider` behind one.
- **Residue** — the cases the seam's two tests do not catch. Step 2d names three of them and states what happens in each.

### Scope

**Parent requirement**: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md)

This ADR decides two things: the non-core hand-off by which an ambient exposes a resolution source to the container package, and — because the hand-off is unimplementable without it — which object computes a pipeline's `ScopeAffinity` when its participating factories have different configured lifetimes.

**In scope.** Each requirement below is discharged here by the named mechanism.

- **FR-10's ambient-source half — the ambient source is a public seam.** `IAmAScopeProvider` is a core interface with one member, so an implementation may live in any assembly. OOS-3 bounds the obligation: implementations for other containers must be *permitted*, and none ships here. This ADR declares two of FR-10's three named types, `IAmAScopeProvider` and `ScopeAffinity`; `IAmAScope` is ADR 0070's, on the model FR-13 also follows.
- **FR-11 — a host with no usable ambient behaves exactly as it does today.** Ladder row 3 covers "no provider registered" (FR-11(a)) and ladder row 8 covers "an ambient this container package cannot use" (FR-11(b)).
- **FR-12 — Brighter never disposes a scope it borrowed.** Ladder row 10 borrows without owning, and a borrowed `ServiceProviderPipelineScope` disposes nothing. FR-13's borrowed-scope carve-out lands here: FR-13 routes "a borrowed scope is never disposed at all" to FR-12 in terms rather than keeping a clause of its own.
- **FR-16 — pipelines in one HTTP request share the request scope.** `ScopedArtefactCache` gives FR-16(a): two `Post`s in one request share one mapper (D7). The borrowed scope gives dependency identity across a `Send`'s handler pipeline and a `Post`'s transform pipeline in one request, which is FR-16(b)'s mechanism and AC-34's assertion. FR-16(c) is FR-16(b) extended across the call boundary, and it is what carries a caller's transaction into a handler (C-21, AC-52).
- **FR-18 — an opted-in host with no ambient available creates its own scope.** Ladder row 7, the fall-back to a Brighter-owned scope when a registered provider offers nothing.
- **FR-19 — no consumer pipeline adopts.** ADR 0075's pump-flow bracket in `Performer.Run()` suppresses the pump's own flow. Every consumer pipeline's ask therefore carries `AlwaysNew` and lands on ladder row 6, which creates and owns a scope. ADR 0075 owns the mechanism and states the site; ADR 0076 supplies the property and its inheritance onto `ConsumersOptions`; this ADR discharges the requirement. The pump publishing no per-message ambient (D0b, OOS-1) is not the reason and is not offered as one. It would leave a `Dispatcher` started from inside a live request free to inherit an `HttpContext`, which the seam would borrow at ladder row 10. C-14 states the invariant the bracket delivers, and AC-55 pins it.
- **FR-21 — affinity applies to `Scoped` only.** Ladder rows 1 and 2 make a factory whose configured lifetime is not `Scoped` offer nothing and make no ask, and `ScopeAffinityPolicy` yields `JoinAmbient` only where at least one participant is `Scoped` and none is `Transient`. AC-26 is its guard. ADR 0076 supplies the property those tests read and its `AlwaysNew` default, and says so.
- **FR-23 — an ambient Brighter must not resolve from is declined, not used.** `AmbientScopeProbe` and ladder rows 8 and 9, and the decline is the first of `AmbientScopeDiagnostics`' three latched warnings, *ambient offered but unusable*.
- **FR-24 — the provider contract.** `AmbientScopeSourceException` and the six builder `catch` clauses carry a provider fault to the caller unwrapped (FR-24.1). `AmbientScopeDiagnostics` latches the other two, *no ambient offered* and *ignored for an `AlwaysNew` ask* (FR-24.2, FR-24.4). FR-24.3 is split: this ADR fixes the registration model that makes a duplicate detectable, and ADR 0074 owns the site at which the rule is evaluated.
- **FR-26 — Brighter holds no state that outlives a scope it does not own.** `ScopedArtefactCache` is resolved *from* the borrowed scope rather than held beside it, so the container disposes it with the scope.
- **FR-27.1 and FR-27.2 — which pipelines take a scope, and with what affinity.** `ScopeAffinityPolicy` applies the rule over the pipeline's whole participating set.
- **The affinity computation itself, which no tagged requirement carries.** FR-27.2 states the rule over the participating set, but `CreatePipelineScope()` is called on one factory, so some object has to hold the rule for the set. That object is `ScopeAffinityPolicy`.
- **NFR-7 — the seam is implementable off ASP.NET and off Microsoft's container.** NFR-7 is a non-preclusion clause, and what discharges it is the hand-off's shape: `IAmAScopeProvider` is a one-member role interface, so any assembly implements it without inheriting anything and without a container reference. The guard is **AC-35**, whose provider holds its ambient in an `AsyncLocal` and references no ASP.NET type. ADRs 0073, 0075 and 0076 each keep their own mechanism open on the same terms and name this ADR.
- **NFR-4 for the borrowed request scope — the one place this design does share state.** Relocating the artefact cache turns something one pipeline owned into something every concurrent pipeline in a request contends for. `ScopedArtefactCache` answers it by inheriting the `ConcurrentDictionary<Type, Lazy<object?>>` publish protocol `ServiceProviderLifetimeScope.cs:163-178` already uses, rather than inventing one. The transform and handler families are ADRs 0070's and 0071's, and suppression is ADR 0075's.

**Contributed to here, discharged elsewhere.**

- **FR-13's disposal-failure clause** belongs to ADR 0070 for transform pipelines and to ADR 0071 for handler pipelines, where AC-33 guards it. FR-13's two clauses divide between those two ADRs; what this ADR adds is the ownership half — who owns, and who must not dispose, a scope the pipeline was handed.
- **FR-27.3 — suppression as a subscriber property** — is ADR 0075's. It enters the protocol below at one line and adds no outcome to it.
- **NFR-2 — no ASP.NET dependency in the DI package.** The dependency direction this ADR's hand-off fixes is what makes it achievable, but the requirement is discharged by ADR 0073, which puts the ASP.NET ambient in a package of its own. **AC-22.2** is the guard.
- **NFR-8 — `IAmAScope` against `IAmALifetime`.** This ADR declares neither type, so nothing here discharges the obligation. It is ADRs 0070's and 0071's, split between the two documentation ends, with ADR 0074's guidance page carrying the same distinction.

**Out of scope.**

- **Three names ADR 0073 settles** — the ambient-query member `GetAmbient(ScopeAffinity)`, the package `Paramore.Brighter.Extensions.AspNetCore` and the registration extension `AddBrighterRequestScope(ScopeAffinity)` (C-11). This ADR uses their settled spellings where it names them at all.
- **The shape and spelling of the opt-in property on `IBrighterOptions` — ADR 0076's** (C-9). This ADR calls it *the affinity option* and depends on none of its spelling.
- **How a `Publish` subscriber suppresses adoption — ADR 0075's**, which owns the flag, all three brackets and the reasoning about `ExecutionContext`.
- **Where FR-22's validation rules are evaluated — ADR 0074's.** For FR-24.3's duplicate-provider warning this ADR decides the registration model that makes a duplicate detectable and resolution predictable; the site at which the rule is evaluated and the message produced are ADR 0074's.

This ADR supersedes no prior ADR. It extends the 0066–0069 sequence and completes the seam ADRs 0070 and 0071 opened.

### Where this ADR sits

Seven ADRs deliver the parent requirement; the requirements constrain observable behaviour only and hand how it is implemented to design (C-13). This is the third; the first two close the defects and build the handle, and this is where the feature starts.

| ADR | Decides |
| --- | --- |
| 0070 | a transform pipeline takes one DI scope, carried **as a parameter** |
| 0071 | handler pipelines converge onto the **same handle**, carried on the object they already pass |
| **0072** *(this one)* | how a pipeline discovers an **ambient** DI scope the host owns |
| 0073 | the **ASP.NET Core package**, and the one line an application writes to opt in |
| 0074 | **where** the scope-configuration rules are evaluated |
| 0075 | how a `Publish` subscriber and the consumer pump **suppress** adoption, for themselves and every pipeline created beneath them |
| 0076 | the **affinity option**, and how one setting reaches all four registration paths in any order |

The rule the first two state is **the per-pipeline object carries the DI scope**, and this ADR is where that object learns it may not have created its own scope at all.

The two siblings converged both pipeline families onto one member, `CreatePipelineScope()`. That is why adoption is a change in one place: joining an ambient scope is simply what that member returns.

This ADR does not use "lifetime scope" for anything it introduces; the `Terms` block above says where that vocabulary is fixed. NFR-8 is a documentation obligation about one specific ambiguity, `IAmAScope` against `IAmALifetime`, and this ADR declares neither of those two types: it is discharged by ADR 0070 on `IAmAScope` and ADR 0071 on `IAmALifetime`, and repeated on ADR 0074's guidance page.

### What the two siblings leave open

| Question | Where it stands after 0070 and 0071 |
| --- | --- |
| How does a pipeline learn that an ambient DI scope exists? | Nothing exists. `IAmAScopeProvider` and `ScopeAffinity` are named by D4 but no ADR has introduced them |
| Once an ambient is offered, how does a container-backed factory resolve from it? | `IAmAScope` has no members beyond disposal, deliberately, so it carries no answer. ADR 0070 explicitly declined to introduce a hand-off type |
| When a pipeline's participating factories have different lifetimes, who decides the affinity? | FR-27.2 states the rule over the whole participating set, but `CreatePipelineScope()` is called on **one** factory |

### The forces

- **Core must stay container-agnostic.** ADR 0014 is the principle: Brighter offers per-family factory interfaces rather than abstracting an IoC container, and the application supplies the implementation. No type in `Paramore.Brighter` may name `IServiceProvider`, `IServiceCollection`, `ServiceLifetime` or `ServiceDescriptor`. That has to hold at the level of core's *source*, because `Microsoft.Extensions.DependencyInjection` is already on core's compile closure transitively through `Microsoft.Extensions.Logging`. So the type that carries a resolution source cannot live in core, and `IAmAScope` cannot grow a member that returns one.
- **C-1 — Microsoft's DI scopes do not nest.** A scope created from a scoped provider is root-parented, so it cannot see the request's `DbContext`. Adopting can therefore only mean resolving directly from the caller's provider. There is no "wrap the ambient in a child scope" option that would have let the handle stay opaque.
- **C-7 — ownership travels with the DI scope.** Created implies Brighter disposes (FR-13); adopted implies the caller's, never disposed by Brighter (FR-12). An ambient that is offered and then declined is the *created* case, and Brighter must not dispose the thing it declined. Two conditions produce a declined ambient: a stale resolution source (FR-23), and an ambient returned for an `AlwaysNew` ask (FR-24.4).
- **D11 — the provider is an ambient source, not a scope supplier.** The container package always creates and owns the `IServiceScope` when it is not borrowing one, so "no provider registered" (FR-11), "provider returned nothing" (FR-24.2) and "ambient present but unusable" (FR-23) all collapse onto one path: create your own. Brighter registers no default provider, which is what makes registering the ASP.NET one incapable of producing a duplicate.
- **NFR-7 — the seam is a public extension point, and must be implementable off ASP.NET and off Microsoft's container.** Two criteria pull in opposite directions and together fix the hand-off's shape. AC-35 requires a test-assembly provider that holds its ambient in an `AsyncLocal`, references no ASP.NET, and whose ambient a Brighter handler genuinely resolves from. AC-13 requires a different fake, in an assembly that references no container package at all, which records only the affinity each pipeline asked with.
- **FR-10 pairs that obligation with OOS-3.** Implementations of the seam for other containers — Autofac, SimpleInjector, Lamar — must be permitted, and none ships here. The shape below is therefore designed against a participant this specification will not itself supply, which is why AC-35's and AC-13's stand-ins have to carry the weight a shipped one would.
- **FR-8 must be honourable from outside this package.** Per-subscriber isolation on `Publish` is decided by ADR 0075, but the pipeline that honours it is this one. Whatever carries that decision has to be readable by a container package Brighter does not ship (NFR-7), which is why the protocol below reads a flag rather than receiving an argument.
- **D19 — the diagnostic latches are per Brighter container**, once per (condition, provider implementation type). They must belong to a container-scoped singleton rather than to a `static`, or AC-11's third branch is unsatisfiable by a correct implementation.
- **NFR-2 and D1 — no ASP.NET dependency in the DI package.** The ASP.NET package depends on the DI package; never the reverse. Registering the provider *is* the opt-in, and there is no middleware.
- **NFR-4 — thread safety.** Concurrent pipelines on different threads must not interfere with one another, and nothing the seam introduces may be torn or shared between them.

## Decision

**An ambient scope offers its resolution source through a role interface that lives in the container package, and a factory either borrows that source without owning it or creates and owns a scope exactly as it does today.**

The role is `IAmAServiceProviderScope`, an `IAmAScope` that can name the `IServiceProvider` behind it. A container-backed factory tests for the role inside `CreatePipelineScope()`, having asked the ambient source exactly once, carrying an affinity computed over the pipeline's whole participating set. The affinity is computed by a policy object rather than by each factory, because a pipeline's participating factories can carry different configured lifetimes and only one of them is asked to create the scope. Everything else — where a declined ambient goes, when a diagnostic fires, who owns what — falls out of a single ordered protocol.

### The mechanism, end to end

One pipeline's scope acquisition, from the builder's call to the handle it gets back:

```mermaid
sequenceDiagram
    participant B as pipeline builder
    participant F as container-backed factory
    participant Pol as ScopeAffinityPolicy
    participant Src as IAmAScopeProvider
    participant Pr as AmbientScopeProbe
    participant H as ServiceProviderPipelineScope

    B->>F: CreatePipelineScope()
    F->>Pol: the affinity for this participating set
    Pol-->>F: JoinAmbient, or AlwaysNew
    F->>Src: GetAmbient(affinity)
    Note over F,Src: asked exactly once per pipeline, whatever the affinity
    Src-->>F: an ambient, or nothing
    F->>Pr: CanResolveFrom, if the ambient implements the role
    Pr-->>F: usable, or not
    alt JoinAmbient, role implemented, probe passes
        F->>H: borrow — resolve from Services, own nothing
    else every other outcome
        F->>H: create and own a scope, as today
    end
    H-->>B: an IAmAScope the pipeline holds and ends
```

The full protocol is a ladder. Each row is tested in order and the first that matches decides:

| | Situation | Outcome | Diagnostic |
| --- | --- | --- | --- |
| 1 | **the factory being asked** has no scope to offer — its own configured lifetime is not `Scoped` (mapper factory: `MapperLifetime`; transformer factory: `TransformerLifetime`), or, for the handler factory, is `Singleton` | `null`: this factory offers nothing. **Transform family**: ADR 0070's first-non-null routing asks the next participant. **Handler family**: there is no next participant — the pipeline takes no pipeline scope and makes no ask | none |
| 2 | `Scoped` does not participate in this pipeline — handler family, `HandlerLifetime` is `Transient` | **a handle, but not an FR-27 pipeline scope** — ADR 0067's per-resolution machinery riding on a handle — and **no ask is made at all** (FR-27.1) | none |
| 3 | no `IAmAScopeProvider` is registered at all | **OWNED**, and no ask is made — there is nothing to ask. Behaviour is exactly as before this change whatever the affinity option says (FR-11(a)) | none — see the note below the table |
| 4 | the ambient source throws | the fault is wrapped in `AmbientScopeSourceException`, which each builder's `catch` recognises: whatever cleanup that `catch` already runs still runs (none, in the handler family), then the **original** is rethrown **unwrapped** — a misconfigured container is a startup-class fault, never degraded to "no ambient" and never folded into `ConfigurationException` (FR-24.1, AC-30) | none |
| 5 | the ask did **not** carry `JoinAmbient`, and something came back | **OWNED**; the ambient is ignored *before* it is probed, and never disposed (FR-24.4) | *ambient offered for an `AlwaysNew` ask and ignored* |
| 6 | the ask did **not** carry `JoinAmbient`, and nothing came back | **OWNED** | none |
| 7 | the ask carried `JoinAmbient`, and nothing came back | **OWNED** (FR-24.2, which includes FR-18's ordinary "no current `HttpContext`" case) | *no ambient offered* |
| 8 | something came back, but does not implement `IAmAServiceProviderScope` | **OWNED**; declined, never disposed (FR-11(b), FR-13, C-7) | *ambient offered but unusable* |
| 9 | something came back and implements the role, but fails the usability probe | **OWNED**; declined, never disposed (FR-23) | *ambient offered but unusable* |
| 10 | something came back, implements the role, and passes the probe | **BORROWED** — resolve from it, own nothing, dispose nothing (FR-12, C-7) | none |

Row 3 is silent by requirement rather than by omission. FR-11(a) makes the affinity irrelevant where no provider is registered, and the diagnostics FR-19 names are bounded to hosts *where an ambient source is registered*.

**A pipeline that declines an ambient, or is offered none, ends at create and own a scope, which is exactly today's behaviour.** That is the design's central property. Six distinct failures converge on one fallback, and it is the one that already works: no provider, nothing offered, a stale ambient, an ambient from a container this package cannot use, an ambient offered for an `AlwaysNew` ask, and suppression in force.

Five further invariants can be read off the ladder.

**Rows 1 and 2 are both FR-27.1's "no pipeline scope", and row 2 still yields an object.** FR-27.1 puts them in one category — a pipeline with no `Scoped` participant takes no pipeline scope and asks nothing — and neither row makes an ask. What row 2 returns is nonetheless non-null, because a `Transient` handler pipeline carries ADR 0067's per-resolution isolation and `IsolateTransientHandlerScope` on the same handle (ADR 0071). That handle is not what FR-27 means by a pipeline scope, and rows 3–10's `OWNED` is reserved for one that is. An implementation asserting AC-46's "no pipeline scope taken" over the handle's nullness is testing the wrong thing. Under FR-27.1 the ask and the pipeline scope are co-extensive: a pipeline that takes one asks exactly once, and a pipeline that takes none never asks. The recorder's zero asks *is* the assertion of "no pipeline scope taken". There is no separate observable, and none is needed; AC-13's own note says the fake cannot see scopes.

FR-27.1 and AC-46 define *"takes no pipeline scope"* over the seam — no ambient ask, no adoption decision — and not over `IAmALifetime.PipelineScope`'s nullness, which is non-null under `Transient` (ADR 0067). ADR 0071 states the same rule beneath its own contract table.

**Suppression enters at exactly one line, and that line is the affinity computation.** Rows 3 onwards are reached only after the affinity has been computed:

> `affinity = AmbientScopeSuppression.IsSuppressed ? AlwaysNew : the policy over the whole participating set`

`AmbientScopeSuppression` is ADR 0075's type — it owns the flag, all three of its brackets and the reasoning — and this is the one line in this design that reads it. Suppression adds no row to the ladder: a suppressed pipeline takes rows 5 or 6, which is where a host whose ambient source offers nothing lands anyway.

**Neither a `Publish` subscriber nor a consumer pipeline ever adopts.** Two kinds of flow reach the affinity computation already suppressed. A `Publish` subscriber, and everything nested beneath it, is suppressed by ADR 0075's first two brackets (FR-8, FR-27.3). A consumer pipeline is suppressed for its whole life by that ADR's third bracket in `Performer.Run()`, so no pipeline the pump drives ever computes `JoinAmbient`, whatever the affinity option says and whatever flow the `Dispatcher` was started from. The consumer's ask is still made and still carries `AlwaysNew` (D16), so the decision stays observable; ADR 0073's provider returns nothing on such an ask, so it lands on row 6 — `OWNED`, no diagnostic. That is what discharges FR-19 here, and ADR 0075 step 4a is where the site and the reason for it live.

FR-19, AC-20 and C-14 state this outcome. A `JoinAmbient` consumer run records **zero** FR-24.2 *no ambient offered* `Warning`s, because the pump-flow bracket makes the ask carry `AlwaysNew` and row 6 fires. AC-55 pins the same result for a `Dispatcher` started from inside a live request.

**Row 1 is a test on one factory, and the ladder runs once per pipeline.** Those two facts have to hold together or D16's *exactly one ask* is not delivered, and what makes them hold is ADR 0070's first-non-null routing: the participants are asked in a fixed order and the first that offers a handle wins, so at most one of them ever gets past row 1. Walk `{Transient mapper, Scoped transformer}`. The registry is asked first and forwards to the mapper factory, whose `MapperLifetime` is not `Scoped`, so row 1 returns `null` and it makes no ask. The transformer factory is asked next. Its `TransformerLifetime` *is* `Scoped`, so it falls through row 1 and runs the rest of the ladder, computing the affinity over both lifetimes. That is why the policy is not a per-factory test. The pipeline gets one scope and one ask, from the participant that had something to offer.

**Three further details of the ladder.** The ask happens even when the affinity is `AlwaysNew` — rows 5 and 6 (D16) — which is what makes a pipeline's adoption decision observable at all. A declined ambient is never disposed, at rows 5, 8 and 9 alike, because Brighter does not own what it declined (C-7). And the three diagnostics are distinct, independently latched conditions, not three spellings of one.

### Where the pieces live

```mermaid
flowchart TB
    subgraph core["Paramore.Brighter — core, names no container type"]
        handle["IAmAScope, from ADR 0070<br/>the handle a pipeline holds and ends"]
        provider["IAmAScopeProvider — NEW<br/>IAmAScope? GetAmbient(ScopeAffinity)"]
        affinity["ScopeAffinity — NEW<br/>AlwaysNew = 0, JoinAmbient"]
        fault["AmbientScopeSourceException — NEW<br/>carries a provider fault past the builders' catch"]
    end

    subgraph di["Paramore.Brighter.Extensions.DependencyInjection"]
        role["IAmAServiceProviderScope : IAmAScope — NEW<br/>names the IServiceProvider behind an ambient"]
        policy["ScopeAffinityPolicy — NEW, internal<br/>FR-27.2 over the whole participating set"]
        probe["AmbientScopeProbe — NEW, internal static<br/>is this ambient usable? one answer for all five factories"]
        pipescope["ServiceProviderPipelineScope<br/>owned: owns a lifetime scope and disposes its IServiceScope<br/>borrowed: resolves from Services and disposes NOTHING"]
        lifescope["ServiceProviderLifetimeScope<br/>resolves the artefact cache from the scope in play,<br/>owned or borrowed alike"]
        cache["ScopedArtefactCache — NEW, TryAddScoped<br/>artefact identity belongs to the DI scope, not the handle"]
        diag["AmbientScopeDiagnostics — NEW<br/>container-scoped singleton, three independent latches"]
        facs["the five container-backed factories<br/>run the protocol inside CreatePipelineScope()"]
        facs --> policy
        facs --> probe
        facs --> pipescope
        facs --> diag
        pipescope --> lifescope
        lifescope --> cache
    end

    subgraph aspnet["Paramore.Brighter.Extensions.AspNetCore — new package, ADR 0073"]
        asp["an IAmAScopeProvider over IHttpContextAccessor,<br/>offering HttpContext.RequestServices"]
    end

    facs -- "GetAmbient(affinity)" --> provider
    facs -- "throws on a provider fault" --> fault
    asp -. "implements" .-> provider
    asp -. "its ambient implements" .-> role
    role -. "extends" .-> handle
    facs -. "type-tests for" .-> role
```

### Key Components

Three types implement or extend the pipeline scope handle, and telling them apart is what the rest of this section turns on. The sequence diagram above shows which object calls which, and the roles table below names each one's collaborators; this diagram shows only what implements what.

```mermaid
classDiagram
    class IAmAScope {
        <<interface, core, ADR 0070>>
        +Dispose()
        +DisposeAsync()
    }
    class IAmAScopeProvider {
        <<interface, core, NEW>>
        +GetAmbient(ScopeAffinity) IAmAScope
    }
    class IAmAServiceProviderScope {
        <<interface, DI package, NEW>>
        +Services IServiceProvider
    }
    class ServiceProviderPipelineScope {
        <<class, DI package, internal ctor>>
        owned or borrowed
    }
    class TheHostsAmbientScope {
        <<class, whichever package supplies it>>
        the caller owns it
    }

    IAmAServiceProviderScope --|> IAmAScope : extends
    ServiceProviderPipelineScope ..|> IAmAScope : Brighter builds this one
    TheHostsAmbientScope ..|> IAmAServiceProviderScope : a provider offers this one
    IAmAScopeProvider ..> IAmAScope : offers one, or nothing
```

A factory type-tests an offered `IAmAScope` for `IAmAServiceProviderScope`. An ambient that implements only `IAmAScope` cannot name a resolution source, which is why the role is a separate interface and why the test is a test rather than a cast.

#### The roles, and what each is responsible for

| Role | Type | Responsibilities | Responsibility classifier | Collaborators |
| --- | --- | --- | --- | --- |
| Ambient source | `IAmAScopeProvider` (core) | Answers, for one pipeline carrying one affinity, whether there is an ambient it may adopt. Creates nothing, owns nothing, disposes nothing | **deciding** | the five container-backed factories, which are its only callers; the `IAmAScope` it offers |
| Pipeline scope handle | `IAmAScope` (core, ADR 0070) | Is the scope a pipeline resolves from. Says nothing about where it came from or who owns it | **knowing** | the pipeline that holds it; `ServiceProviderPipelineScope`, which implements it |
| Resolution source | `IAmAServiceProviderScope` (DI package) | Names the `IServiceProvider` behind an ambient, so a Microsoft-container-backed factory can resolve from it | **knowing** | `AmbientScopeProbe`; the borrowed `ServiceProviderPipelineScope` built over it |
| Affinity policy | `ScopeAffinityPolicy` (DI package, internal) | Knows every participant's configured lifetime. Applies FR-27.2 to a participating set and yields the pipeline's `ScopeAffinity` | **knowing**, **deciding** | `IBrighterOptions`, which supplies the lifetimes; the five factories, each of which holds one |
| Usability probe | `AmbientScopeProbe` (DI package, internal static) | Answers, for one offered ambient, whether this container package may resolve from it — usable, and not the container's own root provider. It is the single implementation the five factories share | **deciding** | `IAmAServiceProviderScope`; `IServiceScopeFactory` and `ScopedArtefactCache`, which are what it resolves; the root `IServiceProvider` the calling factory holds, which is what it compares against |
| Per-scope artefact cache | `ScopedArtefactCache` (DI package) | Holds the `Scoped` artefacts one DI scope has produced, keyed by type. Creates one on first ask, through the factory it is given | **knowing**, **doing** | `ServiceProviderLifetimeScope`, which resolves it; the DI scope that owns and releases it |
| Diagnostics latch | `AmbientScopeDiagnostics` (DI package) | Knows which (condition, provider implementation type) pairs have already fired. Emits each of the three ambient `Warning` conditions at most once per pair per Brighter container | **knowing**, **doing** | the five factories; `ILogger` |
| Scope adopter | the five container-backed factories | Inside `CreatePipelineScope()`: compute, ask, decline or borrow, create | **deciding**, **doing** | `ScopeAffinityPolicy`, `IAmAScopeProvider`, `AmbientScopeProbe`, `AmbientScopeDiagnostics`, `ServiceProviderPipelineScope` |

#### `IAmAScopeProvider` and `ScopeAffinity` — the core half of the seam (new, core, public)

```csharp
namespace Paramore.Brighter
{
    /// <summary>
    /// An ambient source. Answers whether there is a DI scope the calling application already owns
    /// that this pipeline may resolve from. It supplies no scope of its own: whatever it does not
    /// offer, the container package creates and owns.
    /// </summary>
    public interface IAmAScopeProvider
    {
        IAmAScope? GetAmbient(ScopeAffinity affinity);
    }

    public enum ScopeAffinity
    {
        AlwaysNew = 0,
        JoinAmbient
    }
}
```

Both type names are settled by D4. The *member* spelling `GetAmbient` was a working name under C-11; ADR 0073 keeps it, and its contract — one argument, the asking pipeline's affinity; one return, an ambient or nothing — was fixed by D17 and never open. `AlwaysNew` is `0` so that `default(ScopeAffinity)` is the safe value.

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `GetAmbient(ScopeAffinity)` | the affinity of the pipeline that is asking, computed by the caller over the whole participating set | an ambient the pipeline may adopt, or `null` | A throw reaches the caller of `Send`/`Publish`/`Post` **unwrapped**, by the mechanism below. Returning `null` is not an error; it is the ordinary answer where there is no ambient. Returning an ambient for an `AlwaysNew` ask violates this contract, and Brighter ignores it rather than trusting the provider |

The error column rests on three requirements.

- **FR-24.1 and AC-30** — a misconfigured container is a startup-class fault. It must not be degraded to "no ambient", nor folded into the `ConfigurationException` every other build failure becomes. AC-30 pins that rule on a `Send` and a `Post`; step 1b says what those reach and what they do not.
- **FR-24.2** — an absent ambient is the ordinary case, not a fault.
- **FR-24.4** — the guard on Brighter's side is what makes FR-8 hold. Both the obligation and the guard are required, and both are stated here. The provider must neither consult nor adopt on an `AlwaysNew` ask, and Brighter ignores anything returned for one anyway. `IAmAScopeProvider` is a public extension point, so FR-8's per-subscriber isolation must not be defeasible by a third-party implementation.

**The ask is made even when the affinity is `AlwaysNew`** (D16). It is what makes a pipeline's adoption decision observable at all: without it, FR-27.2's decline-to-adopt rule has no observable, and AC-13 and AC-46 — which assert *exact* counts of adoption decisions and the affinity each carried — are unimplementable. The cost is one virtual call per pipeline that takes a pipeline scope. NFR-6 does not bless it and is not cited for it: NFR-6 budgets DI scopes, and an `AlwaysNew` ask allocates none. The ask is justified on observability alone.

#### `IAmAServiceProviderScope` — the hand-off (new, DI package, public)

```csharp
namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// An <see cref="IAmAScope"/> that names the <see cref="IServiceProvider"/> behind it, so a
    /// container-backed Brighter factory can resolve a pipeline's artefacts and their dependencies
    /// from it. Implement this on an ambient scope offered by an <see cref="IAmAScopeProvider"/>.
    /// The implementer owns the underlying scope; Brighter never disposes it.
    /// </summary>
    public interface IAmAServiceProviderScope : IAmAScope
    {
        IServiceProvider Services { get; }
    }
}
```

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `Services` | none | the provider a pipeline adopting this ambient resolves from — for the ASP.NET provider, `HttpContext.RequestServices` | Must not throw, must not be `null`, and must not be the container's **root** provider: an ambient is a DI scope the caller owns, and the root is not one (FR-23). It **may** name a provider whose scope has already been disposed; that is FR-23's other case and Brighter probes for it before resolving anything. Brighter never disposes the returned provider, nor the `IAmAServiceProviderScope` itself (FR-12, C-7) |
| `Dispose()` / `DisposeAsync()` (from `IAmAScope`) | — | — | Brighter never calls either on an ambient, adopted or declined. They exist only because `IAmAScope` carries them |

Four properties of this shape are load-bearing.

**It is a role interface, not a base class and not a concrete type.** Any assembly can implement it without inheriting anything, which is what AC-35's non-ASP.NET, non-Microsoft-container-package provider needs and what NFR-7 requires generally.

**It lives in the DI package, not core.** It names `IServiceProvider`, so core is the one place it cannot go — and it is meaningful only to a package that resolves from Microsoft's container. A package built over Autofac or SimpleInjector declares its own equivalent role over its own container's resolution source, and nothing in core has to change for it.

**The four container-backed transform factories and the handler factory type-test for the interface**, never for a class: `if (ambient is IAmAServiceProviderScope src)`. `ServiceProviderPipelineScope`'s borrowed construction path therefore stays internal to the DI package. The class itself is `public` — the DI package's convention, which ADR 0070's *Technology Choices* states with the count — but its constructor is `internal`, so no third party ever builds one and none is handed one. The seam binds an implementer to a contract rather than to Brighter's implementation.

**An ambient that does not implement the role is ignored, not rejected.** The subject here is an ambient a registered `IAmAScopeProvider` *offered*, and the question is what Microsoft-backed factories do with one that carries another container package's role type. That is a configuration the seam must survive rather than diagnose by throwing: the factory declines the ambient and takes the *created* path, which is FR-11(b), FR-13 and C-7's third case. Nothing is thrown, and the declined `IAmAScope` is not disposed. (ADR 0071 answers a different question about a different object — what a handler factory does with an `IAmALifetime.PipelineScope` handle it does not recognise — and its answer is to reject. The two are not the same rule and do not read alike.)

The decline is reported under FR-23's condition, *ambient offered but unusable*, since that is exactly what a foreign-role ambient is from this container package's side. Reporting it there reuses a specified, latched diagnostic instead of inventing a fourth, and it is an extension of FR-23's diagnostic beyond that requirement's literal text:

- FR-23 is written about a *stale* resolution source, and AC-29 exercises a capturing provider, so neither reaches an ambient of a foreign role type.
- No acceptance criterion guards this ladder row.
- The extension is taken on the same reasoning ADR 0070 uses to extend NFR-1(b) to the two mapper registries, and the gap is recorded in *Negative* rather than left to be discovered.

**On the name.** `IAmAServiceProviderScope` is this ADR's to choose — it is not one of C-11's three working names — and it is chosen because it states both halves of what the type is, an `IAmAScope` whose resolution source is an `IServiceProvider`. It also carries the `ServiceProvider*` prefix every other Microsoft-container type in the package already wears (`ServiceProviderLifetimeScope`, `ServiceProviderHandlerFactory`, `ServiceProviderMapperFactory`).

#### `ScopeAffinityPolicy` — who computes the affinity (new, DI package, internal)

A pipeline's affinity is a property of every factory that participates in it, but only one of those factories is asked to create the scope. The policy is the object that holds the rule for the set, so that the asked factory can apply it without knowing which of the five it is.

FR-27.2 makes the affinity a property of the whole participating set, ADR 0070's protocol calls `CreatePipelineScope()` on one factory, and D16 requires exactly one ask per pipeline. The factory that creates the pipeline scope must therefore know every participant's configured lifetime. It can: all five container-backed factories already read `IBrighterOptions` in their constructors, and `IBrighterOptions` (`BrighterOptions.cs:72`) carries all three of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime`. The five constructor reads are `ServiceProviderMapperFactory.cs:44-45`, `ServiceProviderMapperFactoryAsync.cs:45-46`, `ServiceProviderTransformerFactory.cs:44-45`, `ServiceProviderTransformerFactoryAsync.cs:45-46` and `ServiceProviderHandlerFactory.cs:49-50`. Today each factory keeps only its own lifetime; from here each keeps the policy instead.

```csharp
internal sealed class ScopeAffinityPolicy
{
    public ScopeAffinityPolicy(IBrighterOptions? options);

    public ScopeAffinity ForHandlerPipeline();     // participants: { HandlerLifetime }
    public ScopeAffinity ForTransformPipeline();   // participants: { MapperLifetime, TransformerLifetime }
}
```

One object holds FR-27.2's rule so that five factories do not each re-derive it. There are two members rather than one general one because D12 fixes exactly two participating sets and there are only two.

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| ctor | the resolved `IBrighterOptions`, or `null` where none is registered | — | Never throws. The policy answers `AlwaysNew` **unconditionally** when the options object is `null`, so every pipeline creates its own scope — the same degradation every other failure path takes |
| `ForHandlerPipeline()` | none; reads `{ HandlerLifetime }` and the affinity option | `JoinAmbient` when the affinity option is `JoinAmbient` and `HandlerLifetime` is `Scoped`; `AlwaysNew` otherwise | Never throws. **Tests for `JoinAmbient` positively**, so any value outside the enum degrades to `AlwaysNew` |
| `ForTransformPipeline()` | none; reads `{ MapperLifetime, TransformerLifetime }` and the affinity option | `JoinAmbient` when the affinity option is `JoinAmbient`, at least one of the two is `Scoped`, and neither is `Transient`; `AlwaysNew` otherwise. `Singleton` participants are ignored (FR-27.2) | as above |

The null-options rule is stated as its own rule rather than derived from the property defaults, because the factories' existing `null` fallbacks are not those defaults. `ServiceProviderMapperFactory.cs:45` and its three transform-family siblings fall back to `ServiceLifetime.Singleton`, and only `ServiceProviderHandlerFactory.cs:50` falls back to `Transient`. Those fallbacks are unchanged by this ADR and are not what this rule reads.

Both members are pure functions of state fixed at container build, so they are safe to call concurrently and hold nothing. A factory may keep one policy instance for its life and call it once per pipeline from any thread.

**Positive testing for `JoinAmbient` is a contract, not an implementation detail.** ADR 0076 relies on it: `ScopeAffinity` is a plain non-nullable enum on a public options interface, so an application can assign a cast integer that is neither member, and the safe degradation is *do not adopt*. Every reader of a `ScopeAffinity` in this design — the policy here, and the affinity guard on the provider's answer — tests for `JoinAmbient` positively rather than testing for `AlwaysNew` and treating everything else as adoption. `AlwaysNew = 0` makes `default(ScopeAffinity)` safe for the same reason.

#### `AmbientScopeSourceException` — the courier (new, core, public)

The one type in this ADR an implementer outside Brighter is *obliged* to construct. It carries a provider's own exception out of `GetAmbient` without letting it be mistaken for an ordinary build failure.

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `AmbientScopeSourceException(Exception inner)` | the exception the provider threw | an instance whose `InnerException` is **never `null`** | `ArgumentNullException` on a `null` `inner`. The constructor validates, because that is the only thing that makes the never-null invariant a guarantee rather than an assertion |

**The never-null invariant is load-bearing**, not incidental: it is what licenses `e.InnerException!` at the six sites that unwrap it. A provider that constructs one without an inner exception would break those call sites rather than merely being untidy, which is why the guard is structural. Three facts make a `null` argument reachable rather than hypothetical:

- The type is `public` in `Paramore.Brighter`, which targets `netstandard2.0`, so a nullable-oblivious consumer reaches the constructor with no compiler diagnostic.
- The caller is not a single trusted one. Any factory that asks a provider for an ambient must wrap a throw from that ask in this type, including a factory in a third-party container package (NFR-7).
- The alternative to throwing at the construction site is a `NullReferenceException` from inside a pipeline builder's rethrow.

The discrimination in the builders' `catch` filters is by this type and nothing else. *Technology Choices* says why a bespoke type rather than a reused one.

#### `ScopedArtefactCache` — artefact identity under a borrowed scope (new, DI package, public)

Under `JoinAmbient` the borrowed DI scope owns the artefact, not merely its dependencies (D7, FR-16(a)): two `Post`s in one request share one mapper. ADR 0070 gives artefact identity per pipeline, by way of `ServiceProviderLifetimeScope`'s per-type `_scopedInstances` cache (`:163-178`) riding on the handle, and says in terms that this is sufficient for the owned case and insufficient for adoption. Supplying what adoption needs is this ADR's.

Per-pipeline is not enough because a borrowed `ServiceProviderPipelineScope` is constructed per pipeline too: each `Post` calls `CreatePipelineScope()` and gets its own handle over the same `HttpContext.RequestServices`. A cache that is a private field of the handle (`ServiceProviderLifetimeScope.cs:49`) would therefore give per-pipeline artefact identity and two mappers, falsifying FR-16(a) and AC-17.

**So the cache moves off the handle and into the DI scope.** `ScopedArtefactCache` is registered `TryAddScoped` by the DI package and holds the per-type artefact dictionary; `ServiceProviderLifetimeScope`'s `Scoped` path resolves it from the scope in play rather than owning a field:

- **borrowed** — resolved from `src.Services`, so one instance per request scope, shared by every pipeline in that request, released by the container when the request scope ends;
- **owned** — resolved from the `IServiceScope` `EnsureRootScopePublished()` (`:185`) just created, so one instance per pipeline, exactly today's behaviour and exactly what FR-1, FR-2 and AC-1 require.

One mechanism, both cases. It is FR-26's recommended mechanism, and it is what makes FR-26 hold with no weak references and no eviction logic: the container owns the association's lifetime and disposes it with the scope. `TryAddScoped` rather than `AddScoped` keeps the registration idempotent. `BrighterHandlerBuilder` runs once per registration entry point, so it runs twice in a host that calls both `AddBrighter` and `AddConsumers`, and every other registration in that method is a `TryAdd` too. The cache disposes nothing it holds — MS DI already tracks disposable transient resolutions against the scope that created them, which is what AC-17 asserts — so its `Dispose` only drops references and decrements AC-37 clause 3's counter.

**Where a borrowed provider cannot supply a `ScopedArtefactCache`, the handle declines the whole borrow at the probe.** An ambient from a container Brighter did not register into is such a provider, and step 2a's probe carries the test that makes the decline deliverable: it asks the offered provider for `ScopedArtefactCache`, and a `null` answer is a failed probe. What is declined is the whole borrow, so the cost is not confined to artefact identity — the pipeline creates and owns a scope, and dependency sharing, the headline of adoption, goes with it. That is a trade rather than a free outcome, and it is stated as one. The alternative is a borrowed scope that shares dependencies while quietly reverting artefact identity to per pipeline, which is *Alternatives Considered* 7 and is rejected there for falsifying FR-16(a) and AC-17: two `Post`s in one request would resolve two mappers. There is no private fallback cache, because `ServiceProviderLifetimeScope.cs:49`'s `_scopedInstances` field *becomes* a resolution of this service, as step 3a says. That keeps one statement of where the cache lives, and puts the decline at ladder row 9, where the other two decline points already are (C-7).

**Contract.** The move from a per-pipeline field to a request-`Scoped` service turns a cache one pipeline owned into one every concurrent pipeline in a request contends for, which is squarely inside NFR-4. It is answered by inheriting today's protocol verbatim rather than inventing one:

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `GetOrAdd(Type, Func<object?>)` | the artefact type, and a factory that resolves one | the single instance of that type held by this cache | Concurrency is `ConcurrentDictionary<Type, Lazy<object?>>` with the `Lazy` publish protocol `ServiceProviderLifetimeScope.cs:163-178` already uses: concurrent first-resolvers of one type produce **one** instance and the losers see the winner's, and a resolution that throws propagates to every waiter. A faulted resolution is not retained: the entry is removed and the exception propagates. A losing waiter's removal is a no-op, which is the required behaviour (NFR-4), so a later resolution of the same type in the same scope resolves again rather than rethrowing a remembered failure. The owned and borrowed paths are identical here — one protocol, not two |
| `Dispose()` | — | — | Drops its references and nothing else. It disposes no artefact it holds: MS DI already tracks disposable transient resolutions against the scope that created them, which is what AC-17 asserts. A `Dispose` racing a `GetOrAdd` is the container disposing a borrowed scope while a pipeline is still resolving from it. It is not the same error it is today: under `AlwaysNew` the scope is Brighter's own and no caller can dispose it mid-pipeline, so adoption is what creates the window. Nor is it left as caller error — step 4 has the borrowed resolution path translate the resulting `ObjectDisposedException` into a `ConfigurationException` naming the cause, which is what keeps FR-23 true for the whole life of a pipeline |

**How a faulted entry is removed, and why the form is not free.** The removal must take the observed pair, not the key:

```csharp
((ICollection<KeyValuePair<Type, Lazy<object?>>>)_cache)
    .Remove(new KeyValuePair<Type, Lazy<object?>>(type, observedLazy));
```

Three constraints fix that line.

- **Not `TryRemove(type, out _)`.** Under `ExecutionAndPublication` every thread that awaited the faulting `Lazy` observes the exception and every one of them attempts the eviction, so a key-only removal can delete a *healthy* `Lazy` that a concurrent resolver published in between. Under `JoinAmbient` that yields two `Scoped` artefacts in one borrowed request scope, which is exactly what FR-16(a) and AC-17 forbid.
- **The explicit interface implementation, not `ConcurrentDictionary`'s own `TryRemove(KeyValuePair<Type, Lazy<object?>>)`.** That overload arrived in .NET Core 2.0 and is absent from `netstandard2.0`, one of the DI package's four targets (`src/Directory.Build.props:43`). Writing it gives a compile error on that target, and no package can backfill an overload on a platform type. The `ICollection<T>.Remove` cast has been present since .NET Framework 4.0, compiles on all four targets with no `#if`, and matches pairs identically. The same file already documents an equivalent gap at `ServiceProviderLifetimeScope.cs:507-508`.
- **Matching is by reference.** The value comparison is `EqualityComparer<Lazy<object?>>.Default` and `Lazy<T>` does not override `Equals`, so only the exact `Lazy` instance the caller observed is evicted. That is the whole of what makes the pair form safe.

**The one place the protocol is not inherited, and why.** `Lazy`'s default `LazyThreadSafetyMode.ExecutionAndPublication` caches the fault: a `GetService` that throws is remembered, and every later request for that type rethrows it. Today that is confined to one pipeline. Moving the cache into the scope is what makes it unacceptable — under `JoinAmbient` the fault would live as long as the request, so one transient resolution failure would poison that artefact type for every remaining pipeline in it. The widening is this ADR's doing, so the fix is this ADR's obligation rather than an adjacent one: `GetOrAdd` evicts a faulted entry instead of publishing it, on both the owned and the borrowed `Scoped` path. That is issue **#4260**'s fix for the `Scoped` cache, and the `Singleton` cache is out of scope — step 3a says why. Fixing the owned path here rather than only the borrowed one is deliberate: evicting on fault *only* where the scope is borrowed splits one protocol across two paths for half a fix, and leaves the owned path with the behaviour that was tolerable only because its cache was short-lived.

#### `AmbientScopeDiagnostics` — the three latches (new, DI package, container-scoped singleton)

Three rules require a latched `Warning` naming a provider implementation type, and they are three distinct diagnostics, latched independently:

| Condition | Rule | When |
| --- | --- | --- |
| *no ambient offered* | FR-24.2 | a `JoinAmbient` ask returned nothing. Includes FR-18's ordinary case — an opted-in host with no current `HttpContext`. Never fires on an `AlwaysNew` ask |
| *ambient offered but unusable* | FR-23 | a `JoinAmbient` ask returned an ambient that does not implement this package's hand-off role, or one that failed the usability probe |
| *ambient offered for an `AlwaysNew` ask and ignored* | FR-24.4 | any ask carrying `AlwaysNew` returned something |

Each is latched once per **(condition, provider implementation type)**, and the latch belongs to an instance registered `TryAddSingleton` on the Brighter container — the host's root provider — rather than to a `static` (D19). A process-static latch makes AC-31's `AlwaysNew` branch vacuous and AC-11's third branch unsatisfiable by a correct implementation, both of which reuse one provider implementation type across branches in separate hosts. Each message names its condition in terms a capturing `ILoggerProvider` can discriminate on; naming only the provider type is insufficient, because all three do that.

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `WarnOnce(condition, providerType)` | one of the three conditions above, and the **implementation** type of the provider that was asked. There is no key for "no provider is registered", and none is needed: that case makes no ask and emits no diagnostic (ladder row 3, FR-11(a)) | the message is logged at `Warning` on the first call for that pair and on no later one | Atomic per (condition, provider implementation type) — a single `ConcurrentDictionary<(Condition, Type), byte>.TryAdd`, whose return value decides whether to log. Never throws; a logging failure is the logger's |

**The latch has to be atomic rather than check-then-set.** AC-11 asserts exact warning counts, and a `Publish` runs its subscribers concurrently on both twins — `Parallel.ForEach` (`CommandProcessor.cs:481`) on the sync path and `await Task.WhenAll(tasks)` (`:601`) on the async one, which is the twin AC-11 is written over. Three subscribers hitting the same condition on a check-then-set latch could log two or three times.

**Only one ordering constraint is real, and it is FR-24's exclusivity rule.** FR-24.4 is evaluated first, because an ambient returned for an `AlwaysNew` ask is ignored *before* it is probed. A stale ambient returned for such an ask is therefore reported under FR-24.4 and never under FR-23. FR-23 and FR-24.2 are mutually exclusive. One is "an ambient came back and cannot be used", the other is "nothing came back", so their relative order is immaterial; the ladder and the pseudo-code test *nothing came back* first only because it is the cheaper test. The requirements do not say the order is immaterial. They fix FR-24.4, then FR-23, then FR-24.2, and record that the overlap between the last two is real. The two are reconciled rather than in conflict: this ladder's *nothing came back* is strictly narrower than FR-23's *treat a failed probe exactly as "no ambient"*, so separating those rows yields the same outcomes the requirements' order would.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| `Paramore.Brighter` | `IAmAScopeProvider`, `ScopeAffinity` | **new** |
| `Paramore.Brighter` | `AmbientScopeSourceException` | **new** — carries a provider fault past the pipeline builders' wrapping `catch` |
| `Paramore.Brighter` | `PipelineBuilder<TRequest>` | an `AmbientScopeSourceException` clause ahead of the wrapping `catch` in both build paths (`:202-205`, `:248-251`) |
| `Paramore.Brighter` | `TransformPipelineBuilder`, `TransformPipelineBuilderAsync` | an `AmbientScopeSourceException` clause ahead of each wrapping `catch` — `:116-125` and `:157-166`, at identical lines in both files, quoted catch-line through closing brace as ADR 0070 quotes them — so cleanup runs and then the original is rethrown |
| `…DependencyInjection` | `IAmAServiceProviderScope`, `ScopeAffinityPolicy`, `ScopedArtefactCache`, `AmbientScopeDiagnostics` | **new** |
| `…DependencyInjection` | `AmbientScopeProbe` | **new** — internal static, one member `CanResolveFrom(IAmAServiceProviderScope, IServiceProvider)`, whose second argument is the root provider the calling factory already holds; the ladder's usability test, shared by all five factories |
| `…DependencyInjection` | `ServiceProviderPipelineScope` | an **internal** borrowed construction path with non-owning disposal, and on that path one translation: an `ObjectDisposedException` from an ambient disposed after the probe is rethrown as `ConfigurationException` (step 4) |
| `…DependencyInjection` | `ServiceProviderLifetimeScope` | an internal borrowed mode (resolve from a given provider; create and dispose nothing); the `Scoped` path resolves its artefact cache from the scope in play rather than owning `_scopedInstances` (`:49`), and a faulted resolution is evicted, not published (#4260's `Scoped` half, step 3a). `GetOrCreateSingleton` (`:152`) and its `_singletonInstances` cache are **not** touched |
| `…DependencyInjection` | `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`, `ServiceProviderTransformerFactoryAsync`, `ServiceProviderHandlerFactory` | keep a `ScopeAffinityPolicy`, the resolved `IAmAScopeProvider`, the diagnostics singleton and the root `IServiceProvider` the probe compares against — which only `ServiceProviderHandlerFactory` keeps today (`:36`), the other four holding a `ServiceProviderLifetimeScope` built over it and no field of their own; `CreatePipelineScope()` runs the protocol below. The diagnostics singleton is held **nullable**, so a factory constructed by hand over a provider that never ran `AddBrighter` makes `WarnOnce` a no-op rather than a null dereference — the same degradation `ScopeAffinityPolicy` takes for a null `IBrighterOptions`. The protocol includes one read of core's `AmbientScopeSuppression.IsSuppressed` at the affinity computation; the flag, all three brackets, the reasoning **and this edit** are ADR 0075's. The line appears at this ADR's step 3 to show where in the protocol it sits, and it arrives with ADR 0075's commit: it would not compile in this one, because the type it reads does not exist until 0075 declares it |
| `…DependencyInjection` | `ServiceCollectionExtensions.BrighterHandlerBuilder` (`:142`, reached from `:119`) | registers `ScopedArtefactCache` (`TryAddScoped`) and `AmbientScopeDiagnostics` (`TryAddSingleton`) |
| `Paramore.Brighter.Extensions.AspNetCore` | the provider | **new package**, kept under that name by ADR 0073; its ambient implements `IAmAServiceProviderScope` over `HttpContext.RequestServices` |

**Unchanged, and listed deliberately rather than left out.**

- `CommandProcessor`, whose dispatch methods gain nothing here.
- `IAmAScope`, and every interface ADRs 0070 and 0071 changed: no member is added to any of them here. `CreatePipelineScope()`'s **contract** is widened, though its signature is not. The handle it returns may now name a borrowed ambient, so the member promises that the caller must always *release*, not that it *owns*. Only the handle knows whether releasing disposes anything (FR-12).
- `MessageMapperRegistry`, whose two forwarding members behave exactly as ADR 0070 specifies.
- `IAmALifetime` and `HandlerLifetimeScope`.
- `PipelineBuilder.Dispose()` (`:269-270`), so D10's release timing is preserved by construction.
- `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder` and `ConsumerFactory` (C-2), and the pump's per-message behaviour (D0b).
- `BrighterOptions`' three lifetime properties and `IsolateTransientHandlerScope` (`:37`).
- `RequestContext`.

### Technology Choices

#### Why a provider fault needs a type of its own to escape

FR-24.1 asks for something the surrounding code actively prevents. The ask is made inside `CreatePipelineScope()`, and every call site of that member sits inside a pipeline builder's guarded region: `PipelineBuilder.cs:190` and `:235` are inside the `try` whose `catch` at `:202` and `:248` turns everything that is not already a `ConfigurationException` into one, and `TransformPipelineBuilder.cs:116` and `:157` do the same without even a filter. A provider's `InvalidOperationException` would therefore reach the caller as a `ConfigurationException`, which is precisely the degradation FR-24.1 forbids and AC-30 falsifies.

Three ways out were available, and the difference between them is where the fault is allowed to escape.

- **Move the ask outside the guarded region.** This is the obvious one and the worst. On the handler path the ask is per subscriber, inside a loop inside the `try` (`PipelineBuilder.cs:187` sync, `:232` async). Hoisting it leaves two options, and both are worse. Ask once for a set of subscribers whose lifetimes are independent, which loses D16's one-ask-per-pipeline meaning. Or restructure the loop so each iteration has its own guarded region, which changes the dispatch path this specification is otherwise careful not to touch (C-2's neighbourhood, and D10's release timing rides on that loop). The obstacle is the loop structure, not a cleanup: `PipelineBuilder`'s catches run none.
- **Let the fault reach the caller as a typed Brighter exception.** Defensible, but it changes what AC-30 asserts.
- **Give the ask its own exception type**, and teach the wrapping `catch` in each of the three builders' two build paths — six in all — to recognise it. Whatever cleanup that catch already ran still runs, and then the original exception is rethrown with `ExceptionDispatchInfo.Capture(...).Throw()`, stack intact. Step 1b says what that is in each family. The caller sees what the provider threw, nothing is leaked, and no dispatch method changes.

#### The type is a courier to an application and a contract to an implementer

An application never observes it: it exists between the ask and the builder that catches it, and what reaches a caller is always the provider's own exception, unwrapped. But it is `public` in `Paramore.Brighter` and it has to be, because a container package Brighter does not ship implements `CreatePipelineScope()` itself, and NFR-7 makes that a first-class case rather than a hypothetical one. Any `IAmAScope`-producing factory that asks an `IAmAScopeProvider` must wrap a throw from that ask in this type, or FR-24.1 silently fails for that package: the provider's fault is folded into `ConfigurationException` like any other build failure, and the "misconfigured container is a startup-class fault" rule does not hold.

Its contract is one line and is guaranteed rather than incidental: the constructor takes the provider's exception, and `InnerException` is never `null`, which is what licenses the `e.InnerException!` in the builders' rethrow. *Guaranteed* is meant literally — the constructor throws `ArgumentNullException` instead of trusting its callers, precisely because under NFR-7 they are not all Brighter's.

Discriminating by type rather than by position is what lets `CreatePipelineScope()` carry two error behaviours at once: the ambient ask propagates, while a failure to *create* a container scope stays an ordinary build failure and becomes the `ConfigurationException` AC-5 requires. It is also why the scope acquisition can sit inside the builder's `try` where AC-5 needs it, which is what ADR 0070's implementation sketch does.

#### Why the affinity is computed on Brighter's side rather than asked of the provider

The provider does not know the configured lifetimes and must not have to. It answers one question — is there an ambient here — and the pipeline tells it the affinity it is asking with. That keeps `IAmAScopeProvider` implementable in an assembly that references no container package at all, which is precisely what AC-13's fake is.

#### Why `IAmAScope` stays empty

Making the hand-off a *derived* role, rather than a member on `IAmAScope`, keeps ADR 0070's promise that a core handle knows nothing about resolution. It keeps the ASP.NET package free of any obligation the DI package does not also impose. And it means the same `IAmAScope` type serves an owned Brighter scope, a borrowed ambient and an ambient Brighter declined, with no capability flag.

#### Artefact identity, restated for both affinities

Dependency identity always follows the DI scope. Artefact identity follows the pipeline under `AlwaysNew` and the borrowed DI scope under `JoinAmbient`; `Singleton` sits outside both, resolving from the root provider. That is what `ScopedArtefactCache` implements, and it is the sentence FR-25's guidance page has to carry.

### Implementation Approach

#### 1. The core types

Add `IAmAScopeProvider` and `ScopeAffinity` to `src/Paramore.Brighter/`, and `AmbientScopeSourceException` beside them. That confirms the home C-8 assumes for those two seam types, ADR 0070 having confirmed `IAmAScope`'s. None names a container type; the source-level guard AC-22.3 runs returns nothing new.

#### 1a. Structural, and separate: one spelling for the two `PipelineBuilder` catch filters

`:248` reads `when(!(e is ConfigurationException))` where `:202` reads `when (e is not ConfigurationException)`. Normalising them changes no behaviour and belongs in its own commit ahead of the behavioural change, per Tidy First. Doing it first also means the clause added below is added twice to the same shape.

#### 1b. The six builder `catch` blocks learn one clause

Ahead of each existing wrapping `catch` — `PipelineBuilder.cs:202` and `:248`, `TransformPipelineBuilder.cs:116` and `:157`, and the same two lines in `TransformPipelineBuilderAsync` — add a clause for `AmbientScopeSourceException` that rethrows the inner exception through `ExceptionDispatchInfo.Capture(...).Throw()`.

What the clause does *before* rethrowing differs between the two families, because their general clauses differ. In the four transform-builder catches it calls the same cleanup the general clause calls, ADR 0070's `CleanUpQuietly`, which is why 0070 lifts it to a named method. In `PipelineBuilder`'s two catches it calls nothing, because those catches run no cleanup: `:202-205` and `:248-251` do nothing but wrap in `ConfigurationException`. What discharges AC-30's "no pipeline scope is leaked" on that path is `PipelineBuilder.Dispose()` (`:269-270`) firing from the `using var builder` at each of `CommandProcessor`'s four dispatch sites; it runs whether or not the build threw, and this ADR does not disturb it. Nothing else in the builders changes, the general clause's behaviour for every other failure is untouched (AC-5), and both `PipelineBuilder` filters now also exclude the new type.

**What AC-30 reaches.** FR-24.1 states the rule over `Send`/`Publish`/`Post`, and AC-30's two branches are a `Send` and a `Post`, so of the six catches above it exercises two. The `Send` builds a handler pipeline and lands on `PipelineBuilder.cs:202`, the family whose catches run no cleanup — so what discharges that branch's second conjunct, *no pipeline scope is leaked*, is `PipelineBuilder.Dispose()`, not the added clause. The `Post` builds a transform pipeline, and that is the branch where the added clause does the most: it is what exercises the `CleanUpQuietly` half. `Publish` and the async twins of both families remain uncovered. The clause is textually identical at all six sites, and each family's behaviour is stated once above. Identical code is not a criterion, and this ADR does not offer it as one — the same declination step 4 makes for AC-8.

#### 2. The `CreatePipelineScope()` protocol

This is the decision ladder under *The mechanism, end to end*, written out as the code runs it. One protocol, run inside every container-backed factory's `CreatePipelineScope()`. Adoption is one change, not two: after ADR 0071 both families reach their scope through this member, so a borrowed ambient is simply what it returns. There is no second path for handlers.

Step 1 is a test on the asked factory alone, and D16's *exactly one ask per pipeline* is delivered per family rather than by anything here. For a transform pipeline it is ADR 0070's first-non-null routing: the mapper registry is asked first and the transformer factory only if the registry offered nothing, so at most one participant reaches step 3. For a handler pipeline there is no routing to rely on and none is needed, because one factory is called once per pipeline (ADR 0071), so at most one ask is structurally guaranteed.

Under `{Transient mapper, Scoped transformer}` the registry returns `null` at step 1 without asking, and the transformer factory runs steps 3 onward — computing the affinity over the whole set, `{MapperLifetime = Transient, TransformerLifetime = Scoped}`. That set contains a `Transient`, so FR-27.2 yields `AlwaysNew` and the pipeline creates and owns its scope, even though the factory that was asked is itself `Scoped`. That is the case the policy exists for, and the reason step 3 cannot be a per-factory test.

```
CreatePipelineScope():

  1. if THIS factory's own configured lifetime has no scope to offer -> return null
        (mapper factory: MapperLifetime not Scoped.                     ADR 0070's routing
         transformer factory: TransformerLifetime not Scoped.           then asks the next
         handler factory: HandlerLifetime is Singleton.)                participant

  2. if Scoped does not participate in this pipeline                 -> return ADR 0067's
        (handler family only: HandlerLifetime == Transient)             per-resolution handle,
                                                                        make NO ask    [FR-27.1]

  3. affinity = AmbientScopeSuppression.IsSuppressed        [ADR 0075: type AND edit]
                  ? ScopeAffinity.AlwaysNew
                  : policy.ForHandlerPipeline() / ForTransformPipeline()   [FR-27.2]

  4. if (_scopeProvider is null) return OWNED           // ladder row 3: nothing to ask,
                                                       // and no diagnostic  [FR-11(a)]
  5. ambient = _scopeProvider.GetAmbient(affinity)     // exactly once  [D16, D17]
                 // a throw is wrapped in AmbientScopeSourceException here, and
                 // rethrown unwrapped by the builder's catch  [FR-24.1, AC-30]

  6. if (affinity != ScopeAffinity.JoinAmbient):     // positive test: anything that is
                                                     // not JoinAmbient does not adopt
        if ambient is not null -> diagnostics.WarnOnce(IgnoredForAlwaysNewAsk, providerType)
        -> OWNED                                                     [FR-24.4, AC-11]

     if ambient is null -> diagnostics.WarnOnce(NoAmbientOffered, providerType)
        -> OWNED                                                     [FR-24.2, FR-18]

     if ambient is not IAmAServiceProviderScope src
        -> diagnostics.WarnOnce(OfferedButUnusable, providerType); OWNED

     if not AmbientScopeProbe.CanResolveFrom(src, _serviceProvider)
        -> diagnostics.WarnOnce(OfferedButUnusable, providerType); OWNED   [FR-23]

     -> BORROWED over src.Services                                   [FR-12, C-7]
```

At no point is a declined ambient disposed (C-7).

`_scopeProvider` is resolved once, in the factory's constructor, from the root `IServiceProvider` it already receives. `_serviceProvider` is that same root provider, kept as a field so the probe can be given it: `ServiceProviderHandlerFactory` already holds it (`:36`), and the other four factories pass their constructor argument to a `ServiceProviderLifetimeScope` and keep no field of their own, so those four gain one.

#### 2a. `AmbientScopeProbe` — what the probe is

It is an internal static helper in the DI package with one member, `bool CanResolveFrom(IAmAServiceProviderScope, IServiceProvider root)`, so the five factories share one answer rather than five private copies of it. It reads `Services`, compares it with `root`, then resolves two services from it:

- `IServiceScopeFactory`, which every Microsoft-shaped provider supplies with no descriptor of its own;
- `ScopedArtefactCache`, which only a container `AddBrighter` registered into has a descriptor for.

**Five outcomes are a failed probe**: a `null` `Services`, a `null` `IServiceScopeFactory`, a `null` `ScopedArtefactCache`, a `Services` reference-equal to `root`, and any exception thrown by reading `Services` or by either resolution, `ObjectDisposedException` among them. The first two ask whether the provider is live and Microsoft-shaped. The third is what makes the `ScopedArtefactCache` section's decline deliverable rather than merely asserted. The fourth tests identity, not capability, and it is there because the root provider answers all three of the others: where the container was built without `ValidateScopes`, a `Scoped` service resolves from it and returns one process-wide instance disposed by nothing (probed), defeating FR-1 and FR-2. Where validation is on the cache resolution throws and the exception outcome already declines, so the reference test is what makes the outcome the same on the hosts where the borrow would otherwise be silent.

A failed probe declines at ladder row 9. It carries the same *ambient offered but unusable* diagnostic as the role-type decline at row 8, on the same extension of FR-23's text. Row 8 has no criterion at all; AC-54 exercises row 9 for one of its five outcomes. The probe allocates no scope and runs once per pipeline; resolving the cache constructs the container-`Scoped` instance the pipeline is about to use, so on the passing path it costs one object per borrowed scope and nothing per pipeline after the first. It keeps `ObjectDisposedException` from Brighter's own resolution off the caller's stack at the point of adoption; a source that is live here and disposed afterwards is a second window, and step 4 says what bounds that one (FR-23).

**Treating a `null` or throwing `Services` as a failed probe guards the contract table's obligation on that member rather than merely stating it.** The alternative was to let a violating provider produce a `NullReferenceException` from inside `CreatePipelineScope()`, which the pipeline builders' general `catch` would turn into `ConfigurationException` — the same degradation FR-24.1 forbids for a *throwing* provider, arriving by a different route. A provider that breaks its contract is declined under FR-23's *offered but unusable*, latched once, and the pipeline creates its own scope.

#### 2b. Which providers reach rows 8 and 9, and which cannot

A disposed-but-offered resolution source is reachable only from a provider that *captures* one rather than reading it afresh on every ask. Three kinds do: an `AsyncLocal`-backed provider for non-ASP.NET hosts (NFR-7, AC-35), a provider that stores `HttpContext.RequestServices` at construction, and a custom `IHttpContextAccessor` that does not clear itself at end of request.

ADR 0073's provider is none of these, though the opposite is the intuitive reading. ASP.NET Core's built-in `HttpContextAccessor` holds its `AsyncLocal` over a shared holder object and clears that holder when the request ends. A flow that outlives the response — deferred work whose request has already completed — therefore observes a null `HttpContext`, `GetAmbient` returns `null`, and the ask lands on row 7, *no ambient offered* (FR-24.2). It never reaches the probe.

That reasoning holds only for a flow that outlives the *response*, and a `Dispatcher` started from inside a live request is not one. While the request is still open, that flow sees a non-null `HttpContext` whose `RequestServices` is the live request scope, so nothing in the probe declines it and row 10 would fire. It is not this paragraph's case, and it is not reached at all: ADR 0075's pump-flow bracket suppresses that flow, so the ask carries `AlwaysNew` and never gets past row 6. So the probe is not dead code, but the case that justifies it belongs to providers Brighter does not ship, which is what makes it a seam obligation rather than an ASP.NET one.

**What the type test and the probe do not discriminate is the ambient's container.** The type test asks whether the offered `IAmAScope` implements this package's role; the probe asks whether the provider behind it is live and Microsoft-shaped. Neither asks which container built it. The probe *can* discriminate whether a container is one `AddBrighter` registered into — that is exactly what its `ScopedArtefactCache` test does — but that is a question about registrations, not about provenance. `System.IServiceProvider` is the interface every container's Microsoft-DI adapter exposes, so nothing here can name the container behind it, and nothing needs to.

An ASP.NET host running Autofac or SimpleInjector behind `AutofacServiceProviderFactory` therefore offers an `HttpContext.RequestServices` that passes both tests, the cache test included, and is borrowed from, on its own terms and correctly: Brighter's registrations went into the same `IServiceCollection` that container was populated from, which is both why the cache resolves and why borrowing is right. What the type test declines is an ambient from a package that declares its own role type over its own container's resolution source, as *Technology Choices* says such a package must; that ambient never implements `IAmAServiceProviderScope`, and declining it is the intended outcome.

#### 2c. What borrowing does to Brighter's own registrations

Two facts settle it.

- **Brighter registers no container-`Scoped` service of its own today; `ScopedArtefactCache` above is the first.** Every registration `AddBrighter` and `AddConsumers` make is `Singleton` or `Transient`, and the lifetimes `AddProducers` and `UsePublicationFinder` take as an argument default to `Transient` (`ServiceCollectionExtensions.cs:250`, `:386`, `:513`). A `Singleton` is built in the root scope whatever scope asked for it, so borrowing cannot change what any of Brighter's infrastructure sees. That includes the factory-function registrations, every one of which takes the provider as a parameter and captures none (`ServiceCollectionExtensions.cs:201-220`, `:410`, `:420`, `:428`, `:484`, and `BuildCommandProcessor` at `:700`).
- **Every mapper, transform and handler type is registered `Transient`** whatever `MapperLifetime`, `TransformerLifetime` or `HandlerLifetime` say, because those options are read by Brighter's own factories rather than by the container (C-17). A `Transient` resolves from any scope of the container that holds its descriptor, and is tracked for disposal by the scope that resolved it. Under adoption that scope is the borrowed one, which is what AC-17 and AC-34 assert.

So registration needs nothing. `Singleton`s are unaffected because they never leave the root, and artefacts resolve correctly because their descriptors are in the collection the borrowed scope's container was built from — the same fact the paragraph above turns on.

Brighter's relational transaction providers wrap a `DbContext` taken by constructor injection (`MsSqlEntityFrameworkCoreTransactionProvider.cs:18`), and the provider is registered `Transient` by default while the `DbContext` is `Scoped`. A handler resolves from whichever scope its pipeline holds (`ServiceProviderHandlerFactory.cs:67-68`, `:85-86`), so which transaction it joins is decided by which scope resolved the pair. *Consequences* states what that is worth.

This ADR does not widen what the outbox does, and the boundary matters. `IAmAnOutbox` and `IAmAnOutboxProducerMediator` are `Singleton`s (`ServiceCollectionExtensions.cs:484`) built in the root scope, so they never borrow. The three container-side transaction-provider resolutions (`:431`, `:487`, `:648`) are type discovery only. `DepositPost` without an explicit provider still passes `null` (`CommandProcessor.cs:795`). **Adoption changes the provider instance the handler holds, and nothing else.** A `Publish` subscriber is excluded from all of it by FR-8's suppression, which is C-4 and is unchanged.

#### 2d. The residue, in three parts

**A provider that passes both tests and then cannot resolve a Brighter *artefact*** yields `null` from `Create`, and the builder's existing guard turns that into `ConfigurationException` (`PipelineBuilder.cs:193`) rather than a latched `Warning`. No cheap test distinguishes that case in advance, and the probe deliberately does not try. It resolves `ScopedArtefactCache`, which is one container-`Scoped` service per borrowed scope; resolving a mapper, transform or handler type to find out would be a different cost entirely — an artefact construction per pipeline on the fast path, for a case the builder's existing `null` guard already turns into a `ConfigurationException`.

**A provider whose scope is disposed after the probe** is the second part, and it is a genuine window rather than a caller's error. The probe runs once per pipeline, so an owner may dispose a borrowed scope at any point after it and before a later `Create`, and resolution from a disposed MS DI scope throws `ObjectDisposedException`. Under `AlwaysNew` that cannot arise — the scope is Brighter's own and no caller holds it — so adoption is what creates the window, and FR-23 leaves no room to accept it: its carve-out is for a *handler* that captures and uses `HttpContext.RequestServices` itself, and this is Brighter's own resolution, squarely inside the prohibition. Step 4 says what closes it. Re-probing before every resolution is not what closes it: a test and a use cannot be made atomic against an owner disposing in between, so per-resolution probing would buy a cost on every resolution and still leave the window open. It narrows a TOCTOU rather than closing one.

**A provider that offers the root provider by a handle the reference test cannot match** is the third part, and it is what that test leaves rather than what it closes. Microsoft's container injects the root `ServiceProviderEngineScope` into a singleton, so a provider handing back the `IServiceProvider` it was constructed with is caught; the `ServiceProvider` object `BuildServiceProvider()` returns — `IHost.Services` — is a different object with the same resolutions (probed), and a wrapper over either is a third. The contract table forbids all of them and identity catches the plain mistake, which is the shape the borrow would otherwise take in silence. Alternative 8 is the test that would reach further and why it is not taken.

#### 3. The affinity rule, stated over the family

A pipeline's participating set is structural (D12):

| Pipeline | Participating set | Notes |
| --- | --- | --- |
| transform | `{ MapperLifetime, TransformerLifetime }` | both, always — whether or not the mapper declares any `[WrapWith]`/`[UnwrapWith]`, and whether or not a transformer factory instance exists at all (`TransformPipelineBuilder.cs:180`'s v9 null path). Participation is structural, so it fixes the affinity rather than who is available to be asked |
| handler | `{ HandlerLifetime }` | alone |

The rule, over that set:

- the pipeline **takes a pipeline scope and asks, exactly once**, if and only if `Scoped` is the configured lifetime of a participant that exists to be asked (FR-27.1). The DI package always supplies a transformer factory (`ServiceCollectionExtensions.cs:945`), so in every host it registers that is the same test as `Scoped` being in the set. A hand-built v9 host with a `null` transformer factory has nobody to carry a `Scoped` `TransformerLifetime`, so that pipeline takes no scope and makes no ask;
- the ask carries `JoinAmbient` if and only if the affinity option is `JoinAmbient`, `Scoped` is in the set, `Transient` is **not** in the set, and suppression is not in force on this flow (ADR 0075); otherwise `AlwaysNew` (FR-27.2);
- `Singleton` participants are ignored by the test, exactly as FR-22.2 ignores them. A `{Scoped mapper, Singleton transformer}` transform pipeline adopts, and the `Singleton` transformer resolves from the root provider as it always did, ignoring the handle it is passed.

Two consequences follow.

**`TransformerLifetime = Transient` always vetoes adoption for a transform pipeline.** That is D12 plus FR-27.2, pinned by AC-46, and it is an accepted cost rather than a defect. Adopting for half a pipeline — the mapper in the caller's transaction, its transforms in a throwaway scope off root — is the failure mode D8 exists to prevent, and the fail-safe is to create an owned scope. Since all three lifetimes default to `Transient` (`BrighterOptions.cs:20`, `:52`, `:69`), an application adopting must move all three together, which is the same conclusion FR-16(b) and FR-22.2 reach from the other direction.

**The `Transient`/`Scoped` asymmetry between the two families is about the handle, not about the ask.** ADR 0071 gives a handler pipeline a handle for `Transient` as well as `Scoped`, because the handler factory's per-pipeline scope also carries `IsolateTransientHandlerScope` (ADR 0067, C-6); a transform pipeline takes one only under `Scoped`. That handle-for-`Transient` is ADR 0067's per-resolution machinery riding on a handle, it is not FR-27's pipeline scope, and step 2 above makes no ask for it. The ask is tied to `Scoped` participation, never to whether a handle exists. AC-46's first branch, `{Transient, Transient, Transient}`, records zero decisions across a `Send`, a three-subscriber `Publish` and a `Post`, even though every one of those handler pipelines holds a handle. That is why FR-27.1 and AC-46 define *"takes no pipeline scope"* over the ask and not over the handle, as the ladder above states.

#### 3a. Artefact caching, and the one behaviour that is not inherited

`ScopedArtefactCache` takes the `Lazy` publish protocol `ServiceProviderLifetimeScope.cs:163-178` already uses, with one deliberate change: a factory that throws does not leave a faulted entry behind. The `Lazy` is removed from the dictionary before the exception propagates, by the pair-matching removal the contract above specifies, so only the faulted entry goes. A later resolution of the same type in the same scope therefore calls the factory again. `ServiceProviderLifetimeScope.cs:49`'s private `_scopedInstances` field becomes a resolution of this service and inherits the same rule, so the owned and borrowed paths keep one protocol between them. It is required work rather than an adjacent nicety: moving the cache into the scope is what turns a fault confined to one pipeline into one confined to a whole request, and this ADR is what moves it.

**What is in scope of the #4260 fix, and what is not.** Both `Scoped` paths — owned and borrowed — stop publishing a faulted `Lazy`. `GetOrCreateSingleton` (`:152`) and its `_singletonInstances` cache are deliberately left alone, so this closes the `Scoped` half of #4260 and no more. The reason is the same one that keeps `Singleton` out of the ladder: a `Singleton` artefact resolves from the root provider and sits outside both affinities, so adoption does not widen its blast radius, and the behaviour that was tolerable before this ADR is exactly as tolerable after it. Fixing it belongs to #4260 on its own terms. An implementor must not read "both" as "both methods".

#### 4. Borrowing, and what it does and does not own

`ServiceProviderPipelineScope` gains an internal borrowed construction path over an `IServiceProvider`. It holds nothing but the borrowed provider and a `ServiceProviderLifetimeScope` in borrowed mode.

Borrowed implies `Scoped` by construction. A pipeline only reaches the borrowed *outcome*, the last line of step 6 in the protocol above, with `Scoped` participating and no `Transient` participant; it may well reach the *ask* carrying `AlwaysNew`, which is what ladder rows 5 and 6 and D16 describe. So the borrowed mode has no `Transient` per-resolution path of its own, and a `Singleton` participant sharing the pipeline resolves from the root provider without consulting the handle at all.

**The artefact cache is not held here.** In both modes it is `ServiceProviderLifetimeScope`'s `Scoped` path that resolves `ScopedArtefactCache` from the scope in play: from `src.Services` when borrowed, from the `IServiceScope` it just created when owned. That gives the cache one owner instead of two, and it is the edge the *Where the pieces live* diagram draws.

`Dispose()` and `DisposeAsync()` are idempotent no-ops (AC-16, AC-38). AC-8's idempotence rule is written over two live pipelines each holding a Brighter-created handle, so it does not reach this case and is not cited for it. Brighter disposes neither the provider, nor the ambient `IAmAScope`, nor any instance resolved from it (FR-12, AC-16): the instances are disposable transients that MS DI has already tracked against the request scope, and the caller disposes them when the request ends. On the failed-build path there is no owned scope, so `CleanUpAfterFailedBuild` releases nothing the caller owns and AC-38 holds by construction.

**The one thing the borrowed path adds to resolution.** Resolving through a borrowed handle can fail in a way an owned one cannot: the owner may dispose the ambient after the probe has passed and while the pipeline is still building, and MS DI then throws `ObjectDisposedException`. `ServiceProviderPipelineScope`'s borrowed `Create` path catches it and rethrows a `ConfigurationException` naming the cause — *the ambient offered by `<provider implementation type>` was disposed while a pipeline was resolving from it* — carrying the `ObjectDisposedException` as its inner exception. The provider implementation type is the identifier the three latches are already keyed on (D19), and the factory that built the handle already holds it.

One site covers every caller, because all five factories reach the borrowed provider through this handle rather than through a provider of their own, and the translation is the shape AC-5 already blesses for a build failure. `PipelineBuilder`'s two filters exclude `ConfigurationException`, so a `Send` caller sees it as thrown. The four transform-builder catches carry no filter, so a `Post` caller sees it as the inner exception of the builder's own `ConfigurationException`. Nothing is latched: this is a fault, not a declined adoption, and the three diagnostics are about declines. On the owned path there is nothing to translate — the scope is Brighter's own for the pipeline's whole life — which is why this belongs to the borrowed mode and to this ADR.

#### 5. Registration

All four registration entry points route through `ServiceCollectionExtensions.BrighterHandlerBuilder` (`:142`; the `BrighterOptions` overload at `:119` forwards to it), so that is the single place `ScopedArtefactCache` (`TryAddScoped`) and `AmbientScopeDiagnostics` (`TryAddSingleton`) are registered, and it is the only registration point this ADR adds. Nothing here depends on the `IOptions` pipeline, so C-12a's split across the four paths does not bite. The affinity option's journey to `IBrighterOptions` is FR-17's problem and ADR 0076's decision, and this ADR reads whatever object `IBrighterOptions` resolves to, exactly as the factories already do.

**The registration model for `IAmAScopeProvider`.** FR-24.3 requires a plain `services.AddSingleton<IAmAScopeProvider, T>()` on every path, including the ASP.NET extension's, and never `TryAddSingleton`. Every duplicate descriptor then stays in the collection where validation can see it, while MS DI resolves the service type to the last unkeyed descriptor. That registration model is this ADR's; the site at which the duplicate rule is evaluated and its message produced is ADR 0074's. Because Brighter registers no default provider (D11), registering the ASP.NET one can never itself create a duplicate.

#### 6. What is left to the siblings

ADR 0073 settles the spelling of `GetAmbient`, the ASP.NET package name and the registration extension name. ADR 0076 settles the opt-in property and how a setting reaches it. Nothing above changes shape for either of them. ADR 0074 decides where FR-22's rules and FR-24.3's duplicate-provider rule are evaluated; this ADR has fixed the registration model those rules read, and the three runtime latches they do not. ADR 0075 decides how a `Publish` subscriber suppresses adoption; it enters the protocol above at step 3 and nowhere else.

## Consequences

### Positive

- **Adoption is implemented once.** One protocol inside `CreatePipelineScope()` serves handler pipelines and transform pipelines, sync and async, producer and consumer. That is what ADR 0071's structural change was bought for, and it is now spent.
- **A third party can supply an ambient without inheriting anything.** `IAmAServiceProviderScope` is a one-member role, so AC-35's `AsyncLocal`-backed console-host provider participates on exactly the terms the ASP.NET package does, with no ASP.NET reference anywhere near it (NFR-7).
- **A provider can be written with no container reference at all.** AC-13's recorder implements `IAmAScopeProvider`, returns nothing, and records the affinity each pipeline asked with — because the ambient source and the resolution source are different roles, and only one of them touches a container.
- **Six of the seam's outcomes converge on one behaviour.** No provider registered, provider returned nothing, ambient stale, ambient from a container this package cannot use, ambient offered for an `AlwaysNew` ask, suppression in force — all six end at *create and own a scope*. There is one path to reason about, and it is the one that already exists (FR-11, FR-13, C-7).
- **FR-8 cannot be defeated by a third-party provider.** The guard on Brighter's side ignores an ambient returned for an `AlwaysNew` ask before it probes it, so a provider that violates the contract changes nothing about isolation and merely earns a latched warning (AC-11).
- **A `Send` handler's outbox write joins the caller's transaction, and that is the change applications will feel.** It is not a new mechanism: it is FR-16's instance sharing observed one level up. A transaction provider wraps an injected `DbContext`, so adopting the caller's scope makes the handler's `DbContext` the caller's, and a `DepositPost` from inside the handler writes the outbox row on the caller's connection inside the caller's transaction. Under `AlwaysNew` it does not, and nothing says so — the deposit succeeds and only atomicity is lost, which is why C-21 records the silence and AC-52 pins it with a rollback and an `AlwaysNew` negative control. This is the first place in the set that states the transaction consequence positively; every other statement about joining a caller's transaction is the `Publish`-subscriber limitation (C-4, OOS-10), which FR-8 keeps true and which this leaves untouched (FR-16(c)).
- **FR-16 and FR-26 are satisfied by one mechanism with no bookkeeping.** Making the artefact cache a container-`Scoped` service gives per-request artefact identity under adoption and per-pipeline identity without it, and the container releases it either way — no weak references, no eviction, no disposal callback Brighter does not get.
- **`Transient` and `Singleton` are untouched.** ADR 0067's per-resolution scopes, `IsolateTransientHandlerScope` (`BrighterOptions.cs:37`) and `Singleton`'s root resolution do not pass through the seam and make no ask (C-6, OOS-7, AC-46).
- **Release timing is unchanged.** `PipelineBuilder.Dispose()` (`:269-270`) still drains every subscriber's scope together at end of publish; nothing here tightens it (D10, AC-10).
- **Core stays container-agnostic** (ADR 0014). The three new core types name no container type, and the source-level guard AC-22.3 runs finds nothing new.

### Negative

- **Artefact identity has to move off the handle**, and that costs a new registered service plus a change to `ServiceProviderLifetimeScope`'s `Scoped` path. `_scopedInstances` (`:49`) stops being a private field and becomes a resolution, which is a small but real complication of a class that is already the densest in the package. ADR 0070 anticipated the need but deliberately did not pay for it, so the whole of the cost lands here.
- **`ScopedArtefactCache` is public surface in the DI package** that most users will never name. It has to be public so that AC-37's positive control can re-register it, and so that the pattern is legible to a non-Microsoft container package. `TryAddScoped` means an application can silently change Brighter's artefact identity by registering it differently.
- **Issue #4260's blast radius widens under adoption, and this ADR fixes the half it widens.** `GetOrCreateScoped` and `GetOrCreateSingleton` both cache a `Lazy<object?>` in default mode, which caches a faulted `GetService`. Today a faulted `Scoped` entry is confined to one pipeline's cache; once the cache is owned by a borrowed request scope, one transient resolution failure poisons that artefact type for every remaining pipeline in that request. Fixing the `Scoped` half therefore becomes a prerequisite of adoption rather than an adjacent nicety. The `Singleton` half is untouched: it resolves from the root provider, adoption does not reach it, and it stays #4260's to close.

  **This is a behavioural break, and it is this ADR's own.** It reaches a host that never opts in and registers no provider, because the eviction rule applies to the owned `Scoped` path as well: a resolution that faulted once is retried where today the remembered fault is rethrown. There is no compile error to warn of it, which is the release note's first category. It belongs in ADR 0070 step 7a's single entry — the one ledger the set keeps — and not in an entry of its own; step 7a carries it as *Behavioural, ADR 0072*, a one-line pointer back to this bullet, which is where the break is argued. This is the only break this ADR makes.
- **`TransformerLifetime = Transient` silently prevents every transform pipeline from adopting**, whatever `MapperLifetime` says and whether or not any transform is declared (D12, AC-46). Since all three lifetimes default to `Transient`, an application that sets only `MapperLifetime = Scoped` and opts in gets no adoption and — unless it calls `ValidatePipelines()` (C-15) — no signal either. That is accepted, and it is the reason FR-25's decision guide has to be framed as a joint choice over all three.
- **An ambient that does not implement this package's hand-off role is declined with a `Warning` and nothing else.** Consider a host that registers a provider from a package built over another container, which offers its own role type as such a package must, alongside Microsoft-backed factories. It gets working software that never adopts, reported once per container. That is the fail-safe behaviour C-7 asks for, but it is a quiet one, **and no acceptance criterion exercises it**. FR-23 is written about a *stale* resolution source and AC-29 uses a capturing provider, so neither reaches an ambient of a foreign role type; the row is covered by the extension of FR-23's diagnostic rather than by a criterion. Ladder row 9's decline — a provider that fails the probe — is exercised by AC-54 for one of its five outcomes, and the other four keep the gap. (A host merely *running* Autofac behind `AddBrighter` is a different case and adopts normally; the probe section says why.)
- **A provider that passes both tests and still cannot resolve Brighter's artefacts fails loudly, not quietly.** The pipeline borrows, `Create` returns `null`, and the builder's existing guard raises `ConfigurationException` (`PipelineBuilder.cs:193`) rather than a latched `Warning`. The seam declines what it can detect cheaply and no more; detecting this case in advance would cost an artefact construction per pipeline on the fast path, to detect a case the builder already reports. A borrowed scope disposed after the probe fails loudly too, and there the seam cannot detect it in advance at all: step 4 translates the resulting `ObjectDisposedException` into a `ConfigurationException` rather than let FR-23's forbidden exception reach the caller.
- **The migration cost, and who pays it.** Applications pay nothing to keep today's behaviour: the affinity option defaults to `AlwaysNew`, no provider is registered by default, and every path behaves exactly as it does today. The cost falls on applications that opt in, and it is the joint-lifetime choice. All three of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` must move together, `{Scoped, Scoped, Transient}` is not a destination, and an in-process `Publish` subscriber still cannot join the caller's transaction (C-4). Implementers of `IAmAScopeProvider` pay a second cost: an ambient that does not implement `IAmAServiceProviderScope` is inert against Brighter's own container package.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Brighter disposes a scope the caller owns | Ownership is decided in one place — the last two lines of the protocol — and a borrowed `ServiceProviderPipelineScope`'s disposal is a no-op. A **declined** ambient is never disposed either, which the protocol states at each of its three decline points (C-7). AC-16 and AC-38 |
| A provider offers a resolution source whose scope is disposed — either already, or after the probe and while a pipeline is still resolving — surfacing `ObjectDisposedException` from Brighter's own resolution | **Two mechanisms, because the probe alone bounds only the first resolution.** *Already disposed*: the usability probe runs before any pipeline instance is resolved from the ambient, and a failed probe declines and creates. *Disposed after the probe*: `ServiceProviderPipelineScope`'s borrowed `Create` path translates the `ObjectDisposedException` into a `ConfigurationException` naming the cause (step 4); no probe can close that window, and re-probing per resolution would only narrow it. Together they bound Brighter's own resolution, which is the whole of what FR-23 asks — a handler that captures and uses `RequestServices` itself is outside Brighter's control and is FR-23's own carve-out. AC-29, whose provider is one that **captures** a resolution source; see step 2b's *which providers reach rows 8 and 9* for why the ASP.NET provider is not one |
| Two `Post`s in one request get two mappers, falsifying FR-16(a) | The artefact cache is owned by the DI scope, not by the per-pipeline handle — the one thing ADR 0070 identifies as needed for adoption and does not itself supply. AC-17 is the guard |
| Brighter-held per-scope state accumulates across requests | The cache is a container-`Scoped` service, so the container disposes it with the scope and Brighter needs no disposal callback. AC-37's three clauses — including the positive control on the production path — measure it |
| A faulted resolution poisons an artefact type for a whole request | #4260's faulted-`Lazy` caching must be fixed on the `Scoped` path as part of this work; that cache must not retain a faulted entry. The `Singleton` cache is out of scope and unaffected by adoption (step 3a) |
| The three diagnostics collapse into one, or latch for the process | Three independent latches keyed on (condition, provider implementation type), held by a container-scoped singleton (D19). AC-11's third branch is the only case that distinguishes the schemes, and it is deliberately written to |
| A duplicate provider changes which ambient is used without anyone noticing | Plain `AddSingleton` on every path keeps every descriptor in the collection so validation can see it, and MS DI's last-unkeyed-descriptor resolution makes the effective provider predictable. Brighter registers no default, so the extension cannot itself create a duplicate. AC-32 |
| Adoption is implemented twice — once against a handle, once against the handler factory's dictionary | There is one protocol, in `CreatePipelineScope()`, and after ADR 0071 there is no second mechanism for it to compete with: that ADR **removes** the handler factory's dictionary rather than keeping it as a fallback, so a handler pipeline has exactly one way to obtain a scope. A caller supplying an `IAmALifetime` with a null `PipelineScope` no longer reaches a second resolution path; it is rejected at `Create` |
| Under `JoinAmbient`, two `Send`s in one request share a handler instance and therefore its mutable `Context` | This follows from D7 — artefact identity follows the borrowed scope — and is intended. Two *concurrent* in-request sends of the same command type are a genuine hazard; it belongs in `docs/guides/lifetimes-and-scoping.md` (FR-25) beside the statement that `AlwaysNew` is the default |

## Alternatives Considered

**1. Do nothing — no adoption at all.** ADRs 0070 and 0071 already close Defect 1 and Defect 1b, which are the actual bugs; adoption is a feature. **Rejected**, but it is the honest alternative. It leaves the case the specification was raised for with no answer other than "pass state through `RequestContext.Bag`": a Brighter handler and the controller that called it resolve two different `DbContext` instances in one request, while a Darker query handler in the same action resolves the controller's. FR-16 and FR-17 are the requirement.

**2. A concrete-class hand-off: a public borrowed constructor on `ServiceProviderPipelineScope`.** The ASP.NET package constructs a `ServiceProviderPipelineScope` over `HttpContext.RequestServices` and returns it as the ambient; the factories type-test for the class. No new interface at all. **Rejected on two counts.** It freezes `ServiceProviderPipelineScope`'s construction signature and ownership contract forever — the class owns a `ServiceProviderLifetimeScope`, is constructed with a lifetime and an isolate flag, and is the type most likely to change as the seam is used. The class is `public` today, by the package convention ADR 0070's *Technology Choices* records, but its constructor is `internal` and nothing outside the package is handed one, so nothing binds to that shape; a public borrowed constructor is what would make a third party bind to Brighter's implementation rather than to a contract. And it does not generalise: a package over Autofac cannot construct a Microsoft-container class, so NFR-7's "implementable over another container" would be met only by the class not being involved, which is the interface again with extra steps.

**3. An abstract provider base class in the DI package.** Ship `abstract class ServiceProviderScopeProviderBase : IAmAScopeProvider` and have the ASP.NET package and third parties derive from it. **Rejected.** It spends the implementer's single base class to save one property, and it is the wrong shape for the actual implementers: the ASP.NET provider's whole body is "read `IHttpContextAccessor`, return `HttpContext.RequestServices`", and a test double's is "return the `AsyncLocal`". Neither has anything to inherit. Roles in this codebase are interfaces (`IAmA*`), and this one has one member.

**4. Put a resolution member on `IAmAScope` itself.** One type instead of two, no type test, no downcast. **Rejected**, and the rule that forbids it is right rather than merely present. `IAmAScope` is a `Paramore.Brighter` type on core interfaces. Putting `IServiceProvider` on it — or any "give me an instance of this type" member — would make core's public seam an abstraction over an IoC container, which ADR 0014 decided Brighter does **not** do. The practical bite is immediate: `IAmAScope` is what a mapper factory in a test assembly with no container reference has to be able to see, and what an Autofac-backed package has to be able to implement without Microsoft's abstractions on its compile closure. A generic `T? Resolve<T>()` avoids naming `IServiceProvider` but is worse — it is a container abstraction with the name filed off, and it would put resolution semantics core has no way to define into a core contract.

**5. A middleware-based ASP.NET ambient.** `app.UseBrighterScope()`, publishing the request scope for Brighter to pick up. **Rejected by D1 and OOS-4.** It adds a required call site to every ASP.NET application, in a place ordering matters and is easy to get wrong; it does nothing for hosts that are not ASP.NET pipelines; and it does not remove the need for the provider, because Brighter still has to *ask* for the ambient at the point a pipeline is built. Registering the provider is the opt-in, and there is no per-request gesture at all.

**6. Put the usability probe on the hand-off role.** Add `bool IsUsable { get; }` to `IAmAServiceProviderScope` and let the ambient answer for itself. **Rejected.** The question that matters is "can *this container package* resolve from this provider", which is the DI package's question and not the ambient owner's; making every implementer answer it would have each of them reproduce Microsoft's disposal semantics, and a wrong answer would surface as `ObjectDisposedException` from Brighter's own resolution — exactly what FR-23 forbids. Keeping the role at one member also keeps it implementable by anyone, which is what the role is for.

**7. Give the borrowed handle its own artefact cache and accept per-pipeline artefact identity under adoption.** Simplest possible borrowed scope: no registered service, no change to `ServiceProviderLifetimeScope`. **Rejected**: it falsifies FR-16(a) and AC-17 — two `Post`s in one request would resolve two mappers — and it contradicts D7, which is the reading of the lifetime model that makes adoption coherent at all. If the pipeline's scope *is* the request scope, the request owns the instance.

**8. Identify the root by resolving `IServiceProvider` from the ambient and comparing that, rather than by comparing the ambient's own `Services`.** Microsoft's container answers `GetService<IServiceProvider>()` with the engine scope it was asked of: the root façade returns the root scope, and a child scope returns itself. One extra resolution would therefore also catch the `BuildServiceProvider()` handle the reference test misses. **Rejected.** It rests on a behaviour that is Microsoft's rather than the role's: Autofac's adapter answers with a *fresh* `AutofacServiceProvider` on every ask (probed), so behind any container whose adapter does the same the comparison matches nothing while reading as though it were general. A test that is inert on some containers and silent about which is worse than one that states its bound, and the bound is the residue's third part.

## References

**Related ADRs — the other six of this set:**

- ADR 0070 [0070-per-pipeline-di-scope-for-mapper-and-transform-factories](0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md) — a transform pipeline takes one DI scope, carried as a parameter
- ADR 0071 [0071-pipeline-scope-handle-for-handler-pipelines](0071-pipeline-scope-handle-for-handler-pipelines.md) — handler pipelines converge onto the same handle, carried on the object they already pass
- ADR 0073 [0073-aspnet-core-request-scope-package](0073-aspnet-core-request-scope-package.md) — the ASP.NET Core package, and the one line an application writes to opt in
- ADR 0074 [0074-lifetime-validation-evaluation-site](0074-lifetime-validation-evaluation-site.md) — where the scope-configuration rules are evaluated
- ADR 0075 [0075-publish-and-pump-scope-suppression](0075-publish-and-pump-scope-suppression.md) — how a `Publish` subscriber and the consumer pump suppress adoption, for themselves and every pipeline created beneath them
- ADR 0076 [0076-scope-affinity-option-and-write-through](0076-scope-affinity-option-and-write-through.md) — the affinity option, and how one setting reaches all four registration paths in any order

- Requirements: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md) — FR-1, FR-2, FR-8, FR-10, FR-11, FR-12, FR-13, FR-16/FR-16(a)/**FR-16(c)**, FR-17, FR-21, FR-18, FR-19, FR-22, FR-23, FR-24, FR-25, FR-26, FR-27; NFR-2, NFR-4, NFR-6, NFR-7, NFR-8; C-1, C-2, C-4, C-6, C-7, C-8 (the assumed home of its other two seam types), C-9, C-11, C-12a, C-13, **C-14**, C-15, **C-17**, **C-21**; D0b, D1, D4, D7, D8, D10, D11, D12, D16, D17, D19; AC-1, AC-5, AC-8, AC-10, AC-11, AC-13, AC-14, AC-16, AC-17, **AC-20**, AC-22, AC-26, AC-29, AC-30, AC-31, AC-32, AC-33, AC-34, AC-35, AC-37, AC-38, AC-46, **AC-52**, **AC-54**, **AC-55**; OOS-1, OOS-3, OOS-4, OOS-7
- Related ADRs (cited by slug — ADR numbers are not unique in this repo, C-16):
  - `0070-per-pipeline-di-scope-for-mapper-and-transform-factories` [Proposed] — the transform pipeline takes one DI scope, carried as a parameter; introduces `IAmAScope` and `ServiceProviderPipelineScope`. This ADR keeps its forward-compatibility promises and discharges the one thing it names as outstanding for adoption: artefact identity under a **borrowed** scope, which does not follow from a per-pipeline handle
  - `0071-pipeline-scope-handle-for-handler-pipelines` [Proposed] — handler pipelines converge onto the same handle via `IAmAHandlerFactory.CreatePipelineScope()` and `IAmALifetime.PipelineScope`, which is what lets adoption be one change rather than two
  - `0014-di-friendly-framework` [Accepted] — per-family factory interfaces rather than abstracting an IoC container; the durable reason the hand-off lives outside core and the reason alternative 4 is refused
  - `0067-per-resolution-di-scope-for-transient-factory-instances` [Accepted] — `Transient`'s per-resolution DI scope and `IsolateTransientHandlerScope`, untouched here; its `Terms` block defines the configured-lifetime and registration-lifetime axes this ADR uses and does not restate
  - `0066-release-factory-instances-on-an-opaque-lease` [Accepted] — the opaque `Lease<T>`, whose release remains a no-op for `Scoped` and is unaffected by borrowing
  - `0068-deterministic-disposal-finalizer-safety-net` [Accepted] — the disposal rules a borrowed handle's no-op disposal must still satisfy
  - `0069-factory-registry-ownership-and-disposal-cascade` [Accepted] — why `MessageMapperRegistry` speaks for the factories it owns, and therefore why the transform pipeline's single ask travels through it
  - `0075-publish-and-pump-scope-suppression` [Proposed] — how a `Publish` subscriber and the consumer pump suppress adoption, for themselves and for the pipelines beneath them. Both enter the protocol here at one line, the affinity computation, and add no outcome to the ladder
  - `0039-scoping-dependencies-inline-with-lifetime-scope` [Proposed] — a DI scope per registered subscriber on `Publish`; ADR 0075 exists to protect it, and it is not reopened (D0c)
  - `0053-pipeline-validation-at-startup` [Accepted] and `0064-validate-pipeline-assembly-and-provider-registration` [Accepted] — the `ValidatePipelines()` machinery FR-24.3's duplicate-provider warning lands in; this ADR fixes the registration model, ADR 0074 decides the site
- External references:
  - [Dependency injection guidelines — scope validation and captive dependencies](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
  - Issue #4260 — faulted-`Lazy` caching in `GetOrCreateScoped`/`GetOrCreateSingleton`, whose blast radius this ADR widens
  - Wirfs-Brock & McKean, *Object Design: Roles, Responsibilities, and Collaborations* — the role and responsibility vocabulary used to separate the ambient source (deciding) from the resolution source (knowing)
