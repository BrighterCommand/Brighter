---
id: 0073-azure-service-bus-publication-time-to-live
title: "Configure Azure Service Bus message time to live per publication"
status: Proposed
author:
  - "Irakli Gabisonia"
created: 2026-09-26
summary: "Add an optional time to live to Azure Service Bus publications and apply it when constructing outgoing SDK messages, preserving entity defaults when unset."
tags:
  - "transports"
  - "configuration"
  - "publish"
  - "bulk-messaging"
---

# 73. Configure Azure Service Bus message time to live per publication

Date: 2026-09-26

## Status

Proposed

## Context

Azure Service Bus entities can carry messages with different useful lifetimes.
Brighter exposes an entity-level default but cannot currently set the outgoing
SDK message's time to live. Applications therefore need a custom producer to
expire one publication's messages sooner than the entity default permits.
See [issue #4411](https://github.com/BrighterCommand/Brighter/issues/4411).

## Decision

Add a nullable `TimeSpan` property, `TimeToLive`, to `AzureServiceBusPublication`.
Reject zero and negative durations when configuring the publication. A null value
leaves the SDK message's TTL unset, retaining the entity default.

Apply the publication's TTL when constructing outgoing SDK messages for ordinary,
scheduled, and bulk sends. Set it before SDK batch admission so batch size checks
include the TTL header; preserve it when a message needs the single-message fallback.
Keep the existing public conversion and batching method signatures unchanged.

Publication configuration keeps this capability within the Azure Service Bus
transport. A per-message context or header override would require additional
mapping and serialization conventions; a transport-neutral header would require
agreement on semantics across brokers. Neither is needed for publication-level TTL.

## Consequences

Applications can configure shorter lifetimes per publication without changing
message mappers or persisted outbox messages. TTL comes from the producer's
publication when messages are converted for sending or batching; it is not a
per-request value persisted in the outbox.

Azure Service Bus still caps the message lifetime at the entity default. Broker
expiration and dead-lettering behavior remain controlled by Azure Service Bus.
Existing publications that leave `TimeToLive` unset retain their current behavior.
