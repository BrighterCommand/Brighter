# Tasks

**Specification**: `0037-validate-subscription-channel-factory` — validate that a `Subscription` is compatible with the channel factory it will actually be handed, at startup, via `ValidatePipelines()`.

**Linked issue**: [#4334](https://github.com/BrighterCommand/Brighter/issues/4334) (prompted by [#4331](https://github.com/BrighterCommand/Brighter/issues/4331)).

**Design**: two Accepted ADRs, each carrying its own Implementation Approach, which are the skeleton of the phases below.

- [ADR 0072 — Validate Subscription and Channel Factory Compatibility at Startup](../../docs/adr/0072-subscription-channel-factory-compatibility.md) — the rule (FR-1 to FR-6, FR-13), `CombinedChannelFactory.FactoryTypes`, and the five `ChannelFactoryType` corrections (FR-7 to FR-11) as a consequence. **Phases 1-6.**
- [ADR 0073 — Gateway `ChannelFactoryType` Regression Guard](../../docs/adr/0073-gateway-channel-factory-type-regression-guard.md) — FR-12's construction-free sweep, its pure predicate, and the twelve generated per-gateway tests. **Phases 7-12.**

**Ordering constraint (load-bearing).** ADR 0072 is the critical path. ADR 0073's generated sweep asserts that every `Reason` is `null`, which is false today in five of the twelve assemblies (GcpPubSub, MQTT, AWSSQS, AWSSQS.V4, Postgres). **Phase 5 (tasks 31-35) must land before task 53's generated sweeps are expected green and before task 55 enables them in CI.** Task 53 may commit the generated files earlier — the generated-tree audit wants them — but task 55 must not be enabled until tasks 31-35 have merged.

**Standing obligations on every task below**: no broker, database or network access in any test (NFR-3, AC-31); British spelling in all new documentation and XML comments (NFR-7); one class per file and one behaviour per test (`.agent_instructions/testing.md`); generated files are **never** edited directly — a change is a template edit followed by `./generate-test.sh` (`.agent_instructions/generated_tests.md`).

**Characterisation tasks (maintainer ruling D14; ADR 0071 *Characterisation amendment*).** One task per acceptance criterion (D12) means some `/test-first` tasks assert behaviour an earlier task's implementation already delivers, so the new test is **green on first run**. That is accepted. These tasks are labelled **`CHARACTERISE`** — the label `/spec:implement` and `/spec:ralph-implement` dispatch on — and carry a **🔁** bullet naming the mutation. RED-first still holds, per test file:

1. Write the test and run it. If it **fails**, that is RED — continue as a normal test-then-implement task.
2. If it **passes**, apply the task's **named RED mutation** — a temporary change to *production* code, never to the test.
3. Run the test and confirm it fails **for the right reason** (the assertion the task names, not a compile error or an unrelated exception).
4. **Revert the mutation**, run the test green again, then run the regression suite. `git status` must show no production file left modified.
5. The ⛔ gate fires here in `review-before` — after RED is observed, before committing. The `test:` commit contains the test only; the mutation is never committed. A characterisation test is never rewritten and never "already complete".

**Task labels** are those the gear commands dispatch on: `TEST + IMPLEMENT`, `CHARACTERISE`, `GENERATE`, `STRUCTURAL` (fixtures only — `refactor:`), `SETUP` (configuration and generator scaffolding — `chore:`), `DOC`, `VERIFY`. `GENERATE` (task 53) renders tests with the generator; they are never hand-written.

**Scoping the review gear.** Phase headings are plain ASCII, `## Phase N: <name>`, so a `/spec:gear` scope can be typed exactly — the scope is the heading text without the `#`s, e.g. `Phase 5: Transport corrections`. ADR references and ordering warnings sit in the line under each heading, not in it. Tasks are numbered 1-59 continuously, so a range scope such as `tasks 31-35` works equally well.

---

## Phase 1: Groundwork for the rule

*ADR 0072 step 1, and the C-9 test doubles every later phase uses.*

- [x] **1. STRUCTURAL: the closed test-double set for the rule's criteria (C-9)**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/TestDoubles/`
  - One class per file. The set is **closed** — no acceptance criterion in phases 2-4 may use a double not listed here, and no double here may acquire transport behaviour:
    - `DeclaredChannelFactory : IAmAChannelFactory`
    - `DerivedChannelFactory : DeclaredChannelFactory`
    - `NonMatchingChannelFactory : IAmAChannelFactory`
    - `AlphaBus/ChannelFactory.cs` and `BetaBus/ChannelFactory.cs` — two factories **both simply named `ChannelFactory`** in namespaces `…Validation.TestDoubles.AlphaBus` / `…Validation.TestDoubles.BetaBus`
    - `DeclaringSubscription : Subscription` → declares `DeclaredChannelFactory`
    - `NonMatchingSubscription : Subscription` → declares `NonMatchingChannelFactory`
    - `NullDeclaringSubscription : Subscription` → overrides `ChannelFactoryType` to return `null`
    - `AlphaBus/AlphaSubscription.cs` → declares `AlphaBus.ChannelFactory`
    - request types, one per file: `FakeChannelFactoryRequest`, `FakeOtherRequest`, `AlphaRequest`. **No criterion may use `GreetingMade`** — that type exists only under `samples/WebAPI/*` and is invisible to every test project.
  - **Every `IAmAChannelFactory` member on every factory double MUST throw** (`CreateSyncChannel`, `CreateAsyncChannel`, `CreateAsyncChannelAsync`). The rule compares `Type` objects only, so a double's entire contribution is *being a distinct type*; a test that strays into channel creation must fail loudly rather than pass silently (NFR-3).
  - **Cross-ADR constraint from ADR 0073 — this is why it is called out here rather than discovered in phase 8.** `DeclaringSubscription`, `NonMatchingSubscription` and `AlphaBus.AlphaSubscription` MUST declare `ChannelFactoryType` as an **expression-bodied `typeof(...)`** — `public override Type ChannelFactoryType => typeof(DeclaredChannelFactory);` — and **never** as an auto-property assigned in a constructor. ADR 0073's `Sweep` reads the property from an *uninitialised* instance; an auto-property reads `null` there and the double is reported under the **null** branch, exactly as the existing `MockSubscription` (`tests/Paramore.Brighter.Core.Tests/MessagingGateway/When_constructing_a_channel_with_combined_factory.cs:85`) is. `NullDeclaringSubscription` is reported under the null branch **by design** and that is expected.
  - **Why no test of its own**: these are fixtures with no behaviour. They are consumed by task 2 and by every task in phases 2-3.
  - References: requirements C-9 (the doubles table and the request-type rule); ADR 0072 *Testing Strategy*; ADR 0073 *Hand-written cases in `Core.Tests`* (the expression-bodied constraint).
  - Depends on: nothing.

- [x] **2. TEST + IMPLEMENT: `CombinedChannelFactory.FactoryTypes` — the composite's routing identities, read from a single-pass sequence**
  - **USE COMMAND**: `/test-first when a combined channel factory is built from a single-pass sequence its factory types should list the inner factory types in constructor order`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway"
  - Test file: `When_a_combined_channel_factory_is_built_from_a_single_pass_sequence_should_report_its_factory_types.cs`
  - Test should verify:
    - a `CombinedChannelFactory` constructed from a **single-pass** `IEnumerable<IAmAChannelFactory>` yielding a `DeclaredChannelFactory` then a `NonMatchingChannelFactory` (task 1's doubles), which **throws on a second `GetEnumerator()`**. It is a **hand-written class**, `SinglePassChannelFactorySequence`, in `tests/Paramore.Brighter.Core.Tests/MessagingGateway/TestDoubles/` (one class per file) — **not** a C# `yield` iterator method, whose enumerable restarts on a second `GetEnumerator()` instead of throwing
    - `FactoryTypes` equals `[typeof(DeclaredChannelFactory), typeof(NonMatchingChannelFactory)]`, in that order
    - **What makes this fail**: a `FactoryTypes` built from the primary-constructor `factories` parameter re-enumerates the sequence `_factories = factories.ToList()` has already consumed, and the one-shot iterator throws. A collection expression or array would **not** expose the defect — it re-enumerates successfully — which is why no rule-level criterion (AC-6 included) can stand in for this test
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - File: `src/Paramore.Brighter/CombinedChannelFactory.cs`
    - Add `using System;` (the file today has only `System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`).
    - Add, **in the normative shape ADR 0072 Key Components §1 spells out**:
      - a `private IReadOnlyList<Type>? _factoryTypes;` backing field, and
      - `public IReadOnlyList<Type> FactoryTypes => _factoryTypes ??= _factories.Select(f => f.GetType()).ToList();`
    - **It MUST be derived from the `_factories` field, not from the primary-constructor `factories` parameter.** `_factories = factories.ToList()` has already consumed the `IEnumerable`, so a property built from the parameter re-enumerates a spent sequence — empty, or a throw, depending on the source — and the rule would then flag *every* subscription in *every* multi-bus application whose factories arrive as a single-pass sequence. Note the trap: the compiler rejects a get-only auto-property initialised from `_factories` (CS0236) and *accepts* the one initialised from `factories`, so the error message itself nudges toward the defect (ADR 0072, Risks).
    - Do **not** convert the class to an explicit constructor; do **not** write a plain `=> _factories.Select(…).ToList()` (re-allocates on every read).
    - XML documentation must state the routing contract ("it can serve a subscription exactly when that subscription's `ChannelFactoryType` is one of them") and the `<remarks>` must state that the property is **not** thread-safe: concurrent first reads may each build a list, so every read yields an *equal* list, not necessarily the same instance. The only caller is the single-threaded startup validation path.
  - **Guard check, after GREEN (D14 mechanism)**: temporarily replace the lazy property with ADR 0072's defect form, the get-only auto-property `public IReadOnlyList<Type> FactoryTypes { get; } = factories.Select(f => f.GetType()).ToList();`, and confirm the test fails because the sequence throws on re-enumeration — at **construction**, since the initialiser runs then. Revert. (Capturing `factories` inside the lazy getter instead does not compile: CS9124 is an error under `TreatWarningsAsErrors`.) RED on arrival is only a compile error (`FactoryTypes` does not exist); this check is what shows the guard catches the defect it exists for.
  - This test is the **named regression guard for the re-enumeration defect**. The order it pins is also relied on by tasks 9, 21 and 22 (AC-7, AC-13b, AC-13c).
  - References: `src/Paramore.Brighter/CombinedChannelFactory.cs:14`, `:34`, `:46`, `:59`; NFR-5; C-4; ADR 0072 *Risks and Mitigations* (the `FactoryTypes` re-enumeration trap).
  - Depends on: 1.

---

## Phase 2: The rule's verdict

*ADR 0072 step 3; `Core.Tests`; AC-1 to AC-11 and AC-19.*

All tasks in this phase live in `tests/Paramore.Brighter.Core.Tests/Validation/`, are written against the phase-1 doubles, and need no container, host or broker. The rule itself is `ConsumerValidationRules.ChannelFactoryCompatible(IAmAChannelFactory? defaultChannelFactory)` in `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`, built with the `Specification<Subscription>(predicate, errorFactory)` shape that `PumpHandlerMatch` uses (NFR-1). Task 3 brings it into existence; each subsequent task extends it.

- [x] **3. TEST + IMPLEMENT: A subscription handed an incompatible channel factory yields exactly one correctly-sourced Error (AC-1)**
  - **USE COMMAND**: `/test-first when a plain subscription is handed a transport channel factory the rule should report exactly one Error sourced to the subscription name`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_plain_subscription_is_handed_a_different_channel_factory_should_report_one_error.cs`
  - Test should verify:
    - `Subscription<FakeChannelFactoryRequest>` named `greeting-sub`, `ChannelFactory` null, default channel factory a `DeclaredChannelFactory`
    - exactly one `ValidationError` is produced
    - `Severity == ValidationSeverity.Error`
    - `Source == "Subscription 'greeting-sub'"` — the same format the existing four rules use
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `public static ISpecification<Subscription> ChannelFactoryCompatible(IAmAChannelFactory? defaultChannelFactory)` to `ConsumerValidationRules`, alongside the existing four rules
    - Use the `Specification<Subscription>(predicate, errorFactory)` constructor (`src/Paramore.Brighter/Specification.cs`), not `DisposingSpecification<Subscription>` and not the collapsed many-findings constructor — FR-13 fixes this rule at zero or one finding
    - Introduce the three private statics ADR 0072 §2 prescribes: `ResolveCandidates(Subscription, IAmAChannelFactory?)` returning `(Arm, IReadOnlyList<Type>)`, `IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)`, and `DisplayName(Type)`; `Arm` is a private nested `enum { Direct, Combined }`
    - Emit `new ValidationError(ValidationSeverity.Error, $"Subscription '{s.Name}'", message)` on mismatch and nothing on a pass
    - Read only `Subscription.ChannelFactory`, `Subscription.ChannelFactoryType` and `Subscription.Name` — never `RequestType`, never mutate the subscription, never create a channel
    - No `try`/`catch` anywhere in the rule body — ADR 0064's "rules must not catch" stands
  - Depends on: 1.

- [x] **4. TEST + IMPLEMENT: A subscription whose declared type matches the effective factory passes (AC-2)**
  - **USE COMMAND**: `/test-first when a subscription declares the type of the default channel factory the rule should report no findings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subscription_declares_the_default_channel_factory_type_should_report_no_findings.cs`
  - Test should verify:
    - `DeclaringSubscription` named `greeting-sub`, `ChannelFactory` null, default factory a `DeclaredChannelFactory`
    - no findings are produced
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement the FR-3 **direct arm** in `IsCompatible`: `declared is not null && declared.IsAssignableFrom(candidates[0])`
    - Use `Type.IsAssignableFrom`, **not** the `IsAssignableTo` polyfill — the latter is `internal` to core and unavailable across the assembly boundary
  - Depends on: 3.

- [x] **5. TEST + IMPLEMENT: A subscription's own channel factory takes precedence over the default (AC-3, FR-2 step 1)**
  - **USE COMMAND**: `/test-first when a subscription carries its own channel factory the rule should ignore the configured default`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subscription_carries_its_own_channel_factory_should_ignore_the_default.cs`
  - Test should verify:
    - default channel factory is a `NonMatchingChannelFactory`
    - `DeclaringSubscription` named `sub-a` with `ChannelFactory` set to a `DeclaredChannelFactory`
    - no findings are produced for `sub-a` — the default is not consulted at all
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `ResolveCandidates` applies `subscription.ChannelFactory ?? defaultChannelFactory` — step 1 wins outright, mirroring `DispatchBuilder.cs:146-148`
  - Depends on: 4.

- [x] **6. CHARACTERISE: A subscription falling back to a mismatched default is detected (AC-4, FR-2 step 2)**
  - **USE COMMAND**: `/test-first when a subscription has no channel factory of its own the rule should validate it against the configured default`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subscription_falls_back_to_a_mismatched_default_should_report_one_error.cs`
  - Test should verify:
    - the AC-3 configuration with `sub-a.ChannelFactory` left null
    - exactly one `Error` is produced for `sub-a`
  - **🔁 Characterisation** — green on first run: task 5 already implements `subscription.ChannelFactory ?? defaultChannelFactory`, so the fallback exists. **RED mutation**: make the predicate return `true` (no finding) whenever `subscription.ChannelFactory` is null — the realistic defect of validating only subscriptions that carry their own factory, so the default is never checked. The test fails on its one-Error assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `ResolveCandidates` falls through to the captured default when `subscription.ChannelFactory` is null
  - Depends on: 5.

- [x] **7. CHARACTERISE: The verdict is invariant to `DispatchBuilder`'s back-fill (AC-5, FR-2a)**
  - **USE COMMAND**: `/test-first when the dispatch builder has back-filled the default channel factory into a subscription the rule should produce byte-identical findings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_the_default_channel_factory_has_been_back_filled_should_report_identical_findings.cs`
  - Test should verify:
    - **AC-4's configuration** (`sub-a`, a `DeclaringSubscription` with `ChannelFactory` null, and a `NonMatchingChannelFactory` default, so one Error) is evaluated **before** `subscription.ChannelFactory` is back-filled with the default, and **again after** it has been (simulating `DispatchBuilder.Subscriptions()`'s write at `DispatchBuilder.cs:146-149`, which assigns the **same** default instance the rule was given — the test must back-fill that instance, not a new one)
    - both evaluations produce the same **count**, the same `Source`, and a **byte-identical** `Message`
  - **Why AC-4 and not AC-2 or AC-3**: AC-5 quantifies over all three. AC-2 and AC-3 hold by construction, so only AC-4 can discriminate: it is the only one with both a null `ChannelFactory` for the back-fill to change and a `Message` to compare. AC-2 produces no findings, so there is no message. AC-3 sets `ChannelFactory` explicitly, and `DispatchBuilder` back-fills only null factories, so both runs are identical by construction. Under either one, the RED mutation below cannot fail the test.
  - **🔁 Characterisation** — green on first run: after task 6 the effective factory is already reduced to a `Type` before comparison or rendering. **RED mutation**: render a provenance clause that depends on the route — `(its own channel factory)` when `subscription.ChannelFactory` is non-null, `(the default channel factory)` otherwise. That is the realistic defect: the message distinguishes a back-filled factory from a resolved default. The test fails on its byte-identical assertion. (An identity-hash mutation would **not** work: the back-fill writes the *same* instance the rule captured, so the hash is equal in both runs.)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm the effective factory is reduced to a `Type` (or ordered list of `Type`s) **before any comparison or rendering happens** — this is what makes invariance hold by construction rather than by coincidence (ADR 0072, Architecture Overview)
    - Never interpolate an instance, an identity hash, or anything that distinguishes a back-filled `subscription.ChannelFactory` from a resolved default into the message
  - Depends on: 6.

- [x] **8. TEST + IMPLEMENT: A correct multi-bus configuration produces no false positives (AC-6, FR-3 combined arm)**
  - **USE COMMAND**: `/test-first when every subscription matches an inner factory of a combined channel factory the rule should report no findings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_combined_channel_factory_can_serve_every_subscription_should_report_no_findings.cs`
  - Test should verify:
    - default factory is `new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()])`
    - two subscriptions, `DeclaringSubscription` named `sub-a` and `NonMatchingSubscription` named `sub-b`, both with `ChannelFactory` null
    - no findings are produced for **either** subscription
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `ResolveCandidates` returns `(Arm.Combined, c.FactoryTypes)` when the effective factory is a `CombinedChannelFactory`
    - `IsCompatible`'s combined arm is `candidates.Any(t => t == declared)` — **exact type equality**, mirroring `CombinedChannelFactory.cs:34/46/59` precisely
    - The rule MUST NOT compare `declared` against `typeof(CombinedChannelFactory)`, and MUST NOT recurse into a nested combined factory
    - This test is **not** a guard for task 2's re-enumeration trap: its collection expression is array-backed and re-enumerates successfully, so a `FactoryTypes` built from the `factories` parameter would still pass here. Task 2's single-pass test is that guard
  - Depends on: 2, 7.

- [x] **9. TEST + IMPLEMENT: A subscription no inner factory can serve is an Error naming the inner factories (AC-7, FR-5 item 3)**
  - **USE COMMAND**: `/test-first when no inner factory of a combined channel factory can serve a subscription the rule should name the inner factories in constructor order`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_no_inner_factory_can_serve_a_subscription_should_name_the_inner_factories.cs`
  - Test should verify:
    - the AC-6 `CombinedChannelFactory` with a plain `Subscription<FakeChannelFactoryRequest>` named `greeting-sub`
    - exactly one `Error` for `greeting-sub`
    - the `Message` contains the display names of **both** inner factories, **in constructor order**
    - the `Message` does **not** name `Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed. This configuration is flat, so the prohibition binds without exception — **D5 concerns only AC-10's nested case and does not relax this**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Build the handed clause from `FactoryTypes` (the inner factories), never from the composite's own type
    - Define the `{F-list}` separator `", "` **once** and share it between the body and the remedy so the two cannot disagree
  - Depends on: 8.

- [x] **10. CHARACTERISE: A user subclass of a channel factory is accepted in the direct arm (AC-8, FR-3)**
  - **USE COMMAND**: `/test-first when the default channel factory is a subclass of the declared type the rule should report no findings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_the_channel_factory_is_a_subclass_of_the_declared_type_should_report_no_findings.cs`
  - Test should verify:
    - default factory is a `DerivedChannelFactory` (deriving from `DeclaredChannelFactory`), subscription is a `DeclaringSubscription` with `ChannelFactory` null
    - no findings are produced
  - **🔁 Characterisation** — green on first run: task 4 already implements the direct arm as `declared.IsAssignableFrom(candidates[0])`. **RED mutation**: change the direct arm to exact equality, `declared == candidates[0]`; the subclass is then flagged and the test fails on its no-findings assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm the direct arm uses assignability deliberately: at runtime such a subclass still satisfies the gateway's downcast of the *subscription*, so flagging it would be a false positive
  - Depends on: 9.

- [x] **11. CHARACTERISE: The same subclass inside a `CombinedChannelFactory` is flagged, mirroring runtime (AC-9)**
  - **USE COMMAND**: `/test-first when a subclass of the declared factory sits inside a combined channel factory the rule should report an Error and the composite should throw`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subclass_of_the_declared_type_is_inside_a_combined_factory_should_report_one_error.cs`
  - Test should verify:
    - default factory is `new CombinedChannelFactory([new DerivedChannelFactory()])`, subscription a `DeclaringSubscription` with `ChannelFactory` null
    - exactly one `Error` is produced
    - **companion assertion**: calling `CreateSyncChannel` on that same `CombinedChannelFactory` with the same subscription throws `ConfigurationException` (it throws at `CombinedChannelFactory.cs:35-38`, before dispatching to any inner factory, so no channel is created and no double's throwing member is reached)
  - **🔁 Characterisation** — green on first run: task 8 already implements the combined arm as exact equality. **RED mutation**: change the combined arm to `candidates.Any(t => declared.IsAssignableFrom(t))`; the subclass is then accepted and the test fails on its one-Error assertion (the companion `ConfigurationException` assertion stays green — it exercises unchanged `CombinedChannelFactory.cs:34-38`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Keep the two arms deliberately asymmetric — exact equality in the combined arm, assignability in the direct arm — because the runtime behaviours they predict differ (OOS-5 forbids "fixing" this by changing `CombinedChannelFactory`)
    - **This companion assertion is the only guard against the rule's combined arm drifting from the composite's routing** (ADR 0072, Risks), and is the price of rejecting a `CanRoute` design
  - Depends on: 10.

- [x] **12. CHARACTERISE: Nested combined factories are not unwrapped (AC-10, FR-3)**
  - **USE COMMAND**: `/test-first when a combined channel factory is nested inside another the rule should not recurse into it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_combined_channel_factory_is_nested_should_not_unwrap_it.cs`
  - Test should verify:
    - default factory is `new CombinedChannelFactory([new CombinedChannelFactory([new DeclaredChannelFactory()])])`, subscription a `DeclaringSubscription`
    - exactly one `Error` is produced, matching the runtime behaviour of `CombinedChannelFactory`, which also fails to route this subscription
    - the criterion asserts the **verdict**, not the wording: per **D5**, the message may name `Paramore.Brighter.CombinedChannelFactory` among the candidate types even though that type does not route — do not add an assertion forbidding it here
  - **🔁 Characterisation** — green on first run: task 8 already implements the combined arm as exact equality over `FactoryTypes` with no recursion. **RED mutation**: make the combined arm treat a nested composite as able to route — `candidates.Any(t => t == declared || typeof(CombinedChannelFactory).IsAssignableFrom(t))`; the nested case then passes and the test fails on its one-Error assertion. (Flattening the nested composite is not writable: the rule sees only `FactoryTypes`, and the inner instances are private to `CombinedChannelFactory`.)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Unwrap **exactly one level**; `FactoryTypes` reports an inner composite by its own concrete type, which is what `{F-list}` then renders (D5)
  - Depends on: 11.

- [x] **13. TEST + IMPLEMENT: A null declared type is a mismatch in the direct arm (AC-10a, FR-3, C-13)**
  - **USE COMMAND**: `/test-first when a subscription overrides ChannelFactoryType to null and the effective factory is not combined the rule should report one Error saying it declares no ChannelFactoryType`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subscription_declares_a_null_channel_factory_type_in_the_direct_arm_should_report_one_error.cs`
  - Test should verify:
    - `NullDeclaringSubscription` named `null-sub`, `ChannelFactory` null, default factory a `DeclaredChannelFactory`
    - exactly one `Error` for `null-sub`
    - the `Message` contains the literal `no ChannelFactoryType` **in place of a declared type name**
    - the `Message` **ends with** the T3a literal `— use a subscription type whose ChannelFactoryType is {F}`, naming `DeclaredChannelFactory`
    - the `Message` contains **no** occurrence of the substring `configure a channel factory of type`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Guard the null with an **explicit `declared is null` test** in `IsCompatible` — never let `declared.IsAssignableFrom(…)` throw into the framework's `"Rule evaluation failed"` path, which would block startup with a useless message and the wrong `Source`. This is a defined input handled in the predicate, not a `catch`
    - Make the body's **declared clause** vary on `D`: non-null → `declares ChannelFactoryType '{D}'`; null → `declares no ChannelFactoryType`. A fixed template would render `declares ChannelFactoryType ''`, which does not contain the required literal
    - Select **T3a**, not T1 — T1 names `{D}` on its "change the configuration" side, which is unrenderable when there is no declared type (FR-5's ordered, total selection table)
  - Depends on: 12.

- [x] **14. TEST + IMPLEMENT: A null declared type is a mismatch in the combined arm (AC-10b, FR-3, FR-1)**
  - **USE COMMAND**: `/test-first when a subscription overrides ChannelFactoryType to null inside a combined factory configuration the rule should report one Error listing the inner factories`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subscription_declares_a_null_channel_factory_type_in_the_combined_arm_should_report_one_error.cs`
  - Test should verify:
    - default factory `new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()])` with a `NullDeclaringSubscription`, `ChannelFactory` null
    - exactly one `Error`
    - the `Message` contains the literal `no ChannelFactoryType`
    - the `Message` **ends with** the T3b literal `— use a subscription type whose ChannelFactoryType is one of: {F-list}`, listing both inner factories' display names in constructor order
    - **companion assertion**: `CreateSyncChannel` on that `CombinedChannelFactory` with the same subscription throws `ConfigurationException`, confirming the rule's verdict matches runtime
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - The combined arm already settles this by FR-3's *iff*: `object.GetType()` never returns null, so no inner factory satisfies the predicate. No extra branch is needed for the verdict — only for the message's declared clause and template selection
    - Note the asymmetry for the release note (task 36): the combined arm is **not** new breakage (it already throws on every start today); only the direct arm is (C-13)
  - Depends on: 13.

- [x] **15. TEST + IMPLEMENT: An empty combined factory yields an actionable remedy (AC-10c, FR-5 template T4)**
  - **USE COMMAND**: `/test-first when a combined channel factory has no inner factories the rule should tell the developer to add one`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_combined_channel_factory_has_no_inner_factories_should_advise_adding_one.cs`
  - Test should verify:
    - default factory `new CombinedChannelFactory([])` with a `DeclaringSubscription` named `empty-sub`
    - exactly one `Error` for `empty-sub`
    - the `Message` **ends with** the literal `— add a channel factory to the combined channel factory`
    - the `Message` contains **no** occurrence of the substring `is one of:`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - **T4 is selected first** in FR-5's ordered table, and is **combined-arm only** — the direct arm's candidate set is always exactly one type and can never be empty
    - The body's **handed clause** gains its empty form, `will be handed no channel factory at all`, so `{F-list}` is never interpolated on either side and the message cannot render `one of ''`
    - `will be handed no channel factory at all` is lower-case prose, not the `ChannelFactory` token — it must continue to satisfy AC-15 (task 24)
  - Depends on: 14.

- [x] **16. TEST + IMPLEMENT: The default in-memory configuration is silent (AC-11, FR-4)**
  - **USE COMMAND**: `/test-first when the configuration resolves to the in-memory channel factory a plain subscription should report no findings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_the_configuration_resolves_to_the_in_memory_channel_factory_should_report_no_findings.cs`
  - Test should verify:
    - a plain `Subscription<FakeChannelFactoryRequest>` with `ChannelFactory` null and default channel factory **null** produces no findings (FR-2 step 3 → direct arm, `typeof(InMemoryChannelFactory)` is assignable from itself)
    - the same holds when the default is explicitly `new InMemoryChannelFactory(new InternalBus(), TimeProvider.System)` — the same type `ServiceCollectionExtensions.cs:159` substitutes
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `ResolveCandidates` returns `(Arm.Direct, [typeof(InMemoryChannelFactory)])` when both the subscription's factory and the captured default are null
    - FR-2 step 3 must be `typeof(InMemoryChannelFactory)` — a **`typeof`, not a `new`**; it must never construct an `InMemoryChannelFactory` or an `InternalBus`
  - Depends on: 15.

- [x] **17. CHARACTERISE: A subscription with a null `RequestType` is still checked (AC-19, C-7)**
  - **USE COMMAND**: `/test-first when a subscription has a null RequestType the channel factory rule should still evaluate it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_a_subscription_has_a_null_request_type_should_still_check_the_channel_factory.cs`
  - Test should verify:
    - a `DeclaringSubscription` constructed with `getRequestType:` a mapping function **and** `messagePumpType: MessagePumpType.Proactor` — both are required or the base `Subscription` constructor throws `ConfigurationException` (`Subscription.cs:213`) — such that `RequestType` is null
    - handed a `NonMatchingChannelFactory`
    - exactly one `Error` is produced — the rule does **not** vacuously skip datatype-channel subscriptions, unlike `PumpHandlerMatch`, `HandlerRegistered` and `UnwrapTransformResolvable`
  - **🔁 Characterisation** — green on first run: the rule reads no `RequestType`. **RED mutation**: add `if (s.RequestType is null) return true;` at the head of the predicate; the finding disappears and the test fails on its one-Error assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm the rule body touches `RequestType` nowhere at all; there is no guard to add, only a guard to *not* add
  - Depends on: 16.

---

## Phase 3: The finding message

*ADR 0072 steps 2-3; `Core.Tests`; AC-12 to AC-15 and AC-30.*

`DisplayName` is a **private static on `ConsumerValidationRules`** (ADR 0072 §3): a formatter with one caller does not earn permanent public surface, and `TypeExtensions`/`ReflectionExtensions` are both `internal` to core. Only the five remedy literals are normative; the body wording is ADR 0072's, revisable only while AC-5, AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14, AC-15 and AC-30 all continue to hold.

- [x] **18. TEST + IMPLEMENT: The message names the offending subscription's own type (AC-12, FR-5 item 1)**
  - **USE COMMAND**: `/test-first when reporting a channel factory mismatch the message should name the subscription's own runtime type as a display name`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_reporting_a_channel_factory_mismatch_should_name_the_subscription_type.cs`
  - Test should verify:
    - the AC-1 configuration
    - the `Message` contains the literal `Paramore.Brighter.Subscription<Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest>`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement `DisplayName(Type t)`: non-generic → `t.FullName ?? t.Name`; generic → strip at the `` ` `` from `t.GetGenericTypeDefinition().FullName`, then `"<" + join(", ", t.GetGenericArguments().Select(DisplayName)) + ">"`
    - Render the subscription's own runtime type, the declared type and the handed type(s) all through `DisplayName`
    - Known and accepted simplification: nested types render with the CLR `+` separator. No criterion exercises one — `AlphaBus`/`BetaBus` are *namespaces*, not nested types
  - Depends on: 17.

- [x] **19. TEST + IMPLEMENT: The two-way remedy, where both directions are legitimate (AC-13, template T1)**
  - **USE COMMAND**: `/test-first when both remedies are legitimate the direct arm message should offer both directions`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_both_remedies_are_legitimate_should_offer_both_directions.cs`
  - Test should verify:
    - the AC-4 configuration — a `DeclaringSubscription` (so `D == typeof(DeclaredChannelFactory)`, **not** the in-memory default) resolving to a `NonMatchingChannelFactory`
    - the `Message` **ends with** `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is {F}`, with `{D}` the display name of `DeclaredChannelFactory` and `{F}` that of `NonMatchingChannelFactory`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - T1's condition per FR-5's ordered table: direct arm, `D` not null, `D != typeof(InMemoryChannelFactory)`
    - Append the remedy **last**, so every "ends with" assertion in this phase and in tasks 13-15 holds
    - The remedy is **asymmetric** by design: `{D}` on the "change the configuration" side, `{F}` on the "change the subscription" side
  - Depends on: 18.

- [x] **20. CHARACTERISE: The in-memory case offers only the direction that fixes the defect (AC-13a, template T3a)**
  - **USE COMMAND**: `/test-first when the declared type is the in-memory channel factory the message should offer only the subscription-side remedy`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_the_declared_type_is_the_in_memory_factory_should_offer_only_the_subscription_remedy.cs`
  - Test should verify:
    - the AC-1 configuration — a plain `Subscription<FakeChannelFactoryRequest>` (so `D == typeof(InMemoryChannelFactory)`) handed a `DeclaredChannelFactory`
    - the `Message` **ends with** `— use a subscription type whose ChannelFactoryType is {F}`, naming `DeclaredChannelFactory`
    - the `Message` contains **no** occurrence of the substring `configure a channel factory of type` — the prohibition is on the whole message, so the body must not paraphrase the suppressed half in other words either
  - **🔁 Characterisation (conditional)** — if tasks 13 and 19 already route `D == typeof(InMemoryChannelFactory)` to T3a (task 19's T1 condition excludes it), this test is green on first run. **RED mutation**: drop the `D != typeof(InMemoryChannelFactory)` conjunct from T1's condition and order T1 before T3a; the message then ends with T1's two-way remedy, so the test fails on its **ends-with** assertion (its no-occurrence assertion would fail too). If the test fails on first run, it is simply RED.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - T3a's condition: direct arm and (`D == typeof(InMemoryChannelFactory)` **or** `D is null`) — evaluated **before** T1 in FR-5's ordered table
    - The suppression is architectural, not cosmetic: "configure a channel factory of type `Paramore.Brighter.InMemoryChannelFactory`" is the cheaper of the two remedies in the case this feature fires most often, and following it produces the silent-wrong-bus consumer C-2 exists to prevent
  - Depends on: 19.

- [ ] **21. TEST + IMPLEMENT: The combined arm lists the alternatives (AC-13b, template T2)**
  - **USE COMMAND**: `/test-first when the combined arm has a declared transport type the message should list the inner factories as alternatives`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_the_combined_arm_has_a_declared_transport_type_should_list_the_alternatives.cs`
  - Test should verify:
    - default factory `new CombinedChannelFactory([new NonMatchingChannelFactory(), new AlphaBus.ChannelFactory()])` with a `DeclaringSubscription` named `sub-a`, `ChannelFactory` null — **stated in full and deliberately not inherited from AC-7**, whose inner set contains `DeclaredChannelFactory` and would therefore *match* and produce no finding at all
    - the `Message` **ends with** `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is one of: {F-list}`, with `{F-list}` the inner factories' display names in **constructor order**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - T2's condition: combined arm, non-empty candidate set, `D` not null, `D != typeof(InMemoryChannelFactory)`
    - "is one of" rather than "is" is deliberate — a subscription declares exactly one type, a combined factory offers several
  - Depends on: 20.

- [ ] **22. CHARACTERISE: The combined arm also suppresses the in-memory half (AC-13c, template T3b)**
  - **USE COMMAND**: `/test-first when the combined arm sees a subscription declaring the in-memory factory the message should suppress the configuration-side remedy`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_the_combined_arm_declares_the_in_memory_factory_should_suppress_the_configuration_remedy.cs`
  - Test should verify:
    - the AC-7 configuration — the AC-6 `CombinedChannelFactory` with a plain `Subscription<FakeChannelFactoryRequest>`
    - the `Message` **ends with** `— use a subscription type whose ChannelFactoryType is one of: {F-list}`, listing both inner factories' display names in constructor order
    - the `Message` contains **no** occurrence of the substring `configure a channel factory of type`
  - **🔁 Characterisation (conditional)** — if tasks 14 and 21 already route the in-memory combined case to T3b, this test is green on first run. **RED mutation**: drop the `D != typeof(InMemoryChannelFactory)` conjunct from T2's condition and order T2 before T3b; the message then ends with T2's two-way remedy, so the test fails on its **ends-with** assertion (its no-occurrence assertion would fail too). If the test fails on first run, it is simply RED.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - T3b's condition: combined arm, **non-empty** candidate set, and (`D == typeof(InMemoryChannelFactory)` or `D is null`) — evaluated after T4 and before T2
    - With tasks 13, 15, 19, 20, 21 this completes all **five** literals and confirms the selection conditions are **ordered and total** over the input space
  - Depends on: 21.

- [ ] **23. CHARACTERISE: Display names carry no assembly identity (AC-14, FR-5 display format)**
  - **USE COMMAND**: `/test-first when rendering a closed generic subscription type the message should carry no assembly identity or arity suffix`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_rendering_a_closed_generic_subscription_type_should_carry_no_assembly_identity.cs`
  - Test should verify:
    - any finding produced for a closed generic subscription
    - the `Message` contains no occurrence of `Version=`, `Culture=` or `PublicKeyToken=`
    - the `Message` contains no backtick-arity suffix such as `` `1 ``
  - **🔁 Characterisation** — green on first run: task 18 already implements generic stripping in `DisplayName`. **RED mutation**: make `DisplayName` return `t.FullName ?? t.Name` for generic types too; the closed generic's `FullName` carries `` `1 `` and `Version=…`, and the test fails on both no-occurrence assertions.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm `DisplayName` strips at the `` ` `` of the **generic type definition's** `FullName` and recurses into type arguments, rather than using the closed type's `FullName` directly
  - Depends on: 22.

- [ ] **24. CHARACTERISE: Same-named factory types in different namespaces are distinguishable (AC-15, FR-5 items 2-3)**
  - **USE COMMAND**: `/test-first when two channel factories share a simple name the message should render both namespace-qualified and use no bare ChannelFactory token`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_two_channel_factories_share_a_simple_name_should_render_namespace_qualified_names.cs`
  - Test should verify:
    - an `AlphaBus.AlphaSubscription` (declaring `…TestDoubles.AlphaBus.ChannelFactory`) handed a `…TestDoubles.BetaBus.ChannelFactory`
    - the `Message` contains **both** namespace-qualified display names in full
    - **the assertion is normative as a regex**: `Regex.Matches(message, @"(?<![.\w])ChannelFactory(?!Type)")` MUST find **no match**. Writing it any other way — for example `Message.Contains("ChannelFactory")` — is a defect in the test, not in the message
    - this regex is asserted in **`Core.Tests` only**; no AC-25/AC-26 gateway criterion renders a `Message` for it to run against
  - **🔁 Characterisation (conditional)** — whether this is green on first run depends only on the body wording tasks 3-23 produced. **RED mutation**: render the handed type with `Type.Name` instead of `DisplayName`; the bare `ChannelFactory` token appears and the test fails on its regex assertion. If the test fails on first run, it is simply RED.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - "Token" is a word-boundary match: a composite identifier merely *ending* in the word — `InMemoryChannelFactory`, `CombinedChannelFactory`, `DeclaredChannelFactory` — is a different token and the body may render any of them freely
    - The `(?!Type)` carve-out is required because FR-5's remedy clause contains `ChannelFactoryType` preceded by a space; without it AC-13 and AC-15 could not both pass
  - Depends on: 23.

- [ ] **25. CHARACTERISE: Findings are one-per-subscription, ordered and deterministic (AC-30, FR-13)**
  - **USE COMMAND**: `/test-first when the same configuration is evaluated twice the rule should produce identical ordered findings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation"
  - Test file: `When_evaluating_the_same_configuration_twice_should_produce_identical_ordered_findings.cs`
  - Test should verify:
    - two mismatched subscriptions — `sub-a` a `DeclaringSubscription` and `sub-b` a `NonMatchingSubscription`, in that order — evaluated against a `CombinedChannelFactory` whose three inner factories are `[DerivedChannelFactory, AlphaBus.ChannelFactory, BetaBus.ChannelFactory]` (chosen so neither subscription matches)
    - evaluation is driven through **`PipelineValidator`** — constructed with the subscriptions `[sub-a, sub-b]` and consumer specs `[ConsumerValidationRules.ChannelFactoryCompatible(<that CombinedChannelFactory>)]` — and **not** by calling `IsSatisfiedBy` on each subscription from the test. A test that calls the rule per subscription imposes the order it then asserts, and that assertion cannot fail. Construct it as `tests/Paramore.Brighter.Core.Tests/Validation/When_validator_finds_errors_across_paths_should_aggregate_all.cs:65` does, `new PipelineValidator(pipelineBuilder, publications: null, subscriptions, consumerSpecs)`, with a `PipelineBuilder` over an empty handler registry so the handler path contributes no findings
    - `Validate()` is run **twice**
    - each run produces exactly **two** findings — one per subscription — in the order `sub-a`, `sub-b`
    - the messages are **byte-identical** across the two runs
  - **🔁 Characterisation** — expected green on first run: tasks 3-24 already emit at most one finding with no instance-derived content, and `PipelineValidator.EvaluateSpecs` already iterates subscriptions outermost. **RED mutations**, applied and reverted one at a time: (a) iterate `entities.Reverse()` in `EvaluateSpecs` — the test fails on its `sub-a`, `sub-b` ordering assertion; (b) append `Guid.NewGuid()` to the rule's message — the test fails on its byte-identical assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Emit **at most one** finding per subscription regardless of how many candidates were inspected
    - Subscription ordering is supplied by the framework's subscription-outer/spec-inner loop in `PipelineValidator.EvaluateSpecs`; inner-factory ordering by `FactoryTypes`'s constructor order. Neither is the rule's own responsibility to impose, but both must be preserved
    - Confirm nothing instance-derived leaks into the message — the same discipline task 7 asserts across the back-fill, asserted here across two runs
  - Depends on: 24.

---

## Phase 4: Registration and host start

*ADR 0072 step 4; `Extensions.Tests`.*

`tests/Paramore.Brighter.Core.Tests` references neither `ServiceActivator.Extensions.DependencyInjection` nor any `MessagingGateway.*` assembly, so host-start behaviour lives in `tests/Paramore.Brighter.Extensions.Tests`, which already references what is needed.

- [ ] **26. STRUCTURAL: Extensions.Tests declares its own copies of the doubles (C-9)**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/`
  - `Paramore.Brighter.Extensions.Tests` does **not** reference `Paramore.Brighter.Core.Tests` — test projects here do not reference one another — so it MUST declare its **own** copies of the doubles it needs, in its **own** namespace, with their **own** distinct request types. This duplication is deliberate and is stated so it is not discovered mid-task.
  - Needed for tasks 27-30: a declaring subscription double, a declared channel factory double, a non-matching channel factory double, and one or two local request types (one per file). All channel factory members throw, as in task 1.
  - **Why no test of its own**: fixtures with no behaviour; consumed by tasks 27-30.
  - Depends on: 1 (as the shape to copy).

- [ ] **27. TEST + IMPLEMENT: A channel factory mismatch blocks startup under `throwOnError: true` (AC-16, FR-6) — and the rule is registered**
  - **USE COMMAND**: `/test-first when a channel factory mismatch is validated with throwOnError true the host should fail to start and report the mismatch`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_channel_factory_mismatch_is_validated_with_throw_on_error_should_fail_startup.cs`
  - Test should verify:
    - a host configured with `AddConsumers` containing one mismatched subscription (the AC-1 shape, using this project's own doubles) and `ValidatePipelines(throwOnError: true)`
    - startup **fails**
    - the reported findings include the mismatch `Error`, with the expected `Source` and message
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Append a **fifth** `services.AddSingleton<ISpecification<Subscription>>(…)` to `RegisterConsumerValidationSpecs` in `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:199-229`:
      `sp => ConsumerValidationRules.ChannelFactoryCompatible(sp.GetService<IAmConsumerOptions>()?.DefaultChannelFactory)`
    - Pass the **default factory instance, not the options object** (the C-5 decision) — `IAmConsumerOptions` also carries `Subscriptions`, `InboxConfiguration`, `InstrumentationOptions` and `ShutdownTimeout`, and handing the whole role to a rule that needs one member invites it to iterate `Subscriptions`, which is `PipelineValidator.ValidateConsumers`'s job
    - Use `GetService`, **not** `GetRequiredService` — an absent registration degrades to FR-2 step 3, the same fallback a null `DefaultChannelFactory` takes
    - Change nothing about the four existing registrations
  - Depends on: 25, 26.

- [ ] **28. CHARACTERISE: The same configuration does not block under `throwOnError: false` (AC-17, FR-6)**
  - **USE COMMAND**: `/test-first when a channel factory mismatch is validated with throwOnError false the host should start and still report the mismatch`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_channel_factory_mismatch_is_validated_without_throw_on_error_should_start_and_report.cs`
  - Test should verify:
    - the AC-16 configuration with `ValidatePipelines(throwOnError: false)`
    - the host starts successfully
    - the mismatch `Error` is present in the validation results
  - **🔁 Characterisation** — green on first run: task 27's registration already makes the finding visible and the non-blocking semantics already exist. **RED mutation**: comment out the fifth `ISpecification<Subscription>` registration task 27 added; the host still starts but the mismatch `Error` is absent, so the test fails on its reported-finding assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Require no production change beyond task 27 — the blocking semantics belong to `BrighterValidationHostedService`, unchanged. If a change is needed here, the rule has taken on a responsibility that is not its own
    - This is the workaround the C-10, C-11 and C-13 release notes point at (task 36) — and the one C-12 explicitly does **not** rescue
  - Depends on: 27.

- [ ] **29. CHARACTERISE: A disabled validation run evaluates nothing (AC-17a, FR-6, NFR-4)**
  - **USE COMMAND**: `/test-first when pipeline validation is disabled the host should start and produce no validation results at all`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_pipeline_validation_is_disabled_should_evaluate_no_rules.cs`
  - Test should verify:
    - the mismatched configuration of AC-16 with `ValidatePipelines(enabled: false, throwOnError: false)` — `throwOnError: false` so that, under the RED mutation, the validator runs and the test fails on its no-results assertion rather than at host start
    - the host starts successfully
    - **no** validation results are produced by any rule
  - **🔁 Characterisation** — green on first run: disabled validation already registers nothing. **RED mutation**: remove the `enabled == false` early return in `BrighterPipelineValidationExtensions.cs:58-60`; the validator then runs, results are produced, and the test fails on its no-results assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Require **no guard inside the rule**. `BrighterPipelineValidationExtensions.cs:58-60` returns the builder untouched when `enabled` is false, so the `IAmAPipelineValidator` factory is never registered, `sp.GetServices<ISpecification<Subscription>>()` is never called, and the spec factory lambda never runs — the rule is never even constructed. Zero cost by construction, not by a flag check
  - Depends on: 28.

- [ ] **30. CHARACTERISE: The four existing consumer rules are unaffected (AC-18, FR-6)**
  - **USE COMMAND**: `/test-first when a handler is missing and the channel factory matches only the handler rule should report a finding`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests"
  - Test file: `When_a_handler_is_missing_and_the_channel_factory_matches_should_report_only_the_handler_error.cs`
  - Test should verify:
    - a subscription whose `RequestType` has no registered handler and whose channel factory is **correctly matched**
    - under `ValidatePipelines(throwOnError: true)` the `HandlerRegistered` rule still produces **exactly one** `Error` with its existing message
    - this feature contributes **no additional finding**
  - **🔁 Characterisation** — green on first run: the rule is already silent on a matched factory. **RED mutation**: make `ChannelFactoryCompatible`'s predicate return `false` unconditionally; a second finding appears and the test fails on its exactly-one-finding assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm the addition is purely additive: no change to the severity, `Source`, `Message` or blocking behaviour of `PumpHandlerMatch`, `HandlerRegistered`, `RequestTypeSubtype` or `UnwrapTransformResolvable`, and no change to `ValidatePipelines(enabled = true, throwOnError = true)`'s defaults
  - Depends on: 29.

---

## Phase 5: Transport corrections

*ADR 0072 step 5.* **Prerequisite for phases 10 and 12.**

One task per transport. Each bundles that transport's one-line `ChannelFactoryType` override with its own AC-20/21/22/23/24, its AC-25x, its AC-26x and — only in `AWS.Tests`, `AWS.V4.Tests` and `MQTT.Tests`, the three of the five that reference `Paramore.Brighter.ServiceActivator` — its AC-26f. `TestRequest` is a **placeholder**: each gateway test project supplies its own request type, declared locally, one per file (C-9). No type named `TestRequest` exists in the repository and none is to be created centrally.

**AC-26 asserts the routing *decision*, never channel creation.** Construct the transport's channel factory and evaluate `f.GetType() == subscription.ChannelFactoryType`; do **not** call `CreateSyncChannel`/`CreateAsyncChannel`, which would open a real connection (MQTT connects in `MqttMessageConsumer`'s constructor; `GcpPubSubChannelFactory` calls `EnsureSubscriptionExistsAsync`) and breach NFR-3.

- [ ] **31. TEST + IMPLEMENT: `GcpPubSubSubscription` declares the GCP Pub/Sub channel factory (FR-7 — AC-20, AC-25a, AC-26a)**
  - **USE COMMAND**: `/test-first when reading the channel factory type of a GCP Pub/Sub subscription it should be the GCP Pub/Sub channel factory and a combined factory should route it`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway"
  - Test files:
    - `When_reading_the_channel_factory_type_of_a_gcp_pubsub_subscription_should_be_the_gcp_pubsub_channel_factory.cs` (AC-20)
    - `When_checking_the_gcp_pubsub_declared_channel_factory_should_be_a_real_channel_factory.cs` (AC-25a)
    - `When_matching_a_gcp_pubsub_subscription_against_a_combined_factory_should_select_one_inner_factory.cs` (AC-26a)
    - plus a locally declared request type in its own file (the `TestRequest` placeholder) if no suitable one already exists in that project
  - Test should verify:
    - AC-20: `new GcpPubSubSubscription<TestRequest>(new SubscriptionName("t"), new ChannelName("t"), new RoutingKey("t"), messagePumpType: MessagePumpType.Proactor).ChannelFactoryType == typeof(GcpPubSubChannelFactory)`. The three positional arguments **and** the explicit pump type are both required: `GcpPubSubSubscription<T>` defaults `messagePumpType` to `Unknown` (`GcpPubSubSubscription.cs:164-180`) and `Subscription.cs:213` rejects it
    - AC-25a: `typeof(IAmAChannelFactory).IsAssignableFrom(type)` is true **and** the type is not `typeof(InMemoryChannelFactory)`
    - AC-26a: with a `CombinedChannelFactory` constructed over an instance of `GcpPubSubChannelFactory` (construction only), exactly **one** inner factory satisfies `f.GetType() == subscription.ChannelFactoryType`, where before the correction **none** did
    - **No AC-26f here**: `Paramore.Brighter.Gcp.Tests` does not reference `Paramore.Brighter.ServiceActivator`, so it cannot evaluate the rule. The rule-produces-no-findings clause for GCP is covered in `Core.Tests` by tasks 4 and 8 — the rule's logic is transport-agnostic and the transport-specific part is the declared type, which AC-25a pins
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubSubscription.cs:108` — return `typeof(GcpPubSubChannelFactory)` instead of `typeof(GcpPubSubConsumerFactory)`. `GcpPubSubConsumerFactory` implements `IAmAMessageConsumerFactory` (`GcpPubSubConsumerFactory.cs:15`), not `IAmAChannelFactory`, so the current value can never match any registered channel factory
    - Keep it an **expression-bodied `typeof(...)`** on the non-generic base — ADR 0073's sweep reads it from an uninitialised instance
  - Depends on: 30.

- [ ] **32. TEST + IMPLEMENT: `MqttSubscription` declares the MQTT channel factory (FR-8 — AC-21, AC-25b, AC-26b, AC-26f)**
  - **USE COMMAND**: `/test-first when reading the channel factory type of an MQTT subscription it should be the MQTT channel factory and the rule should report no findings`
  - Test location: "tests/Paramore.Brighter.MQTT.Tests/MessagingGateway"
  - Test files:
    - `When_reading_the_channel_factory_type_of_an_mqtt_subscription_should_be_the_mqtt_channel_factory.cs` (AC-21)
    - `When_checking_the_mqtt_declared_channel_factory_should_be_a_real_channel_factory.cs` (AC-25b)
    - `When_matching_an_mqtt_subscription_against_a_combined_factory_should_select_one_inner_factory.cs` (AC-26b)
    - `When_validating_a_corrected_mqtt_subscription_should_report_no_findings.cs` (AC-26f)
    - plus a locally declared request type in its own file
  - Test should verify:
    - AC-21: `new MqttSubscription<TestRequest>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)`. No explicit `messagePumpType` is needed — `MqttSubscription.cs:132` already defaults to `Proactor`
    - AC-25b and AC-26b as in task 31
    - AC-26f: with the corrected subscription and a `CombinedChannelFactory` containing the real MQTT `ChannelFactory` (construction only — no channel created, no broker contacted), the rule produces **no findings**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.MessagingGateway.MQTT/MqttSubscription.cs:35` — return `typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)` instead of `typeof(MqttMessageConsumerFactory)`, which implements `IAmAMessageConsumerFactory` (`MqttMessageConsumerFactory.cs:30`)
    - Note for the release notes (task 36): MQTT is the one transport whose `ChannelFactory` accepts *any* `Subscription` (`MQTT/ChannelFactory.cs:32-73` passes it through untouched), so a plain `Subscription<T>` consumes MQTT **correctly today** and becomes an `Error` after this change — that is **C-11**
  - Depends on: 31.

- [ ] **33. TEST + IMPLEMENT: `SqsSubscription` (AWSSQS) declares its channel factory (FR-9 — AC-22, AC-25c, AC-26c, AC-26f)**
  - **USE COMMAND**: `/test-first when reading the channel factory type of an AWS SQS subscription it should be the AWS SQS channel factory and the rule should report no findings`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway"
  - Test files:
    - `When_reading_the_channel_factory_type_of_an_sqs_subscription_should_be_the_sqs_channel_factory.cs` (AC-22)
    - `When_checking_the_sqs_declared_channel_factory_should_be_a_real_channel_factory.cs` (AC-25c)
    - `When_matching_an_sqs_subscription_against_a_combined_factory_should_select_one_inner_factory.cs` (AC-26c)
    - `When_validating_a_corrected_sqs_subscription_should_report_no_findings.cs` (AC-26f)
    - plus a locally declared request type in its own file
  - Test should verify:
    - AC-22: `new SqsSubscription<TestRequest>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory)`. No explicit `messagePumpType` — `SqsSubscription.cs:201` already defaults to `Proactor`
    - AC-25c, AC-26c, AC-26f as above; the AWS channel factory must be constructed without contacting AWS
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsSubscription.cs` — **add** `public override Type ChannelFactoryType => typeof(ChannelFactory);` (the AWSSQS `ChannelFactory`, `ChannelFactory.cs:44`). It declares no override today and so inherits `typeof(InMemoryChannelFactory)`, which no AWS SQS configuration can satisfy. That factory downcasts (`ChannelFactory.cs:98`, `:208`), so the mismatch is a genuine runtime failure
    - Note for the release notes (task 36): this is one of the three corrections that cause **C-12**, the exception `ValidatePipelines(throwOnError: false)` does **not** rescue
  - Depends on: 32.

