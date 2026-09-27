# Review: code — 0037-validate-subscription-channel-factory

**Date**: 2026-09-27
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address it before approving.

## Findings

### 1. The combined-arm handed clause leaves out "one of", which ADR 0072's message grammar requires (Score: 62)

ADR 0072 §3 sets out the message body as two clauses that vary independently. The handed clause has three forms:
- direct: `will be handed '{F}'`
- combined, non-empty: `will be handed one of '{F-list}'`
- combined, empty: `will be handed no channel factory at all`

The code builds the same handed clause for the direct arm and the non-empty combined arm, so "one of" never appears. A combined-arm finding therefore reads "…but will be handed 'A, B' — …". That says the subscription gets every inner factory, when `CombinedChannelFactory` routes to at most one. It is the same distinction FR-5 draws for T2 ("is one of" rather than "is").

Nothing in tasks.md, the commits or the ADR records a reason for the change. No test catches it: AC-7, AC-13b, AC-13c and AC-30 only check `Contains` and `EndsWith` on the names and the remedy literal, and a grep for `will be handed` in `tests/` returns nothing.

**Evidence**:
- `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` (in `ChannelFactoryCompatible`):
  ```csharp
  var handedClause = candidates.Count == 0
      ? "no channel factory at all"
      : $"'{string.Join(", ", candidates.Select(DisplayName))}'";
  ```
  This has no `arm == Arm.Combined` branch.
- The grammar it departs from: `docs/adr/0072-subscription-channel-factory-compatibility.md:394-397`.

**Recommendation**: Split the handed clause on arm, so the non-empty combined form renders `one of '{F-list}'`. Add an assertion to the AC-7 test (or a new test) that pins the combined-arm body wording.

---

### 2. The new MQTT correction tests (AC-21, AC-25b, AC-26b, AC-26f) never run in CI (Score: 55)

The MQTT CI job filters with `Category=MQTT&Fragile!=CI` (`.github/workflows/ci.yml:250`). The four new MQTT test classes have no `[Trait("Category","MQTT")]`, so that job never selects them. The new build-job step only runs `FullyQualifiedName~GatewayChannelFactoryDeclarationTests`, so it doesn't pick them up either.

Task 55 (tasks.md:962) and the ci.yml comment both name this trap explicitly ("an untagged test in those projects would never be selected"). The fix was applied to the sweep only, not to the sibling MQTT tests from task 32.

The sweep still catches a revert to `MqttMessageConsumerFactory`, because that type is not a channel factory. It would not catch:
- the exact-type assertion (AC-21);
- the routing decision (AC-26b);
- the rule-silent case (AC-26f).

The existing MQTT channel-factory tests are also untagged, so the gap predates this branch. But this feature added four more tests in the same position while knowing about it.

