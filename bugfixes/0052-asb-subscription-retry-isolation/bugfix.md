# ASB subscription retry isolation

**Linked issues:** [#4546](https://github.com/BrighterCommand/Brighter/issues/4546) and
[#4490](https://github.com/BrighterCommand/Brighter/issues/4490).

## Cause and resulting behavior

Republishing a failed delivery to its topic broadcasts another copy to every matching subscription.
Reusing its MessageId on a queue with duplicate detection can instead silently discard the retry,
while the original is acknowledged and lost.

Direct topic subscriptions now abandon the original delivery with the updated `HandledCount`.
If a delay is requested, they warn and abandon immediately; the configuration remains valid at startup.
They never republish to the topic. This applies to both configured and handler-specific delays.

Forwarding and queue consumers use native queue scheduling by default. A retry gets a distinct,
deterministic delivery ID, while `x-original-message-id` retains the first ID across retries.
Repeated scheduling of the same delivery uses the same retry ID. This allows the broker to suppress
ambiguous duplicate sends within its detection window without suppressing the next retry attempt.
The original is acknowledged only after scheduling succeeds. Scheduling failure or cancellation
leaves it unacknowledged; settlement failures propagate.

`Publication.RequeueScheduler` explicitly selects native or global scheduling. Native scheduling is
the default and is bound to the consuming broker, regardless of a registered global scheduler.
Explicit global selection uses the registered scheduler's backend, which may be another namespace.
No separate scheduler package, registration, scheduler queue or scheduler pump is needed for native
ASB queue retries.

Native abandon increases the broker's delivery count. Configure `MaxDeliveryCount` above Brighter's
`RequeueCount`, allowing additional broker redeliveries as appropriate. A direct subscription cannot
retain a requested delay; use a dedicated queue when retry timing matters.

## Configuration and migration

The [deployment guide](../../docs/guides/azure-service-bus-retries.md) covers runtime
permissions, rollout, rollback, recovery, monitoring, and live Azure release acceptance.

Set `AzureServiceBusSubscriptionConfiguration.ForwardTo` to a queue dedicated to the subscription:

```csharp
var configuration = new AzureServiceBusSubscriptionConfiguration
{
    ForwardTo = "orders-accounting"
};
```

Keep the subscription's routing key set to the topic and its channel name set to the topic subscription.
Publishers continue sending to the topic. Brighter receives from the forwarding queue and schedules
retries there while retaining the logical topic. Each independent subscriber needs its own queue.

- `Create` creates the queue before the forwarding subscription and applies the configured rule.
- `Validate` requires both entities to exist and verifies the forwarding destination.
- `Assume` performs no administration requests. Provision forwarding externally first; the consumer
  needs Listen and Send permissions on the destination queue, but does not need Manage rights.

Existing subscriptions must already forward to the configured destination. `Create` and `Validate`
reject a mismatch instead of changing an existing subscription. Stop the consumer and account for
pending deliveries when migrating broker entities. Brighter does not perform automatic migration.

`ForwardTo` cannot be combined with `UseServiceBusQueue`. Applications that provision forwarding
externally can continue consuming the destination queue with `UseServiceBusQueue = true` and the queue
name as their routing key. Both configurations receive the native retry identity fix.

Session settings apply to the destination queue; the forwarding subscription must not require sessions.
Native session IDs are retained on the retry. Purge is supported only with `OnMissingChannel.Create`;
`Validate` and `Assume` reject purge before closing the receiver or deleting the destination.

## Design

[ADR 0082](../../docs/adr/0082-isolate-azure-service-bus-subscription-retries.md) records the design.
The forwarding consumer owns queue and subscription provisioning. The consumer factory injects the
selected retry scheduler explicitly; producers do not implement the retry-scheduler capability.
Native retries use the gateway's `AzureServiceBusRequeueScheduler`.
The Azure scheduler package delegates its requeue capability to that same native implementation;
ordinary scheduled commands and requests keep their envelope behavior.

Global mode requires a physical queue and a scheduler implementing the retry capability for the
selected consumer mode. Reactor calls the synchronous capability; Proactor calls the asynchronous
capability. Missing capabilities fail before provisioning. Custom receivers for direct subscriptions must implement
`IAmAServiceBusRetryReceiver` to support property-preserving abandon.

Scheduling uses the physical queue separately from the logical topic and leaves the received message
unchanged. Body bytes, correlation, session, handled count and other metadata are preserved.
The retry's CloudEvents ID retains the original event identity while its native delivery ID changes.
Local-only headers are omitted from publishing and telemetry. Diagnostic serialization and context
propagation errors cannot block scheduling. Existing baggage keys can be updated at capacity;
overflow or unsupported ambient baggage is logged while other trace headers continue propagating.
Send and settlement are not transactional:
duplicates remain possible with duplicate detection disabled or outside its detection window.
Handlers must remain idempotent. Each retry sender is closed; the client remains owned by its provider.

## Regression coverage

- Direct-subscription startup works in Create, Validate and Assume modes with configured delays.
- Requested delays on direct subscriptions warn and abandon without sending to sibling subscribers.
- Immediate direct retries preserve handled counts across successive redeliveries.
- Queue retries survive duplicate detection with no scheduler, an Azure scheduler or a general-purpose
  scheduler, through forwarding and direct queue configurations, with zero and positive delays.
- Sync and async consumers and session-enabled queues preserve delivery metadata and isolation.
- Native scheduling stays on the consuming broker; explicit global scheduling uses its configured backend.
- Producer telemetry preserves trace context without changing the original, including when headers
  contain local-only values or broker-supported values that cannot be serialized as diagnostic JSON.
- Full baggage supports updates without exceeding 32 entries. Both consumer modes continue retrying
  with trace parent/state when ambient baggage overflows or contains an unsupported key, including
  when diagnostic JSON serialization also fails. The original baggage remains unchanged.
- A queue with a `subscriptions` path segment works in queue and forwarding modes; subscription and
  subqueue paths fail early. Fixtures use unique queue names so parallel runtime runs cannot collide.
- Transient sender creation failures are retried before acknowledgement; exhausted failures remain visible.
- Scheduling failures and cancellation leave the original unacknowledged; repeated scheduling after
  acknowledgement failure keeps the same retry ID and does not mutate the original message.
- Existing forwarding provisioning, mismatch validation and purge protection remain covered.
- Generated gateway conformance tests use the configured ASB assertion to verify distinct retry
  delivery IDs and retained original identity, alongside the existing metadata and body checks.

Before hardening, all nine startup/fallback and broker-ownership cases failed. The default-path
broker regression reproduced silent retry loss with duplicate detection enabled. Earlier deliberate
mutations also demonstrated that the failure tests catch premature acknowledgement and unstable IDs.

## Validation

The review regression suite covers scheduler selection, consumer mode, producer telemetry, event
identity, retry acquisition, failure settlement, and destination validation. The added queue-name and
telemetry cases first failed against the local pre-fix implementation, then passed against the
emulator after the fixes. In particular, broker round trips verify both local-only header exclusion
and delivery of a `NaN` application property despite diagnostic JSON serialization rejecting it.
Twelve further broker cases reproduced baggage capacity and propagation failures before the fix,
then passed with both consumer modes. A core regression verifies that replacing a key in full baggage
succeeds while adding a 33rd key remains rejected.

Core tests report 1,704 passed and 7 skipped; generator tests report 312 passed on .NET 10.
The core, gateway and Azure scheduler build for netstandard2.0, net8.0, net9.0 and net10.0
with zero warnings and errors.

All **57 review regression cases pass on .NET 9 and .NET 10**. The full Azure Service Bus
suite on .NET 10 reports **592 passed, 10 failed and 4 skipped** out of 606. The ten failed
test names exactly match the results reproduced on unchanged PR head
`8c561701f952c049b6910908a2dbad453d7808b2`; all fail on the emulator's one-hour entity TTL
restriction. There are no additional failures. `git diff --check` is clean.
The queue-name cases also pass with both runtimes running together. Earlier repeat runs
exhausted the emulator's entity quota; the final results above use a recreated disposable
environment with the same cached images.

## Release validation still required

No dedicated Azure test namespace was supplied. The Azure CLI has cached account metadata,
but a read-only namespace lookup failed with AADSTS9002313 and required interactive login.
No live test entities were created. Real Azure validation of least-privilege permissions,
recovery, sustained load and migration remains unverified. The emulator rejects the existing suite's
three- and four-day TTL fixtures because it permits at most one hour; ten such failures have been
established on the unchanged baseline. The deployment guide provides the release acceptance
procedure; these checks remain open until their results are recorded. The public documentation
site needs the same guide when publishing the feature.
