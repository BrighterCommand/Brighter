# Azure Service Bus retries and deployment

Direct topic subscriptions retry by abandoning their current delivery. If a delay
was requested, Brighter warns once per consumer and retries immediately. This keeps the
retry within the failing subscription and preserves startup compatibility.

To retain delayed retries, give each independent subscriber its own forwarding
queue. Publishers continue sending to the topic. Consumers receive and schedule
retries on their dedicated queue in the same namespace.

## Configure the consumer

Set these options on the existing `AzureServiceBusSubscription`:

```csharp
var configuration = new AzureServiceBusSubscriptionConfiguration
{
    ForwardTo = "orders-accounting"
};
```

Keep the routing key set to the topic and the channel name set to its subscription.
Do not combine `ForwardTo` with `UseServiceBusQueue`. Applications that already
manage forwarding externally can instead use `UseServiceBusQueue = true`, with
the destination queue as their routing key.

By default, retries use native Azure scheduling on the consuming broker. No global
scheduler registration, scheduler queue, or scheduler pump is needed.

To explicitly use the registered global scheduler, pass a publication to the consumer
factory and select `Global`:

```csharp
var retryPublication = new AzureServiceBusPublication
{
    RequeueScheduler = RequeueSchedulerMode.Global // Native is the default
};
var consumerFactory = new AzureServiceBusConsumerFactory(clientProvider, retryPublication);
var channelFactory = new AzureServiceBusChannelFactory(consumerFactory)
{
    Scheduler = schedulerFactory.Create(commandProcessor)
};
// Assign channelFactory to the subscription's ChannelFactory.
```

The Service Activator can supply the registered scheduler through the channel factory.
The Azure scheduler supports queue retries. A global scheduler must implement
`IAmAMessageRequeueSchedulerSync` for Reactor or `IAmAMessageRequeueSchedulerAsync` for
Proactor; ordinary request/message scheduling alone is insufficient. Missing capabilities
fail during channel creation. Global mode requires `ForwardTo` or `UseServiceBusQueue`.
It uses the global scheduler's connection: if that targets another namespace, arrange
consumption there explicitly. Native mode always uses the consuming broker's connection.
`ForwardTo` must be a queue name, not a URI, subscription path, or reserved subqueue.
The queue name `subscriptions` is valid; a path such as `orders/Subscriptions/accounting`
identifies a subscription and is rejected.

The selection applies to every subscription using that consumer factory. Subscriptions
that need different modes require separate `AzureServiceBusChannelFactory` instances,
assigned through each `Subscription.ChannelFactory`. Other transports retain their
existing retry behavior; this selection currently applies only to Azure Service Bus.

For manual composition, `AzureServiceBusQueueConsumer` now takes an explicit retry
scheduler in place of its producer; `AzureServiceBusTopicConsumer` no longer takes a
producer. A manually constructed queue consumer rejects a scheduler with neither requeue
capability. Use the factory above to validate the capability required by the selected pump.

| Channel policy | Provisioning behavior | Runtime requirements |
|---|---|---|
| `Create` | Creates a missing queue and forwarding subscription; rejects an existing forwarding mismatch | Administration access plus Send and Listen |
| `Validate` | Checks that both entities exist and the subscription forwards to the expected queue | Administration access plus Send and Listen |
| `Assume` | Makes no administration requests; topology must already exist | Send and Listen on the destination queue |

Use Standard or Premium for forwarding. The source subscription must not require
sessions; the destination queue can require them. Producers must then supply
session IDs. Creating forwarding requires Manage permissions on both entities.
See [Azure autoforwarding requirements](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-auto-forwarding).

## Delivery identity and recovery

