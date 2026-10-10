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

Native scheduling is bound to the consuming broker and takes precedence over a registered scheduler.
A global scheduler pointing to another namespace cannot redirect the retry. No separate scheduler
package, registration, scheduler queue or scheduler pump is needed for native ASB queue retries.

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
The forwarding consumer owns queue and subscription provisioning. The queue producer implements the
optional core requeue-scheduler capability using the gateway's `AzureServiceBusRequeueScheduler`.
The Azure scheduler package delegates its requeue capability to that same native implementation;
ordinary scheduled commands and requests keep their envelope behavior.

Custom producers without native requeue support can use an explicitly configured requeue-capable
scheduler. Custom components are responsible for compatible broker configuration. The existing
producer fallback is retained for custom implementations; the built-in ASB queue producer always
uses the native capability. Custom receivers for direct subscriptions must implement
`IAmAServiceBusRetryReceiver` to support property-preserving abandon.

Scheduling uses the physical queue separately from the logical topic and leaves the received message
unchanged. Body bytes, correlation, session, handled count and other metadata are preserved.
The retry's CloudEvents ID matches its native delivery ID. Send and settlement are not transactional:
duplicates remain possible with duplicate detection disabled or outside its detection window.
Handlers must remain idempotent. Each retry sender is closed; the client remains owned by its provider.

## Regression coverage

- Direct-subscription startup works in Create, Validate and Assume modes with configured delays.
- Requested delays on direct subscriptions warn and abandon without sending to sibling subscribers.
- Immediate direct retries preserve handled counts across successive redeliveries.
- Queue retries survive duplicate detection with no scheduler, an Azure scheduler or a general-purpose
  scheduler, through forwarding and direct queue configurations, with zero and positive delays.
- Sync and async consumers and session-enabled queues preserve delivery metadata and isolation.
- A scheduler backed by another client cannot redirect retries away from the consuming broker.
- Scheduling failures and cancellation leave the original unacknowledged; repeated scheduling after
  acknowledgement failure keeps the same retry ID and does not mutate the original message.
- Existing forwarding provisioning, mismatch validation and purge protection remain covered.
- Generated gateway conformance tests use the configured ASB assertion to verify distinct retry
  delivery IDs and retained original identity, alongside the existing metadata and body checks.

Before hardening, all nine startup/fallback and broker-ownership cases failed. The default-path
broker regression reproduced silent retry loss with duplicate detection enabled. Earlier deliberate
mutations also demonstrated that the failure tests catch premature acknowledgement and unstable IDs.

## Validation

The 26 hardening cases pass on both .NET 9 and .NET 10. The 13 existing Azure scheduler tests
and all 312 generator tests pass on .NET 10. The shared gateway/scheduler implementation builds
for netstandard2.0 and net8.0 with zero warnings and errors.

The final full ASB suite on .NET 10 reports **553 passed, 10 failed and 4 skipped** out of 567.
The ten failed test names exactly match the established baseline and all fail on the emulator
TTL restriction below. There are no additional failures. All six regenerated retry identity
conformance cases pass. `git diff --check` is clean.

Release packages for core, the ASB gateway, and the Azure scheduler build for
netstandard2.0, net8.0, net9.0, and net10.0. A separate .NET 8 consumer compiles
against the three local NuGet packages with zero warnings and errors. Package hashes
confirm that it restored these exact artifacts, rather than previously cached versions.

## Release validation still required

No dedicated Azure test namespace was supplied. The Azure CLI has cached account metadata,
but a read-only namespace lookup failed with AADSTS9002313 and required interactive login.
No live test entities were created. Real Azure validation of least-privilege permissions,
recovery, sustained load and migration remains unverified. The emulator rejects the existing suite's
three- and four-day TTL fixtures because it permits at most one hour; ten such failures have been
established on the unchanged baseline. The deployment guide provides the release acceptance
procedure; these checks remain open until their results are recorded. The public documentation
site needs the same guide when publishing the feature.