- [ ] **34. TEST + IMPLEMENT: `SqsSubscription` (AWSSQS.V4) declares its channel factory (FR-10 — AC-23, AC-25d, AC-26d, AC-26f)**
  - **USE COMMAND**: `/test-first when reading the channel factory type of an AWS SQS V4 subscription it should be the V4 channel factory and the rule should report no findings`
  - Test location: "tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway"
  - Test files: the four equivalents of task 33's, named for the V4 assembly, plus a locally declared request type
  - Test should verify:
    - AC-23: `new SqsSubscription<TestRequest>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory)`
    - AC-25d, AC-26d, AC-26f as above
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsSubscription.cs` — add the equivalent override naming the V4 `ChannelFactory` (`ChannelFactory.cs:44`; downcasts at `:98`, `:208`)
    - **The two AWS packages MUST be corrected together** — correcting only one would leave the V3/V4 pair inconsistent (FR-10)
  - Depends on: 33.

- [ ] **35. TEST + IMPLEMENT: `PostgresSubscription` declares its channel factory (FR-11 — AC-24, AC-25e, AC-26e)**
  - **USE COMMAND**: `/test-first when reading the channel factory type of a Postgres subscription it should be the Postgres channel factory and a combined factory should route it`
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway"
  - Test files:
    - `When_reading_the_channel_factory_type_of_a_postgres_subscription_should_be_the_postgres_channel_factory.cs` (AC-24)
    - `When_checking_the_postgres_declared_channel_factory_should_be_a_real_channel_factory.cs` (AC-25e)
    - `When_matching_a_postgres_subscription_against_a_combined_factory_should_select_one_inner_factory.cs` (AC-26e)
    - plus a locally declared request type in its own file
  - Test should verify:
    - AC-24: `new PostgresSubscription<TestRequest>(new SubscriptionName("t"), new ChannelName("t"), new RoutingKey("t"), messagePumpType: MessagePumpType.Proactor).ChannelFactoryType == typeof(PostgresChannelFactory)` — the explicit pump type is required, `PostgresSubscription<T>` defaulting to `Unknown` (`PostgresSubscription.cs:146-158`)
    - AC-25e and AC-26e as above
    - **No AC-26f here**: `Paramore.Brighter.PostgresSQL.Tests` does not reference `Paramore.Brighter.ServiceActivator`. Covered equivalently in `Core.Tests` by tasks 4 and 8
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `src/Paramore.Brighter.MessagingGateway.Postgres/PostgresSubscription.cs` — add `public override Type ChannelFactoryType => typeof(PostgresChannelFactory);` (`PostgresChannelFactory.cs:11`; downcasts at `:18`, `:39`, `:60`)
    - **After this task all five corrections are in place**, which is the precondition for task 53's generated sweeps being green and for task 55 enabling them in CI
  - Depends on: 34.

---

## Phase 6: Release notes

*ADR 0072 step 6.*

- [ ] **36. DOC: Release notes for C-8's obligations — four separate breaking-change notes**
  - File: `release_notes.md`, under `## Master`
  - Write, in British spelling (NFR-7):
    - A summary entry for the feature: the fifth consumer validation rule, its `Error` severity, and the fact that FR-7 to FR-11 make AWS SQS, AWS SQS V4 and Postgres subscriptions routable by `CombinedChannelFactory` for the first time. Reference [ADR 0072], [ADR 0073] and this spec.
    - **C-10** — out-of-repo `Subscription` subclasses that declare no `ChannelFactoryType` override. Symptom: an `Error` from `ValidatePipelines` citing `Paramore.Brighter.InMemoryChannelFactory` as the declared type. Remedy: `public override Type ChannelFactoryType => typeof(MyChannelFactory);`. Interim workaround: `ValidatePipelines(throwOnError: false)`.
    - **C-11** — a plain `Subscription<T>` used with MQTT, which **works today**. Symptom: an `Error` citing `InMemoryChannelFactory` as the declared type against `Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory`. Remedy: use `MqttSubscription<T>`. Suppressible with `throwOnError: false`.
    - **C-12** — AWS SQS, AWS SQS V4 or Postgres subscriptions routed through an in-memory inner factory of a `CombinedChannelFactory`. Symptom: a **`ConfigurationException` at Dispatcher start**, *not* a validation finding. Remedy: add the transport's real channel factory to the `CombinedChannelFactory`, or stop relying on the in-memory route. **MUST state explicitly that `ValidatePipelines(throwOnError: false)` does NOT avoid this** — it is caused by the corrections, not the rule, and is a routing change rather than a validation verdict.
    - **C-13** — an out-of-repo `ChannelFactoryType` override returning `null` in a single-factory (non-combined) configuration. Symptom: a startup `Error` reading `declares no ChannelFactoryType`. Remedy: return a real channel factory type from the override. **MUST state that, unlike C-12, this one *is* suppressible with `throwOnError: false`.**
  - **Why no test of its own**: documentation. The wording it quotes is pinned by tasks 13, 19-22 (the remedy literals) and task 20's `no ChannelFactoryType` body clause; if the notes and the tests disagree, the tests are authoritative.
  - References: C-8, C-10, C-11, C-12, C-13; ADR 0072 *Implementation Approach* step 6.
  - Depends on: 35 (the corrections and therefore C-12 must be real), 13, 19-22 (the literals to quote).