Each queue retry gets a new delivery ID. `x-original-message-id` retains the first
delivery's ID, while the message's logical topic and body remain unchanged. The
CloudEvents ID and source remain stable across attempts (`cloudEvents:id` and
`cloudEvents:source` in this gateway's application properties).
Repeated scheduling of the same delivery uses the same retry ID. This prevents
the broker from suppressing a legitimate next attempt, while allowing it to
suppress repeated sends within its duplicate-detection window.

Scheduling and acknowledging the original are separate operations. A crash or
network failure between them can leave both deliveries available. Handlers must
be idempotent, using an application business key or the retained original identity
where appropriate. A new delivery ID is not a new business operation. Broker
deduplication is bounded by its configured window and does not provide exactly-once
processing. See [Azure duplicate detection](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection).

Direct abandon increases Azure's delivery count. Set `MaxDeliveryCount` above
Brighter's `RequeueCount`, allowing for lock loss and other broker redeliveries.
Queue scheduling preserves session IDs, but a delayed retry can arrive after
later messages in that session; do not rely on retries preserving business order.

Retries use the destination queue's default TTL as new broker deliveries. They do not
inherit the original delivery's remaining TTL or `AzureServiceBusPublication.TimeToLive`.
Carry and check an application deadline when expiry must span all attempts.

Network retries use the client's `ServiceBusClientOptions.RetryOptions`. Sender construction
errors fail immediately. Scheduling failures leave the
original unacknowledged; a later abandon or lock expiry increases Azure's delivery
count. Account for these failures when setting `MaxDeliveryCount`, even for queue retries.
Broker failures are reported as `ChannelFailureException`, while cancellation propagates
as cancellation. Retry publishing adds producer telemetry to the current activity and
propagates its trace context without changing the original delivery. The event records the
new broker delivery ID and the stable CloudEvents ID separately. Without a current activity,
the retry retains the original trace context.

Native retry telemetry uses the consumer factory's `instrumentationOptions` constructor
argument. Global Azure retry telemetry uses `AzureServiceBusSchedulerFactory.InstrumentationOptions`.
Both default to `InstrumentationOptions.All`, matching the producer. Choose `None` to
omit event tags, or omit `Messaging` and `RequestBody` to exclude headers and body.
Trace propagation remains enabled regardless of telemetry verbosity.
Local-only headers are excluded from retry publishing and telemetry. If diagnostic
header serialization fails (for example, for an AMQP `NaN` value), Brighter logs a
warning and continues scheduling with trace context. Updating an existing baggage key is
allowed at the 32-entry limit. Excess or unsupported ambient baggage is logged and cannot
block retry delivery or propagation of the remaining trace headers. The original delivery's
baggage is unchanged. Transport errors still fail the retry.

## Roll out forwarding

1. Record the current subscription rules, session settings, retry budget, entity
   TTLs, and active, scheduled, and dead-letter message counts.
2. Stop the affected consumer and pause publishing during the topology cutover,
   or use a separately reviewed migration procedure that accounts for concurrent
   publishing. Account for pending messages before changing forwarding.
3. Provision the dedicated queue with the intended session, TTL, duplicate
   detection, capacity, and delivery-limit settings. Apply forwarding externally
   to an existing subscription, retaining its filters. Brighter rejects an
   existing forwarding mismatch rather than changing it.
4. Run one consumer with `Assume` and its restricted runtime credentials. Verify
   successful receive, delayed retry, and acknowledgement without Manage rights.
5. Publish a uniquely identified canary. Make only this subscriber defer it.
   Verify that it receives the retry after the delay and that sibling subscribers
   receive only their original copy. Check retained identity and handled count.
6. Resume normal traffic and monitor scheduled-message counts, processing latency,
   lock-loss errors, retry warnings, queue depth, and dead-letter growth. Inspect
   both the forwarding source's dead letters and the destination's dead letters.

Forwarding failures can place messages in the source dead-letter queue. Restoring
the destination does not automatically replay those messages; recovery must
explicitly account for them. See [Azure forwarding failure behavior](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-auto-forwarding).

## Roll back without discarding pending work

Stop the consumer and pause publishing before changing topology again. Inventory
active, scheduled, and dead-letter messages on both entities. Drain or transfer
them using an application-approved process that preserves business identity.
Do not purge or delete the destination queue while it contains pending retries.

If retaining forwarding during an application rollback, configure the previous
application to read its destination with `UseServiceBusQueue`. Verify that the
previous version's retry behavior is acceptable, especially with duplicate
detection enabled. Returning an old application to direct topic republication
can restore the original broadcast defect.

## Release acceptance on Azure

Use a dedicated test namespace. The integration suite creates and deletes entities.
Load its connection string securely into `BrighterTestsASBConnectionString`; do
not commit it or include it in logs. Alternatively, the existing
`BrighterTestsASBNameSpace` path uses Visual Studio credentials, not Azure CLI
credentials.

Run the full gateway suite on each supported test runtime, for example:

```sh
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj \
  -c Release -f net10.0 --logger "trx;LogFileName=asb-release-net10.trx"
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj \
  -c Release -f net9.0 --logger "trx;LogFileName=asb-release-net9.trx"
```

The emulator used for local validation caps entity TTL at one hour. Existing tests
that exercise multi-day TTLs fail during provisioning there, including on the
unchanged base revision. Run those cases on Azure; retain the baseline comparison
alongside local regression results.
Repeated runs can also exhaust the emulator's entity quota; use a fresh disposable
instance for clean full-suite comparisons.

The suite validates functional behavior using provisioning credentials. It does
not establish least-privilege access, process recovery, or production capacity.
Complete these separate checks in the deployment's representative environment:

| Check | Acceptance evidence |
|---|---|
| Restricted identity | `Assume` receives, schedules, and settles with Send/Listen only; no administration rights are granted |
| Interrupted scheduling | After connectivity is restored, every business message is accounted for as completed, pending, or dead-lettered |
| Process restart | Terminate a consumer after scheduling but before acknowledgement; restart and verify no lost business operation or duplicate side effect |
| Load | Record payload sizes, session distribution, concurrency, retry rate, duration, throughput, latency, throttling, and resource use against the application's agreed limits |
| Migration and rollback | Rehearse the procedures above with pending deliveries and scheduled retries; reconcile all message identities |

Keep the Azure results with the release record. Emulator results remain useful
regression evidence, but do not close these Azure acceptance checks.

See [ADR 0082](../adr/0082-isolate-azure-service-bus-subscription-retries.md) and the
[regression record](../../bugfixes/0052-asb-subscription-retry-isolation/bugfix.md).
