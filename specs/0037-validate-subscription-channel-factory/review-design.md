# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 1)

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

7 findings at or above threshold 60. Address these before approving.

> **Main-agent verification.** All ten findings were checked against the working tree. **All ten
> hold**, and no supporting claim was wrong. Specifically verified: `.agent_instructions/testing.md:105`
> reads "Do not make export classes or methods from a module to test them; we only test exports from
> modules, not implementation details" — a direct prohibition the ADR never quotes, while quoting
> :103-104 and calling it "tension"; `:117` is indeed a dependency-injection prescription, not a
> licence to publish a static predicate. `TestConfiguration.Namespace` defaults to `string.Empty`
> (`TestConfiguration.cs:38`). ADR 0064's stated rationale (`:165`, `:171`) is that the
> `Specification<T>` framework already catches and converts to a finding — **not** that exceptions
> must reach the host, which is what 0073 claims of it. `Generators/` holds six files, not the five
> listed. `SharedGenerator` renders "into the root of the destination folder" (`:34`), and
> `GeneratedTreeAudit`'s scope is the `Generated/` tree (`:56`), so shared files in the project root
> are outside audit scope — which is what makes finding 6's "harm is asserted, not shown" correct.
> The ADR's own pipeline order is `candidates -> subsume -> close generics` (`:104`), confirming
> finding 7's definition-vs-construction gap. Summary counts recomputed by hand and match.
>
> **Finding 2 is the one that matters most.** It is a genuine design defect, not a wording nit:
> `Sweep`'s contract returns only reasons and "is empty when the assembly is sound" (`:156`), yet the
> AC-29 assertion is described as the double appearing "in `Sweep(itsOwnAssembly)` with no reason
> against it" (`:266`). By the contract a sound type does not appear at all, so that assertion cannot
> be written — and the same gap lets all twelve generated sweeps pass vacuously over zero candidates.
> Fixing it interacts with the "exactly two public members" budget the ADR treats as settled.

## Requirement coverage

**FR-12 — partially satisfied.** The construction-free mechanism (`GetUninitializedObject`, with a `#if NETSTANDARD2_0` `FormatterServices` branch) is correct and its justification checks out: `src/Paramore.Brighter/Paramore.Brighter.csproj:5` uses `$(BrighterTargetFrameworks)` = `netstandard2.0;net8.0;net9.0;net10.0` (`src/Directory.Build.props:43`), `TreatWarningsAsErrors` is set (`:17`), and SYSLIB0050 would therefore break the build. The pure predicate exists over `(subscriptionType, declaredFactoryType)`. The one-new-public-type placement is FR-12's own permission. **Gap**: FR-12's guard must not pass vacuously (the whole point of its condition 2), yet the design exposes no way for a sweep test to show it examined anything — see finding 2.

**AC-27 — satisfied in shape.** Twelve per-gateway sweeps, one per *project*, is right: `Paramore.Brighter.AWS.Tests/test-configuration.json` does carry four variants (`SnsStandard`, `SnsFifo`, `SqsStandard`, `SqsFifo`) over one assembly. The twelve-project reference topology in *The forces* is accurate — all twelve `.csproj` files verified: `Paramore.Brighter.Base.Test` in Gcp/MSSQL/PostgresSQL (3), `Paramore.Test.Helpers` in MQTT (1), `Paramore.Brighter.ServiceActivator` in AWS/AWS.V4/MQTT/RMQ.Async/RMQ.Sync/RocketMQ (6); `AzureServiceBus.Tests` and `Redis.Tests` reference only their gateway, so `Paramore.Brighter` reaches them transitively. "Nine edits, three new files" against fourteen existing `test-configuration.json` files is exactly right, and all fourteen do declare an `Outbox`/`Outboxes` or `MessagingGateway`/`MessagingGateways` section.

**AC-28 — satisfied.** `Check` is called with literal arguments over two synthetic subscriptions; both AC-28 shapes are covered. Placement beside the existing `CombinedChannelFactory` tests is real.

**AC-29 — not satisfied as designed.** The assertion the ADR describes cannot demonstrate what AC-29 requires. See finding 2.

Two named concerns did **not** produce findings, recorded rather than manufactured into one:

- **The generic-closing representative is correct.** Every constraint checked, not the summary: `where T : class, IRequest` on `GcpPubSubSubscription<T>` (`:158-159`), `PostgresSubscription<T>` (`:118-119`), `RocketMqSubscription<T>` (`:117-118`); `where T : IRequest` on the other nine. `Command` is `public class Command : ICommand` (`Command.cs:42`) and `ICommand : IRequest` (`ICommand.cs:31`) — a reference type satisfying both forms.
- **The thirteenth-gateway audit's naming premise holds.** Exactly twelve `src/Paramore.Brighter.MessagingGateway.*` directories exist; every one declares a root namespace identical to its directory name, and no gateway `.csproj` sets `RootNamespace` or `AssemblyName`.

All nine `override Type ChannelFactoryType` line numbers are correct (RocketMQ :50, GcpPubSub :108, Redis :32, AzureServiceBus :39, RMQ.Sync :67, RMQ.Async :73, Kafka :162, MsSql :32, MQTT :35), as are `Subscription.cs:35`/`:172`/`:213`/`:258`, the twenty-four-type count, the four shared Liquid templates, the three existing `SharedGenerator` tests, the two existing generated output shapes, `GeneratedTreeAudit`'s `Plan`-based expected set, and `tests/Directory.Build.props:4`. The RDD stereotype **service provider** is on the controlled list (`design_principles.md:15`).

## Findings

### 1. The ADR quotes half of the project rule it says it is in tension with, and the half it omits is a direct prohibition (Score: 70)

The Context (67-70) and the Negative bullet (336-343) both cite `.agent_instructions/testing.md`'s *Test Scope and Isolation*, quoting the bullet at `:103-104`. They never quote `:105`, which is in the same list and bears most directly on this decision. "Tension with" understates it: `:105` prohibits precisely the act, and the honest framing is that FR-12 grants a declared exception to a rule the ADR is otherwise breaking.

The second lean is worse. Lines 70-72 and 403-405 both assert that *No InternalsVisibleTo* "prescribes the alternative" and "names the deliberate public widening chosen here as the alternative". What `:117` actually prescribes is narrower and is about dependency injection, not about publishing a static predicate so a test can call it.

**Evidence**: `.agent_instructions/testing.md:103-105`:
```
- Do not expose more than is necessary from an assembly
  - An assembly is a module, it's surface area should be as narrow as possible.
  - Do not make export classes or methods from a module to test them; we only test exports from modules, not implementation details.
```
`.agent_instructions/testing.md:117`: "If you need to inject a dependency for testing (e.g., randomness, I/O), make the interface **public** so it can be injected through the public API."

Against ADR 0073:67-72 and :337-339.

**Recommendation**: Quote `:105` in the Negative bullet and say plainly that the decision contravenes it under an exception FR-12 grants expressly. Drop or heavily qualify the `:117` argument — the load-bearing justification is already present and sound (only `Paramore.Brighter` reaches all twelve; the Positive bullet's C-10 value). Borrowing a DI prescription it does not make weakens an otherwise defensible case.

---

### 2. The design names vacuous passes as the failure mode to prevent, then specifies two assertions that are themselves vacuous (Score: 68)

Line 176-177 states the principle: a silently skipped type is a vacuous pass, "which is the very failure mode condition 2 exists to prevent". But `Sweep`'s contract returns *only* reasons. Two consequences the ADR does not address:

**(a) The twelve generated sweeps.** A test asserting `Sweep(gatewayAssembly)` is empty passes identically whether the assembly holds twelve sound subscriptions, one, or none. If a future refactor moved the subscription types, or a misconfigured `SubscriptionType` pointed at an assembly with no candidates, the guard would go green while guarding nothing. The thirteenth-gateway audit catches a *missing configuration*; it does not catch a sweep that found zero candidates.

**(b) AC-29 cannot be asserted as described.** The AC-29 double is described as appearing in the sweep "with no reason against it" — but by `Sweep`'s own contract a sound type does not appear in the result at all. The test therefore cannot distinguish "the property was read from an uninitialised instance and found sound" from "the type was never read". AC-29's Then is *no subscription constructor is invoked* — proving that requires evidence the read happened, which this assertion shape cannot supply.

This is a real design consequence, not a wording nit: fixing it interacts with the "exactly two public members" constraint the ADR treats as settled (:131, :343).

**Evidence**: ADR 0073:154-156 ("returns the reasons for every unsound declaration … and is empty when the assembly is sound"), :176-177, :264-267 ("It appears in `Sweep(itsOwnAssembly)` with no reason against it … That is AC-29"), against requirements AC-29 ("no subscription constructor is invoked").

**Recommendation**: Decide and record how a sweep demonstrates it examined something. Options to weigh explicitly against the two-member budget: have `Sweep` return the examined subject types alongside the reasons; add a second return shape; or have the Liquid template assert a minimum candidate count rendered from the configuration. Then restate AC-29's assertion in terms the contract can support (e.g. the double's type is among the examined subjects *and* carries no reason).

