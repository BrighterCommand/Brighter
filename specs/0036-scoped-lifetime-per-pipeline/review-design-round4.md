# Review: design — 0036-scoped-lifetime-per-pipeline (round 4)

**Date**: 2026-08-04
**Threshold**: 60
**Verdict**: NEEDS WORK

63 findings at or above threshold 60. Address these before approving.

Eight reviewers, one per ADR plus one whose only remit is set-level properties, all on opus and all
blind to `PROMPT.md` and to rounds 1–3's findings files. Every reviewer verified its citations
against source rather than reading them, re-derived every count it reported, rendered every mermaid
block with `mmdc`, and ran the escaped-entity grep. Five of the eight compiled and ran .NET probes.

**Per reviewer** — 0070: 11 findings (9 at or above 60) · 0071: 12 (10) · 0072: 8 (8) · 0073: 10 (7) ·
0074: 9 (7) · 0075: 8 (6) · 0076: 8 (7) · set-level: 11 (9).

**Mechanically clean across the set, verified independently by more than one reviewer**: 15/15
mermaid diagrams render under `mermaid-cli@11`; escaped entities 0 in all seven; `docs/adr/index.md`
byte-identical to a fresh regeneration; all seven sibling maps byte-identical after normalisation
with the correct row bolded; the unifying sentence verbatim in all seven with no variants;
frontmatter uniform, all `Proposed`, all `author: Ian Cooper`; supersession sentence in all seven;
no authoring-conversation or ephemeral-state references anywhere; the four "chain" hits are the
ordinary English verb and the *rejected* name `IAmAChainScope`.

---

## Ranked index — every finding at or above threshold, highest first

| Score | Reviewer | # | Finding |
|---|---|---|---|
| 88 | 0071 | 1 | The ADR contradicts itself about whether it discharges a requirement |
| 85 | 0072 | 1 | FR-23's usability probe is unreachable through the ASP.NET provider, so AC-29 cannot pass as designed |
| 85 | set | 1 | 0071 asserts three times that it discharges no requirement, and its own Scope says it discharges one |
| 84 | 0071 | 2 | `ServiceProviderPipelineScope` is specified two different ways across 0070 and 0071 |
| 82 | 0070 | 1 | The new third drain step is unreachable on exactly the failure paths it exists for |
| 82 | 0075 | 1 | The async path's explicit restore is not load-bearing — `PublishAsync` is itself an `ExecutionContext` boundary |
| 80 | 0072 | 2 | `ServiceProviderLifetimeScope.cs:152` cited twice for `_scopedInstances`; `:152` is `GetOrCreateSingleton` |
| 80 | 0076 | 1 | The "TryAddSingleton spelled out" guard is not `TryAddSingleton`, and silently un-registers `IBrighterOptions` |
| 74 | 0071 | 3 | The composed `AggregateException` destroys the handler's own exception; "no failure masks another" is false |
| 74 | 0076 | 2 | The `BrighterHandlerBuilder` single-funnel alternative is never considered, and a sibling uses it |
| 72 | 0070 | 2 | "Three tests encode the old contract and must change" is falsified by this ADR's own steps 6 and 9 |
| 72 | 0071 | 4 | The enumeration of methods threading `IAmALifetime` is wrong: four named, six exist |
| 72 | 0072 | 3 | The protocol pseudo-code tests `affinity == AlwaysNew`, contradicting the ADR's own positive-test contract |
| 72 | 0072 | 4 | "The same cleanup the general clause runs" is false for two of the six builder `catch` blocks |
| 72 | 0073 | 1 | The public/internal decision rests on a misreading of AC-18 |
| 72 | set | 2 | The ledger says nothing implements `IBrighterOptions`; one class does, and 0076 says both in one sentence |
| 70 | 0070 | 3 | `ServiceProviderPipelineScope` "configured `Scoped`" contradicted two paragraphs later, and by 0071 |
| 70 | 0071 | 5 | The AC-9 amendment the ADR says it owes is owed to AC-14, which it never cites |
| 70 | 0073 | 2 | AC-14 does not need an ASP.NET host, and step 4a sends it to the wrong project |
| 70 | 0074 | 1 | The exclusion set is walked over two different input sets, and one is wrong about the handler half |
| 70 | 0075 | 2 | Alternative 5 rejects the `Task.WhenAll` bracket for a harm that cannot occur, citing an AC that cannot detect it |
| 70 | 0076 | 3 | `ArgumentNullException.ThrowIfNull` does not exist on `netstandard2.0`, one of this package's four TFMs |
| 70 | set | 3 | 0072 is the only ADR with no ledger entry and no break statement, while changing shipped behaviour |
| 68 | 0073 | 3 | FR-15's normative clause is assigned to a sibling that does not claim it |
| 68 | 0075 | 3 | The public *mutator* is scope beyond any requirement, and Alternative 3's rejection contradicts the contract table |
| 68 | set | 4 | 0072 gives two different line numbers for `_scopedInstances`; `:152` is wrong |
| 68 | set | 5 | Eight requirements are discharged by no ADR, including the one the spec was raised for (FR-16) |
| 66 | 0070 | 4 | The ACs that falsify this ADR's headline claims are never cited — AC-1 not at all |
| 66 | 0071 | 6 | FR-7's "not re-implemented differently" clause is the one thing this ADR does, and is never quoted |
| 66 | 0071 | 7 | 0070 says AC-24 is satisfied; 0071 says AC-24 needs widening |
| 66 | 0072 | 5 | `AmbientScopeSourceException` is new public core surface with no contract and no NFR-7 obligation stated |
| 66 | 0073 | 4 | The bold Decision sentence, and the frontmatter summary the index publishes, are false as written |
| 66 | 0075 | 4 | Two documentation obligations 0074 assigns to 0075 are unowned in its Scope (NFR-9 appears 0 times) |
| 66 | 0076 | 4 | 0076 owns `ScopeAffinityOverride` but states neither registration obligation the mechanism depends on |
| 66 | set | 6 | FR-15's three-way split is declared by 0073 alone; neither ADR it names picks up its share |
| 65 | 0072 | 6 | #4260 is claimed closed, but the fix as described leaves `GetOrCreateSingleton`'s faulted `Lazy` untouched |
| 65 | 0073 | 5 | `HttpRequestScope.Services` non-nullness is checked at construction but read from a mutable property |
| 65 | 0074 | 2 | FR-17's "position stands in for the value" fallback contradicts the same paragraph's same-affinity exclusion |
| 65 | 0074 | 3 | FR-22.4 is missing from the snapshot-staleness Negative bullet |
| 64 | 0070 | 5 | Step 3's code sample cannot compile in this ADR's commit, and duplicates an edit 0072 claims |
| 64 | 0071 | 8 | Under `Transient` a handler pipeline holds a non-null `PipelineScope` while FR-27.1/AC-46 say it takes none |
| 64 | 0074 | 4 | `SpecificationEvaluator` is new **public** core surface and the ADR never says so |
| 62 | 0070 | 6 | `ServiceCollectionExtensions.cs:807` cited for a claim it does not support |
| 62 | 0070 | 7 | On the failed-build path, which of the two new `Error` messages AC-6 asserts is undetermined |
| 62 | 0070 | 8 | "Nothing in this ADR changes when 0072 arrives" is contradicted by 0072 |
| 62 | 0071 | 9 | The specified XML doc on `IAmALifetime.PipelineScope` says "owned", which 0071 and 0072 both contradict |
| 62 | 0072 | 7 | Ladder row 2 labels a `Transient` handler pipeline OWNED, where FR-27.1 says it takes no pipeline scope |
| 62 | 0073 | 6 | The test-project count is 37, not 38, and "`Brighter.slnx` has no ASP.NET entry" is false |
| 62 | 0074 | 5 | "Six DI-package implementation types on the public surface" — the ADR's own snippet names two |
| 62 | 0074 | 6 | The ADR contradicts itself about what AC-42's `[UsePolicyAsync]` clause pins |
| 62 | 0075 | 5 | The "69 test call sites" count is wrong — 21 use the describe-only constructor |
| 62 | 0076 | 5 | The ServiceActivator DI extension class is `ServiceActivatorServiceCollectionExtensions` |
| 62 | set | 7 | The suppression read is specified circularly: 0075 says 0072 specifies it; 0072 never names the flag |
| 62 | set | 8 | 0071 says "Nothing else in `PipelineBuilder` changes", allows for 0072's edit and omits 0075's |
| 61 | 0073 | 7 | The mechanism diagram omits the null check the ADR twice calls load-bearing |
| 60 | 0070 | 9 | NFR-1(b), NFR-1(c) and AC-24 are cited as covering all six broken interfaces; they cover four |
| 60 | 0071 | 10 | One Consequences bullet carries five `file:line` citations |
| 60 | 0072 | 8 | NFR-8 is cited for a claim it does not make |
| 60 | 0074 | 7 | `### The documentation this set owes` parks set-level bookkeeping inside a one-decision ADR |
| 60 | 0075 | 6 | `Technology Choices` carries six `file:line` citations and one Consequences bullet carries four |
| 60 | 0076 | 6 | The contract table promises an exception message the code sample cannot produce |
| 60 | 0076 | 7 | 0073 says this mechanism has "two callers"; 0076 names one, and there is no second |
| 60 | set | 9 | FR-13 is asserted to be "split three ways" with nothing left over, but its principal clause is unassigned |

**Convergences — reached by reviewers blind to each other, which is why they weigh more than their individual scores:**

- **0071's self-contradiction over whether it discharges a requirement** — 0071 #1 (88) and set #1 (85), the round's two highest scores.
- **`ServiceProviderPipelineScope`'s lifetime specified two ways** — found from *both* ends: 0071 #2 (84) and 0070 #3 (70).
- **`ServiceProviderLifetimeScope.cs:152` cited for `_scopedInstances`** — 0072 #2 (80) and set #4 (68).
- **FR-15's normative clause owned by nobody** — 0073 #3 (68) and set #6 (66).
- **The 38-test-project count** — 0073 #6 (62) and set #10 (52).
- **0073's References omitting four ACs its body relies on** — 0073 #9 (52) and set #11 (48).

**Empirically settled by probe, not by argument** — the class of finding that argument alone cannot
produce: 0075 #1 and #2 (an `async` method is an `ExecutionContext` boundary, so `PublishAsync`'s
restores are defence in depth and AC-12's final clause cannot detect what Alternative 5 rejects);
0076 #1 (`TryAdd` matches on `ServiceType` *and* `ServiceKey`); 0071 #3 (`using var` + a throwing
`Dispose` destroys the in-flight exception); 0072 #1 (the ASP.NET accessor is nulled at end of
request, so the deferred-work case is *no ambient*, not a *stale* one); 0073's `FrameworkReference`
transitivity and `DefaultHttpContext.RequestServices == null` confirmations.

---

## Findings

## Reviewer: ADR 0070 — Per-pipeline DI scope for mapper and transform factories

### 1. The new third drain step is unreachable on exactly the failure paths it exists for (Score: 82)

Step 5 and the `Where each type is touched` row both specify the DI-scope release as a **third step appended after** today's two, while insisting the first two are unchanged:

> "`TransformPipelineDrain.Drain`/`DrainAsync` … gain a third step, ordered **after** the transform scope disposal and the mapper release"
> "a third drain step after today's `disposeScope`/`releaseMapper` (`Drain` `:46`, `DrainAsync` `:85`). **Steps 1 and 2 keep today's hold-and-compose**"
> "Steps 1 and 2 keep today's behaviour **exactly**: each failure is held so the next step still runs, and whatever was held surfaces to the caller composed as an `AggregateException`… Step 3 does not join it"

Today's `Drain` **exits by throwing** in every failure case, so nothing written after it runs. If the mapper release throws, control leaves at line 70 or 71; if only the scope disposal threw, control leaves at line 76. A step 3 written "after today's two" therefore never executes whenever step 1 or step 2 fails — and the pipeline's release-once guard has *already* been claimed (`TransformPipeline.cs:65`, set before the drain at `:69`), so neither a later `Dispose()` nor the finalizer retries it. The DI scope this ADR exists to reclaim leaks on precisely the teardown-failure path.

To make step 3 unconditional the existing hold-and-compose must be restructured (hold the composed exception, run step 3, then throw) — which is not "keeping today's behaviour exactly". Two developers will implement this differently, and one of the two implementations violates FR-6 ("A pipeline scope Brighter owns must be released when the pipeline completes, whether it completes normally **or by exception**") and NFR-5.

**Evidence**: Opened `src/Paramore.Brighter/TransformPipelineDrain.cs:46-77`. Line 65 `releaseMapper();` is inside a `try` whose `catch (Exception releaseError)` at `:67-72` ends `if (scopeError is null) throw; throw new AggregateException(scopeError, releaseError);` — both unconditional throws. Line 76 `if (scopeError is not null) ExceptionDispatchInfo.Capture(scopeError).Throw();` is the last statement of the method. `DrainAsync` (`:85-118`) has the identical shape. Opened `src/Paramore.Brighter/TransformPipeline.cs:62-72`: `Interlocked.Exchange(ref _released, 1)` at `:65` precedes the `Drain` call at `:69`, and `~TransformPipeline()` (`:50-60`) returns immediately on that guard.

**Recommendation**: Specify the new drain shape explicitly rather than as "a third step after": hold steps 1 and 2's failures, run step 3 in a `finally` (or before the composition is thrown), log and swallow step 3's own failure, then throw the composed `AggregateException`. Say in the ADR that the existing composition is *deferred*, not preserved verbatim, and add a Negative bullet for it.

---

### 2. "Three tests encode the old contract and must change" is falsified by this ADR's own steps 6 and 9 (Score: 72)

The Negative section states:

> "**Three tests encode the old contract and must change** … `When_releasing_a_scoped_mapper_it_should_stay_usable_for_later_resolutions.cs` … The first asserts precisely the cross-pipeline reuse that FR-1 removes. The second and third are about a factory-wide scope that a `Scoped` factory no longer keeps for the pipeline path"

But step 6 says `Create(Type, IAmAScope?)` "resolves through the handle when it is a `ServiceProviderPipelineScope` and the lifetime is `Scoped`; **otherwise it takes exactly today's path**", and step 9 says "A third party calling a factory's `Create(type)` directly, with the defaulted `null` scope, gets **today's behaviour**." All three named tests construct a `ServiceProviderMapperFactory` directly and call `Create(type)` with **no scope** — they never go near a pipeline. Under the ADR as written they pass unchanged.

The larger point the reader is misled about: this ADR does **not** remove the factory-wide `Scoped` cache. It bypasses it for the builder path. That is defensible and is what step 9 chooses, but the Negative bullet asserts the opposite, and a reviewer reading only *Consequences* will believe Defect 1 is closed on every path.

**Evidence**: Read all three files. `tests/Paramore.Brighter.Extensions.Tests/When_releasing_a_scoped_mapper_it_should_stay_usable_for_later_resolutions.cs:26-41` — `var factory = new ServiceProviderMapperFactory(provider); var first = factory.Create(typeof(DisposableMapper)); factory.Release(first!); var second = factory.Create(typeof(DisposableMapper)); … Assert.Same(first!.Instance, second!.Instance);` — no scope argument, no builder, no pipeline. `When_two_threads_first_resolve_a_scoped_mapper_concurrently_it_should_not_leak_a_scope.cs:24,31` — `void Resolve() => factory.Create(typeof(NonDisposableMapper));`. `When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it.cs:24` — same shape. I also grepped for every test file that constructs `ServiceProviderMapperFactory`/`ServiceProviderTransformerFactory` *and* mentions `Scoped`: there are **four**, not three (the fourth is `When_a_scope_is_first_published_while_the_owner_is_disposing_it_should_not_leak.cs`), and under step 9 none of the four changes.

**Recommendation**: Either delete the bullet and state plainly under *Consequences* that the factory-level `Scoped` cache survives for out-of-bracket `Create`, so no existing factory-level test changes; or change step 6/9 so a `Scoped` factory stops caching at factory level entirely and then the bullet is true. The two cannot both stand.

---

### 3. `ServiceProviderPipelineScope` is specified as "configured `Scoped`" in one paragraph and as not hard-wired in the next — and ADR 0071 says this ADR's description is wrong (Score: 70)

*Technology Choices* opens:

> "`ServiceProviderPipelineScope` owns exactly one `ServiceProviderLifetimeScope`, **configured `Scoped`**."

Two paragraphs later, the same section says the opposite:

> "it is constructed with **that lifetime and the isolate-transient flag** rather than hard-wired to `Scoped`. ADR 0071 needs exactly that"

And the `Where the pieces live` diagram node reads "owns one ServiceProviderLifetimeScope, which owns the IServiceScope" with no lifetime at all. This ADR owns the type (its touched table marks it **new**), so the specification of the type is 0070's to get right. ADR 0071 has to correct it in prose.

**Evidence**: ADR 0070 lines 273 and 280. ADR 0071 line 272: *"0070 describes the type as owning one `ServiceProviderLifetimeScope` **configured `Scoped`** … That is too narrow for the handler family … **The type's lifetime is therefore its creator's, not a constant**, and that is a change to 0070's description rather than a restatement of it."* ADR 0071's touched table (line 260) likewise records `ServiceProviderPipelineScope` as "**widened**".

**Recommendation**: State the type once, in 0070, as owning one `ServiceProviderLifetimeScope` constructed with **its creator's** configured lifetime and isolate-transient flag, and note that for a transform pipeline that lifetime is always `Scoped`. Then 0071 restates rather than amends, and its "widened" row can go.

---

### 4. The acceptance criteria that falsify this ADR's headline claims are never cited — AC-1 not at all (Score: 66)

