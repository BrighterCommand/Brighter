# tasks.md — Fix pipeline-cache simple-name collision (key caches by `System.Type`)

**Spec:** `specs/0035-pipeline-cache-type-key/requirements.md`
**ADR:** `docs/adr/0064-pipeline-cache-type-key.md` (Implementation Approach steps 1-4)
**Issue:** #4192

## Scope summary

Re-key the six process-global static mementos in the three pipeline builders from `ConcurrentDictionary<string,…>` (keyed by `GetType().Name`) to `ConcurrentDictionary<Type,…>` (keyed by `GetType()`), so two same-simple-name types in different namespaces registered for one request type no longer collide. This is a surgical correctness fix: only the key type and key expression change. No public API, no control flow, no `Describe`/`DescribeTransforms`/`ClearPipelineCache` behaviour changes.

**Structural constraint driving decomposition:** each builder class's static-dictionary retype is atomic — flipping `<string,…>` to `<Type,…>` forces every key read/write site in that class (and any test reflecting those fields) to change together to compile. So the core work is grouped one task per builder **class**, each covering all of that class's directions/modes.

**TFMs:** net9.0;net10.0 — fix must pass on both.

**Test conventions (verified against the codebase):**
- Brighter Core tests are straight xUnit with `When_<scenario>` class/file names (e.g. existing `When_Building_A_Pipeline_Post_Attributes_Are_Cached`). Not the BDD Given/When/Then partial-class style.
- Every pipeline test calls the relevant `ClearPipelineCache()` as its first line to isolate the static cache (`PipelineBuilder<T>.ClearPipelineCache()`, `TransformPipelineBuilder.ClearPipelineCache()`, `TransformPipelineBuilderAsync.ClearPipelineCache()`).
- Assert pipeline/transform contents via `PipelineTracer` / `TransformPipelineTracer` → `DescribePath` → `ToString()` (see `MessageSerialisation/When_A_Message_Mapper_Map_To_Message_Has_A_Transform.cs` for the `TransformPipelineTracer` pattern; `"A|B"` pipe-delimited describe strings).
- Colliding test doubles = inline classes in two nested namespaces inside the test file, same simple name, different namespaces, differing decorators/transforms, both registered for the same request type.
- Existing transform tests live in `tests/Paramore.Brighter.Core.Tests/MessageSerialisation/`; handler-pipeline tests in `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/`.

---

## Tasks

### Task 1 — Handler builder: disambiguate handler decorator caches by runtime Type (sync + async)

