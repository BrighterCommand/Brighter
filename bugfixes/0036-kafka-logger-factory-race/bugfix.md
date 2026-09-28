# Bugfix: KafkaMissingTopicWarningTests fails with "collection was empty" in kafka-ci

**Linked Issue**: #4447
**Status**: Verified

## Symptom
**Observed.** In kafka-ci run 36365863761 / job 108753642430 (PR #4282, head `980541a72`), `KafkaMissingTopicWarningTests.When_consumer_protocol_assumes_a_topic_should_warn_at_startup(protocol: Consumer, policy: Assume, expectWarning: True)` failed with `Assert.Single() Failure: The collection was empty`. The other 8 theory rows and the `[Fact]` passed.

**What the CI log actually shows** (pulled with `gh run view --log`). Two points contradict the issue text:
1. **The stack frame points to line 100, not line 108.** The frame is `When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs(100,0)`, which is `var warning = Assert.Single(result.Warnings);`. That is the in-memory validator result. The *logged* warning check is `Assert.Single(loggedWarnings)` at line 108. The issue says `result.Warnings` "evidently still passes" and the logged collection is the empty one. The trace says the reverse, unless the Release-build line mapping is wrong (see Hypothesis B).
2. **The "retry" is the second target framework, not a retry.** The job builds and tests both `NET9_0` and `NET10_0` in Release. The failures at 01:36:08 (xUnit 00:00:00.58) and 01:36:11 (xUnit 00:00:00.81) are one run per TFM — the test failed on **both** frameworks in the same job. That weakens "intermittent": in that CI environment it may be deterministic.

**Expected.** For Consumer + Assume, `result.Warnings` holds exactly one KIP-848 warning, and the TestCorrelator captures exactly one matching logged Warning event.

**Reproduction (CI).** `dotnet test ./tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj --filter "Category=Kafka&Category!=Confluent&Fragile!=CI" --configuration Release` (`.github/workflows/ci.yml:394`). No local reproduction yet.

**Code version.** PR head `980541a72` is an ancestor of `master` (0 ahead, 1 behind). The test file at that SHA is byte-identical to local `master` (`bb10b8fae`). The failing code is current master.

## Suspected Location
- `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs`
  - `:44-45` the class has `[Trait("Category","Kafka")]` but no `[Collection(...)]` attribute, so xUnit puts it in its own default collection that runs in parallel with the others.
  - `:61` `using var logContext = TestCorrelator.CreateContext();`
  - `:79-84` builds the provider, calls `Validate()`, then runs `StartAsync` on every `IHostedService`.
  - `:90-92` collects `loggedWarnings` from `TestCorrelator.GetLogEventsFromCurrentContext()`.
  - `:100` `Assert.Single(result.Warnings)`, the line CI reports.
  - `:108` `Assert.Single(loggedWarnings)`, the line the issue assumes.
  - `:127-141` `CreateProvider`: `AddLogging(... AddSerilog(new LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger(), dispose: true))` (`:130-131`), registers `KafkaConsumerValidationRules.MissingTopicDetection()` (`:137-138`), then `ValidatePipelines(throwOnError: true)` (`:139`).
- `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterValidationHostedService.cs`
  - `:49`, `:58-67` the logger is a constructor-injected `ILogger<BrighterValidationHostedService>` taken from the test's own container. It is **not** a `static readonly` field and not `ApplicationLogging`.
  - `:73-74` returns early (no logging) if `ConsumerOwnsValidation` is set.
  - `:76` runs `Validate()` a second time.
  - `:90-93` `_logger.LogWarning("Pipeline validation warning from {Source}: {Message}", ...)`. This is the only place the KIP-848 warning gets logged.
- `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs`
  - `:58-100` `ValidatePipelines`. The validator factory resolves consumer specs from `sp.GetServices<ISpecification<Subscription>>()` (`:79-80`) and subscriptions from `IAmConsumerOptions` (`:144-149`), and registers `BrighterValidationHostedService` (`:96`).
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaConsumerValidationRules.cs:45-58` the rule itself: a pure pattern match on `KafkaSubscription { GroupProtocol: ConsumerGroupProtocol, MakeChannels: Assume }`. It has no logger.
- `src/Paramore.Brighter/Validation/PipelineValidator.cs:97-109` `Validate()`, `:175-178` `ValidateConsumers`, `:182-201` `EvaluateSpecs`.
- `src/Paramore.Brighter/Specification.cs:76` mutable instance field `_lastResults`, `:114-119` `IsSatisfiedBy`, `:175-200` `EvaluateSimple`. `ValidationResultCollector.Visit` returns `specification.LastResults`. The spec keeps state between `IsSatisfiedBy` and `Accept` — harmless single-threaded, but the only mutable state on the validation path.
- `src/Paramore.Brighter/Validation/PipelineValidationResult.cs` `Warnings` is materialized once with `warnings.ToList()`, so it does not re-evaluate lazily.
- `src/Paramore.Brighter/Logging/ApplicationLogging.cs:7` `public static ILoggerFactory LoggerFactory { get; set; } = new LoggerFactory();` confirms a process-wide mutable static. It is reassigned at `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:705` (`BuildCommandProcessor`) and `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:141` (`BuildDispatcher`).
- `tests/Paramore.Brighter.Kafka.Tests/Initializer.cs:10-15` a `[ModuleInitializer]` sets `ApplicationLogging.LoggerFactory` to its own Serilog logger with a **second, process-wide TestCorrelator sink** (`MinimumLevel.Debug()`). TestCorrelator 4.0.2 keeps captured events in a static queue and tracks context ids with `AsyncLocal`.
- Parallelization:
  - The test project has no `[CollectionDefinition]`, `CollectionBehavior` or `DisableParallelization` anywhere (grep of `tests/Paramore.Brighter.Kafka.Tests`, excluding bin/obj, found nothing).
  - No xunit runner json; the only json is `test-configuration.json`.
  - Other TestCorrelator-based tests use an undefined-but-shared `[Collection("Kafka")]`, e.g. `MessagingGateway/When_error_log_level_returns_none_should_suppress_logging.cs:12` and `MessagingGateway/Proactor/When_an_async_produce_throws_should_warn_and_synthesize_not_persisted.cs:13`. Tests inside that collection run serially with each other, but in parallel with `KafkaMissingTopicWarningTests`.
- Referenced prior fix `49e19c3b7`: not reachable. `git show` gives "unknown revision", and it is not in the `origin/spec/scoped-lifetime-per-pipeline` commit list. The remedy actually applied in `Extensions.Tests` could not be inspected. A related remote branch `origin/fix/instance-scoped-logger-factory` exists.

## Root-Cause Hypothesis
_(superseded — see Confirmed Root Cause below. The static-`ApplicationLogging.LoggerFactory` race
from the original issue is WRONG; the real cause is a DI registration collision. See also the
"stale-clone correction" note under Evidence: an earlier confirm pass on this bug wrongly concluded
the defect didn't exist on master, because the local clone hadn't fetched a PR that merged into
`origin/master` during this session.)_

## Confirmed Root Cause

`BrighterPipelineValidationExtensions.ValidatePipelines()` registers **two** implementations under
the same service type, `IAmAPipelineValidator`:

- `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs:79`
  — `builder.Services.TryAddSingleton<IAmAPipelineValidator>(sp => ... PipelineValidator ...)`,
  which receives the consumer specs (including the KIP-848 `MissingTopicDetection` rule) and is the
  one that evaluates them.
- `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs:106`
  — `builder.Services.AddSingleton<IAmAPipelineValidator>(sp => ... ScopeConfigurationValidator ...)`,
  registered *after* the first, with no consumer specs at all.

With Microsoft.Extensions.DependencyInjection, `GetRequiredService<T>()` (and `GetService<T>()`)
resolve the **last** registration for a given service type. Any caller that resolves a single
`IAmAPipelineValidator` instead of `IEnumerable<IAmAPipelineValidator>` silently gets
`ScopeConfigurationValidator`, not `PipelineValidator` — so consumer-rule warnings (like the KIP-848
"assumes a topic" warning) never get evaluated and `result.Warnings` is always empty for that caller.

`tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:82`
does exactly this (`provider.GetRequiredService<IAmAPipelineValidator>().Validate()`), so for the
`Consumer + Assume` theory row `Assert.Single(result.Warnings)` at line 100 throws
"The collection was empty" — deterministically, on every run, both TFMs. This is **not** a race and
**not** related to `ApplicationLogging`/TestCorrelator at all; the original issue's hypothesis is
wrong. The correct fix pattern already exists in production code:
`src/Paramore.Brighter.Extensions.DependencyInjection/BrighterValidationHostedService.cs:49,78` uses
`IEnumerable<IAmAPipelineValidator>` + `PipelineValidationResult.Combine(_validators.Select(v =>
v.Validate()).ToArray())` — the Kafka test (and several others, see Scope Notes) never got updated
to that pattern.

## Evidence

- [x] **Red repro (local, no infra needed).** After fast-forwarding the local clone to
  `origin/master` (`ea294324d`, which merged PR #4282 "Spec 0036: scoped lifetime per pipeline" at
  2026-09-28T06:42:43+01:00 — **during this session**, so the earlier confirm attempt was working
  from a stale pre-merge clone and wrongly concluded the defect wasn't on master yet), running:
  `dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj --filter "FullyQualifiedName~KafkaMissingTopicWarningTests" -c Release`
  reproduces the exact CI failure on both TFMs:
  ```
  Paramore.Brighter.Kafka.Tests.Validation.KafkaMissingTopicWarningTests.When_consumer_protocol_assumes_a_topic_should_warn_at_startup(protocol: Consumer, policy: Assume, expectWarning: True) [FAIL]
  Assert.Single() Failure: The collection was empty
    at ...When_consumer_protocol_assumes_a_topic_should_warn_at_startup(...) in .../When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:line 100
  Failed! - Failed: 1, Passed: 9, Skipped: 0, Total: 10 (net10.0)
  Failed! - Failed: 1, Passed: 9, Skipped: 0, Total: 10 (net9.0)
  ```
  This matches the CI stack trace (line 100) exactly and is deterministic (both TFMs fail every
  time), confirming hypothesis "A" from triage and refuting hypothesis "B" (no Release line
  mis-mapping) and "C" (it is real, current, reproducible — not a stale/flaky artifact).
- [x] **Code-trace** confirming the mechanism: the dual registration at
  `BrighterPipelineValidationExtensions.cs:79` and `:106`, and the correct multi-resolve pattern at
  `BrighterValidationHostedService.cs:49,78`, both verified present on `origin/master` after fetch.

## Scope Notes

The same last-registration-wins defect affects every other caller that resolves a single
`IAmAPipelineValidator` instead of `IEnumerable<IAmAPipelineValidator>`. Grepped repo-wide on current
master — these are the only call sites:

- `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:119`
  — the `[Fact]` asserts `Assert.Empty(result.Warnings)`. This passes **vacuously**
  (`ScopeConfigurationValidator` also produces no warnings) — a silent loss of coverage, not a
  visible failure. The fix must update this call site too, or the test stops meaning anything.
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox.cs:58`
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox_async.cs:59`
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings.cs:57`
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings_async.cs:58`
  — all four assert `Assert.Empty(validation.Errors)` only, which also passes vacuously against
  `ScopeConfigurationValidator`. Same silent-coverage-loss shape as above; not currently visible as a
  failing test, but the assertions are not testing what they claim to.

No other Kafka-specific validation rules are affected — `KafkaConsumerValidationRules.cs` has only
the one rule (`MissingTopicDetection`), and it is a pure predicate with no defect of its own.

**Design-intent check (requested before approving the fix direction).** The dual registration is
**not** an oversight — it is the deliberately-designed outcome of
`docs/adr/0074-lifetime-validation-evaluation-site.md` (spec 0036, just-merged PR #4282), which
explicitly:
- Decides "the seven rules are evaluated by a second validator that the container package
  contributes, and both validation hosts resolve every registered validator and combine the
  results" (ADR 0074, Decision, ~line 148-152).
- Names the exact DI hazard in play and accepts the cost: "A plain `AddSingleton` alongside would
  leave Microsoft's container resolving the last unkeyed descriptor, and the core validator's
  findings would silently disappear. Making the seam a pull is what this ADR pays for, and it is a
  one-line change in each host." (~line 339).
- Prescribes the exact `TryAddSingleton` (for `PipelineValidator`) + `AddSingleton` (for
  `ScopeConfigurationValidator`) pairing verbatim, with the comment "`AddSingleton`, not `TryAdd`,
  because `TryAdd` tests the service type and would never add a second implementation of it"
  (~line 343-363) — this is the "TryAdd over Add" shift and last-registration-wins discussion.
- Is recorded as an intentional, accepted breaking change in `release_notes.md:23`: "an application
  that registers its own `IAmAPipelineValidator` no longer replaces Brighter's validation wholesale;
  both are now run and their findings combined... any caller constructing it directly must migrate."
- Names the two production call sites it migrated (`BrighterValidationHostedService` →
  `IEnumerable<IAmAPipelineValidator>` + `Combine`, `ServiceActivatorHostedService` → `GetServices`)
  — both confirmed already correct on master.

**What the ADR does not cover:** any caller that bypasses the hosted services and resolves a single
`IAmAPipelineValidator` directly — exactly the shape of the 6 call sites above. Nothing in ADR 0074
(or 0070/0071/0072/0073/0075/0076, checked for the same terms) discusses migrating test call sites,
and there is no analyzer/guard that would have caught them.

**Conclusion: this is a complete, correct design with an incomplete migration sweep — not a design
defect.** The fix is properly scoped to a bugfix (migrate the missed call sites to
`provider.GetServices<IAmAPipelineValidator>()` + `PipelineValidationResult.Combine(...)`, mirroring
the ADR-mandated pattern already in `BrighterValidationHostedService.cs:49,78`), not a new ADR or
architectural change.

**Fix-scope decision (user-approved at the Confirm gate):** cover all 6 missed call sites in the
regression test / fix, not just the originally-reported Kafka case — they share the identical root
cause and ADR-mandated remedy:
1. `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:82` (the reported case)
2. `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:119`
3. `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox.cs:58`
4. `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox_async.cs:59`
5. `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings.cs:57`
6. `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings_async.cs:58`

**Documented for future recurrence:** this "multiple implementations under one service type, last-wins on single-resolve" pattern is counter-intuitive and easy to reintroduce elsewhere. Captured as a general design principle in `.agent_instructions/design_principles.md` (new bullet before "Decide visibility by what belongs on the package boundary"), so it's checked the next time a second implementation is added under an existing service type.

## Regression Test

**No new test file was needed.** The pre-existing test already pins the reported defect, and an
experiment confirmed the other 5 approved-scope sites have no independent red state to pin.

- **Regression pin (already red, confirmed twice — in `/bugfix:confirm` and again just before
  `/bugfix:fix`):**
  `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:82,100`
  (`When_consumer_protocol_assumes_a_topic_should_warn_at_startup`, `Consumer + Assume` theory row).
  Fails today with `Assert.Single() Failure: The collection was empty` on both net9.0 and net10.0.
  Expected to flip to green once the resolve call at `:82` becomes
  `PipelineValidationResult.Combine(provider.GetServices<IAmAPipelineValidator>().Select(v => v.Validate()).ToArray())`.
- **Verified-safe, no new red state (temporarily patched all 4 to the combine pattern + the Kafka
  `Fact`, ran them, reverted with `git checkout`):**
  - `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:119` (`When_subscription_is_not_kafka_should_not_warn_about_missing_kafka_topics`) — stayed green.
  - `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox.cs:58` — stayed green.
  - `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox_async.cs:59` — stayed green.
  - `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings.cs:57` — stayed green.
  - `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings_async.cs:58` — stayed green.
  - Full Release run after patching: Kafka `KafkaMissingTopicWarningTests` 10/10 pass (net9.0 + net10.0);
    Extensions `ConsumerGlobalInboxTests`/`ConsumerExplicitInbox*` 6/6 pass (net9.0 + net10.0).
  - Conclusion: no hidden second defect behind the vacuous passes — fixing these 5 is a pure
    correctness/consistency change (bringing them in line with the ADR 0074-mandated resolve
    pattern), not a behavior fix, so no new failing test is warranted for them.
- **User-approved test-first gate**: accepted the existing failing test as the regression pin and
  confirmed no new test files are needed for the other 5 sites (see conversation).

## Fix

Minimal, single `fix:` commit — no tidy-first needed (each change is a localized one-line resolve
swap plus a `using System.Linq;` where missing). Migrated all 6 approved-scope call sites from
single-resolve (`GetRequiredService<IAmAPipelineValidator>().Validate()`) to the ADR
0074-mandated multi-resolve pattern
(`PipelineValidationResult.Combine(provider.GetServices<IAmAPipelineValidator>().Select(v => v.Validate()).ToArray())`),
mirroring `BrighterValidationHostedService.cs:49,78`:

- `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:82` — the reported case.
- `tests/Paramore.Brighter.Kafka.Tests/Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs:119`
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox.cs:58` (+ added `using System.Linq;`)
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_deduplicate_using_the_global_inbox_async.cs:59` (+ added `using System.Linq;`)
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings.cs:57` (+ added `using System.Linq;`)
- `tests/Paramore.Brighter.Extensions.Tests/When_consumers_have_no_producers_should_preserve_explicit_inbox_settings_async.cs:58` (+ added `using System.Linq;`)

**Verification:**
- `KafkaMissingTopicWarningTests`: 10/10 pass on net9.0 and net10.0 (was 9/10, `Consumer + Assume`
  row failing).
- `ConsumerGlobalInboxTests` / `ConsumerExplicitInbox*`: 6/6 pass on net9.0 and net10.0 (unchanged
  from before the fix — confirms no hidden second defect, per the Regression Test section).
- `git diff --stat`: 6 test files changed, 32 insertions / 6 deletions, no production code touched
  — the defect and its fix are entirely in test call sites that hadn't migrated to the ADR
  0074-mandated pattern.

## Verify (`/bugfix:verify`)

- **Full `Paramore.Brighter.Kafka.Tests` suite, against live Kafka broker infra** (`docker-compose-kafka.yaml`,
  matching CI's `Category=Kafka&Category!=Confluent&Fragile!=CI` filter): **183/183 pass on both
  net9.0 and net10.0** (~10m per TFM). No regressions.
- **Full `Paramore.Brighter.Extensions.Tests` suite**: net9.0 514/514 pass; net10.0 510/511 pass with
  1 pre-existing, unrelated failure —
  `FailedBuildScopeDisposalLoggingTests.When_a_failed_build_scope_release_throws_it_should_log_at_error_and_not_mask_the_build_failure`
  (expects `ConfigurationException`, gets `InvalidOperationException` from a poisoned dependency's
  `Dispose()` on net10.0 only). **Verified pre-existing**: stashed this fix's changes entirely and
  reran the same test in isolation — it fails identically without the fix. Different subsystem
  (build-scope disposal exception typing, not pipeline-validator resolution); not touched by this
  change and not a new regression.
- No cross-backend parity concerns — the confirmed cause and fix are Kafka/Extensions test-only; no
  other transport/broker call sites resolve `IAmAPipelineValidator` singly (see Scope Notes).