---

### 3. "A `GatewayConformance` section and nothing else" contradicts the template's dependence on `{{ Namespace }}` (Score: 65)

The three new configuration files are specified to carry the conformance section only, but the template renders its namespace from `{{ Namespace }}`. `TestConfiguration.Namespace` is a separate top-level property defaulting to `string.Empty`, so a file written literally as described renders `namespace .MessagingGateway.Generated.Conformance` — which does not compile, and fails only after generation rather than at configuration load. All fourteen existing configurations carry `Namespace`.

**Evidence**: ADR 0073:215-217 and :207-209, against `TestConfiguration.cs:38` (`public string Namespace { get; set; } = string.Empty;`).

**Recommendation**: Say the three new files carry `Namespace` and `GatewayConformance`. If a missing `Namespace` should be a hard error rather than an invalid rendering, decide that here.

---

### 4. The "narrowest option that reaches all twelve" claim is asserted, not established — a generator-emitted predicate is never considered (Score: 65)

The Negative bullet rests the public-surface cost on a comparative claim, and Alternatives Considered rejects duplication only in its *hand-written* form, on drift grounds. But this ADR's own central mechanism is generator-owned duplication, and `SharedGenerator` already emits four helper files into every configured project root from single templates. A generator-emitted predicate would reach all twelve projects, would not drift (one template), and would add zero public surface — so it is strictly narrower on the axis the bullet invokes, and it is not in the Alternatives list.

The decision itself is settled and survives: the real discriminator is C-10, since a template-emitted copy cannot be pointed at a community gateway author's own assembly, and a guard whose logic lives in the shipped package is the one the shipped package can be held to. But that argument appears only as a Positive bullet and is never used to close this gap, so the comparative claim stands unsupported.

**Evidence**: ADR 0073:340-341 and :407-409, against `SharedGenerator.cs:34` ("rendered into the root of the destination folder").

**Recommendation**: Add "a generator-emitted predicate, one template rendered into each of the twelve projects" to Alternatives Considered and reject it on the C-10 ground plus the shipped-guard ground. Or drop "narrowest option" and say "the narrowest option that also reaches out-of-repo gateway authors", which is defensible as written.

---

### 5. "Condition 1" and "condition 2" carry two incompatible meanings in the same document (Score: 62)

`Check`'s own list numbers three conditions: 1 null, 2 not an `IAmAChannelFactory`, 3 `InMemoryChannelFactory`. Two later passages use FR-12's numbering instead, in which 1 is "implements `IAmAChannelFactory`" and 2 is "is not `typeof(InMemoryChannelFactory)`". Line 261 assigns the no-override shape to "condition-2", which under the ADR's own numbering is the non-`IAmAChannelFactory` case — the other shape entirely. An implementer reading top to bottom maps these backwards.

**Evidence**: ADR 0073:138-145 (Check's 1/2/3), :176-177 and :260-263 (FR-12's numbering), against requirements FR-12:182-185.

**Recommendation**: Give `Check`'s three branches names rather than numbers (null / not-a-channel-factory / inherited-default), and reserve "condition 1" and "condition 2" for FR-12's meanings throughout.

---

### 6. Making `SharedGenerator` conditional is scope FR-12 does not ask for, on a harm that is asserted rather than shown (Score: 60)

The counts in step 4 are correct — four shared templates, fourteen existing configurations each declaring an outbox or gateway section, three existing `SharedGenerator` tests. What is missing is the harm. The four files land in the *project root*, which is outside the tree the generated-tree audit scopes, so they are clutter no CI job will fail on; and the ADR does not say whether `DefaultMessageBuilder.cs` and friends would even compile in `AzureServiceBus.Tests`, `MQTT.Tests` and `RMQ.Sync.Tests` — the one fact that would settle the question either way. "Unrelated … never asked for them" is an aesthetic argument being used to justify a behavioural change to a shared generator plus a fourth test, in a spec whose functional requirement is a regression sweep.

