# ASB subscription retry isolation

**Linked issue:** #4546

## Cause and scope

The topic consumer previously republished retries to the original topic. Every matching subscription receives
another copy, including subscriptions that already processed the event successfully.

Immediate retries on direct subscriptions now abandon the original delivery and persist the updated
`HandledCount`. The broker's delivery count also increases, so `MaxDeliveryCount` can dead-letter a
message before Brighter exhausts `RequeueCount`.

Delayed retries are isolated when the subscription uses a dedicated forwarding queue. This is an
explicit topology choice. A positive `RequeueDelay` on a direct topic subscription now fails consumer
creation with a `ConfigurationException` explaining how to configure forwarding. A positive delay
passed directly to `Requeue`/`RequeueAsync` is rejected before publishing or settling the original.
This deliberately breaks configurations that previously broadcast retries: migrate them to a dedicated
queue to retain delayed retry behavior. Immediate retries remain supported on direct subscriptions.

Handler-specific delays also require forwarding. If a handler requests one on a direct subscription,
the pump treats the exception as a failed requeue and nacks the original; it cannot honor that delay.
Brighter does not automatically migrate existing broker entities.

## Configuration

Set `AzureServiceBusSubscriptionConfiguration.ForwardTo` to a queue dedicated to that subscription:

```csharp
var configuration = new AzureServiceBusSubscriptionConfiguration
{
    ForwardTo = "orders-accounting"
};
```

Keep the Brighter subscription's routing key set to the topic name and its channel name set to the
topic subscription name. Publishers continue sending to the topic. The consumer receives from the
forwarding queue and sends retries directly to that queue, preserving the logical topic and message
metadata. Each independent subscriber needs its own queue.

- `Create` creates the queue before the forwarding subscription and applies the configured rule.
- `Validate` requires both entities to exist and checks the subscription's forwarding destination.
- `Assume` performs no administration requests; provision the topology externally first.

Existing subscriptions must already forward to the configured destination. `Create` and `Validate`
reject a mismatch instead of changing an existing subscription. Plan migration with the consumer
stopped and account for pending messages before configuring forwarding externally.

`ForwardTo` cannot be combined with `UseServiceBusQueue`. The latter remains available for applications
that already provision forwarding and consume the destination queue directly.

Purge is supported only with `OnMissingChannel.Create`, where the destination can be recreated.
`Validate` and `Assume` reject purge before closing the receiver or deleting the queue, preserving
existing deliveries and externally managed infrastructure.

Session settings apply to the destination queue. The forwarding subscription must not require
sessions. Native session IDs are retained on receipt so a delayed retry remains valid for the queue.

## Design

The forwarding consumer owns queue and subscription provisioning. Its retry producer has a separate
physical destination, so sending to the queue does not require mutating or copying message headers.

`IAmAServiceBusRetryReceiver` extends the existing receiver contract with property-preserving abandon.
It leaves existing interface implementations source-compatible; custom receivers used for immediate
topic retries must implement the additional capability. Unsupported receivers fail explicitly.

## Regression coverage

- Three subscriptions receive the original; only the retrying subscription receives another copy.
- Immediate retries preserve handled counts across repeated redelivery, for sync and async consumers.
- Delayed forwarding covers sync and async consumers, all three provisioning modes, and session queues.
- Existing subscriptions with mismatched forwarding are rejected without reconfiguration.
- Delayed retry configuration is rejected for direct subscriptions in every provisioning mode.
- Per-message delays on direct subscriptions neither publish copies nor settle the original.
- Unsupported forwarding purges preserve the destination and its active message locks.
- Named mutations restore a topic retry producer and accept mismatched forwarding. The regression tests
  must fail on duplicate-delivery and missing-rejection assertions respectively.

The conformance fixture uses forwarding queues so its delayed retry scenarios exercise the supported
production topology. Its DLQ reads and cleanup target the destination queues as well as source topics.

Tests use an isolated Service Bus emulator. Existing tests with three- or four-day message lifetimes
fail during provisioning because the emulator permits at most one hour; those failures also occur on
the unchanged master revision.

## Validation

Against master `2890f8610`, all four immediate-retry cases failed on delivery to an unrelated
subscription. The twelve safety-guard cases also failed before their implementations: eight for
missing delayed-retry rejection, and four for deleting externally managed forwarding queues.
All 26 regression cases pass on .NET 9 and .NET 10.

The full .NET 10 ASB suite reports 535 passed, 10 failed, and 4 skipped. All 10 failures also occur
on master and reject the existing tests' configured message lifetimes during provisioning. The
conformance fixture uses a one-hour lifetime compatible with the emulator and disposes its shared
client between cases; its delayed retry and dead-letter scenarios pass with forwarding.

The gateway builds for netstandard2.0, net8.0, net9.0, and net10.0 with zero warnings and errors.
The routing mutation caused eight duplicate-delivery assertion failures; the validation mutation
caused two missing-rejection assertion failures. Both mutations were reverted before the final runs.
