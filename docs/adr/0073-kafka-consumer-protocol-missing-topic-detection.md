---
id: 0073-kafka-consumer-protocol-missing-topic-detection
title: "Kafka Consumer Protocol Missing-Topic Detection"
status: Proposed
author:
  - "Avtandil Ushikishvili"
created: 2026-09-26
summary: "Perform a bounded, existence-only metadata check on the first receive for KIP-848 consumers using Assume, cache success, and leave failed checks retryable. Classic and explicit provisioning policies remain unchanged."
tags:
  - "kafka"
  - "provisioning"
  - "error-handling"
---

# 73. Kafka Consumer Protocol Missing-Topic Detection

Date: 2026-09-26

## Status

Proposed

## Context

`OnMissingChannel.Assume` promises to avoid infrastructure checks while failing
fast when infrastructure is missing. Classic Kafka consumers can meet that
contract through consume errors. KIP-848 deliberately permits subscription to a
missing topic without the corresponding subscription error. A receive can then
return no message indefinitely, indistinguishable from a healthy empty topic.

Brighter cannot repair this by propagating a missing exception. Nor can it infer
absence from an empty receive or an empty assignment: rebalances and consumer
groups with more members than partitions legitimately produce those states.

## Decision

For the effective KIP-848 consumer protocol with `OnMissingChannel.Assume`, check
topic metadata on the first receive, before polling. Use the consumer's existing
client handle so connection and security settings applied through `configHook`
are respected. Both synchronous and asynchronous receives use this same path.

The check confirms existence only. It neither creates infrastructure nor validates
partition or replication counts. Preserve the broker's error code and reason in
the channel failure; connection and authorization errors must not masquerade as
missing topics. Do not latch ordinary absence as a fatal consumer error.

Bound the check by the existing `topicFindTimeout`, independently of the receive
poll timeout. This can add that timeout to the first receive, including a receive
with a zero poll timeout. A successful check is cached for the consumer lifetime;
a failed check remains eligible for retry on a later receive. This allows the
same consumer to recover if the topic is subsequently provisioned.

This is a documented exception to Assume's no-check behavior, limited to the
Kafka Consumer protocol. Classic and explicit Validate/Create behavior remain
unchanged. It is startup detection, not continuous monitoring for topic deletion
after a successful check.

## Consequences

- Missing topics produce a channel failure instead of an indefinite empty receive.
- Empty topics and temporary lack of assignment are not failures.
- The first successful receive path pays for one metadata request. Failed checks
  may repeat until successful; existing caller retry policies govern retries.
- A zero poll timeout does not make the initial metadata check nonblocking.
- Metadata permissions and availability are required for the initial check.
- A topic can still be deleted after detection; the cached check is not a lifetime
  guarantee that infrastructure remains available.
- Maintainers must review this protocol-specific exception before accepting the
  proposed policy.

## Alternatives Considered

### Document the limitation and recommend Validate

This preserves Assume's no-check promise but leaves its fail-fast promise unmet
and allows misconfigured consumers to appear idle indefinitely.

### Reuse full Validate behavior

This also enforces partition and replication settings, changing more than missing
topic detection. The existence-only check avoids rejecting usable topics whose
shape differs from configuration intended for provisioning.

### Check on every empty receive

This adds recurring metadata traffic for healthy idle consumers and still needs
the same distinction between missing topics, unavailable brokers, and permissions.
Startup detection is sufficient for the reported missing-topic subscription case.

### Treat missing assignments as fatal

This incorrectly rejects healthy consumers during rebalances and consumers with
no assigned partition. It would also prevent recovery after later provisioning.

## References

- [Issue #4299](https://github.com/BrighterCommand/Brighter/issues/4299)
- [librdkafka 2.15.0 error-handling changes](https://github.com/confluentinc/librdkafka/blob/v2.15.0/INTRODUCTION.md#error-handling-changes)
- [OnMissingChannel contract](../../src/Paramore.Brighter/OnMissingChannel.cs)
