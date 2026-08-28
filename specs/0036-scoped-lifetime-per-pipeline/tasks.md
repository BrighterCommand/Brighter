# Implementation Tasks — Spec 0036: Scoped lifetime per pipeline

## Overview

The seven ADRs do not implement in ADR-number order, because they state their own compile dependencies and those dependencies cross the numbering. ADR 0070 comes first: it declares `IAmAScope`, the handle every other ADR takes, and changes six interfaces that every implementation in the repository must move with in one commit. ADR 0071 puts the same handle on the handler family. Only then do the seam types, the suppression flag and the affinity option make sense as a group (Phase 3), because ADR 0072's `CreatePipelineScope()` ladder reads `AmbientScopeSuppression.IsSuppressed` (ADR 0075's type) at step 3, ADR 0076 step 1 cannot name `ScopeAffinity` until ADR 0072 step 1 declares it, and ADR 0073 step 2 states outright that *"nothing in this package compiles before both [0072 and 0076]"*. So the three core-type declarations land together in Phase 3, the adoption protocol in Phase 4, the suppression brackets in Phase 5, the ASP.NET package and its new test project in Phase 6, and validation plus the guidance page last in Phase 7 — validation reads `IBrighterOptions` as the factories see it, which only exists after ADR 0076, and the guidance page's troubleshooting section keys to messages only Phase 7 produces.

Inside each phase the ADR's own numbered steps keep their order. Every behavioural task uses the mandatory `/test-first` template with its approval gate; structural tasks use `/tidy-first` and must not share a commit with behavioural change. Each task carries a `Depends on` line (requirement 7) placed immediately before its `References` line.

⚠ **One task is one approval gate, but not always one test fact.** Several acceptance criteria state their `Then` over more than one arrangement — a different registration order, a different affinity, the other sync/async twin, a control host that must produce *no* finding. `/test-first` writes and gates **one** test, so any task needing more than one `[Fact]` carries an explicit **`Facts:`** line saying how many it needs and which file they live in.

⚠ **The trigger is mechanical, and it is not "more than one host".** Twice — in review rounds 1 and 2 — this rule was swept by working a list of named tasks rather than by applying it, and both times it left tasks behind, including tasks that arrange no host at all. **A task needs a `Facts:` line whenever its `Test should verify` cannot be written as a single `[Fact]`.** That is any of:

- **a second host** — a different triple, affinity, registration order or entry point;
- **the other twin** — sync and async are two runs, never one (this spec's signature failure mode);
- **a second act on the same host** — a `Send` branch and a `Post` branch, `throwOnError` true and false, `Dispose()` and `DisposeAsync()`;
- **a distinct arrangement** — an input that throws beside one that does not, a control that must produce *no* finding, a positive control built from a deliberately broken host, a parameter swept over two values.

⚠ **The counter-case, so the rule is not over-applied**: several assertions over **one** run are still **one** fact, even when the prose says "both". *"Two `Send` calls recording **exactly one** `Warning` between them"* (T4.5, T4.7, T6.7) is a single aggregate assertion and cannot be split without destroying it. Sequential acts in one narrative — message N then N+1, a second `Post` proving a failure is not latched — are one fact too.

The rule for multi-fact tasks: **the ⛔ gate is taken once, on the first fact**; the remaining facts land in the *same* red-green cycle and the *same* file unless the task names a second file. A task with no `Facts:` line is a single fact. Do not treat a multi-fact task as done when the first fact passes — the negative and control branches are usually the last ones written and are the ones that make the criterion falsifiable.

Counts and `file:line` anchors below were re-derived against the working tree; where a re-derivation differs from an ADR the task says so inline.

⚠ **"Ladder row N" always means ADR 0072's canonical ten-row table** under *The mechanism, end to end* (`0072-ambient-scope-adoption-seam.md:153-165`). ADR 0072's step 2 restates the same decisions as a six-step pseudo-code block with **different** numbers, and flags the mismatch itself (`4. if (_scopeProvider is null) return OWNED // ladder row 3`). No task in this document uses the pseudo-code numbering. The rows that carry a diagnostic are **5** (*ambient offered for an `AlwaysNew` ask and ignored*), **7** (*no ambient offered*) and **8**/**9** (*ambient offered but unusable*); rows 1, 2, 3, 4, 6 and 10 are silent.

---

## Phase 1 — ADR 0070: per-pipeline DI scope for the mapper and transform factories

- [ ] **STRUCTURAL: T1.1 — add `IAmAScope` to core, with the NFR-8 disambiguation on it and on `IAmALifetime`**
  - **USE COMMAND**: `/tidy-first add the IAmAScope pipeline scope handle to Paramore.Brighter with XML documentation distinguishing it from IAmALifetime`
  - Files: `src/Paramore.Brighter/IAmAScope.cs` (new); `src/Paramore.Brighter/IAmALifetime.cs` (XML documentation only)
  - `public interface IAmAScope : IDisposable, IAsyncDisposable` with no members; `Microsoft.Bcl.AsyncInterfaces` is already conditioned on `netstandard2.0` at `src/Paramore.Brighter/Paramore.Brighter.csproj:24`, so no new dependency
  - NFR-8's XML documentation rides here, not in a separate task: `IAmAScope` states it *is* a DI scope, `IAmALifetime` states it *tracks handler instances*
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: nothing
  - **References**: NFR-8, C-8; ADR 0070 step 1

- [ ] **TEST + IMPLEMENT: T1.2 — Core declares no container types and no package creeps into core, the DI package or the ServiceActivator**
  - **USE COMMAND**: `/test-first the solution's project files and core source declare no container or ASP.NET types`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Architecture"
  - Test file: `When_the_solution_is_built_it_should_declare_no_container_types_in_core.cs` (class `DependencyBoundaryTests`)
  - **Facts**: **3**, all in this one file — AC-22's three clauses, which read three different things (the built assembly's public surface, three `.csproj` files as XML, and core's `*.cs` source) and cannot share one `[Fact]`. ⚠ **Clause 3 is the load-bearing one and is written last**: the task says so itself — `Microsoft.Extensions.DependencyInjection` is already on core's compile closure transitively, so a green on clause 2 would pass a change that put container types in core's source. Gate once, on the first fact
  - Test should verify:
    - no public interface in the `Paramore.Brighter` assembly declares a member whose signature mentions `IServiceProvider` (AC-22 clause 1)
    - parsing `Paramore.Brighter.csproj`, `Paramore.Brighter.Extensions.DependencyInjection.csproj` and `Paramore.Brighter.ServiceActivator.csproj` as XML: core has no **direct** `PackageReference`/`ProjectReference` matching `Microsoft.Extensions.DependencyInjection*`; the DI package has none matching `Microsoft.AspNetCore.*`; `Paramore.Brighter.ServiceActivator` has exactly one `ProjectReference` (to `Paramore.Brighter`) and no `PackageReference` (AC-22 clause 2) — re-derived: that project has exactly one `ProjectReference` at HEAD
    - scanning every `*.cs` under `src/Paramore.Brighter/`, none references `ServiceLifetime`, `IServiceCollection`, `IServiceProvider` or `ServiceDescriptor` (AC-22 clause 3). This clause is the load-bearing one: `Microsoft.Extensions.DependencyInjection` is already on core's compile closure transitively through `Microsoft.Extensions.Logging`, so clause 2 alone would pass a change that put those types in core's source
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - require **no production change** — it is a guard rail placed first so that every later phase, including Phase 6's new ASP.NET package, is protected. If it fails, the change that broke it is at fault
    - be re-run when Phase 6 lands, where the NFR-2 clause first becomes falsifiable. ⚠ It is an **automated test**, so it needs no separate gate of its own: **T6.1 and T6.2 each carry it as a `Done when` condition** and CI re-runs it on every build thereafter (nothing references ASP.NET today: re-derived, zero `Microsoft.AspNetCore` references across all 37 test projects and across `src/` except `Paramore.Brighter.ServiceActivator.Control.Api`)
  - **Depends on**: T1.1
  - **References**: AC-22 (NFR-1, NFR-2, NFR-3)

- [ ] **STRUCTURAL: T1.3 — the six mapper/transform interfaces gain `CreatePipelineScope()` and the scope parameter, and every implementation in the repository moves with them**
  - **USE COMMAND**: `/tidy-first add CreatePipelineScope and the IAmAScope parameter to the four mapper and transformer factory interfaces and the two mapper registries, moving every implementation in one change`
  - Files:
    - core interfaces: `IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync` (`CreatePipelineScope()`; `IAmAScope? scope = null` on `Create`); `IAmAMessageMapperRegistry`, `IAmAMessageMapperRegistryAsync` (`CreatePipelineScope()`; scope on `Get<T>`/`GetAsync<T>`)
    - **12 classes in `src/`** — re-derived at HEAD and matching the ADR: `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`, `ServiceProviderTransformerFactoryAsync`, `SimpleMessageMapperFactory`, `SimpleMessageMapperFactoryAsync`, `SimpleMessageTransformerFactory`, `SimpleMessageTransformerFactoryAsync`, `EmptyMessageTransformerFactory`, `EmptyMessageTransformerFactoryAsync`, `ControlBusMessageMapperFactory`, `MessageMapperRegistry`
    - **70 test doubles across 38 test files** — re-derived at HEAD: 64 factory doubles (61 on a single-line declaration plus 3 wrapped onto a continuation line, in `When_async_disposing_a_running_dispatcher_it_drains_before_disposing_factories.cs:100`, `.../Proactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched_async.cs:87`, `.../Reactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched.cs:83`) across 37 files, and 6 registry doubles across 3 files, whose union is 38 files. **The ADR's counts hold**
  - ⚠ **This must stay one commit.** `netstandard2.0` has no default interface member, so the solution does not compile until every implementation moves. Do **not** split by assembly or by suite
  - ⚠ **Get the file list from the tree, not from the count above.** The count is a *check* on the list, not a substitute for it — run this first and work the output, then confirm it returns 38:

    ```sh
    grep -rlE ':[[:space:]]*(IAmAMessageMapperFactory|IAmAMessageMapperFactoryAsync|IAmAMessageTransformerFactory|IAmAMessageTransformerFactoryAsync|IAmAMessageMapperRegistry|IAmAMessageMapperRegistryAsync)\b' \
      tests --include="*.cs" | sort -u
    ```

    Verified at HEAD: returns exactly **38** files. Three doubles declare the base type on a **continuation line**, so a line-oriented scan that assumes one declaration per line will miss them — they are in `When_async_disposing_a_running_dispatcher_it_drains_before_disposing_factories.cs:100`, `.../Proactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched_async.cs:87` and `.../Reactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched.cs:83`
  - Every non-container implementation except `MessageMapperRegistry` gets the same two-line treatment: `CreatePipelineScope()` returns `null`, `Create` ignores the parameter. `MessageMapperRegistry` forwards both members to the (up to two) factories it owns. `Paramore.Brighter.ServiceActivator` gains no container dependency, because `IAmAScope` is a core type (NFR-3)
  - ⚠ **The four container-backed factories get that same treatment in this commit** — `CreatePipelineScope()` returns `null` and `Create` ignores the parameter. `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory` and `ServiceProviderTransformerFactoryAsync` are named in the 12-class list because their **signatures** move here; their **behaviour** is T1.5's, which replaces these bodies with the `Scoped`-only scope offer. Implementing the container behaviour here would put behavioural change in a `/tidy-first` commit, which the last bullet forbids
  - The defaulted `IAmAScope? scope = null` keeps every existing *call site* compiling; the default is `null` and must stay `null`
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T1.1
  - **References**: NFR-1(b), NFR-1(c), C-19; ADR 0070 step 2

- [ ] **STRUCTURAL: T1.4 — lift the inline failed-build cleanup guard to a private `CleanUpQuietly` on both transform builders**
  - **USE COMMAND**: `/tidy-first extract the inline failed-build cleanup guard in TransformPipelineBuilder and TransformPipelineBuilderAsync to a private CleanUpQuietly method`
  - Files: `src/Paramore.Brighter/TransformPipelineBuilder.cs` (`:116-125` wrap, `:157-166` unwrap — re-derived, both `catch (Exception e)` at `:116` and `:157`), `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs` (the same two lines, re-derived: `catch (Exception e)` at `:116` and `:157`)
  - `CleanUpQuietly` calls `CleanUpAfterFailedBuild` and logs a cleanup failure rather than letting it mask the error the caller needs. Behaviour is unchanged; it is lifted here because ADR 0072 step 1b adds a second clause that needs the identical cleanup as a **named** method
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T1.3
  - **References**: FR-5; ADR 0070 step 3 (as required by ADR 0072 step 1b)

- [ ] **TEST + IMPLEMENT: T1.5 — a `Scoped` mapper is one instance per transform pipeline, not one per process**
  - **USE COMMAND**: `/test-first a Scoped mapper is constructed once per transform pipeline and disposed before the next pipeline's instance is constructed`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_consuming_two_messages_a_scoped_mapper_should_not_be_reused.cs`
  - Test should verify:
    - lifetime triple `{HandlerLifetime = Scoped, MapperLifetime = Scoped, TransformerLifetime = Scoped}` — an FR-22.2-conformant triple; a mapper type registered `AddScoped` recording its construction identity and its disposal
    - consuming message N then message N+1 constructs **two distinct** mapper instances
    - the instance used for N was **disposed before** the instance for N+1 was constructed (the ordering, not merely the distinctness)
    - zero Brighter-created scopes live at the end (NFR-5)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - thread the scope through `TransformPipelineBuilder`/`TransformPipelineBuilderAsync`: acquire **inside** the `try` (a container that cannot create a scope is an ordinary build failure) in all four of `BuildWrapPipeline<TRequest>()` (`:93`) and `BuildUnwrapPipeline<TRequest>()` (`:134`) on **both** builders — four methods, wrap and unwrap symmetric (ADR 0070 step 3)
    - add the private `CreatePipelineScope()` helper that asks the mapper registry first (`_mapperRegistry` `:51` sync, `_mapperRegistryAsync` `:50` async), then the transformer factory null-conditionally (the v9 compatibility path, `TransformPipelineBuilder.cs:180`), returning the first non-null handle
    - have `TransformPipeline<TRequest>`/`TransformPipelineAsync<TRequest>` hold the handle as an optional trailing constructor parameter, threaded through `WrapPipeline`, `UnwrapPipeline`, `WrapPipelineAsync`, `UnwrapPipelineAsync` (step 5)
    - give `TransformPipelineDrain.Drain`/`DrainAsync` a **third delegate and a third step, run in a `finally` around the first two** (`:46`, `:85`). It must be a `finally`: today's drain exits by throwing at `:67-72` and `:76`, so an appended step would never run on a failure path. Steps 1 and 2 keep hold-and-compose; the `AggregateException` is thrown *after* the `finally`
    - have each of the four container-backed factories return a new `ServiceProviderPipelineScope` from `CreatePipelineScope()` **only when its own configured lifetime is `Scoped`**, and `null` otherwise; `Create(Type, IAmAScope?)` resolves through the handle when it is a `ServiceProviderPipelineScope` and the lifetime is `Scoped` (step 6)
    - leave the six build/release call sites in `OutboxProducerMediator`, `Reactor` and `Proactor` untouched — the scope is created in the builder and released by the pipeline, which is what keeps C-2 intact (step 8)
  - **Depends on**: T1.3, T1.4
  - **References**: AC-1 (FR-1, NFR-5); ADR 0070 steps 3, 5, 6, 8

- [ ] **TEST + IMPLEMENT: T1.6 — a `Scoped` transform is one instance per transform pipeline, on both builders**
  - **USE COMMAND**: `/test-first a Scoped unwrap transform is constructed once per transform pipeline on both the sync and async builders`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_consuming_two_messages_a_scoped_transform_should_not_be_reused.cs`
  - **Facts**: **2**, both in this one file — the sync twin over `TransformPipelineBuilder` (Reactor), and the async twin over `TransformPipelineBuilderAsync` (Proactor). ⚠ The task's own text says **a single-twin test does not discharge AC-2**; the two builders are separate types, so this is two runs and never one. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; an unwrap transform registered `AddScoped`
    - two messages construct two distinct transform instances and the first is disposed before the second is constructed
    - asserted for **both twins** — `TransformPipelineBuilder` (sync/Reactor) **and** `TransformPipelineBuilderAsync` (async/Proactor). This task touches both; a single-twin test does not discharge AC-2
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - pass the pipeline scope into `BuildTransformPipeline<TRequest>` → `new TransformerFactory<TRequest>(attribute, _messageTransformerFactory)` (`:193`) → `factory.Create(transformerType, scope)`; the two internal helpers `TransformerFactory<TRequest>` (`:32`, create at `TransformerFactory.cs:42`) and `TransformerFactoryAsync<TRequest>` (`:30`, `TransformerFactoryAsync.cs:40`) both take the scope
    - keep `ResolveMapperInfo`/`ResolveAsyncMapperInfo` (`TransformPipelineBuilder.cs:172`, `TransformPipelineBuilderAsync.cs:172`) scopeless — they resolve a mapper *type*, not an instance
  - **Depends on**: T1.5
  - **References**: AC-2 (FR-2); ADR 0070 steps 3, 6

- [ ] **TEST + IMPLEMENT: T1.7 — within one transform pipeline the mapper and its transforms share one `Scoped` dependency (Defect 1b)**
  - **USE COMMAND**: `/test-first a container Scoped dependency injected into a mapper and its unwrap transform is one instance for the pipeline`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_mapping_a_message_the_mapper_and_its_transform_should_share_one_scoped_dependency.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; `IMarker` registered `AddScoped`, injected into both the mapper and its `[UnwrapWith]` transform
    - for one message, `ReferenceEquals(mapper.Marker, transform.Marker)` is `true`
    - for a second message both see a second, different `IMarker`, and the first `IMarker` was **disposed at the end of the first pipeline**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - deliver the *Share* leg: one `IServiceScope` behind one handle serves both `Create` calls, because the builder threads the **same** handle to the registry and the transformer factory (C-19). Only `{Scoped, Scoped}` closes Defect 1b — the mixed cases in ADR 0070 step 7 stay as that table states, and FR-27.2's veto is Phase 4's
  - **Depends on**: T1.6
  - **References**: AC-3 (FR-3, C-19); ADR 0070 steps 3, 6, 7

- [ ] **TEST + IMPLEMENT: T1.8 — producer transform pipelines scope per `Post`/`DepositPost`**
  - **USE COMMAND**: `/test-first two Post calls from a console host resolve two distinct Scoped mapper instances, the first disposed before the second is constructed`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_posting_twice_from_a_console_host_each_post_should_get_its_own_scoped_mapper.cs`
  - Test should verify:
    - a console host — **no ambient, no `IAmAScopeProvider` registered** — with lifetime triple `{Scoped, Scoped, Scoped}`
    - `Post(commandA)` completes, then `Post(commandB)`: two distinct mapper instances, the first disposed before the second is constructed
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - require no new production code beyond T1.5–T1.7 if the scope really is created in the builder and released by the pipeline: `OutboxProducerMediator`'s four producer sites (`:1248`/`ReleasePipeline` `:1258`, `:1312`/`ReleasePipelineAsync` `:1321`) and its two unwrap sites (`:569`, `:587`) are correct without being touched (step 8). If it does need a change, the change is in the builder, not at a call site
  - **Depends on**: T1.7
  - **References**: AC-4 (FR-4); ADR 0070 step 8

- [ ] **TEST + IMPLEMENT: T1.9 — a failed transform-pipeline build releases the owned pipeline scope and preserves the configuration error**
  - **USE COMMAND**: `/test-first a thousand failing Post attempts leak no Brighter-created scopes and each still throws ConfigurationException carrying the original resolution failure`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_pipeline_build_fails_repeatedly_it_should_leak_no_scopes.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; a mapper whose constructor depends on an unregistered service, so the build throws
    - 1,000 `Post` attempts, each throwing `ConfigurationException` whose **inner exception is the original resolution failure**
    - the count of Brighter-created scopes begun equals the count released — **zero live at the end** (NFR-5)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - give `CleanUpAfterFailedBuild<TRequest>` (`:231` on both builders) the scope, releasing it **in a `finally` around the lease releases and not as a statement after them**. `ReleaseTransforms` guards each transform release (`TransformPipelineBuilder.cs:215-223`) but `_mapperRegistry.Release(messageMapperLease)` at `:244` is **not** guarded, so an appended statement would be skipped by a throwing mapper `Release` and leak the scope this step exists to reclaim
    - take the branch distinction seriously: where a pipeline object *was* constructed (`:104`, with throws still possible at `:106`, `:108`, `:111`) cleanup delegates to `pipeline.Dispose()` and the drain owns the release; where it was not, cleanup releases the scope directly (step 4)
  - **Depends on**: T1.8
  - **References**: AC-5 (FR-5, NFR-5); ADR 0070 step 4

- [ ] **TEST + IMPLEMENT: T1.10 — a pipeline-scope release failure on a failed build is logged at `Error` and does not mask the configuration error**
  - **USE COMMAND**: `/test-first a failing build whose pipeline scope disposal also throws still surfaces the ConfigurationException and logs the disposal failure at Error`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_failed_build_scope_release_throws_it_should_log_at_error_and_not_mask_the_build_failure.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; a failing pipeline build **where releasing the pipeline scope itself throws**
    - the caller receives the `ConfigurationException` for the build failure
    - a capturing `ILoggerProvider` registered for `Paramore.Brighter.*` records the disposal failure at `LogLevel.Error` — specifically `FailedToDisposePipelineScopeAfterFailedBuild`, which is the branch where **no pipeline object was constructed**. The test must drive that branch deliberately (a mapper/transform resolution failure), not fail the build at an arbitrary point and assume which of the two messages fires
    - no `FailedToCleanUpAfterFailedBuild` `Warning` is written for the same event (the new message logs and swallows)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - add **step 4b first, without which nothing here can fire**: `ServiceProviderLifetimeScope` gains a **surfacing** disposal path that rethrows instead of logging and swallowing, and `ServiceProviderPipelineScope.Dispose()`/`DisposeAsync()` use it. The existing `Dispose()` (`:462-501`) and its `FailedToDisposeScope` at `Warning` (`:522`) keep today's terminal-teardown behaviour exactly, and `DisposeScope` (`:406`) with its `SynchronizationContext` suppression (`:422-436`) is unchanged
    - give `ServiceProviderLifetimeScope` `IAsyncDisposable` and a whole-scope `DisposeAsync()` routed through the existing `DisposeScopeAsync` (`:449`), so the handle's async release has something async to call (step 6)
    - add **two new `Error`-level messages and leave the existing ten at `Warning`** (step 4a): `FailedToDisposePipelineScopeAfterFailedBuild` on `TransformPipelineBuilder.Log`/`TransformPipelineBuilderAsync.Log` (beside `FailedToCleanUpAfterFailedBuild` at `:409`/`:318`), and `FailedToDisposePipelineScope` on a new `TransformPipelineDrain.Log` with a static logger the type does not have today. Both name the request type
    - **not** edit `OutboxProducerMediator` (`:1449`), `Reactor` (`:638`) or `Proactor` (`:652`) — their `FailedToReleasePipeline` keeps its level, message and meaning
  - **Depends on**: T1.9
  - **References**: AC-6 (FR-5, FR-13); ADR 0070 steps 4a, 4b, 6

- [ ] **TEST + IMPLEMENT: T1.11 — design-owed: a completed transform pipeline whose owned scope disposal throws returns its result unchanged**
  - **USE COMMAND**: `/test-first a Post whose transform pipeline completes and whose pipeline scope disposal throws returns unchanged and logs one Error`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_completed_transform_pipeline_scope_disposal_throws_the_post_should_still_succeed.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`, not opted in; a `Post` whose transform pipeline **completes** and whose owned scope's disposal throws (injected through a container-`Scoped` dependency whose `Dispose()` throws, so the container's scope disposal throws)
    - the `Post` returns its result unchanged
    - **exactly one** `FailedToDisposePipelineScope` at `LogLevel.Error` through a capturing `ILoggerProvider` for `Paramore.Brighter.*`
    - a second `Post` behaves normally — the failure is **not latched**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - make the drain's third step catch its own failure, log `FailedToDisposePipelineScope` at `Error` and swallow it, so it does **not** join steps 1 and 2's `AggregateException` composition (step 5). A capturing provider then sees `Error` for a scope-disposal failure and `Warning` for a mapper or transform release failure from one `Dispose()`, without any call site telling them apart — and both if both happen
    - keep the release-once guard as it is: `Interlocked.Exchange(ref _released, 1)` at `TransformPipeline.cs:65`, claimed before the drain at `:69`
    - run the third step on the **finalizer path too** — both exits funnel through `ReleaseUnmanagedResources` into the same synchronous `Drain` (`TransformPipeline.cs:37-72`; `TransformPipelineAsync.cs:96-118`), and nothing in the signature tells them apart. Do not introduce a flag to skip it
  - ⚠ **This is a design-owed test, not a nicety.** No acceptance criterion reaches it: AC-6 covers the failed-build case and AC-33 the handler one
  - **Depends on**: T1.10
  - **References**: FR-13 (disposal clause, transform side); ADR 0070 steps 5, 9a (verification table, *design-owed test* row)

- [ ] **TEST + IMPLEMENT: T1.12 — disposing a Brighter-created pipeline scope twice is a no-op and does not affect a concurrently live pipeline**
  - **USE COMMAND**: `/test-first disposing an IAmAScope twice is a no-op and leaves a concurrently live pipeline's scope usable`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_pipeline_scope_is_disposed_twice_it_should_not_throw_or_affect_another_pipeline.cs`
  - **Facts**: **2**, both in this one file — X's scope disposed a second time through `Dispose()`, and ⚠ **separately** through `DisposeAsync()`. `IAmAScope` is `IDisposable` **and** `IAsyncDisposable` (T1.1), so the two paths are different members and a green on one proves nothing about the other. Y's scope staying live and usable is asserted in **both** facts, not in a third. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}` — so both pipelines take a pipeline scope (FR-27.1); two concurrently live pipelines X and Y, each holding a Brighter-created `IAmAScope`; X's scope already disposed
    - invoking `Dispose()` a second time on X's scope raises no exception — **and separately** `DisposeAsync()` a second time, in either order
    - Y's scope and the instances resolved from it remain **undisposed and usable**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - have `ServiceProviderPipelineScope` claim disposal with a **single atomic exchange**, disposing its one `ServiceProviderLifetimeScope` exactly once under either entry point (step 6)
  - **Depends on**: T1.11
  - **References**: AC-8 (FR-6); ADR 0070 steps 6, 5 (`IAmAScope` idempotence)

- [ ] **TEST + IMPLEMENT: T1.13 — on the consumer, the transform pipeline's scope ends before the handler pipeline begins**
  - **USE COMMAND**: `/test-first on the consumer an unwrap transform and the handler do not share a Scoped dependency and the transform's is disposed before Handle is entered`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_consuming_a_message_the_transform_scope_should_end_before_the_handler_pipeline_begins.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; `IMarker` registered `AddScoped` and injected into **both** an unwrap transform and the handler for the same message
    - the transform's `IMarker` and the handler's `IMarker` are **not** reference-equal
    - the transform's instance was **disposed before** `Handle`/`HandleAsync` was entered
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - require no new code — this criterion asserts the ADR did **not** silently widen the unit of work. `TranslateMessage` runs its `finally` before `InvokeDispatchRequest` (`Proactor.cs:239` then `:241`), so the release ordering preserves C-3 by construction, not by accident
    - be read against AC-34, which is the *opposite* outcome on the opted-in producer side (Phase 6) — the two together are what pin C-3 as intended rather than accidental
  - **Depends on**: T1.12
  - **References**: AC-21 (C-3, FR-19); ADR 0070 step 9a

- [ ] **TEST + IMPLEMENT: T1.14 — design-owed: a direct `Create(type)` with no scope resolves fresh under `Scoped` and caches nothing**
  - **USE COMMAND**: `/test-first a container backed mapper factory called directly with no pipeline scope resolves a fresh Scoped artefact each time`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_scoped_factory_create_is_called_outside_a_pipeline_it_should_resolve_fresh.cs`
  - **Facts**: **4**, all in this one file — one per container-backed factory, because the task's closing clause (*"the same holds on the transformer factories and on both async twins"*) names the whole family: `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory` and `ServiceProviderTransformerFactoryAsync`. ⚠ These are the same four types T1.5 gives the `Scoped`-only scope offer; a fix applied to one and not its twin is this spec's signature failure mode. Gate once, on the first fact
  - Test should verify:
    - `MapperLifetime = Scoped` (stated with the full triple `{Scoped, Scoped, Scoped}`); two direct `factory.Create(type)` calls with the defaulted `null` scope return **two different instances**
    - reclamation happens where the caller releases, not at process exit — the factory-wide `_lifetimeScope` built in the constructor (`ServiceProviderMapperFactory.cs:46`) no longer serves `Scoped`
    - the same holds on the transformer factories and on both async twins
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - remove the factory-wide `Scoped` cache rather than leaving it as a second behaviour on the same factory (step 9). Brighter's own paths always pass a scope — the only mapper resolutions in `src/` are `TransformPipelineBuilder.cs:332` and `TransformPipelineBuilderAsync.cs:255`, and the only transformer `Create` callers are `TransformerFactory.cs:42` and `TransformerFactoryAsync.cs:40`
    - be release-noted: this is one of ADR 0070's five own breaking items, recorded by T7.14
  - ⚠ **Design-owed.** No acceptance criterion reaches the out-of-bracket path, and ADR 0070 step 7a treats it as a behavioural break with no compile error to warn a caller
  - **Depends on**: T1.13
  - **References**: FR-1 (Defect 1 on its last surviving path), AC-24 (general clause); ADR 0070 steps 9, 7a

---

## Phase 2 — ADR 0071: handler pipelines onto the same handle

- [ ] **STRUCTURAL: T2.1 — `IAmAHandlerFactory` gains `CreatePipelineScope()`, `IAmALifetime` gains `PipelineScope`, and every implementation moves with them**
  - **USE COMMAND**: `/tidy-first add CreatePipelineScope to IAmAHandlerFactory and PipelineScope to IAmALifetime, moving every implementation in one change`
  - Files:
    - core: `IAmAHandlerFactory` (`:7`, today a bare marker), `IAmALifetime` (`:34`)
    - **6 classes in `src/`** — re-derived at HEAD and matching the ADR: `ServiceProviderHandlerFactory`, `ControlBusHandlerFactorySync` (`ControlBusHandlerFactory.cs`), `SimpleHandlerFactory`, `SimpleHandlerFactorySync`, `SimpleHandlerFactoryAsync`, and `HandlerLifetimeScope`
    - **22 test files** — re-derived at HEAD and matching the ADR: 16 `IAmAHandlerFactory` implementation files (the `QuickHandlerFactory`/`QuickHandlerFactoryAsync` doubles across the AWS, AWS.V4, Gcp, RMQ.Async, RMQ.Sync and RocketMQ suites, plus five in `Paramore.Brighter.Core.Tests`) and 6 `TestLifetimeScope` files, all in `tests/Paramore.Brighter.Extensions.Tests/`, none of which also carries a factory double
  - ⚠ **This must stay one commit**, for the same `netstandard2.0` reason as T1.3. Do not split by assembly or by suite
  - ⚠ **Get the file list from the tree**, as T1.3 does — the count checks the list rather than replacing it:

    ```sh
    grep -rlE ':[[:space:]]*(IAmAHandlerFactory|IAmAHandlerFactorySync|IAmAHandlerFactoryAsync)\b|:[[:space:]]*IAmALifetime\b' \
      tests --include="*.cs" | sort -u
    ```

    Verified at HEAD: returns exactly **22** files — the 16 carrying an `IAmAHandlerFactory` implementation and the 6 carrying a `TestLifetimeScope`, which are disjoint. The five in `Paramore.Brighter.Core.Tests` that the prose does not name individually are in that output
  - Only two implementations do more than answer `null`: `ServiceProviderHandlerFactory` (T2.3) and `HandlerLifetimeScope` (T2.2). `DummyHandlerFactory`, which implements the bare marker with no body, gains one. `Paramore.Brighter.ServiceActivator` gains no container dependency (NFR-3)
  - `IAmAScope`'s XML documentation gains a sentence about handler pipelines; `IAmALifetime`'s gains the reciprocal one NFR-8 requires — the XML documentation rides here, not in a separate task
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T1.14 (Phase 1 complete)
  - **References**: NFR-1(b), NFR-8; ADR 0071 step 1

- [ ] **TEST + IMPLEMENT: T2.2 — design-owed: `HandlerLifetimeScope` releases every tracked handler, then disposes the handle, and never throws**
  - **USE COMMAND**: `/test-first a handler release that throws still releases the remaining handlers, still disposes the pipeline scope last, and Dispose returns normally`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline"
  - Test file: `When_a_handler_release_throws_the_scope_should_still_release_the_rest.cs`
  - Test should verify:
    - three tracked handlers whose factory's `Release` throws on the **first**; a recording `IAmAScope` handle supplied by that factory's `CreatePipelineScope()` (both types are public core types, so the test needs no `InternalsVisibleTo` — the repository has none anywhere)
    - the other two are still released, **both tracking lists are cleared**, and the handle is disposed
    - **exactly one** `LogLevel.Error` record naming the failing release
    - `Dispose()` itself **returns normally** — it does not surface the failure composed; that is the transform family's shape and is rejected here
    - ⚠ **the ordering assertion, and this test is the only thing that carries it**: the factory ticks each `Release`, the handle ticks its `Dispose()`, and the handle's tick must be **last**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - take `IAmAScope? pipelineScope` on `HandlerLifetimeScope`'s constructor after the factory arguments, with the three existing constructors forwarding it, and expose it as `PipelineScope`
    - rewrite `Dispose()` to hold-and-compose **handler to handler**: today a throw from the first tracked handler skips every remaining `Release` *and* both `Clear()` calls (`HandlerLifetimeScope.cs:74-93`, no `try`/`catch` anywhere). Release every sync then every async handler catching per item; clear both lists unconditionally; dispose `PipelineScope` last and unconditionally, holding any failure
    - log every held failure at `LogLevel.Error` through two **new** members on the existing `Log` partial (`HandlerLifetimeScope.cs:95`) — `FailedToReleaseHandler` and `FailedToDisposePipelineScope` — and **throw nothing**. The four existing `Debug` members are unchanged
    - depend on ADR 0070 step 4b (T1.10): without the surfacing disposal path, `FailedToDisposePipelineScope` never fires because `ServiceProviderLifetimeScope.Dispose()` catches the failure and writes `FailedToDisposeScope` at `Warning`
  - ⚠ **Design-owed.** ADR 0071 states in terms that no acceptance criterion reaches the ordering and none can be written over Brighter's own types, because on the handle path Brighter's handler factory releases nothing at all
  - **Depends on**: T2.1, T1.10
  - **References**: FR-5, FR-6, FR-13; ADR 0071 steps 2, 6 (first required test)

- [ ] **TEST + IMPLEMENT: T2.3 — a handler pipeline scope is torn down at the end of `Send`, and a real owned `IServiceScope` was created for it**
  - **USE COMMAND**: `/test-first a Send disposes the handler's Scoped dependency by the time it returns and a second Send resolves a different instance`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_send_returns_the_handler_pipeline_scope_should_be_torn_down.cs`
  - Test should verify:
    - **not opted in** — no `IAmAScopeProvider` registered, which is FR-11's own precondition — and lifetime triple `{Scoped, Scoped, Scoped}`; a handler with a `Scoped` `IDisposable` dependency
    - by the time `Send` returns, that dependency has been disposed
    - a second `Send` resolves a **different** instance
    - both clauses fail unless a **real owned `IServiceScope`** was created: an implementation resolving from the root provider leaves the dependency undisposed and hands the second `Send` the same instance (this is what makes the criterion discharge FR-11(b), not only FR-7)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - have `PipelineBuilder.GetSyncInstanceScope()` (`:567`) and `GetAsyncInstanceScope()` (`:578`) ask the factory already in hand and pass the answer to the `HandlerLifetimeScope` constructor. Nothing else in `PipelineBuilder` changes in this ADR (step 3); `PipelineBuilder.Dispose()` (`:269-270`) needs no change and D10's end-of-publish release timing is preserved by construction
    - have `ServiceProviderHandlerFactory.CreatePipelineScope()` return a new `ServiceProviderPipelineScope` when `_handlerLifetime` is **not** `Singleton`, and `null` when it is — ⚠ **ADR 0070's null rule does not transfer**: a handler factory offers a handle for `Transient` too, because ADR 0067's per-resolution scope rides on the same `ServiceProviderLifetimeScope` (C-6)
    - have both `Create` overloads keep their `Singleton` branch on `_singletonScope` and otherwise resolve through `lifetime.PipelineScope` when it is a `ServiceProviderPipelineScope`, throwing `ConfigurationException` where it is `null` or unrecognised; **delete** `_lifetimeScopes` (`:40`), `GetOrCreateLifetimeScope` (`:127-131`) and `ReleaseLifetimeScope` (`:133-137`); both `Release` overloads (`:102-107`, `:120-125`) keep their signatures and **lose their bodies**, their XML documentation carrying the reason (step 4)
    - ⚠ **migrate the 26 facts in the same commit** — the solution does not compile otherwise. Re-derived at HEAD: **26 facts across the six files** ADR 0071 names (`FactoryLifetimeTests` 11, `FactoryErrorHandlingTests` 4, `FactoryThreadSafetyTests` 4, `When_two_handlers_share_a_lifetime_the_scope_follows_the_handler_lifetime` 4, `When_releasing_a_transient_disposable_handler_should_dispose_it_once` 2, `When_a_transient_handler_captures_the_service_provider_should_resolve_after_create` 1). Twenty-one configure a non-`Singleton` lifetime and must obtain a handle from `CreatePipelineScope()` and construct their `TestLifetimeScope` with it; four configure `Singleton` and are untouched beyond T2.1; one passes a `null` lifetime deliberately and becomes an assertion about `ConfigurationException`
    - ⚠ **`FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`FactoryLifetimeTests.cs:36`, re-derived ✓) and its async twin `AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`:154`, re-derived ✓) are AC-14's *"Explicitly NOT excluded"* pair. They are MIGRATED onto the handle path, not duplicated — the same two tests, asserting the same within-pipeline handler identity over the new carrier. That is an amendment to AC-14, which T6.21 carries**
  - **Depends on**: T2.2
  - **References**: AC-9 (FR-7, FR-11); ADR 0071 steps 3, 4, 6 (migration), amending AC-14

- [ ] **TEST + IMPLEMENT: T2.4 — a throwing handler still releases the pipeline scope, exactly once**
  - **USE COMMAND**: `/test-first a handler that throws still has its Scoped dependency disposed exactly once and the exception reaches the caller unchanged`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_handler_throws_the_pipeline_scope_should_still_be_released_once.cs`
  - Test should verify:
    - **not opted in**, lifetime triple `{Scoped, Scoped, Scoped}`; a handler with a `Scoped` `IDisposable` dependency whose `HandleAsync` throws `InvalidOperationException`
    - the caller observes `InvalidOperationException` — unchanged, not replaced
    - the dependency's `Dispose` was called **exactly once**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T2.2's `Dispose()` rewrite if that rewrite is correct. ⚠ Note that this AC's Given is a throwing **handler**, not a throwing `Release` — it guards FR-6's release-exactly-once and says nothing about a teardown that itself fails. AC-51 (T2.6) is the criterion for that, and the two read alike
  - **Depends on**: T2.3
  - **References**: AC-7 (FR-6); ADR 0071 step 2

- [ ] **TEST + IMPLEMENT: T2.5 — each `Publish` subscriber gets a distinct scope, all released at end of publish**
  - **USE COMMAND**: `/test-first a PublishAsync to three subscribers resolves three distinct Scoped IUnitOfWork instances all disposed by the time it returns`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_publishing_to_three_subscribers_each_should_get_its_own_scope.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`, **not opted in**; three handlers registered for `OrderPlaced`, each taking a `Scoped` `IUnitOfWork`
    - three **distinct** `IUnitOfWork` instances were resolved
    - all three were disposed **by the time `PublishAsync` returns** — released together at end of publish, which is today's behaviour and what FR-9's release-timing clause preserves (D10). This work does **not** tighten release to end-of-subscriber
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - require no change — this is the ADR 0039 regression guard. Each subscriber already gets its own `HandlerLifetimeScope` (`PipelineBuilder.cs:572`, `:583`); after T2.3 each of those holds its own handle, and `PipelineBuilder.Dispose()` drains them at end of publish from the `using var builder` at `CommandProcessor.cs:472`/`:575`
  - **Depends on**: T2.4
  - **References**: AC-10 (FR-8, D10, ADR 0039); ADR 0071 step 3

- [ ] **TEST + IMPLEMENT: T2.6 — a handler-factory `Release` that throws is logged and does not reach the caller (the one an implementation is most likely to fail)**
  - **USE COMMAND**: `/test-first a handler factory Release that throws is logged at Error and never replaces the handler's own exception`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_handler_factory_release_throws_it_should_be_logged_and_not_reach_the_caller.cs`
  - **Facts**: **4**, all in this one file — the handler that completes normally; ⚠ the handler whose `Handle` throws; the three tracked handlers whose first release throws; and the second `Send`. ⚠ The second is the one ADR 0071 step 6 names as *"the one an implementation is most likely to fail"* — a green on the first fact proves nothing about it. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`, **not opted in**; a handler factory whose `Release` throws `InvalidOperationException`
    - branch 1 — the handler **completes normally**: the caller observes normal completion and the handler's result unchanged, and a capturing `ILoggerProvider` for `Paramore.Brighter.*` records the release failure at `LogLevel.Error`
    - ⚠ branch 2 — a handler whose `Handle` throws `InvalidOperationException`: the caller observes **that** exception, **not** the release failure and **not** an `AggregateException` composing the two; the release failure appears only in the log at `Error`. **This is the clause that fails on the natural wrong implementation**: a teardown that rethrows replaces the handler's own exception, because the builder is disposed under `using var`
    - branch 3 — three tracked handlers whose factory's `Release` throws on the first: the other two are still released, the pipeline scope is still disposed, and exactly one `LogLevel.Error` names the failing release
    - branch 4 — a second `Send` in the same host succeeds identically: logged and swallowed, **not latched**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - be satisfied by T2.2's rule that `Dispose()` logs both failure kinds at `Error` and throws nothing. If a change is needed here, it is in `HandlerLifetimeScope.Dispose()`, never at a `CommandProcessor` call site
  - ⚠ **Risk-mitigation task.** ADR 0071 step 6 names this as *"the one an implementation is most likely to fail"*: an implementation that lets `Dispose()` throw passes every other test in this ADR and fails this one
  - **Depends on**: T2.5
  - **References**: AC-51 (FR-13, FR-5, FR-6); ADR 0071 steps 2, 6 (third required test)

- [ ] **TEST + IMPLEMENT: T2.7 — a disposal failure on a successful handler pipeline does not affect the result**
  - **USE COMMAND**: `/test-first a Send whose pipeline scope disposal throws returns the handler's result unchanged and logs at Error`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_successful_send_pipeline_scope_disposal_throws_the_result_should_be_unchanged.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}` and **not opted in**, so the pipeline scope is one Brighter created and owns (a borrowed scope cannot be used — Brighter never disposes one, FR-12; and the provider supplies no disposable scope, D17, which is why this discharges FR-13 and not FR-24)
    - the stated injection point: a container-`Scoped` dependency of the handler whose `Dispose()` throws `InvalidOperationException`, so releasing the owned pipeline scope throws
    - `Send` with a handler that completes normally: caller observes normal completion and the handler's result unchanged; a capturing `ILoggerProvider` for `Paramore.Brighter.*` records the disposal failure at `LogLevel.Error`
    - a second `Send` succeeds identically — logged and swallowed, **not latched**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - reach `HandlerLifetimeScope.Log.FailedToDisposePipelineScope` by the route ADR 0070 step 4b opened — it inherits that surfacing path and needs nothing of its own (T1.10)
  - **Depends on**: T2.6, T1.10
  - **References**: AC-33 (FR-13); ADR 0071 steps 2, 6 (second required test)

- [ ] **TEST + IMPLEMENT: T2.8 — bounded scope growth over sustained consumption**
  - **USE COMMAND**: `/test-first consuming ten thousand messages begins as many pipeline scopes as it releases with zero live at the end`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_consuming_ten_thousand_messages_scopes_begun_should_equal_scopes_released.cs`
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; 10,000 messages consumed
    - the number of pipeline scopes **begun equals** the number **released**, and **zero live** after the last message
    - the count of scopes begun equals the count of **pipelines**, not the count of resolved instances (NFR-6)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need no new production code — it is the counting guard over both families. It is placed here rather than in Phase 1 because only after T2.3 do **both** families reach their scope through `CreatePipelineScope()`/`ServiceProviderPipelineScope`, so one instrument counts both and the count is stable
    - carry NFR-6's begin/release-count half together with AC-37 clause 3 (T6.16); AC-13's fake provider cannot observe it, because under D11 the container package creates and disposes those scopes
  - **Depends on**: T2.7
  - **References**: AC-23 (NFR-5, NFR-6); ADR 0070 step 9a, ADR 0071 step 5

---

## Phase 3 — core types and the affinity option (ADR 0072 step 1, ADR 0075 step 1, ADR 0076 steps 1–3)

This phase declares types and moves a registration. It has **no acceptance criterion of its own**: every AC that would exercise adoption needs the protocol (Phase 4) or the extension (Phase 6). Two contract-level tests and one design-owed write-through test carry it, and each is traced to an FR.

- [ ] **STRUCTURAL: T3.1 — add `IAmAScopeProvider`, `ScopeAffinity` and `AmbientScopeSourceException` to core**
  - **USE COMMAND**: `/tidy-first add the IAmAScopeProvider seam interface, the ScopeAffinity enum and AmbientScopeSourceException to Paramore.Brighter`
  - Files: `src/Paramore.Brighter/IAmAScopeProvider.cs`, `src/Paramore.Brighter/ScopeAffinity.cs`, `src/Paramore.Brighter/AmbientScopeSourceException.cs` (all new)
  - `IAmAScope? GetAmbient(ScopeAffinity affinity)` — the member spelling is a working name that ADR 0073 keeps; the contract is fixed by D17. `AlwaysNew = 0` so that `default(ScopeAffinity)` is the safe value. Names are settled by D4 and may not be changed
  - None names a container type, so AC-22 clause 3 (T1.2) returns nothing new
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T2.8 (Phase 2 complete)
  - **References**: FR-10, D4, D17, C-8; ADR 0072 step 1

- [ ] **TEST + IMPLEMENT: T3.2 — `AmbientScopeSourceException` never carries a null inner exception**
  - **USE COMMAND**: `/test-first constructing an AmbientScopeSourceException with a null inner exception throws ArgumentNullException`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline"
  - Test file: `When_an_ambient_scope_source_exception_is_constructed_with_no_inner_it_should_throw.cs`
  - **Facts**: **2**, both in this one file — the null argument, which must **throw** `ArgumentNullException`; and a non-null one, whose `InnerException` must be the exception passed. One fact throws and one does not, so they cannot share a `[Fact]`. Gate once, on the first fact
  - Test should verify:
    - `new AmbientScopeSourceException(null!)` throws `ArgumentNullException`
    - a constructed instance's `InnerException` is the exception passed and is never null
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - validate in the constructor. ⚠ **The never-null invariant is load-bearing, not incidental**: it is what licenses `e.InnerException!` at the **six** builder `catch` sites T4.2 adds. The type is `public` in a `netstandard2.0` assembly, so a nullable-oblivious consumer reaches the constructor with no compiler diagnostic, and any factory in a third-party container package must construct one (NFR-7)
  - **Depends on**: T3.1
  - **References**: FR-24.1, NFR-7; ADR 0072 step 1 (*the courier* contract table)

- [ ] **TEST + IMPLEMENT: T3.3 — `AmbientScopeSuppression` carries one bit along a logical flow and restores it on dispose**
  - **USE COMMAND**: `/test-first AmbientScopeSuppression reports false outside a bracket, true inside one, restores the captured value on dispose and nests correctly`
  - Test location: "tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline"
  - Test file: `When_suppression_brackets_are_nested_they_should_restore_the_captured_value.cs`
  - **Facts**: **4**, all in this one file — a reader outside any bracket; a lexically nested pair restoring the **captured** value on dispose; a bracket disposed **twice**; and ⚠ the **flow-branch** fact, where a bracket is taken on one flow and read on a branched one. The last is a different arrangement from the other three — it has to branch the flow before asserting — and it is the one that pins `AsyncLocal<bool>` semantics rather than a plain field. Gate once, on the first fact
  - Test should verify:
    - `IsSuppressed` is `false` for a reader outside any bracket, and never throws
    - inside `Suppress()` it is `true`; on dispose the **captured** value is restored, so lexically nested brackets nest correctly
    - disposing a bracket twice is a no-op
    - a bracket taken on one flow and a value read on a branched flow behaves as `AsyncLocal<bool>` does — a write after a flow has branched does not reach it
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - add `public static class AmbientScopeSuppression` to `src/Paramore.Brighter/`, backed by a private `static readonly AsyncLocal<bool>`; `Suppress()` captures, sets `true`, and returns an idempotent bracket. It names no container type, so AC-22 clause 3 is untouched
    - carry `<remarks>` stating that the member is **not intended for direct application use**, why it is nevertheless public (`Paramore.Brighter.ServiceActivator`, a container package honouring FR-8 under NFR-7, and Brighter's own tests all live in separate assemblies, and this repository uses no `InternalsVisibleTo`), and **both misuse modes**: disposing a bracket on a flow other than the one that took it, and disposing brackets out of order. Neither is detected, and neither is reachable from Brighter's own three lexical brackets
  - **Depends on**: T3.1
  - **References**: FR-8, FR-9, NFR-4, D6; ADR 0075 step 1

- [ ] **STRUCTURAL: T3.4 — `IBrighterOptions` and `BrighterOptions` gain `DefaultScopeAffinity`**
  - **USE COMMAND**: `/tidy-first add the DefaultScopeAffinity property to IBrighterOptions and BrighterOptions defaulting to AlwaysNew`
  - Files: `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterOptions.cs` (`:9`) and its `IBrighterOptions` interface
  - `ScopeAffinity DefaultScopeAffinity { get; set; }` on the interface, `= ScopeAffinity.AlwaysNew` on the class. `ConsumersOptions : BrighterOptions` (`ConsumersOptions.cs:10`) inherits it with no separate work, and it is settable in an `AddConsumers` delegate
  - It is **not** a compatibility flag and does not interact with `IsolateTransientHandlerScope` (`BrighterOptions.cs:37`), whose domain is `Transient` only
  - Adding the member is a source and binary break for a hand-rolled implementation; re-derived at HEAD: **one** implementation in `src/` (`BrighterOptions`) and **none** in `tests/`, so nothing in this repository breaks. Release-noted by T7.14
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T3.1 (the DI package cannot name `ScopeAffinity` until core declares it)
  - **References**: FR-14, FR-15, C-9 (settled), AC-24 (general clause); ADR 0076 step 1

- [ ] **STRUCTURAL: T3.5 — add `ScopeAffinityOverride`, `BrighterOptionsRegistration` and `RegisterBrighterOptions`, uncalled**
  - **USE COMMAND**: `/tidy-first add ScopeAffinityOverride, BrighterOptionsRegistration and the private RegisterBrighterOptions helper to the DI package without calling it`
  - Files: `src/Paramore.Brighter.Extensions.DependencyInjection/ScopeAffinityOverride.cs` (new, **public**), `BrighterOptionsRegistration.cs` (new, **internal**), `ServiceCollectionExtensions.cs` (the new `private static RegisterBrighterOptions`, beside `BrighterHandlerBuilder` at `:142`)
  - `ScopeAffinityOverride`'s `<remarks>` must state the registration obligation it places on every registrar: a **constructed instance** under a plain `AddSingleton` — never `TryAdd*`, which would make the first call win the affinity while the last wins the provider, and never a factory delegate, whose descriptor carries no instance for validation to read (this is what T7.9's rule reports on)
  - `BrighterOptionsRegistration` carries only the identity of the `IBrighterOptions` descriptor `RegisterBrighterOptions` added — no affinity and no options object. Identity is **reference equality against the descriptor object `services.Add` received**, which survives ADR 0074's snapshot because that snapshot copies the *list*, not the descriptors
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T3.4
  - **References**: FR-17, FR-22.4, D18; ADR 0076 step 2

- [ ] **TEST + IMPLEMENT: T3.6 — design-owed: a registered affinity override reaches the object the factories read, on all four registration paths**
  - **USE COMMAND**: `/test-first a registered ScopeAffinityOverride is applied to the resolved IBrighterOptions on all four Brighter registration entry points`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_an_affinity_override_is_registered_it_should_reach_the_resolved_brighter_options.cs`
  - **Facts**: **8**, all in this one file — the four registration entry points × the two directions (the override reaching the resolved options, and the falsifiable case where it does not). Gate once, on the first fact
  - Test should verify:
    - four hosts, each using **exactly one** entry point — `AddBrighter(Action<BrighterOptions>)`, `AddBrighter(Func<IServiceProvider, BrighterOptions>)`, `AddConsumers(Action<ConsumersOptions>)` alone, and `AddConsumers(Func<IServiceProvider, ConsumersOptions>)` alone (all four route through `BrighterHandlerBuilder`, so each is a complete host); each setting lifetime triple `{Scoped, Scoped, Scoped}` and each registering `new ScopeAffinityOverride(ScopeAffinity.JoinAmbient)` by hand under a plain `AddSingleton`
    - in all four, `DefaultScopeAffinity` on the resolved `IBrighterOptions` — **the object the factories read** (`ServiceProviderMapperFactory.cs:44`) — is `JoinAmbient`, even on the three paths that run no `IOptions` pipeline (C-12a)
    - ⚠ the **falsifiable direction**: each host first sets `DefaultScopeAffinity = JoinAmbient` by whatever means its entry point supports, then registers an override carrying `AlwaysNew`, and the resolved value is `AlwaysNew`. A Then written against a fresh host would also pass on an implementation that dropped the override, because `AlwaysNew` is the property's own default
    - ⚠ **which `AddConsumers` overload and in what order** (C-12): the two consumer hosts here use each overload **alone**, so the `Func` overload's `InvalidCastException` hazard (`ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:89-90`) is not walked into
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - call `RegisterBrighterOptions(services, optionsFunc)` from `BrighterHandlerBuilder` (`:142`) and **delete the four site registrations in one commit** — `Extensions.DependencyInjection/ServiceCollectionExtensions.cs:74` and `:97`, and `ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:38` and `:88`. Doing half leaves a path registering `IBrighterOptions` *before* `BrighterHandlerBuilder` runs, which wins the guard and silently drops the override on that path — the failure mode FR-17 exists to prevent
    - correct `:77-79`'s circular `optionsFunc` lambda to `sp => sp.GetRequiredService<IOptions<BrighterOptions>>().Value`; `:38` becomes a factory delegate. ⚠ The ServiceActivator package's `:39` (`TryAddSingleton<IAmConsumerOptions>(options)`) and `:89-90` are explicitly **not** touched
    - ⚠ keep the `ServiceKey is null` clause in the spelled-out `TryAdd` guard. `TryAdd` matches on `ServiceType` **and** `ServiceKey`; a `ServiceType`-only guard would give a host with a keyed `IBrighterOptions` **no descriptor at all**, failing at first resolution from `BuildCommandProcessor` (`:708`) rather than where the mistake was made, and would make T7.6's rule report an `Error` against an application that did nothing wrong
    - raise `InvalidOperationException` on a `null` return from `optionsFunc`, and read the override with `GetService` (absence is the ordinary case and must be silent, FR-15)
  - ⚠ **Design-owed for this phase.** AC-45 asserts the same over the ASP.NET **extension** and lands at T6.9; this task is the write-through half, which is all that can compile before Phase 6
  - **Depends on**: T3.5
  - **References**: FR-17 (all-four-paths clause), FR-14, C-12, C-12a, D18; ADR 0076 step 3 (AC-45 completes it at T6.9)

---

## Phase 4 — ADR 0072: the ambient scope adoption seam

- [ ] **STRUCTURAL: T4.1 — one spelling for the two `PipelineBuilder` catch filters**
  - **USE COMMAND**: `/tidy-first normalise the two PipelineBuilder catch filters to one spelling`
  - Files: `src/Paramore.Brighter/PipelineBuilder.cs` — re-derived at HEAD: `:202` reads `catch (Exception e) when (e is not ConfigurationException)` and `:248` reads `catch (Exception e) when(!(e is ConfigurationException))`. **Both anchors and both spellings match the ADR**
  - ADR 0072's own words: *"Normalising them changes no behaviour and belongs in its own commit ahead of the behavioural change, per Tidy First. Doing it first also means the clause added below is added twice to the same shape"*
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T3.6 (Phase 3 complete)
  - **References**: ADR 0072 step 1a

- [ ] **TEST + IMPLEMENT: T4.2 — a throwing ambient source surfaces to the caller unwrapped, on both a `Send` and a `Post`**
  - **USE COMMAND**: `/test-first a scope provider whose GetAmbient throws surfaces that exception unwrapped from both Send and Post with no leaked scope`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_ambient_query_throws_the_caller_should_see_it_unwrapped.cs`
  - **Facts**: **2**, both in this one file and **both in the same host** — the `Send`, whose handler pipeline consults the ambient source; and the `Post`, whose transform pipeline does. ⚠ The task states why the second is not redundant: **the two verbs build different pipelines whose builders differ in what they clean up**, so without it an implementation that leaked the pipeline scope on the transform-build path would pass. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}` — so **both** the `Send`'s handler pipeline and the `Post`'s transform pipeline consult the ambient source (FR-27.1) — and an `IAmAScopeProvider` whose `GetAmbient` throws `InvalidOperationException`
    - `Send`: the caller observes that `InvalidOperationException` **unwrapped** (not a `ConfigurationException`), and no pipeline scope is leaked
    - ⚠ `Post` **in the same host**: the same exception, unwrapped, and no scope leaked. **Without this second branch an implementation that leaked the pipeline scope on the transform-pipeline build path would pass**, because the two verbs build different pipelines whose builders differ in what they clean up
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - ⚠ **land the minimal ask this test needs.** No earlier task provides one: Phase 3 only *declares* `IAmAScopeProvider` (T3.1) and pins its courier's contract (T3.2); nothing resolves a provider or calls `GetAmbient`, so without this the test cannot go red for the stated reason. Resolve `_scopeProvider` **once, in the constructor** in each of the five container-backed factories — from the root `IServiceProvider` each already receives, which `ServiceProviderHandlerFactory` already holds (`:36`) and the other four gain — and make an **unconditional** `GetAmbient` call inside `CreatePipelineScope()`. ⚠ Unconditional **only until T4.3**, which puts the affinity computation and the ladder in front of it. Nothing existing moves: with no provider registered the field is null and the ask is unreachable, which is why every Phase 1–3 test stays green
    - wrap a throw from the ask in `AmbientScopeSourceException` inside `CreatePipelineScope()`, and add a clause **ahead of** each existing wrapping `catch` at all **six** sites — `PipelineBuilder.cs:202` and `:248`, `TransformPipelineBuilder.cs:116` and `:157`, and the same two lines in `TransformPipelineBuilderAsync` (all re-derived ✓) — rethrowing the inner exception through `ExceptionDispatchInfo.Capture(...).Throw()`
    - differ per family before rethrowing: the four transform-builder clauses call `CleanUpQuietly` (T1.4's named method); `PipelineBuilder`'s two call **nothing**, because those catches run no cleanup. What discharges the `Send` branch's *no scope leaked* conjunct is `PipelineBuilder.Dispose()` (`:269-270`) firing from the `using var builder` at each of `CommandProcessor`'s four dispatch sites, which this ADR does not disturb
    - leave the general clause's behaviour for every other failure untouched (AC-5 must keep passing), and add the new type to both `PipelineBuilder` filters' exclusions
  - **Depends on**: T4.1, T3.2
  - **References**: AC-30 (FR-24.1); ADR 0072 steps 1b, 2

- [ ] **STRUCTURAL: T4.2a — land `AmbientScopeDiagnostics` and its registration, inert**
  - **USE COMMAND**: `/tidy-first add the AmbientScopeDiagnostics container-scoped singleton and register it in BrighterHandlerBuilder, with no caller`
  - Files: `src/Paramore.Brighter.Extensions.DependencyInjection/AmbientScopeDiagnostics.cs` (new); `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` (`:142`, `BrighterHandlerBuilder` — the single registration point all four entry points route through, ADR 0072 step 5)
  - The type, its three `Condition` values (*no ambient offered*, *ambient offered but unusable*, *ambient offered for an `AlwaysNew` ask and ignored*) and the `WarnOnce(condition, providerImplementationType)` member are declared and registered `TryAddSingleton`. **Nothing calls it in this commit** — the same shape T3.5 uses for `RegisterBrighterOptions`
  - ⚠ **Why this is separated out.** T4.3 implements canonical ladder **row 5**, whose outcome is *ignore and warn*, so the type must exist before T4.3 — not after it. Registering it here also keeps the registration in **one** task: T4.4 registers `ScopedArtefactCache` beside it and must not re-register this
  - Each of the five container-backed factories holds the singleton **nullable**, so a factory constructed by hand over a provider that never ran `AddBrighter` makes `WarnOnce` a no-op rather than a null dereference
  - Latch semantics — the atomic `ConcurrentDictionary<(Condition, Type), byte>.TryAdd`, per-container and never `static` — are **behaviour and land in T4.5**, gated by AC-31. This commit may leave `WarnOnce` a stub that logs unconditionally; no test asserts it yet
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T4.2
  - **References**: FR-24.2, FR-24.4, FR-23, D19; ADR 0072 steps 2, 5, *`AmbientScopeDiagnostics`*

- [ ] **TEST + IMPLEMENT: T4.3 — the ambient query is made only for `Scoped` pipelines, and a pipeline mixing `Scoped` with `Transient` declines to adopt**
  - **USE COMMAND**: `/test-first a recording scope provider sees zero asks for an all Transient host and one AlwaysNew ask for a mixed Scoped and Transient transform pipeline`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_no_participating_factory_is_scoped_the_ambient_source_should_not_be_asked.cs`
  - **Facts**: **2**, both in this one file — the all-`Transient` host executing a `Send`, a `Publish` with three subscribers and a `Post`; and the `{Transient, Scoped, Transient}` host executing one `Post` from within an ambient. ⚠ The first is a **negative** fact — the recorder's zero asks *is* the assertion — so a green on the second discharges nothing. Gate once, on the first fact
  - Test should verify:
    - the recording `IAmAScopeProvider` (implemented in a test assembly with **no reference to `Microsoft.Extensions.DependencyInjection`**, recording every `GetAmbient(ScopeAffinity)` call and the affinity it carried), the affinity option `JoinAmbient`, and `ValidatePipelines()` **not** called — the second configuration is an FR-22.2 violation and this AC pins what the seam does when validation was never run (C-15)
    - ⚠ **the negative branch**: a host with `{Transient, Transient, Transient}` executing one `Send`, one `Publish` with three subscribers and one `Post` records **zero** adoption decisions and no pipeline scope taken. **The recorder's zero asks *is* the second assertion** — it must **not** be asserted over any scope object the implementation hands the pipeline, because a `Transient` handler pipeline holds a handle for ADR 0067's per-resolution isolation and `lifetime.PipelineScope is null` would be testing the wrong thing and would fail
    - a host with `{HandlerLifetime = Transient, MapperLifetime = Scoped, TransformerLifetime = Transient}` executing one `Post` from within an available ambient, **using a mapper that declares no `[WrapWith]`/`[UnwrapWith]` transform at all**: exactly **one** decision for that transform pipeline carrying **`AlwaysNew`** — the ask is made despite the affinity, which is what makes the decline observable (D16) — and the mapper's container-`Scoped` dependency is **not** the ambient's instance
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - add `ScopeAffinityPolicy` (DI package, internal), holding all three lifetimes off `IBrighterOptions`. Each of the five container-backed factories keeps the policy instead of only its own lifetime; the five constructor reads are `ServiceProviderMapperFactory.cs:44-45`, `ServiceProviderMapperFactoryAsync.cs:45-46`, `ServiceProviderTransformerFactory.cs:44-45`, `ServiceProviderTransformerFactoryAsync.cs:45-46`, `ServiceProviderHandlerFactory.cs:49-50`
    - implement the participating set **structurally** (D12): transform = `{MapperLifetime, TransformerLifetime}` **always**, whether or not the mapper declares a transform and whether or not a transformer factory instance exists; handler = `{HandlerLifetime}` alone
    - implement **canonical ladder rows 1, 2, 3, 5 and 6** — ⚠ the numbering is the **ten-row table** under ADR 0072's *The mechanism, end to end* (`0072…md:153-165`), **never** the six-step pseudo-code block in its step 2, which renumbers the same decisions and says so inline (`4. if (_scopeProvider is null) return OWNED // ladder row 3`). Row 1 the asked factory's own lifetime; row 2 the handler family's `Transient` handle with **no ask**; row 3 no provider registered — **OWNED, no ask, no diagnostic** (FR-11(a)); row 5 an ambient returned for an ask that did not carry `JoinAmbient` — **ignored before it is probed, never disposed, and warned once**; row 6 such an ask that returned nothing — **OWNED, no diagnostic**
    - ⚠ **the affinity computation is not a ladder row.** It sits between rows 3 and 5, ahead of the ask (the `AmbientScopeSuppression.IsSuppressed` read is **ADR 0075's edit and lands in T5.2**, not here)
    - the ladder's other rows belong to other tasks and must not be implemented here: **row 4** (the source throws) is T4.2's, **row 7** (`JoinAmbient`, nothing came back) T4.5's, **rows 8 and 9** (foreign role type; failed probe) T4.6's and T4.7's, and **row 10** (BORROWED) T4.4's
    - test for `JoinAmbient` **positively**, never for `AlwaysNew` with everything else treated as adoption, so an out-of-range cast enum value degrades to `AlwaysNew` (ADR 0076 places that obligation here)
    - ⚠ **not** re-introduce the `_scopeProvider` field or the root-provider field — **T4.2** already resolved both, once in each factory's constructor. What this task does is **replace T4.2's unconditional ask** with the affinity computation and the ladder in front of it; the two fields stay exactly as T4.2 left them
  - **Depends on**: T4.2a (which lands `AmbientScopeDiagnostics`, without which row 5 cannot warn)
  - **References**: AC-46 (FR-27.1, FR-27.2, D12, D16); ADR 0072 steps 2, 3

- [ ] **TEST + IMPLEMENT: T4.4 — the seam admits a non-ASP.NET ambient, and adoption works over it**
  - **USE COMMAND**: `/test-first an AsyncLocal backed scope provider in a console host lets a Send adopt the ambient scope and a Send outside it create and dispose its own`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_non_aspnet_provider_offers_an_ambient_the_pipeline_should_adopt_it.cs`
  - Test should verify:
    - a test-assembly `IAmAScopeProvider` holding its ambient in an `AsyncLocal` and **not referencing `Microsoft.AspNetCore.*`**, registered in a **console** host with lifetime triple `{Scoped, Scoped, Scoped}` and the affinity option `JoinAmbient`
    - the first `Send`, made with an ambient established, resolves the ambient's `Scoped` instance and **does not dispose it**
    - the second `Send`, made outside the ambient, creates, uses and disposes its own
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - add `IAmAServiceProviderScope : IAmAScope` with `IServiceProvider Services` to the **DI package** (it names `IServiceProvider`, so core is the one place it cannot go), as a **role interface** any assembly can implement
    - add `AmbientScopeProbe` (internal static) with one member `bool CanResolveFrom(IAmAServiceProviderScope, IServiceProvider root)`, shared by all five factories
    - add `ScopedArtefactCache` (DI package, public), registered `TryAddScoped`, holding the per-type artefact dictionary. ⚠ `ServiceProviderLifetimeScope.cs:49`'s private `_scopedInstances` field **becomes a resolution of this service** — borrowed resolves it from `src.Services` (one per request scope), owned from the `IServiceScope` `EnsureRootScopePublished()` (`:185`) just created (one per pipeline, exactly today's behaviour and what AC-1 requires). One mechanism, both cases; it is FR-26's recommended mechanism
    - add the **internal borrowed construction path** on `ServiceProviderPipelineScope` with **non-owning disposal**: `Dispose()` and `DisposeAsync()` are idempotent no-ops, and Brighter disposes neither the provider, nor the ambient `IAmAScope`, nor any instance resolved from it (FR-12, C-7)
    - type-test for the **interface**, never a class: `if (ambient is IAmAServiceProviderScope src)`. ⚠ An ambient that does **not** implement the role is **ignored, not rejected** — the factory declines and creates its own scope, and the declined `IAmAScope` is not disposed. That is a different rule from ADR 0071's *reject an unrecognised `IAmALifetime.PipelineScope`*, and the two do not read alike: decline where a fallback exists, throw where none does
    - register `ScopedArtefactCache` (`TryAddScoped`) in `ServiceCollectionExtensions.BrighterHandlerBuilder` (`:142`), the single registration point (step 5). ⚠ **`AmbientScopeDiagnostics` is already registered there by T4.2a** — do not register it a second time
    - register `IAmAScopeProvider` with a **plain `AddSingleton`** on every path and never `TryAddSingleton`, so every duplicate descriptor survives for T7.7's rule while MS DI resolves the last unkeyed one
  - **Depends on**: T4.3
  - **References**: AC-35 (NFR-7), FR-12, FR-16(a), FR-26, C-7; ADR 0072 steps 2, 2a, 3a, 4, 5

- [ ] **TEST + IMPLEMENT: T4.5 — a null-returning ambient query is treated as "no ambient", and warns once per Brighter container**
  - **USE COMMAND**: `/test-first a scope provider returning null on a JoinAmbient ask behaves as the unregistered case and warns exactly once`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_ambient_query_returns_null_it_should_be_treated_as_no_ambient.cs`
  - **Facts**: **2**, both in this one file — the `JoinAmbient` host, whose two `Send` calls record **exactly one** `Warning` between them; and ⚠ the **second host of the same shape** under `AlwaysNew`, which records **no `Warning` at all**. The second is what stops an implementation warning on every `AlwaysNew` ask from passing. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`, the affinity option **`JoinAmbient`**, and an `IAmAScopeProvider` whose `GetAmbient` returns `null`
    - two `Send` calls both succeed with behaviour identical to the unregistered case (FR-11), and a capturing `ILoggerProvider` records **exactly one** `LogLevel.Warning` naming the **no ambient offered** condition and the provider's implementation type
    - ⚠ **a second host of the same shape** — a fresh Brighter container, so the latch starts unlatched (D19) — registering the **same** provider implementation type with the affinity option **`AlwaysNew`**: the two `Send` calls behave identically but the capturing provider records **no `Warning` at all**. Without this branch an implementation that warns on every `AlwaysNew` ask — on the default affinity, and on FR-25.11's register-without-opting-in gesture — would pass
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - ⚠ **not** add or register `AmbientScopeDiagnostics` — **T4.2a** landed the type and its registration inert. This task adds the **behaviour**: the *no ambient offered* condition and the latch that makes it fire once
    - make `WarnOnce(condition, providerImplementationType)` **atomic** — a single `ConcurrentDictionary<(Condition, Type), byte>.TryAdd` whose return value decides whether to log. Check-then-set would let three concurrent `Publish` subscribers log two or three times
    - keep the latch on the **Brighter container** (the host's root provider) and **never a `static`** (D19). ⚠ A process-static latch makes this AC's `AlwaysNew` branch vacuous and AC-11's third branch unsatisfiable by a correct implementation
    - hold the diagnostics singleton **nullable** in each factory, so a factory constructed by hand over a provider that never ran `AddBrighter` makes `WarnOnce` a no-op rather than a null dereference
    - name the condition in each message in terms a capturing `ILoggerProvider` can discriminate on — naming only the provider type is insufficient, because all three conditions do that
  - **Depends on**: T4.4
  - **References**: AC-31 (FR-24.2, FR-18, D19); ADR 0072 steps 2, *`AmbientScopeDiagnostics`*

- [ ] **TEST + IMPLEMENT: T4.6 — a stale ambient is declined, not surfaced**
  - **USE COMMAND**: `/test-first a provider offering a disposed resolution source is declined with one warning and no ObjectDisposedException reaches the caller`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_an_offered_ambient_is_stale_it_should_be_declined_and_reported_once.cs`
  - Test should verify:
    - an opted-in host (lifetime triple `{Scoped, Scoped, Scoped}`, affinity `JoinAmbient`) registering a provider that **captures** a resolution source and keeps offering it after that source's DI scope has been disposed — the shape AC-35's provider establishes, and the only shape that reaches this rule
    - `Send`: **no `ObjectDisposedException` from Brighter's own resolution** reaches the caller; the handler's `Scoped` dependency is a fresh instance disposed when `Send` returns
    - **exactly one** `LogLevel.Warning` naming the **ambient offered but unusable** condition and the provider's implementation type, **and no entry naming either of FR-24's other two conditions** — FR-23 is the more specific rule and FR-24.2 does not also fire
    - a second `Send` in the same host behaves identically and records **no further** `Warning`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - probe **before resolving any pipeline instance** from the ambient, at ladder row 9, and treat a failed probe exactly as "no ambient" **for adoption and scope creation only** — the diagnostic stays FR-23's
    - implement the probe's **five failed outcomes**: a `null` `Services`, a `null` `IServiceScopeFactory`, a `null` `ScopedArtefactCache`, a `Services` reference-equal to `root`, and **any exception** from reading `Services` or from either resolution, `ObjectDisposedException` among them
    - evaluate FR-23 on **`JoinAmbient` asks only**; an `AlwaysNew` ask never probes, because an ambient offered for one is ignored first (evaluation order: FR-24.4, then FR-23, then FR-24.2)
    - carry the same *offered but unusable* diagnostic for the **role-type decline at row 8**, which has no criterion at all — this is a deliberate extension of FR-23's text, recorded as such
  - **Depends on**: T4.5
  - **References**: AC-29 (FR-23, D19); ADR 0072 steps 2, 2a, 2b

- [ ] **TEST + IMPLEMENT: T4.7 — an ambient that names the root provider is declined, not borrowed from**
  - **USE COMMAND**: `/test-first a scope provider offering the root service provider is declined and each Send resolves its own scope`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_an_offered_ambient_names_the_root_provider_it_should_be_declined.cs`
  - Test should verify:
    - an opted-in host (lifetime triple `{Scoped, Scoped, Scoped}`) whose registered `IAmAScopeProvider` offers a resolution source naming the **root `IServiceProvider` it was itself constructed with**
    - ⚠ **the container is built WITHOUT `ValidateScopes`, deliberately** — it is the only configuration in which the borrow would otherwise succeed, since scope validation makes the probe's own resolution throw and the ambient is then declined for the wrong reason. Do not "harden" this host by turning validation on
    - two `Send` calls in two different requests each resolve the handler's container-`Scoped` dependency from a scope **Brighter created and owns**: the two calls yield **different** instances, each disposed when its own `Send` returns, and neither survives into the other
    - **exactly one** `LogLevel.Warning` across both calls naming the **ambient offered but unusable** condition and that provider's implementation type, and **no entry naming either of FR-24's other two conditions**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - implement the probe's **reference-identity** test against the root provider the calling factory holds. It tests identity, not capability, and it exists because the root answers all three of the other tests: without `ValidateScopes` a `Scoped` service resolves from it and returns one process-wide instance disposed by nothing, defeating FR-1 and FR-2
    - accept the residue rather than widening: `IHost.Services` (the object `BuildServiceProvider()` returns) and wrappers over either are **not** caught, and the contract table forbids them rather than the test detecting them
  - **Depends on**: T4.6
  - **References**: AC-54 (FR-23, D19); ADR 0072 steps 2a, 2d

- [ ] **TEST + IMPLEMENT: T4.8 — design-owed: an ambient disposed after the probe surfaces as a `ConfigurationException` naming the provider**
  - **USE COMMAND**: `/test-first an ambient disposed while a pipeline is still resolving from it surfaces a ConfigurationException naming the provider type`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_borrowed_ambient_is_disposed_mid_pipeline_it_should_surface_a_configuration_error.cs`
  - **Facts**: **2**, both in this one file — the `Send`, which sees the `ConfigurationException` **thrown directly**, and the `Post`, which sees it as the **inner** exception of the transform builder's own `ConfigurationException`. ⚠ The two differ because `PipelineBuilder`'s filters exclude `ConfigurationException` and the transform builder's four catches carry no filter — the same fault, two shapes at the caller, so one fact cannot assert both. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`, affinity `JoinAmbient`, an ambient that passes the probe and is then disposed by its owner before a later `Create`
    - the caller sees a `ConfigurationException` whose message names *the ambient offered by `<provider implementation type>` was disposed while a pipeline was resolving from it*, carrying the `ObjectDisposedException` as its **inner** exception
    - `Send` sees it thrown directly (both `PipelineBuilder` filters exclude `ConfigurationException`); `Post` sees it as the **inner** exception of the transform builder's own `ConfigurationException` (those four catches carry no filter)
    - **nothing is latched** — this is a fault, not a declined adoption, and the three diagnostics are about declines
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - put the translation on `ServiceProviderPipelineScope`'s **borrowed** `Create` path — one site covers every caller, because all five factories reach the borrowed provider through this handle
    - **not** re-probe before every resolution: a test and a use cannot be made atomic against an owner disposing in between, so per-resolution probing costs on every resolution and still leaves the window open
    - leave the owned path untouched — there is nothing to translate there
  - ⚠ **Design-owed.** ADR 0072 step 2d records this as a genuine window that adoption creates and FR-23 leaves no room to accept; no acceptance criterion reaches it
  - **Depends on**: T4.7
  - **References**: FR-23, FR-12; ADR 0072 steps 2d, 4

- [ ] **TEST + IMPLEMENT: T4.9 — design-owed: the `Scoped` artefact cache evicts a faulted resolution instead of publishing it**
  - **USE COMMAND**: `/test-first a Scoped artefact resolution that throws is not remembered and a later resolution of the same type resolves again`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_scoped_artefact_resolution_throws_the_cache_should_not_retain_the_fault.cs`
  - **Facts**: **3**, all in this one file — the faulted-then-successful resolution on the **owned** path; the same on the **borrowed** path (the task's *"one protocol, not two"* is the assertion, and it is only falsifiable if both are run); and ⚠ the **concurrency** fact — concurrent first-resolvers producing one instance with the losers seeing the winner's (NFR-4), which carries the losing-waiter clause that its removal must not delete a **healthy** `Lazy` published in between. The third needs a different arrangement from the first two. Gate once, on the first fact
  - Test should verify:
    - lifetime triple `{Scoped, Scoped, Scoped}`; a resolution that throws on first call and succeeds on the next
    - the exception propagates to **every** waiter on the first resolution, and a later resolution of the same type **in the same scope** resolves again rather than rethrowing a remembered failure
    - asserted on **both** the owned path and the borrowed path — one protocol, not two
    - concurrent first-resolvers of one type produce **one** instance and the losers see the winner's (NFR-4)
    - a losing waiter's removal is a no-op — it must not delete a **healthy** `Lazy` a concurrent resolver published in between
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - use `ConcurrentDictionary<Type, Lazy<object?>>` with the publish protocol `ServiceProviderLifetimeScope.cs:163-178` already uses, with one change: a faulted entry is removed before the exception propagates
    - ⚠ remove by the **observed pair**, not the key: `((ICollection<KeyValuePair<Type, Lazy<object?>>>)_cache).Remove(new KeyValuePair<Type, Lazy<object?>>(type, observedLazy))`. `TryRemove(type, out _)` can delete a healthy entry, and `ConcurrentDictionary.TryRemove(KeyValuePair)` is **absent from `netstandard2.0`**, one of the DI package's four targets (`src/Directory.Build.props:43`, re-derived: `netstandard2.0;net8.0;net9.0;net10.0`). Matching is by reference, because `Lazy<T>` does not override `Equals`
    - leave `GetOrCreateSingleton` (`:152`) and `_singletonInstances` **alone** — this closes the `Scoped` half of #4260 and no more. "Both" means both *paths*, not both *methods*
  - ⚠ **Design-owed**, and a release-noted behavioural break reaching a host that never opts in (ADR 0070 step 7a's sibling list, ADR 0072's *Negative*)
  - **Depends on**: T4.8
  - **References**: FR-16(a), NFR-4, AC-24 (general clause), issue #4260 (`Scoped` half); ADR 0072 steps 3a, *`ScopedArtefactCache`* contract

---

## Phase 5 — ADR 0075: publish and pump scope suppression

- [ ] **STRUCTURAL: T5.1 — `PipelineBuilder`'s two dispatch constructors learn which kind of build it is**
  - **USE COMMAND**: `/tidy-first add a defaulted isolateSubscribers flag to PipelineBuilder's two dispatch constructors`
  - Files: `src/Paramore.Brighter/PipelineBuilder.cs` — `bool isolateSubscribers = false` on the two dispatch constructors (`:59` sync, `:76` async), stored and not yet read
  - The **describe-only** constructor (`:92`) does **not** take it: it resolves nothing and can adopt nothing. The two validation-time construction sites (`BrighterPipelineValidationExtensions.cs:75`, `:116`) use that constructor and are unaffected either way
  - `CommandProcessor` passing `true` lands with T5.2, not here
  - Binary-breaking on two public constructors (source-compatible for a recompiling caller) — release-noted by T7.14
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T4.9 (Phase 4 complete)
  - **References**: FR-9(a), AC-24 (general clause); ADR 0075 step 2

- [ ] **TEST + IMPLEMENT: T5.2 — core expresses scoping only through the seam, with the correct affinity mix (bracket 1, resolution time)**
  - **USE COMMAND**: `/test-first a Send a three subscriber Publish and a Post produce exactly five adoption decisions with AlwaysNew for each subscriber`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_send_a_publish_and_a_post_run_the_recorder_should_show_five_adoption_decisions.cs`
  - Test should verify:
    - the fake `IAmAScopeProvider` of T4.3 (test assembly, no `Microsoft.Extensions.DependencyInjection` reference), the affinity option **`JoinAmbient`**, and lifetime triple `{Scoped, Scoped, Scoped}`
    - one `Send`, one `Publish` with three subscribers and one `Post` produce **exactly five** adoption decisions: `JoinAmbient` for the `Send`'s handler pipeline; `JoinAmbient` for the `Post`'s transform pipeline (**one** decision for the pipeline, shared by the mapper and transformer factories — C-19, D12); and `AlwaysNew` for **each of the three** `Publish` subscribers
    - **no further decision** for any individual resolution within a pipeline (NFR-6), and **none** for the `Post`'s transformer factory separately from its mapper factory
    - ⚠ the AC asserts nothing about scope release, which the fake cannot observe and which for the two adopting pipelines does not happen at all (FR-12)
    - ⚠ **the three subscriber asks are still made.** A suppressed subscriber calls `GetAmbient(AlwaysNew)` and the recorder sees it. That is the difference from a host with **no provider registered**, where ladder row 3 makes no call at all — the *outcome* matches and the *path* does not. An implementor who skips the ask fails this AC and AC-46
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - have `CommandProcessor.Publish` (`:472`) and `PublishAsync` (`:575`) construct the builder with `isolateSubscribers: true`; `Send` (`:317`) and `SendAsync` (`:394`) keep the default and therefore never suppress
    - put **bracket 1 inside the per-subscriber lambda** on both twins — `observerTypes.Each(observer => { … })`, `Build` at `:187-198` and `BuildAsync` at `:232-244` — wrapping the `GetSyncInstanceScope()`/`GetAsyncInstanceScope()` call, the handler `Create` and `BuildPipeline`/`BuildAsyncPipeline` with the decorator resolution inside it. ⚠ Establishing it around the **whole build loop** is wrong (all subscribers would share one pipeline scope); establishing it only at dispatch is wrong (the artefacts already exist, resolved from the caller's unsuppressed ambient)
    - add **the five container-backed factories' single read** of `AmbientScopeSuppression.IsSuppressed`, at the line ADR 0072's protocol calls step 3 — the affinity computation. The type arrived in T3.3; **this edit is ADR 0075's and lands in this commit**, which is why ADR 0072's commit never referenced a type that did not exist
    - ⚠ **restore explicitly** on bracket 1's sync path: `observerTypes.Each` is a plain synchronous `foreach` (`Extensions/Each.cs:39-45`) on the calling thread with no `ExecutionContext` boundary, inside a `void Publish`, so a suppression set for one subscriber's build persists into the next subscriber's build and reaches the caller unless restored. **This is where the caller's flow is genuinely exposed**
    - add **no outcome** to ADR 0072's ladder — suppression selects one that already exists, and it silences no diagnostic
  - **Depends on**: T5.1, T3.3
  - **References**: AC-13 (FR-10, FR-8, FR-27.1, NFR-6, D16); ADR 0075 steps 2, 3, 6

- [ ] **TEST + IMPLEMENT: T5.3 — bracket 2: a pipeline created while a subscriber runs does not adopt, and the caller's flow is unsuppressed after the publish**
  - **USE COMMAND**: `/test-first a Post issued from inside a Publish subscriber does not adopt the ambient and a Send after the publish does`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_subscriber_issues_a_nested_pipeline_it_should_not_adopt_the_ambient.cs`
  - **Facts**: **2**, both in this one file — the **sync** twin over `Publish` (`Parallel.ForEach`, `CommandProcessor.cs:481`) and the **async** twin over `PublishAsync` (start loop `:591-599`, awaited at `:601`). ⚠ The two dispatch through genuinely different mechanisms, so a green on one says nothing about the other; each fact carries both the nested-pipeline clauses and the post-publish clauses that actually fail on a leak. Gate once, on the first fact
  - Test should verify:
    - the `AsyncLocal`-backed test provider of T4.4 with an ambient established on the caller's flow; affinity option `JoinAmbient`; lifetime triple `{Scoped, Scoped, Scoped}`
    - a nested `Send` and a nested `Post` issued from **inside** a subscriber's `Handle`/`HandleAsync` resolve instances that are **not** the ambient's — on **both** twins, sync `Publish` (`Parallel.ForEach`, `CommandProcessor.cs:481`) and `PublishAsync` (start loop `:591-599`, awaited at `:601`)
    - ⚠ **the clauses that actually fail on a leak**: after the publish returns, a `Send` **and** a `Post` issued outside any subscriber both resolve from the ambient's scope
    - ⚠ **no assertion depends on subscriber ordering, and no gate requires two bodies to overlap on the sync path** — `Parallel.ForEach` neither orders nor guarantees overlap and may inline every body on the calling thread. Any latch used to observe overlap is bounded by an explicit timeout whose expiry records "no overlap observed" without failing the test
    - ⚠ **no assertion that suppression leaked from one subscriber body to the next.** Per FR-9(i) that leak is unobservable: under FR-8 every subscriber must be suppressed anyway
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - place bracket 2 on the **sync** path inside the `Parallel.ForEach` body (`:481-497`), around `handleRequests.Handle(@event)` (`:489`), restored on every exit path of the body
    - place bracket 2 on the **async** path around the **invocation** of `handleRequests.HandleAsync(@event, cancellationToken)` inside the start loop (`:596`) — **never around `Task.WhenAll` (`:601`)**. By then every subscriber's task has branched, and a write made after a flow has branched does not reach it, so bracketing `WhenAll` would suppress **nothing in any subscriber at any point in its life**
    - write the restore explicitly on **both** brackets and **both** paths. ⚠ **Risk mitigation — a reader who has the mechanism backwards will place the next bracket by the wrong rule**: an `async` method is itself an `ExecutionContext` boundary, so nothing inside `PublishAsync` (`:559`) reaches its caller restored or not; `Parallel.ForEach` restores per **replica**, including the inlined one, so a body-level write does not reach the caller either but **does** leak from one body to the next on a shared worker; the only place the caller's flow is genuinely exposed is bracket 1 on the synchronous twin
  - ⚠ This task implements the bracket over the non-ASP.NET provider, which is all that can compile in this phase. **AC-12, AC-39 and AC-47 pin it end-to-end over an ASP.NET host in Phase 6** (T6.13, T6.14, T6.15)
  - **Depends on**: T5.2
  - **References**: FR-8, FR-9(b), NFR-4, D6, OOS-14 (AC-12, AC-39, AC-47 discharge it at Phase 6); ADR 0075 steps 4, 5, 5a

- [ ] **TEST + IMPLEMENT: T5.4 — bracket 3: the consumer pump's own flow is suppressed, so FR-19 is an invariant rather than an assumption**
  - **USE COMMAND**: `/test-first a consumer pipeline asks with AlwaysNew and adopts nothing whatever flow the pump was started on`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_pump_is_started_on_a_flow_carrying_an_ambient_the_consumer_should_not_adopt.cs`
  - Test should verify:
    - the `AsyncLocal`-backed test provider with a **live** ambient established on the flow that starts the `Dispatcher`; affinity option `JoinAmbient`; lifetime triple `{Scoped, Scoped, Scoped}`
    - every consumer pipeline's ask carries **`AlwaysNew`** and the recorder shows **zero adoptions**: each pipeline resolves a container-`Scoped` dependency that is **not** the starting flow's instance, and disposes it at the end of its own pipeline
    - **no `Warning` naming any of FR-24's three conditions** is recorded, because the ask was not a `JoinAmbient` one
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - take the bracket **inside the task `Performer.Run()` starts** (`Performer.cs:62-69`), around the `_messagePump.Run()` call it already makes — so the bracket is taken and disposed **on the flow it suppresses**, ends when the pump stops, and does not depend on context flow through `Task.Factory.StartNew`. No signature changes
    - ⚠ **not** touch the pump. C-2 and OOS-5 freeze `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder` and `ConsumerFactory`, and `Run()` is implemented on `Reactor.cs:95` and `Proactor.cs:95`. `Performer` is **not** one of the five, and its stated responsibility (`Performer.cs:31-32`) is the flow boundary rather than the pump. The pump still publishes no per-message ambient (D0b, OOS-1)
    - ⚠ **not** try to solve this in configuration. Brighter's factories read **one `IBrighterOptions` for the whole host** and nothing on it says which side is asking; in a mixed host that object is whichever side won the `TryAdd` (C-12). Suppression works because it is a property of the **flow the pipeline was created on**
  - ⚠ This task implements the bracket; **AC-55 and AC-20 pin it in Phase 6** (T6.21, T6.20)
  - **Depends on**: T5.3
  - **References**: FR-19, C-14, NFR-7 (AC-20, AC-55 discharge it at Phase 6); ADR 0075 step 4a

- [ ] **DOC: T5.5 — `docs/guides/lifetimes-and-scoping.md` part 1: the lifetime model, the `IAmAScope`/`IAmALifetime` distinction and NFR-9's truth table**
  - No test. Documentation whose substance is fixed by ADRs 0070, 0071, 0072 and 0075 and is not re-decided here
  - **Verified by**: a line on the PR checklist, and re-checked by **T7.15**, which re-verifies every truth-table row's AC citation once Phase 6 has landed and the forward references close
  - File: `docs/guides/lifetimes-and-scoping.md` (new — re-derived: no such page exists; the only lifetime prose in the repository is inside ADRs 0066 and 0067)
  - Content, per ADR 0074's clause-to-source map:
    - **FR-25.1** — the get/release cycle for `Transient`, `Scoped` and `Singleton`, from ADR 0070 step 7 (transform pipelines), ADR 0071 step 5 (handler pipelines) and ADR 0067 for `Transient`'s per-resolution scope
    - **FR-25.2** — that affinity applies to `Scoped` only (FR-21), and that an inert opt-in is reported (the message itself is Phase 7's)
    - **FR-25.3 / NFR-9** — the truth table giving the resolution source (adopted ambient / Brighter-created pipeline scope / no-op) for each of `Send`, `Publish` subscriber, pipeline nested inside a subscriber, `Post` and consume; for each affinity setting; for each of `Transient`/`Scoped`/`Singleton`; and the no-provider case. The `Publish`-subscriber and nested rows come from ADR 0075 step 7; the source column from ADR 0072's ladder
    - **FR-25.4 / NFR-8** — `IAmAScope` versus `IAmALifetime`, from ADR 0070's component entry and ADR 0071's two-responsibilities paragraph
    - **every row of the truth table cites the AC that asserts it** (AC-13, AC-14, AC-15, AC-17, AC-18, AC-19, AC-20, AC-21, AC-26, AC-29, AC-34, AC-39, AC-46, AC-47) or is marked as derived from a cited row; **any row citing no AC is itself a finding**
  - ⚠ Six of those cited ACs land in Phase 6 (AC-14, AC-15, AC-17, AC-18, AC-19, AC-34) and one in Phase 7 workflows. Their citations are forward references here; **T7.15 re-verifies every row's citation after Phase 6 lands**
  - **Depends on**: T5.4
  - **References**: AC-25 (clauses 1–4, NFR-8, NFR-9), FR-25.1, FR-25.2, FR-25.3, FR-25.4; ADR 0074 (clause-to-source map, clauses 1–4), ADR 0075 step 7

---

## Phase 6 — ADR 0073: the ASP.NET Core request-scope package and its test project

- [ ] **PROJECT: T6.1 — build `src/Paramore.Brighter.Extensions.AspNetCore`**
  - No test of its own. **Done when**: `dotnet build` succeeds for the new project on every framework in `$(BrighterCoreTargetFrameworks)`, the solution still builds end to end, and T1.2's `DependencyBoundaryTests` still passes — that last is the NFR-2 gate, and this is the commit where its ASP.NET clause first becomes falsifiable
  - A `Microsoft.NET.Sdk` **class library** targeting `$(BrighterCoreTargetFrameworks)` — re-derived at `src/Directory.Build.props:45`: `net8.0;net9.0;net10.0` — with a `ProjectReference` to `Paramore.Brighter.Extensions.DependencyInjection` and one `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` for `IHttpContextAccessor` and `AddHttpContextAccessor`
  - ⚠ **No `Directory.Packages.props` entry** — a framework reference is not a package reference and central package management has nothing to manage. `netstandard2.0` is deliberately dropped
  - Add to `Brighter.slnx`
  - Three types, namespace `Paramore.Brighter.Extensions.AspNetCore` (the package's own, as every Brighter package does): `BrighterAspNetCoreExtensions`, `HttpContextScopeProvider`, `HttpRequestScope`. Types only; behaviour lands with T6.3
  - **Depends on**: T5.5 (Phase 5 complete), T3.5 (`ScopeAffinityOverride`), T3.1 (`IAmAScopeProvider`, `ScopeAffinity`) — ADR 0073 step 2: *"Nothing in this package compiles before both"*
  - **References**: FR-17, NFR-2, D1; ADR 0073 steps 1, 2

- [ ] **PROJECT: T6.2 — build `tests/Paramore.Brighter.Extensions.AspNetCore.Tests`**
  - No test of its own. **Done when**: the new test project restores and builds on `$(BrighterTestTargetFrameworks)`, `dotnet test` on it succeeds with zero tests, it appears in `Brighter.slnx`, and T1.2's `DependencyBoundaryTests` still passes — confirming the ASP.NET reference reached only this project and the new package
  - Re-derived: the repository has **37** test projects and **zero** reference `Microsoft.AspNetCore.*`, `Microsoft.AspNetCore.Mvc.Testing` or `WebApplicationFactory`; `Brighter.slnx` has no ASP.NET **test** entry. **The ADR's claim holds**
  - A `Microsoft.NET.Sdk.Web`-hosted `WebApplicationFactory` fixture, targeting `$(BrighterTestTargetFrameworks)` — re-derived at `tests/Directory.Build.props:4`: `net9.0;net10.0`
  - ⚠ Add a `Directory.Packages.props` entry for `Microsoft.AspNetCore.Mvc.Testing` (re-derived: **no such entry today**). Step 1's "no `Directory.Packages.props` entry" claim is true of the **src package only**
  - Add to `Brighter.slnx`
  - ⚠ **The `ProjectReference`s, each owed to a task sited here.** The 22 criteria this project hosts need more than the ASP.NET reference, and no other task supplies them — `T6.20:1008`'s *"T6.21 already sits here with the same consumer packages"* is only true once this bullet is honoured. Re-derived at HEAD: all five `src/` projects below exist, and `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj:26-30` carries exactly this set for the same ground:
    - `Paramore.Brighter.Extensions.AspNetCore` (T6.1's new package) — every task calling `AddBrighterRequestScope(...)`, and T6.22's spy half, which takes the reference and deliberately never calls it
    - `Paramore.Brighter.Extensions.DependencyInjection` — `AddBrighter` in every fixture here (and the AspNetCore package already references it, so this is explicit rather than transitive)
    - `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` and `Paramore.Brighter.ServiceActivator.Extensions.Hosting` — the consumer hosts: **T6.9, T6.20, T6.21 and T7.6**, each of which registers `ServiceActivatorHostedService` explicitly because `AddConsumers` does not (C-15, D14)
    - `Paramore.Brighter.Outbox.Sqlite` and `Paramore.Brighter.Sqlite.EntityFrameworkCore` — **T6.19** alone, which needs a `Scoped` `DbContext` registered by `AddDbContext`, a relational outbox and a transaction provider over it
  - ⚠ These are `ProjectReference`s to existing projects, so they add **no** `Directory.Packages.props` entry and change nothing about NFR-2: none of the five references `Microsoft.AspNetCore.*`, and T1.2's `DependencyBoundaryTests` — already this task's `Done when` — is what proves it
  - The project has **two roles**: it hosts the criteria that need a running ASP.NET Core host with a controller action; and in **T6.22's spy fixture alone** it **references the src package and deliberately never calls the extension**, which is the only arrangement in which AC-14's spy clause is about anything. That second role wants the *reference*, not a host
  - ⚠ **Size it for twenty-two, not for eight.** ADR 0073 step 4a's *"eight"* is that ADR's count of the criteria **it** cites — AC-15, AC-16, AC-17, AC-18, AC-19, AC-34, AC-48, AC-49 — and step 4c hands the distribution question to task breakdown in terms: *"Which fixtures the project holds, and how those criteria are distributed across them, is task-breakdown work and is not decided here."* Re-derived against this task list: **21 test tasks are sited wholly here — T6.3 through T6.21, plus T7.6 and T7.8 — and half of T6.22, 22 in all.** T7.6 (AC-50) and T6.20 (AC-20) are here because both call `AddBrighterRequestScope(...)` and `Paramore.Brighter.Extensions.Tests` holds no reference to the package; T7.6 additionally needs a controller action
  - **Depends on**: T6.1
  - **References**: AC-15, AC-16, AC-17, AC-18, AC-19, AC-34, AC-48, AC-49, AC-14 (spy half); ADR 0073 steps 4a, 4c

- [ ] **TEST + IMPLEMENT: T6.3 — opted in, a `Send` from a controller shares the request scope**
  - **USE COMMAND**: `/test-first an opted in controller action and the handler it Sends to resolve the same Scoped DbContext`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_controller_sends_in_an_opted_in_host_the_handler_should_share_the_request_scope.cs`
  - Test should verify:
    - an ASP.NET test host that references the new package, calls `AddBrighterRequestScope()`, sets lifetime triple `{HandlerLifetime = Scoped, MapperLifetime = Scoped, TransformerLifetime = Scoped}` and makes **no other change**, with `IOrderDbContext` registered `AddScoped`
    - a controller action capturing its own `IOrderDbContext` and calling `Send(placeOrder)` whose handler also takes `IOrderDbContext`
    - the handler's instance is **reference-equal** to the controller's
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - implement `HttpContextScopeProvider`: an affinity check, **two** null checks — `_accessor.HttpContext`, then that context's `RequestServices` — and `new HttpRequestScope(context.RequestServices)`. It takes `IHttpContextAccessor` as a **constructor dependency**, not a static
    - implement `HttpRequestScope`: a constructor taking an `IServiceProvider` and `ArgumentNullException.ThrowIfNull`-ing it, a property returning the **captured** provider, and two **no-op** disposals (ASP.NET owns the request scope, FR-12, C-7). ⚠ Capture the `IServiceProvider`, **never the `HttpContext`** — `HttpContext.RequestServices` has a public setter over a replaceable `IServiceProvidersFeature`
    - implement `AddBrighterRequestScope(this IServiceCollection services, ScopeAffinity affinity = ScopeAffinity.JoinAmbient)`: `ArgumentNullException.ThrowIfNull(services)`, `services.AddHttpContextAccessor()`, `services.AddSingleton<IAmAScopeProvider, HttpContextScopeProvider>()` (**plain `AddSingleton`**, FR-24.3) and `services.AddSingleton(new ScopeAffinityOverride(affinity))` (**plain `AddSingleton`, constructed instance**, FR-17). It reads nothing from the collection and removes nothing; it never throws on ordering and never alters a lifetime
    - extend `IServiceCollection`, **not `IBrighterBuilder`** — an `IBrighterBuilder` extension would make "call the extension before `AddBrighter`" unexpressible, and AC-48 requires that ordering to work
  - **Depends on**: T6.2, T3.6
  - **References**: AC-15 (FR-17, FR-14, D1, D13); ADR 0073 steps 1, 2

- [ ] **TEST + IMPLEMENT: T6.4 — Brighter does not dispose a borrowed scope**
  - **USE COMMAND**: `/test-first the request scoped DbContext a handler used is still usable after Send returns and is disposed once by ASP.NET`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_send_returns_the_borrowed_request_scope_should_not_be_disposed.cs`
  - Test should verify:
    - the T6.3 setup (lifetime triple `{Scoped, Scoped, Scoped}`)
    - after `Send` returns, the controller action uses its `IOrderDbContext` again **successfully** — it was not disposed by Brighter
    - its `Dispose` was called **exactly once**, by ASP.NET at end of request, **after** the action completed
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - rely on the borrowed `ServiceProviderPipelineScope`'s idempotent no-op disposal from T4.4 and `HttpRequestScope`'s no-op disposal. ⚠ AC-8's idempotence rule is written over two live pipelines each holding a **Brighter-created** handle and is not cited for this case
  - **Depends on**: T6.3
  - **References**: AC-16 (FR-12, C-7); ADR 0072 step 4, ADR 0073 (*Why `HttpRequestScope`'s disposal is a no-op*)

- [ ] **TEST + IMPLEMENT: T6.5 — two `Post`s in one request share the request scope, and one mapper instance**
  - **USE COMMAND**: `/test-first two Posts in one HTTP request resolve the same Scoped mapper instance disposed only when ASP.NET disposes the request scope`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_two_posts_run_in_one_request_they_should_share_one_scoped_mapper.cs`
  - Test should verify:
    - the T6.3 setup (lifetime triple `{Scoped, Scoped, Scoped}`)
    - one controller action calls `Post(commandA)` then `Post(commandB)`, both mapped by the same `Scoped` mapper type: the two mapper instances are **reference-equal**
    - neither was disposed by Brighter when the second `Post` returned
    - both are disposed when ASP.NET disposes the request scope — MS DI tracks disposable **transient** resolutions against the scope that created them, so the caller disposes them
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - depend on `ScopedArtefactCache` resolving from `src.Services` on the borrowed path (T4.4), which is what makes artefact identity follow the **borrowed scope** (D7) rather than the pipeline. A cache that stayed a private field of the handle would give per-pipeline identity and two mappers
  - **Depends on**: T6.4, T4.4
  - **References**: AC-17 (FR-16a, FR-14, D7); ADR 0072 *`ScopedArtefactCache`*

- [ ] **TEST + IMPLEMENT: T6.6 — a mapper and a handler in one request share a `Scoped` dependency**
  - **USE COMMAND**: `/test-first a Post's mapper and a Send's handler in one request resolve the same Scoped IMarker`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_post_and_a_send_run_in_one_request_the_mapper_and_handler_should_share_a_scoped_dependency.cs`
  - Test should verify:
    - the T6.3 setup (lifetime triple `{Scoped, Scoped, Scoped}` — the triple matters: the transform pipeline will not adopt while `TransformerLifetime` is `Transient`), with `IMarker` registered `AddScoped` and injected into both a mapper and a `Send` handler
    - one controller action calls `Post(commandA)` then `Send(commandB)`: the mapper's `IMarker` and the handler's `IMarker` are **reference-equal**
    - neither is disposed until ASP.NET disposes the request scope
    - contrast with AC-21 (T1.13), the consumer case, whose outcome is the **opposite** and intended
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need no new code if both pipelines borrow the same request scope. This is the opted-in producer exception to C-3
  - **Depends on**: T6.5
  - **References**: AC-34 (FR-16b, C-3's exception); ADR 0072 step 4

- [ ] **TEST + IMPLEMENT: T6.7 — no `HttpContext` means a new, Brighter-owned scope, with exactly one `Warning`**
  - **USE COMMAND**: `/test-first a hosted service and a background thread with no HttpContext each get a fresh Brighter owned scope and the host records one warning in total`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_there_is_no_http_context_the_pipeline_should_create_its_own_scope.cs`
  - Test should verify:
    - the opted-in host of T6.3 (lifetime triple `{Scoped, Scoped, Scoped}`), with a capturing `ILoggerProvider` registered for `Paramore.Brighter.*`
    - an `IHostedService` in the same host — **and, separately, a background thread with no `HttpContext`** — calls `Send`: each resolves a fresh `Scoped` dependency, disposed when `Send` returns, no exception thrown, and **zero** entries at `LogLevel.Error` or above during the call
    - ⚠ **across the two calls together, exactly one `LogLevel.Warning`** naming the **no ambient offered** condition and the ASP.NET provider's implementation type. The host is opted in, so both asks carry `JoinAmbient`, both correctly return nothing, and the latch is once per Brighter container per provider implementation type (D19). **Asserting the count is what makes this branch discriminating**: an implementation logging nothing and one logging per call both satisfy the `Error`-or-above clause alone
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need no new production code beyond T4.5's latch and T6.3's provider — the provider returns `null` where `HttpContext` is null or its `RequestServices` is null, and both take FR-18's path
    - not resolve from the root provider directly, and not log at `Error` or above
  - **Depends on**: T6.6, T4.5
  - **References**: AC-19 (FR-18, FR-13, FR-24.2, D19); ADR 0073 step 1

- [ ] **TEST + IMPLEMENT: T6.8 — the provider itself declines an `AlwaysNew` ask without consulting the accessor**
  - **USE COMMAND**: `/test-first HttpContextScopeProvider returns null on an AlwaysNew ask without touching IHttpContextAccessor`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_ask_carries_always_new_the_provider_should_not_consult_the_accessor.cs`
  - **Facts**: **2**, both in this one file — ⚠ the **`AlwaysNew`** ask, which must return `null` with the spy recording **zero accesses**, and the **`JoinAmbient`** ask, which must return an `HttpRequestScope` over `HttpContext.RequestServices`. ⚠ **The first is a negative fact** — the zero-access count *is* the assertion, and it is one of the spec's deliberately negative criteria (AC-46). The second is the control that stops a provider which simply never consults the accessor from passing. Gate once, on the first fact
  - Test should verify:
    - an `IHttpContextAccessor` **spy** counting accesses, and a live `HttpContext`
    - `GetAmbient(ScopeAffinity.AlwaysNew)` returns `null` and the spy records **zero accesses**
    - `GetAmbient(ScopeAffinity.JoinAmbient)` returns an `HttpRequestScope` over `HttpContext.RequestServices`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - short-circuit on affinity **before** touching `IHttpContextAccessor`. This is the provider honouring its half of FR-10's contract; Brighter ignores an ambient returned for an `AlwaysNew` ask anyway (FR-24.4, discharged by AC-11), so both halves exist
  - No acceptance criterion covers the provider-side short-circuit directly (AC-18 says so in terms: through a conforming test double it is true by construction). This task exists because the ADR states it as a step and it is falsifiable here
  - **Depends on**: T6.3
  - **References**: FR-10 (the provider's obligation), D16; ADR 0073 step 3

- [ ] **TEST + IMPLEMENT: T6.9 — the opt-in reaches the object the factories read, on all four registration paths**
  - **USE COMMAND**: `/test-first the registration extension's affinity reaches the resolved IBrighterOptions and adoption works on all four Brighter entry points`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_extension_is_called_on_each_entry_point_the_affinity_should_reach_the_resolved_options.cs`
  - **Facts**: **8**, all in this one file — the four entry points × the two directions, as T3.6. ⚠ The two consumer hosts must use the `Action<ConsumersOptions>` overload and register `ServiceActivatorHostedService` explicitly (C-12, C-15, D14). Gate once, on the first fact
  - Test should verify:
    - four hosts, each using **exactly one** entry point — `AddBrighter(Action<BrighterOptions>)`, `AddBrighter(Func<IServiceProvider, BrighterOptions>)`, `AddConsumers(Action<ConsumersOptions>)` **alone**, `AddConsumers(Func<IServiceProvider, ConsumersOptions>)` **alone** — each setting lifetime triple `{Scoped, Scoped, Scoped}` and each calling `AddBrighterRequestScope()` with **no affinity argument** (so the `JoinAmbient` default applies)
    - in all four the affinity on the resolved `IBrighterOptions` is `JoinAmbient`, not the `AlwaysNew` default, even on the three paths that run no `IOptions` pipeline (C-12a)
    - with a request-scope ambient available and a `Send` issued from within it, in all four the handler resolves the ambient's `Scoped` instance
    - ⚠ all four rebuilt so each **first** sets `DefaultScopeAffinity = JoinAmbient` by whatever means its entry point supports and **then** calls the extension passing `ScopeAffinity.AlwaysNew`: the resolved options carry `AlwaysNew` and nothing adopts. The non-default starting value is what makes the clause falsifiable
    - ⚠ each `AddConsumers` overload is used **alone**, so C-12's `InvalidCastException` hazard is not walked into
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T3.6's write-through and T6.3's extension. If it fails, the fault is in `RegisterBrighterOptions` or in one of the four deleted site registrations
  - **Depends on**: T6.8, T3.6
  - **References**: AC-45 (FR-17, FR-14, C-12a, D13, D18); ADR 0076 step 3, ADR 0073 step 2

- [ ] **TEST + IMPLEMENT: T6.10 — the extension's affinity argument decides adoption, and order is irrelevant**
  - **USE COMMAND**: `/test-first the affinity passed to the registration extension decides adoption whichever value the application assigned and whatever the registration order`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_extension_carries_an_affinity_argument_it_should_decide_adoption.cs`
  - **Facts**: **4**, all in this one file — branch 1's host; branch 2's host; and branch 3, which is ⚠ **each of those two hosts built again** with the extension call moved before `AddBrighter`/`AddConsumers` — **two** further host builds, not one. Gate once, on the first fact
  - Test should verify:
    - an ASP.NET host with lifetime triple `{Scoped, Scoped, Scoped}` whose **own** `AddBrighter(Action<BrighterOptions>)` delegate sets `DefaultScopeAffinity = JoinAmbient`, which then calls the extension passing `ScopeAffinity.AlwaysNew` — the starting value is deliberately **not** the property's default — and which then registers a recording `IAmAScopeProvider` that **wraps and delegates to** the ASP.NET provider. The recorder is registered **last**, so under FR-24.3 it is the single effective provider; ⚠ **`ValidatePipelines()` is not called**, so no duplicate-implementation-type warning arises and AC-43's six single-finding hosts are unaffected
    - branch 1: `Send` from a controller — the handler's `Scoped` dependency is **not** the controller's instance and is disposed when `Send` returns, and the recorder shows **exactly one** decision for that pipeline carrying **`AlwaysNew`**
    - branch 2: the same host with its own delegate setting `AlwaysNew` and the extension called with **no argument** (the `JoinAmbient` default) — the handler's dependency **is** the controller's instance
    - branch 3: each of those two hosts built again with the extension call moved **before** `AddBrighter`/`AddConsumers` — the outcome is unchanged in both cases
    - ⚠ the recorder captures `IServiceProvider` and resolves `IEnumerable<IAmAScopeProvider>` **on its first `GetAmbient` call, not in its constructor**, selecting the entry that is not itself: injecting `IAmAScopeProvider` resolves to the recorder itself under last-wins, and injecting the enumerable makes MS DI construct the recorder to build the enumerable it is being constructed with
    - ⚠ the AC deliberately asserts **nothing** about whether the recorder consulted the ambient it wraps — that is the provider's half, true by construction through a conforming double; Brighter's ignore half is AC-11's
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T3.6 and T6.3 — the extension wins because it writes **last**, from inside the producer, and it writes last on every path because three of the four run no `IOptions` pipeline (D18). There is no ordering to get right, because there is no ordering
  - **Depends on**: T6.9
  - **References**: AC-18 (FR-15, FR-17, FR-24.3, C-10, D13, D16, D18); ADR 0076 step 3, ADR 0073 step 2

- [ ] **TEST + IMPLEMENT: T6.11 — the extension's argument wins over an affinity the application assigned itself, symmetrically and in either order**
  - **USE COMMAND**: `/test-first the extension's affinity argument overrides an affinity the application assigned, in both directions and in both registration orders, with no validation finding`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_application_assigns_an_affinity_and_calls_the_extension_the_argument_should_win.cs`
  - **Facts**: **3**, all in this one file — the host with the extension called **after** `AddBrighter`; the same with it called **before**; and the **mirror**. ⚠ Per AC-48 the mirror is a **single** configuration and is *not* repeated in both orderings, and the *no validation finding* clause is an assertion over the first two rather than a fourth fact. Gate once, on the first fact
  - Test should verify:
    - an ASP.NET host with lifetime triple `{Scoped, Scoped, Scoped}` setting `DefaultScopeAffinity = AlwaysNew` in its own `AddBrighter(Action<BrighterOptions>)` delegate **and** calling `AddBrighterRequestScope()` with no argument: the resolved `IBrighterOptions` carries **`JoinAmbient`** and the handler resolves the controller's own `Scoped` instance
    - the same with the extension call placed **before** `AddBrighter` as well as after it
    - ⚠ **no validation finding is raised for the conflict, on either ordering** — per FR-17 it is documented (FR-25.11), not validated, because without the banned sentinel it is indistinguishable from the ordinary opt-in
    - the **mirror**: the application's delegate setting `JoinAmbient` while the extension is passed `ScopeAffinity.AlwaysNew` — the resolved options carry `AlwaysNew` and nothing adopts. The rule is symmetric and is **not** "the more permissive value wins"
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing new — and specifically must **not** add a validation rule for this conflict. ADR 0076 step 5 declines it deliberately
  - **Depends on**: T6.10
  - **References**: AC-48 (FR-17, C-10, D18); ADR 0076 steps 3, 5

- [ ] **TEST + IMPLEMENT: T6.12 — affinity is inert outside `Scoped`**
  - **USE COMMAND**: `/test-first only an all Scoped host under JoinAmbient shares a container Scoped dependency with the controller`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_lifetimes_are_not_scoped_the_affinity_should_be_inert.cs`
  - **Facts**: **6**, all in this one file — three lifetime triples × both affinity settings. Each pair must agree, which is the whole assertion. Gate once, on the first fact
  - Test should verify:
    - the opted-in host of T6.3, run three times with a different triple each time — `{Transient, Transient, Transient}` (the defaults), `{Scoped, Scoped, Scoped}` and `{Singleton, Singleton, Singleton}` — and each run repeated under **both** affinities, **selected by the argument passed to `AddBrighterRequestScope(...)`** rather than by assigning the option alongside it
    - ⚠ **`ValidatePipelines()` is not called**: under `JoinAmbient` the `{Transient, Transient, Transient}` and `{Singleton, Singleton, Singleton}` runs are exactly FR-22.1's inert-opt-in error, and this AC pins the *runtime* inertness that error warns about
    - only the `{Scoped, Scoped, Scoped}` run under `JoinAmbient` yields reference-equality on a container-`Scoped` dependency; the `{Transient, Transient, Transient}` run resolves a different instance under both settings
    - ⚠ in the `{Singleton, Singleton, Singleton}` run the handler takes a container-**`Singleton`** dependency, **not** a `Scoped` one, and two `Send` calls from different requests resolve the same handler instance and the same dependency under both affinities. A `Singleton` handler taking a container-`Scoped` dependency is a captive-dependency graph that throws under `ValidateScopes` (the ASP.NET default in Development) and silently returns a captive instance without it
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond ladder rows 1 and 2 (T4.3): only `Scoped` participates in the ask, `Transient` keeps ADR 0067's per-resolution scope and `Singleton` resolves from the root, under either affinity
  - **Depends on**: T6.11
  - **References**: AC-26 (FR-21, D5); ADR 0072 step 3

- [ ] **TEST + IMPLEMENT: T6.13 — `Publish` subscribers do not adopt even when opted in, and a provider that ignores the affinity cannot change that**
  - **USE COMMAND**: `/test-first Publish subscribers in an opted in host never resolve the request's instance and an affinity ignoring provider is warned about once per container`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_publishing_from_an_opted_in_controller_the_subscribers_should_not_adopt.cs`
  - **Facts**: **3**, all in this one file — branch 1's opted-in application; branch 2's hand-rolled affinity-ignoring provider; and ⚠ branch 3's **second host of the same shape**, which is **the only case in the document that pins independent latching** — a single shared latch, or one keyed on provider type alone, records one entry and fails it. Gate once, on the first fact
  - Test should verify:
    - branch 1: an opted-in ASP.NET application, lifetime triple `{Scoped, Scoped, Scoped}`, a request scope containing `IUnitOfWork` instance `R`, two subscribers for `OrderPlaced` each taking `IUnitOfWork`. After `PublishAsync(orderPlaced)` from a controller action: **neither** subscriber resolved `R`, the two resolved **two distinct** instances, both were disposed when the publish completed, and **`R` was not disposed**
    - branch 2: the same application except its ambient source is a **hand-rolled** `IAmAScopeProvider` reading the same request ambient — registered as the **only** `IAmAScopeProvider` descriptor, in place of the ASP.NET one, with the affinity option `JoinAmbient` so **no extension call is involved** — which **violates FR-10** by returning that ambient for *every* ask including `AlwaysNew` ones, with a capturing `ILoggerProvider` for `Paramore.Brighter.*`. The outcome is unchanged, **and exactly one `LogLevel.Warning`** names the **ambient offered for an `AlwaysNew` ask and ignored** condition and that provider's implementation type; a second `PublishAsync` in the same host records **no further** entry
    - ⚠ branch 3: a **second host of the same shape** — a fresh Brighter container, so all three latches start unlatched (D19) — registering the **same** provider implementation type, varying in one respect: it returns the ambient for `AlwaysNew` asks but `null` for `JoinAmbient` asks. A `Send` (one `JoinAmbient` ask) then `PublishAsync` to the same two subscribers (two `AlwaysNew` asks) records **exactly two** `Warning` entries — one **no ambient offered**, one **ambient offered for an `AlwaysNew` ask and ignored** — both naming the same provider implementation type; repeating both operations records no further entry. **This branch is the only case in the document that pins independent latching**: a single shared latch, or a latch keyed on provider type alone, records one entry and fails it
    - ⚠ **this is a resolution-time assertion**: both subscribers' handlers and their `IUnitOfWork` dependencies are resolved inside `BuildAsync` in the controller's own flow, before either subscriber runs (`PipelineBuilder.cs:235-236`). It fails unless FR-9's **resolution-time** bracket is implemented; an execution-time bracket alone cannot make it pass
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T5.2's bracket 1 and T4.5's latch, plus **canonical ladder row 5's** ignore-and-warn. ⚠ **Row 5, not row 6** — row 6 is the same `AlwaysNew` ask with *nothing* returned and emits **no** diagnostic at all. If branch 3 fails, the latch key is wrong
  - **Depends on**: T6.12, T5.2, T4.5
  - **References**: AC-11 (FR-8, FR-10, FR-24.4, D6, D19); ADR 0075 steps 3, 6, ADR 0072 step 2

- [ ] **TEST + IMPLEMENT: T6.14 — suppression propagates into an async subscriber and does not leak out of it**
  - **USE COMMAND**: `/test-first two concurrently running Publish subscribers and a nested SendAsync all resolve outside the request scope while the caller stays unsuppressed`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_two_subscribers_run_concurrently_suppression_should_propagate_and_not_leak.cs`
  - Test should verify:
    - an opted-in ASP.NET application, lifetime triple `{Scoped, Scoped, Scoped}`, request-scope `IUnitOfWork` instance `R`; subscribers A and B each holding a `Scoped` `IUnitOfWork`, each signalling entry and awaiting a gate so their executions provably overlap — ⚠ **every gate bounded by an explicit timeout that fails the test rather than hanging it**, since `PublishAsync` starts every subscriber (`:591-599`) and awaits them together at `:601`, so an unbounded gate deadlocks the publish; and A calling `SendAsync(new InnerCommand())` from inside `HandleAsync`
    - both subscribers were in flight **simultaneously**
    - neither subscriber's own `IUnitOfWork` is `R` — which fails unless suppression was established at the **resolution-time** bracket
    - `InnerCommand`'s handler resolved an `IUnitOfWork` that is **not** `R`, **not** A's and **not** B's
    - ⚠ **the assertions that can actually fail on a leak**: after the publish completes, a `Send` **and** a `Post` issued from the controller outside any subscriber both resolve from `R`'s scope
    - ⚠ **no assertion that B's `IUnitOfWork` is unaffected by A's suppression** — under FR-8 B is `AlwaysNew` and suppressed regardless, so that would be vacuous
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T5.2 and T5.3's async bracket around the **invocation** at `:596`
  - **Depends on**: T6.13, T5.3
  - **References**: AC-12 (FR-8, FR-9, NFR-4, D6, D10); ADR 0075 steps 3, 4

- [ ] **TEST + IMPLEMENT: T6.15 — synchronous `Publish` isolates each subscriber and leaves the caller's flow unsuppressed**
  - **USE COMMAND**: `/test-first a synchronous Publish isolates every subscriber and its nested Sends and leaves the controller able to adopt afterwards`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_publishing_synchronously_each_subscriber_should_be_isolated_and_the_caller_left_unsuppressed.cs`
  - Test should verify:
    - an opted-in ASP.NET host, lifetime triple `{Scoped, Scoped, Scoped}`, request-scope `IUnitOfWork` `R`, and three **synchronous** subscribers each taking a `Scoped` `IUnitOfWork`, **two** of which — identified by their own recorded markers, ⚠ **not by execution order** — each call `Send(new InnerCommand())` from inside `Handle`
    - `Publish(orderPlaced)` (the sync path, `Handle` not `HandleAsync`, dispatched via `Parallel.ForEach` at `CommandProcessor.cs:481`): all three subscribers resolved **distinct** `IUnitOfWork` instances, none of them `R`
    - each nested `Send` resolved an `IUnitOfWork` that is neither `R`, nor its own subscriber's, nor the other nesting subscriber's or its nested `Send`'s — pinning D6 and OOS-14 on the sync path
    - ⚠ **the clauses that actually fail on a leak**: after `Publish` returns, a `Send` **and** a `Post` issued from the controller outside any subscriber both resolve from `R`'s scope — false if suppression was established around the dispatch loop in the caller's own flow
    - ⚠ **no assertion depends on which subscriber ran first, and no gate requires overlap.** Any latch used to observe overlap is bounded by an explicit timeout whose expiry records "no overlap observed" **without failing the test** — the concurrency assertion belongs to AC-12
    - ⚠ this is **not** a detector of suppression leaking between subscriber bodies; per FR-9(i) that leak is unobservable
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T5.2's bracket 1 (whose sync restore is load-bearing) and T5.3's bracket 2 inside the `Parallel.ForEach` body
  - **Depends on**: T6.14, T5.3
  - **References**: AC-39 (FR-8, FR-9, NFR-4, D6, OOS-14); ADR 0075 steps 3, 4, 5, 5a

- [ ] **TEST + IMPLEMENT: T6.16 — suppression fires for subscriber pipelines only, and fires even when the subscriber's own pipeline takes no scope**
  - **USE COMMAND**: `/test-first a nested Post adopts under a Send but not under a Publish subscriber whose own pipeline takes no scope`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_subscriber_takes_no_pipeline_scope_it_should_still_suppress_the_ambient.cs`
  - **Facts**: **2**, both in this one file and in the same host — the `Send`, whose nested `Post` **must** resolve `R` (the parent handler pipeline is `Transient`, takes no pipeline scope and, not being a subscriber, suppresses nothing); and the `Publish`, whose nested `Post` must resolve an instance that is **not** `R`. ⚠ **The two outcomes are opposite, and that contrast is the criterion** — a fact asserting only the `Publish` half would pass an implementation that suppressed everywhere. Gate once, on the first fact
  - Test should verify:
    - an opted-in ASP.NET host with `{HandlerLifetime = Transient, MapperLifetime = Scoped, TransformerLifetime = Scoped}` and ⚠ **`ValidatePipelines()` not called** (FR-22.2 would reject this triple; the AC pins the seam rule, C-15); request-scope `IMarker` instance `R` injected into the mapper; a `Send` handler and a `Publish` subscriber that each issue the same `Post`
    - `Send`: the nested `Post`'s mapper resolves **`R`** — the parent handler pipeline is `Transient`, so it takes no pipeline scope (FR-27.1) and, not being a subscriber, suppresses nothing (FR-27.3)
    - `Publish`: the nested `Post`'s mapper resolves an instance that is **not** `R` — a subscriber suppresses for pipelines nested inside it **even though its own pipeline has no `Scoped` participant and took no scope of its own**
    - ⚠ suppression here must come from FR-9's **execution-time** bracket, since the nested `Post` is created while the subscriber runs
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - confirm that suppression is applied **irrespective of the subscriber's own lifetimes** — the bracket is not conditional on the pipeline taking a scope
  - **Depends on**: T6.15
  - **References**: AC-47 (FR-27.3, FR-8, D6); ADR 0075 step 4

- [ ] **TEST + IMPLEMENT: T6.17 — borrowed-scope state does not accumulate across requests**
  - **USE COMMAND**: `/test-first Brighter held per scope associations are unreachable after a request completes and their peak count tracks concurrency not throughput`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_serving_many_requests_borrowed_scope_state_should_not_accumulate.cs`
  - **Facts**: **4**, all in this one file — clause 1's reachability run; ⚠ clause 2's **positive control, which is a second host** deliberately built with the per-scope association made process-lifetime and which must report `IsAlive == true`; and clause 3's steady-state measurement at **C = 1** and at **C = 8**, which are two runs of 10,000 requests each. ⚠ **Clause 2 is what makes clause 1 falsifiable** — without it, a harness whose `WeakReference` always died would pass. Gate once, on the first fact
  - Test should verify:
    - an opted-in ASP.NET host (lifetime triple `{Scoped, Scoped, Scoped}`) whose controller action performs two `Post`s and one `Send`
    - **clause 1 — reachability**: a `WeakReference` (not tracking resurrection) to the mapper instance used by request 1 has `IsAlive == false` after 10,000 further requests and `GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`
    - ⚠ **clause 2 — positive control, on the production path**: the same harness run against a host whose Brighter-held per-scope association is deliberately made process-lifetime — registered `Singleton` rather than `Scoped`, or replaced by a double that never releases — reports `IsAlive == true`. **The control must exercise Brighter retaining the association**; a test-owned static field holding the mapper would prove only that `WeakReference` and `GC.Collect` behave as documented
    - **clause 3 — steady state**: the peak number of live Brighter-held per-scope associations, sampled while requests are in flight, measured for concurrency C = 1 and C = 8 over 10,000 requests each; both peaks bounded by a constant multiple of C, and neither grows with the request count. The instrument is a test-visible count incremented in `ScopedArtefactCache`'s constructor and decremented in its `Dispose`, so the clause measures the container actually releasing it rather than being true by construction
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T4.4's `ScopedArtefactCache` registered `TryAddScoped` — the container owns its lifetime and disposes it with the scope, so no weak references and no eviction logic are needed. Its `Dispose` drops references only; MS DI already tracks disposable transient resolutions against the scope that created them
  - **Depends on**: T6.16, T4.4
  - **References**: AC-37 (FR-26, NFR-5, NFR-6, D7); ADR 0072 *`ScopedArtefactCache`*

- [ ] **TEST + IMPLEMENT: T6.18 — a failed build under `JoinAmbient` disposes nothing the caller owns**
  - **USE COMMAND**: `/test-first a hundred failing Posts in an opted in request leave the request scope usable and leak no Brighter owned scope`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_build_fails_under_join_ambient_the_request_scope_should_not_be_disposed.cs`
  - Test should verify:
    - an opted-in ASP.NET host (lifetime triple `{Scoped, Scoped, Scoped}`) with a mapper whose constructor depends on an unregistered service, so the transform pipeline build throws
    - a controller action calls `Post` 100 times, each throwing `ConfigurationException`
    - the **request scope is not disposed** — the controller can still resolve and use its own `Scoped` `IOrderDbContext` after the failures
    - **no Brighter-owned scope is leaked, because under adoption none was created**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - hold by construction on the borrowed path: `CleanUpAfterFailedBuild` releases the handle, whose borrowed disposal is a no-op, so it releases nothing the caller owns
  - **Depends on**: T6.17
  - **References**: AC-38 (FR-5, FR-12, C-7); ADR 0072 step 4, ADR 0070 step 4

- [ ] **TEST + IMPLEMENT: T6.19 — a handler's outbox write is in the caller's transaction when opted in, and is not when it is not**
  - **USE COMMAND**: `/test-first an opted in handler's DepositPost shares the controller's DbContext and transaction and rolls back with it`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_handler_deposits_in_an_opted_in_request_the_outbox_write_should_join_the_caller_transaction.cs`
  - **Facts**: **3**, all in this one file — the committing run; ⚠ the **rollback** re-run, which is what makes this a test of *atomicity* rather than of ordering; and ⚠ the **negative control** under `AlwaysNew`, which asserts today's behaviour and must pass **before** any of this specification is implemented. Gate once, on the first fact
  - Test should verify:
    - an opted-in ASP.NET host (lifetime triple `{Scoped, Scoped, Scoped}`) with a `Scoped` `DbContext` registered `AddDbContext`, a relational outbox, and a `Send` handler injecting both that `DbContext` and a transaction provider over it
    - a controller action resolves the `DbContext`, opens a transaction, writes an entity, calls `Send(command)` whose handler calls `DepositPost(event, provider)`, then commits: the handler's `DbContext` is **reference-equal** to the controller's; the outbox row and the entity are visible to one another inside the transaction before the commit; after the commit both are present
    - ⚠ re-run with the controller **rolling back**: **neither** the entity nor the outbox row is present — the clause that makes this a test of *atomicity* rather than of ordering
    - ⚠ **the negative control**: the identical host and application code calling **`AddBrighterRequestScope(ScopeAffinity.AlwaysNew)`** — the affinity supplied as the extension's **argument**, never assigned on the options object. On rollback, the handler's `DbContext` is **not** reference-equal to the controller's, the outbox row **survives** the rollback, and **no `Warning` or `Error`** is recorded by any Brighter logger — pinning C-21's silence as present behaviour rather than letting an implementation satisfy the criterion by *reporting* the case instead of fixing it
    - the negative control asserts today's behaviour and is expected to pass **before** any of this specification is implemented and to keep passing after
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing new: adoption changes **the provider instance the handler holds**, and nothing else. `IAmAnOutbox` and `IAmAnOutboxProducerMediator` are `Singleton`s (`ServiceCollectionExtensions.cs:484`) built in the root scope and never borrow; the three `GetService<IAmABoxTransactionProvider>()` sites (`:431`, `:487`, `:648`) are **type discovery only**; `DepositPost` without an explicit provider still passes `null` (`CommandProcessor.cs:795`), so the caller must still pass the provider
  - **Depends on**: T6.18
  - **References**: AC-52 (FR-16c, FR-15, C-21); ADR 0072 step 2c

- [ ] **TEST + IMPLEMENT: T6.20 — the affinity flag is inert on the consumer side**
  - **USE COMMAND**: `/test-first a mixed producer and consumer host consumes a hundred messages identically under both affinity settings and records no ambient warnings`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_mixed_host_consumes_messages_the_affinity_flag_should_be_inert.cs`
  - **Facts**: **2**, both in this one file — the `AlwaysNew` run and the `JoinAmbient` run, 100 messages each. ⚠ Both are largely **negative**: each asserts **zero** `Warning` entries naming any of FR-24's three conditions, and the two runs must be identical in resolution, identity and disposal. Gate once, on the first fact
  - ⚠ **Why this project.** The host calls `AddBrighterRequestScope(...)`, so it needs the new package's reference, and `Paramore.Brighter.Extensions.Tests` does not have one. Siting it here rather than adding that reference is deliberate: it keeps **T6.2**'s *"the only arrangement"* and **T6.22**'s *"no other Brighter test project references `Microsoft.AspNetCore.Http.Abstractions`"* true, both of which a reference from `Extensions.Tests` would falsify. This task needs **no controller and no web host** — only the extension and a consumer — and T6.21 already sits here with the same consumer packages
  - Test should verify:
    - ⚠ a mixed host calling `AddBrighter(...)` **before `AddConsumers(Action<ConsumersOptions>)` — the `Action` overload specifically**, because per C-12 the `Func<IServiceProvider, ConsumersOptions>` overload throws `InvalidCastException` in this ordering — so the producer's options object is the registered `IBrighterOptions`
    - the ASP.NET provider registered by calling `AddBrighterRequestScope(...)` with the affinity supplied **as that extension's argument**, never assigned on the options object; lifetime triple `{Scoped, Scoped, Scoped}`; run once passing `AlwaysNew` and once passing `JoinAmbient`
    - 100 messages consumed in each run: the two runs are **identical** in resolution, instance identity and disposal — the same count of distinct mapper, transform and handler instances, each disposed at the end of its pipeline, and zero Brighter-created scopes live at the end of each run
    - ⚠ log output is excluded from the identity comparison but **asserted separately**: over the 100 messages **each** run records **zero** `Warning` entries naming any of FR-24's three conditions. The consumer ask carries `AlwaysNew` whatever the option says (C-14), so FR-24.2's condition — `JoinAmbient` asks only — is never reached, and neither is FR-23's
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T5.4's pump-flow bracket. **Unbracketed, the consumer ask in this host would carry `JoinAmbient`, reach ladder row 7 and emit FR-24.2's latched `Warning`** — which is exactly the clause this AC forbids
  - **Depends on**: T6.19, T5.4
  - **References**: AC-20 (FR-19, C-12, C-14, D18); ADR 0075 step 4a

- [ ] **TEST + IMPLEMENT: T6.21 — a consumer pipeline does not adopt, whatever flow started the `Dispatcher`**
  - **USE COMMAND**: `/test-first a Dispatcher started from inside a live request still gives every consumer pipeline an AlwaysNew ask and zero adoptions`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_a_dispatcher_is_started_from_inside_a_request_the_consumer_should_not_adopt.cs`
  - Test should verify:
    - an opted-in ASP.NET host — provider registered by `AddBrighterRequestScope(ScopeAffinity.JoinAmbient)`, the affinity supplied as the **argument**, lifetime triple `{Scoped, Scoped, Scoped}` — with AC-13's recorder wrapping that provider; and a `Dispatcher` started **from inside a live request**, by a controller action, so the pump inherits a flow on which `IHttpContextAccessor.HttpContext` is non-null and its `RequestServices` names the request's own DI scope
    - ten messages consumed while that request is still open: **every** consumer pipeline's ask carries **`AlwaysNew`** and the recorder shows ⚠ **zero adoptions** — each pipeline resolves a container-`Scoped` dependency that is **not** the controller's instance, and disposes it at the end of its own pipeline
    - **no `Warning`** naming any of FR-24's three conditions, because the ask was not a `JoinAmbient` one
    - the test does not license the pattern: starting a `Dispatcher` from inside a live request is erroneous use of the library, and what this pins is that the consumer side **fails toward isolation** when it happens
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T5.4. ⚠ **FR-23 does not reach this case** — the ambient is live, not stale — which is why the guarantee is placed on the pump's own flow rather than on its start site
  - **Depends on**: T6.20, T5.4
  - **References**: AC-55 (FR-19, C-14, D16); ADR 0075 step 4a, ADR 0073 (*What C-14 asks of this package*)

- [ ] **TEST + IMPLEMENT: T6.22 — not opted in, adoption behaviour is identical to today (one criterion, two test projects)**
  - **USE COMMAND**: `/test-first a host with no scope provider behaves exactly as before and a host that only references the ASP.NET package never touches IHttpContextAccessor`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests" **and** "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_no_scope_provider_is_registered_the_existing_suite_should_pass.cs` (in `Paramore.Brighter.Extensions.Tests`) **and** `When_the_package_is_referenced_but_the_extension_is_not_called_the_accessor_should_not_be_touched.cs` (in `Paramore.Brighter.Extensions.AspNetCore.Tests`)
  - ⚠ **This is one acceptance criterion living in two test projects, and that shape is deliberate. Do not merge the halves and do not split the criterion into two tasks.**
  - **Facts**: **2**, and ⚠ **this is the one task in the document whose facts live in two different files, in two different projects** — half 1's regression run in `Paramore.Brighter.Extensions.Tests`, and half 2's spy fact in `Paramore.Brighter.Extensions.AspNetCore.Tests`. That is the Overview's *"unless the task names a second file"* case, and it is the only one. ⚠ **Half 2 is a negative fact** — the spy's **zero accesses** *is* the assertion — so a green on half 1 discharges nothing of AC-14's second half. Gate once, on half 1. **Two facts, two files, still one criterion and one task**
  - Test should verify:
    - **half 1 (regression, `Paramore.Brighter.Extensions.Tests`)** — an application configured exactly as before this change: **no `IAmAScopeProvider` registered** and the affinity option left at its default. The existing Brighter test suite for `Send`, `Publish`, `Post`, `DepositPost` and consumption passes
    - the **named exclusions**, which are a floor and not a closed enumeration — in `tests/Paramore.Brighter.Extensions.Tests/`, all three re-derived as present at HEAD: `When_releasing_a_scoped_mapper_it_should_stay_usable_for_later_resolutions.cs`, `When_two_threads_first_resolve_a_scoped_mapper_concurrently_it_should_not_leak_a_scope.cs`, `When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it.cs`
    - ⚠ the **"Explicitly NOT excluded" pair** — `FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`FactoryLifetimeTests.cs:36`) and `AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`:154`) — **as migrated by T2.3 onto the handle path**, not duplicated. ADR 0071 step 6 amends AC-14 here: the pair is designated over the carrier that replaces the dictionary, and in that form must keep passing as regression guards for AC-9
    - **half 2 (spy, `Paramore.Brighter.Extensions.AspNetCore.Tests`)** — the project takes the new package's **reference** and ⚠ **never calls its registration extension**, with **no web host**, and an `IHttpContextAccessor` **spy** registered there. A `Send`, a `Publish` and a `Post` executed in that host: the spy records ⚠ **zero accesses**, and the three calls behave as they do with no package reference at all
    - ⚠ **why the spy half cannot live beside half 1**: `IHttpContextAccessor` lives in `Microsoft.AspNetCore.Http.Abstractions`, which no other Brighter test project references (re-derived: zero of the 37) and which the DI package may not acquire (NFR-2), so a spy registered beside the suite would record zero accesses whatever the package did — the assertion could not fail
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - require **no production change**: nothing in the package runs unless the extension is called. If half 1 fails, the fault is in an earlier phase; if half 2 fails, the package is doing something at startup that it must not
  - **Depends on**: T6.21, T2.3, T6.2
  - **References**: AC-14 (FR-11(a), FR-15), amended by ADR 0071 step 6; ADR 0073 steps 4, 4b

---

## Phase 7 — ADR 0074: lifetime validation, and the FR-25 guidance page

⚠ **ADR 0074 changes no code in `Paramore.Brighter`, so no Tidy-First step is owed IN CORE.** Its step 1 says so in terms — *"a comment amendment is neither structural nor behavioural"* — and the scope of that claim is core, not the phase. So the `PipelineValidator` XML-comment amendment lands inside **T7.5** rather than in a commit of its own, and no core-only tidy task is to be invented for it.

Two Tidy-First steps **are** owed in the DI package, and both are sequenced ahead of T7.1 for reasons each states: **T7.0a** lands the validation entities and the registration snapshot inert, and **T7.0b** widens the two validation hosts to many validators while there is still only one. Neither touches core.

- [ ] **STRUCTURAL: T7.0a — land the validation entities and the registration snapshot, inert**
  - **USE COMMAND**: `/tidy-first add the ContainerRegistrationSnapshot and the five scope-configuration entity types to the DI package with no rule and no caller`
  - Files, all new under `src/Paramore.Brighter.Extensions.DependencyInjection/`: `ContainerRegistrationSnapshot.cs`, `ScopeConfiguration.cs`, `DescriptorRecord.cs`, `ArtefactRegistration.cs`, `ArtefactKind.cs`, `ArtefactConstructorSelector.cs`
  - `ContainerRegistrationSnapshot` is built from an `IServiceCollection` and answers **three** queries (ADR 0074 step 2): the effective lifetime for a service type — the last **unkeyed** descriptor, matching Microsoft's resolution, or the last for a `(type, key)` pair where a parameter names one; the artefact candidates with their kinds, over keyed and unkeyed descriptors alike; and the `DescriptorRecord`s for a service type **in registration order**, each carrying the service key where there is one, the implementation type where one is statically known, the registration position where none is, and the `ImplementationInstance` where the descriptor supplies one
  - `ScopeConfiguration` and `ArtefactRegistration` are **records** rather than parameter lists, and `ArtefactConstructorSelector` is its own object — ADR 0074's *Technology Choices* argues both
  - **Nothing calls any of it in this commit.** No rule, no validator, no wiring in `ValidatePipelines()`
  - ⚠ **Why this is separated out.** ADR 0074's *Positive* section states that **"the rules are unit-testable without a host — a `ServiceCollection`, an options object and a `Type` are enough"**. Without this split, T7.1's single AC-27 test would drive eight new types plus the wiring in one red-green cycle, and the scaffolding for six rules that have no test yet would ship inside it
  - ⚠ **`ArtefactConstructorSelector` lands here as a shell** — the type and its signature, **no rule body**. D15's selection rule (the public constructor with the most parameters; where two public constructors have the same parameter count, the type is not inspected) is **behaviour, and lands in T7.5**, driven by AC-42's two constructor-selection clauses. ⚠ **The distinction is not "logic versus no logic"** — the snapshot's three queries above are more code than D15's rule, and they land here. It is that **D15 is a named design decision with an acceptance criterion driving it**: AC-42's two constructor-selection clauses exist to exercise that rule, so the rule belongs in the red-green cycle those clauses own, and writing it before them would be speculative implementation. The snapshot's queries are descriptor reads with **no criterion of their own and no caller** — nothing could gate them here, and T7.1's single AC-27 test is what first exercises them. The type is separated out only so that T7.5 does not also have to create the file. The rule is **testable with a `Type` alone**, which is why T7.5's two constructor-selection clauses can guard it **without running an application constructor**
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T6.22 (Phase 6 complete), T3.6
  - **References**: FR-22.1, FR-22.2, FR-22.3, FR-22.4, D15, C-20; ADR 0074 steps 2, 3

- [ ] **STRUCTURAL: T7.0b — widen both validation hosts to many validators, while there is still only one**
  - **USE COMMAND**: `/tidy-first widen the two validation hosted services and the twelve affected test sites from one IAmAPipelineValidator to many`
  - Files: `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterValidationHostedService.cs` — the field (`:47`), the constructor parameter (`:60`) and `StartAsync` (`:71`, validating at `:76`); `src/Paramore.Brighter.ServiceActivator.Extensions.Hosting/ServiceActivatorHostedService.cs` (`:50-54`). All four anchors re-derived at HEAD ✓
  - Change the field and constructor parameter to `IEnumerable<IAmAPipelineValidator>`, with `StartAsync` calling `PipelineValidationResult.Combine` over each `Validate()` **before** the existing throw-and-log block, which does not change. Change `ServiceActivatorHostedService` from `GetService` to `GetServices` and its `!= null` guard to an empty-sequence one; its throw-and-log block does not change either
  - ⚠ **An empty sequence must behave exactly as today's `null` did** — validate nothing, throw nothing. That is what keeps a host which never called `ValidatePipelines()` unaffected
  - ⚠ **Migrate the seven resolution sites in the existing suite in the same commit**, from `GetRequiredService<IAmAPipelineValidator>()` to `GetServices<IAmAPipelineValidator>()` combined through `PipelineValidationResult.Combine`. Re-derived at HEAD — **7 sites across 6 files**, and the discovery command is `grep -rn "GetRequiredService<IAmAPipelineValidator>\|GetService<IAmAPipelineValidator>" tests/ --include="*.cs"`:
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_validator_resolved_from_di_should_validate_through_full_path.cs:50`
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_validation_step_present_and_no_provider_through_di_should_surface_warning.cs:52`
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_validate_pipelines_with_producers_should_receive_publications.cs:57` **and** `:88` — two sites in one file
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_throw_on_error_true_with_transform_and_provider_triggers_should_not_block.cs:72`
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_publication_wrap_transform_unresolvable_through_di_should_surface_warning.cs:64`
    - `tests/Paramore.Brighter.Extensions.Tests/When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:60`
  - ⚠⚠ **And migrate the five CONSTRUCTION sites, which the grep above cannot find.** The change at `:60` is to a **constructor parameter**, so every site that calls the constructor breaks too — a different set from the resolution sites, and the reason to run a second discovery command rather than trust the first total:

    ```sh
    grep -rn "new BrighterValidationHostedService" tests/ --include="*.cs"
    ```

    Re-derived at HEAD — **5 sites across 4 files**, plus the two `BuildService` helper signatures that pass the argument through:
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_both_validate_and_describe_registered_should_describe_once.cs:51`
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_validation_hosted_service_starts_without_consumers_should_validate.cs:50`, and its helper signature at `:41`
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_throw_on_error_false_should_log_errors_not_throw.cs:48`, and its helper signature at `:42`
    - `tests/Paramore.Brighter.Core.Tests/Validation/When_validation_hosted_service_has_warnings_should_log_them.cs:49` **and** `:71` — two sites in one file

    **The total for this task is therefore seven resolution sites plus five construction sites, across nine files.** The two lists stay separate because only the resolution sites carry the silent-runtime hazard below — a construction site fails to **compile**, which is loud
  - ⚠ **`ServiceActivatorHostedService`'s constructor is deliberately NOT in scope, and its call sites must not be migrated.** It does not take the validator as a constructor parameter — it resolves it inside `StartAsync` (`:50`), and its constructor is `(logger, dispatcher, provider, options)`, untouched here. Re-derived at HEAD: its **11** construction sites across 4 files in `Paramore.Brighter.Extensions.Tests` all build `provider` from a real `ServiceCollection`/`BuildServiceProvider`, some registering the validator with `AddSingleton<IAmAPipelineValidator>` and two registering none, so `GetService` → `GetServices` preserves their behaviour exactly — including the two that register none, which is the case the empty-sequence rule above exists to protect. Named here so a later reader does not read the omission as an oversight
  - ⚠⚠ **Why this must land before T7.1, not after.** T7.1 adds a second `AddSingleton<IAmAPipelineValidator>` beside the existing `TryAddSingleton` at `BrighterPipelineValidationExtensions.cs:71`. From that commit the **last unkeyed descriptor wins**, so each of the seven sites silently resolves the *new* lifetime validator and then asserts the **core** validator's findings against it. That is a **green-to-red at runtime with no compile error to catch it** — unlike T2.3's migration, which the compiler forces. Landing the widening first makes T7.1's second registration purely additive
  - **Behaviour-preserving, and provably so**: while exactly one validator is registered, `GetServices` yields one and `Combine` over one result returns that result. `PipelineValidationResult.Combine(params PipelineValidationResult[])` already exists and is `public` (`src/Paramore.Brighter/Validation/PipelineValidationResult.cs:64`), so **nothing is added to core** and AC-22 clause 3 (T1.2) returns nothing new
  - Be release-noted **on both halves** — behavioural: an application-supplied `IAmAPipelineValidator` no longer replaces Brighter's validation wholesale, and both hosts now combine every registered validator; **source and binary**: `BrighterValidationHostedService`'s public constructor takes `IEnumerable<IAmAPipelineValidator>` in place of `IAmAPipelineValidator`, which is why five sites in this repository break (T7.14 item 12)
  - **Done when**: the solution builds and the full existing suite is green with **no registration added** — all twelve migrated sites included, the five construction sites among them
  - This is a **structural change** — it must NOT share a commit with behavioural change (Tidy First)
  - **Depends on**: T7.0a
  - **References**: FR-22.1, AC-24 (general clause); ADR 0074 step 5b

- [ ] **TEST + IMPLEMENT: T7.1 — a wholly inert opt-in is an error, in a producer host**
  - **USE COMMAND**: `/test-first a producer host that opts in with all three lifetimes Transient fails validation with a message naming the affinity and all three lifetimes`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_the_opt_in_is_inert_validation_should_report_an_error.cs`
  - **Facts**: **2**, both in this one file — the host with `throwOnError: true`, which must fail startup; and the same with `throwOnError: false`, which must **succeed** and log the identical message at `LogLevel.Error`. Gate once, on the first fact
  - Test should verify:
    - a **producer-only** host (`AddBrighter` alone, so `BrighterValidationHostedService` owns validation) with the affinity option `JoinAmbient`, triple `{Transient, Transient, Transient}` left at the defaults, and `ValidatePipelines()` called **last** with `throwOnError: true`
    - startup fails with `PipelineValidationException` whose message names the affinity setting, lists **all three** lifetimes and their values, states the opt-in has no effect, and contains the literal string `docs/guides/lifetimes-and-scoping.md`
    - with `throwOnError: false`: startup succeeds and the same message is logged at `LogLevel.Error`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - ⚠ **not** add the entities or the snapshot — **T7.0a** landed `ContainerRegistrationSnapshot`, `ScopeConfiguration`, `DescriptorRecord`, `ArtefactRegistration`, `ArtefactKind` and `ArtefactConstructorSelector` inert. This task adds only what AC-27 forces
    - add `ScopeConfigurationRules` carrying **the FR-22.1 specification alone** (step 4). The other six rules arrive with their own criteria in T7.3–T7.9; do not stub them
    - add `ScopeConfigurationValidator` (**public**, `internal` constructor because C# forbids a public constructor whose parameter types are less accessible), evaluating over core's public `ISpecification<T>`, `Specification<T>` and `ValidationResultCollector<T>` with **its own harvest loop** — `PipelineValidator.EvaluateSpecs` (`:152`) is **not** extracted, moved or widened, because there is no `InternalsVisibleTo` anywhere and lifting it would put permanent public API on core's `netstandard2.0` surface (step 5)
    - wire it in `ValidatePipelines()`: keep the existing `TryAddSingleton` returning the core validator at `:71` and add one `AddSingleton` returning this one (step 6). ⚠ This registration is **additive only because T7.0b already landed** — both hosted services now resolve `IEnumerable<IAmAPipelineValidator>` and combine, and the seven direct resolution sites in the existing suite were migrated with them. Without that, the second descriptor would win resolution and silently displace the core validator
    - read the three lifetimes and the affinity from the object **`IBrighterOptions` resolves to**, with the override already applied — not from `IOptions<BrighterOptions>.Value`, which is a different object on three of the four paths
  - **Depends on**: T7.0b (without which this task's own registration breaks the existing suite), T7.0a, T6.22 (Phase 6 complete), T3.6
  - **References**: AC-27 (FR-22.1, D5); ADR 0074 steps 2, 3, 4, 5, 6

- [ ] **TEST + IMPLEMENT: T7.2 — the same error fires in a consumer host**
  - **USE COMMAND**: `/test-first a consumer host that opts in with all three lifetimes Transient fails validation through the ServiceActivator hosted service`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_consumer_host_owns_validation_the_inert_opt_in_error_should_still_fire.cs`
  - Test should verify:
    - ⚠ the T7.1 configuration in a host that calls `AddBrighter(Action<BrighterOptions>)` **before** `AddConsumers(Action<ConsumersOptions>)` — **the order and overload C-12 requires an AC of this shape to state**, because `IBrighterOptions` is `TryAddSingleton` on both sides and the producer's `BrighterOptions` must win it in order to be the object the validator reads. In the reverse order `IBrighterOptions` resolves to the `ConsumersOptions` instance and the error does not fire at all
    - so that `ConsumerOwnsValidation` is `true` and `BrighterValidationHostedService.StartAsync` returns immediately (`:73`)
    - ⚠ **and that explicitly registers `services.AddHostedService<ServiceActivatorHostedService>()`** from `Paramore.Brighter.ServiceActivator.Extensions.Hosting`, because `AddConsumers` does **not** register it and without it **no host owns validation at all** (C-15, D14)
    - startup still fails with `PipelineValidationException` carrying the same message — surfaced by `ServiceActivatorHostedService`, not `BrighterValidationHostedService`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - ⚠ **need nothing new.** **T7.0b** widened both hosted services to `IEnumerable<IAmAPipelineValidator>` and made them combine; **T7.1** registered the lifetime validator beside the core one. What this criterion adds is the proof that the error still reaches the caller when the **consumer** host owns validation — surfaced by `ServiceActivatorHostedService`, not `BrighterValidationHostedService`. If it fails, the fault is in T7.0b's widening or T7.1's registration, not here
  - **Depends on**: T7.1, T7.0b
  - **References**: AC-40 (FR-22.1, C-12, C-15, D14), AC-24 (general clause); ADR 0074 step 5b

- [ ] **TEST + IMPLEMENT: T7.3 — mixing `Transient` and `Scoped` is an error under either affinity setting**
  - **USE COMMAND**: `/test-first a host mixing Transient and Scoped across the three lifetimes fails validation under both affinity settings`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_transient_and_scoped_are_mixed_validation_should_report_an_error.cs`
  - **Facts**: **3**, all in this one file — the `JoinAmbient` run and the `AlwaysNew` run, which must **both** fail because the error is not conditional on affinity; and the `throwOnError: false` run, which must succeed and log. Gate once, on the first fact
  - Test should verify:
    - a producer-only host with `{HandlerLifetime = Scoped, MapperLifetime = Transient, TransformerLifetime = Scoped}` and `ValidatePipelines()` called last with `throwOnError: true`, run once with affinity `JoinAmbient` and once with `AlwaysNew`
    - **both** runs fail with `PipelineValidationException` whose message lists all three lifetimes with their values, states the mixed pair do not share pipeline-scoped dependencies, and contains `docs/guides/lifetimes-and-scoping.md` — **the error is not conditional on affinity**
    - with `throwOnError: false`: startup succeeds and the same message is logged at `LogLevel.Error`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - implement FR-22.2 as: discard any of the three that is `Singleton`; the remainder must all be equal
    - ⚠ **not** name the three `ServiceLifetime` values positionally in a way that a transposition survives. Transposing `MapperLifetime` and `TransformerLifetime` produces a rule that still passes AC-41 and still fails AC-28, and nothing would catch it until AC-42's kind-varying cases
  - **Depends on**: T7.2
  - **References**: AC-28 (FR-22.2, C-18, D8); ADR 0074 step 4

- [ ] **TEST + IMPLEMENT: T7.4 — `Singleton` is excluded from the consistency rule**
  - **USE COMMAND**: `/test-first Singleton lifetimes are discarded before the consistency rule compares the remainder`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_lifetime_is_singleton_it_should_be_discarded_from_the_consistency_rule.cs`
  - **Facts**: **5**, all in this one file — the four triples that must raise **nothing** (`{Scoped, Singleton, Scoped}`, `{Singleton, Singleton, Singleton}`, `{Transient, Singleton, Transient}`, `{Transient, Transient, Transient}`), and the fifth `{Scoped, Singleton, Transient}` that **must** fail. ⚠ Four of the five are negative; only the fifth proves the rule still fires. Gate once, on the first fact
  - Test should verify:
    - a producer-only host with `ValidatePipelines()` called last with `throwOnError: true`, over four triples: `{Scoped, Singleton, Scoped}`, `{Singleton, Singleton, Singleton}`, `{Transient, Singleton, Transient}` and `{Transient, Transient, Transient}` — **none** raises the consistency error
    - a fifth triple `{Scoped, Singleton, Transient}` — startup **does** fail with the consistency error, because discarding `Singleton` leaves `{Scoped, Transient}`
    - the exclusion is required for consistency with FR-20, which prescribes `MapperLifetime = Singleton` as the migration this spec ships
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need nothing beyond T7.3's rule if the discard is written as the rule states
  - **Depends on**: T7.3
  - **References**: AC-41 (FR-22.2, FR-20, D8); ADR 0074 step 4

- [ ] **TEST + IMPLEMENT: T7.5 — a captive dependency on a `Singleton` artefact is a warning, and the detection contract is pinned**
  - **USE COMMAND**: `/test-first a Singleton mapper requiring a Scoped service is reported as one warning and the four bounds of the detection contract are pinned`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_singleton_artefact_requires_a_scoped_service_validation_should_warn.cs`
  - **Facts**: **9**, all in this one file — the nine mapper/transform/host configurations AC-42 enumerates. ⚠ Re-derived: **six of the nine assert *no* warning** and only three assert one, so a green on the first fact proves very little. ⚠ Note in particular the **pair sharing the `Paramore.Brighter.Extensions.Tests` assembly and differing in outcome** — the prefix-excluded *transform* asserts **no** warning, while the C-20(iv) *mapper* in that same assembly asserts **one**. Same assembly, opposite outcome. Gate once, on the first fact
  - Test should verify:
    - a producer-only host with `{Transient, Singleton, Transient}` — FR-22.2-conformant because `Singleton` is discarded and the remainder is uniform — and `ValidatePipelines()` called last with `throwOnError: true`
    - a mapper whose **single** constructor requires `IOrderDbContext` registered `AddScoped`: startup **succeeds** (a warning, not an error) and **exactly one** warning names both the mapper type and `IOrderDbContext`, and contains `docs/guides/lifetimes-and-scoping.md`
    - a mapper requiring only `Singleton`- and `Transient`-registered services: **no** warning
    - ⚠ a mapper requiring a `Transient` service that *itself* requires the `AddScoped` `IOrderDbContext`: **no** warning — pinning C-20(ii)'s direct-parameter-only limit as intended
    - a mapper with two public constructors, a wider `(ISomeSingleton, ISomeTransient, IOrderDbContext)` and a narrower `(ISomeSingleton)`: a warning **is** reported naming `IOrderDbContext` (D15 — most parameters)
    - a `Singleton` handler decorated with `[UsePolicyAsync]` so `ExceptionPolicyHandlerAsync<>` joins its pipeline: **no** warning against that decorator — the *handler* half of the exclusion mechanism
    - `{_, _, TransformerLifetime = Singleton}` using Brighter's own `ClaimCheckTransformer` with `IAmAStorageProvider` and `IAmAStorageProviderAsync` registered `AddScoped`: **no** warning — the *transform* half via `TransformAttribute.GetHandlerType()`, which `RequestHandlerAttribute` never reaches. ⚠ This case does **not** pin the prefix rule
    - ⚠ `{Transient, Transient, Singleton}` with a mapper decorated by a `[WrapWith]` transform **defined in the test assembly `Paramore.Brighter.Extensions.Tests`** requiring the `AddScoped` `IOrderDbContext`: **no** warning — the assembly-name **prefix** match. This is the only case that fails under `== "Paramore.Brighter"` and passes under the prefix rule, and no Brighter-shipped out-of-core transform can substitute (all are parameterless today)
    - ⚠ the *same* `Paramore.Brighter.Extensions.Tests` assembly with a `Singleton` **mapper** requiring the `AddScoped` `IOrderDbContext`, triple `{Transient, Singleton, Transient}`: a warning **is** reported — pinning C-20(iv)'s gap as a deliberate **asymmetry**. Same assembly, opposite outcome
    - a mapper with two public constructors of the **same** parameter count, one taking `IOrderDbContext` and one not: **no** warning. ⚠ That mapper is **not activatable by MS DI at all**, so the test asserts **validation output only and must not resolve the mapper**
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - ⚠ **not** create `ArtefactConstructorSelector` — **T7.0a** landed the type and its signature as a shell. **Fill in its rule body**, implementing **only** D15's rule (widest, tie → none, no public constructor or parameterless → nothing inspected), which is what AC-42's two constructor-selection clauses drive
    - add `ArtefactExclusionSet.Build(pipelineBuilder, registry, publications, subscriptions)`. The **handler half** comes from `PipelineBuilder<IRequest>.Describe()` (`PipelineBuilder.cs:151`), an *instance* method, so the registration delegate constructs a builder of its own over its own `sp`, exactly as `BrighterPipelineValidationExtensions.cs:73-75` does; the **transform half** from `TransformPipelineBuilder.DescribeTransforms` (`:270`) called with ⚠ **`includeAsync: true`** — the two-argument overload at `:255` defaults it to `false`, under which a transform declared only on an async-resolved mapper is warned against as the application's
    - ⚠ walk **only the transform half** over request types. `Describe()` is parameterless and enumerates the subscriber registry itself (`:146-162`); an implementation that walks the handler half over publications and subscriptions produces an empty transform half in a host whose `ResolvePublications` returns `null` (`:135-142`), and then warns against the very transform the `Paramore.Brighter.Extensions.Tests` clause asserts no warning for
    - add `ValidationMapperRegistry` (registered in `ValidatePipelines()`), wrapping a `Lazy<MessageMapperRegistry>?` — null exactly when no `ServiceCollectionMessageMapperRegistryBuilder` was registered — exposing `Value` for the exclusion set and `Factory` for `PipelineValidator`'s existing `mapperRegistryFactory`, both null over the same condition. It is `IDisposable` and the container drains it. Double disposal is safe because `MessageMapperRegistry.Dispose()` claims with a single `Interlocked.Exchange` (`:360-362`)
    - ⚠ **amend the `mapperRegistryFactory` XML doc on `PipelineValidator.cs:45-51`** — the one thing this ADR changes in `Paramore.Brighter`, and it lands with the change that makes it true, not in a Tidy-First commit of its own. The amended text says the registry may be forced by the caller and shared with it, and that the `Interlocked` claim is what makes the shared ownership safe. A comment is not API, so AC-22 clause 3 (T1.2) is untouched
    - read, for each parameter, the `ServiceLifetime` of the **last unkeyed** descriptor, or the last for the `(type, key)` pair where the parameter carries `[FromKeyedServices(key)]`; a parameter with no descriptor is **not** a finding
    - resolve nothing and run no application constructor
  - **Depends on**: T7.4
  - **References**: AC-42 (FR-22.3, C-20, D15); ADR 0074 steps 3, 4, 5a, 5b

- [ ] **TEST + IMPLEMENT: T7.6 — an opt-in defeated by an application's own `IBrighterOptions` registration is reported, not silent**
  - **USE COMMAND**: `/test-first a host whose own IBrighterOptions registration defeats the opt-in is reported as one error on every path and in either ordering`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_application_registers_its_own_brighter_options_the_defeated_opt_in_should_be_reported.cs`
  - ⚠ **Why this project and not `Extensions.Tests`.** AC-50's *Given* is **an ASP.NET host**, its *When* is **a controller action calls `Send`**, and two of its *Then* clauses turn on the controller's own `Scoped` instance — the base host asserts the handler does **not** resolve it, the control host asserts it **does**. Neither is expressible without a controller. Every one of the nine facts also calls `AddBrighterRequestScope()`, so all nine need the package reference regardless; splitting the criterion would not avoid that and would break a criterion the ACs treat as one
  - **Facts**: **9**, all in this one file — the base host (`throwOnError: false`); the same with `throwOnError: true`; the application registration placed *after* `AddBrighter`; the extension passed `AlwaysNew` over a pre-registered `AlwaysNew`; the extension placed *before* `AddBrighter`; one host on **each of the other three entry points**; and the **control host**. ⚠ The last is the one that makes the other eight mean anything — it must produce **no** finding. Gate once, on the first fact
  - Test should verify:
    - a host registering its own options object **before** Brighter — `services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = Scoped, MapperLifetime = Scoped, TransformerLifetime = Scoped })`, an FR-22.2-conformant triple so no lifetime rule fires — then `AddBrighter(Action<BrighterOptions>)` whose delegate sets no lifetime, and `AddBrighterRequestScope()` with no argument; `ValidatePipelines()` called **last** with ⚠ **`throwOnError: false`**, so startup proceeds and the runtime clauses are reachable
    - the resolved `IBrighterOptions` carries **`AlwaysNew`** and the handler does **not** resolve the controller's `Scoped` instance
    - **exactly one `Error`**, naming the affinity the extension registered, stating that the resolved `IBrighterOptions` was supplied by the application rather than by Brighter so the override was never applied, giving the remedy, and containing `docs/guides/lifetimes-and-scoping.md`
    - **no *other* finding** — the triple is conformant and, the override having been defeated, the resolved affinity is `AlwaysNew`, so FR-22.1's `JoinAmbient` precondition fails
    - with **`throwOnError: true`**: startup fails with `PipelineValidationException` carrying that message and no controller action runs
    - ⚠ the application's registration placed **after** `AddBrighter` — a plain `AddSingleton` that does not contest the `TryAdd` but wins resolution as the **last unkeyed** descriptor: the same single `Error`. **This branch fails under a mechanism that only records whether Brighter's own `TryAddSingleton` found the service already present**
    - ⚠ the extension passed **`ScopeAffinity.AlwaysNew`** over a pre-registered `IBrighterOptions` that also carries `AlwaysNew` — override and resolved object holding the *same* value: the same single `Error`. **This is the only branch that fails under an implementation comparing the override's affinity with the resolved object's**
    - the **extension call placed before `AddBrighter`** — AC-48's ordering, on which the extension is required to win: the same single `Error`; the defeat is not an ordering failure of the extension
    - a host of the same shape on **each of the other three entry points**. ⚠ **The two consumer hosts must pre-register a `ConsumersOptions` instance as `IBrighterOptions`, not a bare `BrighterOptions`** — the `Func` overload binds `IAmConsumerOptions` by casting whatever `IBrighterOptions` resolves to (`ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:89-90`), so a bare `BrighterOptions` throws `InvalidCastException` while the dispatcher is constructed, before any validation host starts (C-12). Both consumer hosts register `ServiceActivatorHostedService` explicitly (C-15, D14)
    - a **control host** identical to the first except that the application's registration is removed and the same `{Scoped, Scoped, Scoped}` triple is set in the `AddBrighter(Action<BrighterOptions>)` delegate instead: **no** finding, the resolved options carry `JoinAmbient`, and the handler resolves the controller's own instance — so the `Error` is attributable to the defeated registration and not to the host shape
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - implement FR-22.4 as a rule about **registrations, not values**, with two conjuncts: an affinity override is present in the snapshot, **and** the `IBrighterOptions` descriptor the container will resolve — the **last unkeyed** one — is not the one Brighter's own registration produced
    - answer the second conjunct by asking ADR 0076's `BrighterOptionsRegistration` (T3.5), which arrives as the `ImplementationInstance` of a descriptor for its own service type and so needs no query of its own. **Do not attempt to recognise Brighter's registration by inspecting a delegate**
    - read the **unkeyed** population only: Brighter's factories resolve `IBrighterOptions` unkeyed, so a rule taking "the last descriptor of any kind" would raise a false `Error` against a host that registered a keyed `IBrighterOptions` for its own purposes
    - note the two hosts the rule deliberately leaves silent: an application that registers `IBrighterOptions` and never opts in (re-derived at HEAD: **125 files under `tests/` register `IBrighterOptions` themselves**, none of which opts in), and a mixed host where whichever side won registered through `RegisterBrighterOptions`
  - **Depends on**: T7.5, T3.5, T6.2 (the test project and its ASP.NET host)
  - **References**: AC-50 (FR-22.4, FR-17, FR-14, C-12, C-15, D14, D18); ADR 0074 step 4, ADR 0076 step 2

- [ ] **TEST + IMPLEMENT: T7.7 — duplicate scope providers: the last registration wins, and the duplicate is reported**
  - **USE COMMAND**: `/test-first two distinct scope providers produce one warning naming both and the pipeline resolves through the last registered one`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_two_distinct_scope_providers_are_registered_validation_should_report_the_duplicate.cs`
  - **Facts**: **2**, both in this one file — the host registering two **distinct** provider implementations, and ⚠ the control registering the **same** implementation type twice, which must produce **no** warning. Without the control, an implementation warning on any two descriptors passes. Gate once, on the first fact
  - Test should verify:
    - two **distinct** `IAmAScopeProvider` implementations, each registered with a plain `services.AddSingleton<IAmAScopeProvider, T>()` in a **stated order** (FR-24.3 forbids `TryAdd` here precisely so both descriptors exist), `ValidatePipelines()` called **after both registrations** (C-15's snapshot semantics), and lifetime triple `{Scoped, Scoped, Scoped}`
    - validation reports **exactly one** warning naming **both** provider types, identifying the **last**-registered one as effective, and containing `docs/guides/lifetimes-and-scoping.md`
    - the pipeline resolved via the **last**-registered provider, matching MS DI's resolution of the service type
    - the *same* implementation type registered twice instead: **no** warning — idempotent in effect and not a finding
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - take distinctness over **implementation types**, over the **unkeyed** descriptors only — a keyed provider registration is never effective, so counting it would report a conflict between one provider that is used and one that cannot be
    - fall back, for a descriptor registered by factory delegate or instance, to its **registration position**, and its runtime type where `ImplementationInstance` supplies one
    - rely on Brighter registering **no default provider** (D11), so the ASP.NET extension can never itself create a duplicate — two application registrations are the only way to reach this rule
  - **Depends on**: T7.6
  - **References**: AC-32 (FR-24.3, C-15, D11); ADR 0074 step 4, ADR 0072 step 5

- [ ] **TEST + IMPLEMENT: T7.8 — a repeated opt-in resolves to the last call, and a conflicting repeat is reported**
  - **USE COMMAND**: `/test-first calling the registration extension twice with different affinities takes the last call's value and reports one warning naming both`
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"
  - Test file: `When_the_registration_extension_is_called_twice_the_last_call_should_win_and_be_reported.cs`
  - **Facts**: **3**, all in this one file — the host calling the extension `AlwaysNew` then `JoinAmbient`; the second host in the **reversed** order, which pins *last call wins* rather than *most permissive wins*; and ⚠ the third host calling it twice with the **same** affinity, which must report **no** finding. Gate once, on the first fact
  - Test should verify:
    - an ASP.NET host, lifetime triple `{Scoped, Scoped, Scoped}`, calling the extension **twice** — first `ScopeAffinity.AlwaysNew`, then `ScopeAffinity.JoinAmbient` — with `ValidatePipelines()` called **after both calls**
    - the resolved `IBrighterOptions` carries **`JoinAmbient`** and the handler resolves the controller's own `Scoped` instance, so the affinity and the provider agree on which call won
    - validation reports **exactly one** `Warning` naming both `AlwaysNew` and `JoinAmbient`, identifying `JoinAmbient` as effective, and containing `docs/guides/lifetimes-and-scoping.md`
    - ⚠ **no *duplicate-provider* finding alongside it** — both calls register the same provider implementation type, which FR-24.3 excludes in terms; that exclusion is exactly why this is a sixth rule rather than a wider fifth one
    - a second host calling the extension in the **reversed** order: the resolved options carry `AlwaysNew`, nothing adopts, and the same single `Warning` names both values — the rule is "last call wins", **not** "the more permissive value wins"
    - a third host calling the extension twice with the **same** affinity: that affinity is effective and validation reports **no** finding
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - take distinctness over the `ScopeAffinity` **value**, read from the descriptors' `ImplementationInstance` — which exists because the extension registers `ScopeAffinityOverride` as a **constructed instance** under a plain `AddSingleton` (T6.3)
    - ⚠ **not** borrow FR-24.3's registration-position fallback. That key is an implementation type, for which a position is a defensible "unknown, treat as distinct"; this key is a **value**, and two positions are always distinct, so the fallback would turn the idempotent repeat FR-17 exempts into a `Warning` and falsify the third branch
    - need no new input — the descriptors are already in the `ValidatePipelines()`-time snapshot
  - **Depends on**: T7.7, T6.3
  - **References**: AC-49 (FR-17, FR-25, NFR-10, C-15); ADR 0074 step 4, ADR 0073 (*What a repeated call resolves to*)

- [ ] **TEST + IMPLEMENT: T7.9 — an affinity override registered by factory delegate is reported, and the repeat it hides is what the report is about**
  - **USE COMMAND**: `/test-first an affinity override registered by factory delegate produces a warning about the registration shape while still taking effect`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_an_affinity_override_is_registered_by_factory_delegate_validation_should_report_the_shape.cs`
  - **Facts**: **3**, all in this one file — the factory-delegate host, which must **succeed** under `throwOnError: true` and report one `Warning`; the second host registering **two** overrides, where FR-17's repeated-opt-in `Warning` must **not** appear alongside it; and ⚠ the third host registering its override as a **constructed instance**, which must report **no** finding at all. Gate once, on the first fact
  - Test should verify:
    - a host with an FR-22.2-conformant `{Scoped, Scoped, Scoped}` triple registering an affinity override **by factory delegate** — `AddSingleton(sp => new ScopeAffinityOverride(x))`, the shape a third-party opt-in package can write since the type is public in the DI package — with `ValidatePipelines()` called last and ⚠ **`throwOnError: true` (the default, which a warning must not trip)**
    - startup **succeeds**, and **exactly one** `Warning` states that an affinity override is registered by factory delegate, that its value cannot be read without resolving, that the remedy is to register it as a constructed instance, and contains `docs/guides/lifetimes-and-scoping.md`
    - ⚠ the affinity that override carries **is** the effective one on the resolved `IBrighterOptions` — the finding is about **reportability, not a lost opt-in**, and an implementation that treated the delegate registration as a defeat would fail here
    - a second host registering **two** overrides carrying **different** affinities, one as an instance and one by factory delegate: the unreadable-override `Warning` is reported and FR-17's repeated-opt-in `Warning` is **not** — the delegate descriptor contributes nothing to the distinctness set, which is precisely the silence this rule exists to break
    - a third host registering its override as a **constructed instance**, as the extension does: **no** finding — so the `Warning` is attributable to the registration shape and not to the presence of an override
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - read the shape, not the value: a delegate-registered descriptor has `ImplementationInstance == null` and `ImplementationFactory != null`, both already in the snapshot; no resolution and no comparison
    - keep it a `Warning`, because nothing about such a host is broken — the affinity the registrar passed is the affinity the factories read, and what is lost is a diagnostic
  - **Depends on**: T7.8
  - **References**: AC-53 (FR-17); ADR 0074 step 4 (*FR-17's second rule*), ADR 0076 step 2

- [ ] **TEST + IMPLEMENT: T7.10 — every validation message points at the guidance page**
  - **USE COMMAND**: `/test-first all seven validation messages contain the guidance page path`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_validation_rule_reports_it_should_name_the_guidance_page.cs`
  - **Facts**: **7**, all in this one file — one host per validation message, each configured to trigger **exactly one** finding. ⚠ Do not reuse T7.6's multi-finding hosts or T6.10's host, which never calls `ValidatePipelines()`. Gate once, on the first fact
  - Test should verify:
    - **seven hosts, each configured to trigger exactly one finding** — inert opt-in (FR-22.1, T7.1), mixed `Transient`/`Scoped` (FR-22.2, T7.3), captive dependency (FR-22.3, T7.5), a defeated opt-in (FR-22.4, T7.6), duplicate scope provider (FR-24.3, T7.7), a conflicting repeated opt-in (FR-17, T7.8) and an unreadable override registered by factory delegate (FR-17, T7.9)
    - all seven messages — **three errors** (FR-22.1, FR-22.2, FR-22.4) and **four warnings** (FR-22.3, FR-24.3, FR-17's two) — contain the literal string `docs/guides/lifetimes-and-scoping.md`; the duplicate-provider warning and both FR-17 warnings carry that obligation from FR-24.3 and FR-17 respectively, **not** from FR-22
    - **no message states only that the configuration is wrong without naming that page**
    - ⚠ each host triggers **exactly one** finding, so T6.10's host (which does not call `ValidatePipelines()`) and T7.6's multi-finding branches must not be reused here
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - need no new rule — it is the cross-cutting guard over the seven. If a message fails it, that rule's message text is at fault
  - **Depends on**: T7.9
  - **References**: AC-43 (FR-17, FR-22, FR-24.3, FR-25, NFR-10); ADR 0074 step 4 (*The seven rules* table)

- [ ] **DOC: T7.11 — guidance page part 2: the decision guide (FR-25.9)**
  - No test. Documentation whose substance is fixed by FR-22.2's rule and is **derived, not authored**
  - **Verified by**: a line on the PR checklist, and by **T7.15**'s reviewer walk — for each of the seven validation messages, the path message → troubleshooting entry → decision guide must yield a conformant **triple** or a specific corrective registration, without reading Brighter's source
  - File: `docs/guides/lifetimes-and-scoping.md`
  - Content:
    - a **decision guide framed as the joint choice** FR-22.2 makes it, not as nine independent per-kind cells — a guide answering "when is a `Scoped` mapper right?" on its own would produce configurations that fail startup, which NFR-10 forbids
    - ⚠ **the table of passing configurations is derived from FR-22.2's rule, not authored**: discard any of the three that is `Singleton` and the remainder must be uniform, so the passing set is exactly `{Transient, Transient, Transient}` and `{Scoped, Scoped, Scoped}`, and either with any subset of members replaced by `Singleton`, less those FR-22.1 then rejects under `JoinAmbient` because nothing remains `Scoped`. **State the set with each entry's cost and when it is right; do not restate the rule**, so that if the rule ever changes the table follows from it rather than drifting against it
    - a per-kind note for handler, mapper and transform saying when *that* kind should be `Singleton` (stateless, or deliberately caching across pipelines — with the captive-dependency caveat)
    - explicit answers to: when `Scoped` is needed rather than `Transient` (sharing a `DbContext`, transaction or other pipeline-scoped dependency across the pipeline, and adopting the caller's scope — **stating that this choice moves all three lifetimes together**); when `Singleton` is right; and why `Transient` remains the default — each **tied to the situation it belongs to**, because FR-1 makes `Scoped` the fix for per-pipeline state while FR-20 prescribes `Singleton` as the migration for cross-pipeline caching, and the three answers otherwise read as contradictory
    - prescriptive enough that a reader hitting the FR-22.2 error can pick a conformant **triple** without reading the source
  - **Depends on**: T7.10, T5.5
  - **References**: AC-25 (decision-guide clause), FR-25.9, NFR-10, C-18; ADR 0074 (clause-to-source map, clause 9; *Clause 9's table is derived, not authored*)

- [ ] **DOC: T7.12 — guidance page part 3: the troubleshooting section, keyed to each of the seven validation messages (FR-25.10)**
  - No test beyond AC-44's reviewer walk (T7.15)
  - File: `docs/guides/lifetimes-and-scoping.md`
  - Content:
    - a statement that validation reaches an application only if it calls `ValidatePipelines()` **and** a validation host runs — ⚠ naming the consumer case where `AddConsumers` defers to a `ServiceActivatorHostedService` **the application must itself register** (C-15, D14)
    - ⚠ **"call `ValidatePipelines()` last" must be stated in the stronger form** ADR 0074 requires: hold the `IBrighterBuilder` and call `ValidatePipelines()` as a **separate statement after every other registration**, rather than chaining it onto `AddBrighter`. In the natural fluent form an application's own `services.AddSingleton<IBrighterOptions>(...)` sits after the snapshot and FR-22.4 is defeated by the very shape it exists to catch
    - a troubleshooting entry for **each of the seven** messages, each stating **cause and remedy**: FR-22's four (inert opt-in, mixed `Transient`/`Scoped`, captive dependency, a defeated opt-in), FR-24.3's duplicate provider, and FR-17's two (a conflicting repeated opt-in, and an override registered by factory delegate whose value cannot be read). **Three are errors** (FR-22.1, FR-22.2, FR-22.4) and **four are warnings** (FR-22.3, FR-24.3, FR-17's two)
    - each remedy of the kind AC-44 requires: a specific **triple** for the three lifetime-related messages; a specific corrective **registration action** for the duplicate-provider, unreadable-override, repeated-opt-in and defeated-opt-in messages, including which registration to remove, which value takes effect if the fault is left, and what is lost until it is fixed
  - **Depends on**: T7.11
  - **References**: AC-25 (troubleshooting clause), AC-43, AC-44, FR-25.10, NFR-10, C-15, D14; ADR 0074 (clause-to-source map, clause 10; *Both host shapes, enumerated*)

- [ ] **DOC: T7.13 — guidance page part 4: the transaction consequence, the breaks and migration, the captive-dependency hazard, and the extension's three gestures**
  - No test beyond AC-36's read and AC-44's walk. **Verified by**: **T7.15**'s reviewer walk
  - File: `docs/guides/lifetimes-and-scoping.md`
  - Content:
    - **FR-25.5 (AC-36)** — that in-process `Publish` subscribers, **and pipelines nested inside them**, cannot join a transaction the caller opened, and that **the outbox is the answer**. Substance is ADR 0075's, from its subscriber brackets; nothing is re-decided
    - **FR-25.6** — the `MapperLifetime.Scoped` break and its migration to `Singleton`, with no compatibility flag — **including the joint consequence**: a per-pipeline `Scoped` mapper requires all three lifetimes set together, and `{Scoped, Scoped, Transient}` is not a valid destination
    - **FR-25.7** — that handlers, mappers and transforms must not mix `Transient` and `Scoped`, that `Singleton` is excluded from that rule, that adopting `Scoped` therefore moves all three lifetimes together, and that this is enforced **only when `ValidatePipelines()` is called**
    - **FR-25.8** — the captive-dependency hazard for `Singleton` artefacts and its interaction with the FR-20 migration (moving a mapper to `Singleton` is safe **only if** it has no container-`Scoped` dependencies), ⚠ stating that Brighter's warning inspects **direct constructor parameters only** and that the container's own **`ValidateScopes` remains the complete check**. The two bounds a reader cannot diagnose alone must be prepared for: a warning naming a Brighter type (C-20(iv)) and one naming a constructor the container never uses (C-20(i))
    - **FR-25.11** — that the extension's affinity **argument** is the value; that assigning `DefaultScopeAffinity` alongside the extension is a configuration error whose outcome is the extension's value, on every registration path and in any order, and that **validation does not report it** because no sentinel exists to distinguish it from the ordinary opt-in; and **the correct gesture for each of the three intents**: opt in with `AddBrighterRequestScope()`; register the ambient source without opting in with `AddBrighterRequestScope(ScopeAffinity.AlwaysNew)`; opt out entirely by not calling the extension. ⚠ Also state the **one thing the argument does not beat** — an application that registers `IBrighterOptions` itself, which defeats the write-through in either ordering and is carried in T7.12's troubleshooting section as one of the seven messages
  - **Depends on**: T7.12
  - **References**: AC-36 (C-4, FR-25.5), AC-25, AC-24, FR-25.6, FR-25.7, FR-25.8, FR-25.11, C-18, C-20, D18; ADR 0074 (clause-to-source map, clauses 5–8, 11), ADR 0075 step 7, ADR 0073 step 5, ADR 0076 step 4

- [ ] **DOC: T7.14 — `release_notes.md`: one entry, thirteen breaking-change items**
  - No test. Verified by a PR checklist, **one line per item in the entry** (AC-24's verifier)
  - File: `release_notes.md` — ⚠ **a single entry**, so that a reader upgrading sees one list rather than several unrelated ones. **No sibling ADR opens a second entry, and no item is numbered**, because the order they are written in is not a fact about the release
  - **ADR 0070's five**:
    1. *Behavioural* — `MapperLifetime.Scoped` stops meaning "one instance for the process" and starts meaning "one instance per pipeline"; no compile error warns of it; the migration is `MapperLifetime = Singleton`; there is **no compatibility flag** (FR-20, T1.5)
    2. *Source and binary* — the six factory and registry interfaces gain `CreatePipelineScope()` and a scope parameter, naming each and stating the migration (T1.3)
    3. *Behavioural* — a `Scoped` mapper or transformer factory no longer caches at factory level; a direct `Create(type)` resolves fresh (T1.14)
    4. *Binary* — the six transform-pipeline constructors gain a defaulted trailing `IAmAScope?`: `WrapPipeline.cs:53`, `UnwrapPipeline.cs:45`, `WrapPipelineAsync.cs:57`, `UnwrapPipelineAsync.cs:47` and the two abstract bases (`TransformPipeline.cs:21`, `TransformPipelineAsync.cs:22`). All six types are public; source-compatible for a caller that recompiles, binary-breaking for one that does not (T1.5)
    5. *Behavioural* — a pipeline scope's disposal failure is no longer swallowed inside the DI package: it is reported at `Error` as `FailedToDisposePipelineScope` or `FailedToDisposePipelineScopeAfterFailedBuild`, then swallowed one layer up. An operator's log level and message change; no exception reaches a caller that did not already see one (T1.10)
  - **The siblings' eight, each a one-line pointer to the *Consequences* bullet that states it**:
    6. *Source and binary, ADR 0071* — `IAmAHandlerFactory` gains `CreatePipelineScope()` and `IAmALifetime` gains `PipelineScope`, so **eight** interfaces break across the two ADRs rather than six, three of them not factories (the two mapper registries and `IAmALifetime`) (T2.1)
    7. *Behavioural, ADR 0071* — `HandlerLifetimeScope.Dispose()` is repaired to survive a throwing handler `Release`, so an exception a caller catches today only reaches the log afterwards (T2.2)
    8. *Behavioural, ADR 0071* — `ServiceProviderHandlerFactory` stops keeping a DI scope of its own; a `Create` given a lifetime whose `PipelineScope` is `null`, on a non-`Singleton` handler lifetime, throws `ConfigurationException` (T2.3)
    9. *Behavioural, ADR 0072* — the `Scoped` artefact cache stops publishing a faulted `Lazy`, on the owned path as well as the borrowed one, so it reaches a host that never opts in (issue #4260's `Scoped` half; the `Singleton` cache is unchanged) (T4.9)
    10. *Binary, ADR 0075* — `PipelineBuilder<TRequest>`'s two public dispatch constructors gain a defaulted `bool isolateSubscribers` (T5.1)
    11. *Source and binary, ADR 0076* — `IBrighterOptions` gains `DefaultScopeAffinity`, breaking a hand-rolled implementation (T3.4)
    12. *Behavioural **and** source and binary, ADR 0074* — both validation hosted services resolve every registered `IAmAPipelineValidator` and combine the results, so an application that registers its own no longer replaces Brighter's validation wholesale; `GetService<IAmAPipelineValidator>()` now returns whichever descriptor is last. ⚠ **The source and binary half**: `BrighterValidationHostedService`'s public constructor takes `IEnumerable<IAmAPipelineValidator>` in place of `IAmAPipelineValidator`, so any caller constructing it directly must migrate. ⚠ Name **only** that type — `ServiceActivatorHostedService` resolves its validator inside `StartAsync` and its constructor is unchanged (T7.0b)
    13. *Compatibility, ADR 0074* — C-18's note: an application that calls `ValidatePipelines()` and mixes `Transient` with `Scoped` across the three lifetimes now fails to start (T7.3)
  - AC-24's four named clauses must be satisfied in terms: the `MapperLifetime.Scoped` break and its migration; C-18's compatibility note; the joint consequence for adopters (`{Scoped, Scoped, Transient}` is not a valid destination); and, for each of `IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync`, `IAmAHandlerFactorySync`, `IAmAHandlerFactoryAsync`, `IAmAHandlerFactory` and `IAmALifetime`, **what changed and how a hand-rolled implementation is migrated** — a source and binary breaking change on `netstandard2.0`, where no default interface member can absorb it
  - Both `release_notes.md` and `docs/guides/lifetimes-and-scoping.md` must state the first three of those clauses (AC-24 requires both files)
  - **Depends on**: T7.13
  - **References**: AC-24 (FR-20, FR-25.6, NFR-1(c)); ADR 0070 step 7a (the catalogue), ADR 0074 step 7, ADR 0076 step 4

- [ ] **DOC: T7.15 — the guidance page is self-sufficient, and the truth table's citations still hold**
  - No automated signal. Recorded on the **PR checklist**, one line per message and one line per `Then` clause
  - Verification, by a reviewer walking the merged `docs/guides/lifetimes-and-scoping.md`:
    - from **each of the seven messages** (T7.10's hosts) to the troubleshooting entry it names, and from there to the decision guide: each path yields a **concrete correction** of the kind the message calls for — a specific value for each of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` forming a conformant triple for the three lifetime-related messages; a specific corrective **registration** action for the duplicate-provider, unreadable-override, repeated-opt-in and defeated-opt-in messages, each naming which registration to remove, which value takes effect if it is left, and what is lost until it is fixed
    - in every case **without needing to consult Brighter's source, the ADRs, or the specification**
    - ⚠ **re-verify T5.5's truth table**: every row cites the AC that asserts it (AC-13, AC-14, AC-15, AC-17, AC-18, AC-19, AC-20, AC-21, AC-26, AC-29, AC-34, AC-39, AC-46, AC-47) or is marked as derived from a cited row — the forward references T5.5 left open are now closed. **Any row citing no AC is itself a finding: either the row is wrong or an AC is missing**
    - the checklist covers AC-24, AC-25, AC-36 and AC-43 as well as AC-44 — ⚠ **these are the only ACs in the document with no automated signal, and the ones most likely to be skipped**. AC-24's last clause takes **one line per item in the `release_notes.md` entry**, not one for the clause
  - **Depends on**: T7.14
  - **References**: AC-44 (NFR-10), AC-25 (verifier), AC-24 (verifier), AC-36, AC-43; ADR 0074 (the guidance-page section)

---

## Coverage Cross-Reference

### Functional requirements

| FR | Covered by |
| --- | --- |
| FR-1 | T1.5 (AC-1), T1.14 (design-owed) |
| FR-2 | T1.6 (AC-2) |
| FR-3 | T1.7 (AC-3) |
| FR-4 | T1.8 (AC-4) |
| FR-5 | T1.4 (structural, the `CleanUpQuietly` guard), T1.9 (AC-5), T1.10 (AC-6), T2.2 (design-owed), T2.6 (AC-51), T6.18 (AC-38) |
| FR-6 | T1.12 (AC-8), T2.2 (design-owed), T2.4 (AC-7), T2.6 (AC-51) |
| FR-7 | T2.3 (AC-9) |
| FR-8 | T2.5 (AC-10), T3.3 (the suppression carrier both clauses are built on), T5.2 (AC-13), T5.3, T6.13 (AC-11), T6.14 (AC-12), T6.15 (AC-39), T6.16 (AC-47) |
| FR-9 (a) | T3.3 (the carrier), T5.1, T5.2 (AC-13), T6.13 (AC-11), T6.14 (AC-12) |
| FR-9 (b) | T3.3 (the carrier), T5.3, T6.14 (AC-12), T6.15 (AC-39), T6.16 (AC-47) |
| FR-10 | T3.1, T4.3 (AC-46), T5.2 (AC-13), T6.8, T6.13 (AC-11) |
| FR-11 (a) | T6.22 (AC-14) |
| FR-11 (b) | T2.3 (AC-9) |
| FR-12 | T4.4 (AC-35), T4.8 (the post-probe window), T6.4 (AC-16), T6.18 (AC-38) |
| FR-13 | T1.10 (AC-6), T1.11 (design-owed, transform), T2.2 (design-owed), T2.7 (AC-33), T2.6 (AC-51), T6.7 (AC-19) |
| FR-14 | T3.4, T3.6, T6.3 (AC-15), T6.5 (AC-17), T6.9 (AC-45), T7.6 (AC-50) |
| FR-15 | T3.4, T6.10 (AC-18), T6.19 (AC-52 negative control), T6.22 (AC-14) |
| FR-16 (a) | T4.4, T6.5 (AC-17), T4.9 |
| FR-16 (b) | T6.6 (AC-34) |
| FR-16 (c) | T6.19 (AC-52) |
| FR-17 | T3.5, T3.6, T6.1 (the package itself), T6.3 (AC-15), T6.9 (AC-45), T6.10 (AC-18), T6.11 (AC-48), T7.6 (AC-50, the one exception), T7.8 (AC-49), T7.9 (AC-53), T7.10 (AC-43) |
| FR-18 | T6.7 (AC-19), T4.5 (AC-31) |
| FR-19 | T1.13 (AC-21), T5.4, T6.20 (AC-20), T6.21 (AC-55) |
| FR-20 | T7.4 (AC-41), T7.14 (AC-24), T7.13 |
| FR-21 | T6.12 (AC-26) |
| FR-22 (as a whole) | T7.10 (AC-43) — ⚠ AC-43 cites **bare `FR-22`** in its own parenthetical (`requirements.md:743`) because its seven hosts span FR-22.1–FR-22.4 together. A task quoting an AC's citation is quoting it verbatim; this row is what makes the bare form resolve, and the sub-clause rows below are unaffected |
| FR-22.1 | T7.0a (scaffolding), T7.0b (scaffolding), T7.1 (AC-27), T7.2 (AC-40) |
| FR-22.2 | T7.0a (scaffolding), T7.3 (AC-28), T7.4 (AC-41) |
| FR-22.3 | T7.0a (scaffolding), T7.5 (AC-42) |
| FR-22.4 | T3.5, T7.0a (scaffolding), T7.6 (AC-50) |
| FR-23 | T4.2a (scaffolding), T4.6 (AC-29), T4.7 (AC-54), T4.8 (design-owed) |
| FR-24.1 | T3.2, T4.2 (AC-30) |
| FR-24.2 | T4.2a (scaffolding), T4.5 (AC-31), T6.7 (AC-19) |
| FR-24.3 | T4.4 (registration model), T6.10 (AC-18), T7.7 (AC-32), T7.10 (AC-43) |
| FR-24.4 | T4.2a (scaffolding), T6.13 (AC-11) |
| FR-25 (as a whole) | T7.8 (AC-49), T7.10 (AC-43) — ⚠ both ACs cite **bare `FR-25`** (`requirements.md:799`, `:743`), because what they assert is the obligation that a message *names the page* (`docs/guides/lifetimes-and-scoping.md`), not any one of its eleven clauses. The clauses themselves are the rows below, and neither task discharges one |
| FR-25.1 | T5.5 |
| FR-25.2 | T5.5 |
| FR-25.3 | T5.5, T7.15 (citation re-verification) |
| FR-25.4 | T5.5 (and the XML docs in T1.1, T2.1) |
| FR-25.5 | T7.13 (AC-36) |
| FR-25.6 | T7.13, T7.14 (AC-24) |
| FR-25.7 | T7.13, T7.14 (AC-24) |
| FR-25.8 | T7.13 — ⚠ **no AC clause asserts this one in terms**; AC-44's walk is the only signal |
| FR-25.9 | T7.11 (AC-25), T7.15 (AC-44) |
| FR-25.10 | T7.12 (AC-25, AC-43, AC-44) |
| FR-25.11 | T7.13 (AC-25) |
| FR-26 | T4.4 (`ScopedArtefactCache`), T6.17 (AC-37) |
| FR-27.1 | T4.3 (AC-46), T5.2 (AC-13) |
| FR-27.2 | T4.3 (AC-46) |
| FR-27.3 | T6.16 (AC-47) |

### Non-functional requirements

| NFR | Covered by |
| --- | --- |
| NFR-1 | T1.2 (AC-22 clauses 1–3), T1.3, T2.1 (the (b) all-implementations-move clause), T7.14 (the (c) release-note clause) |
| NFR-2 | T1.2 (AC-22 clause 2); the re-run is owned as a **Done when** condition on T6.1 and T6.2, where the clause first becomes falsifiable |
| NFR-3 | T1.2 (AC-22 clause 2), T1.3, T2.1 |
| NFR-4 | T3.3, T4.5 (atomic latch), T4.9 (cache protocol), T6.14 (AC-12), T6.15 (AC-39) |
| NFR-5 | T1.5 (AC-1), T1.9 (AC-5), T2.8 (AC-23), T6.17 (AC-37) |
| NFR-6 | T2.8 (AC-23), T5.2 (AC-13), T6.17 (AC-37 clause 3) |
| NFR-7 | T4.4 (AC-35), T3.3 (public mutator rationale) |
| NFR-8 | T1.1, T2.1 (XML documentation), T5.5 (AC-25) |
| NFR-9 | T5.5 (AC-25 truth table), T7.15 (citations) |
| NFR-10 | T7.11, T7.12, T7.15 (AC-44), T7.10 (AC-43) |

### Acceptance criteria

| AC | Task |
| --- | --- |
| AC-1 | T1.5 |
| AC-2 | T1.6 |
| AC-3 | T1.7 |
| AC-4 | T1.8 |
| AC-5 | T1.9 |
| AC-6 | T1.10 |
| AC-7 | T2.4 |
| AC-8 | T1.12 |
| AC-9 | T2.3 |
| AC-10 | T2.5 |
| AC-11 | T6.13 |
| AC-12 | T6.14 |
| AC-13 | T5.2 |
| AC-14 | T6.22 (two test files, two projects; amended by T2.3's migration) |
| AC-15 | T6.3 |
| AC-16 | T6.4 |
| AC-17 | T6.5 |
| AC-18 | T6.10 |
| AC-19 | T6.7 |
| AC-20 | T6.20 |
| AC-21 | T1.13 |
| AC-22 | T1.2 |
| AC-23 | T2.8 |
| AC-24 | T7.14 (`release_notes.md`), T7.13 (the guidance-page half) — ⚠ AC-24's `Then` requires the `MapperLifetime.Scoped` break, C-18's note and the joint consequence in **both** files |
| AC-25 | T5.5 (clauses 1–4), T7.11 (decision guide), T7.12 (troubleshooting), T7.13 (FR-25.11 rule), T7.15 (verifier) — ⚠ **the one AC deliberately split across tasks, per the owner's documentation decision (c)** |
| AC-26 | T6.12 |
| AC-27 | T7.1 |
| AC-28 | T7.3 |
| AC-29 | T4.6 |
| AC-30 | T4.2 |
| AC-31 | T4.5 |
| AC-32 | T7.7 |
| AC-33 | T2.7 |
| AC-34 | T6.6 |
| AC-35 | T4.4 |
| AC-36 | T7.13 |
| AC-37 | T6.17 |
| AC-38 | T6.18 |
| AC-39 | T6.15 |
| AC-40 | T7.2 |
| AC-41 | T7.4 |
| AC-42 | T7.5 |
| AC-43 | T7.10 |
| AC-44 | T7.15 |
| AC-45 | T6.9 |
| AC-46 | T4.3 |
| AC-47 | T6.16 |
| AC-48 | T6.11 |
| AC-49 | T7.8 |
| AC-50 | T7.6 |
| AC-51 | T2.6 |
| AC-52 | T6.19 |
| AC-53 | T7.9 |
| AC-54 | T4.7 |
| AC-55 | T6.21 |

### ADR decisions

| ADR | Step | Covered by |
| --- | --- | --- |
| 0070 | 1 | T1.1 |
| 0070 | 2 | T1.3 |
| 0070 | 3 | T1.4 (structural lift), T1.5 (threading) |
| 0070 | 4 | T1.9 |
| 0070 | 4a | T1.10 |
| 0070 | 4b | T1.10 |
| 0070 | 5 | T1.5, T1.11 |
| 0070 | 6 | T1.5, T1.10, T1.12 |
| 0070 | 7 | No code — the per-lifetime table; its substance lands in T5.5's FR-25.1 clause |
| 0070 | 7a | T7.14 (the single thirteen-item release-note entry) |
| 0070 | 8 | T1.8 (asserts the six call sites need no change) |
| 0070 | 9 | T1.14 |
| 0070 | 9a | T1.5, T1.6, T1.7, T1.8, T1.11, T1.13, T2.8 (the verification table's seven rows) |
| 0070 | 10 | No code — what is left standing for siblings |
| 0071 | 1 | T2.1 |
| 0071 | 2 | T2.2 |
| 0071 | 3 | T2.3 |
| 0071 | 4 | T2.3 (including the 26-fact migration in the same commit) |
| 0071 | 5 | No code — the per-lifetime table; substance lands in T5.5 |
| 0071 | 6 | T2.2 (ordering, design-owed), T2.7 (AC-33), T2.6 (AC-51), T2.3 (migration and the AC-14 amendment) |
| 0072 | 1 | T3.1, T3.2 |
| 0072 | 1a | T4.1 |
| 0072 | 1b | T4.2 |
| 0072 | 2 | T4.3, T4.4, T4.5, T4.6, T4.7 |
| 0072 | 2a | T4.6, T4.7 |
| 0072 | 2b | No code — which providers reach rows 8 and 9; informs T4.6 and T6.21 |
| 0072 | 2c | No code — what borrowing does to Brighter's registrations; asserted indirectly by T6.19 |
| 0072 | 2d | T4.8 (the disposed-after-probe window), T4.7 (the root-handle residue) |
| 0072 | 3 | T4.3, T6.12 |
| 0072 | 3a | T4.9 |
| 0072 | 4 | T4.4, T4.8, T6.4, T6.18 |
| 0072 | 5 | **T4.2a** (`AmbientScopeDiagnostics`, declared and registered inert), T4.4 (`ScopedArtefactCache`, the `IAmAScopeProvider` registration model) |
| 0072 | 6 | No code — hand-off to siblings |
| 0073 | 1 | T6.1, T6.3 |
| 0073 | 2 | T6.3 |
| 0073 | 3 | T6.8 |
| 0073 | 4 | T6.22 |
| 0073 | 4a | T6.2 |
| 0073 | 4b | T6.22 (the two halves, two projects) |
| 0073 | 4c | T6.2 (`Microsoft.AspNetCore.Mvc.Testing` entry) |
| 0073 | 5 | T7.13 (FR-25.11's three gestures) |
| 0073 | 6 | No code — hand-off; contributes **no** release-note item |
| 0074 | 1 | Explicitly **no Tidy-First task in core**, per the ADR — the XML-comment amendment lands inside T7.5. ⚠ Two Tidy-First tasks *are* owed in the **DI package**, T7.0a and T7.0b; the ADR's claim is scoped to `Paramore.Brighter` |
| 0074 | 2 | **T7.0a** (`ContainerRegistrationSnapshot` and its three queries, landed inert) |
| 0074 | 3 | **T7.0a** (the five entity types, and the constructor selector as a **shell**, landed inert), T7.5 (the selector's **rule body**, driven by AC-42) |
| 0074 | 4 | T7.1, T7.3, T7.4, T7.5, T7.6, T7.7, T7.8, T7.9 |
| 0074 | 5 | T7.1 |
| 0074 | 5a | T7.5 (exclusion set, `includeAsync: true`, the comment amendment) |
| 0074 | 5b | **T7.0b** (both hosted services widened to `IEnumerable`, and all twelve affected test sites migrated — seven resolution plus five construction — ahead of T7.1's second registration), T7.2 (AC-40, that the error still surfaces through the consumer host), T7.5 (`ValidationMapperRegistry`) |
| 0074 | 6 | T7.1 |
| 0074 | 7 | T7.12 (troubleshooting), T7.14 (its two release-note items) |
| 0075 | 1 | T3.3 |
| 0075 | 2 | T5.1 |
| 0075 | 3 | T5.2 |
| 0075 | 4 | T5.3, T6.14, T6.15, T6.16 |
| 0075 | 4a | T5.4, T6.20, T6.21 |
| 0075 | 5 | T5.3 (the explicit restore and why it is unobservable) |
| 0075 | 5a | T5.3 (the asymmetry warning — risk mitigation) |
| 0075 | 6 | T5.2 (the factories' single `IsSuppressed` read) |
| 0075 | 7 | T5.5 (truth-table rows), T7.13 (FR-25.5) |
| 0076 | 1 | T3.4 |
| 0076 | 2 | T3.5 |
| 0076 | 3 | T3.6, T6.9 |
| 0076 | 4 | T7.13 (FR-25.11), T7.14 (the `IBrighterOptions` release-note item) |
| 0076 | 5 | No code — hand-off to ADR 0074; the declined value-comparison rule is asserted by T6.11's *no finding* clause |

### Gaps

Nothing is left without a task. Four coverage notes are recorded honestly rather than papered over:

1. **FR-25.8 has no acceptance-criterion clause of its own.** AC-25's `Then` clauses do not name the captive-dependency hazard, so T7.13 writes it and AC-44's reviewer walk (T7.15) is its only signal. This is the requirements' shape, not a hole in the task list.
2. **ADR 0072 ladder row 8 (an ambient of a foreign role type) has no criterion.** The ADR says so in terms and records the extension of FR-23's diagnostic in its *Negative*. T4.6 implements it and asserts the diagnostic; nothing pins the row itself.
3. **Three ADR 0075 brackets are implemented in Phase 5 against non-ASP.NET instruments and pinned end-to-end in Phase 6.** T5.3 and T5.4 carry no AC; AC-12, AC-39, AC-47 (T6.14–T6.16) and AC-20, AC-55 (T6.20, T6.21) discharge them. Each task says so.
4. **C-2's five frozen types have no mechanical guard.** No AC asks for one, and none is added. `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder` and `ConsumerFactory` must be untouched by every task above; ADR 0075 step 4a's `Performer` bracket is the deliberate near-miss and `Performer` is not one of the five.

### Scope creep

None. Every task traces to an AC, to a named FR/NFR clause, or to an ADR step. Re-derived: **eleven** `TEST + IMPLEMENT` tasks carry no acceptance criterion **of their own** — the remaining 51 carry the 55 ACs between them. Each of the eleven is named as **design-owed** or **scaffolding** by an ADR, and each carries its FR/NFR trace. ⚠ Five of them (`T1.14`, `T3.6`, `T4.9`, `T5.3`, `T5.4`) *mention* an AC on their `References` line as a **cross-reference** — "AC-24 (general clause)", "AC-45 completes it at T6.9", "AC-20, AC-55 discharge it at Phase 6" — which is not the same as carrying one, and a grep for `AC-` will miscount them:

- T1.11 — FR-13's transform-side disposal clause (ADR 0070 step 9a names it *design-owed*)
- T1.14 — FR-1 on its last surviving path (ADR 0070 step 9, release-noted)
- T2.2 — FR-5/FR-6/FR-13 ordering (ADR 0071 step 6 names it *design-owed* and says no AC can be written over Brighter's own types)
- T3.2, T3.3 — contract tests over two new **public** core types whose ADR contract tables state explicit error conditions (FR-24.1; FR-8/FR-9/NFR-4)
- T3.6, T4.8, T4.9, T5.3, T5.4, T6.8 — FR-17's four-path clause, FR-23's post-probe window, #4260's `Scoped` half, FR-9(b), FR-19/C-14, and FR-10's provider obligation respectively, each an explicit ADR step with no AC that can compile in its phase

## Concerns (not acted on)

None that would change the plan. Two observations, recorded because they are the places a task list can be misread rather than because the design is wrong:

- **Phase 6 carries nineteen acceptance criteria**, which is more than any other phase, because the eight host-requiring criteria plus every criterion whose `Given` says "opted-in ASP.NET" cannot compile earlier. The owner's rule — place an AC at the later phase where it spans — produces this concentration; it is not a sequencing error.
- **AC-25 is the single acceptance criterion split across tasks**, because owner decision (c) splits the guidance page by clause group while decision (a) gives each AC one task. Decision (c) is the more specific rule for documentation and I have followed it, with the split recorded in the AC table.