**Evidence**: ADR 0073:304-307, against `GeneratedTreeAudit.cs:56` ("Scope is the `Generated/` tree") and `SharedGenerator.cs:34`.

**Recommendation**: Either establish the concrete harm — if any of the four fails to compile in a project with no outbox and no gateway configuration, say so and the step justifies itself — or drop step 4 and accept twelve extra checked-in files, or declare the deviation by amending `requirements.md`, which is this spec's stated convention for work the requirements do not own.

---

### 7. Subsumption is under-specified for a generic ancestor — the one case the design claims to handle (Score: 60)

A candidate is dropped when "an ancestor in its base chain is also a candidate in the same assembly". The candidate set comes from `Assembly.GetTypes()`, which yields open generic *definitions* (`Foo<>`); a base chain yields closed *constructions* (`Foo<Bar>`). The pipeline order in the Architecture Overview runs subsumption *before* closing, so the comparison is definition against construction, and the ADR never says whether it is made via `GetGenericTypeDefinition()`. Two implementers resolve this differently, and the difference decides whether a `FooBar : Foo<Bar>` that declares no override is reported or silently dropped — exactly the "hide a real defect" risk the Risks section claims to have closed.

No shipped assembly has that shape today (all twelve are non-generic base + generic derived), so this is future-facing; but the ADR devotes a pipeline stage to generics and a Risks bullet to subsumption, so the corner belongs in the contract.

**Evidence**: ADR 0073:104 (`candidates -> subsume -> close generics -> read -> Check`), :162-164, :376-378.

**Recommendation**: State that ancestry is matched on the generic type definition where the ancestor is a constructed generic, and add the three-level/generic-ancestor case to the synthetic tests step 2 already introduces.

---

### 8. ADR 0064's "rules must not catch" is paraphrased with a rationale 0064 does not give (Score: 55)

The distinction the ADR draws — a startup validation rule is not a test guard, so catching and reporting every fault is right here — is sound, and the decision to catch is right. But the stated reason 0064 gives is not 0064's reason. 0064's rule exists because the `Specification<T>` framework already catches; exceptions reach the *framework*, which converts them to an `Error` finding. Nothing in 0064 says exceptions must reach the host.

**Evidence**: ADR 0073:185-187, against ADR 0064:165 ("The `Specification<T>` framework already wraps rule evaluation in a `try`/`catch` … and reports any rule-body exception as a `ValidationSeverity.Error`") and :171 ("it deviated from the solution's established rule pattern (rules must not catch; the framework reports evaluation errors)").

**Recommendation**: Restate as: 0064's rule exists because `Specification<T>` already provides a catch that converts a rule-body exception into a finding; a static sweep has no such surrounding framework, so catching per type is how it obtains the equivalent behaviour. That argument is stronger than the one currently written and it is the one 0064 actually supports.

---

### 9. "Four additions to `tools/Paramore.Brighter.Test.Generator`" does not match the four bullets (Score: 45)

The second bullet contains two additions (a new `Configuration/GatewayConformanceConfiguration.cs` *and* a new property on the existing `TestConfiguration`); the fourth bullet is not an addition at all but the output path and namespace the template uses. A fifth real edit — `Program.cs` invoking the new generator — appears only later, in Implementation step 3.

**Evidence**: ADR 0073:193 against the bullets at :195-210 and step 3 at :301-302.

**Recommendation**: Drop the count, or list the file-level additions (template, `GatewayConformanceConfiguration.cs`, `GatewayConformanceGenerator.cs`) and the edits (`TestConfiguration`, `Program.cs`, `GeneratedTreeAudit`) separately.

---

### 10. Reference-list omission (Score: 40)

The grounded-references entry for the generator lists five files under `Generators/` but omits `OutboxGenerator.cs`, which the ADR relies on twice — at :302-303 (`GeneratedTreeAudit.ExpectedFilesUnder` adds the new `Plan` "alongside `OutboxGenerator.Plan`") and at :440. `Generators/` on disk holds six files.

**Evidence**: ADR 0073:439 against the directory listing: `BaseGenerator.cs`, `GenerationSuite.cs`, `MessagingGatewayGenerator.cs`, `OutboxGenerator.cs`, `PlannedFile.cs`, `SharedGenerator.cs`.

**Recommendation**: Add `OutboxGenerator` to the list.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 2 |

**Total findings**: 10
**Findings at or above threshold (60)**: 7
