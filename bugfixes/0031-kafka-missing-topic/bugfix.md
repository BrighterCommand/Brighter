# Bugfix: Surface a missing Kafka topic under the consumer protocol

**Linked Issue**: #4299
**Status**: Verified

## Symptom

With broker auto-topic creation disabled, `ConsumerGroupProtocol` and
`OnMissingChannel.Assume`, receiving from a missing topic reportedly keeps
returning `MT_NONE`. Classic protocol instead raises a channel failure wrapping
an unknown-topic consume error. Both sync and async receive paths are affected.

## Suspected Location

References describe base commit `f702408c3`.

- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagingGateway.cs:59`:
  Assume skips metadata validation; Validate/Create call `FindTopic` at line 64.
- `src/Paramore.Brighter.MessagingGateway.Kafka/ConsumerGroupProtocol.cs:60`:
  selects the consumer protocol.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:265`:
  installs the error callback, subscribes at line 269, and checks policy at line 279.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:509`:
  consumes; lines 511-516 return an empty message for a null result; lines 530-533
  wrap an actual consume exception. Async receive delegates here at line 573.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs:797`:
  only fatal callback errors set the fatal-error latch.
- `tests/Paramore.Brighter.Kafka.Tests/test-configuration.json:25`:
  disables missing-infrastructure detection under Assume for Consumer protocol.

## Root-Cause Hypothesis

Initial hypothesis: KIP-848 does not report the missing subscription topic through the
consume error path used by Classic. Assume intentionally bypasses metadata
validation, so the client returns no record and Brighter returns `MT_NONE`.
There may be no missing-topic exception for Brighter to propagate. An alternative
is that a usable nonfatal callback error is merely logged.

## Confirmed Root Cause

Independent review confirms the protocol-level explanation from the pinned
upstream documentation and Brighter's code trace. A lost exception inside Brighter
has not been established. The local regression run now reproduces the missing
failure through the public receive APIs on Kafka 4.0.2.

KIP-848 permits missing-topic subscriptions without Classic's missing-topic
subscription error. Assume performs no independent existence check. A null
consume result therefore takes the existing empty-message branch, with no
missing-topic exception to rethrow.

This exposes a contract mismatch: `OnMissingChannel.Assume` promises both to avoid
existence checks and to fail fast if infrastructure is missing. Empty KIP-848
receives cannot establish absence. The diagnosis and fail-fast direction are
approved, and the regression tests were approved before execution and implementation.

## Evidence