- [x] **TEST + IMPLEMENT: `PipelineBuilder<TRequest>` resolves each same-simple-name handler's own decorators by runtime Type, for both the sync and async pipelines, independent of build order**
  - **USE COMMAND**: `/test-first PipelineBuilder caches pre/post handler attributes keyed by the handler's runtime Type, so two handlers sharing a simple class name in different namespaces (both registered for the same request type) each build with their own declared decorators regardless of which was built first, for both BuildPipeline (sync) and BuildAsyncPipeline (async); and a single handler built twice leaves exactly one cache entry keyed by typeof(that handler) with an equivalent decorator sequence`
  - Test location: `"tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/"`
  - Test file: `When_Building_A_Pipeline_Disambiguates_Handlers_By_Type.cs`
  - Test should verify (each `[Fact]` calls `PipelineBuilder<TRequest>.ClearPipelineCache()` first; colliding handlers are inline classes in two nested namespaces sharing a simple name, e.g. `...TypeKeyed.A.OrderHandler` and `...TypeKeyed.B.OrderHandler`, registered for one request type, with different declared `RequestHandlerAttribute` decorators; assert pipeline contents via `PipelineTracer` → `DescribePath` → `ToString()`):
    - **(AC-1, FR-1, sync)** Build sync pipeline for `A.OrderHandler` first, then `B.OrderHandler`: `B`'s pipeline contains `B`'s own decorators with `B`'s attribute arguments, not `A`'s.
    - **(AC-2, FR-1, sync, order-independence)** Same two types built in the opposite order: each still resolves its own decorators.
    - **(AC-3, FR-2, async)** Two async handler doubles sharing a simple name across namespaces with different decorators: `BuildAsyncPipeline` for each yields that type's own decorators, both build orders.
    - **(AC-6a, FR-5, single-type reuse)** This fact must be isolated: register a **single** handler type for its own request type, call `ClearPipelineCache()` first, and build **only** that handler (twice) — do not reuse the colliding doubles or build any other handler, since the mementos are process-global statics and the count assertion is otherwise fragile. Note the mementos are static **per closed generic** `PipelineBuilder<TRequest>`, so use a request type unique to this fact (not one a sibling fact also builds) to keep the count deterministic. Assert `s_preAttributesMemento` and `s_postAttributesMemento` each have `Count == 1` with the single key `typeof(thatHandler)`, and the second build's decorator sequence is equivalent to the first (same decorator types, order, `Step`, and `HandlerTiming` Before/After). Read the caches by reflection on the static fields (mirror the existing `GetPostAttributesCacheKeys` helper, but typed `IEnumerable<Type>` / `Cast<Type>()` and asserting `typeof(...)`).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should (ADR step 1-2, `src/Paramore.Brighter/PipelineBuilder.cs`):
    - Re-type both static dictionaries from `ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>>` to `ConcurrentDictionary<Type, …>` at `PipelineBuilder.cs:49-50` (`s_preAttributesMemento`, `s_postAttributesMemento`).
    - Replace the key expression `implicitHandler.Name.ToString()` with `implicitHandler.GetType()` at the four sync sites `BuildPipeline` lines 275, 284, 292, 301 and the four async sites `BuildAsyncPipeline` lines 319, 327, 334, 342.
    - Touch nothing else: `ClearPipelineCache()` (`:253`), `Describe`/`Describe()` (`:106-137, 144-155`), `AddGlobalInboxAttributes`/`AddGlobalInboxAttributesAsync` `UseInbox` construction (`:350-389`), `HandlerName`, `RequestHandler.Name` (FR-7, AC-8, AC-9, OOS-3).

### Task 2 — Update the existing white-box cache-key test for the `Type` key (mandatory; same atomic change as Task 1)