---

## Phase 7: The declaration predicate

*ADR 0073 step 1 — `Check`.*

New public static class `SubscriptionChannelFactoryDeclaration` in namespace `Paramore.Brighter`, in `src/Paramore.Brighter`. It has **exactly two public members** and is the **one** new public type FR-12 permits — no result record, no type-scoped `Sweep` overload, no third member added later so a test can avoid a `Single(...)`.

- [ ] **37. STRUCTURAL: the `ChannelFactoryDeclaration` test-double folder and its two helper types**
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration/TestDoubles/`
  - These live **beside** the existing `CombinedChannelFactory` and channel tests and are deliberately **not** in `Validation/TestDoubles/`, whose double set C-9 declares closed for the rule's own criteria.
  - Add, one class per file:
    - a **non-factory marker type** implementing nothing, for the AC-28 not-a-channel-factory double to declare
    - one **sound `IAmAChannelFactory` double**, for the AC-29 double and the subsumption base to declare. C-9's throw-rule **does** apply to this one: `CreateSyncChannel`, `CreateAsyncChannel` and `CreateAsyncChannelAsync` must all throw
  - **Why no test of its own**: fixtures with no behaviour; consumed by tasks 38-48.
  - References: ADR 0073 *Hand-written cases in `Core.Tests`* — "Two helper types they declare, named here because the design depends on both".
  - Depends on: nothing (may start in parallel with phases 2-6).

- [ ] **38. TEST + IMPLEMENT: `Check` rejects a declaration that is not a channel factory (AC-28, not-a-channel-factory branch)**
  - **USE COMMAND**: `/test-first when a subscription declares a type that is not a channel factory the declaration check should report a reason naming it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_subscription_declares_a_type_that_is_not_a_channel_factory_should_report_a_reason.cs`
  - Test should verify:
    - a synthetic `Subscription` subclass declared in `TestDoubles/` that overrides `ChannelFactoryType` with the **non-factory marker type** from task 37
    - `Check` is called with **both arguments written literally** — this is the Evident Data the AC-28 assertions are about
    - the returned reason is non-null, names the **offending subscription type** and the **type it declared**, both by `Type.FullName`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Create `public static class SubscriptionChannelFactoryDeclaration` in `src/Paramore.Brighter/SubscriptionChannelFactoryDeclaration.cs`
    - Add `public static string? Check(Type subscriptionType, Type? declaredFactoryType)` — null means **sound**, otherwise the single reason it is not
    - Implement the **not-a-channel-factory** branch: `!typeof(IAmAChannelFactory).IsAssignableFrom(declaredFactoryType)` → *"… declares `{full name}`, which does not implement `Paramore.Brighter.IAmAChannelFactory`."*
    - Render every type with `Type.FullName`, not `Type.Name` — eight transports name their channel factory class `ChannelFactory`
    - XML documentation in British spelling (NFR-7); the branches are **named, not numbered**, because FR-12 numbers its own two conditions differently
    - Pure: no reflection beyond `IsAssignableFrom`, no machinery yet
  - Depends on: 37.

- [ ] **39. TEST + IMPLEMENT: `Check` rejects a subscription that inherits the in-memory default (AC-28, inherited-default branch)**
  - **USE COMMAND**: `/test-first when a subscription inherits the in-memory channel factory default the declaration check should report a reason`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_subscription_inherits_the_in_memory_channel_factory_default_should_report_a_reason.cs`
  - Test should verify:
    - a synthetic `Subscription` subclass declared in `TestDoubles/` with **no** `ChannelFactoryType` override, so it inherits `typeof(InMemoryChannelFactory)`
    - `Check` called with literal arguments returns a non-null reason naming the subscription type and `Paramore.Brighter.InMemoryChannelFactory`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the **inherited-default** branch: `declaredFactoryType == typeof(InMemoryChannelFactory)` → *"… declares `Paramore.Brighter.InMemoryChannelFactory`. A shipped gateway subscription must declare its own transport's channel factory; a type that does not override `ChannelFactoryType` inherits this default."*
    - **This is FR-12 condition 2, and it is what catches the AWS SQS / Postgres class of defect** — a guard that only checked types *declaring* an override would pass over them vacuously
    - Accepted duplication, recorded: this judgement and ADR 0072's T3a/T3b case both turn on `D == typeof(InMemoryChannelFactory)`. 0072's rule is **not** expected to consume `Check` — it answers a different question and carries severity and message obligations `Check` has none of. A change to what "inherited default" means must be made in both places
  - Depends on: 38.