**Evidence**: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/When_reading_the_channel_factory_type_of_an_mqtt_subscription_should_be_the_mqtt_channel_factory.cs`, `…When_checking_the_mqtt_declared_channel_factory_should_be_a_real_channel_factory.cs`, `…When_matching_an_mqtt_subscription_against_a_combined_factory_should_select_one_inner_factory.cs` and `…When_validating_a_corrected_mqtt_subscription_should_report_no_findings.cs`. `grep -L Trait` lists all four.

**Recommendation**: Widen the build-job step's filter for `MQTT.Tests` to include these four classes, or give them a trait the build job runs.

---

### 3. The release note for the C-12 breaking change misdescribes its trigger (Score: 50)

C-12 applies to a `CombinedChannelFactory` that **contains** an `InMemoryChannelFactory` inner factory. The old `ChannelFactoryType == InMemoryChannelFactory` matched that inner factory by exact type (`CombinedChannelFactory.cs:34`).

The release note instead says the combined factory "with no matching inner factory would silently fall through to `InMemoryChannelFactory` at routing time". `CombinedChannelFactory` has never fallen through; with no match it throws. A reader following the note would look for the wrong configuration. C-8/C-12 require the note to name the symptom and remedy accurately.

**Evidence**: `release_notes.md` (the "AWS SQS, AWS SQS V4 and Postgres subscriptions routed through an in-memory `CombinedChannelFactory` slot" section): "a `CombinedChannelFactory` with no matching inner factory would silently fall through to `InMemoryChannelFactory` at routing time". Compare requirements.md C-12 at lines 290-292.

**Recommendation**: Reword it along these lines: "a `CombinedChannelFactory` that included an `InMemoryChannelFactory` previously matched these subscriptions by exact type and routed them to the in-memory bus."

---

### 4. FR-2a's back-fill invariance is only checked for one of the configurations it covers (Score: 45)

AC-5 says "any configuration from AC-2, AC-3 or AC-4". FR-2a goes further: the step-3 case (a null default, with DI substituting a fresh `InMemoryChannelFactory`) "MUST be asserted, not assumed".

The only AC-5 test checks the AC-4 shape: a mismatched default written back into `subscription.ChannelFactory`. Two cases are never checked before and after back-fill:
- the compatible cases (AC-2, AC-3);
- the step-3 case, where `ChannelFactoryCompatible(null)` is compared with the subscription back-filled with a new `InMemoryChannelFactory`.

The code does in fact hold the invariance: the direct arm renders `typeof(InMemoryChannelFactory)` either way.

**Evidence**: `tests/Paramore.Brighter.Core.Tests/Validation/When_the_default_channel_factory_has_been_back_filled_should_report_identical_findings.cs` is a single scenario using `NonMatchingChannelFactory` only.

**Recommendation**: Add a case with a transport-declaring double, `ChannelFactoryCompatible(null)`, and a back-fill with `new InMemoryChannelFactory(...)`. Assert identical Source and Message.

---

### 5. Two new test files hold more than one test (Score: 30)

testing.md:102 lists "one test per file" as a convention that holds in every gear.

**Evidence**:
- `tests/Paramore.Brighter.Core.Tests/Validation/When_the_configuration_resolves_to_the_in_memory_channel_factory_should_report_no_findings.cs` has two `[Fact]`s. The second, `When_the_configuration_explicitly_uses_the_in_memory_channel_factory…`, does not match the file name.
- `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/When_auditing_synthetic_gateway_conformance_configuration_should_report_unconfigured_and_doubly_configured_gateways.cs` has three test attributes.

**Recommendation**: Split each into one file per test, with matching `When_…` names.

---

### 6. `GatewayConformanceConfiguration.Category` is dead configuration (Score: 20)

The XML doc says the property is "Unused by the sweep guard; present for symmetry with the other configuration sections". The template never renders it and no configuration sets it. That makes it a speculative member, which code_style and design_principles both disallow.

**Evidence**: `tools/Paramore.Brighter.Test.Generator/Configuration/GatewayConformanceConfiguration.cs` (the `Category` property). The Liquid template doesn't reference `Category`.

**Recommendation**: Remove it, or render it as a trait if a category is actually wanted. A trait would also help with finding 2.

---

### 7. Hand-written new files carry a 2014 licence year (Score: 20)

documentation.md:93-95 says a new file takes the contributor's name and the current year. Several new hand-written files copy `Copyright © 2014`. The other new files on the branch (for example `SubscriptionChannelFactoryDeclaration.cs`) correctly use 2026.

**Evidence**:
- `tools/Paramore.Brighter.Test.Generator/Configuration/GatewayConformanceConfiguration.cs`
- `tools/…/Generators/GatewayConformanceGenerator.cs`
- `tools/…/Templates/GatewayConformance/…cs.liquid`, which propagates the year into all twelve generated sweeps
- `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/GatewayConformanceAudit.cs` and its two audit tests

**Recommendation**: Change the year to 2026 in the hand-written files and the template, then regenerate.

---

### 8. A test's method name doesn't match its file name, plus a redundant null-conditional (Score: 15)

- **Name mismatch**: in `tests/Paramore.Brighter.Extensions.Tests/When_a_channel_factory_mismatch_is_validated_with_throw_on_error_should_fail_startup.cs` the method is `…_with_throw_on_error_true_should_fail_startup`.
- **Redundant `?.`**: `SubscriptionChannelFactoryDeclaration.Check` uses `declaredFactoryType?.FullName` after an explicit `is null` early return, so the `?.` does nothing.

**Evidence**: the two locations above.

**Recommendation**: Make the method name match the file name, and drop the `?.`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 5 |

**Total findings**: 8
**Findings at or above threshold (60)**: 1

**What checks out:**
- All five transport corrections (FR-7 to FR-11) are present and minimal.
- The rule is registered as the fifth spec using `GetService`, per ADR 0072 §4.
- The direct and combined arms, the null-`D` handling, all five remedy templates and the display names match the requirements.
- The AC-9 and AC-10b companion `ConfigurationException` assertions are present, and AC-15 uses the normative regex.
- All twelve generated sweeps exist, and each asserts the exact subject set with every `Reason` null.
- The thirteenth-gateway audit enumerates the 12 `src/Paramore.Brighter.MessagingGateway.*` directories.
- All 59 tasks are ticked and have matching commits; each behaviour commit carries its test with it.
- No skipped tests, commented-out asserts or security issues found.
- The four `SharedGenerator` helper files in each of the three new projects are an accepted, documented cost (ADR 0073 around line 632; tasks.md line 918), not scope creep.
- The untracked out-of-scope files noted in the brief are still present and were not reviewed.