The pinned Confluent.Kafka version is 2.15.0 (`Directory.Packages.props:39`).
The [librdkafka 2.15.0 protocol documentation](https://github.com/confluentinc/librdkafka/blob/v2.15.0/INTRODUCTION.md#error-handling-changes)
states that missing-topic subscription errors differ under KIP-848; subscription
can proceed while the topic is absent. The initial local regression run on .NET 10
reported 8 failed and 4 passed: missing-topic, later-recovery prerequisite, and
unreachable-metadata cases returned without the expected channel failure; the
existing-empty-topic controls passed.

- `src/Paramore.Brighter/OnMissingChannel.cs:47-52` documents Assume's
  no-check/fail-fast contract and the cost avoidance it provides.
- `KafkaMessagingGateway.cs:59-69` bypasses checks for Assume and performs them
  for Validate/Create.
- `KafkaMessageConsumer.cs:509-516` returns an empty message after a null poll;
  lines 530-533 preserve actual consume exceptions as channel failures.
- `KafkaMessageConsumer.cs:797-808` latches fatal callback errors and logs
  nonfatal ones. This is not evidence that KIP-848 emits a missing-topic callback.
- `KafkaMessagingGateway.cs:115-133` validates existence, partition count, and
  replication count. Reusing this whole routine for Assume would import more
  behavior than simple absence detection.

The abbreviated Kafka source paths above are relative to
`src/Paramore.Brighter.MessagingGateway.Kafka/`.

The current test providers do contain `RetryableChannelSync` and
`RetryableChannelAsync`, unlike the issue's historical description. They preserve
channel failures and rethrow after the receive budget expires. The generated
Classic Assume test sends through a producer first and accepts any non-xUnit
exception, so its passing result alone cannot prove consumer error behavior.

## Scope Notes

Do not interpret an empty receive or an unassigned
partition set as proof of a missing topic: existing topics can legitimately be
empty, rebalancing, or have more consumers than partitions.

`OnMissingChannel.Assume` explicitly avoids provisioning checks. Adding an
existence probe needs an agreed policy for timing, recovery, and metadata errors;
it must not silently become full `Validate` mode, which also validates partition
and replication counts. The existing ConsumerGroupProtocol XML remarks about
weaker Validate guarantees are inconsistent with the actual metadata path.

A blind catch/propagate patch is not supported by the diagnosis. The selected
direction is an explicit metadata existence check for KIP-848 under Assume,
instead of documenting the current silent behavior as a permanent limitation.
Upstream maintainer acceptance of this policy is still subject to PR review.

The proposed check occurs on the first receive, bounded independently by the
existing `topicFindTimeout`. Successful detection is cached for the consumer's
lifetime; failures remain retryable. This is startup detection, not continuous
monitoring for topics deleted after successful detection. It must use the
effective client protocol and connection settings after `configHook`, not just
the supplied protocol object. It checks existence only, without validating
partition or replica counts. Classic and Validate/Create keep their policies.

The first receive can therefore take up to the metadata timeout in addition to
its normal poll timeout, including when a zero poll timeout is requested. This
protocol-specific exception to Assume's no-check promise needs explicit XML
documentation and a proposed ADR before production implementation.

## Regression Test

The proposed consumer-only integration tests are in
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_a_consumer_protocol_topic_is_missing_should_fail_fast_on_receive.cs`.
They use a real local Kafka broker with auto-topic creation disabled and exercise
public sync/async receive methods without a producer in the missing-topic cases.

The 12 cases cover:

- Missing-topic failures on the first receive, including when `configHook`
  selects the effective consumer protocol (4 cases).
- Existing empty topics returning no message, despite different configured
  partition and replica counts, under both Classic and Consumer (4 cases).
- The same Consumer-protocol instance receiving a message after an initial
  missing-topic failure and later topic creation (2 cases).
- A metadata connection failure honoring the configured timeout and retaining
  its actual error instead of being reported as a missing topic (2 cases).

Topic names and group IDs are unique. Teardown targets only the topic a case
attempted to create. Setup and recovery use bounded metadata/receive polling.
Existing generated Validate/Create coverage will also be run after approval.

All 12 regression cases now pass on .NET 9 and .NET 10 against Kafka 4.0.2.
The recovery fixture needed the producer's normal `Init()` call before publishing;
that setup correction did not change the expected consumer behavior.

## Fix

`KafkaMessageConsumer.Receive` performs the bounded existence-only check through
a dependent admin client using the consumer's handle. Its existing Kafka exception
handling surfaces failures as channel failures, and success is cached. The async
receive path delegates to the same code. The effective client protocol controls
the check, including overrides applied by `configHook`.

The Kafka Consumer configuration no longer opts out of Assume conformance. Its
two missing-topic tests were regenerated using the repository generator. The
feature-flag documentation no longer describes Kafka as an outstanding exception.

XML documentation describes the first-receive timeout cost and corrects the old
claim that explicit Validate had weaker guarantees under KIP-848.
[ADR 0073](../../docs/adr/0073-kafka-consumer-protocol-missing-topic-detection.md)
records the policy as Proposed, pending upstream maintainer acceptance.

## Verification

- Regression tests: 12 passed on .NET 10 (Debug) and .NET 9 (Release), no skips.
  The .NET 10 Release full-suite run also includes all 12 regression cases.
- Generator tests, including the generated-tree audit: 292 passed on .NET 10.
- Assume/Validate conformance checks: 12 passed on .NET 9, covering all three
  Kafka configurations through both sync and async paths.
- Kafka gateway Release build: .NET Standard 2.0 and .NET 8, 9, and 10 succeeded
  with 0 warnings and 0 errors. The test project has existing compiler/analyzer
  warnings, including obsolete wire message-type usage.
- ADR frontmatter validation passed. The final staged whitespace check reports
  three trailing spaces inherited from the unchanged templates in the two newly
  generated Assume tests; the remaining changes pass the whitespace check.
- Full Kafka suite on .NET 10 Release: 216 passed, 0 failed, and 0 skipped in
  13 minutes 29 seconds, using Kafka 4.0.2 and schema registry 8.0.6. This includes
  the two restored Consumer Assume conformance tests and existing Validate/Create
  coverage. The entire .NET 9 suite was not run; its focused runs are listed above.
- The broker was local and unauthenticated; live production clusters and
  authenticated/ACL-denial scenarios were not exercised.

Commands (from the repository root, with Kafka and schema registry running):

```sh
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -f net10.0 --filter FullyQualifiedName~KafkaMissingTopicDetectionTests
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release -f net9.0 --filter FullyQualifiedName~KafkaMissingTopicDetectionTests
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release -f net9.0 --filter 'FullyQualifiedName~WhenInfrastructureMissingAndAssumeChannel|FullyQualifiedName~WhenInfrastructureMissingAndValidateChannel'
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release -f net10.0
dotnet test tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj -f net10.0
dotnet build src/Paramore.Brighter.MessagingGateway.Kafka/Paramore.Brighter.MessagingGateway.Kafka.csproj -c Release --no-restore
git diff --check
```
