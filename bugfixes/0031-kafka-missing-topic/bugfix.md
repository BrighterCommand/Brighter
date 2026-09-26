# Bugfix: Warn about missing-topic detection under the Kafka consumer protocol

**Linked Issue**: #4299
**Status**: Verified with regression caveat

## Symptom

With broker auto-topic creation disabled, `ConsumerGroupProtocol` and
`OnMissingChannel.Assume`, a missing topic can keep producing empty receives
without a subscription error. Classic reports an unknown-topic error instead.

## Confirmed Root Cause

KIP-848 deliberately permits missing-topic subscriptions without Classic's
subscription error. Assume bypasses metadata validation. A null consume result
therefore follows Brighter's existing empty-message path; there is no established
missing-topic exception being swallowed.

The pinned [librdkafka 2.15.0 documentation](https://github.com/confluentinc/librdkafka/blob/v2.15.0/INTRODUCTION.md#error-handling-changes)
describes the protocol difference. The original broker reproduction confirmed it
on Kafka 4.0.2. Empty receives and unassigned consumers cannot establish absence:
existing topics may be idle, rebalancing, or have fewer partitions than consumers.

Relevant code is in `KafkaMessagingGateway.EnsureTopic`,
`KafkaMessageConsumer.Receive`, and `ConsumerGroupProtocol.Apply`.

## Review Decision

The initial revision in commit `37b1e6b9d` introduced an existence-only metadata
check on first receive. Its tests passed, but that changed Assume's no-check
contract and added network, authorization, and timeout requirements. The
maintainer review on [PR #4424](https://github.com/BrighterCommand/Brighter/pull/4424)
selected a configuration-only startup warning instead. Moving the same broker
lookup to startup would not preserve Assume either.

The metadata check and its tests are removed in this revision. Their code and
historical verification remain available in the original commit; those results
are not evidence for the revised implementation.

## Fix

`KafkaConsumerValidationRules.MissingTopicDetection()` returns an
`ISpecification<Subscription>` from the Kafka assembly. Registering it before
`ValidatePipelines()` makes the existing startup validation path warn about a
declared `ConsumerGroupProtocol` combined with `OnMissingChannel.Assume`.

The rule performs no broker lookup, creates no client, executes no configuration
hooks, and does not change subscription settings. The warning names the
subscription and topic, explains the limitation, and suggests Validate when an
explicit check is required and permitted. It does not assert that a topic is
missing or block startup.

The core package gains no Kafka dependency. Custom protocol implementations and
protocol overrides inside `ConfigHook` are not evaluated by this static rule.
The guide and XML documentation make this boundary explicit.

The Consumer Assume generation flag remains false. The two generated exception
tests added by the first revision are removed because they require the rejected
runtime behavior; explicit Validate/Create coverage is retained.

See [ADR 0073](../../docs/adr/0073-kafka-consumer-protocol-missing-topic-detection.md)
and the [registration guide](../../docs/guides/kafka-missing-topic-warning.md).

## Regression Tests

`Validation/When_consumer_protocol_assumes_a_topic_should_warn_at_startup.cs`
exercises the public rule through DI, `ValidatePipelines()`, and the validation
hosted service, without a broker configuration or channel factory:

- Consumer, Classic, and default protocol crossed with Assume, Validate, and Create.
- A non-Kafka subscription using Assume.
- Warning severity, subscription/topic context, guidance, and startup logging.
- Startup with `throwOnError: true` does not throw for the warning.
- A throwing configuration hook is never executed; the provisioning policy is unchanged.

RED was established with a no-op rule: 1 failed, 9 passed on .NET 10, with the
failure caused by the absent warning. The implementation then passed all 10 cases.
The initial compilation failure for the missing rule is recorded separately.

## Verification

- Warning tests: 10 passed on .NET 9 Release. All 10 also passed in the .NET 10
  Release full suite, including the hosted-service log and configuration-hook assertions.
  A final .NET 10 run after removing the Kafka test containers passed all 10 again,
  confirming the warning path works without the broker running.
- CI selection correction: the warning test class now carries `Category=Kafka`.
  Before the tag was added, the CI filter selected none of these tests. Afterward,
  the exact CI filter discovers all 10 on each target framework, and a run using
  those category conditions restricted to this class passes all 10 on .NET 9 and
  all 10 on .NET 10. No test assertions or CI exclusions were changed.
- Full Kafka suite on .NET 10 Release: 210 passed, 2 failed, 0 skipped in
  13 minutes 28 seconds, against isolated Kafka 4.0.2 and Schema Registry 8.0.6.
  The failures were the existing Classic Reactor round-trip and PartitionKey
  Reactor zero-delay requeue cases, both receiving `MT_NONE` within their receive
  windows. Both passed unchanged in an isolated rerun (2 passed, 0 failed).
  This is not recorded as a completely green full-suite run. The local broker
  output also contained refused IPv6 loopback connections; no definitive cause
  for the intermittent failures was established.
- Generator tests and generated-tree audit: 292 passed on .NET 10. Kafka's files
  were regenerated with the Consumer Assume opt-out restored; Validate coverage remains.
- Kafka gateway Release build: .NET Standard 2.0 and .NET 8, 9, and 10 succeeded
  with 0 warnings and 0 errors. The test project retains existing compiler/analyzer warnings.
- A source comparison confirms `KafkaMessageConsumer` matches upstream commit
  `9aa737d96` after excluding XML documentation and blank lines. No receive-time
  behavior from the rejected implementation remains.
- ADR metadata validation and `git diff --check` passed. New-file whitespace checks passed.
- The complete .NET 9 Kafka suite was not run. No authenticated production broker
  or ACL-specific scenarios were tested; the new rule does not make broker requests.

Commands from the repository root:

```sh
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~KafkaMissingTopicWarningTests
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release -f net9.0 --filter FullyQualifiedName~KafkaMissingTopicWarningTests
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release --filter 'Category=Kafka&Category!=Confluent&Fragile!=CI&FullyQualifiedName~KafkaMissingTopicWarningTests'
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release -f net10.0
dotnet test tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj -f net10.0
dotnet build src/Paramore.Brighter.MessagingGateway.Kafka/Paramore.Brighter.MessagingGateway.Kafka.csproj -c Release
git diff --check
```