- [x] **REGRESSION VERIFICATION (not /test-first) — retype the existing white-box memento-key helper so it compiles and asserts `Type` keys**
  - This change is forced by, and must land together with, Task 1's `PipelineBuilder` retype. `Cast<string>()` over `Type` keys is **not** a compile error — it throws `InvalidCastException` at runtime — so re-typing the helper is mandatory.
  - File: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/When_Building_A_Pipeline_Post_Attributes_Are_Cached.cs` (ADR step 4).
  - Edits:
    - `GetPostAttributesCacheKeys()` return type `IEnumerable<string>` → `IEnumerable<Type>` (signature line 58); body `cache.Keys.Cast<string>()` → `cache.Keys.Cast<Type>()` (line 66).
    - Sync assertion `Assert.Contains(nameof(MyPreAndPostDecoratedHandler), …)` → `Assert.Contains(typeof(MyPreAndPostDecoratedHandler), …)` (line 33).
    - Async assertion `Assert.Contains(nameof(MyPreAndPostDecoratedHandlerAsync), …)` → `Assert.Contains(typeof(MyPreAndPostDecoratedHandlerAsync), …)` (line 55).
  - Pass condition: both existing `[Fact]`s in this file pass against the Task 1 production change. This is the intended "the key type changed" signal (ADR Risks/Mitigations) — it asserts the cache's internal representation only, which C-5 leaves unconstrained; it is not a new behavioural test.
  - **Depends on:** Task 1 (same atomic compile unit — implement in the same step as Task 1's GREEN).

### Task 3 — Sync transform builder: disambiguate wrap/unwrap caches by runtime Type

- [x] **TEST + IMPLEMENT: `TransformPipelineBuilder` resolves each same-simple-name mapper's own wrap and unwrap transforms by runtime Type, independent of build order**
  - **USE COMMAND**: `/test-first TransformPipelineBuilder caches wrap and unwrap transform attributes keyed by the mapper's runtime Type, so two message mappers sharing a simple class name in different namespaces (both registered for the same request type) each build with their own declared wrap/unwrap transforms regardless of build order; and a single mapper built twice leaves exactly one entry per transform cache keyed by typeof(that mapper) with an equivalent transform sequence`
  - Test location: `"tests/Paramore.Brighter.Core.Tests/MessageSerialisation/"`
  - Test file: `When_Building_A_Transform_Pipeline_Disambiguates_Mappers_By_Type.cs`
  - Test should verify (each `[Fact]` calls `TransformPipelineBuilder.ClearPipelineCache()` first; colliding mapper doubles are inline classes in two nested namespaces sharing one simple name — e.g. `...TypeKeyed.A.EventMapper` with `[CompressPayload]`-style wrap/unwrap attributes and `...TypeKeyed.B.EventMapper` with different ones — each registered for its own request type via a `MessageMapperRegistry`; assert transform contents via `TransformPipelineTracer` → `DescribePath` → `ToString()` as in the existing `When_A_Message_Mapper_Map_To_Message_Has_A_Transform` test):
    - **(AC-4, FR-3, wrap, order-independence)** Build the wrap pipeline for the two same-simple-name mappers in each order: each wrap pipeline contains that mapper's own wrap transforms, not the other's.
    - **(AC-5, FR-4, unwrap, order-independence)** Same for unwrap pipelines: each contains that mapper's own unwrap transforms, both build orders.
    - **(AC-6b, FR-5, single-type reuse)** Isolated fact: register a **single** mapper type, call `TransformPipelineBuilder.ClearPipelineCache()` first, build only that mapper's wrap and unwrap pipelines twice (single-threaded), nothing else. Assert `s_wrapTransformsMemento` and `s_unWrapTransformsMemento` each have `Count == 1`, keyed `typeof(thatMapper)`; the second build's transform sequence is equivalent to the first (same transform types, order, `Step`). Read the caches by reflection on the static fields, `Cast<Type>()`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should (ADR step 1-2, `src/Paramore.Brighter/TransformPipelineBuilder.cs`):
    - Re-type both static dictionaries from `ConcurrentDictionary<string, …>` to `ConcurrentDictionary<Type, …>` at `TransformPipelineBuilder.cs:57-61` (`s_wrapTransformsMemento`, `s_unWrapTransformsMemento`).
    - Change `var key = messageMapper.GetType().Name` to `var key = messageMapper.GetType()` in `FindWrapTransforms` (line 238) and `FindUnwrapTransforms` (line 253); keep the surrounding `TryGetValue` / `TryAdd` population unchanged.
    - Touch nothing else: `ClearPipelineCache()` (`:223`), `DescribeTransforms` (`:197-221`) unchanged (FR-7, AC-8, AC-9).

### Task 4 — Async transform builder: disambiguate wrap/unwrap caches by runtime Type