The ADR claims to discharge FR-1 … FR-7 and to close Defect 1 and Defect 1b. The acceptance criteria that test exactly that are **AC-1** (a `Scoped` mapper is not reused across messages), **AC-2** (transform), **AC-3** (one `Scoped` dependency across mapper and transforms), **AC-4** (producer side), **AC-21** (C-3's boundary) and **AC-23** (bounded growth over 10,000 messages).

AC-1 appears nowhere in the ADR — not in the body, not even in *References*. AC-2, AC-3, AC-4, AC-21 and AC-23 appear **only** in the *References* list and are never connected to anything the ADR specifies. There is also no verification step in *Implementation Approach*: the only test named in the whole ADR is one line in the Risks table ("a test asserts each of the four returns a scope under `Scoped` and `null` otherwise") plus the two `FactoryLifetimeTests` cited as an unchanged handler regression guard.

Contrast ADR 0071, which has an explicit **"6. Regression guards"** step naming AC-7, AC-9 and AC-33, saying which existing tests move, which are inadequate, and what must be added.

**Evidence**: Grepped the ADR body (lines 1–452, excluding *References*) for each id: `AC-1` → 0 occurrences; `AC-2` → 0; `AC-3` → 0; `AC-4` → 0; `AC-21` → 0; `AC-23` → 0. (`AC-5` → 5, `AC-6` → 9, `AC-8` → 3, `AC-24` → 4, `AC-30` → 2, `AC-33` → 3.) `requirements.md:417` defines AC-1 (FR-1, NFR-5); `:422` AC-2; `:427` AC-3; `:434` AC-4; `:646` AC-21; `:661` AC-23.

**Recommendation**: Add a numbered verification step to *Implementation Approach* that names AC-1 … AC-4, AC-21 and AC-23 and says which of them the pipeline-scope mechanism makes observable and where. Add AC-1 to *References*, or remove the five uncited ids so the list stops overstating coverage.

---

### 5. Step 3's code sample cannot compile as part of this ADR's commit, and duplicates an edit ADR 0072 claims (Score: 64)

The *Where this ADR sits* table says the seven are "meant to be read in order; this is the first", and *Scope* says this ADR "does not decide … the *ambient* concept … Each is deferred". Yet step 3 — this ADR's implementation step, in commit order — presents:

```csharp
catch (AmbientScopeSourceException e)                       // ADR 0072, FR-24.1, AC-30
```

`AmbientScopeSourceException` is a type ADR 0072 introduces (its touched table: "`Paramore.Brighter` | `AmbientScopeSourceException` | **new**"), and ADR 0072 step **1b** claims the very edit: *"The six builder `catch` blocks learn one clause … `TransformPipelineBuilder.cs:202`… ahead of each existing wrapping `catch`."* The ADR's `CreatePipelineScope()` contract table likewise writes 0072's ambient-fault behaviour into a member whose contract 0070 owns.

An implementor taking 0070's steps in order writes code that does not compile; an implementor reading 0072's step 1b writes the same clause twice.

**Evidence**: ADR 0070 lines 296–322 (step 3) and 233 (contract table). ADR 0072 lines 313, 315 and 342, 346. Verified in source that no `AmbientScopeSourceException` exists today (`grep -rn "AmbientScopeSourceException" src/` returns nothing) and that `TransformPipelineBuilder.cs:116-125` / `:157-166` carry a single unfiltered `catch (Exception e)` in both builders.

**Recommendation**: Show step 3 with only the `catch (Exception e)` clause that this ADR's commit contains, and add a sentence saying the discriminating clause arrives with ADR 0072 (naming step 1b), so ownership of the edit is unambiguous. Keep the contract table's error column, but mark the ambient half as 0072's widening rather than as part of this ADR's contract.

---

### 6. `ServiceCollectionExtensions.cs:807` is cited for a claim it does not support, and the sentence it sits in is wrong about where the mapper factories are built (Score: 62)

The forces bullet reads:

> "**The two factories are constructed at different sites, and nothing today connects them.** The mapper factories are built **inside the `MessageMapperRegistry`**; the transformer factories are built from separate public helpers, each taking only an `IServiceProvider` (`ServiceCollectionExtensions.cs:807`)."

Both halves fail. The mapper factories are **not** built inside `MessageMapperRegistry`; they are built in the same static helper as everything else and handed to the registry's constructor. And `:807` is `new ServiceProviderMapperFactory(provider)` — a **mapper** factory, not one of the transformer helpers the clause is attached to. The transformer helpers are `TransformFactory` at `:943-945` and `TransformFactoryAsync` at `:955-957`, which step 6 cites correctly.

**Evidence**: Opened `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:802-811` — `public static MessageMapperRegistry MessageMapperRegistry(IServiceProvider provider) { … var messageMapperRegistry = new MessageMapperRegistry(new ServiceProviderMapperFactory(provider) /* :807 */, new ServiceProviderMapperFactoryAsync(provider) /* :808 */, …); }`. And `:943-946` / `:955-958` for the two transformer helpers. `src/Paramore.Brighter/MessageMapperRegistry.cs:43-44,68-69` shows the factories arriving as constructor arguments, not being constructed there.

**Recommendation**: "The mapper factories are built by one static helper and handed to the `MessageMapperRegistry`; the transformer factories are built by two separate public helpers, each taking only an `IServiceProvider` (`ServiceCollectionExtensions.cs:945`, `:957`)."

---

### 7. On the failed-build path, which of the two new `Error` messages AC-6 asserts is undetermined (Score: 62)

Step 4a assigns `FailedToDisposePipelineScopeAfterFailedBuild` to "`CleanUpAfterFailedBuild` when releasing the owned scope throws on the failed-build path". But step 4 states that path has **two branches**:

> "When a pipeline object was constructed it already owns the scope and `pipeline.Dispose()` releases it; when it was not, the cleanup releases the scope directly"

In the first branch the scope release runs inside `pipeline.Dispose()` → the drain, so under step 5 the failure is caught *there*, logged as `FailedToDisposePipelineScope` (the **completed-pipeline** message), and swallowed — it never reaches `CleanUpAfterFailedBuild` and the "after failed build" message is never written. Only the second branch produces `FailedToDisposePipelineScopeAfterFailedBuild`. Which branch AC-6's test exercises decides which message name it must assert, and the ADR — which goes to some length to justify two distinguishable messages — never says.

This is reachable in practice: `pipeline` is assigned at `TransformPipelineBuilder.cs:104` and the build can still throw at `:106`, `:108` or `:111`, so the "pipeline was constructed" branch is live, not hypothetical.

**Evidence**: Opened `src/Paramore.Brighter/TransformPipelineBuilder.cs:98-125` (assignment at `:104`, further work at `:106-111`, `catch` at `:116`) and `:231-245` (`CleanUpAfterFailedBuild`: `if (pipeline is not null) { pipeline.Dispose(); return; }` at `:237-241`). `requirements.md:447-450` — AC-6 asserts only "a capturing `ILoggerProvider` … records **the disposal failure** at `LogLevel.Error`", without naming a message.

**Recommendation**: State in step 4a which message each branch emits, and say which AC-6 pins — or make `CleanUpAfterFailedBuild` emit the "after failed build" message on both branches by not delegating scope release to `pipeline.Dispose()`.

---

### 8. "Nothing in this ADR changes when [ADR 0072] arrives" is contradicted by 0072 (Score: 62)

*Technology Choices* asserts:

> "Adoption therefore needs the cache to belong to the DI scope rather than to the handle, and **ADR 0072 supplies that**, as a container-`Scoped` service. **Nothing in this ADR changes when it does**: the owned case still resolves one cache per pipeline"

ADR 0072 changes two things this ADR specifies. Its touched table: "`ServiceProviderLifetimeScope` | … the `Scoped` path **resolves its artefact cache from the scope in play rather than owning `_scopedInstances`**". And its `ScopedArtefactCache` section: "**`GetOrAdd` evicts a faulted entry instead of publishing it**, **on both paths**" — a behavioural change to the owned path's `Lazy` publish protocol, which is the exact mechanism 0070 names as supplying artefact identity ("the `ServiceProviderLifetimeScope`'s per-type `_scopedInstances` cache (`:163-178`) supplies **artefact** identity … which is C-17 preserved"). The *outcome* (one cache per pipeline in the owned case) survives; the mechanism and its fault behaviour do not.

**Evidence**: ADR 0070 line 282. ADR 0072 lines 270, 286, 319. Verified the mechanism in source: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:49` declares `private readonly ConcurrentDictionary<Type, Lazy<object?>> _scopedInstances = new();` and `:163-178` is the `GetOrCreateScoped<T>` that reads it — the field 0072 removes from the owned path.

**Recommendation**: Change to "the *outcome* of this ADR is unchanged when it does — the owned case still gets one cache per pipeline — though ADR 0072 relocates the cache off `ServiceProviderLifetimeScope` and changes its fault-caching behaviour on both paths."

---

### 9. NFR-1(b), NFR-1(c) and AC-24 are cited as covering all six broken interfaces; they cover four (Score: 60)

The forces bullet and the Negative bullet both hang the whole six-interface break on NFR-1:

> "**NFR-1(b)** requires every one of them to move in the same change"
> "That is NFR-1(c)'s framing and AC-24's obligation"

NFR-1's withdrawn signature freeze names a **different six** — the four mapper/transformer factories plus `IAmAHandlerFactorySync` and `IAmAHandlerFactoryAsync`. Neither `IAmAMessageMapperRegistry` nor `IAmAMessageMapperRegistryAsync` is in it, and NFR-1(b)'s enumeration of implementations that must move ("the four container-backed factories, the six core factories … and every test double") does not reach the registries, `MessageMapperRegistry` or `ControlBusMessageMapperFactory` either. AC-24's clause is written over "the **six factory interfaces** whose signature changed" — the registries are not factory interfaces.

ADR 0071 states this as an amendment it owes, and names the two mapper registries as two of the three non-factories. ADR 0070 — which is the ADR that actually breaks them — records the fact only inside step 7a's *ADR 0071* bullet, and still cites NFR-1(b)/(c)/AC-24 as if they covered its own six.

**Evidence**: `requirements.md:352` (NFR-1's withdrawal paragraph) enumerates *"`IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync`, `IAmAHandlerFactorySync` and `IAmAHandlerFactoryAsync`"*; clause (b) *"the four container-backed factories, the six core factories (`SimpleMessageMapperFactory[Async]`, `SimpleMessageTransformerFactory[Async]`, `EmptyMessageTransformerFactory[Async]`) and every test double"*; clause (c) *"any application that implements one of **the six** by hand"*. `requirements.md:677` — AC-24: *"for each of the **six factory interfaces** whose signature changed"*. ADR 0071 line 341 states the amendment.

**Recommendation**: Move the amendment into 0070's own forces/Negative text, in 0070's voice: NFR-1's withdrawal covers four of this ADR's six; NFR-1(b)/(c) and AC-24 are read as extending to the two mapper registries on the same reasoning, and that extension is what step 7a's single release-note entry records.

---

### 10. `release_notes.md` cross-reference points at step 7, not step 7a (Score: 45)

> "That is NFR-1(c)'s framing and AC-24's obligation, and **step 7** records where it is written down."

Step 7 is "Behaviour by configured lifetime" (the three-row lifetime table). The release-note content is step **7a**.

**Evidence**: ADR 0070 line 407 against lines 358 (step 7) and 370 (step 7a).

**Recommendation**: s/step 7/step 7a/.

---

### 11. `TransformPipelineBuilder.Log (:409)` cites a member, not the partial class (Score: 35)

The touched table row is headed "`TransformPipelineBuilder.Log`, `TransformPipelineBuilderAsync.Log` (`:409`, `:318`)". Those lines are the `FailedToCleanUpAfterFailedBuild` declarations; the `Log` partial classes begin earlier. Harmless, but an implementor looking for the class declaration lands on a member.

**Evidence**: `src/Paramore.Brighter/TransformPipelineBuilder.cs:408-409` and `TransformPipelineBuilderAsync.cs:317-318`.

**Recommendation**: Cite the class declaration line, or say "beside `FailedToCleanUpAfterFailedBuild` (`:409`, `:318`)".

---

### Verification log

- **Citations checked: 78.** Every `file:line` in the ADR was opened. **One failed**: `ServiceCollectionExtensions.cs:807` cited for "the transformer factories are built from separate public helpers" (finding 6) — `:807` is `new ServiceProviderMapperFactory(provider)`. **Two imprecise**: `TransformPipelineBuilder.Log (:409)`/`Async (:318)` name a member not the class (finding 11); `ServiceProviderLifetimeScope :384-388` for the Proactor sync-context guidance actually begins at `:383`. All others held exactly, including every line in the two-factory-families table, `ServiceProviderHandlerFactory.cs:102-107/:127-131/:133-137`, `ServiceProviderLifetimeScope.cs:42/:118-123/:126/:132-142/:136/:139-140/:163-178/:259-261/:320/:367/:406/:422-436/:449/:462`, both builders at `:50/:51/:52/:93/:95-97/:116-125/:122-123/:124/:134/:157-166/:163-164/:165/:172/:174/:180/:193/:215-223/:231/:244/:255/:330/:332`, `TransformPipeline.cs:16/:21/:24/:65`, `TransformPipelineAsync.cs:22`, `TransformPipelineDrain.cs:38/:46/:85`, `TransformerFactory.cs:32/:42`, `TransformerFactoryAsync.cs:30/:40`, `WrapPipeline.cs:53`, `UnwrapPipeline.cs:45`, `WrapPipelineAsync.cs:57`, `UnwrapPipelineAsync.cs:47`, `MessageMapperRegistry.cs:41`, `IAmAMessageMapperRegistry.cs:34` (quotation exact), `ControlBusMessageMapperFactory.cs:31`, `ClaimCheckTransformer.cs:62`, `Paramore.Brighter.csproj:24`, `BrighterOptions.cs:20/:37/:52/:69`, `OutboxProducerMediator.cs:569/:587/:1248/:1258/:1269-1279/:1312/:1321/:1448`, `Reactor.cs:531/:637`, `Proactor.cs:239/:241/:538/:651`, `ServiceCollectionExtensions.cs:808/:945/:957`, `FactoryLifetimeTests.cs:36/:154`.
- **Counts re-derived** (multi-line-aware regex over class/record/struct declarations with wrapping base lists, across `src/`, `tests/`, `samples/`):
  - "12 classes in `src/`" → **12** ✓ (exact set matches the ADR's enumeration: 4 container-backed, 6 core, `ControlBusMessageMapperFactory`, `MessageMapperRegistry`).
  - "70 test doubles" → **70** ✓. "38 test files in all" → **38** ✓. "64 factory doubles across 37 test files" → **64 / 37** ✓. "six registry doubles in three files, one of which contains no factory double" → **6 / 3**, and `When_a_pipeline_finalizer_release_throws_it_should_not_escape.cs` is indeed registry-only ✓.
  - "82 implementations" (Risks) → 12 + 70 = **82** ✓.
  - "six interfaces" → **6** ✓. "six pipeline constructors" → 4 concrete + 2 abstract bases = **6** ✓, all six types public.
  - "four construction sites" → **4** ✓ (`:807`, `:808`, `:945`, `:957`).
  - "the five messages that exist all log at `Warning`" → **5**, all `LogLevel.Warning` ✓ (`TransformPipelineBuilder.cs:408`, `TransformPipelineBuilderAsync.cs:317`, `OutboxProducerMediator.cs:1448`, `Reactor.cs:637`, `Proactor.cs:651`).
  - "six call sites, all unchanged" → **6** ✓.
  - "the one Brighter-shipped transform with constructor dependencies" → **1 of 3** transformer classes has a constructor at all ✓.
  - "Three tests encode the old contract" → **4** files drive a `ServiceProvider*Factory` under `Scoped`, and by the ADR's own steps 6 and 9 **none** of them changes — finding 2.
  - `Paramore.Brighter.ServiceActivator.csproj` → exactly **1** `ProjectReference`, **0** `PackageReference` ✓ (NFR-3 claim holds).
- **Mermaid blocks rendered: 2/2**, `mmdc@11` exit 0 for both, `.svg` produced. Both also rendered to PNG at `-w 1600 -b white` and visually inspected: the `sequenceDiagram` (12 numbered messages, three `Note` bands) is legible and the ACQUIRE/SHARE/RELEASE structure reads off it cleanly; the `flowchart TB` (2 subgraphs, 5 nodes, 4 edges) is legible, with both boundary-crossing solid edges running DI-package → core as the prose claims. No `;` in `sequenceDiagram` text, no bare `<`/`>` in any label.
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;'` → **0** ✓.
- **Tone/terminology greps**: no reference to conversation participants, `PROMPT.md`, spec phase, review rounds or commit hashes — **0 hits**. "chain" appears **once**, at line 449, as the *rejected* name `IAmAChainScope` and the explicit statement that "chain" is not a term of art — legitimate per the brief.
- **Structure**: heading wording, order and nesting match all six siblings exactly (`## Status` → `## Context` → `### Where this ADR sits` → problem → `### The forces` → `## Decision` → `### The mechanism, end to end` → `### Where the pieces live` → `### Key Components` → `#### The roles, and what each is responsible for` → contracts → `#### Where each type is touched` → `### Technology Choices` → `### Implementation Approach` → `## Consequences`/`### Positive`/`### Negative`/`### Risks and Mitigations` → `## Alternatives Considered` → `## References`). The unifying sentence "**the per-pipeline object carries the DI scope**" is stated identically in all seven; the sibling map table is identical in all seven with the correct row bolded and marked *(this one)* in each. `Where each type is touched` closes with an explicit "Unchanged, and named so the omission is not read as an oversight" paragraph. No structural finding.
- **Probes compiled/run: none.** I did not need a C# probe: the two framework claims (a defaulted parameter's value is baked into the call site, so adding one to an interface method is source-compatible for callers and binary-breaking; MS DI child scopes are root-parented, C-1) are both standard and the second is carried from `requirements.md` rather than invented here. I state this plainly rather than reporting them as empirically verified. `npx` had network and ran successfully, so the mermaid check is real.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 3 |
| 50-69 | 6 |
| 0-49 | 2 |

Total: 11 · At or above 60: 9
## Reviewer: ADR 0071 — Handler pipelines take their DI scope as a pipeline scope handle

### 1. The ADR contradicts itself about whether it discharges a requirement (Score: 88)

The *Scope* paragraph claims a requirement clause; two other places flatly deny it. Which is right determines whether AC-33 has an owner for the handler family — and ADR 0072 has already routed it here.

**Evidence**: Line 30 (*Scope*): "It discharges **FR-13's disposal-failure clause for the handler family** — a `PipelineScope` disposal that throws on a pipeline whose handler completed normally is logged at `LogLevel.Error` and swallowed … (step 2, AC-33)." The frontmatter `summary` says the same ("… so a pipeline whose handler completed normally is never failed by its own teardown (FR-13, AC-33)"), and *Implementation Approach* step 2 plus its third required test are written as AC-33's regression guard.

Against that, line 38 (*Where this ADR sits*): "This is the second, and **the only one that discharges no requirement of its own**." And line 346 (*Negative*): "**This ADR discharges no requirement of its own, and it is still not free.**"

Verified from the sibling side: `docs/adr/0072-ambient-scope-adoption-seam.md:31` — "FR-13's ownership clause is who owns … that requirement's disposal-failure clause is ADR 0070's for transform pipelines and **ADR 0071's for handler pipelines, where AC-33 guards it**." Requirements `specs/0036-scoped-lifetime-per-pipeline/requirements.md:230` (FR-13 "Disposal failure on a pipeline that succeeded", "Discharged by AC-33") and `:532` (AC-33) confirm the clause exists and is a real requirement.

**Recommendation**: Delete "the only one that discharges no requirement of its own" from the sibling map and rewrite the *Negative* bullet as "discharges one clause of one requirement and is otherwise structural". Keep the *Scope* claim, which the siblings and the requirements both support.

---

### 2. `ServiceProviderPipelineScope` is specified two different ways across 0070 and 0071, and 0070 was never amended (Score: 84)

0071 changes the configured lifetime of a type ADR 0070 introduces, states that it is doing so, and leaves 0070's own text asserting the opposite. An implementor working from 0070's step list builds a `Scoped`-only type; one working from 0071 builds a creator-configured one.

**Evidence**: 0071 *Technology Choices*: "0070 describes the type as owning one `ServiceProviderLifetimeScope` **configured `Scoped`** … **The type's lifetime is therefore its creator's, not a constant**, and that is a change to 0070's description rather than a restatement of it."

I opened `docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md:273`: "`ServiceProviderPipelineScope` owns exactly one `ServiceProviderLifetimeScope`, **configured `Scoped`**." Unchanged. 0070's *Where each type is touched* (`:263`) lists the type only as "**new**", with no note of the widening; 0070's step 7a (`:370-375`) enumerates the cross-ADR breaks but not this one. Both ADRs are `status: Proposed`, so amending 0070 is available. The type does not exist in source yet — `grep -rn "ServiceProviderPipelineScope" src tests --include="*.cs"` (excluding `bin`/`obj`) returns nothing — so the only specification is these two conflicting paragraphs.

**Recommendation**: Amend 0070 in the same commit so its `:273` sentence and its touched-table row read "configured with its creator's lifetime", and have 0071 cite the amended 0070 rather than declaring a unilateral widening.

---

### 3. The composed `AggregateException` destroys the handler's own exception on the failure path, and "no failure masks another" is provably false (Score: 74)

*Implementation Approach* step 2 makes `HandlerLifetimeScope.Dispose()` throw an `AggregateException` whenever any handler `Release` failed, unconditionally. `CommandProcessor` disposes the builder with `using var`, so on a pipeline whose handler *also* threw, the compiler-generated `finally` replaces the in-flight exception. The ADR never addresses this, and FR-6/AC-7 are written over exactly that scenario.

**Evidence**: ADR text: "if a *handler release* failed, throw those failures composed as an `AggregateException`, **so no failure masks another**." And *Negative*: "Code that catches the specific type a handler's `Dispose` throws must catch `AggregateException` instead."

Source shape verified at `src/Paramore.Brighter/CommandProcessor.cs:394-413`: `using var builder = new PipelineBuilder<T>(...)` at `:394`, then `try { … await handlerChain.First().HandleAsync(…) } catch (Exception e) { …; throw; } finally { _tracer?.EndSpan(span); }`. The `using var` lowering wraps all of it.

Probe (compiled and run, `net9.0`, scratchpad `probe0071`): a `Builder` whose `Dispose()` throws `AggregateException`, a body that throws `InvalidOperationException("HANDLER THREW")`, wrapped exactly as above. Output:

```
caller observed: AggregateException: release failures composed (handler Release threw)
```

The handler's `InvalidOperationException` is gone entirely — not inner, not suppressed, lost. Requirements `:452` AC-7 (FR-6): "**Then** the caller observes `InvalidOperationException`, and the dependency's `Dispose` was called exactly once." The ADR names AC-7 as its regression guard (line 30) but specifies a `Dispose()` that can falsify it. The ADR also does not say whether a *single* release failure is wrapped or rethrown bare — two developers would implement that differently too.

**Recommendation**: State the rule for a teardown failure on an already-failing pipeline — either latch and log rather than throw when the pipeline is unwinding, or attach as `AggregateException`'s inner and accept the type change explicitly — and add the AC-7 case (handler throws **and** a `Release` throws) to step 6's required tests.

---

### 4. The enumeration of the methods that thread `IAmALifetime` is wrong: four are named, six exist, and the two async decorator methods are missing everywhere (Score: 72)

The count is load-bearing — Alternative 2's rejection is an arithmetic argument, and the "what is unchanged" list is supposed to name what is deliberately not touched. Two real methods are absent from every list, and the ADR uses "four", "six" and "eight" for overlapping enumerations.

**Evidence**: ADR *forces*: "through **four methods** that thread it onwards and resolve nothing themselves". *Technology Choices*: "`BuildPipeline` (`PipelineBuilder.cs:272`), `BuildAsyncPipeline` (`:316`), `PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`) … A scope parameter would travel beside it through **all eight**". *Alternatives* 2: same four "as well as the two direct `Create` calls, so … through **all six**". *Positive*: "so the **six methods** that thread `IAmALifetime` are untouched". *Risks*: "the **four** threading sites are listed in *Technology Choices*". *Where each type is touched* → unchanged: "`BuildPipeline`, `BuildAsyncPipeline`, `PushOntoPipeline` and `AppendToPipeline`, which keep threading `IAmALifetime` and nothing beside it".

Re-derived with `grep -n "IAmALifetime instanceScope\|IAmALifetime lifetime" src/Paramore.Brighter/PipelineBuilder.cs src/Paramore.Brighter/HandlerFactory.cs src/Paramore.Brighter/AsyncHandlerFactory.cs`. `PipelineBuilder` has **six** such methods, not four:

- `BuildPipeline` `:272` ✓ cited
- `BuildAsyncPipeline` `:316` ✓ cited
- `AppendToPipeline` `:430` ✓ cited
- **`AppendToAsyncPipeline` `:451` — never named**
- `PushOntoPipeline` `:499` ✓ cited
- **`PushOntoAsyncPipeline` `:525` — never named**

Both omitted methods are live: `BuildAsyncPipeline` calls `PushOntoAsyncPipeline` at `:339` and `AppendToAsyncPipeline` at `:352`, and both call `_asyncHandlerFactory.CreateAsyncRequestHandler(…, instanceScope)` (`:461`, `:535`). Adding `HandlerFactory.CreateRequestHandler` (`HandlerFactory.cs:44`) and `AsyncHandlerFactory.CreateAsyncRequestHandler` (`AsyncHandlerFactory.cs:42`) gives **eight** methods carrying `IAmALifetime`, so Alternative 2's "all six" is understated and the *forces* "four" is wrong. (The four *resolution* sites — `PipelineBuilder.cs:191`, `:236`, `HandlerFactory.cs:47`, `AsyncHandlerFactory.cs:46` — are correct; I opened each.)

**Recommendation**: Replace "four methods" with six everywhere, add `AppendToAsyncPipeline` (`:451`) and `PushOntoAsyncPipeline` (`:525`) to *Technology Choices*, to Alternative 2 and to the unchanged list, and reconcile "all six"/"all eight" to one number.

---

### 5. The AC-9 amendment the ADR says it owes is owed to AC-14, which the ADR never cites — and AC-9's own scenario does exercise the handle path (Score: 70)

The ADR builds a *Negative* consequence and a required extra test on the premise that FR-7's guarantee ends up "guarded only where it no longer applies". AC-9's own Given/When/Then is an end-to-end `Send`, which necessarily goes through `PipelineBuilder` and therefore through the handle path.

**Evidence**: ADR step 6: "The requirements designate exactly this pair as the regression guards for **AC-9** … they will, and that is the problem … **otherwise FR-7's guarantee is guarded only where it no longer applies**." *Negative*: "`FactoryLifetimeTests`' two tests — designated in the requirements as the regression guards for **AC-9** … That designation now has to attach to the duplicated handle-path pair as well, which is an amendment to how AC-9 is discharged."

I opened `specs/0036-scoped-lifetime-per-pipeline/requirements.md:464-467` — AC-9 in full: "**Given** not opted in, all three … `Scoped` … and a handler with a `Scoped` `IDisposable` dependency, **When** `Send` returns, **Then** that dependency has been disposed; **and** a second `Send` resolves a different instance." No mention of `FactoryLifetimeTests`. That test is driven through `Send`, i.e. through `PipelineBuilder.Build` → `GetSyncInstanceScope()` → the handle. It does not move to the fallback.

The designation the ADR is quoting lives at `requirements.md:510`, inside **AC-14 (FR-11, FR-15)**: "*Explicitly NOT excluded:* `FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`FactoryLifetimeTests.cs:36-55`) and its async twin (`:154`) … They must keep passing unchanged, and serve as regression guards for AC-9." AC-14 appears nowhere in ADR 0071, including its *References* list.

Test citations themselves check out: `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs:36` and `:154` are the two named methods, and `TestLifetimeScope` is at `:311` with exactly three members.

**Recommendation**: Say the amendment is to **AC-14**'s "explicitly NOT excluded" clause, add AC-14 to *References*, and drop or soften "FR-7's guarantee is guarded only where it no longer applies" — AC-9's own end-to-end scenario still guards it on the handle path. The duplicated pair remains worth having; the justification needs correcting.

---

### 6. FR-7's "not re-implemented differently" clause is the one thing this ADR does, and it is never quoted or argued (Score: 66)

FR-7 is the requirement 0071 says it exists to protect. Its final clause, read literally, forbids replacing the mechanism. The ADR reinterprets FR-7 as a constraint on *scoping* without ever putting the clause on the page.

**Evidence**: `requirements.md:193` — "**FR-7 — Handler pipeline scoping is preserved.** One `Send`/`SendAsync` takes one handler pipeline scope, released when the pipeline completes. This is today's behaviour and must be regression-guarded, **not re-implemented differently**."

ADR *forces*: "FR-7 requires today's handler behaviour to be preserved and regression-guarded: one DI scope per handler pipeline, resolved from at the same points, disposed at the same point. That is a constraint on *scoping*, and it is not the same as a promise that nothing observable changes." That is a reading of the first sentence; the fourth clause is not quoted anywhere in the ADR (`grep -c "re-implemented"` over the ADR: 0). The change replaces `ConcurrentDictionary<IAmALifetime, ServiceProviderLifetimeScope>` (`ServiceProviderHandlerFactory.cs:40`, `:127-131`, `:133-137` — all verified) with an `IAmAScope` handle, which is a re-implementation by any ordinary reading.

**Recommendation**: Quote FR-7's clause verbatim in the *forces* bullet and state the reading explicitly — that "not re-implemented differently" governs the observable scoping, not the internal carrier — or flag it as a third amendment owed, alongside the AC-24 and AC-14 ones.

---

### 7. 0070 says AC-24 is satisfied; 0071 says AC-24 needs widening (Score: 66)

The two ADRs reach opposite conclusions about the same obligation for the same eight interfaces, in text that otherwise agrees word for word.

**Evidence**: ADR 0071 *Negative*: "**Neither break is covered by the authorities usually cited for them, and that is an amendment this ADR owes rather than a discharge it claims.** … So AC-24's enumeration **needs widening** to eight interfaces across this ADR and ADR 0070 … This is stated as an amendment … the requirement as written does not reach the change."

`docs/adr/0070-…:375` (step 7a): "**Eight interfaces break across the two ADRs, not six**, and three of the eight are not factories — the two mapper registries and `IAmALifetime`. AC-24's own wording enumerates a different six … **this single entry is a superset of it and satisfies it**."

A third sibling takes yet another position: `docs/adr/0075-…` — "**AC-24 does not reach it** … Step 7a carries it because the ledger is written as a superset of AC-24, **not because AC-24 asks for it**."

The underlying facts check out: `requirements.md:352` (NFR-1's withdrawal) names exactly six — `IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync`, `IAmAHandlerFactorySync`, `IAmAHandlerFactoryAsync` — so neither `IAmAHandlerFactory` nor `IAmALifetime` is among them, as 0071 says. `requirements.md:671` is AC-24. The disagreement is only about whether a requirements amendment is owed.

**Recommendation**: Pick one. Either the superset release note discharges AC-24 (0070's and 0075's position, in which case 0071 drops "an amendment this ADR owes"), or AC-24 needs widening (0071's position, in which case 0070 step 7a and 0075 must say so too).

---

### 8. Under `Transient`, a handler pipeline holds a non-null `PipelineScope` while FR-27.1 and AC-46 say such a pipeline "takes no pipeline scope" (Score: 64)

The ADR is aware of this and answers it by asserting a terminology carve-out rather than proposing the requirements amendment it is scrupulous about proposing elsewhere. The property is literally named `PipelineScope` and the Decision sentence says the handle *is* the pipeline's DI scope.

**Evidence**: ADR *Decision*: "**A handler pipeline's DI scope is an `IAmAScope`** …"; contract table: `CreatePipelineScope()` returns "an `IAmAScope` the caller must release for `Transient` and `Scoped` alike". *Forces*: "**A `Transient` handle is not what FR-27.1 calls a pipeline scope** … a `Transient` handler pipeline makes no ambient ask and takes no adoption decision (AC-46's first branch)."

`requirements.md:250` (FR-27.1): "A pipeline none of whose participating factories is `Scoped` **takes no pipeline scope** and asks nothing." `requirements.md:783` (AC-46): "**Then** the recorder shows **zero** adoption decisions **and no pipeline scope taken** — `Transient`'s per-resolution scope does not pass through the seam."

The *no-ask* half is corroborated by the sibling — `docs/adr/0072-…:92`, ladder row 2: "`Scoped` does not participate in this pipeline — handler family, `HandlerLifetime` is `Transient` | **OWNED**, and **no ask is made at all** (FR-27.1)". So 0072 also has a `Transient` handler pipeline holding an OWNED scope while row 2's own condition column says `Scoped` does not participate. Both ADRs rely on the same unwritten carve-out. An implementer writing AC-46's assertion as "`lifetime.PipelineScope is null` for the `{Transient,Transient,Transient}` host" fails; one writing it as "the recorder saw zero calls" passes.

**Recommendation**: Either propose the FR-27.1/AC-46 wording amendment explicitly (as the ADR does for AC-24 and AC-14), or state in the contract table that AC-46's "no pipeline scope taken" is asserted over the ambient recorder only and never over `PipelineScope`'s nullness.

---

### 9. The specified XML doc on `IAmALifetime.PipelineScope` says the handle is "owned", which 0071's own factory doc and 0072 both contradict (Score: 62)

The ADR writes out two XML doc comments; they disagree with each other about ownership, and one of them is wrong the moment ADR 0072 lands.

**Evidence**: 0071, `IAmAHandlerFactory` member: "The caller must always release the returned handle; **releasing it may or may not dispose an underlying scope**, and the handle alone knows which." Two sections later, `IAmALifetime.PipelineScope`: "The DI scope this handler pipeline resolves from, or null when it has none. **Owned by this lifetime scope** and released when it is."

`docs/adr/0072-…:324`: "`CreatePipelineScope()`'s **contract** is widened: the handle it returns may now name a **borrowed** ambient, so the member promises that the caller must always *release* rather than that it *owns*, and only the handle knows whether releasing disposes anything (FR-12)." `requirements.md:216`/FR-12 — "Brighter never disposes a borrowed scope." Since 0071's *Consequences* explicitly plans for "**ADR 0072 builds adoption once.** A borrowed ambient becomes what `CreatePipelineScope()` returns, for handler pipelines and transform pipelines alike", the `PipelineScope` doc will be false for adopted pipelines the day 0072 ships.

**Recommendation**: Change the `PipelineScope` XML doc to "released when this lifetime scope is released; whether releasing disposes anything is the handle's own business" — matching the factory member's wording and 0072's widened contract.

---

### 10. One Consequences bullet carries five `file:line` citations (Score: 60)

`.agent_instructions/documentation.md` § *ADR readability*: "**Concentrate the citations.** `file:line` references are load-bearing for the implementor and pure noise inside an argument. **At most one** per forces or Consequences bullet."

**Evidence**: *Negative* → "**The handler pipeline releases its scope synchronously, and the transform pipeline does not.**" carries `CommandProcessor.cs:394`, `IAmALifetime.cs:34`, `IAmAPipelineBuilder.cs:36`, `IAmAnAsyncPipelineBuilder.cs:37` in one sentence, and its follow-on paragraph adds `ServiceProviderLifetimeScope.DisposeScope` `:422-436` — five in one bullet. (All five are correct: I opened each. `CommandProcessor.cs:394` is `using var builder = new PipelineBuilder<T>(…)`; `IAmALifetime.cs:34` is `public interface IAmALifetime : IDisposable`; `IAmAPipelineBuilder.cs:36` and `IAmAnAsyncPipelineBuilder.cs:37` are both `internal interface … : IDisposable`; `ServiceProviderLifetimeScope.cs:422-436` is the `SynchronizationContext` suppress/restore, with the LOAD-BEARING INVARIANT comment at `:416`.) The *forces* bullets and the rest of *Consequences* obey the rule; this one bullet is the outlier.

**Recommendation**: Keep `CommandProcessor.cs:394` in the bullet and move the three interface citations and `:422-436` into *Implementation Approach* or the touched table.

---

### 11. `ControlBusHandlerFactorySync` is cited as `(:6)`, but the file is `ControlBusHandlerFactory.cs` (Score: 40)

Every other bare `:NN` in the touched table resolves under the type-name-equals-file-name convention. This one does not, so a reader following it looks for a file that does not exist.

**Evidence**: ADR touched table: "`Paramore.Brighter.ServiceActivator` | `ControlBusHandlerFactorySync` (`:6`)". Actual location: `src/Paramore.Brighter.ServiceActivator/Ports/ControlBusHandlerFactory.cs:6` — `internal sealed class ControlBusHandlerFactorySync : IAmAHandlerFactorySync`. Line number correct, filename implied wrongly. (The accompanying NFR-3 claim is sound: `src/Paramore.Brighter.ServiceActivator/Paramore.Brighter.ServiceActivator.csproj:10` has exactly one `ProjectReference` to `Paramore.Brighter` and no `PackageReference`.)

**Recommendation**: Write `ControlBusHandlerFactory.cs:6`.

---

### 12. "Only the fourth changes identity" is not what the two diagrams show (Score: 30)

**Evidence**: ADR: "The lifelines are in the same order, so only the fourth changes identity — the dictionary becomes the handle." Comparing the fences, the **third** lifeline also changes label: `participant Factory as ServiceProviderHandlerFactory` in the *today* diagram becomes `participant Factory as the handler factory` in the mechanism diagram. Both render (exit 0), so this is prose only.

**Recommendation**: Say "only the fourth changes *role*", or keep the third lifeline's label identical across both diagrams.

---

### Verification log

- **Citations checked: 41.** Verified against source and confirmed: `PipelineBuilder.cs` `:47`, `:179-205`, `:190`, `:191`, `:192-193`, `:202-204`, `:235`, `:236`, `:248-250`, `:269-270`, `:272`, `:316`, `:430`, `:499`, `:567`, `:578`; `ServiceProviderHandlerFactory.cs` `:34`, `:40`, `:94-99`, `:102-107`, `:120-125`, `:127-131`, `:129`, `:133-137`; `HandlerLifetimeScope.cs` `:33`, `:74-93`, `:95`; `IAmAHandlerFactory.cs:7` (bare `public interface IAmAHandlerFactory;`); `IAmAHandlerFactorySync.cs` `:32-34` (quote exact), `:36`, `:44`; `IAmAHandlerFactoryAsync.cs:36`; `IAmALifetime.cs:34`; `IAmAPipelineBuilder.cs:36`; `IAmAnAsyncPipelineBuilder.cs:37`; `HandlerFactory.cs:47`; `AsyncHandlerFactory.cs:46`; `SimpleHandlerFactorySync.cs:33` (+ `Release` really calls `disposable?.Dispose()`); `SimpleHandlerFactoryAsync.cs:33`; `SimpleHandlerFactory.cs:11` (public, implements both twins); `BrighterOptions.cs:37`; `CommandProcessor.cs:394`; `ServiceProviderLifetimeScope.cs:422-436` (+ LOAD-BEARING INVARIANT at `:416`); `FactoryLifetimeTests.cs` `:36`, `:154`, `:311`; `tests/…/MessageSerialisation/When_a_transform_release_throws_the_scope_still_releases_the_rest.cs` (exists). All six referenced ADR slugs exist with the stated statuses.
  **Failures**: `ControlBusHandlerFactorySync (:6)` — line correct, file is `ControlBusHandlerFactory.cs` (finding 11). The four-method threading enumeration omits two real methods at `:451` and `:525` (finding 4). `requirements.md:831` cites `PipelineBuilder.cs:528`/`:539` for the `HandlerLifetimeScope` construction sites where the ADR correctly uses `:567`/`:578` — the requirement is stale, not the ADR.
- **Counts re-derived** (multi-line-aware Perl scan over all `.cs` outside `bin`/`obj`, including `samples/`):
  - `IAmAHandlerFactory` family implementations — claimed **21** (5 `src/`, 16 test doubles); got **21** (5 `src/`: `ServiceProviderHandlerFactory`, `ControlBusHandlerFactorySync`, `SimpleHandlerFactory`, `SimpleHandlerFactorySync`, `SimpleHandlerFactoryAsync`; 16 test doubles). ✔
  - `IAmALifetime` implementations — claimed **7** (one `src/` internal + 6 test doubles); got **7** (`HandlerLifetimeScope` + six `TestLifetimeScope` classes, all in `Paramore.Brighter.Extensions.Tests`). ✔
  - The bare-marker implementation — `sealed class DummyHandlerFactory : IAmAHandlerFactory;` at `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/When_There_Is_No_Sync_Or_Async_Handler_Factories.cs:56`. ✔
  - `HandlerLifetimeScope.Log`'s "four existing `Debug` members" — got **4** (`:97`, `:100`, `:103`, `:106`). ✔ "The three existing constructors" — got **3** (`:42`, `:46`, `:50`). ✔
  - Methods threading `IAmALifetime` — claimed **4** / "all six" / "all eight"; got **6** in `PipelineBuilder` (`:272`, `:316`, `:430`, `:451`, `:499`, `:525`) and **8** counting `HandlerFactory.cs:44` and `AsyncHandlerFactory.cs:42`. ✘ (finding 4)
  - Interfaces breaking across 0070 + 0071 — claimed **8**, three not factories; consistent with 0070 step 7a's own enumeration. ✔ (the disagreement is about AC-24's discharge, finding 7)
- **Mermaid blocks rendered: 3/3** — `mmdc@11`, exit 0 on all three, SVGs produced (30 425 / 31 198 / 21 314 bytes). Flowchart and mechanism sequence diagram additionally rendered to PNG at `-w 1600 -b white` and inspected. The flowchart is readable, has no decision points, shows exactly two solid boundary-crossing edges running DI-package → core and exactly one dotted call edge inside core — all three claims in the "Reading the edges" paragraph hold.
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;' docs/adr/0071-…md` → **0**. ✔
- **Probes compiled/run**: one, `net9.0` console app under `scratchpad/probe0071`, reproducing `CommandProcessor.SendAsync`'s `using var` + `try/catch/finally` shape with a `Dispose()` that throws `AggregateException` over a body that throws `InvalidOperationException`. Result: `caller observed: AggregateException` — the body's exception is destroyed, not chained. This disproves the ADR's "so no failure masks another" (finding 3). I also confirmed by reading `ServiceProviderLifetimeScope.cs:100-200` that the wrapped `IServiceScope` is created lazily (`EnsureRootScopePublished`), so eagerly constructing the handle in `GetSyncInstanceScope()` does not create a container scope earlier than today — the ADR's "cost per `Create`: none" claim survives.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 5 |
| 50-69 | 5 |
| 0-49 | 2 |

Total: 12 · At or above 60: 10
## Reviewer: ADR 0072 — Adopting an ambient DI scope

### 1. FR-23's usability probe is unreachable through the ASP.NET provider, so AC-29 — which the ADR names as its guard — cannot pass as designed (Score: 85)

Ladder rows 8 and 9, the `AmbientScopeProbe`, and the *ambient offered but unusable* latch all exist to serve FR-23, and the Risks table names AC-29 as their guard: "A stale `HttpContext.RequestServices` surfaces `ObjectDisposedException` from Brighter's own resolution | The usability probe runs before any pipeline instance is resolved from the ambient … AC-29". But AC-29's scenario — deferred work that outlives the response — does not produce a *stale ambient*. It produces **no ambient**, because `HttpContextAccessor` nulls its `AsyncLocal` holder at end of request, so the deferred flow sees `HttpContext == null`. ADR 0073's contract for `HttpContextScopeProvider.GetAmbient` then returns `null` — which lands on **ladder row 7, *no ambient offered* (FR-24.2)**, the one condition AC-29 explicitly forbids ("no entry naming either of FR-24's other two conditions").

**Evidence**: I compiled and ran a probe (`Microsoft.NET.Sdk.Web`, net9.0) that reproduces the framework mechanism exactly — set `IHttpContextAccessor.HttpContext = ctx` with `ctx.RequestServices = scope.ServiceProvider`, start deferred work from that flow via `Task.Run`, then dispose the scope and set `accessor.HttpContext = null` as `HostingApplication.DisposeContext` does. Output: `deferred: accessor.HttpContext is NULL`. The deferred flow never reaches `RequestServices` at all, so the probe never runs and `ObjectDisposedException` is never the discriminator. (A second probe confirms the probe *mechanism* is sound where an ambient does arrive: `scope.ServiceProvider.GetService(typeof(IServiceScopeFactory))` on a disposed `ServiceProviderEngineScope` throws `ObjectDisposedException`, and `IServiceScopeFactory` resolves with no descriptor of its own — `descriptor present in collection? False`. Both ADR claims hold; it is the *reachability* that does not.) The only path by which the built-in provider can hand over a stale-but-non-null `RequestServices` is a call made while an `HttpContext` is still current and `RequestServices` non-null but its scope already disposed — a narrow middleware/`OnCompleted` case that is not what AC-29 describes and that the ADR never identifies.

**Recommendation**: State explicitly which provider shapes can produce FR-23's condition, and say plainly that the ASP.NET provider's ordinary post-request case is FR-24.2 (row 7), not FR-23 (rows 8/9). Either AC-29 needs a scenario that keeps an `HttpContext` current, or FR-23's guard has to be written over a non-ASP.NET provider (AC-35's `AsyncLocal` one can hold a disposed scope trivially). Raise it with the requirements owner rather than leaving the Risks row asserting a mitigation that scenario cannot exercise.

---

### 2. `ServiceProviderLifetimeScope.cs:152` is cited twice as the `_scopedInstances` field; line 152 is `GetOrCreateSingleton` — and the ADR cites the correct line elsewhere (Score: 80)

Two citations point an implementor at the **Singleton** path, which is the one path the ADR insists must not move (`Singleton` "sits outside both, resolving from the root provider"). Both are in the normative places — the change table and the implementation step.

**Evidence**: ADR *Where each type is touched*: "the `Scoped` path resolves its artefact cache from the scope in play rather than owning `_scopedInstances` (`:152`)". ADR step 3a: "`ServiceProviderLifetimeScope.cs:152`'s private `_scopedInstances` field becomes a resolution of this service". I opened `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs`:
- `:49` — `private readonly ConcurrentDictionary<Type, Lazy<object?>> _scopedInstances = new();`
- `:152` — `private T? GetOrCreateSingleton<T>(Type objectType) where T : class`
- `:163-178` — `GetOrCreateScoped`, the `Lazy` publish protocol the ADR correctly cites elsewhere
- `:185` — `EnsureRootScopePublished()`, correctly cited

The ADR itself cites `:49` correctly twice — "A cache that is a private field of the handle (`ServiceProviderLifetimeScope.cs:49`)" and the Negative bullet "`_scopedInstances` (`:49`) stops being a private field". So the ADR contradicts itself on the same fact, and the wrong version is the one in the implementor's section.

**Recommendation**: Replace both `:152` with `:49` for the field, and cite `:163-178` where the *scoped resolution path* is meant. If the intent of step 3a is that `GetOrCreateSingleton`'s `_singletonInstances` also changes, say so separately — see finding 6.

---

### 3. The protocol pseudo-code tests `affinity == AlwaysNew`, contradicting the ADR's own contract that every `ScopeAffinity` reader tests for `JoinAmbient` positively (Score: 72)

The ADR makes the positive test a **contract** and ADR 0076 places it on 0072 as an explicit obligation. The pseudo-code — which the ADR says is "the decision ladder … written out as the code runs it" — does the opposite, and so does the ladder table's own row wording.

**Evidence**: ADR: "**Positive testing for `JoinAmbient` is a contract, not an implementation detail.** … Every reader of a `ScopeAffinity` in this design — the policy here, **and the affinity guard on the provider's answer** — tests for `JoinAmbient` positively rather than testing for `AlwaysNew` and treating everything else as adoption." The affinity guard on the provider's answer is step 6, which reads:

```
  6. if affinity == AlwaysNew:
        if ambient is not null -> diagnostics.WarnOnce(IgnoredForAlwaysNewAsk, providerType)
        -> OWNED
```

An out-of-enum value falls *through* step 6 and proceeds to borrow — precisely the fail-open the contract forbids. Ladder rows 5–7 partition the same way ("the ask carried `AlwaysNew`" / "the ask carried `JoinAmbient`"), leaving a third value unhandled. Cross-checked against `docs/adr/0076-scope-affinity-option-and-write-through.md:204`: "That is an **obligation this ADR places on ADR 0072** … every reader of a `ScopeAffinity` tests for `JoinAmbient` positively … ADR 0072 states the same rule on `ScopeAffinityPolicy`'s contract" — and 0076's References line repeats "the positive `JoinAmbient` test that makes an out-of-range value fail safe". 0072 states the rule and then violates it in the only place it writes the guard out.

**Recommendation**: Rewrite step 6 as `if (affinity != ScopeAffinity.JoinAmbient) { … OWNED }` (or `if (affinity is JoinAmbient) { … } else { OWNED }`) and reword ladder rows 5–7 so the `AlwaysNew` rows read "the ask did not carry `JoinAmbient`".

---

### 4. "The same cleanup the general clause runs" is false for two of the six builder `catch` blocks — `PipelineBuilder`'s catches run no cleanup at all, and AC-30's no-leak clause rests on `Dispose()`, not on the catch (Score: 72)

The instruction in step 1b is vacuous for the handler family, and the *Technology Choices* rationale for rejecting the hoist alternative attributes to `PipelineBuilder`'s `catch` a cleanup it does not have — on the very path AC-30 is written over.

**Evidence**: ADR step 1b: "add a clause for `AmbientScopeSourceException` that runs **the same cleanup the general clause runs**". ADR *Technology Choices*: "on the handler path the ask is *per subscriber*, inside a loop inside the `try`, so hoisting it means restructuring the loop and **forfeiting the cleanup that runs when a later subscriber fails** — and AC-30's second clause, 'no pipeline scope is leaked', depends on exactly that cleanup."

`src/Paramore.Brighter/PipelineBuilder.cs:202-205`:
```
catch (Exception e) when (e is not ConfigurationException)
{
    throw new ConfigurationException("Error when building pipeline, see inner Exception for details", e);
}
```
`:248-251` is the same shape. Neither runs any cleanup — no `CleanUpAfterFailedBuild`, no scope release. By contrast `TransformPipelineBuilder.cs:122-123` and `:163-164` (identical in `TransformPipelineBuilderAsync`) do: `try { CleanUpAfterFailedBuild(pipeline, transformLeases, messageMapperLease); } catch … `. What actually makes AC-30's "no pipeline scope is leaked" hold on the handler path is `PipelineBuilder.Dispose()` (`:269-270`, `=> _instanceScopes.Each(s => s.Dispose());`) firing from `using var builder = new PipelineBuilder<T>(…)` at `CommandProcessor.cs:317`, `:394`, `:472` and `:575` — which hoisting the ask would not disturb. I also checked ADRs 0070 and 0071: 0070 touches only the two transform builders (its step 3 sketch introduces `CleanUpQuietly`); 0071's implementation touches `HandlerLifetimeScope.Dispose()`, not `PipelineBuilder`'s catch. So no sibling supplies the missing cleanup either.

**Recommendation**: Split the instruction: in the two transform builders the new clause calls the shared cleanup (0070's `CleanUpQuietly`); in `PipelineBuilder` it rethrows only, because that catch has no cleanup, and name `PipelineBuilder.Dispose()` + `using var builder` as what discharges AC-30's second clause there. Then re-argue the hoist rejection on a reason that survives — the per-subscriber loop structure — rather than on a cleanup that does not exist.

---

### 5. `AmbientScopeSourceException` is a new **public core type** with no contract table, no stated inner-exception guarantee, and no stated obligation on the third-party container packages NFR-7 requires (Score: 66)

Every other new type in the ADR gets a Member / Input / Output / Error-conditions table. This one gets only the sentence "The type is a courier, not a contract: it exists only between the ask and the builder that catches it, is never observed by an application" — which is not true of an implementer, and which under-specifies the one thing the implementation sketch relies on.

**Evidence**: ADR *Where each type is touched*: "`Paramore.Brighter` | `AmbientScopeSourceException` | **new**". It must be constructible from `Paramore.Brighter.Extensions.DependencyInjection`, so it is public core surface, not a courier. The catch clause in ADR 0070's sketch (`0070-…md:311-316`) does `ExceptionDispatchInfo.Capture(e.InnerException!).Throw();` — a non-null assertion on a property the ADR never guarantees. And the ADR's own forces state "**The seam is a public extension point and must be implementable off ASP.NET and off Microsoft's container** (NFR-7)"; NFR-1/NFR-7 in `specs/0036-scoped-lifetime-per-pipeline/requirements.md:349,356` confirm the seam must be implementable over Autofac or SimpleInjector. A container package Brighter does not ship implements its own `CreatePipelineScope()` and must therefore wrap its provider's throws in this exact type or FR-24.1 silently fails for it — an obligation the ADR nowhere states, while simultaneously calling the type not-a-contract.

**Recommendation**: Add a contract row: constructor takes the provider's exception, `InnerException` is guaranteed non-null, the type is public in `Paramore.Brighter`, and any `IAmAScope`-producing factory that asks an `IAmAScopeProvider` **must** wrap a throw in it. Drop or qualify "never observed by an application" — it is observed by every implementer of the seam.

---

### 6. #4260 is claimed closed, but the fix as described touches only `_scopedInstances`; `GetOrCreateSingleton`'s faulted `Lazy` is untouched (Score: 65)

Two developers would implement this differently: one fixes both dictionaries, one fixes only the scoped one, and both can cite the ADR.

**Evidence**: Negative bullet: "`GetOrCreateScoped` **and** `GetOrCreateSingleton` cache a `Lazy<object?>` in default mode, which caches a **faulted** `GetService` … Fixing #4260 becomes a prerequisite of adoption". Step 3a: "`ServiceProviderLifetimeScope.cs:152`'s private `_scopedInstances` field becomes a resolution of this service and inherits the same rule, so the owned and borrowed paths keep one protocol between them. **This closes issue #4260 for both**" — where "both" plainly means the two *paths*, not the two *methods*. Nothing in the ADR moves or changes `_singletonInstances`. I confirmed the singleton fault-caching is real and untouched: `ServiceProviderLifetimeScope.cs:154-155`
```
var lazy = _singletonInstances.GetOrAdd(objectType, _ =>
    new Lazy<object?>(() => _serviceProvider.GetService(objectType)));
```
and a probe confirms `Lazy` (default `ExecutionAndPublication`) rethrows the cached fault on every subsequent `.Value` (`4.0 InvalidOperationException: boom` / `4.1 InvalidOperationException: boom`). Since `Singleton` "sits outside both [affinities], resolving from the root provider", adoption does not widen the singleton blast radius — so leaving it is defensible, but the ADR must say so rather than claim closure over a bullet that names both methods.

**Recommendation**: Say explicitly whether `_singletonInstances` is in scope. If it is not, change "closes issue #4260" to "closes the `Scoped` half of #4260; the `Singleton` cache is unaffected by adoption and is left for that issue", and align the Negative bullet.

---

### 7. Ladder row 2 labels a `Transient` handler pipeline **OWNED**, where FR-27.1 says such a pipeline takes no pipeline scope — and the ladder's own row 1 uses the opposite wording for the same category (Score: 62)

The ladder is the ADR's orienting artefact (and `.agent_instructions/documentation.md:88` holds this file up as *the* worked example of the decision-ladder form), so an inconsistency inside it costs more than usual. The reconciliation arrives ~300 lines later.

**Evidence**: Row 2: "`Scoped` does not participate in this pipeline — handler family, `HandlerLifetime` is `Transient` | **OWNED**, and **no ask is made at all** (FR-27.1)". Row 1, for the handler family under `Singleton`: "there is no next participant — **the pipeline takes no pipeline scope** and makes no ask". `requirements.md:249` (FR-27.1): "A pipeline none of whose participating factories is `Scoped` **takes no pipeline scope** and asks nothing". So the ladder gives two different outcome words to two configurations FR-27.1 puts in one category, and uses for one of them (`OWNED`) the same word rows 3–9 use for "creates and owns an FR-27 pipeline scope". The distinction is only drawn at line 418 — "That handle-for-`Transient` is ADR 0067's per-resolution machinery riding on a handle — it is **not** FR-27's pipeline scope" — which is correct and matches `0071-…md:207` ("a handler factory offers a handle for `Transient` too"), but it is far from the table an implementor will read.

**Recommendation**: Give row 2 its own outcome word — e.g. "**a handle, but not an FR-27 pipeline scope** (ADR 0067's per-resolution machinery); no ask" — and put the one-line reconciliation in the row or immediately under the table instead of 300 lines downstream.

---

### 8. NFR-8 is cited for a claim it does not make (Score: 60)

**Evidence**: ADR: "Per **NFR-8**, 'lifetime scope' is not used for anything introduced here." `requirements.md:357` — "**NFR-8 — Documentation must disambiguate `IAmAScope` from `IAmALifetime`.** The two names are close and the existing `IAmALifetime` (the handler instance-tracking lifetime, `HandlerLifetimeScope`) is not being replaced. Documentation and XML comments must state plainly what each is for and how they relate." NFR-8 says nothing about the phrase "lifetime scope" and imposes a documentation obligation on XML comments, which this ADR neither discharges nor mentions. Sibling ADR 0073 carries the corrective explicitly — `0073-…md:56`: "(NFR-8 is a documentation obligation about one specific ambiguity, `IAmAScope` against `IAmALifetime`; it is discharged where this package documents its types, **not by this sentence**.)" — so the set already knows the sentence is a miscitation, and 0072 (with 0075 and 0076) carries the uncorrected form. Aggravating: the ADR's own diagram node reads `ServiceProviderPipelineScope / owned: **owns a lifetime scope** and disposes its IServiceScope`, which uses the phrase the sentence claims is avoided. NFR-8 also appears in the References list, implying coverage this ADR does not provide.

**Recommendation**: Either adopt 0073's parenthetical verbatim, or drop the NFR-8 sentence and the NFR-8 reference from this ADR and let 0073 carry it.

---

### Verification log

- **Citations checked: ~30 distinct — 1 fact wrong, appearing twice.**
  - Verified correct: `ServiceProviderMapperFactory.cs:44-45` and `:45` (falls back to `ServiceLifetime.Singleton`); `ServiceProviderMapperFactoryAsync.cs:45-46`; `ServiceProviderTransformerFactory.cs:44-45`; `ServiceProviderTransformerFactoryAsync.cs:45-46`; `ServiceProviderHandlerFactory.cs:49-50` and `:50` (falls back to `Transient`); `BrighterOptions.cs:20`, `:37`, `:52`, `:69`, `:72`; `ServiceProviderLifetimeScope.cs:49`, `:163-178`, `:185`; `PipelineBuilder.cs:190`, `:193`, `:202`, `:202-204`, `:235`, `:248`, `:248-250`, `:269-270` (including that `:248` really does read `when(!(e is ConfigurationException))` against `:202`'s `when (e is not ConfigurationException)` — step 1a's Tidy-First observation is accurate); `TransformPipelineBuilder.cs:116-125`, `:157-166`, `:180` (v9 null path), and identical line numbers in `TransformPipelineBuilderAsync.cs`; `CommandProcessor.cs:481` (`Parallel.ForEach`), `:601` (`await Task.WhenAll(tasks)`); `ServiceCollectionExtensions.cs:119` and `:142`.
  - **Failed**: `ServiceProviderLifetimeScope.cs:152` cited as `_scopedInstances` (twice) — line 152 is `GetOrCreateSingleton`; the field is `:49`. (Finding 2.)
- **Counts re-derived**:
  - "the five container-backed factories" — **5** ✓ (`grep "class ServiceProvider"` returns 6 classes in the DI package; `ServiceProviderLifetimeScope` is not a factory). "The four container-backed transform factories" — **4** ✓.
  - "All four registration entry points route through `BrighterHandlerBuilder`" — **4** ✓ (`ServiceCollectionExtensions.cs:77`, `:98`; `ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:64`, `:131`).
  - "six builder `catch` blocks" / "three builders' two build paths" — **6** ✓ (2 in `PipelineBuilder`, 2 each in the two transform builders).
  - "six distinct failures … all converge on one fallback" — **6** ✓ as enumerated; ladder rows = **10**; "three latches" = **3** ✓; "Four properties of this shape" = **4** ✓; "Three ways out" = **3** ✓; "Two consequences" = **2** ✓; alternatives = **7** ✓; "seven ADRs" = **7** ✓ and the sibling map matches all six siblings' tables verbatim.
  - Unifying sentence — "**the per-pipeline object carries the DI scope**" — present and identically worded in all seven ADRs ✓.
- **Mermaid blocks rendered: 1/1.** `mmdc@11 -i d1.mmd -o d1.svg` exit 0, 31,713-byte SVG produced. Also rendered to PNG at `-w 1600 -b white` and inspected visually: readable, no swallowed generics, dependency arrows point DI → core correctly.
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;'` → **0** ✓. `grep -ni "chain"` → **0 hits** ✓. Tone grep for authoring-conversation / ephemeral-state references → no hits (the two "working name" hits both cite C-11, a durable constraint).
- **Probes compiled/run: 2.**
  1. `Microsoft.Extensions.DependencyInjection` 9.0, net9.0 — confirmed the ADR's probe design is sound: `IServiceScopeFactory` resolves from a live scope with **no descriptor of its own** (`descriptor present in collection? False`), and a **disposed** scope throws `ObjectDisposedException` for `IServiceScopeFactory`, for a real scoped service, and for `IServiceProviderIsService`. Also confirmed default-mode `Lazy` caches the fault across repeated `.Value` (relevant to findings 6 and to #4260).
  2. `Microsoft.NET.Sdk.Web`, net9.0 — reproduced ASP.NET's end-of-request accessor clearing; deferred work started from the request flow sees `accessor.HttpContext is NULL`, which is finding 1.
- I did not open any of the prohibited prior-review files.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 4 |
| 50-69 | 4 |
| 0-49 | 0 |

Total: 8 · At or above 60: 8
## Reviewer: ADR 0073 — ASP.NET Core request-scope package

### 1. The public/internal decision rests on a misreading of AC-18 (Score: 72)

The Decision's second paragraph gives two reasons for making `HttpContextScopeProvider` and `HttpRequestScope` public. The first is stated as decisive and is falsified by AC-18's own text.

**Evidence**: ADR 0073 line 84: *"AC-18 pins the ordering of an opt-in relative to `AddBrighter` using a recorder that **wraps** the ASP.NET provider and delegates to it, **which is unbuildable against an internal type**."* AC-18 (`specs/0036-scoped-lifetime-per-pipeline/requirements.md:561-569`) specifies exactly how the recorder obtains the instance it wraps: *"(The recorder captures `IServiceProvider` and resolves `IEnumerable<IAmAScopeProvider>` **on its first `GetAmbient` call rather than in its constructor**, selecting **the entry that is not itself**.)"* The recorder never names `HttpContextScopeProvider`; it filters the enumerable by reference identity and delegates through `IAmAScopeProvider`, which ADR 0072 puts in core as `public`. An `internal` `HttpContextScopeProvider` builds AC-18's recorder without difficulty. The requirements' own revision-9 note records that this parenthetical was added precisely to say "how the recorder obtains the instance it wraps" (`requirements.md:855`). The *second* reason (NFR-7 composability, `requirements.md:358`) does hold, and ADR 0075 makes the analogous argument for its own type (`docs/adr/0075-publish-subscriber-scope-suppression.md:228`) — so the decision may well be right, but the reason the ADR leads with is wrong.

**Recommendation**: Delete the AC-18 clause, or restate it accurately — e.g. AC-19 and AC-29 assert a warning naming *the ASP.NET provider's implementation type*, which a cross-assembly test reaches most naturally through `typeof(HttpContextScopeProvider)`. Let NFR-7 and 0075's precedent carry the decision.

---

### 2. AC-14 does not need an ASP.NET host, and step 4a sends it to the wrong project (Score: 70)

Step 4a asserts that ten named acceptance criteria "need a running ASP.NET Core host with a controller action" and therefore belong in the new test project. AC-14 is not one of them, and misplacing it makes the criterion unimplementable where it is sent.

**Evidence**: ADR 0073 line 274: *"Ten of the acceptance criteria this ADR cites or discharges — **AC-14**, AC-15, AC-16, AC-17, AC-18, AC-19, AC-29, AC-34, AC-48 and AC-49 — need a running ASP.NET Core host with a controller action."* AC-14 (`requirements.md:505-510`) reads: **Given** *"an application configured exactly as before this change — **no `IAmAScopeProvider` registered** … with an `IHttpContextAccessor` spy registered"*, **When** *"**the existing Brighter test suite** for `Send`, `Publish`, `Post`, `DepositPost` and consumption is run"*, and it goes on to enumerate excluded files by name under `tests/Paramore.Brighter.Extensions.Tests/` and a non-excluded pair at `FactoryLifetimeTests.cs:36-55` / `:154`. There is no controller, no `HttpContext`, and no provider. AC-14 is a whole-suite regression criterion executed across the existing projects; it cannot be run from a new `Paramore.Brighter.Extensions.AspNetCore.Tests`. I verified the two named exclusion targets exist as projects: `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` is on disk and in `Brighter.slnx`. By contrast AC-48 (`:741`) and AC-49 (`:750`) each open "Given an ASP.NET host … a controller action calls `Send`" and genuinely do need one.

**Recommendation**: Drop AC-14 from step 4a's list (nine, not ten — and correct the matching "ten of this ADR's criteria need one" in the *Where each type is touched* row). Say separately what AC-14 actually needs: an `IHttpContextAccessor` spy visible to the existing suite, which is a reference question, not a hosting one.

---

### 3. FR-15's normative clause is assigned to a sibling that does not claim it (Score: 68)

0073 splits FR-15 and hands the normative half to ADR 0076. 0076's Scope statement does not take it, so no ADR in the set discharges FR-15's normative clause.

**Evidence**: ADR 0073 line 34: *"It discharges **FR-15's package-inertness half** … **FR-15's normative clause, the affinity option's default value, is ADR 0076's**."* ADR 0076 line 32: *"It discharges FR-14 and **the write-through half of FR-17**… It serves FR-16, FR-18, FR-19, FR-20, FR-21, FR-22, FR-23, FR-25.11, NFR-1, NFR-4 and NFR-7."* FR-15 is absent from that sentence and from every "discharges" clause in the set — I grepped `FR-15` across all seven ADRs: it appears in 0073 (6 times) and 0076 (4 times, all incidental — a contract-table note at `:241`, a *Where each type is touched* row at `:319`, a Technology-Choices aside at `:337`, a Positive bullet at `:359`, plus References). ADR 0072's Scope (`:31`) discharges FR-10, FR-11, FR-12, FR-13's ownership clause, FR-21, FR-24, FR-27.1 and FR-27.2 — no FR-15. FR-15 itself (`requirements.md:264`) is one normative sentence plus an example, and it is the example half 0073 takes.

**Recommendation**: Either have 0076's Scope claim FR-15's normative clause explicitly (it is the ADR that sets `DefaultScopeAffinity = AlwaysNew`), or drop 0073's assignment and say FR-15 is discharged jointly by 0076's default and 0072's ladder without naming an owner it hasn't agreed to.

---

### 4. The one bold Decision sentence, and the frontmatter summary the index publishes, are false as written (Score: 66)

The unifying sentence claims a public surface the ADR then contradicts one paragraph later. The frontmatter summary makes the same claim and is copied verbatim into the generated index, where the retraction does not travel with it.

**Evidence**: ADR 0073 line 80 (Decision, bold): *"…offered to Brighter by a package of its own, **whose entire public surface is one `IServiceCollection` extension**…"* Line 84, immediately after: *"`HttpContextScopeProvider` and `HttpRequestScope` are **public**."* The frontmatter `summary` (line 8) says *"whose **whole surface** is one IServiceCollection extension AddBrighterRequestScope(…)"*, and `docs/adr/index.md:110` reproduces that sentence in full as the ADR's index entry — the index is a generated file (`docs/adr/index.md:1-3`, "GENERATED FILE — DO NOT EDIT BY HAND"), so a reader browsing the index is told the package has three public types fewer than it has. Per `.agent_instructions/documentation.md:108`, the rule is "State the unifying rule once, in one sentence" — a sentence that needs the next paragraph to withdraw it is not that.

**Recommendation**: Rewrite the bold sentence to say what is true and load-bearing — e.g. "…whose entire opt-in surface is one `IServiceCollection` extension: an application calls one method and names no type." Then update the frontmatter `summary` to match and regenerate the index.

---

### 5. `HttpRequestScope.Services` non-nullness is checked at construction but read from a mutable property (Score: 65)

The ADR asserts that ADR 0072's non-null obligation on `Services` is made "true rather than assumed" by a provider-side check. The types it specifies do not deliver that: the check runs once, the read happens later, and the ADR specifies the scope as holding an `HttpContext` rather than the provider.

**Evidence**: ADR 0073 line 202 (contract table): *"**Obliged** to be non-null by ADR 0072's role contract, and this implementation is guarded so that it is: an `HttpRequestScope` is **never constructed over a null `RequestServices`**, because the provider checks it before wrapping (step 1)."* But line 84 fixes the scope's construction as taking *"only an `HttpContext`"*, and the same table row gives `HttpRequestScope.Services` Input "none" / Output "`HttpContext.RequestServices`" — i.e. a pass-through read of a settable property on a stored context, not a captured `IServiceProvider`. `HttpContext.RequestServices` has a public setter and is backed by a replaceable `IServiceProvidersFeature`; the construction-time check is therefore not an invariant of the read. ADR 0072 line 224 states the obligation flatly: *"Must not throw and must not be `null`."* I confirmed the failure mode is reachable in the shape the ADR itself calls "an expected caller": a probe compiled against `Microsoft.AspNetCore.App` on net10.0 printed `DefaultHttpContext.RequestServices is null: True`.

**Recommendation**: Specify that `HttpRequestScope` is constructed over the **`IServiceProvider`** the provider validated, not over the `HttpContext` — `new HttpRequestScope(context.RequestServices)`. Then the non-null guarantee is structural, FR-23's stale-scope case is unaffected (a captured provider can be disposed just as a re-read one can), and the contract-table sentence becomes true.

---

### 6. The test-project count is 37, not 38, and "`Brighter.slnx` has no ASP.NET entry" is false and self-contradicted (Score: 62)

**Evidence**: ADR 0073 line 274: *"**no test project in the repository can host one**: none of the **38** references `Microsoft.AspNetCore.*`, `Microsoft.AspNetCore.Mvc.Testing` or `WebApplicationFactory`, and **`Brighter.slnx` has no ASP.NET entry**."*
- Recount: `find tests -name "*.csproj" -not -path "*/obj/*" | wc -l` → **37**. The 38 comes from the solution file: `grep -o 'Path="tests/[^"]*"' Brighter.slnx` → 38 entries, of which one is `tests/README.md`, a solution item and not a project (`comm` against the on-disk list shows exactly that one difference, and zero projects missing from the solution).
- The substantive half holds: no `tests/*.csproj` contains `AspNetCore` or `FrameworkReference`, `WebApplicationFactory` appears nowhere in `tests/`, and `Microsoft.AspNetCore.Mvc.Testing` is not in `Directory.Packages.props`.
- The slnx clause is false: `Brighter.slnx:318` is `<Project Path="src/Paramore.Brighter.ServiceActivator.Control.Api/Paramore.Brighter.ServiceActivator.Control.Api.csproj" />`, and the ADR's own *Technology Choices* (line 256) calls that project *"a packable ASP.NET Core library on exactly these targets … `Sdk="Microsoft.NET.Sdk.Web"` with `OutputType=Library`"* — which I verified by reading the csproj.

**Recommendation**: "none of the **37** test projects" and "`Brighter.slnx` has no ASP.NET **test** entry — the only ASP.NET project in the solution is `src/…Control.Api`, cited below."

---

### 7. The "mechanism, end to end" diagram omits the null check the ADR twice calls load-bearing (Score: 61)

The section is meant to lead with the artefact that orients and then read the invariants off it. The sequence diagram has one null branch where the specified mechanism has two, and the missing one is the branch the ADR elsewhere says makes 0072's contract true.

**Evidence**: The `sequenceDiagram` (lines 90–114) branches `alt an HttpContext is current` / `else none — hosted service, pump, background thread, startup`. There is no `RequestServices is null` branch. Yet step 1 (line 266) says: *"The provider's whole body is an affinity check, **two** null checks — `_accessor.HttpContext`, then that context's `RequestServices` — and a wrap… **The second null check is what makes ADR 0072's non-null obligation on `Services` true rather than assumed**"*; the contract table (line 201) requires the provider not to throw *"where a context is current but carries no `IServiceProvidersFeature`"*; and the Risks table (line 307) lists *"a directly-constructed `DefaultHttpContext`"* as a named risk. The prose immediately under the diagram — *"Every `null` on that diagram means the same thing"* — is true of the two nulls shown and silently excludes a third the reader is later told is essential. Both mermaid blocks render (exit 0, SVG and PNG produced) and I read the PNG: the diagram is otherwise clear and correctly sized.

**Recommendation**: Add the branch — `else a context is current but RequestServices is null` → `null` — inside the `JoinAmbient` arm, or fold it into the existing else label. It costs one line and removes the only place the diagram and the mechanism disagree.

---

### 8. FR-10 is cited three times for a rule FR-10 does not state (Score: 55)

**Evidence**: ADR 0073 cites FR-10 for the provider-side `AlwaysNew` obligation at line 72 (*"D16 / FR-10 — the ask is made even under `AlwaysNew`… and the provider must neither consult nor adopt on such an ask"*), line 201 (*"It neither consults `IHttpContextAccessor` nor returns anything on an `AlwaysNew` ask (D16, FR-10)"*) and line 270 (*"FR-10 requires the provider neither to consult nor to adopt on such an ask"*). FR-10 (`requirements.md:213`) says only that the seam exists, names the three types, and requires core never to see a service provider; it says nothing about `AlwaysNew` asks. The rule is **D16**'s (`:820`: *"An `AlwaysNew` ask must neither consult nor adopt"*). Mitigating: the requirements themselves make the same attribution twice (AC-18 at `:568` speaks of "FR-10's `AlwaysNew` rule"; FR-24 at `:244` of "a provider that violates FR-10"), so this is inherited rather than invented — which is why it is Medium and not higher.

**Recommendation**: Cite D16 alone, or D16 + FR-24.4. If the attribution is meant to survive, FR-10 needs the clause added on the requirements side.

---

### 9. References omit four ACs the body relies on, and never name AC-22.2 — the automated guard for this ADR's central constraint (Score: 52)

**Evidence**: References (line 332) lists `AC-14, AC-18, AC-19, AC-22, AC-29, AC-48, AC-49`. Step 4a (line 274) additionally relies on **AC-15, AC-16, AC-17 and AC-34**, none of which appear. Separately, the *Unchanged* paragraph (line 250) states the ADR's load-bearing dependency rule — *"`Paramore.Brighter.Extensions.DependencyInjection` gains no ASP.NET reference (NFR-2)"* — and the flowchart prose (line 148) calls it *"the whole of NFR-2"*, but the ADR only ever cites **AC-22.3**, the *core-source* guard. The clause that mechanically enforces NFR-2 is **AC-22.2** (`requirements.md:657`): *"`Paramore.Brighter.Extensions.DependencyInjection` has no `PackageReference`/`ProjectReference` whose `Include` matches `Microsoft.AspNetCore.*` (NFR-2)"*.

**Recommendation**: Add AC-15, AC-16, AC-17, AC-34 to References, and cite AC-22.2 beside the NFR-2 claim in the *Unchanged* paragraph.

---

### 10. "The sole permitted difference" understates FR-19's bound (Score: 48)

**Evidence**: ADR 0073 line 289: *"the only thing distinguishing it is FR-24.2's single latched `Warning`, **which FR-19 fixes as the sole permitted difference** (AC-19)"*, and line 65: *"save one latched `Warning` per container per provider type, which FR-19 makes the only permitted difference between the two."* FR-19 (`requirements.md:287`) actually says: *"**Log entries are the only permitted difference**… Where an ambient source is registered there are exactly **two**, and both are bounded: FR-23's *ambient offered but unusable* entry … and FR-24.2's *no ambient offered* entry"*. FR-19 permits two entries; only one fires in the FR-18 host 0073 is describing, so the outcome is right and the gloss on FR-19 is loose. FR-19 is also framed as the *consumer-side* affinity-inertness rule, not as a comparison between opted-in and not-opted-in hosts.

**Recommendation**: "…the only difference **in this case**, FR-19 permitting at most two log entries in total for a host with an ambient source registered."

---

### Verification log

**Citations checked: 12 — all verified except as noted below.**
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:65-66` — `if (services == null) throw new ArgumentNullException(nameof(services));` ✅ exact.
- `ServiceCollectionExtensions.cs:119` and `:142` — the two `BrighterHandlerBuilder` overloads ✅ exact.
- `BrighterOptions.cs:37` `IsolateTransientHandlerScope`; `:20` `HandlerLifetime`; `:52` `MapperLifetime`; `:69` `TransformerLifetime` ✅ all four exact.
- `src/Directory.Build.props:43` `BrighterTargetFrameworks = netstandard2.0;net8.0;net9.0;net10.0` ✅ exact; `:45` `BrighterCoreTargetFrameworks = net8.0;net9.0;net10.0` ✅ exact.
- `src/Paramore.Brighter.ServiceActivator.Control.Api` — `Sdk="Microsoft.NET.Sdk.Web"`, `OutputType=Library`, `TargetFrameworks=$(BrighterCoreTargetFrameworks)`, `IsPackable=true` ✅ all four claims hold.
- Requirement ids verified against `requirements.md`: FR-10 `:213` (**does not** support the `AlwaysNew` claim — finding 8), FR-12 `:226`, FR-15 `:264`, FR-16 `:267`, FR-17 `:274`, FR-18 `:281`, FR-19 `:287` (looser than cited — finding 10), FR-21 `:294`, FR-23 `:284`, FR-25.11 `:687`, NFR-1 `:350`, NFR-2 `:353`, NFR-7 `:358`, NFR-8 `:359`, C-7 `:371`, C-10 `:374`, C-11 `:375`, C-14 `:386`, C-15 `:387`, D1 `:805`, D11 `:815`, D13 `:817`, D14 `:818`, D16 `:820`, D17 `:821`, OOS-4 `:395`, AC-14 `:505` (**mis-scoped** — finding 2), AC-18 `:561` (**mis-read** — finding 1), AC-19 `:570`, AC-22.3 `:661`, AC-29 `:576`, AC-48 `:741`, AC-49 `:750`. Blindness rules honoured — I opened none of the review-round files or `PROMPT.md`.

**Counts re-derived:**
- "24 projects in `src/` already target `$(BrighterCoreTargetFrameworks)`" → **24** ✅.
- "none of the 38 [test projects]" → **37** ❌ (the 38th `tests/` path in `Brighter.slnx` is `tests/README.md`).
- "Every `Use*` in the repository extends `IBrighterBuilder`", and the eight named (`UseScheduler`, `UseOutboxSweeper`, `UseOutboxArchiver`, `UseFluentValidation`, `UseAsyncApi`, `UseExternalLuggageStore`, `UseBoxProvisioning`, `UsePublicationFinder`) → all eight exist and all return `IBrighterBuilder` ✅. Repo-wide there are **10** distinct `Use*` extensions; the two not listed (`UseSpecification`, `UseDataAnnotations`, plus `UseRequestScheduler`/`UseMessageScheduler` beside `UseScheduler`) also extend `IBrighterBuilder`, so the universal claim holds ✅.
- "The `IServiceCollection` extensions the application sees are `AddBrighter` and `AddConsumers`" → grep for `this IServiceCollection` across `src/` returns exactly four methods, two `AddBrighter` and two `AddConsumers` ✅. `AddProducers` (`:247`, `:383`) and `AddControl` (`ControlExtensions.cs:11`) extend `IBrighterBuilder` ✅.
- "a repository-wide search for `namespace Microsoft.Extensions.DependencyInjection` in `src/` finds nothing" → **0 hits** ✅. Namespaces match assemblies for both `AddBrighter` (`:43`) and `AddConsumers` (`:12`) ✅.
- "a grep for `FrameworkReference` or `AspNetCore` across `src/*.csproj` finds nothing" → **0 hits each** ✅.
- "Nothing in the repository implements `IAmAScopeProvider`" → the type does not exist anywhere in `src/` or `tests/` ✅.
- The `Paramore.Brighter.Extensions.*` family (`DependencyInjection`, `Diagnostics`, `OpenTelemetry`) and `ServiceActivator.Extensions.Hosting` → all four directories exist ✅.
- Step 4a's list is 10 items and matches the "ten of this ADR's criteria" row ✅ (membership is the problem, not the count).
- "six scope-configuration rules" (0073's map row for 0074) → 0074 names FR-22's four + FR-24.3 + FR-17 = **6** ✅.

**Mermaid blocks rendered: 2/2 — none failed.** `mmdc` (mermaid-cli 11) exit 0 for both; `d1.svg` (30,690 B) and `d2.svg` (23,213 B) produced. Both also rendered to PNG at `-w 1600 -b white` and inspected visually. `d2` (the assembly flowchart) is legible and confirms the ADR's claim that every boundary-crossing edge runs new-package → DI package → core. `d1` (the registration/request sequence) is legible; its defect is omission, not rendering — finding 7.

**Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;' docs/adr/0073-…md` → **0** ✅. Tone checks: no reference to authoring-conversation participants, `PROMPT.md`, spec phase, commit hashes or review rounds. "chain" appears three times, all as the ordinary English verb ("for chaining", "It would chain naturally") ✅.

**Probes compiled/run** (scratchpad `probe/`, `Microsoft.NET.Sdk` class library `net8.0;net9.0;net10.0` + `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`, consumed by a plain `Microsoft.NET.Sdk` console app via `ProjectReference`; SDK 10.0.102):
1. **`FrameworkReference` transitivity — CONFIRMED, twice.** The consuming console app's `ConsumerApp.runtimeconfig.json` contains `{"name": "Microsoft.AspNetCore.App", "version": "10.0.0"}` even though the app itself declares no ASP.NET dependency. `dotnet pack` of the library emits a nuspec with a `<frameworkReferences>` group per TFM, so the flow holds for NuGet consumers as well as project references. The ADR's summary sentence and its Negative consequence are both correct and were worth verifying.
2. **`IHttpContextAccessor` → `Microsoft.AspNetCore.Http.Abstractions`; `AddHttpContextAccessor`/`HttpServiceCollectionExtensions` → `Microsoft.AspNetCore.Http`** — printed at runtime from the loaded assemblies, and cross-checked against the ref pack XML at `/usr/local/share/dotnet/packs/Microsoft.AspNetCore.App.Ref/10.0.2/ref/net10.0/`. ✅ matches the ADR exactly.
3. **`AddHttpContextAccessor()` is idempotent** — two calls leave `services.Count == 1`. ✅ the `TryAddSingleton` claim holds.
4. **`DefaultHttpContext.RequestServices` returns `null`** (does not throw) with no `IServiceProvidersFeature`. ✅ the ADR's stated failure mode is real — and is the basis of finding 5.
5. **MS DI last-descriptor-wins for an instance-registered singleton** — two `AddSingleton(new Box(...))` calls leave **both** descriptors in the collection with distinct `ImplementationInstance` values (`first,second`), and `GetRequiredService` resolves `second`. ✅ this is exactly the registration model 0073 specifies and 0074 reads; both halves of the claim hold.
6. **`Directory.Packages.props` claim** — the library built and packed with no CPM entry; `<frameworkReferences>` carries no version, so central package management genuinely has nothing to manage. ✅ (`ManagePackageVersionsCentrally` is `true` at `Directory.Packages.props:3`, and `Microsoft.AspNetCore.Mvc.Testing` is indeed absent from it, so step 4a's qualification about the *test* project is also correct.)

No network for `curl`, so I could **not** verify the claim that "the only shippable versions of either [`Microsoft.AspNetCore.Http.Abstractions`, `Microsoft.AspNetCore.Http`] are the end-of-life 2.2.x line" against nuget.org. It matches my knowledge of those packages but is unverified here.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 2 |
| 50-69 | 7 |
| 0-49 | 1 |

Total: 10 · At or above 60: 7
## Reviewer: ADR 0074 — `docs/adr/0074-lifetime-validation-evaluation-site.md`

### 1. The exclusion set is walked over two different input sets in two places, and one of the two is wrong about the handler half (Score: 70)

The ADR states the request-type sourcing for `ArtefactExclusionSet` twice and the statements disagree — and the second one is additionally false about `Describe()`.

**Evidence**:

Line 306 (*Captive-dependency detection*):
> "`PipelineBuilder<IRequest>.Describe()` (`PipelineBuilder.cs:151`) yields every `PipelineStepDescription.HandlerType`, and `TransformPipelineBuilder.DescribeTransforms(registry, requestType, includeAsync: true)` (`:270`) yields every `TransformStepDescription.TransformType` **for each request type reachable from the publications, the subscriptions, and the registered handlers**. A mapper reachable by none of those three is unreachable at run time as well…"

Line 404 (Implementation Approach step 5a):
> "**The request types both halves are walked over come from the publications and the subscriptions.** So the signature is `ArtefactExclusionSet.Build(pipelineBuilder, registry, publications, subscriptions)`…"

Two defects. (a) Three sources become two; "the registered handlers" is dropped, and step 5a is the implementor's section, so that is the one that gets built. (b) "both halves are walked over [request types]" is false for the handler half. I opened `src/Paramore.Brighter/PipelineBuilder.cs:146-162`: `Describe()` is parameterless and iterates `inspector.GetRegisteredRequestTypes()` itself — it consumes no publication and no subscription. `DescribeTransforms` at `TransformPipelineBuilder.cs:270` is the only half that needs a request type (and the two-arg overload at `:255` does default `includeAsync: false`, as the ADR says — verified).

This is not cosmetic: AC-42's `Paramore.Brighter.Extensions.Tests` transform clause asserts **no** warning, and in a host whose `ResolvePublications` returns `null` (`BrighterPipelineValidationExtensions.cs:135-142` returns null when there is no `IAmAProducerRegistry`) an implementation that follows step 5a literally produces an **empty transform half** and warns against that transform. The two readings are separable by a test.

**Recommendation**: state the source set once, in step 5a, and make it match line 306 — "the request types the transform half is walked over come from the publications, the subscriptions and `pipelineBuilder.Describe()`'s registered request types; the handler half needs none, because `Describe()` enumerates the subscriber registry itself."

---

### 2. FR-17's "position stands in for the value" fallback contradicts the same paragraph's same-affinity exclusion (Score: 65)

FR-17's rule is defined as distinctness over the `ScopeAffinity` **value**, then given a fallback borrowed verbatim from FR-24.3 that substitutes a *registration position* for a value. Two positions are always distinct, so the fallback turns the idempotent case into a finding.

**Evidence**: line 284 —
> "Distinctness here is over the `ScopeAffinity` **value**, so a repeat carrying the same affinity is not a finding, mirroring FR-24.3's own exclusion and for the same reason (AC-49's third branch). The values are read from the descriptors' `ImplementationInstance` … **a descriptor supplying no instance contributes its registration position, as FR-24.3's does.**"

FR-24.3's version is sound because its key is the *implementation type* and a position is a defensible "unknown, treat as distinct". FR-17's key is a value, and a position is not one. Under the fallback, two descriptors carrying the *same* affinity but registered by factory delegate yield two distinct keys → one `Warning`, which requirements.md:275 forbids in terms ("Repeated calls carrying the **same** affinity are idempotent in effect and are **not** a finding") and AC-49's third branch (requirements.md:758-759) falsifies.

The fallback is reachable. I checked `docs/adr/0073-…:189` — the ASP.NET extension does register an instance (`services.AddSingleton(new ScopeAffinityOverride(affinity));`), so 0074's claim about `ImplementationInstance` holds *for that caller*. But `ScopeAffinityOverride` is **public** in the DI package (`0076-…:216`, `:321`) and `0073-…:290` explicitly anticipates "another package registers its own `IAmAScopeProvider` and its own `ScopeAffinityOverride` in exactly the same two lines" (NFR-7). A third-party opt-in package using `AddSingleton(sp => new ScopeAffinityOverride(x))` lands in the fallback.

**Recommendation**: drop the fallback for FR-17 and say what the rule does instead — a descriptor from which no `ScopeAffinity` value can be read contributes **nothing** to the distinctness set (it cannot be compared, and reporting an uncomparable descriptor as a conflicting affinity is worse than missing it). Note the obligation on any override registrar that it must register an instance if it wants to be reportable.

---

### 3. FR-22.4 is missing from the snapshot-staleness Negative bullet, which is the one rule whose staleness is most likely and most costly (Score: 65)

The ADR admits in the body that FR-22.4 inherits the snapshot precondition and that the natural authoring form defeats it — then omits FR-22.4 from the Negative consequence that enumerates exactly this exposure.

**Evidence**: line 292 —
> "**Like FR-24.3, this rule reads the snapshot and therefore inherits its precondition** … In the natural fluent form an application registration made after `AddBrighter` is also after the snapshot, so **the rule would see nothing — the same silent loss it exists to break.**"

But line 430, the Negative bullet titled "Every rule that reads the collection sees only what was registered before the call", names only: "The duplicate-provider and repeated-opt-in rules miss a registration made after `ValidatePipelines()` … The captive-dependency rule reads the same snapshot for *both* of its inputs…". FR-22.4 is absent, and the *Failure modes, enumerated and accepted* table (lines 323-332) is captive-dependency-specific. The Risks table (lines 442-453) has no row for it either.

This matters more than for the other two rules because AC-50's after-ordering branch (requirements.md:769-770) is a plain `services.AddSingleton<IBrighterOptions>(...)` — the shape an application writes beside its other `services.Add*` calls in `Program.cs`, i.e. after the `AddBrighter(...).ValidatePipelines()` statement has already snapshotted. The ADR's own claim that "AC-50's Given carries that constraint" rests on reading "`ValidatePipelines()` called last" as "called after that statement too", which requires holding the `IBrighterBuilder` and calling it separately — not the fluent form the rest of the ADR assumes.

**Recommendation**: add FR-22.4 to the line-430 Negative bullet by name, and add a Risks row: "the defeated-opt-in error is itself defeated by an application registration made after `ValidatePipelines()`; mitigation is FR-25.10's guidance only, which is weaker than a mechanism". Also say explicitly, in *How the inputs reach the rules*, what "called last" has to mean for AC-50's after-ordering branch to be reachable.

---

### 4. `SpecificationEvaluator` is a new **public** type on core's surface, and the ADR never says so (Score: 64)

Every other new type in the ADR carries an accessibility. The one type added to `Paramore.Brighter` does not, and the design forces it to be public.

**Evidence**: *Where each type is touched*, line 370 —
> "| `Paramore.Brighter` | `SpecificationEvaluator` | **new** — the entity/spec harvest loop lifted verbatim out of `PipelineValidator.EvaluateSpecs` (`:152`). No container types |"

compare the rows immediately below it: "**new**, public", "**new**, internal". And step 5 (line 402): "`ScopeConfigurationValidator` … **evaluates both entity families through `SpecificationEvaluator`**" — a call from `Paramore.Brighter.Extensions.DependencyInjection`. I grepped for `InternalsVisibleTo` across `src/Paramore.Brighter/` (all `*.cs`, `*.csproj`) and `Directory.Build.props`: **zero** hits. So `SpecificationEvaluator` must be `public`.

Two consequences the ADR does not draw. (i) The Positive bullet "Core gains no *container* concept … Core does gain one type" understates it: core gains new **public API**, permanently, on a `netstandard2.0` assembly. (ii) `EvaluateSpecs` is `private static void EvaluateSpecs<T>(IEnumerable<T>, IEnumerable<ISpecification<T>>, List<ValidationError> findings)` (verified at `PipelineValidator.cs:152-171`) — "lifted verbatim" would put a mutable `List<ValidationError>` out-parameter on a public core signature. And the extraction is not forced: `ISpecification<T>`, `Specification<T>` and `ValidationResultCollector<T>` are all already `public` in core (`Specification.cs:35`, `:71`; `ValidationResultCollector.cs:35`), so the DI package can run the loop itself; only `Specification<T>.LastResults` is internal and the loop does not touch it.

**Recommendation**: state the accessibility in the touched table, state the signature the public form takes (returning `IEnumerable<ValidationError>` rather than filling a caller's `List`), and add the public-core-surface cost to *Negative* — or reconsider the extraction, since the Technology Choice weighs it against "a second copy" without weighing it against "new permanent public API in the assembly this ADR exists to keep clean".

---

### 5. "Six DI-package implementation types on the public surface" — the ADR's own snippet names two (Score: 62)

**Evidence**: line 206 —
> "**Its constructor is therefore `internal`, while the type is public** — C# forbids a public constructor whose parameter types are less accessible (CS0051) … The fix is the constructor's accessibility, not the entities': **widening them would put six DI-package implementation types on the public surface** to satisfy a compiler rule."

The CS0051 rule is correctly stated, but the count is not derivable from the ADR. The constructor the ADR gives at lines 229-234 takes five arguments: `inner` (`PipelineValidator`, public, core), `sp.GetRequiredService<IBrighterOptions>()` (public, core), `snapshot` (`ContainerRegistrationSnapshot`, internal), `ArtefactExclusionSet.Build(...)` (`ArtefactExclusionSet`, internal), `registry` (`Lazy<MessageMapperRegistry>?`, public, core). **Two** internal types appear in the signature — those are the only ones CS0051 could ever force open. `ScopeConfiguration`, `DescriptorRecord`, `ArtefactRegistration`, `ArtefactKind`, `ArtefactConstructorSelector` and `ScopeConfigurationRules` are built inside and never named on the constructor.

For reference I re-derived the other counts in this ADR and they hold: nine new DI types + one core type (touched table, lines 372-374) = 9 + 1 ✓; six rules, consistently, in the map row, the sequence-diagram note, the roles table (`**deciding** ×6`), the rule table, and 5 + 1 across the two entity families ✓; "three errors and three warnings" ✓; four identifiers in AC-22.3's scan ✓; eleven FR-25 clauses ✓; "three files numbered 0053, two numbered 0054 and two numbered 0064" ✓ (`ls docs/adr` gives exactly 3/2/2); "125 files under `tests/` register `IBrighterOptions` themselves" ✓ (`grep -rl -E 'AddSingleton<IBrighterOptions|TryAddSingleton<IBrighterOptions|AddSingleton\(typeof\(IBrighterOptions' tests/` → **125**).

**Recommendation**: replace "six" with the two types the constructor actually names, or restate the sentence as being about the entity family generally without a count.

---

### 6. The ADR contradicts itself about what AC-42's `[UsePolicyAsync]` clause pins (Score: 62)

**Evidence**: line 313 —
> "`ExceptionPolicyHandlerAsync<>` is registered by `ServiceCollectionSubscriberRegistry` and would also be skipped by the open-generic rule below, so **on that type the two paths are indistinguishable from outside** and **AC-42's `[UsePolicyAsync]` clause does not pin which one ran** — both yield no warning, and the clause asserts output only."

line 404 —
> "a null one produces the handler half and an empty transform half, and **AC-42's `[UsePolicyAsync]` clause is pinned on exactly that half**."

If the clause cannot distinguish the exclusion mechanism from the open-generic skip, it is not pinned on the handler half — it would pass with the handler half absent entirely. Step 5a uses that claim as the justification for `registry` being nullable ("a null one produces the handler half…"), so the argument for a design choice rests on a property line 313 denies.

I confirmed the underlying facts: requirements.md:628-629 does frame the `[UsePolicyAsync]` clause as "pinning the *handler* half of FR-22.3's 'which types' exclusion mechanism", and `ServiceCollectionSubscriberRegistry.cs:63/76/90/116/130/146/160` all register at `ServiceLifetime.Transient` as their own service type (all seven lines verified) — so line 313's observation that the two paths coincide on that type is the correct one and line 404 is the loose sentence.

**Recommendation**: fix line 404 to say what is actually true — the `[UsePolicyAsync]` clause *exercises* the null-registry path but does not discriminate the mechanism — and pick a different justification for `registry` being nullable (the honest one is already in the ADR: `mapperRegistryFactory` is nullable at `BrighterPipelineValidationExtensions.cs:85-88`, verified).

---

### 7. `### The documentation this set owes` parks set-level implementation-plan bookkeeping inside a one-decision ADR (Score: 60)

The ADR opens by declaring one decision, then spends a full `###` section (lines 58-78) deciding nothing and saying so.

**Evidence**: lines 60-62 —
> "**`docs/guides/lifetimes-and-scoping.md` is an implementation-plan deliverable, and this is where that is recorded.** … **It is not an ADR-level decision.** Writing a guidance page decides nothing that six ADRs have not already decided; what it needs is **an owner in the implementation plan** and a map saying where each clause's substance comes from…"

`.agent_instructions/documentation.md` §*ADR structure* gives `## Context` → `### Where this ADR sits` → `### {the problem}` → `### The forces`, and §*Writing tone* says "Do not reference ephemeral working state … current spec phase". "An owner in the implementation plan" is exactly that. Structurally, 0074 is the only ADR in the set with **three** `###` sections between `Where this ADR sits` and `The forces` (0070/0072/0073/0075/0076 have one; 0071 has two) — I extracted the `###` skeletons of all seven to compare. The FR-25 clause map is genuinely useful set-level content, but it belongs where the other six ADRs' obligations are already listed (each sibling's step 5/6 "what this leaves to the siblings"), or in the spec's task breakdown, not in the Context of the ADR that decides where six rules are evaluated.

The rest of the structure passes: headings are the canonical set in the canonical order; behaviour (`### The mechanism, end to end`) precedes structure (`### Where the pieces live`) precedes signatures; `Context` opens in plain language rather than naming interfaces; the roles table exists with knowing/doing/deciding stereotypes and rows that are roles; `Where each type is touched` closes with an explicit *Unchanged* list; the forces bullets carry at most one `file:line` each; and the unifying sentence — "**the per-pipeline object carries the DI scope**" — is present verbatim in all seven siblings.

**Recommendation**: keep the FR-25 clause-to-source map (it is the useful part and every row checks out — I verified 0070 step 7 is a per-lifetime table at `:358`, step 7a at `:370`, 0071 step 5 "Behaviour by configured lifetime", 0073 step 5 the three gestures, 0076 step 4 FR-25.11, and 0072's bolded "**Artefact identity, restated for both affinities.**" at `:338`), but move it out of `Context` — into `Implementation Approach` step 7, beside the documentation this ADR already owes — and delete the sentences about implementation-plan ownership.

---

### 8. "`ClaimCheckTransformer` would be warned against in **any** host with `TransformerLifetime = Singleton`" overstates it (Score: 55)

**Evidence**: line 310 —
> "Without the transform half, `ClaimCheckTransformer` (`src/Paramore.Brighter/Transforms/Transformers/ClaimCheckTransformer.cs:62`, taking `IAmAStorageProvider` and `IAmAStorageProviderAsync`) would be warned against in **any host** with `TransformerLifetime = Singleton` and an `AddScoped` storage provider."

The citation is exact (`ClaimCheckTransformer.cs:62` is the two-parameter constructor). But the ADR's own candidate rule is snapshot-based: "**Candidates** come from the snapshot… A descriptor contributes a candidate when its implementation type implements one of the … marker interfaces". `ClaimCheckTransformer` reaches the collection only through `ServiceCollectionTransformerRegistry.Add` (`:56`), which is driven by `TransformsFromAssemblies` (`ServiceCollectionBrighterBuilder.cs:219-233`) — and `AutoFromAssemblies` (`:118-122`) filters out every assembly whose `FullName` starts with `Paramore.Brighter`. So an application that uses `[ClaimCheck]` must register the transform explicitly for it to be a candidate at all; "any host" is not the population.

The same filter bears on AC-42's prefix case: a transform in `Paramore.Brighter.Extensions.Tests` is also excluded from `AutoFromAssemblies` scanning, so that test must register explicitly too — worth a sentence, since the ADR presents AC-42 as the sole falsifier for the prefix half.

**Recommendation**: change "any host" to "any host that has registered it" and add one line noting that a `Paramore.Brighter.*` assembly is not auto-scanned, so both AC-42 transform cases need explicit registration.

---

### 9. The two-kind de-duplication rule has no owner (Score: 50)

**Evidence**: line 302 —
> "A type presenting two kinds is evaluated under each, and **findings are de-duplicated by (artefact type, dependency service type)** so it cannot be reported twice for one dependency."

Nothing in the roles table, the contract tables, or the numbered Implementation Approach carries that responsibility. `SpecificationEvaluator` is the harvest loop lifted verbatim from `PipelineValidator.EvaluateSpecs` (verified at `:152-171`: it appends every failed result with no de-duplication), so the collapse has to happen either when the `ArtefactRegistration` list is built or inside `ScopeConfigurationRules`' FR-22.3 spec — and the ADR says which nowhere. Given the ADR's own care about `ArtefactConstructorSelector` being "its own object" because "D15's rule is a *deciding* responsibility", this is a visible asymmetry.

**Recommendation**: name the owner — either "`ContainerRegistrationSnapshot` yields one `ArtefactRegistration` per (type, kind) and FR-22.3's spec de-duplicates its own findings", or fold it into the snapshot's candidate query.

---

### Verification log

- **Citations checked: 46 — none failed.** Verified by opening the file at the line: `PipelineValidator.cs:54` (class decl), `:69-71` (the `Lazy`), `:85` (`Dispose`), `:92-93` (registry drain), `:139-140` (`.Value` access), `:152` (`EvaluateSpecs`); `BrighterPipelineValidationExtensions.cs:58`, `:64-66`, `:68-69`, `:71`, `:71-94`, `:75`, `:85-88`, `:91-93`; `BrighterValidationHostedService.cs:60`, `:73`, `:80`, `:84`, `:90-93`; `ServiceActivatorHostedService.cs:45-71`, `:50`, `:57`, `:61`, `:67-70`; `PipelineValidationResult.cs:45`, `:52`, `:64`; `BrighterPipelineValidationOptions.cs:47`; `MessageMapperRegistry.cs:360-362`; `ServiceCollectionTransformerResolvabilityProbe.cs:40-56`; `ServiceProviderMapperFactory.cs:44`; `ServiceCollectionExtensions.cs:74`, `:97`; ServiceActivator `ServiceCollectionExtensions.cs:38`, `:88`, `:89-90`, `:60`, `:127`; `ServiceCollectionSubscriberRegistry.cs:63/76/90/116/130/146/160`; `ServiceCollectionMessageMapperRegistryBuilder.cs:80/99/116/117/127/137`; `ServiceCollectionTransformerRegistry.cs:56`; `RequestHandlerAttribute.cs:91` (`public abstract`); `TransformAttributeBase.cs` class `:5` / member `:17`; `PipelineBuilder.cs:151`; `TransformPipelineBuilder.cs:255`, `:270`; `ClaimCheckTransformer.cs:62`; `JustSayingCompressionTransform.cs:34` and `MassTransitTransform.cs:40` (both confirmed parameterless — no declared constructor). Cross-ADR: 0070 step 7/7a, 0071 step 5, 0072 `:338`/`:399`, 0073 `:189`/step 2/step 5, 0076 step 4/`:287`/`:310` — all present and saying what 0074 says they say. Statuses checked: 0053-pipeline-validation-at-startup **Accepted**, 0064-validate-pipeline-assembly-and-provider-registration **Accepted**, 0014 **Accepted**, 0067 **Accepted** (and it does have a `### Terms` block at `:40`), 0054-roslyn-analyzer **Proposed** — all as cited.
- **Counts re-derived**: six rules → **6** (agrees in map row, scope, sequence-diagram note, roles table `×6`, rule table, and 5+1 across the two families); nine DI types + one core type → **9 + 1**; three errors / three warnings → **3/3**; AC-22.3's four identifiers → **4**; FR-25 clauses → **11**; ADR-number collisions → **3 × 0053, 2 × 0054, 2 × 0064** ✓; `IBrighterOptions` self-registering test files → **125** ✓ (exact); AC-22.3 source scan under `src/Paramore.Brighter/` → **0** ✓ (exact); "six DI-package implementation types" on the public surface → **2** ✗ (finding 5).
- **Mermaid blocks rendered: 2/2**, `mmdc` exit 0 for both. `sequenceDiagram` and `flowchart TB` both clean; no `;` in message/note text, no bare `<`/`>` in any label (notably `Lease`/generic names are avoided throughout). The flowchart was rendered to PNG at `-w 1600 -b white` and inspected: readable, one subgraph per assembly, three assemblies, no decision points. *Integrity note*: my first render round collided with a sibling reviewer writing `d1.mmd`/`d2.mmd` into the shared scratchpad and produced ADR 0070's diagram; I re-extracted into an isolated directory and the results above are from that run.
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;'` → **0** ✓. `grep -i chain` → **0 hits** ✓. No reference to authoring-conversation participants, `PROMPT.md`, spec phase, review rounds or commit hashes ✓ (the one "the user's" at line 310 means Brighter's user, which is legitimate).
- **Probes compiled/run: none.** I settled the framework-behaviour claims by reading source rather than compiling: `Specification<T>` does convert an uncaught rule-body exception to `ValidationSeverity.Error` (`Specification.cs:155-200`, both `EvaluateCollapsed` and `EvaluateSimple`) ✓; `PipelineValidationResult.Combine` exists and is unused in `src` (grep) ✓; `ValidationSeverity` is exactly `Error = 0, Warning = 1` ✓; the non-generic marker interfaces the candidate rule needs all exist in core (`IHandleRequests` `:36`, `IHandleRequestsAsync` `:39`, `IAmAMessageMapper` `:32`, `IAmAMessageMapperAsync`, `IAmAMessageTransform` `:36`, `IAmAMessageTransformAsync` `:38`) ✓; nothing in `src` registers `ServiceActivatorHostedService`, confirming D14 ✓; no `InternalsVisibleTo` anywhere in core, which forces finding 4.
- **Blindness**: I read none of the prior review files. The only spec-directory files opened were `requirements.md` and `.adr-list`.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 1 |
| 50-69 | 8 |
| 0-49 | 0 |

Total: 9 · At or above 60: 7
## Reviewer: ADR 0075 — Suppressing ambient scope adoption beneath a `Publish` subscriber

### 1. The async path's explicit restore is not load-bearing: an `async` method is itself an `ExecutionContext` boundary, and `PublishAsync` is one (Score: 82)

The ADR's central mechanical claim about the asynchronous twin is empirically false. `AsyncMethodBuilderCore.Start` saves the thread's `ExecutionContext` before the state machine's first `MoveNext` and restores it in a `finally`. So **no** `AsyncLocal` write made anywhere inside `CommandProcessor.PublishAsync` — bracket 1 inside the (synchronous) `BuildAsync`, or bracket 2 in the start loop — can reach the caller's flow, restored or not. The ADR asserts the opposite three times, and then warns the reader against exactly the error it is making.

**Evidence**: the ADR says, in *Implementation Approach* 5:

> "The conclusion is that the restore must be explicit rather than inherited from `ExecutionContext`, **on both brackets and on both publish paths**. On bracket 1 it is load-bearing: nothing else would restore it."

> "**Bracket 1 is where the caller's flow is genuinely exposed**, because its loop is `observerTypes.Each(observer => …)` — a plain synchronous `foreach` (`Extensions/Each.cs:39-45`) with no `ExecutionContext` boundary of any kind — and it runs on the calling thread throughout."

> "On bracket 2's synchronous half it is defence in depth and symmetry with the async twin, **where the restore *is* load-bearing**…"

> "Stating which is which matters, because a reader who believes the `Parallel.ForEach` restore is what saves the caller has the mechanism backwards and will place the next bracket by the wrong rule."

Verified against source: `CommandProcessor.cs:559` is `public async Task PublishAsync<T>(` — an `async` method; `CommandProcessor.cs:458` is `public void Publish<T>(` — not. `PipelineBuilder.BuildAsync` (`PipelineBuilder.cs:219`) is a *synchronous* method, so it inherits the boundary of whatever `async` method encloses it, which on this path is `PublishAsync`.

Probe compiled and run (`net10.0`, `dotnet 10.0.102`), reproducing the exact `PublishAsync` shape — a sync `BuildAsync`-style plain `foreach` writing the flag, then a start loop writing it, **with no restores anywhere**:

```
1) sync caller, after invoking async callee that wrote true in its sync prefix: V=False
2) sync caller, after fully-synchronous async callee wrote true:               V=False
3) sync caller, after plain sync callee wrote true:                            V=True   <- bracket 1 on the SYNC path
4) caller after a PublishAsync-shaped async method with NO restores at all:     V=False  <- ADR says the restore is load-bearing here
```

Row 3 confirms the claim for the **synchronous** `Publish` (`public void Publish<T>` — no boundary, so bracket 1's restore genuinely is load-bearing there). Row 4 disproves it for `PublishAsync`. The `Each` extension is indeed a plain `foreach` (`src/Paramore.Brighter/Extensions/Each.cs:39-45`, verified), but "with no `ExecutionContext` boundary of any kind" is a property of the *enclosing dispatch method*, not of the loop, and the two twins differ.

The design (explicit restores everywhere) remains correct and harmless. What is wrong is the stated mechanism — and the ADR makes that mechanism load-bearing prose, not an aside.

**Recommendation**: split the claim by twin. State that on the synchronous `Publish` the explicit restores are load-bearing because `Publish` is a plain `void` method and neither `Each` nor the calling thread's `Parallel.ForEach` replica restores anything the caller can observe; and that on `PublishAsync` the runtime restores the caller's `ExecutionContext` at the async method's first suspension (`AsyncMethodBuilderCore.Start`), so the explicit restores there are defence in depth and symmetry — the reverse of what the ADR currently says. Then re-derive the sentences in *The mechanism, end to end* ("The restores are explicit … on both brackets and on both publish paths") and the *Risks* row from the corrected statement.

---

### 2. Alternative 5 rejects the `Task.WhenAll` bracket for a harm that cannot occur, and names an Acceptance Criterion that cannot detect it (Score: 70)

The rejection of the async half of Alternative 5 rests on a consequence that does not happen, and cites AC-12's final clause as its detector. AC-12's final clause reads *after the publish completes* — by which point the runtime has already restored the caller's `ExecutionContext`. The same misattribution is repeated in the *Risks and Mitigations* table, where it is described as the clause that "can actually fail".

**Evidence**: Alternative 5:

> "Around the dispatch it is wrong outright on the async path: a bracket around `Task.WhenAll` (`CommandProcessor.cs:601`) is established *after* every handler's synchronous prefix has already run, and leaves the caller's own flow suppressed for the duration of the publish, **which is what AC-12's final clause detects**."

Risks row 1:

> "Both brackets restore explicitly on every exit path — normal return, exception, cancellation — rather than relying on `ExecutionContext`. **AC-12 and AC-39 each assert a `Send` and a `Post` from the controller after the publish resolving from the request scope; those are the clauses that can actually fail**"

Probe (same project), modelling `PublishAsync` with a single `using (Suppress()) await Task.WhenAll(tasks)` and an async "controller" that reads the flag after awaiting it:

```
controller before publish: V=False
   [inside publish, bracket taken, before await: V=True]
A) controller AFTER PublishAsync (bracket around Task.WhenAll): V=False   <- AC-12 final clause reads here
A') controller after a further await:                          V=False
```

AC-12 verified at `specs/0036-scoped-lifetime-per-pipeline/requirements.md:487-497`; its final clause is "after the publish completes, a `Send` **and** a `Post` issued from the controller outside any subscriber both resolve from `R`'s scope". `CommandProcessor.cs:601` is indeed `await Task.WhenAll(tasks).ConfigureAwait(continueOnCapturedContext);` (verified). AC-39 (`:724-731`) is written over the **synchronous** `Publish` and *can* fail — that citation is sound. AC-12's cannot.

Note the other half of the Alternative 5 rejection — "established *after* every handler's synchronous prefix has already run" — is correct and on its own sufficient, so the alternative stays rejected.

**Recommendation**: drop "and leaves the caller's own flow suppressed for the duration of the publish, which is what AC-12's final clause detects" and rest the rejection on the synchronous-prefix argument alone (which AC-12's *nested `SendAsync`* clause and its resolution-time clause do detect). In the Risks row, restrict "the clauses that can actually fail" to AC-39, and say plainly that AC-12's final clause is a guard against a regression the async runtime already prevents — otherwise an implementor reading the table will believe the async path has a falsifying test it does not have.

---

### 3. The public *mutator* is scope beyond any requirement, and Alternative 3's rejection of the narrower option contradicts the ADR's own contract table (Score: 68)

`NFR-7` is cited eight times to justify the flag being `public`, but it only supports public **read**. The public **write** is justified by a rationale that the ADR contradicts two sections earlier, plus a speculative convenience — and it buys a permanent public mutator on core's surface with no requirement or Acceptance Criterion behind it.

**Evidence**: Alternative 3:

> "Public read alone was considered and is the narrower option, but it makes the type asymmetric — readable by anyone, writable only by Brighter — **for a guarantee that a leaked bracket already breaks from inside**."

But under public-read-only there *are* no third-party brackets to leak, and the ADR says so itself. Contract table, `Suppress()` row: "Both halves are reachable **only from a caller of the public mutator**; Brighter's own brackets are lexical and always disposed innermost-first." *Consequences → Negative*: "**Neither is reachable from Brighter's own code**". So the guarantee the narrower option buys is *not* already broken from inside; it is broken only by the very thing Alternative 3 is arguing for.

Verified against requirements: `NFR-7` (`requirements.md:358`) reads "**Extensibility without ASP.NET.** The seam must **not preclude** a later `AsyncLocal`-based `IAmAScopeProvider` for non-ASP.NET hosts, nor implementations over other containers … **Neither is delivered here** (see Out of Scope)." The ADR escalates this to "NFR-7 **requires** the seam to be implementable over Autofac or SimpleInjector". No AC in the document exercises a third-party container package reading `IsSuppressed`: AC-13's fake records affinities, AC-35's is an ambient *source*. The remaining justification is the speculative one in *Technology Choices*: "a background job started from a request whose `HttpContext` still flows … without waiting for a Brighter release" — which the contract table then names as "the likeliest misuse of a public mutator".

**Recommendation**: either fix Alternative 3's rationale (public write is a convenience for hosts, at the cost of making FR-8 an asserted rather than an undefeatable invariant — which *Consequences* already says honestly) and delete the "already breaks from inside" clause; or take the narrower option, `public` getter with an `internal` setter plus a public `Suppress()` only if a requirement asks for it. Separately, soften "NFR-7 requires" to "NFR-7 requires the design not to preclude", since that is what the requirement says.

---

### 4. Two documentation obligations that ADR 0074 assigns to 0075 are unowned in its Scope and unscheduled in its Implementation Approach (Score: 66)

ADR 0074 names 0075 as the source of two pieces of user-facing documentation. 0075 acknowledges one only in a Consequences bullet and never mentions the other at all — `NFR-9` appears zero times in the whole ADR, including its References list.

**Evidence**: `docs/adr/0074-lifetime-validation-evaluation-site.md:68`:

> "3 — NFR-9's truth table | ADR 0072's adoption ladder supplies the *source* column … **ADR 0075 supplies the `Publish`-subscriber and nested-pipeline rows.**"

and `:70`:

> "5 — `Publish` subscribers, and pipelines nested inside them, cannot join the caller's transaction (C-4) | **ADR 0075**, which owns suppression and its two brackets"

Re-derived counts on `docs/adr/0075-publish-subscriber-scope-suppression.md`: `grep -c "NFR-9"` → **0**. 0075's *Scope* paragraph enumerates what it discharges — "It discharges FR-8, FR-9 and **FR-27.3** … and serves NFR-4" — and neither FR-25.5 nor NFR-9 is in it. FR-25.5 surfaces only in one Negative bullet ("It has to be stated plainly in `docs/guides/lifetimes-and-scoping.md` (FR-25.5)") and in the References list; *Implementation Approach*'s six numbered steps contain no documentation step at all, in an ADR whose siblings (0070 step 7a) schedule theirs explicitly. Requirements verified: `NFR-9` at `requirements.md:361` demands a truth table covering "`Publish` (subscriber, and nested inside a subscriber)"; `C-4` at `:368` is "Discharged by FR-25(5)".

**Recommendation**: add `FR-25.5` and `NFR-9`'s two row-families to the *Scope* paragraph's discharge list, add `NFR-9` to *References*, and add an Implementation Approach step naming the two paragraphs/rows this ADR owes `docs/guides/lifetimes-and-scoping.md`, pointing at 0074 as the page's declaring ADR. Without it, an implementor working from 0075 alone writes no documentation and AC-36 has no owner in this ADR.

---

### 5. The "69 test call sites" count is wrong — 21 of them use the describe-only constructor this ADR explicitly leaves unchanged (Score: 62)

The Consequences bullet that sizes the binary break reports the total number of `PipelineBuilder` constructions in `tests/`, not the number of **dispatch**-constructor call sites, in the same sentence that distinguishes the two.

**Evidence**: *Consequences → Negative*:

> "the two dispatch constructors are called at four sites there (`:317`, `:394`, `:472`, `:575`) and at **69** in `tests/`, every one of which recompiles unchanged."

Re-derived with a brace-aware scan over wrapped call sites:

```
total: 69   single-arg (describe-only ctor): 21   multi-arg (dispatch ctors): 48
```

The 21 are all single-argument calls binding to the describe-only constructor at `PipelineBuilder.cs:92` — e.g. `tests/Paramore.Brighter.Core.Tests/Validation/When_pipeline_builder_describes_handler_should_return_pipeline_description.cs` (six sites), `…/When_the_mapper_registry_is_not_needed_it_is_not_built.cs` (two), `…/When_disposing_validation_components_they_dispose_the_registry.cs` (two). That is precisely the constructor the ADR's *unchanged* paragraph names: "the describe-only `PipelineBuilder` constructor (`:92`) … does not take it". The four `src/` sites and the two validation sites (`BrighterPipelineValidationExtensions.cs:75`, `:116` — both verified as describe-only) are correct.

**Recommendation**: change "69" to "48", or reword to "69 `PipelineBuilder` constructions in `tests/`, of which 48 use a dispatch constructor and 21 the describe-only one".

---

### 6. `Technology Choices` carries six `file:line` citations and one Consequences bullet carries four, against the documented density rule (Score: 60)

`.agent_instructions/documentation.md` puts `file:line` in `Implementation Approach` and the `Where each type is touched` table, and caps forces/Consequences bullets at one apiece. Two sections breach it, and the breaching paragraph in *Technology Choices* is an argument, which is exactly where the standard calls citations "pure noise".

**Evidence**: `.agent_instructions/documentation.md:105-106`: "**Concentrate the citations.** `file:line` references are load-bearing for the implementor and pure noise inside an argument. **At most one per forces or Consequences bullet.**" and `:82`: "`### Implementation Approach` — the implementor's section, and **the only place `file:line` density belongs**."

Counted in 0075: *Technology Choices* → "`PipelineBuilder<TRequest>` is `public` (`PipelineBuilder.cs:37`) and so are all three of its constructors (`:59`, `:76`, `:92`)" (4) plus "which are both `internal` (`IAmAPipelineBuilder.cs:36`, `IAmAnAsyncPipelineBuilder.cs:37`)" (2) = **6**. *Consequences → Negative* → "the two dispatch constructors are called at four sites there (`:317`, `:394`, `:472`, `:575`)" = **4** in one bullet. All eight distinct citations verified correct against source (`PipelineBuilder.cs:37` `public partial class PipelineBuilder<TRequest>`; `:59`/`:76` the two dispatch ctors; `:92` describe-only; both interfaces `internal`). The forces bullets are compliant (one each).

**Recommendation**: move the constructor-visibility and interface-visibility citations into *Implementation Approach* step 2 (where `:59`, `:76`, `:92` already appear) and leave the *Technology Choices* paragraph to make its argument in prose. Reduce the Consequences bullet to "four sites in `CommandProcessor`" and let *Where each type is touched* carry the line numbers, which it already does.

---

### 7. "Two brackets are two places to get wrong" undercounts the maintenance surface it exists to state honestly, and its own sentence enumerates three (Score: 58)

**Evidence**: *Consequences → Negative*:

> "**Two brackets are two places to get wrong**, and they are in different files with different shapes — one inside a lambda in `PipelineBuilder`, one inside a `Parallel.ForEach` body and one inside an async start loop in `CommandProcessor`."

The ADR's own *Where each type is touched* table specifies **four** code sites: "the resolution-time bracket inside **both** build-loop bodies (`:187-198` sync, `:232-244` async)" and "the execution-time bracket around `Handle` … and around the `HandleAsync` **invocation**". All four verified in source (`PipelineBuilder.cs:187-198` and `:232-244` are the two `observerTypes.Each(observer => { … })` lambdas; `CommandProcessor.cs:481` and `:596`). The bullet's own list names three shapes while its headline says two, and the true count is four.

**Recommendation**: "Two brackets, four places to get wrong — the resolution-time bracket in each of `PipelineBuilder`'s two build loops, and the execution-time bracket in each of `CommandProcessor`'s two dispatch paths, all with different shapes."

---

### 8. `CommandProcessor.cs:481` is cited for a bracket that goes around a call on `:489` (Score: 45)

**Evidence**: *Where each type is touched*: "the execution-time bracket around `Handle` inside the `Parallel.ForEach` body (`:481`)". `:481` is `Parallel.ForEach(handlerChain, (handleRequests) =>`; the body runs `:482-497` and `handleRequests.Handle(@event);` is on **`:489`**. The sibling citation for the async twin is exact (`:596` *is* `tasks.Add(handleRequests.HandleAsync(@event, cancellationToken));`), so the asymmetry reads as an oversight. *Implementation Approach* 4 has the same "`(:481)`, around `handleRequests.Handle(@event)`".

**Recommendation**: cite `:481-497` for the body and `:489` for the call, matching the precision of the `:596` citation.

---

### Verification log

- **Citations checked: 23** — all opened against source. Passing: `PipelineBuilder.cs:37` (public partial class), `:59`, `:76` (dispatch ctors), `:92` (describe-only ctor), `:187-198` (sync `Each` lambda), `:232-244` (async `Each` lambda), `:269-270` (`Dispose() => _instanceScopes.Each(...)`); `CommandProcessor.cs:317`, `:394`, `:472`, `:575` (the four builder constructions), `:596` (`HandleAsync` invocation), `:601` (`Task.WhenAll`); `Extensions/Each.cs:39-45` (plain `foreach`); `IAmAPipelineBuilder.cs:36` and `IAmAnAsyncPipelineBuilder.cs:37` (both `internal`); `BrighterPipelineValidationExtensions.cs:75` and `:116` (both describe-only — note the file lives in `Paramore.Brighter.Extensions.DependencyInjection`, not core, which the ADR does not say); `RequestContext.cs:61` (`Bag`). **One imprecise**: `CommandProcessor.cs:481` (finding 8). Requirement ids verified individually against `requirements.md`: FR-5, FR-8 (`:196`), FR-9 and its clauses (a)/(b)/(i)/(ii) (`:199-208`), FR-24.4, FR-25(5) (`:340`), FR-27.1/.2/.3 (`:247-255`), NFR-4 (`:355`), NFR-7 (`:358`), NFR-8, C-2 (`:366`), C-4 (`:368`), C-5 (`:369`), C-16 (`:388`), D0b/D0c/D6/D10/D16 (`:803`, `:804`, `:810`, `:814`, `:820`), OOS-14 (`:405`), AC-10/11/12/13 (`:469-505`), AC-24 (`:671`), AC-36 (`:690`), AC-39 (`:724`), AC-46 (`:780`), AC-47 (`:787`). All say what the ADR says they say **except** AC-12's final clause (finding 2) and NFR-7's "must not preclude" (finding 3). AC-36 is cited in *References* only and never used in the body — noted, not scored.
- **Counts re-derived**: "69 in `tests/`" → **69 total but only 48 dispatch-constructor sites, 21 describe-only** (finding 5). "four sites there (`:317`, `:394`, `:472`, `:575`)" → **4**, correct. "the five container-backed factories" → **5** files present in `src/Paramore.Brighter.Extensions.DependencyInjection/` (`ServiceProviderMapperFactory`, `…MapperFactoryAsync`, `…TransformerFactory`, `…TransformerFactoryAsync`, `…HandlerFactory`), correct. "four ADRs carry that number [0039]" → **4**, correct. "the eight interface signatures of ADRs 0070 and 0071" → matches 0070 step 7a's "Eight interfaces break across the two ADRs, not six", correct. "Two brackets are two places" → **four sites** (finding 7). Sibling-map table: MD5-identical across all seven ADRs 0070–0076 — **not stale**. Unifying sentence ("The rule the first two state is **the per-pipeline object carries the DI scope**") present verbatim in all five siblings that carry it.
- **Mermaid blocks rendered: 2/2** — `d1.mmd` (sequenceDiagram) and `d2.mmd` (flowchart) both exit 0 under `npx -y -p @mermaid-js/mermaid-cli@11 mmdc`, both `.svg` produced. `d1` also rendered to PNG at `-w 1600 -b white` and inspected: readable, correctly nested `loop`/`alt`, notes legible, no swallowed generics. No flowchart exceeds four decision points.
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;'` → **0**. Tone greps also clean: no "chain", no "the user"/"reviewer"/`PROMPT.md`/review-round/commit-hash references.
- **Probes compiled/run** (3 console projects, `net10.0`, SDK 10.0.102, under scratchpad):
  1. `Parallel.ForEach` cross-body leak on one worker — **confirms the ADR**: 1997 of 2000 bodies saw a leaked `true` at entry.
  2. `Parallel.ForEach` caller-flow leak from an unrestored body write — **confirms the ADR**: `False` at N=3 (×5 trials), N=1, `MaxDegreeOfParallelism=1`, and after a body throw, even though 942/2000 bodies ran on the calling thread (the inlined replica *is* EC-restored). A caller pre-set to `true` also survives a body that clears it.
  3. Bracket-around-invocation on the async path — **confirms the ADR**: caller clean after the start loop, all three post-await continuations saw suppression, and a `Task.Run` started inside a handler's synchronous prefix saw it too.
  4. `Each` as a plain `foreach` — **confirms the ADR**: caller left `True` on an unrestored write.
  5. `async`-method `ExecutionContext` boundary (`AsyncMethodBuilderCore.Start`) — **disproves the ADR** (findings 1 and 2): a `PublishAsync`-shaped async method with no restores anywhere leaves the caller `False`; a `using (Suppress()) await Task.WhenAll(...)` inside it also leaves the caller `False`; only the plain-`void` synchronous shape leaks (`True`).

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 2 |
| 50-69 | 5 |
| 0-49 | 1 |

Total: 8 · At or above 60: 6
## Reviewer: ADR 0076 — `docs/adr/0076-scope-affinity-option-and-write-through.md`

### 1. The "TryAddSingleton spelled out" guard is **not** `TryAddSingleton`, and the difference silently un-registers `IBrighterOptions` (Score: 80)

`RegisterBrighterOptions` replaces `TryAddSingleton<IBrighterOptions>` with a hand-rolled guard and the ADR asserts the two are equivalent. They are not. `TryAdd` in Microsoft.Extensions.DependencyInjection matches on **`ServiceType` *and* `ServiceKey`**; the ADR's guard matches on `ServiceType` alone. A host that has registered a *keyed* `IBrighterOptions` (multi-tenant options, a test fixture, any `AddKeyedSingleton<IBrighterOptions>(...)`) works today and stops working after this change — `RegisterBrighterOptions` returns having added **no descriptor at all**, and the first `GetRequiredService<IBrighterOptions>()` inside `BrighterHandlerBuilder` throws at startup. There is also a false-positive knock-on into the sibling: with no descriptor and no `BrighterOptionsRegistration`, ADR 0074's FR-22.4 rule reports an `Error` against an application that did nothing wrong.

**Evidence**: the ADR's own comment and code —

> `//TryAddSingleton spelled out, because the descriptor we add has to be one we can hand on:`
> `if (services.Any(d => d.ServiceType == typeof(IBrighterOptions)))`
> `    return;`

and ADR 0074 repeats the equivalence claim at `docs/adr/0074-lifetime-validation-evaluation-site.md:264` — *"the **effect** is `TryAddSingleton`'s"*.

Probe compiled and run against `Microsoft.Extensions.DependencyInjection` **10.0.10** (the pinned version — `Directory.Packages.props:89`), scratchpad project `probe0076`:

```
C today (TryAdd): resolves = brighter
C after ADR: descriptors added = 0; resolves = NULL
P1b GetRequiredService THREW InvalidOperationException
```

i.e. with `AddKeyedSingleton<IBrighterOptions>("t", …)` present, today's `TryAddSingleton` **does** register Brighter's descriptor and resolution succeeds; the ADR's guard adds nothing and resolution fails. Verified there is no other divergence: the four in-repo `TryAddSingleton<IBrighterOptions>` sites are the only ones (`grep -rn "TryAddSingleton<IBrighterOptions>" src/` → `Extensions.DependencyInjection/ServiceCollectionExtensions.cs:74,:97`; `ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:38,:88`).

**Recommendation**: make the guard `services.Any(d => d.ServiceType == typeof(IBrighterOptions) && d.ServiceKey is null)`, and say in the ADR *why* the key clause is there — otherwise a maintainer will "simplify" it back. Alternatively call `TryAdd(descriptor)` and then locate the descriptor by reference in the collection (`Contains`) to decide whether to record it; that keeps the semantics identical by construction. Either way, delete the unqualified claim that the effect is `TryAddSingleton`'s, here and at 0074:264.

---

### 2. The obvious alternative — apply the write-through in `BrighterHandlerBuilder`, the single funnel all four paths already call — is never considered, and a sibling ADR uses exactly that funnel for the same job (Score: 74)

The ADR lists **seven** alternatives and rewrites **four** call sites, then carries a risk it created: *"A fifth registration path is added later and does not apply the override, so the opt-in fails silently on it."* Its own Context states the fact that dissolves that risk — *"All four route through `ServiceCollectionExtensions.BrighterHandlerBuilder`, which registers `IAmACommandProcessor`, so each alone is a complete Brighter host."* A design that registers `IBrighterOptions` **inside** `BrighterHandlerBuilder` has one call site, not four, and a "fifth path" cannot exist without calling it, because calling it is what makes something a Brighter host. This alternative is not mentioned, let alone rejected.

**Evidence**: `BrighterHandlerBuilder` already **takes the per-path options factory and never uses it**. `grep -n "optionsFunc" src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` returns exactly two hits — the doc comment at `:140` and the parameter declaration at `:144`. The parameter is dead; the body (`:145-224`) resolves `IBrighterOptions` from the provider throughout (`:160`, `:168`). Three of the four callers already hand it precisely the func `RegisterBrighterOptions` wants: `:98-100` passes `configure`; `:121-123` passes `_ => options` (the SA `Action` path via `:64`); SA `:131-133` passes `sp => configure(sp)`. Only `AddBrighter(Action)` at `:77-79` passes a circular lambda, and that site is being rewritten by this ADR anyway. I verified there are no other callers: `grep -rn "BrighterHandlerBuilder(" src/ tests/ samples/` → only `:77`, `:98`, `:121`, SA `:64`, SA `:131`.

Sibling 0072 picks that same funnel for its own registrations and says so: `docs/adr/0072-ambient-scope-adoption-seam.md:424` — *"All four registration entry points route through `ServiceCollectionExtensions.BrighterHandlerBuilder` (`:142`; the `BrighterOptions` overload at `:119` forwards to it), so that is the single place `ScopedArtefactCache` … and `AmbientScopeDiagnostics` … are registered."* Two ADRs in the same set solve "register one thing on all four paths" two different ways, and neither reconciles the split.

**Recommendation**: add it as alternative 8 with a real rejection (e.g. `BrighterHandlerBuilder` is `public` and directly callable, so moving the `IBrighterOptions` registration into it changes its published contract — note that against the fact that it has no callers outside these four). If the argument does not hold, adopt it: the risk-table row and the "four sites move in one commit" instruction both go away.

---

### 3. `ArgumentNullException.ThrowIfNull` does not exist on `netstandard2.0`, which is one of the four TFMs this package builds (Score: 70)

The `RegisterBrighterOptions` sample opens with two `ArgumentNullException.ThrowIfNull` calls. That API is .NET 6+. `Paramore.Brighter.Extensions.DependencyInjection` targets `netstandard2.0` — a fact the ADR itself relies on two sections earlier — so the sample does not compile on one of the four TFMs, and `TreatWarningsAsErrors` is on repo-wide.

**Evidence**: ADR text — *"Adding a member to `IBrighterOptions` is a source and binary break … and `netstandard2.0` has no default interface member to absorb it."* `src/Paramore.Brighter.Extensions.DependencyInjection/Paramore.Brighter.Extensions.DependencyInjection.csproj:5` → `<TargetFrameworks>$(BrighterTargetFrameworks)</TargetFrameworks>`; `src/Directory.Build.props:43` → `netstandard2.0;net8.0;net9.0;net10.0`. `src/Directory.Build.props:18` → `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. There is no polyfill package (`grep -n "PolySharp\|Polyfill" Directory.Packages.props src/Directory.Build.props` → no hits); the repo's own convention is an `#if NET` guard — `src/Paramore.Brighter/Tasks/BrighterTaskScheduler.cs:35-39`:

```csharp
#if NET
        ArgumentNullException.ThrowIfNull(asyncContext);
#else
        if (asyncContext is null) throw new ArgumentNullException(nameof(asyncContext));
#endif
```

The four sites this method replaces all use the plain form (`ServiceCollectionExtensions.cs:65-66`, `:92-95`; SA `:33-34`, `:82-85`).

**Recommendation**: write the sample as `if (services is null) throw new ArgumentNullException(nameof(services));`, matching the four call sites it is replacing.

---

### 4. 0076 owns `ScopeAffinityOverride` but states neither registration obligation the mechanism depends on — and NFR-7 makes third-party packages first-class users (Score: 66)

Two properties of how a `ScopeAffinityOverride` is registered are load-bearing, and both are stated only in ADR 0073, the *consumer* of the type, never in the ADR that defines it, its contract table, or the XML doc it specifies:

1. **Plain `AddSingleton`, never `TryAddSingleton`** — otherwise the first call wins the affinity while the provider's plain `AddSingleton` gives the last call's provider, and FR-17's repeat rule has nothing to read.
2. **An *instance* registration, not a factory** — FR-17's repeat rule reads affinity **values** off descriptors without resolving; a `sp => new ScopeAffinityOverride(a)` descriptor carries no `ImplementationInstance` and is unreadable.

The ADR asserts both outcomes while mandating neither: *"one immutable value, last registered wins"* (diagram only), *"Both halves resolve to the last call"*, and *"the `ScopeAffinityOverride` descriptors as they stand in the collection … two differing affinity **values** are visible in the collection"*. Its Positive consequence explicitly invites a package Brighter does not ship to do this — *"an `AsyncLocal`-backed provider package for console hosts registers its provider and its override in exactly the same two lines (NFR-7)"* — but that package's author reads 0076 (or the XML doc), not 0073.

**Evidence**: 0076's `ScopeAffinityOverride` contract table rows are `Affinity` and *(the type as a service)*; neither names a registration form. Its XML doc summary says only *"Registering one of these is how a package that knows nothing about Brighter's registration paths sets the default affinity."* The obligations exist and are stated elsewhere: `docs/adr/0073-aspnet-core-request-scope-package.md:206` (*"`ScopeAffinityOverride` is registered with plain `AddSingleton` … A `TryAddSingleton` here would satisfy neither"*), `:189` (`services.AddSingleton(new ScopeAffinityOverride(affinity));`), and `docs/adr/0074-lifetime-validation-evaluation-site.md:197` (*"where the descriptor supplies an `ImplementationInstance` the runtime value is carried too (which is how FR-17 reads the affinities)"*). Requirements confirm both are mandatory — `requirements.md:275` (FR-17): *"whatever mechanism carries the affinity must leave **every** call's descriptor in the collection"* and *"Where the collection holds affinity-carrying descriptors with **more than one distinct affinity value**"*.

**Recommendation**: add a row to `ScopeAffinityOverride`'s contract table — *"registration form: plain `AddSingleton` of a constructed **instance**; never `TryAdd*`, never a factory delegate"* — with the two reasons, and put the same sentence in the specified XML doc. That is the only place NFR-7's third party will see it.

---

### 5. The ServiceActivator DI extension class is named `ServiceActivatorServiceCollectionExtensions`, not `ServiceCollectionExtensions` (Score: 62)

The *Where each type is touched* table — the implementor-facing table — names the wrong type in the ServiceActivator assembly. The file is `ServiceCollectionExtensions.cs`, but the class inside it is `ServiceActivatorServiceCollectionExtensions`. The `Paramore.Brighter.Extensions.DependencyInjection` assembly has a *different* class also called `ServiceCollectionExtensions`, so the table as written reads as though one class spans both assemblies — which is exactly the confusion the two-assembly `RegisterBrighterOptions` story cannot afford.

**Evidence**: ADR row — `| Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection | ServiceCollectionExtensions (:38, :88) | the same. :38 is the one pre-built instance registration…`. Source: `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:17` → `public static class  ServiceActivatorServiceCollectionExtensions`. Every *line* citation in the ADR is correct (I opened all of them — `:29`, `:36`, `:37`, `:38`, `:39`, `:45`, `:64`, `:78`, `:88`, `:89-90`, `:131-133`); only the type name is wrong.

**Recommendation**: change the Type cell to `ServiceActivatorServiceCollectionExtensions`, and disambiguate the several prose references that say "the ServiceActivator DI package's `ServiceCollectionExtensions`".

---

### 6. The contract table promises an exception message the code sample cannot produce (Score: 60)

The `RegisterBrighterOptions` contract row states the null-return guard raises *"`InvalidOperationException` **naming the calling registration path**"*. The sample throws a constant string with no path in it, and the method has no parameter from which a path could be derived — `optionsFunc` is an anonymous delegate. Two developers implementing from this ADR will produce two different things: one adds a `[CallerMemberName]` or a `string callerName` parameter (changing the public signature the same ADR fixes), the other ships the literal.

**Evidence**: contract row — *"A `null` **return** from `optionsFunc` raises `InvalidOperationException` naming the calling registration path"*; sample — `?? throw new InvalidOperationException("The Brighter options factory returned null.");`. I verified the framework baseline the row claims to restore: probe on MS DI 10.0.10, a singleton factory returning null gives `GetService` → `null` and `GetRequiredService` → `InvalidOperationException: No service for type 'IFoo' has been registered.` So the *type* is preserved but no existing message names a path either — the row is inventing a requirement nothing needs.

**Recommendation**: either drop "naming the calling registration path" from the contract row, or add the parameter that makes it possible (`string registrationPath` / `[CallerMemberName]`) and show it in the sample and the signature.

---

### 7. 0073 says this mechanism has "two callers"; 0076 names one, and there is no second (Score: 60)

A countable fact about 0076's own mechanism is stated differently in the two ADRs.

**Evidence**: `docs/adr/0073-aspnet-core-request-scope-package.md:36` — *"It does not decide the opt-in property, the override that carries this extension's argument, or how either reaches the four registration paths — that is ADR 0076, and this extension is **one of its two callers**."* 0076 says the opposite everywhere: *"ADR 0073's ASP.NET extension is the **first** caller"* (Context and forces), diagram label *"an opt-in package — ADR 0073 ships the first"*, and — for the *other* mechanism, `RegisterBrighterOptions` — *"the **four** existing sites are its only callers"*. Neither "two" nor any reading of it matches: one opt-in caller, four registration callers.

**Recommendation**: fix 0073 to *"this extension is its first caller"*. If "two" was meant to count the DI package and the ServiceActivator DI package as callers of `RegisterBrighterOptions`, say so — but that count is four, not two.

---

### 8. `NFR-4` is cited for a guarantee it does not make (Score: 55 — below threshold, recorded for completeness)

*Thread safety* closes with *"the write to `DefaultScopeAffinity` happens exactly once and completes before any caller holds the reference the `IBrighterOptions` factory returns (**NFR-4**)"*, and Scope lists NFR-4 among the requirements served. NFR-4 (`requirements.md:355`) is about *"Beginning and releasing pipeline scopes, and establishing and clearing ambient suppression … under concurrent pipelines"* and `Parallel.ForEach`/`ExecutionContext` — it says nothing about options-object construction. The underlying framework claim is sound (MS DI builds a singleton once under its own lock), but the citation does not support it.

**Recommendation**: keep the paragraph, drop the NFR-4 citation or replace it with a plain statement of the MS DI guarantee.

---

### Verification log

- **Citations checked: 28 — 0 line-number failures, 1 type-name failure.**
  - `ServiceProviderMapperFactory.cs:44` ✓ (`GetService(typeof(IBrighterOptions))`).
  - `Extensions.DependencyInjection/ServiceCollectionExtensions.cs` `:61` ✓ `AddBrighter(Action)`, `:69` ✓ `AddOptions<BrighterOptions>()`, `:71` ✓ `Configure(configure)`, `:74` ✓ `TryAddSingleton<IBrighterOptions>`, `:77-79` ✓, `:88` ✓ `AddBrighter(Func)`, `:97` ✓, `:98-100` ✓, `:119`/`:142` ✓ both `BrighterHandlerBuilder` declarations.
  - `ServiceActivator…/ServiceCollectionExtensions.cs` `:29` ✓, `:36` ✓, `:37` ✓, `:38` ✓, `:39` ✓, `:45` ✓ (`options.InboxConfiguration` read at registration), `:64` ✓, `:78` ✓, `:88` ✓, `:89-90` ✓ (the `InvalidCastException` cast), `:131-133` ✓. **Type name wrong** (finding 5).
  - `ConsumersOptions.cs:10` ✓ (declaration is `: BrighterOptions, IAmConsumerOptions` — the ADR quotes only the base).
  - `BrighterOptions.cs:9` ✓, `:37` ✓ `IsolateTransientHandlerScope`.
  - `src/Paramore.Brighter/IAmConsumerOptions.cs:7` ✓.
  - Requirement ids: FR-8, FR-14, FR-15, FR-17, FR-19, FR-21, FR-22.4, FR-24.3, FR-25.10, FR-25.11, FR-27.2, NFR-1, NFR-2, NFR-4, NFR-7, NFR-8, C-9, C-10, C-12, C-12a, C-15, C-18, D13, D18, AC-20, AC-22.3, AC-24, AC-45, AC-48, AC-50 — **all exist**. AC-48's quoted clause matches verbatim; AC-45's "non-default starting value" clause matches; AC-50's both-orderings and identical-values branches match. **NFR-4 does not support its claim** (finding 8).
- **Counts re-derived:**
  - "four" registration sites → **4** ✓ (`grep -rn "TryAddSingleton<IBrighterOptions>" src/`).
  - "five container-backed factories" → **5** ✓ (`ServiceProviderMapperFactory:44`, `MapperFactoryAsync:45`, `TransformerFactory:44`, `TransformerFactoryAsync:45`, `HandlerFactory:49`).
  - "exactly one implementation of `IBrighterOptions` in `src/`, none in `tests/`" → **1 / 0** ✓ (multi-line-aware scan for `(class|record|struct) X : … IBrighterOptions` over `src/` and `tests/` → only `BrighterOptions.cs:9`).
  - "125 files under `tests/` register `IBrighterOptions` themselves" → **125** ✓ exactly.
  - `IAmConsumerOptions` "five members" → **5** ✓.
  - "no consumer of `IAmConsumerOptions` in `src` or `tests` downcasts" → ✓ verified; the five consumers (`ServiceCollectionExtensions.cs:641`, `BrighterPipelineValidationExtensions.cs:146`, `:157`, `AsyncApiBrighterBuilderExtensions.cs:84`, SA `:143`) read only `Subscriptions`, `InboxConfiguration`, `DefaultChannelFactory`.
  - "FR-22's four rules + FR-24.3 + FR-17" = six → matches 0074's "six scope-configuration rules" ✓.
  - Sibling map: all seven ADRs carry the identical 7-row table and the identical unifying sentence *"the per-pipeline object carries the DI scope"* ✓. Heading order/nesting matches `.agent_instructions/documentation.md` § *ADR structure* exactly ✓.
- **Mermaid blocks rendered: 3/3** — `mmdc` (@mermaid-js/mermaid-cli@11) exit 0 for all three, `.svg` produced for each. The `flowchart LR` (most complex) also rendered to PNG at `-w 1600 -b white` and inspected: readable, five nodes plus one dotted read-edge, no overlap. No semicolons in the `sequenceDiagram`, no bare `<`/`>` in any label.
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;'` → **0** ✓.
- **Tone**: no reference to conversation participants, `PROMPT.md`, review rounds or commit hashes. No use of "chain". ✓
- **Probes compiled/run** (two console apps, net10.0, `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Options` **10.0.10**, matching `Directory.Packages.props:89,101`):
  - `TryAddSingleton<T>` vs the ADR's `Any(ServiceType==T)` guard with a keyed descriptor present → **divergent**; ADR guard registers nothing, `GetRequiredService` throws (finding 1).
  - Extension-**before** and extension-**after** orderings through a faithful re-implementation of `RegisterBrighterOptions` → both yield `JoinAmbient` on the resolved `IBrighterOptions`. **The core order-independence claim holds.**
  - `Func<IServiceProvider, ConsumersOptions>` is accepted where `Func<IServiceProvider, BrighterOptions>` is expected (delegate return covariance) → the `AddConsumers(Func)` site compiles as written ✓.
  - Last-descriptor-wins → ✓; a plain `AddSingleton` placed **after** Brighter's `TryAddSingleton` wins resolution **and Brighter's factory never runs** → ✓, exactly as the ADR's defeat analysis states.
  - `IOptions<T>.Value`, `IOptionsSnapshot<T>.Value` and `IOptionsMonitor<T>.CurrentValue` → **three distinct instances** ✓, confirming the ADR's Negative consequence about `IOptionsSnapshot`/`IOptionsMonitor` never seeing the extension's value.
  - Factory-registered singleton disposed on provider dispose = `True`; instance-registered = `False` → ✓ confirms the ADR's instance-to-factory disposal claim for the SA `Action` path.
  - Singleton factory returning `null`: `GetService` → `null`, `GetRequiredService` → `InvalidOperationException` → ✓ confirms "MS DI raises its own error", and grounds finding 6.
  - `(ScopeAffinity)99 == JoinAmbient` → `False` → ✓ the positive-test fail-safe works; 0072 does state the reciprocal obligation (`0072…:262`), so the ADR's cross-reference is honest.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 3 |
| 50-69 | 5 |
| 0-49 | 0 |

Total: 8 · At or above 60: 7
# Reviewer: the set (all seven ADRs, set-level properties)

### 1. ADR 0071 asserts three times that it discharges no requirement, and its own `Scope` says it discharges one (Score: 85)

The positional sentence under 0071's sibling map is a **set-level comparative claim**, and it contradicts 0071's own `Scope` paragraph and the FR-13 partition all three participating ADRs state.

**Evidence**:
- `docs/adr/0071-pipeline-scope-handle-for-handler-pipelines.md:30` — "It discharges **FR-13's disposal-failure clause for the handler family** — a `PipelineScope` disposal that throws … is logged at `LogLevel.Error` and swallowed … (step 2, AC-33). The transform-pipeline instance of the same clause is ADR 0070's, and FR-13's other clause … is ADR 0072's; **the requirement is split three ways and no ADR claims the whole of it.**"
- `:38` — "This is the second, and **the only one that discharges no requirement of its own.**"
- `:346` — "**This ADR discharges no requirement of its own, and it is still not free.**"

The other two ADRs holding shares of FR-13 both name 0071 as an owner: `0070:32` ("The **handler-family** instance of the same clause, which is what AC-33 exercises, is ADR 0071's") and `0072:31` ("that requirement's disposal-failure clause is ADR 0070's for transform pipelines and **ADR 0071's for handler pipelines, where AC-33 guards it**"). I verified AC-33 in `specs/0036-scoped-lifetime-per-pipeline/requirements.md` — it is written over a `Send` with a handler whose `Scoped` dependency's `Dispose()` throws, i.e. the handler family. So the set assigns a third of FR-13 to 0071 from three directions, and 0071 denies owning it in two places. A reader auditing coverage gets opposite answers depending on which paragraph they read.

**Recommendation**: pick one. Either 0071 discharges FR-13's handler clause — in which case `:38` becomes "the only one that decides no new mechanism" or similar, and the `:346` bullet is rewritten — or it does not, in which case 0070's and 0072's hand-offs must be re-pointed and FR-13's partition becomes two-way.

---

### 2. The release-note ledger states that nothing in the repository implements `IBrighterOptions`; one class does, and ADR 0076 says both things in one sentence (Score: 72)

**Evidence**:
- `0070…:378` (the ledger, step 7a) — "**Source and binary, ADR 0076.** `IBrighterOptions` gains `DefaultScopeAffinity`, which breaks a hand-rolled implementation; **nothing in this repository implements it.**"
- `0076…:366` — "**Nothing in this repository implements it — one implementation in `src/`, none in `tests/`** — but 'we could not find one' is not 'there is none'…"
- `0076…:318` (the touched table) has the correct formulation: "One implementation repo-wide, none in `tests/`, so nothing in this repository **breaks**."

Verified against source: `grep -rnE '(class|record|struct)\s+\w+\s*:\s*[^{]*IBrighterOptions' src/ tests/` returns exactly one hit, `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterOptions.cs:9: public class BrighterOptions : IBrighterOptions`, and zero under `tests/`. So 0076's count is right and its own prose bullet contradicts itself in the same clause; the ledger has propagated the wrong half. The distinction is load-bearing — "nothing implements it" would make the interface break costless in-repo, whereas one implementation must be edited in the same commit.

**Recommendation**: change both to "one implementation in `src/`, edited in the same change; nothing in this repository *breaks*."

---

### 3. ADR 0072 is the only ADR in the set with no entry in the release-note ledger and no break statement, while changing shipped behaviour (Score: 70)

The ledger declares itself exhaustive: `0070…:370` — "The upgrade breaks belong in **one** entry … This ADR is where the first three originate; the rest arrive with its siblings and **are enumerated here so none is left to be noticed on its own** … each sibling states its own break in its own *Consequences* and points back here rather than opening a second entry."

**Evidence**: `grep -n 'step 7a\|release_notes' docs/adr/007[0-6]*.md` returns hits in 0070, 0071, 0074, 0075 and 0076. **0072 has none** — no `release_notes.md`, no "step 7a", no "break". The ledger's nine bullets attribute entries to 0070 (×3), 0071 (×2), 0074 (×2), 0075 (×1) and 0076 (×1); 0072 and 0073 are absent. 0073's absence is correct (a new package breaks nothing). 0072's is not: it changes behaviour that ships today.

`0072…:420` (step 3a): "`ServiceProviderLifetimeScope.cs:152`'s private `_scopedInstances` field becomes a resolution of this service **and inherits the same rule**, so the owned and borrowed paths keep one protocol between them." The rule is that "a factory that throws does not leave a faulted entry behind." `0072…:446` states the current behaviour plainly: "`GetOrCreateScoped` and `GetOrCreateSingleton` cache a `Lazy<object?>` in default mode, which caches a **faulted** `GetService`." Verified at `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:167` (`_scopedInstances.GetOrAdd(objectType, _ => new Lazy<object?>(…))`) and `:154` for the singleton twin — both default-mode `Lazy`. So on the **owned** path, in a host that never opts in and never registers a provider, a resolution that faulted once will now be retried where today it rethrows the remembered fault. That is behavioural, silent, and has no compile error to warn it — precisely the ledger's own first-bullet category.

**Recommendation**: add a "Behavioural, ADR 0072" bullet to step 7a covering the #4260 faulted-`Lazy` eviction on the owned path, and give 0072 the reciprocal *Consequences* sentence pointing back at the ledger that every other break-bearing sibling carries.

---

### 4. ADR 0072 gives two different line numbers for `_scopedInstances`, the one field its central change moves; `:152` is wrong (Score: 68)

**Evidence**: 0072 cites the field three ways.
- `0072…:268` — "A cache that is a private field of the handle (`ServiceProviderLifetimeScope.cs:49`)"
- `0072…:319` (touched table) — "the `Scoped` path resolves its artefact cache from the scope in play rather than owning `_scopedInstances` (`:152`)"
- `0072…:420` (step 3a) — "`ServiceProviderLifetimeScope.cs:152`'s private `_scopedInstances` field becomes a resolution of this service"

Opened the source. `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:49` is `private readonly ConcurrentDictionary<Type, Lazy<object?>> _scopedInstances = new();` — correct. Line 152 is `private T? GetOrCreateSingleton<T>(Type objectType) where T : class` — a different member, governing the **singleton** cache, not the scoped one. `_scopedInstances` occurs at exactly three places in that file: `:49`, `:167`, `:500`; `:152` is not one of them. The `:163-178` range 0070 and 0072 both use for the `Lazy` publish protocol *is* correct (`GetOrCreateScoped` opens at `:163`, the `GetOrAdd` is at `:167`).

This matters more than a stale citation usually would, because `:152` names the singleton accessor, and 0072's Negative bullet at `:446` says the fix applies to `GetOrCreateScoped` **and** `GetOrCreateSingleton`. An implementer following `:152` would edit the singleton path believing it to be the scoped one.

**Recommendation**: replace both `:152` citations with `:49`, and state separately that `GetOrCreateSingleton` (`:152`) takes the same eviction rule.

---

### 5. Eight requirements are discharged by no ADR, including the one the specification was raised for (Score: 68)

I swept every id in `requirements.md` (27 FR, 10 NFR, 50 AC, 21 C, 22 D, 14 OOS) and matched it against every ADR's `Scope` discharge/serve claims. The set makes this distinction load-bearing itself — `0076…:34`: "**FR-19 and FR-21 are served here, not discharged here**, and the distinction is worth making because **a reader auditing coverage should land on the mechanism**" — so a requirement carrying only "serves" claims has no owner by the set's own rule.

**Evidence — the complete discharge map**, from the seven `Scope` paragraphs:

| ADR | Discharges |
|---|---|
| 0070 | FR-1…FR-7, C-19, FR-13 (transform disposal clause), C-8 (disposal half) |
| 0071 | FR-13 (handler disposal clause) — *and denies it, see finding 1* |
| 0072 | FR-10, FR-11, FR-12, FR-13 (ownership clause), FR-21, FR-24, FR-27.1, FR-27.2 |
| 0073 | FR-15 (package-inertness half), FR-17 (registration half) |
| 0074 | FR-22, FR-24.3 (evaluation half), FR-17 (evaluation half), NFR-9 |
| 0075 | FR-8, FR-9, FR-27.3 |
| 0076 | FR-14, FR-17 (write-through half) |

**Discharged by nobody**: **FR-15's normative clause, FR-16, FR-16a, FR-18, FR-19, FR-20, FR-23, FR-26** — plus **NFR-1 through NFR-8 and NFR-10**, and every constraint except C-19 and half of C-8. FR-25 is also undischarged but is *stated* as such (`0074…:60-62`: "an implementation-plan deliverable … It is not an ADR-level decision"), so it is not a silent gap.

Two are worse than merely unclaimed:
- **FR-19** — `0076…:34` disclaims it explicitly and names the deliverer: "delivered by the pump publishing no per-message ambient (D0b, C-2, **ADR 0072**)". `grep -n 'FR-19' 0072*.md` returns exactly two hits: ladder row 3's diagnostic column (`:93`) and the References list (`:484`). 0072's `Scope` neither discharges nor serves it. The hand-off is to an ADR that never receives it.
- **FR-16** is the headline: "Pipelines in one HTTP request share the request scope when opted in" — the case `0072…:468`, `0073…:316` and `0076…:388` each call "the reason the specification was raised". Three ADRs *serve* it; none owns it.

**Recommendation**: either assign each of the eight explicitly (FR-16/FR-16a and FR-26 read as 0072's; FR-18 and FR-23 as 0072's with 0073 serving; FR-19 as 0072's; FR-20 as 0070's step 7a), or add a one-line note to the set stating that NFRs and constraints are met by construction rather than discharged, so a coverage audit terminates.

---

### 6. FR-15's three-way split is declared by ADR 0073 alone; neither ADR it names picks up its share (Score: 66)

The set splits FR-13 and FR-17 three ways, and in both cases **every** participant names the other two — I diffed all six statements and they agree. FR-15 is split the same way and only one end says so.

**Evidence**: `0073…:34` — "It discharges **FR-15's package-inertness half** … **FR-15's normative clause, the affinity option's default value, is ADR 0076's**, and the 'no pipeline adopts' behaviour that follows from that default is **ADR 0072's ladder**."

- 0076's `Scope` (`:32`) discharges "FR-14 and **the write-through half of FR-17**" and serves "FR-16, FR-18, FR-19, FR-20, FR-21, FR-22, FR-23, FR-25.11, NFR-1, NFR-4 and NFR-7". **FR-15 appears in neither list.** It appears only in the body (`:241`, `:319`, `:337`, `:359`) and the References line.
- `grep -n 'FR-15' 0072*.md` returns **zero hits**. The ADR named as delivering the second half of the split never mentions the requirement at all.

Compare the FR-17 split, which is stated identically from all three ends (`0073:34`, `0074:32`, `0076:32`). FR-15's is stated from one.

**Recommendation**: add the reciprocal clause to 0076's `Scope` ("It discharges FR-14 and **FR-15's normative clause** — the affinity option's default value — and the write-through half of FR-17"), and either drop the 0072 leg or have 0072 acknowledge it.

---

### 7. The suppression read is specified circularly: 0075 says ADR 0072 specifies it; 0072 never names the flag (Score: 62)

**Evidence**:
- `0075…:220` (*Where each type is touched*) — "`…DependencyInjection` | the five container-backed factories | one read of `IsSuppressed` at the affinity computation, **specified by ADR 0072**"
- `0075…:259` — "ADR 0072's `CreatePipelineScope()` protocol reads `IsSuppressed` once, at the line that computes the pipeline's affinity"
- `0072…:33` — "It does not decide **how a `Publish` subscriber suppresses adoption** … that is ADR 0075, **which owns the flag, both brackets and the reasoning**"

`grep 'AmbientScopeSuppression\|IsSuppressed' docs/adr/007[0-6]*.md` matches **0075 only**. 0072's protocol shows an unattributed pseudo-code line — "`3. affinity = suppressed on this flow [ADR 0075]`" (`:363`) — and its five-factory row in *Where each type is touched* (`:320`) enumerates exactly what each factory keeps: "a `ScopeAffinityPolicy`, the resolved `IAmAScopeProvider` and the diagnostics singleton". No suppression read. 0072 is the ADR whose *Unchanged, and named so the omission is not read as an oversight* convention is heaviest in the set, so the absence reads as deliberate rather than pending.

Concretely: a reader of 0072's touched table alone would not know the five container-backed factories take a dependency on a core static, and a reader of 0075's would go to 0072 for the specification and not find it.

**Recommendation**: add `AmbientScopeSuppression.IsSuppressed` to 0072's five-factory row and to its `Where the pieces live` diagram, or change 0075's "specified by ADR 0072" to "specified here; read at the line ADR 0072's protocol calls step 3".

---

### 8. ADR 0071 says "Nothing else in `PipelineBuilder` changes", makes an exception for 0072's edit, and omits 0075's (Score: 62)

Three ADRs edit `PipelineBuilder<TRequest>`. 0071 accounts for one sibling and not the other.

**Evidence**:
- `0071…:255` (touched table) — "`PipelineBuilder<TRequest>` | `GetSyncInstanceScope()` (`:567`) and `GetAsyncInstanceScope()` (`:578`) ask the factory and pass the result to the `HandlerLifetimeScope` constructor. **Nothing else**"; and `:306` — "**Nothing else in `PipelineBuilder` changes.**" Its *Unchanged* list at `:262` names `PipelineBuilder.Dispose()`.
- 0071 *does* flag 0072's edit: `:207` — "0072 widens it and amends both `PipelineBuilder` `catch` filters (`:202-204`, `:248-250`)".
- 0075 edits the same class harder: `:218` — "a defaulted constructor argument `bool isolateSubscribers = false` on the two dispatch constructors (`:59`, `:76`), **and the resolution-time bracket inside both build-loop bodies** (`:187-198` sync, `:232-244` async)". 0071 nowhere mentions this.

Verified all cited lines against `src/Paramore.Brighter/PipelineBuilder.cs`: `:59`, `:76`, `:92` are the three public constructors; `:187`/`:232` are the `observerTypes.Each(observer =>` loops; `:202` is `catch (Exception e) when (e is not ConfigurationException)` and `:248` is `catch (Exception e) when(!(e is ConfigurationException))` — including the spelling asymmetry 0072 step 1a calls out, which is exactly right; `:269-270` is `Dispose()`; `:567`/`:578` are the instance-scope helpers. Every citation is accurate; only the completeness claim is not.

**Recommendation**: qualify 0071's "Nothing else" to "nothing else *in this ADR*", and extend its existing 0072 aside to name 0075's constructor parameter and build-loop brackets, so the class reads the same from both ends.

---

### 9. FR-13 is asserted to be "split three ways" with nothing left over, but its principal clause is unassigned (Score: 60)

All three ADRs make the completeness claim in identical words — "the requirement is split three ways and no ADR claims the whole of it" (`0070:32`, `0071:30`) — and 0072 restates the same partition (`:31`). The partition names: transform disposal-failure (0070), handler disposal-failure (0071), ownership (0072).

**Evidence**: FR-13 (`requirements.md:229-231`) has a lead normative clause that is none of those three: "**FR-13 — Brighter disposes every scope it created.** When a pipeline creates its own pipeline scope … Brighter must release that scope when the pipeline completes, subject to FR-9's release-timing clause for `Publish` subscribers, **and must therefore dispose the container-`Scoped` instances resolved from it**." The disposal-failure rule is a *second* paragraph beginning "**Disposal failure on a pipeline that succeeded.**"

The work is done — `0070…:345` (step 5) and `0071…:280` (step 2) — but both cite **FR-6** for it, not FR-13. FR-6 is a different requirement ("A pipeline scope is released exactly once, on every exit path", `requirements.md`). So the three-way partition covers one paragraph of FR-13 twice and the other zero times, while claiming to be exhaustive.

Separately, 0072's share is labelled "FR-13's **ownership** clause — who owns, and who must not dispose, a scope the pipeline was handed", but FR-13's own text routes exactly that to FR-12: "(The failed-*build* case is FR-5; **a borrowed scope is never disposed at all, FR-12**.)" 0072 discharges FR-12 as well, so nothing is lost — but the label points at the wrong requirement.

**Recommendation**: make the partition four-way (lead clause → 0070 for transforms and 0071 for handlers, or state it as jointly discharged with FR-6), and rename 0072's share to what FR-13 actually delegates.

---

### 10. ADR 0073 says there are 38 test projects; there are 37 (Score: 52)

**Evidence**: `0073…:274` — "no test project in the repository can host one: **none of the 38** references `Microsoft.AspNetCore.*`, `Microsoft.AspNetCore.Mvc.Testing` or `WebApplicationFactory`, and `Brighter.slnx` has no ASP.NET entry."

Re-derived: `find tests -name '*.csproj' | wc -l` → **37**; `find tests -mindepth 1 -maxdepth 1 -type d | wc -l` → **37**; `grep -o 'tests[/\\][^"]*csproj' Brighter.slnx | wc -l` → **37**. One of the 37 (`Paramore.Test.Helpers`) is a helper library rather than a test project, so the strict count is 36.

The **conclusion is correct** — `grep -rl 'Microsoft.AspNetCore' --include='*.csproj' tests/` and `grep -rl 'WebApplicationFactory' --include='*.cs' tests/` both return 0 — so the new test project is justified. Only the count is off.

**Recommendation**: "none of the 37".

---

### 11. ADR 0073 names ten acceptance criteria it "cites or discharges"; four of them are not in its References (Score: 48)

**Evidence**: `0073…:274` — "Ten of the acceptance criteria this ADR cites or discharges — AC-14, AC-15, AC-16, AC-17, AC-18, AC-19, AC-29, AC-34, AC-48 and AC-49 — need a running ASP.NET Core host". Its References line (`:332`) lists "AC-14, AC-18, AC-19, AC-22, AC-29, AC-48, AC-49" — **AC-15, AC-16, AC-17 and AC-34 are absent**. All four are cited by 0072's References. Cosmetic, but it makes the References line unusable as the coverage index it is elsewhere in the set.

**Recommendation**: add the four, or reword step 4a to "criteria this ADR's package is needed to test".

---

### Verification log

- **Citations checked: 41.** Verified correct: `ServiceProviderMapperFactory.cs:44,45,46,78`; `ServiceProviderMapperFactoryAsync.cs:45-46`; `ServiceProviderTransformerFactory.cs:44-45`; `ServiceProviderTransformerFactoryAsync.cs:45-46`; `ServiceProviderHandlerFactory.cs:34,40,49-50,102-107,120-125,127-131,133-137`; `ServiceProviderLifetimeScope.cs:42,49,118-123,126,136-137,139-140,163-178,185,259,406,449,462`; `BrighterOptions.cs:9,20,37,52,69,72`; `PipelineBuilder.cs:37,47,59,76,92,151,187,202,232,248,269-270,567,578`; `CommandProcessor.cs:317,394,472,481,575,601`; `IAmAHandlerFactory.cs:7` (`public interface IAmAHandlerFactory;` — a bare marker, exactly as 0071 says); `IAmALifetime.cs:34`; `HandlerLifetimeScope.cs:33`; `src/Directory.Build.props:43,45`. **Failed: 1** — `ServiceProviderLifetimeScope.cs:152` cited twice by 0072 for `_scopedInstances`; the field is at `:49` and `:152` is `GetOrCreateSingleton<T>` (finding 4).
- **Counts re-derived**: `IBrighterOptions` implementations — claimed 1 in `src/` / 0 in `tests/`, **got 1 / 0** ✓ (and the ledger's contradicting "nothing implements it", finding 2). Files under `tests/` registering `IBrighterOptions` — claimed 125, **got 125** ✓. `new PipelineBuilder<` in `tests/` — claimed 69, **got 69** ✓; in `src/` — claimed 4 dispatch + 2 validation, **got exactly those 6** ✓. `IAmALifetime` implementations — claimed 7 (1 src + 6 doubles), **got 7** ✓. `IAmAHandlerFactory` — claimed 21 (5 src + 16 doubles), **got 5 + 16 = 21** ✓. `src/` projects on `$(BrighterCoreTargetFrameworks)` — claimed 24, **got 24** ✓. ADR number collisions — claimed 3×0053, 2×0054, 2×0064, **got exactly that** ✓. Test projects — claimed 38, **got 37** ✗ (finding 10). Eight breaking interfaces across 0070+0071, three non-factories — **re-derived 6+2=8, non-factories = 2 registries + `IAmALifetime` = 3** ✓, and consistent in 0070 §7a, 0071, 0075, 0076. "Five container-backed factories" (0072, 0074, 0075) vs "four" (0070) — **consistent**: 0070 predates the handler factory joining the seam. "Six scope-configuration rules" in all seven sibling maps vs 0074's 4+1+1 — **consistent**. 0074's "nine new types in the DI package plus one in core" — **re-counted 9 + 1** ✓. AC-13's "five decisions" and AC-46's "zero decisions" as quoted by 0072/0075 — **verified verbatim against the ACs**. AC-24's four clauses as characterised by 0071/0075 — **verified**; AC-24 does enumerate "the six factory interfaces" (NFR-1's withdrawal list), so 0070 §7a's "AC-24's own wording enumerates a different six" is correct.
- **Mermaid blocks rendered: 15/15.** Extracted per ADR (0070: 2, 0071: 3, 0072: 1, 0073: 2, 0074: 2, 0075: 2, 0076: 3) and rendered each with `npx -p @mermaid-js/mermaid-cli@11 mmdc` (v11.16.0). Every one produced a non-empty `.svg` with exit 0. **None failed.**
- **Escaped-entity grep**: `grep -c '&lt;\|&gt;\|&amp;'` returns **0** on all seven.
- **"chain" grep**: 4 hits, all legitimate — the rejected name `IAmAChainScope` (0070 Alternative 7) and three uses of "chaining"/"chain naturally" as ordinary English about fluent APIs (0073). No stale terminology.
- **Authoring-conversation / ephemeral-state grep**: no hits for `PROMPT.md`, "at the user's direction", "the user chose", "per the user", "review round", reviewer references, commit hashes, or `/spec:` commands across all seven.
- **Sibling maps**: extracted the nine table lines under `### Where this ADR sits` from each ADR, normalised `**` and ` *(this one)*`, and diffed pairwise against 0070 — **all six diffs IDENTICAL**. Each ADR bolds exactly its own row and marks it *(this one)*. Lead line "Seven ADRs deliver the parent requirement, one decision each" present in all seven; positional sentences read first/second/third/fourth/fifth/sixth/seventh with no ordinal collision.
- **Unifying sentence**: "**the per-pipeline object carries the DI scope**" appears **verbatim in all seven** (0070:52, 0071:50 and again at :115 and :268, 0072:51, 0073:54, 0074:52, 0075:52, 0076:54). The introducing clause takes three forms ("One rule unifies the first two…", "ADR 0070's rule is the one this ADR applies:", "The rule the first two state is") but the rule itself is never paraphrased. **No variant found — not a finding.**
- **Frontmatter / status / supersession**: uniform seven-key shape (`id`, `title`, `status`, `author`, `created`, `summary`, `tags`) on all seven; all `status: Proposed`; all `author: Ian Cooper`; `created` 2026-08-02 (0070–0074) and 2026-08-03 (0075, 0076). All seven carry "This ADR **supersedes no prior ADR.**" with a distinct trailing clause. H2 heading set and order identical across all seven (`Status`, `Context`, `Decision`, `Consequences`, `Alternatives Considered`, `References`).
- **Index**: `awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > $SCRATCH/idx && diff $SCRATCH/idx docs/adr/index.md` → **byte-identical**, awk exit 0, no stderr.
- **Coverage sweep**: enumerated every id in `requirements.md` (27 FR, 10 NFR, 50 AC, 21 C incl. C-12a, 22 D incl. D0b/D0c, 14 OOS) and built a mention matrix across the seven. **Every FR, NFR, AC and D is mentioned by at least one ADR.** The single unmentioned constraint is **C-13** ("*how* … is a design/ADR concern"), which is a meta-constraint the set satisfies by existing — not reported as a finding. OOS-1/2/3/5/6/9/10/11/12/13 are unmentioned, which is correct for out-of-scope declarations. **No requirement is claimed as *discharged* by more than one ADR** — the FR-13, FR-15 and FR-17 three-way splits are the only shared ids and each names disjoint clauses. The gaps are in finding 5.
- **Probes compiled/run**: none. Every claim I tested was settleable by reading source or re-deriving a count; no framework-behaviour claim in the set-level remit needed one.

### My tally

| Range | Count |
|---|---|
| 90-100 | 0 |
| 70-89 | 3 |
| 50-69 | 7 |
| 0-49 | 1 |

Total: 11 · At or above 60: 9

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 23 |
| 50-69 (Medium) | 47 |
| 0-49 (Low) | 7 |

**Total findings**: 77
**Findings at or above threshold (60)**: 63
