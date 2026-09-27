# Review: code — 0037-validate-subscription-channel-factory (round 2)

**Date**: 2026-09-27
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Fix verification (round 1 findings 1-3)

**Fix 1: combined-arm "one of" wording (`286b99cad`). Confirmed correct.** `ConsumerValidationRules.cs:132-136` now builds the handed clause in this order: an empty candidate set gives `no channel factory at all`; `arm == Arm.Combined` gives `one of '{F-list}'`; anything else gives `'{F}'`. This matches the grammar in ADR 0072 (lines 392-397): direct gives `will be handed '{F}'`, combined non-empty gives `will be handed one of '{F-list}'`, combined empty gives `no channel factory at all`. The direct-arm branch is unchanged. The empty-set check still runs first, so AC-10c cannot render `one of ''`. The fix did not touch `RemedyClause` (lines 244-263), so the T2/T3b `is one of:` literals remain byte-identical to FR-5's templates. The AC-10b and AC-13c tests still pin those literals (`...null_channel_factory_type_in_the_combined_arm...cs:55`, `...declared_transport_type_should_list_the_alternatives.cs:57`). The new assertion `Assert.Contains("will be handed one of '", message)` sits on the AC-7 test (line 59). The existing `DoesNotContain(CombinedChannelFactory.FullName)` still holds, because `{F-list}` comes from `FactoryTypes`. All 155 `Core.Tests.Validation` tests on net10.0 pass.

**Fix 2: MQTT `Category` trait (`f32f86c3e`). Confirmed correct.** The commit adds exactly one line, `[Trait("Category", "MQTT")]`, to each of the four class declarations and changes nothing else. All four tests only construct objects:
- AC-21 and AC-25b are reflection-only.
- AC-26b and AC-26f build `MqttMessagingGatewayConsumerConfiguration`, then `ChannelFactory`, then `CombinedChannelFactory`, and never call `CreateSyncChannel`/`CreateAsyncChannel`.

So they are still broker-free, and the MQTT job's `Category=MQTT&Fragile!=CI` filter (`ci.yml:250`) now selects them. The other four transports were checked for the same gap and none was found:
- AWS and AWS V4: the new tests carry no `LiveAWS` trait, so both `aws-mock-ci` (`LiveAWS!=true`) and `aws-ci` (`Fragile!=CI`) select them.
- Postgres: its job filters on `Fragile!=CI` only.
- GCP: its job only excludes the Spanner and Stream categories, which the new tests don't carry.
- The generated MQTT sweep has no MQTT trait either, but the build job runs every sweep by `FullyQualifiedName~GatewayChannelFactoryDeclarationTests` (`ci.yml:80-91`).

**Fix 3: C-12 release note (`4454ba094`). Confirmed correct.** On master, `Subscription.ChannelFactoryType` defaults to `typeof(InMemoryChannelFactory)` (`master:src/Paramore.Brighter/Subscription.cs:172`). `CombinedChannelFactory.CreateSyncChannel`, `CreateAsyncChannel` and `CreateAsyncChannelAsync` (`CombinedChannelFactory.cs:47-81`) each select with `_factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType)` and throw `ConfigurationException("No channel factory found for subscription {name}")` when nothing matches. The rewritten note (`release_notes.md:41-50`) now describes exactly that:
- Before the fix, the AWS SQS, AWS SQS V4 and Postgres subscriptions inherited `InMemoryChannelFactory`, so they matched an `InMemoryChannelFactory` inner factory by exact type and were routed to the in-memory bus.
- After the fix, they declare their real type, so a `CombinedChannelFactory` with no matching inner factory throws when the Dispatcher starts.

The "silently fall through" wording is gone. The note matches the section heading ("routed through an in-memory `CombinedChannelFactory` slot") and requirements.md line 273. The remedy and the "`throwOnError: false` does NOT avoid it" warning are unchanged and still accurate.

## Findings

### 1. The direct-arm handed clause has no test pinning it (Score: 15)

Fix 1 pins the combined-arm handed clause with a single positive `Contains`. Nothing asserts the direct arm's `will be handed '{F}'` form. In particular, nothing asserts that the direct arm does *not* say `one of`. If someone later collapsed the ternary the other way, putting `one of` on both arms, every test would still pass. This is the same gap round 1 found, now closed for one arm but not the other.

**Evidence**: `grep -rn "will be handed" tests/` returns only `When_no_inner_factory_can_serve_a_subscription_should_name_the_inner_factories.cs:59`. The direct-arm branch is `ConsumerValidationRules.cs:136`.

**Recommendation**: Add `Assert.Contains("will be handed '", message)` and `Assert.DoesNotContain("one of", message)` to a direct-arm test, e.g. `When_a_plain_subscription_is_handed_a_different_channel_factory_should_report_one_error.cs`.

---

### 2. Process-tooling changes bundled into the feature branch (Score: 15)

The branch changes the TDD workflow tooling:
- `.claude/commands/spec/implement.md` (+33)
- `.claude/commands/spec/ralph-implement.md` (+98)
- `.claude/commands/tdd/test-first.md` (+14)
- `.agent_instructions/testing.md` (+60)
- an amendment to ADR 0071

None of this traces to any FR, AC or decision in spec 0037. It came out of the spec's tasks-review rounds: the commit messages for `f249e32f2`, `306e92075` and `f5f9e1492` say so. It is docs-only, carries no runtime risk and is documented, but it widens the PR beyond "validate a subscription against its channel factory".

**Evidence**: `git diff --stat master...HEAD` lists the files above. The commits are `f249e32f2`, `306e92075`, `f5f9e1492` and `f966f8c3b`.

**Recommendation**: Mention it in the PR description, or split it into its own PR if the maintainer prefers PRs with a single concern. No code change needed.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

Other checks: `git status --porcelain` shows only the three untracked files noted as out of scope. Round-1 findings 4-8 (left unfixed by maintainer choice) were not re-flagged.
