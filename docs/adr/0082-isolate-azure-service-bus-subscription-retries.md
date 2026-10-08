---
id: 0082-isolate-azure-service-bus-subscription-retries
title: "Isolate Azure Service Bus subscription retries"
status: Proposed
author:
  - "Irakli Gabisonia"
created: 2026-10-08
summary: "Abandon immediate subscription retries and route delayed retries through a dedicated forwarding queue. Compatibility policy remains proposed."
tags:
  - "transports"
  - "retry"
  - "configuration"
---

# 82. Isolate Azure Service Bus subscription retries

Date: 2026-10-08

## Status

Proposed. The release compatibility policy requires agreement before acceptance.

## Context

Republishing a failed delivery to its topic sends another copy to every matching subscription.
Subscribers that already succeeded can process the same event again.
Azure Service Bus supports scheduled sends to queues, but not directly to individual topic subscriptions.

### Scope

Parent requirement: [Issue #4546](https://github.com/BrighterCommand/Brighter/issues/4546).

In scope:

- Preserve the handled count when abandoning an immediate retry.
- Provision a dedicated forwarding queue and send delayed retries to it.
- Reject unsafe delayed retries before publishing or settling the original.
- Protect externally managed forwarding queues from destructive purge.

Out of scope:

- Automatic migration of existing subscriptions or their pending messages.
- Changes to publisher routing or the message pump's retry budget.
- Changes to the lock renewal policy described in [ADR 0079](0079-azure-service-bus-lock-renewal.md).

### Retries must remain with the failing subscriber

| Retry destination | Who receives the retry? |
|---|---|
| Original topic | Every matching subscription |
| Abandoned original delivery | The original subscription |
| Dedicated forwarding queue | Consumers of that queue |

### The forces

- Independent subscribers must not share a retry destination.
- Existing deployments may lack permission to modify broker topology.
- Native abandon increments the broker delivery count.
- Scheduling must survive the consumer process exiting.
- Sync and async consumers must follow the same routing policy.

## Decision

**Use native abandon for immediate direct-subscription retries and dedicated forwarding queues for delayed retries.**

The proposed implementation rejects positive delays on direct subscriptions.
Configured delays fail during consumer creation; per-message delays fail before any send or settlement.
This strict policy is a breaking change and remains subject to acceptance.

### The mechanism, end to end

| Condition | Action |
|---|---|
| Direct subscription, no delay | Abandon with the updated handled count |
| Direct subscription, positive delay | Raise a configuration error |
| Forwarding configured | Receive and retry through the destination queue |
| Queue retry accepted by the broker | Acknowledge the original delivery |
| Existing forwarding destination differs | Require explicit migration |
| Purge with Validate or Assume | Reject before closing the receiver or deleting the queue |

The logical topic stays in the message header while the retry producer uses a separate physical destination.
Native session IDs are retained so retries remain valid for session-enabled destination queues.

### Where the pieces live

All routing and provisioning changes live in the Azure Service Bus gateway assembly.
The consumer factory selects the forwarding consumer and supplies its queue producer.
The forwarding consumer provisions and validates broker entities through the administration wrapper.
The receiver wrapper performs property-preserving abandon through the Azure SDK.
Core message-pump and publisher APIs remain unchanged.

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Responsibilities | Responsibility classifier | Collaborators |
|---|---|---|---|---|
| Retry policy | `AzureServiceBusConsumer` | Select abandon or send; acknowledge after sending | Deciding, doing | Receiver and producer |
| Forwarding endpoint | `AzureServiceBusForwardingConsumer` | Provision and validate the destination; receive from it | Knowing, doing | Administration wrapper, receiver provider |
| Property-preserving release | `IAmAServiceBusRetryReceiver` | Persist properties while releasing a delivery | Doing | Azure SDK receiver |

#### Retry contracts

| Member | Input | Output | Error conditions |
|---|---|---|---|
| `ForwardTo` | Destination queue name | Dedicated receive and retry endpoint | Blank name, absolute URI, or combined queue mode |
| `RequeueAsync` | Message and optional delay | Original abandoned or replacement sent and original acknowledged | Delayed direct retry; unsupported receiver; broker failure |
| `PurgeAsync` | Cancellation token | Destination deleted and recreated in Create mode | Validate and Assume reject before mutation |

#### Where each type is touched

The consumer factory, message producer and administration wrapper implement forwarding within the gateway assembly.
The message creator preserves native session IDs.
The existing receiver interface remains source-compatible; immediate retries require the additional receiver capability.

### Technology Choices

#### Use broker scheduling for delays

Scheduling the replacement in the destination queue avoids holding a delivery lock during the delay.
The broker retains the scheduled message independently of the consumer process.

#### Keep topology migration explicit

Existing subscriptions are validated rather than silently reconfigured.
Operators must account for pending deliveries and provision forwarding before restarting migrated consumers.

### Implementation Approach

1. Add broker regressions for retry isolation and metadata preservation.
2. Implement property-preserving abandon and forwarding queue provisioning.
3. Guard unsupported delayed retries and destructive purge modes.
4. Run conformance scenarios through the supported forwarding topology.

## Consequences

### Positive

- Retries no longer reach unrelated subscriptions through the supported configuration.
- Unsafe direct delayed retries fail before publishing another copy.
- Publishers retain their existing topic routing.

### Negative

- Existing delayed direct-subscription configurations fail startup under the proposed strict policy.
- Native delivery limits can exhaust before Brighter's retry budget after abandon.
- Delayed retries require an additional queue and explicit migration.

### Risks and Mitigations

- Emulator coverage does not replace validation in Azure; verify migration and recovery in staging.
- Existing custom receiver providers require the additional abandon capability for immediate retries.
- Handler-specific delays on direct subscriptions cause failed requeue handling; configure forwarding before enabling those delays.

## Alternatives Considered

A compatible opt-in release can retain legacy retry behavior until forwarding is configured.
That avoids startup failures but leaves unmigrated applications exposed to duplicate processing.
Immediate abandon can also be opt-in to preserve existing delivery-count behavior.
This is the proposed alternative for a patch or minor release; the strict policy may require a major release.

Automatically forwarding every subscription would change infrastructure and permissions for existing deployments.
Holding a delivery lock during a delay ties retry timing and recovery to the consumer process.
Retaining topic republication without migration guidance leaves the reported failure unresolved.

## References

- [Issue #4546](https://github.com/BrighterCommand/Brighter/issues/4546)
- [Regression evidence and migration guidance](../../bugfixes/0052-asb-subscription-retry-isolation/bugfix.md)
- [Delivery count contract](0077-delivery-count-contract.md)
