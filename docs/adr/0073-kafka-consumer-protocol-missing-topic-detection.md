---
id: 0073-kafka-consumer-protocol-missing-topic-detection
title: "Kafka Consumer Protocol Missing-Topic Detection"
status: Proposed
author:
  - "Avtandil Ushikishvili"
created: 2026-09-26
summary: "Preserve Assume's no-check behavior for KIP-848 and expose its missing-topic limitation through an opt-in, configuration-only startup warning registered from the Kafka assembly."
tags:
  - "kafka"
  - "configuration"
  - "observability"
---

# 73. Kafka Consumer Protocol Missing-Topic Detection

Date: 2026-09-26

## Status

Proposed

## Context

KIP-848 permits subscription to a missing topic without the subscription error
reported by Classic. Under `OnMissingChannel.Assume`, receiving no messages can
therefore mean either a missing topic or a healthy idle consumer. Empty receives
and empty assignments cannot distinguish these cases: rebalances and consumer
groups with more members than partitions are also legitimate.

Assume deliberately avoids infrastructure checks. Adding a metadata lookup to
the receive path introduces an extra network request, authorization requirements,
and latency into a policy selected to avoid those checks. Moving the same lookup
to startup would not resolve that contradiction.

## Decision

Keep Assume's receive behavior unchanged. Provide a configuration-only warning
for subscriptions declaring `ConsumerGroupProtocol` and `OnMissingChannel.Assume`.
The warning identifies the subscription and topic, explains that missing topics
do not produce subscription errors, and points to Validate when an explicit
infrastructure check is required and permitted. It does not claim the topic is
missing or prevent startup.

Define the rule in the Kafka assembly as an `ISpecification<Subscription>`.
Applications register it with dependency injection before calling
`ValidatePipelines()`. The existing pipeline validator discovers registered
subscription specifications, and the existing startup services log their
warnings. Core references only its validation abstraction, never Kafka types.
No new core dependency or validator-loading mechanism is required.

The rule inspects the declared protocol object. It does not construct a Kafka
client, connect to a broker, execute a configuration hook, or mutate subscription
settings. Protocol selection performed only by a custom `IGroupProtocol` or
`ConfigHook` is outside this static check. Executing arbitrary configuration
callbacks during validation could have side effects and would not guarantee the
same result when the actual consumer is created.

Retain the Consumer configuration's opt-out from generated Assume missing-topic
exception tests. Explicit Validate/Create coverage remains enabled. Clarify the
general Assume documentation: whether missing infrastructure produces a runtime
error depends on the transport.

## Consequences

- Teams can retain Assume without additional broker requests or permissions.
- Opted-in startup validation makes the limitation visible even if the topic exists.
- Warning-severity findings do not block startup, including with `throwOnError: true`.
- Registration is explicit; merely referencing the Kafka package does not enable the rule.
- No new per-empty-receive warning is added. Existing logging and telemetry remain
  available, but an idle consumer is not treated as proof of failure.
- Missing topics still require operational investigation or explicit validation;
  this change makes a limitation visible rather than detecting topic existence.

## Alternatives Considered

### Mandatory metadata check during receive or startup

Conflicts with Assume's no-check policy and can add failures and latency unrelated
to receiving messages. A successful metadata lookup would also not guarantee the
topic remains present later.

### Documentation alone

Preserves the contract but does not alert operators when an application starts
with the affected configuration. A non-blocking configuration warning complements
documentation without contacting the broker.

### Treat empty receives or missing assignments as errors

Cannot distinguish a missing topic from valid idle or rebalancing consumers and
would introduce false alarms.

## References

- [Issue #4299](https://github.com/BrighterCommand/Brighter/issues/4299)
- [PR #4424](https://github.com/BrighterCommand/Brighter/pull/4424)
- [librdkafka 2.15.0 error-handling changes](https://github.com/confluentinc/librdkafka/blob/v2.15.0/INTRODUCTION.md#error-handling-changes)
- [Kafka missing-topic startup warning](../guides/kafka-missing-topic-warning.md)