- [x] **TEST + IMPLEMENT: `TransformPipelineBuilderAsync` resolves each same-simple-name mapper's own wrap and unwrap transforms by runtime Type, independent of build order**
  - **USE COMMAND**: `/test-first TransformPipelineBuilderAsync caches wrap and unwrap transform attributes keyed by the async mapper's runtime Type, so two async message mappers sharing a simple class name in different namespaces (both registered for the same request type) each build with their own declared wrap/unwrap transforms regardless of build order; and a single mapper built twice post-warmup is served from the single retained cache entry keyed by typeof(that mapper) with an equivalent transform sequence`
  - Test location: `"tests/Paramore.Brighter.Core.Tests/MessageSerialisation/"`
  - Test file: `When_Building_An_Async_Transform_Pipeline_Disambiguates_Mappers_By_Type.cs`
  - Test should verify (each `[Fact]` calls `TransformPipelineBuilderAsync.ClearPipelineCache()` first; same colliding-async-mapper double pattern as Task 3 but using `IAmAMessageMapperAsync<T>` and the async builder/registry; assert via `TransformPipelineTracer` → `DescribePath` → `ToString()`):
    - **(AC-4, FR-3, wrap, order-independence — async builder)** Build the async wrap pipeline for the two same-simple-name async mappers in each order: each contains its own wrap transforms.
    - **(AC-5, FR-4, unwrap, order-independence — async builder)** Same for async unwrap pipelines, both build orders.
    - **(AC-6c, FR-5, single-type reuse — post-warmup)** Isolated fact: register a **single** async mapper type, call `TransformPipelineBuilderAsync.ClearPipelineCache()` first, build only that mapper's wrap and unwrap pipelines twice (single-threaded, so the `GetOrAdd` factory has already run), nothing else. Assert each transform cache has `Count == 1` keyed `typeof(thatMapper)`; second build's transform sequence is equivalent. (Do not assert reference-equality of the retained value; concurrent first-access reuse is out of scope per C-6.) Read the caches by reflection, `Cast<Type>()`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should (ADR step 1-2, `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs`):
    - Re-type both static dictionaries from `ConcurrentDictionary<string, …>` to `ConcurrentDictionary<Type, …>` at `TransformPipelineBuilderAsync.cs:57-61` (`s_wrapTransformsMemento`, `s_unWrapTransformsMemento`).
    - Change `var key = messageMapper.GetType().Name` to `var key = messageMapper.GetType()` in `FindWrapTransforms` (line 201) and `FindUnwrapTransforms` (line 209); keep the `GetOrAdd(key, s => …)` population unchanged (the unused factory parameter remains, now typed `Type`).
    - Touch nothing else: `ClearPipelineCache()` (`:186`) unchanged; leave the pre-existing mis-typed `CreateLogger<TransformPipelineBuilder>` at `:50` untouched (OOS-2). (FR-7, AC-8, AC-9.)

### Task 5 — Global-inbox `UseInbox` immunity (regression guard)

- [x] **REGRESSION VERIFICATION (not /test-first) — `UseInbox` is built per runtime type after the cache read and never flows through the decorator cache**
  - This is a preservation check, expected to PASS without any new production code: pre-fix, `UseInbox` is already constructed from `implicitHandler.GetType()` *after* the cache read in `AddGlobalInboxAttributes`/`AddGlobalInboxAttributesAsync` (`PipelineBuilder.cs:350-389`), so "each handler gets its own `UseInbox`" holds even with the simple-name bug present. It is therefore **not** RED-able and must not be a `/test-first` cycle.
  - Add one clearly-labelled regression-guard `[Fact]` (or rely on the existing global-inbox suite — see "what to run"): with global inbox enabled via `InboxConfiguration` and two same-simple-name handlers registered for `T`, build each pipeline in either order; assert each handler's built pipeline (via `PipelineTracer` → `DescribePath` → `ToString()`) contains a `UseInbox` step constructed against its own runtime type. **For the immunity assertion, inspect the cached _values_, not the keys**: the memento is keyed by handler `Type`, so `UseInbox` can never be a key — a key check would be vacuously true. Assert that the cached attribute sequence `s_preAttributesMemento[typeof(handler)]` (and post) contains **no** `UseInboxAttribute`/`UseInboxAsyncAttribute` (it is pushed onto the local `preAttributes` by `AddGlobalInboxAttributes` *after* the cache `TryAdd`, never into the cached value), while the built pipeline still carries its own `UseInbox` (AC-7, FR-6, C-2).
  - Test location (if adding the guard): `"tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/"`; file: `When_Building_A_Pipeline_With_Global_Inbox_Each_Handler_Gets_Its_Own_UseInbox.cs`.
  - What to run / pass condition: the existing global-inbox suite (`When_Building_A_Pipeline_With_Global_Inbox*.cs`) plus the new guard all pass unchanged against the fixed builders.
  - **Depends on:** Task 1.

