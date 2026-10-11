---
id: 0082-isolate-azure-service-bus-subscription-retries
title: "Isolate Azure Service Bus subscription retries"
status: Proposed
author:
  - "Irakli Gabisonia"
created: 2026-10-08
summary: "Keep subscription retries isolated using native abandon and queue-targeted broker scheduling."
tags:
  - "transports"
  - "retry"
  - "configuration"
---

# 82. Isolate Azure Service Bus subscription retries

Date: 2026-10-08

## Status

Proposed for maintainer review.

## Context

Republishing a failed delivery to its topic sends another copy to every matching subscription.
Subscribers that already succeeded can process the same event again.
Azure Service Bus supports scheduled sends to queues, but not directly to individual topic subscriptions.

### Scope

Parent requirement: [Issue #4546](https://github.com/BrighterCommand/Brighter/issues/4546).

In scope:

- Preserve the handled count when abandoning an immediate retry.
- Provision a dedicated forwarding queue and send delayed retries to it.
- Fall back to immediate abandon with a warning when a direct subscription requests a delay.
- Give queue retries distinct delivery identities compatible with duplicate detection.
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

**Use native abandon for direct-subscription retries and explicit native/global scheduler selection for queue retries.**

A direct subscription with a positive delay remains valid at startup. At requeue time the consumer
abandons immediately, persists the handled count and warns that `ForwardTo` is required to honor
the delay. It never republishes to the topic. This applies equally to configured and handler-specific
delays. Forwarding remains an explicit topology choice.

### The mechanism, end to end

| Condition | Action |
|---|---|
| Direct subscription, no delay | Abandon with the updated handled count |
| Direct subscription, positive delay | Warn and abandon immediately with the updated handled count |
| Forwarding or queue mode, native scheduler | Receive from the queue and schedule retries there using a new delivery ID |
| Forwarding or queue mode, global scheduler | Schedule to that queue name using the global scheduler's configured backend |
| Queue retry accepted by the broker | Acknowledge the original delivery |
| Existing forwarding destination differs | Require explicit migration |
| Purge with Validate or Assume | Reject before closing the receiver or deleting the queue |

The logical topic stays in the message header while the retry scheduler uses a separate physical destination.
Native session IDs are retained so retries remain valid for session-enabled destination queues.

### Where the pieces live

Routing and provisioning changes live in the Azure Service Bus gateway assembly.
The scheduler capability below is additive to core; its native implementation lives in the gateway.
The consumer factory selects the consumer and injects its configured retry scheduler.
The forwarding consumer provisions and validates broker entities through the administration wrapper.
The receiver wrapper performs property-preserving abandon through the Azure SDK.
Core adds the optional retry capability interfaces and publication selection; message-pump APIs remain unchanged.

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Responsibilities | Responsibility classifier | Collaborators |
|---|---|---|---|---|
| Retry policy | `AzureServiceBusConsumer` | Select abandon or queue scheduling; acknowledge after acceptance | Deciding, doing | Receiver and retry scheduler |
| Forwarding endpoint | `AzureServiceBusForwardingConsumer` | Provision and validate the destination; receive from it | Knowing, doing | Administration wrapper, receiver provider |
| Property-preserving release | `IAmAServiceBusRetryReceiver` | Persist properties while releasing a delivery | Doing | Azure SDK receiver |

#### Retry contracts

| Member | Input | Output | Error conditions |
|---|---|---|---|
| `ForwardTo` | Destination queue name | Dedicated receive and retry endpoint | Blank name, absolute URI, subscription/subqueue path, or combined queue mode |
| `Requeue` / `RequeueAsync` | Message and optional delay | Original abandoned or replacement scheduled and original acknowledged | Unsupported receiver/scheduler; broker failure |
| `PurgeAsync` | Cancellation token | Destination deleted and recreated in Create mode | Validate and Assume reject before mutation |

#### Where each type is touched

The consumer factory, forwarding consumer and administration wrapper implement forwarding within the gateway assembly.
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
3. Warn on delays that cannot be honored, and guard destructive purge modes.
4. Run conformance scenarios through the supported forwarding topology.

## Queue-targeted scheduler capability

The [scheduler proposal](https://github.com/BrighterCommand/Brighter/issues/4546#issuecomment-6080723364)
is implemented through optional `IAmAMessageRequeueSchedulerAsync` and
`IAmAMessageRequeueSchedulerSync` capabilities. Existing scheduling contracts remain unchanged.

`Publication.RequeueScheduler` selects `Native` (the default) or `Global`. Pass the
`AzureServiceBusPublication` to the consumer factory to apply that selection. Native scheduling uses
the consuming gateway's client, so registering a global scheduler cannot silently move retries to
another broker. Explicit global selection uses the global scheduler's configured backend, including
its namespace; deployments choosing this mode must arrange consumption of that destination.

The factory selects and injects the scheduler into the consumer. Producers no longer implement retry
scheduling. Reactor calls `IAmAMessageRequeueSchedulerSync` on its calling thread; Proactor calls
`IAmAMessageRequeueSchedulerAsync`. The factory rejects an absent or incompatible global capability
before channel provisioning. Global scheduling also requires a physical queue destination.

`AzureServiceBusRequeueScheduler` in the gateway provides native scheduling on an explicit physical
queue, separate from the message's logical topic. It is public because the gateway consumer factory
and the Azure scheduler package both use it. Subscription and reserved subqueue paths are rejected.
`AzureServiceBusSchedulerFactory` exposes the requeue capability by delegating to that implementation.
Its ordinary scheduled commands and requests retain their existing envelope behavior. Retries use
`ScheduleMessageAsync` directly on the queue, including for zero delays, without a `FireAzureScheduler`
envelope. The Azure SDK only exposes asynchronous I/O; its sync bridge stays inside the adapter.

The native scheduler records Brighter producer telemetry on the current activity and propagates trace
context through a copy, preserving the caller's message. Local-only headers are excluded; diagnostic
serialization failures are logged without preventing scheduling or trace propagation.
Context propagation failures are also contained within telemetry. Existing baggage keys can
be updated at capacity; excess or unsupported ambient baggage cannot prevent delivery or
the remaining trace headers from propagating. The received message is never modified.
Sender acquisition uses five retries with a
100 ms interval, matching the producer's connection policy. Cancellation interrupts the wait.
Broker errors become `ChannelFailureException`; cancellation retains its cancellation exception.
Sender cleanup errors are logged and cannot replace the scheduling outcome.

No management calls are added to the scheduling path. `Assume` supports externally managed topology
and does not require Manage rights. Queue requeue requires Send rights on the destination in addition
to the consumer's Listen rights. Direct-subscription abandon only requires the consumer's Listen rights.

### Retry identity and settlement

A bounded broker MessageId is derived from the incoming delivery's MessageId, physical queue and
updated handled count. This makes successive deliveries distinct while keeping repeated scheduling
of the same delivery stable within the broker's duplicate-detection window. The retry's CloudEvents
ID remains the original event ID (the incoming CloudEvents ID, or the first delivery ID if absent). `x-original-message-id` retains the first delivery's identity across
retries. Body bytes, correlation, session and other metadata are preserved; the input message remains
unchanged so acknowledgement still refers to its original lock.

The original is acknowledged only after the scheduling call succeeds. Scheduling errors and
cancellation propagate without acknowledgement; acknowledgement errors also propagate. Send and
settlement are not transactional: duplicates remain possible outside the broker's deduplication
window or with duplicate detection disabled. Handlers must remain idempotent.

Each retry sender is disposed after scheduling; the provider owns the shared client. This bounds
sender lifetime without an unbounded destination cache, at the cost of opening a sender per retry. Sender caching, broader `ValidatePipelines` diagnostics,
and native capabilities for other brokers remain separate follow-up work.

Retries are new broker deliveries. They use the destination queue's default TTL, not the original
message's remaining lifetime or a publisher's `TimeToLive`. Applications requiring an absolute expiry
must carry and enforce a business deadline. Session IDs are retained, but delayed retries may be
processed after later messages in the same session. Scheduling failures leave the original unsettled;
subsequent abandon or lock expiry increments broker delivery count and can exhaust `MaxDeliveryCount`
before Brighter's handled-count budget.

### Composition compatibility

Manual queue-consumer construction now takes a scheduler in place of a producer; direct topic consumers
no longer take a producer. The abstract consumer constructor takes a nullable scheduler instead of a
producer and no longer has the unused `isAsync` parameter. Consumer schedulers are constructor-injected,
not mutable properties. Factory construction without a publication remains available and defaults to
native retries. This is an intentional source/binary break for manually composed consumers; rebuild
and update those call sites. The consumer factory is the preferred composition entry point.

### Release validation

The native capability is implemented for Azure Service Bus. Other transports and schedulers retain
their existing behavior. Validate permissions, recovery, throughput and topology migration in a
representative Azure environment before release; emulator and injected I/O tests cannot establish
all these operational properties. The public documentation site needs the same migration guidance
when this feature is published.

## Consequences

### Positive

- Retries no longer reach unrelated subscriptions through the supported configuration.
- Direct subscriptions keep starting and never broadcast retries, even when a delay was requested.
- Native queue retries survive duplicate detection without requiring an optional scheduler.
- Publishers retain their existing topic routing.

### Negative

- Direct subscriptions lose the requested delay until forwarding is configured.
- Native delivery limits can exhaust before Brighter's retry budget after abandon. Configure
  `MaxDeliveryCount` above `RequeueCount`, allowing additional broker redeliveries as appropriate.
- Delayed retries require an additional queue and explicit migration.

### Risks and Mitigations

- Emulator coverage does not replace validation in Azure; verify migration and recovery in staging.
- Existing custom receiver providers require the additional abandon capability for immediate retries.
- Handler-specific delays on direct subscriptions fall back to immediate retry; migrate to forwarding
  when retaining the delay matters.

## Alternatives Considered

Rejecting delayed direct subscriptions at startup would enforce migration but prevent existing
applications from starting. Warning and immediate abandon preserve startup while removing broadcasts.
Retaining legacy topic republication until opt-in would leave the reported failure unresolved.

Automatically forwarding every subscription would change infrastructure and permissions for existing deployments.
Holding a delivery lock during a delay ties retry timing and recovery to the consumer process.
Retaining topic republication without migration guidance leaves the reported failure unresolved.

## References

- [Issue #4546](https://github.com/BrighterCommand/Brighter/issues/4546)
- [Regression evidence and migration guidance](../../bugfixes/0052-asb-subscription-retry-isolation/bugfix.md)
- [Deployment and recovery guide](../guides/azure-service-bus-retries.md)
- [Delivery count contract](0077-delivery-count-contract.md)