- [ ] **40. TEST + IMPLEMENT: `Check` rejects a declaration that is null (ADR 0073's own branch, C-13)**
  - **USE COMMAND**: `/test-first when a subscription declares no channel factory type at all the declaration check should report a reason`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_subscription_declares_no_channel_factory_type_should_report_a_reason.cs`
  - Test should verify:
    - `Check(typeof(<some subscription double>), null)` returns a reason of the form *"… declares no channel factory type (`ChannelFactoryType` returned null)."*
    - the reason **contains** `returned null` **and does not contain** `does not implement`
    - **What makes this fail before the implementation**: `typeof(IAmAChannelFactory).IsAssignableFrom(null)` is `false`, so after tasks 38-39 a null declaration is already rejected — but mis-routed into the **not-a-channel-factory** branch. `Assert.NotNull` would therefore pass on arrival; the two wording assertions are what fail, and a null declaration landing in the wrong branch is the realistic defect, not one reported as sound
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the **null** branch as the first of the three, evaluated in order, first failure wins — its position is observed through the reason's wording, not asserted separately
    - **This branch is ADR 0073's own addition — FR-12 states only two conditions — and it is scheduled explicitly here because if it is not, it will not be built.** It is load-bearing three times over: `MockSubscription` and `NullDeclaringSubscription` are both reported under it, and an out-of-repo override may return null (C-13). A `Check` whose null branch returned "sound" must not pass
  - Depends on: 39.

- [ ] **41. CHARACTERISE: `Check` accepts a sound declaration and rejects a missing subject (ADR 0073 `Check` contract)**
  - **USE COMMAND**: `/test-first when a subscription declares a real channel factory the declaration check should report no reason`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test files:
    - `When_a_subscription_declares_a_real_channel_factory_should_report_no_reason.cs`
    - `When_checking_a_declaration_with_no_subscription_type_should_throw.cs`
  - Test should verify:
    - `Check(typeof(<double>), typeof(<the sound IAmAChannelFactory double from task 37>))` returns **null**
    - `Check(null!, typeof(<sound factory>))` throws `ArgumentNullException`
  - **🔁 Characterisation (sound case only)** — `When_a_subscription_declares_a_real_channel_factory_should_report_no_reason.cs` is green on first run: after tasks 38-40 a sound declaration falls through all three branches. **RED mutation**: change `Check`'s final `return null` to return a placeholder reason; the test fails on its null assertion. The `ArgumentNullException` case needs no mutation — no earlier task adds the guard, so it is RED on arrival.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Complete the null-means-sound convention, which is what keeps the addition to the shipped package at **one type** — a `ChannelFactoryDeclarationResult` record would be a second public type carrying a boolean and a message with no behaviour and no invariant
    - `subscriptionType` names the subject only; a null argument throws `ArgumentNullException`
  - Depends on: 40.

---

## Phase 8: The assembly sweep

*ADR 0073 step 2 — `Sweep`.*

`public static IReadOnlyList<(Type Subject, string? Reason)> Sweep(Assembly gatewayAssembly)`. **Candidates** are what step 1 finds; **subjects** are the candidates that survive step 2. One entry per subject.

Every case in this phase sweeps the whole `Paramore.Brighter.Core.Tests` assembly — `Sweep` takes an `Assembly` and nothing narrower, and **no type-scoped overload is introduced so a test can avoid a `Single(...)`**. Each assertion is therefore **subject-scoped**: `result.Single(e => e.Subject == typeof(X))`, except the subsumption cases, whose assertion is that a subsumed type has **no** entry and which therefore read the filtered sequence. This is mandatory because that assembly holds `Subscription` subclasses these cases do not own and which are **not sound**: `MockSubscription` (`MessagingGateway/When_constructing_a_channel_with_combined_factory.cs:85`, an auto-property, reported under the null branch) and the four doubles task 1 adds, of which `NullDeclaringSubscription` is reported under the null branch by design.

- [ ] **42. TEST + IMPLEMENT: Sweeping an assembly reports each subject with the reason its declaration is unsound (the reading path's only negative assertion)**
  - **USE COMMAND**: `/test-first when sweeping an assembly containing unsound declarations the sweep should report each subject with the expected reason`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_sweeping_an_assembly_containing_unsound_declarations_should_report_their_reasons.cs`
  - Test should verify:
    - `Sweep(typeof(<AC-28 double>).Assembly)` over the whole `Core.Tests` assembly
    - the entry for the **not-a-channel-factory** double (task 38) carries the expected non-null `Reason`
    - the entry for the **inherited-default** double (task 39) carries the expected non-null `Reason`
    - both selected with `result.Single(e => e.Subject == typeof(X))`
    - **Why this case exists**: AC-28 calls `Check` with literal arguments, and the twelve generated sweeps assert every `Reason` is `null`, so without this the whole reading path — discovery, subsumption, closing, the uninitialised read, the hand-off into `Check` — is exercised only over *sound* declarations. A `Sweep` that read the wrong property, passed `null` to `Check`, or discarded the read value would leave all of those green
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `Sweep(Assembly)` with step 1 (**candidates**): non-abstract classes that **are** `Paramore.Brighter.Subscription` or whose base chain reaches it — FR-12's inclusive "assignable to" reading, and a type is assignable to itself. **Walk the base chain explicitly** rather than testing with `IsAssignableFrom`, which behaves surprisingly for open generic definitions and every gateway assembly contains one
    - Add step 4 (**read and check**): `GetUninitializedObject` produces an instance, cast to `Subscription`, read `ChannelFactoryType` through the virtual property, pass the value and the **subject** into `Check`. No member-level reflection is needed for the read
    - Target the modern `System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject` on `net8.0` and above, with `System.Runtime.Serialization.FormatterServices.GetUninitializedObject` behind `#if NETSTANDARD2_0`. `FormatterServices.GetUninitializedObject` is obsoleted as **SYSLIB0050** and `src/Directory.Build.props` sets `TreatWarningsAsErrors`, so using it unguarded fails the build
    - Order entries by `Subject.FullName`, **ordinal** — `Assembly.GetTypes()` order is unspecified, and the sort is what makes repeated runs byte-identical
    - Return **one entry per subject including sound ones** — a failures-only contract cannot distinguish a sound assembly from one never looked at, and would make AC-29 unassertable
    - Let a `ReflectionTypeLoadException` from `GetTypes()` **propagate**: broken project references are not a declaration defect
  - Depends on: 41, 1 (the phase-1 doubles must be expression-bodied or this sweep reports them under the null branch and the subject-scoped assertions are written against a moving target).

- [ ] **43. CHARACTERISE: A subscription whose constructor cannot succeed is still examined (AC-29)**
  - **USE COMMAND**: `/test-first when a subscription's constructor cannot succeed the sweep should still report its declaration as sound`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_subscription_constructor_cannot_succeed_should_still_report_its_declaration.cs`
  - Test should verify:
    - a third synthetic double whose **parameterless constructor cannot succeed** — it calls `base(...)` with `MessagePumpType.Unknown`, which `Subscription.cs:213` rejects with `ConfigurationException` — but which declares the **sound** `IAmAChannelFactory` double from task 37
    - it appears among its assembly's subjects with a **null** `Reason`
    - it can only do so because **no constructor ran** — that is the assertion AC-29 is about
    - no broker, database or network is contacted
  - **🔁 Characterisation** — green on first run: task 42 already reads through `GetUninitializedObject`. **RED mutation**: before the uninitialised read, try `Activator.CreateInstance(type, nonPublic: true)` and fall back to `GetUninitializedObject` only on `MissingMethodException`. Doubles without a parameterless constructor are unaffected; the AC-29 double's constructor runs and throws `ConfigurationException` (`Subscription.cs:213`), surfacing as a `TargetInvocationException` from `Activator.CreateInstance`, and the test fails because a constructor was invoked — the thing AC-29 is about. The test double therefore needs a **parameterless** constructor that calls `base(...)` with `MessagePumpType.Unknown`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm `Sweep` uses `GetUninitializedObject` and never `Activator.CreateInstance` — **none** of the twenty-four shipped gateway subscription types declares a parameterless constructor (all-optional parameters do not produce one), so `Activator.CreateInstance(Type)` throws `MissingMethodException` on every one of them
    - This is a **positive** case — it shows a type that cannot be constructed is nonetheless examined — not a negative exercise of the reading path; that is task 42's job
  - Depends on: 42.

- [ ] **44. TEST + IMPLEMENT: A generic subscription declaring its own override is closed, read, and reported as its open definition**
  - **USE COMMAND**: `/test-first when a generic subscription declares its own override the sweep should close it read it and report the open definition`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_generic_subscription_declares_its_own_override_should_report_the_open_definition.cs`
  - Test should verify:
    - a synthetic **generic** subscription in `TestDoubles/` that declares its own sound override
    - its entry's `Subject` is the **open definition literal `typeof(X<>)`**, not the closed construction
    - its `Reason` is null — the value was actually read, which requires closing to have happened
    - **What makes this fail before the implementation**: this is the first generic `Subscription` subclass in `Core.Tests`, so until closing exists step 4 calls `GetUninitializedObject` on an open definition, which throws, and the sweep does not complete. Tasks 42 and 43 go red with it until closing lands — expected, and restored by this task
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add step 3 (**generic closing**): close an open generic subject with `MakeGenericType` using one representative argument, `typeof(Paramore.Brighter.Command)` — a public concrete class implementing `IRequest` that satisfies both constraint forms the gateways use, `where T : IRequest` (nine assemblies) and `where T : class, IRequest` (GcpPubSub, Postgres, RocketMQ)
    - **`Subject` is the candidate as discovered, not as read**: the open definition. The closed type exists only to be instantiated and read. This is the reported identity, so it is what an expected-subject configuration names (`typeof(Foo<>)` is valid C#) and what a reason message renders — a closed construction's `FullName` is ``Foo`1[[Paramore.Brighter.Command, …, Version=…]]``, unusable in a message and unrenderable from a configuration string
    - **The generic-closing path has no shipped type to exercise it** — after subsumption all twelve assemblies resolve to a non-generic base — so this synthetic is the only cover it has (ADR 0073, Risks)
    - **Why closing precedes subsumption in task order** (though it is step 3 of `Sweep` and subsumption is step 2): task 45's subsumption pair carries a generic base, `Base<T>`, which cannot be read until closing exists. Scheduling closing first gives both tasks an observable RED
  - Depends on: 43.

- [ ] **45. TEST + IMPLEMENT: A derived subscription declaring no override is subsumed by its base — over a constructed generic base**
  - **USE COMMAND**: `/test-first when a derived subscription declares no channel factory override the sweep should subsume it under its base`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_derived_subscription_declares_no_override_should_be_subsumed_by_its_base.cs`
  - Test should verify:
    - a base/derived pair in `TestDoubles/` where the base is **generic and the derived closes it**: `Derived : Base<Command>`, with `Derived` declaring **nothing** and `Base<T>` declaring a sound override (reusing task 37's sound factory double, so the base's own entry carries a null reason and the assertion is about subsumption alone)
    - **no entry has `Subject == typeof(Derived)`**
    - `typeof(Base<>)` **is** present among the subjects
    - **What makes this fail before the implementation**: with no subsumption, `Derived` is reported as a subject in its own right (it inherits `Base<T>`'s sound value, so its reason is null), and the no-entry assertion fails
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add step 2 (**subsumption**): drop a candidate when it does **not** itself declare `ChannelFactoryType` (`BindingFlags.DeclaredOnly`) **and** an ancestor in its base chain is also a candidate in the same assembly
    - **Match ancestry on the generic type definition.** Subsumption runs before closing, so candidates are open definitions (`Foo<>`) while a base chain yields closed constructions (`Foo<Bar>`); an ancestor that is a constructed generic must be reduced with `GetGenericTypeDefinition()` before comparison. Without the reduction `Derived` never matches its own base and is reported twice — or, read the other way, silently dropped
    - **The constructed generic base belongs on this pair specifically.** No shipped assembly exercises the reduction — all twelve pairs are `XSubscription<T> : XSubscription` with a *non-generic* base. And the reduction is load-bearing only where the derived type declares no override: on the declares-its-own pair (task 46) the drop condition's first conjunct is already false, so both types are reported whether the reduction works or not and an assertion there could not fail on a broken reduction
  - Depends on: 44 (`Base<T>` must be closable before it can be read).

- [ ] **46. CHARACTERISE: A derived subscription declaring its own override is reported in its own right**
  - **USE COMMAND**: `/test-first when a derived subscription declares its own channel factory override the sweep should report both it and its base`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_derived_subscription_declares_its_own_override_should_be_reported_in_its_own_right.cs`
  - Test should verify:
    - a second base/derived pair in `TestDoubles/` where the **derived type declares its own** `ChannelFactoryType`
    - **both** types are reported as subjects
  - **🔁 Characterisation** — green on first run: task 45's drop condition already carries the `BindingFlags.DeclaredOnly` conjunct. **RED mutation**: remove that conjunct, so a candidate is dropped whenever an ancestor is a candidate; the declaring derived type disappears and the test fails on its both-reported assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Confirm subsumption never drops a declaring derived type: it can disagree with its base, and a guard that hid it would be blind to exactly the declaration the author wrote. An unconditional "at most once" would invert the guard's purpose (FR-12 *Scope*, AC-27's Then clause)
    - **This shape has no shipped instance at all**, so `Core.Tests` is the only place it can be tested — the generated sweeps test the *outcome* of the no-override shape on real assemblies, these two pairs test the *mechanism*
  - Depends on: 45.

- [ ] **47. TEST + IMPLEMENT: A `ChannelFactoryType` getter that throws yields a reason, not a skip and not a terminated sweep**
  - **USE COMMAND**: `/test-first when reading a subscription's channel factory type throws the sweep should report a reason naming the exception`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_reading_a_channel_factory_type_throws_should_report_a_reason_naming_the_exception.cs`
  - Test should verify:
    - a double in `TestDoubles/` whose `ChannelFactoryType` getter **throws** on an uninitialised instance
    - its entry carries a **non-null** reason naming the type, the exception type and its message
    - the rest of the sweep still completes — other subjects are still reported in the same run
    - **no other double exercises this**: `MockSubscription` reads `null` rather than throwing
    - at RED, this double makes tasks 42-46 fail too — the sweep terminates on it until the per-type catch exists. Expected, and restored by this task
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Catch **per type** inside `Sweep` and convert the fault to a reason. This does **not** contradict ADR 0064's "rules must not catch": that rule forbids a *rule* catching because the `Specification<T>` framework already wraps rule evaluation and turns any body exception into an `Error` finding. A static sweep has no such surrounding framework, so catching per type is how it obtains the **equivalent** behaviour — one fault becomes one reported reason instead of terminating the run, and one run reports every offending type. Nothing is swallowed; the sweep still fails, with more information
    - **A reason, never a silent skip** — a skipped type vanishes from the reported subject set, which is the vacuous pass this design exists to prevent
  - Depends on: 46.

- [ ] **48. CHARACTERISE: A generic subscription the representative argument cannot satisfy yields a reason, not a skip**
  - **USE COMMAND**: `/test-first when a generic subscription's constraints cannot be satisfied the sweep should report a reason rather than skip it`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration"
  - Test file: `When_a_generic_subscription_cannot_be_closed_should_report_a_reason_rather_than_skip.cs`
  - Test should verify:
    - a synthetic generic subscription in `TestDoubles/` constrained `where T : IEvent` — enough, since `Paramore.Brighter.Command` implements `ICommand`
    - if the test is RED on arrival, this double makes tasks 42-47 fail too — the sweep terminates on it. Expected, and restored by this task
    - its entry is **present** and carries a **non-null** reason
    - the reason **identifies the closing failure** — it names the representative argument `Paramore.Brighter.Command` and the `ArgumentException` `MakeGenericType` raised for the violated constraint — so it cannot be satisfied by task 47's getter-throws reason alone
  - **🔁 Characterisation (conditional)** — if task 47's per-type catch already encloses step 3 and its reason already names the exception, this test is green on first run. **RED mutation**: move the `MakeGenericType` call outside the per-type catch; the test then fails because `Sweep` throws the constraint `ArgumentException` before any assertion runs — the silent-termination defect this task guards. If task 47's catch does not enclose step 3, the test is RED on arrival and no mutation is needed.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed** — before committing if the test was green on arrival, before implementing if it was not *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Convert an arity-or-constraints failure in step 3 into a reason belonging to `Sweep`, not to `Check`
    - **Why this needs its own test when the exact subject set appears to cover it**: a silent skip is caught by the exact set only for a subject the configuration **already expects** — dropping such a subject shrinks the reported set and fails the comparison. It is **not** caught for an **unconfigured** subject, and that is exactly the event this guard exists to notice: a gateway growing a second declaring subscription which happens to be generic with constraints `Command` cannot satisfy. Nobody has added it to `AdditionalExpectedSubjects` — its absence *is* the event being guarded — so a silent skip leaves the generated test green. No `Core.Tests` case can reproduce that blind spot, because it belongs to the *generated* sweeps whose expectation comes from configuration; the path is closed the other way, by making the sweep's behaviour on it a tested property
    - **Known and accepted gap, recorded rather than closed**: the sixth reason path — an instance that cannot be produced — is not asserted. It is unreachable for any type this design declares and carries the same blind spot
  - Depends on: 47.

---

## Phase 9: Generator additions

*ADR 0073 step 3.*

- [ ] **49. SETUP: the `GatewayConformance` configuration section**
  - Files: `tools/Paramore.Brighter.Test.Generator/Configuration/GatewayConformanceConfiguration.cs` (new), `tools/Paramore.Brighter.Test.Generator/Configuration/TestConfiguration.cs` (add the section, alongside the existing `MessagingGateway` and `Outbox` sections)
  - Properties:
    - `SubscriptionType` — `string`, **required**. The fully-qualified name of the subscription type expected to be reported. Rendered **both** as `typeof(X).Assembly`, to locate the sweep, **and** as an expected subject. **It MUST name a type that survives subsumption** — one declaring its own `ChannelFactoryType`, or a root candidate. Naming the generic derived type (`MqttSubscription<T>`) was a valid locator before and is now a red test.
    - `AdditionalExpectedSubjects` — `List<string>`, optional, default empty. Absent in all twelve today. A **generic** entry is written in JSON with its arity backtick exactly as `Type.FullName` reports it (`` Ns.Foo`1 ``); writing `Ns.Foo<>` is invalid, because the value must stay a name `Type.FullName` could have produced so the audit's namespace comparison keeps working.
    - `Category` — `string`, optional, unused by the guard; present for symmetry with the other sections.
    - `Namespace` — `string?`, merged from the top-level `TestConfiguration.Namespace` when empty, exactly as `MessagingGatewayConfiguration.cs:41, :170-172` and `OutboxConfiguration.cs:51, :120-122` do. **The section, with `Namespace` merged, is the render model** task 50 renders with and task 51 passes.
  - The expected set is `SubscriptionType` ∪ `AdditionalExpectedSubjects`. An assembly that grows a second declaring subscription fails its sweep until that list is updated — which is the point.
  - **Why no test of its own**: it is a data-carrying configuration class. It is exercised by task 50's render test, end-to-end by task 53's generated tests and by task 54's audit, and a mis-shaped section fails configuration load or generation immediately.
  - Depends on: nothing (may run in parallel with phases 7-8).

- [ ] **50. TEST + IMPLEMENT: the Liquid template renders the expected-subject set, including generic entries**
  - **USE COMMAND**: `/test-first when rendering the gateway conformance template with generic additional subjects it should render open generic typeof literals and the full expected set`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/GatewayConformanceTemplate"
  - Test file: `When_rendering_the_gateway_conformance_template_with_generic_subjects_should_render_open_generic_literals.cs`
  - Test should verify, rendering the **real** template file through `Parser.ParseAsync(new ParseContext(templatePath, outputPath, model))` — the path `tests/Paramore.Brighter.Test.Generator.Tests/Parser/When_parsing_template_with_outbox_configuration_should_render_correctly.cs` uses — with a configuration carrying `Namespace = "MyApp.Tests"`, `SubscriptionType = "Ns.Root"` and `AdditionalExpectedSubjects = ["Ns.Foo`1", "Ns.Bar`2"]`:
    - the output contains `typeof(Ns.Root)`, `typeof(Ns.Foo<>)` and `typeof(Ns.Bar<,>)`, and **no** backtick
    - the expected set contains **exactly** those three literals — the union `SubscriptionType` ∪ `AdditionalExpectedSubjects`, neither side dropped
    - the namespace line reads `namespace MyApp.Tests.MessagingGateway.Generated.Conformance`
    - **What makes this fail**: none of the twelve configurations carries `AdditionalExpectedSubjects` (task 52), so task 53's generated files never execute the arity conversion or the union. Without this test that logic ships unexercised
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - File: `tools/Paramore.Brighter.Test.Generator/Templates/GatewayConformance/When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs.liquid` (new folder, new file)
    - **Fix the render model here**, because this test needs it: the model task 51's generator passes must be exactly the one this test renders with. If the arity conversion is done by a precomputed property rather than in Liquid, that property lives on task 49's configuration class and is still covered by this test
    - Render one `[Fact]` in a class named `GatewayChannelFactoryDeclarationTests` — file named for the test method, class for the behaviour, per `.agent_instructions/testing.md`
    - The rendered test asserts exactly two things and **contains no reflection logic of its own**:
      1. `Sweep`'s reported subject set is **exactly** the expected set, compared against rendered `typeof(...)` literals — one entry in all twelve today
      2. every `Reason` is `null`
    - Render a generic `AdditionalExpectedSubjects` entry by replacing the `` `n `` suffix with angle brackets carrying *n*−1 commas — `typeof(Ns.Foo<>)`, `typeof(Ns.Foo<,>)` for arity 2 — matching `Subject`'s open-definition identity
    - Output path `MessagingGateway/Generated/Conformance/`, namespace `{{ Namespace }}.MessagingGateway.Generated.Conformance`. The `Generated` segment is what brings the file inside the generated-tree audit's scope, and the path cannot collide with a per-variant folder (`MessagingGateway/Classic/Generated/…`). **One sweep per project, not per gateway variant** — `Paramore.Brighter.AWS.Tests` has four variants over one assembly
    - Emit **no** `Category` and **no** `Collection` attribute — the test runs in CI's infrastructure-free `build` job (task 55)
    - **Every declaration judgement stays in `SubscriptionChannelFactoryDeclaration`**, driven test-first in tasks 38-48. The template owns only rendering — the literal conversion and the union — which is what this test pins. An earlier draft put the subsumption rule in the template, which would have expressed one rule in two places and made a regeneration something to review rather than to trust
  - Depends on: 49.

- [ ] **51. SETUP: `GatewayConformanceGenerator` and its wiring into generation, planning and the audit**
  - Files: `tools/Paramore.Brighter.Test.Generator/Generators/GatewayConformanceGenerator.cs` (new); `tools/Paramore.Brighter.Test.Generator/Program.cs`; `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/GeneratedTreeAudit.cs`
  - Mirror `MessagingGatewayGenerator`'s `Suites` / `SuitesFor` / `Plan` shape. Per `.agent_instructions/generated_tests.md` the suite **must** be described in `SuitesFor(...)`, so the generate path and the plan path walk one description — a suite only the generate path knows about is written and then reported as an **orphan** by the audit.
  - `Program.cs` invokes the new generator alongside the existing ones.
  - `GeneratedTreeAudit.ExpectedFilesUnder` adds `GatewayConformanceGenerator.Plan` alongside `OutboxGenerator.Plan` and `MessagingGatewayGenerator.Plan`, **or the twelve new files are all reported as orphans**.
  - **Why no test of its own**: the existing audit tests are its tests. `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/When_auditing_the_generated_tree_should_find_no_missing_files.cs` and `…should_find_no_orphans.cs` will fail if `Plan` and the generate path disagree, once task 53 has generated the files.
  - Depends on: 50.

---

## Phase 10: Configurations and generated sweeps

*ADR 0073 step 4; AC-27.* **Sequences after phase 5.**

- [ ] **52. SETUP: the twelve `test-configuration.json` entries — nine edits, three new files**
  - **Three gateway test projects have no `test-configuration.json` today and need one**: `tests/Paramore.Brighter.AzureServiceBus.Tests`, `tests/Paramore.Brighter.MQTT.Tests`, `tests/Paramore.Brighter.RMQ.Sync.Tests`. Theirs carry `Namespace` and a `GatewayConformance` section, nothing else.
  - **`Namespace` is required, not incidental**: it is a top-level property defaulting to `string.Empty` (`Configuration/TestConfiguration.cs:38`), and the template renders `{{ Namespace }}.MessagingGateway.Generated.Conformance`, so omitting it yields `namespace .MessagingGateway.Generated.Conformance` — a failure *after* generation rather than at configuration load. All fourteen existing configurations carry it.
  - The other nine gain the section in the file they already have. `AdditionalExpectedSubjects` is **absent from all twelve**, not omitted here by accident.

    | Test project | `GatewayConformance.SubscriptionType` |
    |---|---|
    | `Paramore.Brighter.AWS.Tests` | `Paramore.Brighter.MessagingGateway.AWSSQS.SqsSubscription` |
    | `Paramore.Brighter.AWS.V4.Tests` | `Paramore.Brighter.MessagingGateway.AWSSQS.V4.SqsSubscription` |
    | `Paramore.Brighter.AzureServiceBus.Tests` *(new file)* | `Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusSubscription` |
    | `Paramore.Brighter.Gcp.Tests` | `Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubSubscription` |
    | `Paramore.Brighter.Kafka.Tests` | `Paramore.Brighter.MessagingGateway.Kafka.KafkaSubscription` |
    | `Paramore.Brighter.MQTT.Tests` *(new file)* | `Paramore.Brighter.MessagingGateway.MQTT.MqttSubscription` |
    | `Paramore.Brighter.MSSQL.Tests` | `Paramore.Brighter.MessagingGateway.MsSql.MsSqlSubscription` |
    | `Paramore.Brighter.PostgresSQL.Tests` | `Paramore.Brighter.MessagingGateway.Postgres.PostgresSubscription` |
    | `Paramore.Brighter.RMQ.Async.Tests` | `Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription` |
    | `Paramore.Brighter.RMQ.Sync.Tests` *(new file)* | `Paramore.Brighter.MessagingGateway.RMQ.Sync.RmqSubscription` |
    | `Paramore.Brighter.Redis.Tests` | `Paramore.Brighter.MessagingGateway.Redis.RedisSubscription` |
    | `Paramore.Brighter.RocketMQ.Tests` | `Paramore.Brighter.MessagingGateway.RocketMQ.RocketSubscription` |

  - Every value names the **non-generic base** that carries the override, never the generic derived type — the generic derived type is subsumed and naming it is now a red test.
  - **Why no test of its own**: configuration data. It is validated by task 53's generated tests and by task 54's audit.
  - Depends on: 51.

- [ ] **53. GENERATE: The sweep finds no invalid channel factory declaration in any of the twelve gateway assemblies (AC-27)**
  - **GENERATED TEST — do NOT use `/test-first` and do NOT hand-write the file.** The twelve test files are rendered by `./generate-test.sh` from task 50's template and must **never** be edited directly (`.agent_instructions/generated_tests.md`; ADR 0073 *Technology Choices*). A change to what they assert is a template edit followed by a regeneration. The behaviour they exercise — `Check` and `Sweep` — was driven test-first in tasks 38-48; this task makes it fire over the twelve **real** assemblies.
  - Test location: `tests/<each of the twelve>/MessagingGateway/Generated/Conformance/`
  - Generated test file (×12): `When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs`
  - The generated tests verify, per assembly:
    - the reported subject set is **exactly** the configured expected set — one subject in all twelve today, because all nine existing overrides and the three tasks 33-35 add sit on the non-generic base while every generic derived type declares none
    - every `Reason` is `null`
  - What the exact set catches: subsumption over-reporting (an extra subject), subsumption inverted — base dropped, derived kept — which a non-emptiness check would miss because the derived type inherits its base's value and reports a null reason, and discovery finding nothing (an empty set). What it does **not** catch is a `SubscriptionType` aimed at the **wrong assembly**, since one value both locates the sweep and supplies the expectation; that is covered by the absent project reference (a cross-gateway misaim usually does not compile), by the reason check (a misaim to `Paramore.Brighter` itself reports `{Subscription}` and fails on `typeof(InMemoryChannelFactory)`), and by task 54's audit.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*
  - Run the generated tests with `dotnet test tests/<project> --filter "FullyQualifiedName~GatewayChannelFactoryDeclarationTests"` for each of the twelve, then the generated-tree audit (`tests/Paramore.Brighter.Test.Generator.Tests`, `GeneratedFileAudit`). Commit the twelve generated conformance files **and** the `SharedGenerator` helper files rendered into the three new projects (see below) as `test:` — passing on first run is the expected case here, **never** "already complete".
  - Implementation should:
    - Build the generator, then run `./generate-test.sh` **from each of the twelve test project directories** (per `.agent_instructions/generated_tests.md` step 3 — running it from the repository root generates into the wrong place), and commit the generated files
    - **Expect `SharedGenerator` to render its four helper files into the three new conformance-only projects as well.** This is deliberate and is **not** to be "fixed": those files reference only `Paramore.Brighter` (which contains the `Observability` namespace — it is not a separate package) and xunit, via fully-qualified `Xunit.Assert` calls rather than a `using`; both are already present in every gateway test project, so they compile. They land in the project root, outside the audit's `Generated/` scope, so no CI job depends on them. Twelve unused files is the accepted cost of not making a behavioural change to a shared generator FR-12 does not ask for. **They are committed**, as every existing gateway test project commits its `SharedGenerator` output (`git ls-files 'tests/*/IAmAMessageBuilder.cs'`) — leaving them untracked would leave the tree dirty and trip `/spec:ralph-implement`'s leftover check
    - **Ordering**: run the twelve and confirm all are green. They are green **only because tasks 31-35 have merged**. If those corrections have slipped, the generated files may still be committed — the generated-tree audit wants them — but **task 55 must not be enabled**, or the `build` job is red on every pull request
  - Depends on: 52, and **35** (all five corrections).

---

## Phase 11: Gateway audit

*ADR 0073 step 5 — the thirteenth-gateway audit.*

- [ ] **54. TEST + IMPLEMENT: Every shipped gateway project is named by exactly one conformance configuration**
  - **USE COMMAND**: `/test-first when auditing gateway conformance configuration every messaging gateway project should be named by exactly one SubscriptionType`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit"
  - Test files, written in this order:
    1. `When_auditing_synthetic_gateway_conformance_configuration_should_report_unconfigured_and_doubly_configured_gateways.cs` — over **synthetic inputs**, so the two failure cases can be asserted at all (the real tree has neither after task 52)
    2. `When_auditing_gateway_conformance_configuration_should_find_every_gateway_named_exactly_once.cs` — over the **real** tree
  - The audit is a pure helper, `GatewayConformanceAudit`, in `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/` beside `GeneratedTreeAudit.cs`. It takes the gateway directory names and the configured `SubscriptionType`s (and each configuration's `AdditionalExpectedSubjects`) and returns the gateways named zero times or more than once. A thin loader reads `src/Paramore.Brighter.MessagingGateway.*` and `tests/*/test-configuration.json` for the real-tree test.
  - **The loader is written in file 2's own cycle, not file 1's.** File 1 uses synthetic inputs and needs only the helper. Writing the loader during file 1 would be speculative code.
  - **For this task, the audit helper and its loader count as the production code under test** in the D14 sense, although they live in a test project. The guard-check mutation below is applied to the loader, and that is the permitted target. It is still never applied to either test file or to any `test-configuration.json`.
  - Test should verify:
    - file 1: given gateways `[A, B, C]`, with A configured once, B not at all and C twice, the audit reports exactly B (unconfigured) and C (configured twice)
    - file 1: given gateways `X` and `X.V2`, each configured once with a type in its own namespace, the audit reports nothing. The comparison is exact equality, not a prefix match: under `StartsWith`, `X.V2`'s configuration would also name `X`, making `X` "named by two". Without this case, only the real tree's `AWSSQS`/`AWSSQS.V4` pair catches a prefix match
    - file 1: an `AdditionalExpectedSubjects` entry in gateway A's namespace does **not** count as naming A — if it did, the very case that key exists for (a second declaring type in an existing gateway) would make A "named by two"
    - file 2: over the real tree, every `src/Paramore.Brighter.MessagingGateway.*` directory is named by **exactly one** `GatewayConformance.SubscriptionType`, comparing the directory name with the namespace containing the configured type — the audit reports nothing
  - **RED on arrival, both files.** File 1: the helper does not exist, then returns nothing until implemented, so the B/C assertion fails. File 2: the loader does not exist, so RED on arrival is a compile error. That is file 2's normal RED, as for task 2.
  - **Guard check, after GREEN (D14 mechanism)**: once the loader is written, file 2 passes because task 52 configured all twelve gateways, so its reports-nothing assertion has not yet been seen to fail. Temporarily make the loader drop the `GatewayConformance` section of `tests/Paramore.Brighter.AWS.Tests/test-configuration.json`, and confirm the test fails on its reports-nothing assertion, naming `Paramore.Brighter.MessagingGateway.AWSSQS` as unconfigured. Revert and confirm green. The mutation names one project because 5 of the 17 configurations carry no `GatewayConformance` section and directory enumeration order is unspecified, so "skip the first one read" could skip nothing. This check is what shows the audit catches the defect it exists for.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - All twelve shipped gateway assemblies use their assembly name as their root namespace, which is what makes the comparison exact
    - **Known and accepted limitation**: the audit relies on that naming convention. A future gateway that breaks it fails the audit loudly, and the fix is to name the assembly explicitly in that configuration (ADR 0073, Risks)
    - Adding a gateway to `src/` fails this audit until a configuration names it — that is the whole point
  - Depends on: 52.

---

## Phase 12: CI and documentation

*ADR 0073 steps 6-7.* **Must follow phase 5.**

- [ ] **55. SETUP: the `build` job runs the twelve sweeps**
  - File: `.github/workflows/ci.yml`, the `build` job (declared at `:36`, which already runs `dotnet build --configuration Release` at `:60`)
  - Add one step running the twelve gateway test projects with
    `--configuration Release --filter "FullyQualifiedName~GatewayChannelFactoryDeclarationTests" --no-build`.
    **The configuration is named explicitly** because `--no-build` otherwise looks for a Debug build the `build` job never produced.
  - **The placement is load-bearing, not tidiness.** `.github/workflows/ci.yml:228` runs MQTT with `--filter "Category=MQTT&…"` and `:361` runs Kafka with `--filter "Category=Kafka&…"`, so an untagged test in those projects would never be selected by the transport jobs; and the RocketMQ job (`:708-769`) is entirely commented out, so a sweep living only there would never run at all. The `build` job already compiles the whole solution, so this costs seconds and needs no broker.
  - **The step's non-vacuity depends on a different step**: a `--filter` that matches nothing does not fail on most runners, so a missing generated file would leave this step green while guarding eleven assemblies. What catches that is the **generated-tree audit in the same job**, which fails when any of the twelve files is missing. Do not remove or reorder that step.
  - **Accepted maintenance cost, recorded**: the step names twelve projects by hand; a thirteenth gateway must be added to it manually. Task 54's audit catches a missing *configuration*, not a missing *CI entry*.
  - **Why no test of its own**: it is CI configuration. The behaviour it runs is pinned by tasks 38-48 and 53.
  - **⚠️ Sequencing**: this step **must not be enabled until tasks 31-35 have merged.** The generated test asserts every `Reason` is `null`, which is false today in five of the twelve assemblies — GcpPubSub and MQTT report **not-a-channel-factory**; AWSSQS, AWSSQS.V4 and Postgres report **inherited-default**.
  - Depends on: 53, 54, and **35**.

- [ ] **56. DOC: `.agent_instructions/generated_tests.md` gains the `GatewayConformance` section**
  - File: `.agent_instructions/generated_tests.md`
  - Add:
    - the new template folder `Templates/GatewayConformance/` to the **Architecture** listing (§ *Architecture*, alongside `Templates/MessagingGateway/{Reactor,Proactor}` and `Templates/Outbox/{Sync,Async,Causation}`)
    - a `### Gateway Conformance Configuration` subsection under **Configuration**, documenting `SubscriptionType` (required; must name a type that survives subsumption; rendered both as locator and as expectation), `AdditionalExpectedSubjects` (optional; generic entries written with the arity backtick as `Type.FullName` reports it) and `Category` (unused, present for symmetry)
    - the output path `MessagingGateway/Generated/Conformance/` and the one-sweep-per-project rule
    - the three new `test-configuration.json` files, so a reader knows why those projects now have one
  - British spelling throughout (NFR-7).
  - **Why no test of its own**: documentation. Its factual claims are enforced by tasks 53 and 54.
  - Depends on: 52, 53.

---

## Phase 13: Verification

*Verification and risk mitigation.*

- [ ] **57. VERIFY: The whole feature's tests pass with no broker, database or network (AC-31, NFR-3)**
  - Run, with no container runtime and no external services available:
    - `tests/Paramore.Brighter.Core.Tests` (phases 2, 3, 7, 8)
    - `tests/Paramore.Brighter.Extensions.Tests` (phase 4)
    - the five transport-correction test classes from phase 5, by name filter
    - the twelve `GatewayChannelFactoryDeclarationTests` from phase 10
    - `tests/Paramore.Brighter.Test.Generator.Tests` (phases 9, 11)
  - Confirm every one passes and none attempts a connection. In particular confirm AC-26f's three cases construct a channel factory only and never call `CreateSyncChannel`/`CreateAsyncChannel`, and that no `Sweep` case invokes a subscription constructor.
  - **Why no `/test-first`**: AC-31 is a property of the whole set of tests the feature introduces, discharged per-task by every task above and confirmed once here. There is no single test file that expresses it.
  - Depends on: 56.

- [ ] **58. VERIFY: Risk mitigation — the rule agrees with `CombinedChannelFactory`'s runtime routing, and `FactoryTypes` is not built from the consumed parameter**
  - Re-read `src/Paramore.Brighter/CombinedChannelFactory.cs` and confirm `FactoryTypes` is derived from the `_factories` **field**, not from the primary-constructor `factories` parameter. The CS0236 compiler behaviour *raises* this risk rather than lowering it: the compiler rejects the correct-looking auto-property and accepts the wrong one. Confirm task 2's single-pass test is present and passing — it is the behavioural guard; this re-read is the second line.
  - Confirm the two companion assertions are present and passing — task 11 (AC-9) and task 14 (AC-10b) — which are the only things tying the rule's combined-arm verdict to the composite's actual routing.
  - **Record the residual risk honestly rather than claiming it away**: the companion assertions can only fire *negatively*. C-9's doubles throw on every member, so the mirror-image assertion — a correct multi-bus configuration where the rule is silent *and* the composite successfully routes — cannot be written as a successful-routing assertion. AC-6 (task 8) is therefore rule-only, with no runtime counterpart for the dispatch half, and AC-10's nested agreement is likewise unpinned at runtime. If a later change introduces a non-throwing double, the positive direction should be pinned then.
  - Check `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs:83` — an in-repo multi-bus configuration (Kafka + RMQ.Async) whose subscriptions must not be flagged — still declares matching factory types after the corrections.
  - **Why no `/test-first`**: this is a review of code and of tests that already exist, not a new behaviour.
  - References: ADR 0072 *Risks and Mitigations* (drift, and the `FactoryTypes` re-enumeration trap); NFR-6.
  - Depends on: 57.

- [ ] **59. VERIFY: Risk mitigation — no regression in the existing rules, the generated tree, or any target framework**
  - Run the full `dotnet build --configuration Release` across the solution and confirm it is clean under `TreatWarningsAsErrors` (`src/Directory.Build.props`). In particular confirm the `#if NETSTANDARD2_0` branch of the uninitialised read compiles on `netstandard2.0` **and** that the `net8.0`/`net9.0`/`net10.0` builds use `RuntimeHelpers.GetUninitializedObject` — `FormatterServices.GetUninitializedObject` is obsoleted as **SYSLIB0050** there and would fail the build. Note that test projects target `net9.0;net10.0` (`tests/Directory.Build.props`), so the netstandard2.0 branch is compiled but never executed in this repository.
  - Run the existing generated-tree audit (`…should_find_no_missing_files.cs`, `…should_find_no_orphans.cs`) and confirm the twelve new conformance files are expected, not orphaned.
  - Run the existing `Validation/` suite in `Core.Tests` and confirm the four pre-existing consumer rules are untouched in severity, `Source`, `Message` and blocking behaviour (task 30 asserts this at host level; this is the unit-level sweep).
  - Confirm the existing `MessagingGateway/When_constructing_a_channel_with_combined_factory.cs` tests still pass — `MockSubscription` is unchanged and is reported under `Check`'s null branch by design, which no assertion in phase 8 treats as a failure.
  - **Why no `/test-first`**: a regression sweep over existing tests and builds, not a new behaviour.
  - Depends on: 58.

---

# Coverage cross-reference

## Functional requirements → tasks

| Requirement | Tasks |
|---|---|
| FR-1 — rule exists, evaluated per subscription, one Error with the right `Source` | 3, 4, 14, 17, 27 |
| FR-2 — effective factory precedence | 5, 6, 16 |
| FR-2a — invariance to `DispatchBuilder`'s back-fill | 7 |
| FR-3 — compatibility, two arms, null `D`, no recursion | 4, 8, 9, 10, 11, 12, 13, 14, 15 |
| FR-4 — default in-memory configuration is silent | 16 |
| FR-5 — message content, display names, five remedy literals | 9, 13, 14, 15, 18, 19, 20, 21, 22, 23, 24 |
| FR-6 — `Error` severity, blocking semantics, purely additive | 3, 27, 28, 29, 30 |
| FR-7 — `GcpPubSubSubscription` | 31 |
| FR-8 — `MqttSubscription` | 32 |
| FR-9 — `SqsSubscription` (AWSSQS) | 33 |
| FR-10 — `SqsSubscription` (AWSSQS.V4) | 34 |
| FR-11 — `PostgresSubscription` | 35 |
| FR-12 — reflection sweep, pure predicate, twelve projects | 37-48 (predicate and sweep), 49-51 (generator), 52-53 (twelve configurations and sweeps), 54 (thirteenth-gateway audit), 55 (CI) |
| FR-13 — one-per-subscription, deterministic, ordered | 25 (with ordering also relied on in 8, 9, 21, 22) |

**No functional requirement is without a task.**

Non-functional, for completeness: NFR-1 → 3; NFR-2 → 18-24, 38-40, 47; NFR-3 → 57 (and every test task); NFR-4 → 29; NFR-5 → 2, 38 (the one permitted new public type is 38's `SubscriptionChannelFactoryDeclaration`); NFR-6 → 36, 58; NFR-7 → 2, 36, 38, 56.

## Acceptance criteria → tasks

| AC | Task | AC | Task |
|---|---|---|---|
| AC-1 | 3 | AC-16 | 27 |
| AC-2 | 4 | AC-17 | 28 |
| AC-3 | 5 | AC-17a | 29 |
| AC-4 | 6 | AC-18 | 30 |
| AC-5 | 7 | AC-19 | 17 |
| AC-6 | 8 | AC-20 | 31 |
| AC-7 | 9 | AC-21 | 32 |
| AC-8 | 10 | AC-22 | 33 |
| AC-9 | 11 | AC-23 | 34 |
| AC-10 | 12 | AC-24 | 35 |
| AC-10a | 13 | AC-25a/b/c/d/e | 31 / 32 / 33 / 34 / 35 |
| AC-10b | 14 | AC-26a/b/c/d/e | 31 / 32 / 33 / 34 / 35 |
| AC-10c | 15 | AC-26f | 32, 33, 34 |
| AC-11 | 16 | AC-27 | 53 (supported by 50, 52; mechanism by 45, 46) |
| AC-12 | 18 | AC-28 | 38, 39 (reading path: 42) |
| AC-13 | 19 | AC-29 | 43 |
| AC-13a | 20 | AC-30 | 25 |
| AC-13b | 21 | AC-31 | 57 (discharged per-task throughout) |
| AC-13c | 22 | | |
| AC-14 | 23 | | |
| AC-15 | 24 | | |

**No acceptance criterion is without a task.**

Two notes on format rather than coverage:

- **AC-27 (task 53)** is carried by a **generated** test. It does not use `/test-first` because the file is rendered by `./generate-test.sh` and must never be hand-written; it nonetheless keeps its ⛔ gate, at the point the generated files are reviewed and committed. The logic it asserts is driven test-first in tasks 38-48, and the template's rendering in task 50.
- **AC-31 (task 57)** is a property of the whole test set, not a test file, so it is a verification task rather than a `/test-first` task. It is additionally an obligation on every test task in phases 2-11.

## ADR decisions → tasks

**ADR 0072 — Key Components**

| Decision | Tasks |
|---|---|
| §1 `CombinedChannelFactory.FactoryTypes`, normative lazy-backing-field shape, thread-safety remark, the C-4 responsibility split | 2 (guarded by its own single-pass test) |
| §2 `ChannelFactoryCompatible`, `Specification<T>` predicate+factory shape, `ResolveCandidates`/`IsCompatible`/`Arm`, explicit null-`D` guard | 3, 4, 8, 13 |
| §3 `DisplayName` as a private static; body = declared clause + handed clause, independently varying; five remedy templates; `{F-list}` separator defined once; T3a/T3b suppression; D5's nested rendering | 12, 13, 15, 18-24 |
| §4 Registration and the C-5 decision (pass the factory instance; `GetService` not `GetRequiredService`) | 27 |
| §5 The five corrections as a consequence of D1 | 31, 32, 33, 34, 35 |

**ADR 0072 — Implementation Approach**: step 1 → task 2 (fixtures: task 1); step 2 → tasks 18, 23, 24; step 3 → tasks 3-17, 25; step 4 → tasks 26-30; step 5 → tasks 31-35; step 6 → task 36.

**ADR 0073**

| Decision | Tasks |
|---|---|
| `Check` — three named branches, `Type.FullName` rendering, `ArgumentNullException`, null-means-sound | 38, 39, 40, 41 |
| `Sweep` — candidates, subsumption (with `GetGenericTypeDefinition()` reduction), generic closing, uninitialised read, ordering, three sweep-owned reasons, `ReflectionTypeLoadException` propagates, `Subject` is the open definition | 42, 43, 44, 45, 46, 47, 48 |
| The generator trio — configuration class, template, `GatewayConformanceGenerator` with `Suites`/`SuitesFor`/`Plan` + `Program.cs` + `GeneratedTreeAudit` | 49, 50, 51 |
| The twelve configurations — nine edits, three new files, `Namespace` required | 52 |
| CI placement — the `build` job, explicit `--configuration Release`, non-vacuity via the generated-tree audit | 55 |
| The thirteenth-gateway audit — `SubscriptionType` only | 54 |
| `Core.Tests` cases — three subscription doubles, two helper types, two subsumption pairs (the no-override one carrying a constructed generic base), a generic declaring its own override, the throwing getter, the bad-constraints generic, the sweep over the AC-28 doubles, and `Check`'s null branch | 37 (helpers), 38 + 39 + 43 (the three subscription doubles), 40 (null branch), 42 (sweep over the AC-28 doubles), 44 (generic declaring its own — scheduled before the pairs because the no-override pair's base is generic), 45 + 46 (the two subsumption pairs), 47 (throwing getter), 48 (bad constraints) |
| The cross-ADR constraint on 0072's doubles (expression-bodied `typeof(...)`) | 1 (stated), 42 (where a violation would show) |
| Accepted gaps recorded rather than closed — the instance-cannot-be-produced path, the twelve unused `SharedGenerator` helper files, the CI project list, the RocketMQ job | 48, 53, 55 |

**ADR 0073 — Implementation Approach**: step 1 → tasks 37-41; step 2 → tasks 42-48; step 3 → tasks 49-51; step 4 → tasks 52-53; step 5 → task 54; step 6 → task 55; step 7 → task 56.

**No ADR decision is without a task.**

## Scope creep

Every task traces to a requirement or an ADR decision. Two are worth flagging as **ADR-derived but not AC-derived**, so a reviewer does not look for a criterion behind them:

- **Task 41** (`Check` returns null for a sound declaration; `ArgumentNullException` for a null subject) — from ADR 0073's `Check` **Contract**, not from AC-28. Without it the null-means-sound convention has no positive assertion.
- **Task 40** (`Check`'s **null** branch) — explicitly ADR 0073's own addition beyond FR-12's two conditions, and scheduled as its own task precisely because, as the ADR says, it will otherwise not be built.

Tasks 1, 26 and 37 are fixture-only, traceable to C-9 and to ADR 0073's *Hand-written cases in `Core.Tests`*. Tasks 57-59 are verification and risk-mitigation tasks traceable to AC-31, NFR-3, NFR-6 and the two ADRs' *Risks and Mitigations* sections. Nothing else is proposed.

## Where the 0072-before-0073 ordering is enforced by task order

- **Phases 1-6 (tasks 1-36) are ADR 0072; phases 7-12 (tasks 37-56) are ADR 0073.** The phase order alone carries the constraint.
- **Task 53 `Depends on: 52, and 35`** — the twelve generated sweeps assert every `Reason` is `null`, which is false today in GcpPubSub, MQTT, AWSSQS, AWSSQS.V4 and Postgres. Task 35 is the last of the five corrections.
- **Task 55 `Depends on: 53, 54, and 35`** — and carries the explicit ⚠️ note that the CI step must not be enabled before the corrections merge, per ADR 0073's step 4 warning. If the corrections slip, task 53's generated files may still be committed (the generated-tree audit wants them) but task 55 must wait.
- **Tasks 37-51 deliberately carry no dependency on phase 5** and may proceed in parallel with phases 2-6: `Check`, `Sweep`, and the generator scaffolding are all correct and green regardless of the gateway declarations. Only the *twelve real assemblies* (task 53) and *CI* (task 55) are gated.
- **Task 42 `Depends on: … 1`** is the one place the dependency runs the other way: ADR 0073's sweep over the `Core.Tests` assembly reads ADR 0072's doubles, so those doubles must already be written as expression-bodied `typeof(...)` or the phase-8 subject-scoped assertions are written against a moving target.