### Task 6 — Preservation of `Describe` / `DescribeTransforms` / `ClearPipelineCache` and public API (regression guard)

- [x] **REGRESSION VERIFICATION (not /test-first) — diagnostic + cache-clear behaviour and public surface unchanged**
  - Preservation check, expected to PASS with no new production code: `Describe`/`DescribeTransforms` do not read the cache, and `ClearPipelineCache` semantics are untouched, so the existing suite must pass unmodified (AC-8, AC-9, FR-7).
  - What to run / pass condition:
    - Existing `Describe`/`DescribeTransforms` tests pass unmodified — including `tests/Paramore.Brighter.Core.Tests/Validation/When_transform_builder_describes_should_return_mapper_and_transforms.cs` (described content equivalence: same step types, order, steps, timings).
    - Existing `ClearPipelineCache` recompute behaviour still holds (covered by every pipeline test's first-line `ClearPipelineCache()` plus the inbox-cache suite).
    - No edits to `ClearPipelineCache`, `Describe`, `DescribeTransforms`, `HandlerName`, `RequestHandler.Name` signatures.
  - **Depends on:** Tasks 1, 3, 4.

### Task 7 — Final verification: both TFMs, Release warnings-as-errors, no public-API change

- [x] **REGRESSION VERIFICATION (not /test-first) — full build + Core suite green on net9.0 and net10.0, no public-API drift**
  - What to run / pass conditions:
    - `dotnet test tests/Paramore.Brighter.Core.Tests` on **both** `net9.0` and `net10.0` — all green (the fix must pass on both TFMs).
    - `dotnet build --configuration Release /p:NetCoreBuild=true` — succeeds with warnings-as-errors (this confirms no new warnings from the retype).
    - Confirm no public-API signature change (AC-9): no public-API approval harness exists in this repo (verified — no `PublicApiGenerator`/`ApiCompat`/Shipped-Unshipped/`.verified.txt` baselines), and every edited member in Tasks 1/3/4 is `private`/`private static`, so AC-9 is structurally guaranteed. Rely on the clean warnings-as-errors Release build plus the private-only edits; no API baseline to update. (If a public-API harness is ever added, this task must be revisited.)
  - **Depends on:** Tasks 1-6.

### AC-10 (concurrency) — covered by design, no dedicated test (decision recorded)

AC-10 (NFR-2 / FR-5 under concurrency) is **covered by design**, not by a discrete concurrency test — this decision is recorded with the user (concurrency tests for a cache race are inherently flaky). `ConcurrentDictionary` retained with a `System.Type` key gives thread-safety by construction: `Type` is reference-unique per loaded type with identity-based `Equals`/`GetHashCode`, so concurrent reads/writes for the same or different types cannot observe another type's metadata or corrupt the cache. The async transform builder's `GetOrAdd` first-access double-scan (C-6) is pre-existing and explicitly unchanged by re-keying — every thread still ultimately observes its own mapper type's transforms and the cache converges to one retained entry per type. No concurrency test task is created; this note is the documented coverage.

---

## Coverage cross-reference

### Functional Requirements
| Req | Covered by |
|---|---|
| FR-1 (handler decorators by Type, sync) | Task 1 (AC-1, AC-2 facts) |
| FR-2 (handler decorators by Type, async) | Task 1 (AC-3 fact) |
| FR-3 (wrap-transform by Type, sync + async) | Task 3 (sync) + Task 4 (async) |
| FR-4 (unwrap-transform by Type, sync + async) | Task 3 (sync) + Task 4 (async) |
| FR-5 (single-type caching preserved) | Task 1 (AC-6a), Task 3 (AC-6b), Task 4 (AC-6c) |
| FR-6 (global-inbox `UseInbox` immunity) | Task 5 (regression guard) |
| FR-7 (no public API / `Describe` / `ClearPipelineCache` change) | Task 6 + Task 7; preserved-by-construction in Tasks 1/3/4 impl notes |

### Non-functional Requirements
| Req | Covered by |
|---|---|
| NFR-1 (no perf regression / O(1) hits) | By design — new key is `GetType()` (already called at every site) + `Type` hashing, no extra reflection scan; confirmed by Task 7 build. No discrete test (nothing RED-able; the change adds no scan). |
| NFR-2 (thread-safety preserved) | By design — `ConcurrentDictionary` retained, `Type` key; see "AC-10 — covered by design" note. Confirmed green in Task 7. |
| NFR-3 (all three builders in one change) | Tasks 1 + 3 + 4 together (sole change set); confirmed by Task 7. |

### ADR decision points
| ADR point | Covered by |
|---|---|
| Step 1 — retype six dictionaries to `<Type,…>` | Task 1 (`PipelineBuilder` ×2), Task 3 (`TransformPipelineBuilder` ×2), Task 4 (`TransformPipelineBuilderAsync` ×2) |
| Step 2 — change key expr to `GetType()` at all read/write sites | Task 1 (8 sites), Task 3 (2 sites), Task 4 (2 sites) |
| Step 3 — leave `ClearPipelineCache`/`Describe`/`DescribeTransforms`/`UseInbox`/`HandlerName`/`RequestHandler.Name` untouched | Tasks 5 + 6 (guards) + impl "touch nothing else" notes in 1/3/4 |
| Step 4 — retype the white-box memento-key test | Task 2 |
| Key = `System.Type` (not `FullName`/`AssemblyQualifiedName`) | Tasks 1/3/4 impl |
| No feature flag | Implicit — single corrected behaviour, no toggle introduced in any task |
| OOS-2 — mis-typed async logger generic left as-is | Task 4 impl note |
| C-6 — async `GetOrAdd` double-scan unchanged | Task 4 (AC-6c scoped post-warmup) + AC-10 note |

### Acceptance Criteria
| AC | Covered by |
|---|---|
| AC-1 (sync handler, winner-built-first) | Task 1 |
| AC-2 (sync handler, order-independent) | Task 1 |
| AC-3 (async handler) | Task 1 |
| AC-4 (wrap, sync + async) | Task 3 + Task 4 |
| AC-5 (unwrap, sync + async) | Task 3 + Task 4 |
| AC-6a (single handler reuse) | Task 1 |
| AC-6b (single mapper reuse, sync) | Task 3 |
| AC-6c (single mapper reuse, async post-warmup) | Task 4 |
| AC-7 (`UseInbox` immunity) | Task 5 (regression guard — not RED-able) |
| AC-8 (`Describe`/`DescribeTransforms`/`ClearPipelineCache` unchanged) | Task 6 (existing suite unmodified) |
| AC-9 (no public-API change) | Task 6 + Task 7 |
| AC-10 (concurrency) | By design — "AC-10 — covered by design" note; no test task (per user decision) |
| AC-11 (both transform builders converted) | Task 3 + Task 4 (independently exercise sync and async builders on the colliding mapper scenarios) |

### Items intentionally not covered by a discrete test task
- **AC-10 / NFR-2 concurrency** — covered by design (`ConcurrentDictionary` + `Type` key), per the user's decision; documented note only.
- **NFR-1 performance** — no RED-able assertion; the change adds no reflection scan and `Type` hashing is O(1). Verified only by the clean Task 7 build.
- **AC-7 / FR-6 `UseInbox`** and **AC-8/AC-9 / FR-7 preservation** — not RED-able (behaviour already correct pre-fix); covered as regression guards (Tasks 5, 6), not `/test-first` cycles.

### Scope-creep check
No task introduces behaviour beyond the ADR's Implementation Approach steps 1-4. Every task traces to a FR/NFR/AC or an ADR decision point above. There are no orphan tasks. Task 2 is mandatory (forced by Task 1's atomic retype; `Cast<string>()` over `Type` keys throws at runtime), not added scope.
