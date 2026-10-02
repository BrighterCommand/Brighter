---
id: 0079-azure-service-bus-lock-renewal
title: "Renew Azure Service Bus locks while messages are pending"
status: Proposed
author:
  - "Irakli Gabisonia"
created: 2026-10-01
summary: "Bound automatic message and session lock renewal, protect buffered deliveries, and stop renewal when delivery ownership ends."
tags:
  - "transports"
  - "configuration"
  - "service-activator"
---

# 79. Renew Azure Service Bus locks while messages are pending

Date: 2026-10-01

## Status

Proposed

## Context

A successful handler can outlive its Azure Service Bus message lock.
Its acknowledgment then fails, causing redelivery and eventual dead-lettering.
Batch reception also starts locks for messages waiting in the channel buffer.
Brighter currently renews neither message locks nor session locks.

### Scope

Parent requirement: [issue #4487](https://github.com/BrighterCommand/Brighter/issues/4487).

In scope:

- Renew locks from reception through settlement, including buffered messages.
- Bound renewal through subscription configuration.
- Skip expired or lost buffered deliveries in the Azure channel factory's channels.
- Renew session locks and retain the receiver until its pending batch is settled.
- Stop renewal on settlement, disposal, receiver replacement, purge, and failed batch mapping.

Out of scope:

- Cancelling a handler already running when its lock is lost.
- Guaranteeing exactly-once delivery through broker outages or process pauses.
- Implementing renewal for third-party receiver-wrapper implementations.
- Adding transport-specific lock semantics to the shared channel classes.

### Long handlers and buffered messages lose their locks

| Delivery | Previous behavior | Required behavior |
| --- | --- | --- |
| Handler exceeds the initial lock | Completion fails | Renew while within the configured budget |
| Message waits behind another handler | Initial lock keeps expiring | Renew from batch receipt |
| Buffered lock expires or renewal reports loss | Handler still runs | Discard the stale delivery and allow broker redelivery |
| First message of a session batch completes | Receiver closes | Retain the receiver for the remaining messages |

### The forces

- Queue and topic consumers share their receive and settlement implementation.
- Sessions require renewal of the session lock, not each message lock.
- Existing public wrapper interfaces must remain compatible with custom implementations.
- Background work must finish before the receiver is closed.
- A finite renewal budget must not become an infinite retry loop.

## Decision

**Renew broker locks while received messages remain pending, subject to a finite subscription budget.**
The Azure receiver wrapper owns renewal and tracks current lock validity.
Azure-specific channel subclasses check that validity before handing a message to the pump.

### The mechanism, end to end

| Event | Action |
| --- | --- |
| Batch received | Track each token and start renewal; session messages share one renewal task |
| Renewal due | Renew before expiry, using the current broker deadline |
| Transient renewal failure | Retry with a delay while the lock and budget remain valid |
| Permanent renewal failure | Mark the lock lost and stop renewal |
| Renewal budget exhausted | Stop renewal; preserve the last successful lock deadline |
| Buffered message requested | Return only a delivery with a valid lock |
| Complete, abandon, or dead-letter | Cancel and await the message renewal before settlement |
| Last session message settles | Stop shared renewal and close the session receiver |
| Dispose, replace, purge, or fail batch mapping | Cancel and await owned renewal tasks before closing the receiver |

Each task uses a monotonic clock for its renewal budget and broker UTC timestamps for lock validity.
The SDK updates the received message or session deadline after successful renewal.
Cancellation also reaches an in-flight renewal request.

### Where the pieces live

```text
Paramore.Brighter.MessagingGateway.AzureServiceBus
    Subscription configuration -> Consumer factory -> Receiver provider
    Channel factory -> Azure channel -> Consumer -> Receiver wrapper
                                                    -> ServiceBusLock -> Azure SDK
Paramore.Brighter
    Channel / ChannelAsync <- Azure channel subclasses
```

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Responsibilities | Responsibility classifier | Collaborators |
| --- | --- | --- | --- | --- |
| Renewal budget | AzureServiceBusSubscriptionConfiguration | Set the maximum duration | knowing | Consumer factory |
| Lock lifetime | ServiceBusLock | Renew, track validity, and stop | knowing, doing | SDK renewal delegate |
| Delivery ownership | ServiceBusReceiverWrapper | Associate tokens with locks and coordinate settlement | knowing, doing | ServiceBusLock, SDK receiver |
| Dispatch eligibility | Azure channel subclasses | Skip invalid buffered deliveries | deciding | Consumer |

#### Configuration contract

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| MaxAutoLockRenewalDuration | Nonnegative TimeSpan | Five-minute default; zero disables renewal | Negative values rejected when creating the consumer |

The budget starts at batch reception and includes waiting in the buffer.
Messages in a session batch share one budget.
The budget ending does not revoke a lock already granted by the broker.
When the budget elapses while a delivery is still pending, log a warning with the message or session
identifier, entity path, configured duration, and last known lock deadline. If the next renewal would
fall outside the budget, wait cancellably until that budget elapses before warning. Normal settlement,
disposal, and disabled renewal do not produce budget warnings. The warning does not imply that the
last successfully acquired lock has already expired.
Skipping a message whose lock has expired or been lost also logs a warning with its message id,
topic, and channel so an otherwise silent delivery loss can be investigated.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| Azure gateway | Subscription configuration and factories | Carry and validate the renewal budget |
| Azure gateway | Receiver wrapper | Own native lock lifetimes and settlement cleanup |
| Azure gateway | Consumer and queue/topic subclasses | Preserve session batches and clean up replaced or purged receivers |
| Azure gateway | Azure channel subclasses | Check validity immediately before dispatch |

Public receiver-wrapper interfaces and shared channel classes remain unchanged.
Custom wrappers keep their existing session settlement behavior; receive-time idle detection only applies to the native wrapper.
Applications constructing shared channels directly around a consumer bypass the Azure-specific dispatch check.
They still receive renewal when using the native consumer factory.

### Technology Choices

#### Why renew every received message?

A buffered message already holds a broker lock.
Waiting until dispatch to renew it cannot recover a lock that has expired.
Renewal therefore begins at receipt, and its configured budget includes buffer time.

#### Why keep validity in the receiver?

The receiver owns the live deadline and renewal outcome.
Copying an initial timestamp into a serializable message header would become stale after renewal.
The channel instead consults the receiver using the existing lock token.

#### Why use Azure channel subclasses?

Shared channels cannot interpret transport-specific lock metadata.
Internal subclasses keep that decision within the Azure gateway and avoid changing other transports.
The existing channel factory remains the public entry point.

### Implementation Approach

1. Reproduce failed completion for slow handling and buffered batches through the public channel factory.
2. Add the configuration budget and internal renewal lifetime.
3. Connect the native wrapper's receipt and settlement operations to that lifetime.
4. Check buffered delivery validity and preserve session receivers until their batches finish.
5. Exercise expiry, transient failure, cancellation, replacement, purge, and both disposal paths.

## Consequences

### Positive

- Slow successful handlers can acknowledge messages beyond the initial lock period.
- Buffered messages retain locks while waiting, within the configured budget.
- Session batches remain usable after the first message completes.
- Custom public wrapper implementations keep their existing interface contracts.

### Negative

- Automatic renewal adds broker calls and one background task per pending message, or per session.
- Native received messages remain referenced until settlement or cleanup.
- A long batch can consume much of its renewal budget before its last handler begins.

### Risks and Mitigations

- Network failures, clock skew, or process suspension can still cause lock loss; delivery remains at least once.
- Lock loss after dispatch cannot undo handler side effects; handlers still need their normal idempotency policy.
- In-memory SDK substitutes verify lifecycle behavior but cannot validate broker protocol behavior; broker integration tests remain necessary.

## Alternatives Considered

- Increase LockDuration: cannot support handlers beyond the broker's maximum lock duration.
- Renew only during handling: leaves buffered deliveries vulnerable to expiry.
- Use the SDK processor: would replace Brighter's pull-based channel and pump lifecycle.
- Change public wrapper interfaces: would break custom implementations in a V10 maintenance change.

## References

- [Issue #4487](https://github.com/BrighterCommand/Brighter/issues/4487)
- [SDK message lock renewal](https://learn.microsoft.com/dotnet/api/azure.messaging.servicebus.servicebusreceiver.renewmessagelockasync)
- [SDK session lock renewal](https://learn.microsoft.com/dotnet/api/azure.messaging.servicebus.servicebussessionreceiver.renewsessionlockasync)
