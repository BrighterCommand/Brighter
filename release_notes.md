# Release Notes

## Master

### Dispatcher shutdown drains consumers still being created (#4541)

The Dispatcher now tracks accepted consumer operations until their channels have either been
registered with a performer task or disposed. A concurrent `End()` cannot finish before those
operations complete. Consumers shut before opening are removed and disposed without registering
a null task. A disposal failure is logged and does not prevent the remaining consumers from draining.

**Behaviour change:** `Receive()`, `Open()`, and `SetActivePerformers()` throw
`InvalidOperationException` while shutdown is in progress. Await `End()` before restarting.
After disposal, these operations throw `ObjectDisposedException`. Channel creation and disposal
run outside the lifecycle lock. See [ADR 0083](docs/adr/0083-coordinate-dispatcher-startup-and-shutdown.md).

### AWS SQS, GCP Pub/Sub and RocketMQ: `requeueCount` now runs down (#4341, spec 0037)

On these transports the broker re-serves its own stored copy of a requeued message, and Brighter never
rewrote that copy, so every redelivery presented the same `HandledCount`. A subscription's
`requeueCount` therefore never ran down. A message that kept failing was requeued for ever, never
dead-lettered by Brighter. The consumers now read the broker's own delivery counter on receive
(SQS `ApproximateReceiveCount`, Pub/Sub `delivery_attempt`, RocketMQ `DeliveryAttempt`) and present it
so that a first delivery reads `0`. The message pump is unchanged and still decides when the budget is
spent. No broker call is added. See [ADR 0077](docs/adr/0077-delivery-count-contract.md).

All three counters are approximate, so a budget of `R` rejects **on or before** delivery `R`, and the
v3 and v4 SQS packages may reject on different deliveries.

**GCP needs a `DeadLetterPolicy`.** Pub/Sub only populates `delivery_attempt` on a subscription that
has one. Without it, `requeueCount` still cannot run down, and Brighter warns (see "New Warnings"
below).

#### Behaviour change: the broker's count overrides a producer-set `HandledCount`

Where a broker counter is available (SQS, GCP with a `DeadLetterPolicy`, RocketMQ), the
`HandledCount` carried in the message header is now ignored on delivery from the source channel. A
producer that sends `HandledCount = N` sees `0` on the first delivery. The header count no longer
carries earlier budget spend into a new delivery.

The exception is a message Brighter routed to a dead-letter or invalid-message destination. A message
whose bag carries the `rejectionReason` key keeps the `HandledCount` stamped on it at rejection. A
dead-letter read therefore shows how many deliveries the message took, not the dead-letter queue's own
counter.

#### Behaviour change: a null-reason `Reject` stamps `rejectionReason = "None"`

On AWS SQS (both packages), GCP Pub/Sub and RocketMQ, a `Reject(message, null)` now stamps
`rejectionReason = "None"` on the routed copy. Before, the key was left off. `rejectionMessage` stays
absent. Any code that reads dead-lettered messages and tests for the presence of `rejectionReason`
will now find it on these copies. The other Brighter-managed transports are unchanged.

#### The rejection-metadata keys are reserved for Brighter

`rejectionReason`, `rejectionMessage`, `originalTopic`, `originalMessageType` and
`rejectionTimestamp` are reserved for Brighter's use. They are now named once in
`Paramore.Brighter.RejectionMetadataKeyNames`. **Don't set them on an ordinary message.** A message
that carries `rejectionReason` is treated as a routed copy: its stamped count is kept, and on a
transport above it can be rejected on its first delivery.

#### Replaying dead-lettered messages: strip the rejection metadata

A tool that puts a dead-lettered message back on its source (a DLQ redrive, a replay script, a manual
move) **must remove the rejection metadata** above, and may reset `HandledCount` to `0`. If it leaves
`rejectionReason` in place, the message is still treated as a routed copy. Its stamped count is at
least the budget that dead-lettered it, so the pump rejects it again on its first delivery whenever
the source's budget is no larger than that stamped count plus one. If the source's budget is larger,
the count stays where it was, and only a native broker limit bounds the message.

#### RocketMQ: header-owned properties win over same-named bag entries

When it publishes, the RocketMQ producer now skips any `Header.Bag` entry whose key it has already
written from the header: `HandledCount`, `MessageId`, `Topic`, `MessageType`, `TimeStamp`, `Source`,
`SpecVersion` and the other header-owned properties. Before, the bag was written last, so a stale
bag entry overwrote the header value (RocketMQ's `AddProperty` is last-write-wins). That is how a
dead-letter copy lost its stamped `HandledCount`. **If you forward a received message, or set one of
these keys in the bag to override a header**, the header value now wins on every publish.

#### New Warnings

Three startup validation rules (reported by `ValidatePipelines`, at Warning, once per subscription):

- **A zero budget, on any transport.** A `requeueCount` of `0`, or below `-1`, rejects the first deferral. The Warning
  asks whether you meant `-1` (requeue for ever) or `1` (reject after one delivery).
- **A budget at or above a native redrive limit.** When `requeueCount` meets or exceeds a native
  limit Brighter can see (SQS `RedrivePolicy.maxReceiveCount`, GCP
  `DeadLetterPolicy.MaxDeliveryAttempts`), the native limit fires first. The Warning names both values.
  A policy configured outside Brighter is invisible to it and isn't warned about.
- **A budget that cannot run down.** For example, a GCP subscription with no `DeadLetterPolicy`. The
  same Warning is also logged once when the channel is created:
  `Subscription '…' has requeueCount … but the delivery count cannot advance: …. The budget will not run down.`

`requeueCount: -1` (the default) disables the budget and none of these fire.

### GCP Pub/Sub: `Reject` routes to a dead-letter and an invalid-message topic (#4341, spec 0037)

GCP `Reject` used to acknowledge the message and discard it. `GcpPubSubSubscription` now takes
`deadLetterRoutingKey` and `invalidMessageRoutingKey` (new optional constructor parameters, at the
end of both constructors; callers must recompile). `Reject` then publishes a copy carrying the
rejection metadata, as SQS and RocketMQ do: `Unacceptable` goes to the invalid-message topic, falling
back to the dead-letter topic; every other reason, and a null reason, goes to the dead-letter topic.
The existing `DeadLetter` (`DeadLetterPolicy`) setting is unchanged, and native dead-lettering still
works alongside it. See [ADR 0078](docs/adr/0078-gcp-rejection-routing-and-dlq-channel-creation.md).

#### Behaviour changes

- **A subscription with no routing keys logs a Warning on every `Reject`:**
  `GcpRejectionRouter: no destination configured for rejected message {Id} with reason {Reason}; message acknowledged without publishing`.
  The message is still acknowledged.
- **A failed routing publish releases the message instead of acknowledging it.** It comes back
  promptly for another attempt, and an Error names the message id. With a native `DeadLetterPolicy`
  that loop ends at its `MaxDeliveryAttempts`. Without one, a publish that keeps failing loops for as
  long as it fails.
- **`Reject` always returns `true` and never throws.** The pull consumer's `Reject` with no receipt
  handle now returns `true` instead of `false`. A failed pull acknowledgement is logged instead of
  rethrown, and the message stays leased until its ack deadline, so the destination may receive a
  duplicate.
- **A destination topic with no subscription drops messages.** The destination producer follows the
  subscription's `MakeChannels`. Under `Create` it creates the topic but not a subscription, and Pub/Sub
  discards messages published to a topic nothing subscribes to. **Create a subscription on each
  destination topic** before relying on it.

#### DLQ-backed channels without project IAM rights: new Warning

Creating a subscription with a `DeadLetterPolicy` grants Pub/Sub's service account the forwarding
roles through two IAM helpers. When the caller lacks project-level IAM rights (`PermissionDenied`,
`Unauthenticated`, `Unimplemented` on the emulator, or no resolvable credentials), the helper is now
abandoned with a Warning instead of failing channel creation:
`{Helper} abandoned: {Rpc} on {Resource} failed with {Status}; native dead-lettering may be inactive`.
The subscription is still created. **If you see this Warning**, grant the forwarding roles yourself,
or native dead-lettering will not move messages. Any other status still fails channel creation.

### RocketMQ: `Requeue` and `Nack` now act on the broker (#4353)

`RocketMessageConsumer.Requeue` and `Nack` never called the broker. A requeued or nacked message came
back only when its receive-time invisibility lease lapsed (30 s by default), and the delay passed to
`Requeue` was ignored. Both now call `ChangeInvisibleDuration` on the message:

- `Requeue(message, delay)` hides the message for `delay`, so it is redelivered after about that long.
  A zero or missing delay redelivers it at once.
- `Nack(message)` releases the message for redelivery at once.

#### Behaviour changes

- **A requeued or nacked message comes back sooner.** Before, every requeue and nack waited for the
  subscription's invisibility lease. A handler that relied on that wait as a back-off should pass an
  explicit delay to `Requeue`, or use a `DeferMessageAction` with a delay.
- **The requeue delay is clamped to the broker's range of 0 to 12 hours.** A negative delay is treated
  as zero. A delay above 12 hours is held for 12 hours and logs a Warning:
  `Requeue delay {RequestedDelay} for message {MessageId} is above the broker's maximum invisible duration; holding it for {MaximumDelay}`.
- **If the broker call fails, the message is not lost.** The consumer logs a Warning,
  `Could not change the invisible duration of message {MessageId} to {InvisibleDuration}; it will reappear when its invisibility timeout lapses`,
  and `Requeue` still returns `true`. The message comes back when its lease lapses, as before.

### GCP Pub/Sub stream: `Nack` releases the message for redelivery (#4449)

`GcpPubSubStreamMessageConsumer.Nack`/`NackAsync` did nothing, on the assumption that not
acknowledging a message is enough for Pub/Sub to redeliver it. That holds for Pull, but not for the
streaming client, which keeps extending the lease on a message it is still holding. A message the pump
declined to acknowledge (`DontAckAction`) was therefore never redelivered, and at the default
`BufferSize: 1` it could stall the consumer for up to `MaxTotalAckExtension` (60 minutes by default).
`Nack` now releases the message, as `Requeue` already did, and it is redelivered promptly.

### GCP Pub/Sub stream: disposing a channel no longer waits for unsettled messages (#4479)

Stopping the stream consumer waited for every message it had delivered to be acknowledged or nacked.
A message that was never settled blocked `Dispose`, and with it shutdown, for up to about an hour. The
consumer now stops with `ShutdownMode.NackImmediately`. A message still held at shutdown is nacked and
redelivered, and `Dispose` returns promptly.

### GCP Pub/Sub stream: reopening a channel on the same subscription works again (#4502)

Channels on one stream subscription share a `SubscriberClient`, which stops when the last channel
leaves. A `SubscriberClient` cannot be restarted. However, Brighter kept the stopped client cached
against the subscription. So when the dispatcher reopened that subscription, after `Shut` then `Open`
or after scaling performers to zero and back, channel creation threw `InvalidOperationException: Can
only start an instance once.` A second attempt then silently gave a channel that never received
anything. A stopped client is now replaced by a new one.

A Reactor performer also disposed its stream consumer twice. With several performers on one
subscription, that could stop the shared client while the others were still reading from it, and
they then received nothing. Disposing the stream consumer is now idempotent.

**Breaking change:** `GcpStreamConsumer.Start()` is replaced by `bool TryStart()`, which returns
`false` once the consumer has stopped. Only `GcpPubSubConsumerFactory` called it in Brighter.

### GCP Pub/Sub stream: settled messages are no longer kept in memory (#4505)

For every message it delivered, the stream handler registered a callback on the `SubscriberClient`'s
cancellation token and never removed it. That token lives as long as the client, so every message the
client delivered, payload included, stayed in memory until the channel was disposed. A long-running
stream consumer's memory grew with its throughput. Stopping the client also had to run one callback for
every message ever delivered. The callback is now removed once the message is settled.

### GCP Pub/Sub: `Purge` now clears the subscription (#4508)

`Purge` and `PurgeAsync` on a GCP channel, Pull or Stream, never worked. They purge by seeking the
subscription to a future time, but the Seek request did not name the subscription, so Pub/Sub
rejected it with `InvalidArgument` and `Purge` always threw. The Seek now names the consumer's
subscription. This also affects `CommandProcessor.Call` over GCP, which purges the reply channel before
sending the request.

On a Stream subscription, a Seek clears only the messages still held by the service. The streaming
client may already have delivered some messages into Brighter's local buffer, and those were still
returned by the next `Receive`. A purge now also acknowledges every buffered message published before
the purge started. Messages published after it are kept.

### Reactor: a channel disposes its message consumer only once (#4511)

When a Reactor performer stopped, its sync `Channel` was disposed twice: once by the pump when it
received the quit message, and again when the dispatcher disposed the performer. `Channel` passed both
calls on, so every transport's sync message consumer was disposed twice on each shutdown. For a Kafka
consumer that had created a requeue or rejection producer, the second dispose threw
`ObjectDisposedException` from the already-disposed producer's `Flush`. `Channel.Dispose` is now
idempotent, as `ChannelAsync` already was, so the consumer is disposed once.

### GCP Pub/Sub: subscription and publication client configuration now adds to the connection's (#4516)

A `GcpPubSubSubscription`'s `streamingConfiguration` used to **replace** the connection's
`StreamConfiguration`, and a `GcpPublication`'s `PublisherClientConfiguration` used to replace the
connection's `PublisherConfiguration`. Any connection-wide setting, such as `EmulatorDetection`, an
endpoint or channel credentials, was dropped for that subscription or publication. On the emulator, the
client then went to production Pub/Sub and failed with `Unauthenticated`. Now the connection's
configuration runs first, then the subscription's or publication's. Where both set the same builder
property, the more specific one wins. A configuration that assigns a new `Settings` object still replaces
whatever `Settings` the connection's configuration set.

#### Behaviour change: the connection's configuration now also runs

If a connection sets `StreamConfiguration` or `PublisherConfiguration`, that action now runs for every
subscription or publication, including those with their own configuration. If you repeated connection
settings in each per-entity configuration to work around the old behaviour, you can remove the
repetition. If a connection setting must not apply to one subscription or publication, override it in
that subscription's or publication's configuration.

#### Message ordering survives a configuration that replaces `Settings`

A publisher configuration that assigned a new `PublisherClient.Settings` switched off the message
ordering an ordered `GcpPublication` asked for. Brighter sends each message with a partition key using
an ordering key, so every such send then threw `InvalidOperationException` ("Message ordering must be
enabled…"). This also broke dead-letter and invalid-message forwarding of keyed messages under a
connection `PublisherConfiguration` that replaced `Settings`. After the configurations have run, Brighter
now switches ordering back on for a publication with `EnableMessageOrdering = true`. A publication
without it keeps whatever the configuration set.

#### Stream configurations can set `Settings` values directly

A stream configuration that set a value such as `builder.Settings.AckDeadline` threw
`NullReferenceException`, because `Settings` was still null when the configuration ran. Brighter now
creates `Settings` before running the configurations, as it already did for the publisher.

### GCP Pub/Sub: keyed messages can be sent through an unordered publication (#4517)

Brighter used to send every message's partition key as the Pub/Sub ordering key. The Google client
refuses an ordering key unless message ordering is enabled, so on a `GcpPublication` without
`EnableMessageOrdering` (the default), every message with a partition key failed with
`InvalidOperationException` ("Message ordering must be enabled in settings before using OrderingKey").
This included the bulk send, and delayed sends when the scheduler fired them. A message mapper copies the
partition key from the request context, so a message could have a key without your code setting one.

Now Brighter sends the partition key as the ordering key only when the publisher client has message
ordering enabled. That is either because the publication sets `EnableMessageOrdering`, or because a
publisher configuration switched ordering on. A publisher configuration that enables ordering keeps working
as before.

#### The partition key now also travels as a `ce-partitionkey` attribute

The ordering key used to be the only place the partition key travelled. Each message with a partition key
now also carries a `ce-partitionkey` attribute, so a consumer receives the key whether or not the
publication is ordered. A consumer reads the attribute first. If there is none, it falls back to the
ordering key, so messages from producers on earlier versions still arrive with their key. The attribute is
not copied into `Header.Bag`. A consumer on an earlier version reads the key from the ordering key as
before, and sees the new attribute in its `Header.Bag`.

#### `GcpMessageProducer` takes an optional `enableMessageOrdering`

If you build a `GcpMessageProducer` yourself rather than through `GcpPubSubMessageProducerFactory`, you can
pass `enableMessageOrdering` to say whether your `PublisherClient` was built with ordering enabled. If you
leave it out, the producer uses the publication's `EnableMessageOrdering`.

### Message pumps reject received messages with no handler (#4500)

`Reactor` and `Proactor` now reject a received message as `Unacceptable` when it maps successfully
but runtime routing selects no handler. Previously, an event was acknowledged after an Information
log reporting zero pipelines. A command raised an exception, but the pump still acknowledged it.
The change also applies to registered routers that select no handler for a particular request.

The existing rejection policy determines the destination: an invalid-message channel or dead-letter
channel where supported and configured, or native dead-lettering on transports such as Azure Service
Bus. Without a rejection destination, the transport determines whether the message is discarded.
Subscriptions that deliberately ignore some types may therefore start sending them to a rejection
destination. To intentionally acknowledge and ignore a type, register a handler that does nothing.

**Unhandled events now count toward `UnacceptableMessageLimit`.** Each rejection increments the
unacceptable-message count; unhandled commands already incremented it. A positive limit can now stop
the pump after repeated unhandled events. Review the types handled by each subscription and its
`UnacceptableMessageLimit` and `UnacceptableMessageLimitWindow` settings when upgrading. The default
limit of zero remains unlimited.

Both commands and events are logged as rejected, with their message ID and channel. The rejection
metadata identifies the request type with no handler. Commands without handlers no longer enter the
general dispatch-error logging path.

Local `Publish`/`PublishAsync` calls, including calls inside handlers using the received context, can
still have zero subscribers. Application-handler exception policy and command-handler cardinality
checks retain their existing behavior.

**New public API:** `RequestContext.RequireHandlerForNextDispatch()` requires a handler for the next
immediate `Send`, `SendAsync`, `Publish`, or `PublishAsync` call using that context. CommandProcessor
consumes the requirement before building pipelines. Context copies and scheduled dispatches do not
carry it. Custom `IAmACommandProcessor` decorators must pass the pump's context instance through to
CommandProcessor; substituting or omitting it loses the requirement and retains the previous
acknowledgement behavior for messages without handlers.

### RabbitMQ: channel factories declare the queue before returning the channel (#4519)

The RabbitMQ `ChannelFactory` in both `Paramore.Brighter.MessagingGateway.RMQ.Async` and
`Paramore.Brighter.MessagingGateway.RMQ.Sync` now honours `Subscription.MakeChannels` when it creates a
channel. Previously, the queue was declared and bound only on the channel's first `Receive` or `Purge`.
A message published to the exchange between creating the channel and that first receive matched no
binding, so RabbitMQ dropped it. Because Brighter publishes with `mandatory: false`, nothing reported
the loss, and with publisher confirms on the publish was still acknowledged.

- **`Create`** declares and binds the queue, the invalid-message queue, the dead-letter queue and, where
  native delay is configured, the delayed-requeue exchange, before the factory returns.
- **`Validate`** checks that the queue exists. If it does not, the factory throws
  `ChannelFailureException`.
- **`Assume`** makes no broker connection.

A message published before any subscriber has created its channel is still not delivered: the
producer does not declare subscriber queues. That is ordinary publish/subscribe behaviour.

#### Behaviour change: provisioning failures now surface when the channel is created

The factory now connects to the broker, so connection, declaration and validation failures come out of
channel creation as `ChannelFailureException`. In a Service Activator host that is dispatcher
start-up (`Dispatcher.Receive()`), so the host fails to start. Previously the same failures surfaced on
the message pump's first receive, which logged them and retried every `ChannelFailureDelay`.

Connecting is retried when the broker is unreachable: `AmqpUriSpecification.ConnectionRetryCount`
attempts (default 3) with exponential back-off from `RetryWaitInMilliseconds` (default 1000 ms; about
14 s in total per channel), behind a circuit breaker that opens for `CircuitBreakTimeInMilliseconds`.
Declaring, binding and validating the queue are not retried.

The consumer also starts consuming when the channel is created rather than on the first receive, so
up to `BufferSize` messages may be prefetched, unacknowledged, before the pump runs. They are returned
to the queue if the channel is disposed or closed.

### Azure Service Bus queue subscription settings (#4269)

Queues created by a consumer now honor `AzureServiceBusSubscriptionConfiguration`, including sessions, delivery count, lock duration, default message lifetime and dead-lettering on expiration. Default consumer-created queues now use the subscription defaults (five deliveries, a three-day lifetime and dead-lettering on expiration) instead of the broker defaults. Existing queues and producer-created queues are unchanged.

For session-enabled queues, provision the queue before producers start, or let the configured consumer create it first. Azure Service Bus does not allow sessions to be enabled on an existing queue.

**Compatibility:** custom `IAdministrationClientWrapper` implementations must add `CreateQueueAsync(string, AzureServiceBusSubscriptionConfiguration)`. The original overload remains available. Calls passing a literal `null` as the second argument must use the `autoDeleteOnIdle` parameter name to select the original overload.

### RabbitMQ shared connection lifetime

Disposing a RabbitMQ producer or consumer now releases only its own use of the pooled connection.
Other gateways sharing that connection can continue sending and receiving. Both RabbitMQ gateways
close the connection when its last gateway releases it, including when channel cleanup throws.
After a reset, disposing an old gateway preserves a replacement held by another gateway and closes
an unused replacement. Explicit pool reset and removal still close connections immediately.

Dispose every producer and consumer to release its connection reference. An undisposed gateway can
keep the connection open for the lifetime of the process. Consumer operations after disposal now
throw `ObjectDisposedException`.

### Relational outbox configuration registration (#4279)

`AddProducers(Action<ProducersConfiguration>, ...)` now registers a relational outbox's database configuration when `IAmARelationalDatabaseConfiguration` is missing.
The fallback reuses the outbox's configuration instance. Existing explicit registrations and provider lifetimes remain unchanged.
A later ordinary registration overrides the fallback for single-service resolution; a later `TryAdd` does not.

The deferred `AddProducers(Func<IServiceProvider, ProducersConfiguration>, ...)` overload still requires explicit configuration registration when a provider needs it.
Non-relational outboxes do not register database configuration.
### Azure configuration options: rebuild and test when upgrading (#4285)

Six Azure configuration fields are now public read/write properties, so property-based tooling can discover them:

| Type | Members |
| --- | --- |
| `AzureServiceBusSubscriptionConfiguration` | `SqlFilter`, `UseServiceBusQueue` |
| `AzureServiceBusPublication` | `UseServiceBusQueue` (also inherited by `AzureServiceBusPublication<T>`) |
| `AzureBlobLockingProviderOptions` | `StorageLocationFunc` |
| `AzureBlobArchiveProviderOptions` | `StorageLocationFunc`, `TagsFunc` |

Names, types, defaults, and post-construction assignment are unchanged. Property-based configuration binding now applies the Service Bus scalar options.
Delegate-valued Blob options remain configured in code; this change does not make delegates bindable from text configuration.

**Rebuild and test applications and dependent libraries when upgrading.** Ordinary reads, assignments, and object initializers remain source-compatible after recompilation.
Already compiled code that accesses these fields is not binary-compatible with the new properties; replacing Brighter assemblies without rebuilding is not sufficient.
Code using field reflection or passing these members by reference needs source changes. Property-based serializers may now encounter delegate values they previously ignored.

### Scoped lifetime per pipeline (spec 0036, #4256)

`HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` now govern a **pipeline-scoped** DI scope: a `Scoped` handler, mapper or transform resolves from one DI scope shared by every `Scoped` participant on that pipeline, and disposed when the pipeline ends. An ASP.NET Core host can additionally opt a pipeline in to **adopting** an ambient request scope instead of creating its own, through a new `Paramore.Brighter.Extensions.AspNetCore` package (`AddBrighterRequestScope(...)`), and `ValidatePipelines()` gained seven new startup checks for common lifetime and scope-registration mistakes. See [docs/guides/lifetimes-and-scoping.md](docs/guides/lifetimes-and-scoping.md) for the full model, decision guide and troubleshooting, and [ADR 0070](docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md) through [ADR 0076](docs/adr/0076-scope-affinity-option-and-write-through.md) for the design.

This is a substantial change with fourteen breaking-change items, catalogued here in one list rather than split across each ADR that introduced one:

- **`MapperLifetime.Scoped` no longer caches for the life of the process** *(behavioural)* — it now means one instance per pipeline, disposed when the pipeline ends, like every other `Scoped` lifetime. Nothing at compile time warns of this, and **there is no compatibility flag**. If your mapper relied on the old process-wide caching, migrate to `MapperLifetime = Singleton`, which states the same intent explicitly — check first that it has no container-`Scoped` constructor dependency (see the captive-dependency item below). This is a different destination from actually wanting per-pipeline *sharing*: because the three lifetimes are validated as one joint choice once `ValidatePipelines()` is called, a per-pipeline-shared `Scoped` mapper requires `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` set together — `{Scoped, Scoped, Transient}` is **not** a valid destination.
- **`IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync`, `IAmAMessageMapperRegistry` and `IAmAMessageMapperRegistryAsync` all gain `IAmAScope? CreatePipelineScope()`, and their creation methods gain a scope parameter** *(source and binary)* — `Create(Type, IAmAScope? scope = null)` on the four factories, `Get<T>`/`GetAsync<T>` on the two registries. A hand-rolled implementation of any of these six must add `CreatePipelineScope()` (returning `null` is legitimate for a factory or registry with no container to scope) and accept the new scope parameter, even if it ignores it.
- **A `Scoped` mapper or transformer factory no longer caches at factory level** *(behavioural)* — calling `Create(type)` directly, outside a pipeline scope, now resolves a fresh instance on every call rather than returning one cached for the process.
- **The six transform-pipeline constructors gain a defaulted trailing `IAmAScope?` parameter** *(binary)* — `WrapPipeline`, `UnwrapPipeline`, `WrapPipelineAsync`, `UnwrapPipelineAsync`, and the two abstract bases `TransformPipeline`/`TransformPipelineAsync`. Source-compatible for a caller that recompiles; binary-breaking for one that does not.
- **A pipeline scope's disposal failure is no longer swallowed inside the DI package** *(behavioural)* — it is now reported at `Error` as `FailedToDisposePipelineScope` or `FailedToDisposePipelineScopeAfterFailedBuild`, then swallowed one layer up exactly as before. Only an operator's log output changes; no exception reaches a caller that did not already see one.
- **`IAmAHandlerFactory` gains `CreatePipelineScope()`, and `IAmALifetime` gains `PipelineScope`** *(source and binary)* — bringing the total to **eight** broken interfaces across the two ADRs behind this and the previous item, three of which are not mapper/transform factories at all: the two mapper registries above, and `IAmALifetime`.
- **`IAmALifetime` also gains `IAsyncDisposable`** *(source and binary)* — so the handler pipeline scope can be disposed asynchronously rather than blocking a thread on an async-only DI scope's `DisposeAsync()`. A hand-rolled implementation must add `DisposeAsync()`; a `netstandard2.0` target has no default-interface-member escape hatch for this, the same constraint the other seven broken interfaces already carry.
- **`HandlerLifetimeScope.Dispose()` is repaired to survive a throwing handler `Release`** *(behavioural)* — an exception a caller catches today from `Dispose()` now only reaches the log afterwards, rather than propagating.
- **`ServiceProviderHandlerFactory` stops keeping a DI scope of its own** *(behavioural)* — `Create` now throws `ConfigurationException` when given a lifetime whose `PipelineScope` is `null` on a non-`Singleton` handler lifetime, rather than silently falling back to a scope it owned itself.
- **The `Scoped` artefact cache stops publishing a faulted `Lazy`, on the owned path as well as the borrowed one** *(behavioural)* — a resolution failure now reaches a host that never opted in to ambient adoption, closing the `Scoped` half of issue [#4260](https://github.com/BrighterCommand/Brighter/issues/4260) (the `Singleton` cache is unchanged).
- **`PipelineBuilder<TRequest>`'s two public dispatch constructors gain a defaulted `bool isolateSubscribers`** *(binary)* — source-compatible for a recompiling caller.
- **`IBrighterOptions` gains `DefaultScopeAffinity`** *(source and binary)* — breaking a hand-rolled implementation of `IBrighterOptions`, which must add the member.
- **Both validation hosted services resolve every registered `IAmAPipelineValidator` and combine the results** *(behavioural, and source and binary)* — an application that registers its own `IAmAPipelineValidator` no longer replaces Brighter's validation wholesale; both are now run and their findings combined. The source and binary half: `BrighterValidationHostedService`'s public constructor now takes `IEnumerable<IAmAPipelineValidator>` in place of `IAmAPipelineValidator`, so any caller constructing it directly must migrate. `ServiceActivatorHostedService`'s constructor is unchanged — it resolves its validators inside `StartAsync`.
- **An application that calls `ValidatePipelines()` and mixes `Transient` with `Scoped` across `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` now fails to start** *(compatibility)* — such a configuration runs today, with the mixed pair simply not sharing pipeline-scoped dependencies; calling `ValidatePipelines()` against this version reports it as an error. The remedy is to pick one lifetime for all three (`Transient` or `Scoped`), with any of the three optionally `Singleton` instead. This break only lands for an application that opts in to validation.

**For each of `IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync`, `IAmAHandlerFactorySync`, `IAmAHandlerFactoryAsync`, `IAmAHandlerFactory` and `IAmALifetime`** — what changed, and how a hand-rolled implementation migrates:

- `IAmAMessageMapperFactory` / `IAmAMessageMapperFactoryAsync` — gain `IAmAScope? CreatePipelineScope()`; `Create` gains a trailing `IAmAScope? scope = null` parameter. Migration: add `CreatePipelineScope()` returning `null` unless the factory has a container to scope; ignore the new `scope` parameter unless the factory means to resolve from it.
- `IAmAMessageTransformerFactory` / `IAmAMessageTransformerFactoryAsync` — the same two additions, on `Create` for a transformer type. Same migration.
- `IAmAHandlerFactorySync` / `IAmAHandlerFactoryAsync` — inherit `IAmAHandlerFactory`'s new `CreatePipelineScope()` member (below); their own `Create`/`Release` signatures are unchanged. Migration: a type implementing either interface directly must add `CreatePipelineScope()`, returning `null` unless it has a container to scope.
- `IAmAHandlerFactory` — gains `IAmAScope? CreatePipelineScope()`, creating a DI scope for one handler pipeline to resolve from. Migration: implement it, returning `null` for a factory with nothing to scope.
- `IAmALifetime` — gains `IAmAScope? PipelineScope { get; }`, carrying the handler pipeline's own scope handle (distinct from this interface's existing job of tracking handler instances so they can be released). Migration: implement the property, returning `null` if the implementation has no pipeline scope of its own. **Also gains `IAsyncDisposable`**, so `CommandProcessor`'s async send/publish paths can `await using` the pipeline scope instead of blocking on a synchronous `Dispose()`. Migration: `public ValueTask DisposeAsync() => PipelineScope?.DisposeAsync() ?? default;`.

### Validate subscription channel factory compatibility (spec 0037, #4334)

Brighter now validates, at `ValidatePipelines()` time, that every subscription's declared
`ChannelFactoryType` is compatible with the channel factory it will actually be handed at startup —
turning "compiles, then dies deep in Dispatcher start" into a named, `ValidationSeverity.Error`
startup finding. Landing the rule also corrected five transports whose `ChannelFactoryType` was
wrong or missing: GCP Pub/Sub and MQTT previously declared a *consumer* factory rather than their
own, and AWS SQS, AWS SQS V4 and Postgres declared none at all — so those three transports are, for
the first time, routable through a `CombinedChannelFactory`. See
[ADR 0072](docs/adr/0072-subscription-channel-factory-compatibility.md),
[ADR 0073](docs/adr/0073-gateway-channel-factory-type-regression-guard.md) and
[spec 0037](specs/0037-validate-subscription-channel-factory/) for full details.

#### Breaking change: a subscription type with no `ChannelFactoryType` override now fails validation

Any out-of-repo `Subscription` subclass that does not override `ChannelFactoryType` inherits the base
class's default, `InMemoryChannelFactory` — previously invisible, now reported. Symptom:
`ValidatePipelines` returns an `Error` citing `Paramore.Brighter.InMemoryChannelFactory` as the
declared type. Remedy:

```csharp
public override Type ChannelFactoryType => typeof(MyChannelFactory);
```

Interim workaround: `ValidatePipelines(throwOnError: false)`.

#### Breaking change: MQTT via a plain `Subscription<T>` now fails validation

A plain `Subscription<T>` used with MQTT works today, because nothing previously checked the declared
type against the gateway it is handed. Symptom: `ValidatePipelines` returns an `Error` citing
`Paramore.Brighter.InMemoryChannelFactory` as the declared type against
`Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory`. Remedy: use `MqttSubscription<T>`.
Suppressible with `ValidatePipelines(throwOnError: false)`.

#### Breaking change: AWS SQS, AWS SQS V4 and Postgres subscriptions routed through an in-memory `CombinedChannelFactory` slot now fail at Dispatcher start, not just at validation

Because AWS SQS, AWS SQS V4 and Postgres subscriptions previously declared no `ChannelFactoryType` at
all, they inherited the base default, `InMemoryChannelFactory`. A `CombinedChannelFactory` that
included an `InMemoryChannelFactory` inner factory therefore matched these subscriptions by exact
type and silently routed them to the in-memory bus instead of failing. The subscriptions now declare
their real factory type, so that in-memory match no longer occurs: if the `CombinedChannelFactory` has
no inner factory of the subscription's real type, routing now fails with a `ConfigurationException`
(`No channel factory found for subscription {name}`) when the `Dispatcher` starts. **This is caused by
the transport corrections, not by the new validation rule, so `ValidatePipelines(throwOnError: false)`
does NOT avoid it** — it is a routing change, not a validation verdict. Remedy: add the transport's
real channel factory to the `CombinedChannelFactory`, or stop relying on the in-memory route.

#### Breaking change: a `ChannelFactoryType` override returning `null` now fails validation

An out-of-repo `ChannelFactoryType` override that returns `null` in a single-factory (non-combined)
configuration previously went unchecked. Symptom: a startup `Error` reading `declares no
ChannelFactoryType`. Remedy: return a real channel factory type from the override. Unlike the
`CombinedChannelFactory` case above, this one **is** suppressible with
`ValidatePipelines(throwOnError: false)`.

### Backstop handlers preserve explicit message-pump actions

`DeferMessageOnError`, `RejectMessageOnError`, `DontAckOnError`, and their async variants now preserve explicit `DeferMessageAction`, `RejectMessageAction`, `DontAckAction`, and `InvalidMessageAction` exceptions instead of replacing them with the backstop's configured action. A non-empty `AggregateException` whose direct inner exceptions are all pump actions is also preserved for the pump to handle. Mixed, empty, or nested aggregates still use the configured fallback. Application failures, including `OperationCanceledException` and `TaskCanceledException` from dependency timeouts, continue to use that fallback.

If a pipeline has multiple backstops, the innermost backstop now determines the action for an application failure; outer backstops preserve it. For example, `[RejectMessageOnError(step: 0)]` wrapping `[DeferMessageOnError(step: 1)]` now defers the message instead of rejecting it. Lower step numbers are outer wrappers. Review pipelines with stacked backstops if they relied on the previous outermost-backstop behavior.
### DynamoDB outbox: configured operation timeouts now take effect (#4434)

Both `Paramore.Brighter.Outbox.DynamoDB` (AWS SDK v3) and `Paramore.Brighter.Outbox.DynamoDB.V4` now honour `DynamoDbConfiguration.Timeout` and per-call outbox timeouts. Previously, these values were ignored.

**Upgrade impact: the existing default of 500 ms now takes effect.** Operations that previously completed after that deadline may now throw `OperationCanceledException`. Review your timeout settings before upgrading, especially for batches and queries: one deadline covers the entire operation, including all batch items or query pages.

To allow a longer deadline, set the configuration's `timeout` constructor argument (in milliseconds), for example:

```csharp
new DynamoDbConfiguration(timeout: 5000);
```

To disable the outbox deadline, use `new DynamoDbConfiguration(timeout: 0)`; a configured value of `-1` also disables it. Caller cancellation and the AWS SDK client's own timeouts still apply.

For methods accepting a per-call timeout:

* `-1` (the default) uses `DynamoDbConfiguration.Timeout`; it does **not** independently disable the deadline.
* `0` disables the outbox deadline for that call.
* A positive value overrides the configured deadline, in milliseconds.

Cancellation is cooperative. Synchronous AWS SDK table-metadata discovery remains governed by the SDK client's timeout. Adding a message to a transaction queues the write; the later commit must be cancelled through the transaction provider. Public signatures and default parameter values are unchanged, but applications that relied on the previously ignored deadlines may need configuration changes.

### AWS: configurable `MaximumMessageSize` for SNS topics and SQS queues

Amazon SNS now accepts message payloads up to 1 MiB, but only if you raise the topic's `MaximumMessageSize` attribute — the default is still 256 KiB. Brighter's send path never assumed a fixed limit, but the provisioning path had no way to set the attribute, so a topic Brighter created with `OnMissingChannel.Create` was stuck at 256 KiB.

SQS is the other way round. A queue created today already defaults to the full 1 MiB, so `SqsAttributes.MaximumMessageSize` is there for when you want a queue to *reject* anything over a size you pick, not to unlock headroom you'd otherwise be missing.

`SnsAttributes` and `SqsAttributes` (both the V3 and V4 packages) gain an optional `maximumMessageSize` constructor parameter, in bytes:

```csharp
new SnsPublication
{
    Topic = new RoutingKey("my-topic"),
    MakeChannels = OnMissingChannel.Create,
    TopicAttributes = new SnsAttributes(maximumMessageSize: 1_048_576)
};
```

For SNS the value is applied with a `SetTopicAttributes` call after `CreateTopic`, because SNS rejects `CreateTopic` when a supplied attribute differs from the existing topic's. That means the setting also raises the limit of a topic Brighter created earlier at the default size. For SQS the attribute joins the `CreateQueue` request, which already re-applies attributes to an existing queue.

#### Before you raise a topic

AWS puts some sharp edges around a topic above 256 KiB, and they are worth reading before you turn this on:

* It only supports Amazon SQS, AWS Lambda and Amazon Data Firehose subscriptions. HTTP/S, email, SMS and mobile push are not, so raising the limit on a topic that has those subscribers will cost you them.
* It is capped at 100 subscriptions, rather than the usual 12.5 million.
* SNS measures the message body and its message attributes together, so your headers count against the limit.
* The subscribed queue does **not** need raising to match. A message delivered through an SNS subscription is bounded by the topic's limit, not by the queue's `MaximumMessageSize` — a 500 KiB message on a 1 MiB topic arrives intact on a queue still sitting at 256 KiB. A direct `SqsMessageProducer` send is bounded by the queue's limit as normal.
* The `[Compress]` and `[ClaimCheck]` thresholds compare the uncompressed body only. A compressed body goes out base64-encoded, which inflates it by roughly a third, so leave yourself headroom under the topic's maximum.

### Replay Outbox Messages on Inbox Duplicate (spec 0027)

When an inbox detects a duplicate request, Brighter can now optionally **replay** the outbox messages that were produced under that request's causation, rather than silently dropping the duplicate. The feature is opt-in (`OnceOnlyAction.Replay` on the inbox attribute) and non-breaking: it requires a causation-tracking inbox and outbox (`IAmACausationTrackingInbox` / `IAmACausationTrackingOutbox`), and the relational stores gate the causation-aware write on a memoized column probe so un-migrated schemas keep depositing unchanged. Startup pipeline validation fails fast if a `Replay` pipeline is configured without causation tracking. See [ADR 0057](docs/adr/0057-replay-outbox-on-inbox-duplicate.md) and [spec 0027](specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/) for full details.

#### Usage requirement: thread your `RequestContext` through `Post` / `DepositPost`

Replay links a duplicate back to its original outbox messages through the **causation id**. `[UseInbox]` stamps that id into the pipeline's `RequestContext.Bag`, and the outbox `Add` reads it back from the bag — **but only if the handler that deposits the outbox message passes its own `Context` down**. If a handler calls the idiomatic `_commandProcessor.Post(evt)` (or `DepositPost`) *without* a request context, `CommandProcessor` creates a fresh context, the causation id is lost, the message is stored with a `null` `CausationId`, and a later `Replay` finds nothing to replay — a **silent no-op** with no error.

To use Replay, every handler that produces outbox messages must forward its handler `Context`:

```csharp
// ❌ Silent no-op under Replay — fresh context, causation id lost
public override MyCommand Handle(MyCommand command)
{
    _commandProcessor.Post(new DownstreamEvent(...));
    return base.Handle(command);
}

// ✅ Threads the causation id so Replay can match
public override MyCommand Handle(MyCommand command)
{
    _commandProcessor.Post(new DownstreamEvent(...), Context);
    return base.Handle(command);
}
```

This applies to the async equivalents (`PostAsync` / `DepositPostAsync`) as well.

#### Source-breaking change: `IRequestContext.InstrumentationOptions`

`IRequestContext` gains a new required `InstrumentationOptions InstrumentationOptions { get; set; }` member. It carries the instrumentation options configured for the pipeline that created the context, so middleware handlers can gate their own telemetry (for example on `InstrumentationOptions.Brighter`) without taking a dependency on how the processor was configured. `Paramore.Brighter` targets `netstandard2.0`, which does not support default interface members, so — as with the spec-0027/0029 box-provisioning interface additions — this is exposed as a plain abstract member.

The change is **source-breaking** for any third-party or test type that implements `IRequestContext`: such types will fail to compile until they add the new member. It affects more consumers than just those adopting Replay, hence its call-out here. The shipped `RequestContext` already implements it and defaults to `InstrumentationOptions.All`, so call sites that use the shipped context require no change.

```csharp
// Custom IRequestContext implementations must add:
public InstrumentationOptions InstrumentationOptions { get; set; } = InstrumentationOptions.All;
```

### Kafka: classify or suppress the log level of consumer error-callback events (#4264)

`librdkafka` reports transport conditions — a broker briefly unreachable, a coordinator failover, a metadata
refresh — through the consumer's error callback. Brighter logged every one of them at a fixed level: fatal at
`Error`, everything else at `Warning`. A busy or lossy network therefore produces a stream of `Warning`
entries for conditions the client recovers from unaided, and the only way to quieten them was to turn the level
down for the whole gateway — losing the warnings that did matter.

`KafkaSubscription` now takes an optional `ErrorLogLevel` hook — `Func<Error, LogLevel>?`, settable through the
constructor or as a property:

```csharp
var subscription = new KafkaSubscription<MyEvent>(
    new SubscriptionName("my-subscription"),
    channelName: new ChannelName("my-channel"),
    routingKey: new RoutingKey("my-topic"),
    groupId: "my-group",
    errorLogLevel: error => error.Code switch
    {
        // Recovered from without intervention; do not log it at all
        ErrorCode.Local_AllBrokersDown => LogLevel.None,
        // Transport churn is expected here, so keep it out of the warning stream
        ErrorCode.Local_Transport => LogLevel.Debug,
        _ => error.IsFatal ? LogLevel.Error : LogLevel.Warning
    });
```

The hook is passed each error the consumer receives and returns the level to log it at; returning
`LogLevel.None` suppresses the entry entirely. When no hook is supplied the previous behaviour is preserved
exactly.

**The hook affects logging only.** Fatal handling is unchanged and `Error.IsFatal` remains authoritative: a
fatal error latches the consumer as unrecoverable whatever level the hook returns, so suppressing a fatal
error's *log entry* does not suppress the *error*.

One internal consequence: because the level is now chosen per error, the two fixed-level `[LoggerMessage]`
sources for consumer errors are replaced by a single `ILogger.Log` call. The message template and its named
placeholders (`{ErrorCode}`, `{ErrorMessage}`, `{FatalError}`) are unchanged, so structured-logging queries
over those fields keep working.

### Kafka: message timestamps no longer lose their time zone (#4283, closes #4253)

`KafkaDefaultMessageHeaderBuilder` wrote the Brighter `timestamp` header from `Header.TimeStamp.DateTime` — the
offset-less component of a `DateTimeOffset` — formatted with the invariant culture. The offset never reached
the wire, so the reader had to guess, and `KafkaMessageCreator` guessed *host-local*: it parsed with
`DateTime.TryParse(..., AssumeUniversal)` and let the result be re-anchored to the consumer's local zone. On a
producer or consumer running anywhere other than UTC, `Header.TimeStamp` drifted by the host's UTC offset — and
drifted again on every hop.

The producer now writes RFC 3339 normalised to UTC (`yyyy-MM-ddTHH:mm:ss.fffZ`), matching the CloudEvents
`ce_time` header — which already round-tripped correctly — and the legacy timestamp header written by the other
gateways. The reader parses offset-aware with `DateTimeOffset.TryParse` under
`AssumeUniversal | AdjustToUniversal`, so an offset on the wire is honoured and the result stays anchored to
UTC.

The Unix-milliseconds fallback had the same defect by a different route: it truncated
`DateTimeOffset.FromUnixTimeMilliseconds(...)` through `.DateTime`, which yields a `DateTimeKind.Unspecified`
value that converts back to a `DateTimeOffset` at the *host* offset. It now returns the `DateTimeOffset`
directly.

#### Mixed-version rollout

The wire format of the `timestamp` header changes, so it is worth knowing what happens while producers and
consumers are on different versions:

* **New consumer, old producer** — handled explicitly and test-covered. `AssumeUniversal` covers the legacy
  offset-less format, so a message in flight from an older producer is read as UTC rather than as local time.
* **Old consumer, new producer** — the legacy reader parses with the same invariant `DateTime.TryParse`, which
  accepts RFC 3339 and honours the trailing `Z`, so the instant is read correctly.

If you were compensating for the drift downstream — adding the host offset back onto `Header.TimeStamp`, say —
remove that correction when you upgrade.


### Azure Service Bus: relative CloudEvents `source` and `dataschema` (#4310)

CloudEvents defines both `source` and `dataschema` as URI-*references*, which may be relative.
`AzureServiceBusMessageCreator` parsed them with the absolute-only `new Uri(string)` overload, so any
message carrying a relative `source` threw `UriFormatException` inside the consumer's receive loop and
was undeliverable. Azure Service Bus was the only gateway affected — every other transport already
parsed wire URIs with `Uri.TryCreate(..., UriKind.RelativeOrAbsolute, ...)`.

Fixed on both sides. The publisher also wrote `dataschema` as a `Uri` object rather than a string; the
Service Bus SDK serialises a `Uri` application property via `Uri.AbsoluteUri`, which throws for a
relative URI, so publishing one failed at send time.

#### Behaviour change: `Header.DataSchema` may now be `null` on messages received over Azure Service Bus

Previously, a message received over Azure Service Bus with no `dataschema` on the wire was given a
fabricated `http://goparamore.io` value. That is the documented default for `Source`, not for
`DataSchema`; `MessageHeader.DataSchema` is nullable and every other Brighter backend yields `null`.
Azure Service Bus now does too.

**If you read `Header.DataSchema` off an Azure Service Bus message without a null check, add one** —
you will now get `null` where you previously got a meaningless URI. Code that already treats the
property as nullable (as its type declares) is unaffected. This also stops Brighter re-publishing the
invented value on every requeue.

A relative `dataschema` stored in a relational Outbox is also now read back correctly:
`RelationDatabaseOutbox` read it with `UriKind.Absolute` and silently dropped it to `null`,
inconsistent with the `Source` reader in the same class and with every other Outbox implementation.

### MQTT: `ReceiveAsync` waits for a message, and honours the caller's cancellation token (#4240)

`MqttMessageConsumer` buffered arrivals in a queue and returned whatever happened to be in it at the
moment of the call. An empty buffer returned immediately with a single `MT_NONE` message, so a caller
who wanted to wait for a message had to sleep before every `Receive` and hope the sleep was long
enough - the timeout argument bounded how long the *drain* was allowed to take, not how long to wait
for a message to arrive. `ReceiveAsync` was `Task.FromResult(Receive(timeOut))`: synchronous, and it
ignored its `cancellationToken` entirely.

Both now wait up to `timeOut` (300 ms if unspecified) for at least one message to arrive, and are
woken by the arrival itself rather than by an interval expiring. A receive that finds nothing inside
the window still returns the single `MT_NONE` message, unchanged.

#### Behaviour change: a cancelled `ReceiveAsync` now throws

`ReceiveAsync` now observes the `cancellationToken` you pass it and throws `OperationCanceledException`
when you cancel, which is the contract the interface declares and the behaviour the other async
gateways already have. Previously the token was accepted and ignored, so a cancelled receive returned
an empty result instead.

**This does not affect the Brighter pump.** `Proactor` calls `Channel.ReceiveAsync(TimeOut)` without a
token and stops on an `MT_QUIT` message, not by cancelling a receive in flight, so shutdown is a clean
stop exactly as before. The change is visible only to code calling
`IAmAMessageConsumerAsync.ReceiveAsync` directly with a token it cancels - most often a test. **If you
have such a call and relied on it returning empty, catch `OperationCanceledException`.** A timeout
elapsing is not cancellation and still returns `MT_NONE`.

#### `BufferSize` is now honoured on MQTT

A consumer built by `MqttMessageConsumerFactory` returns at most the subscription's `BufferSize`
messages per receive, which is what that setting means. Previously a receive drained the whole buffer,
so a burst larger than `BufferSize` overflowed the Brighter `Channel` wrapper and threw. Anything still
buffered is left for the next call. A directly-constructed `MqttMessageConsumer` does not sit behind a
`Channel` and keeps its uncapped behaviour unless you pass the new optional `batchSize` argument.

### AWS SNS: `SendWithDelay` honours its delay, and needs a scheduler to do it (#4240)

`SnsMessageProducer.SendWithDelay` (both `Paramore.Brighter.MessagingGateway.AWSSQS` and
`…AWSSQS.V4`) discarded the delay it was given: the sync overload forwarded `TimeSpan.Zero` to the
async implementation, so a delayed send published **immediately**. It now forwards the delay it was
called with, and a delayed send is handed to the configured message scheduler as the other transports
already do.

#### Behaviour change: a delayed send with no scheduler configured now throws

Because the delay never survived the call, the no-scheduler path could not previously be reached from
`SendWithDelay` — the send simply went out at once. Now that the delay is honoured, a delayed send
with no scheduler configured throws `ConfigurationException` naming the missing setting, rather than
publishing immediately or failing with a `NullReferenceException` from inside the send.

**If you call `SendWithDelay` on SNS and have no `MessageSchedulerFactory` configured**, that call
silently behaved as an immediate publish and will now throw. Either configure a scheduler, or call
`Send` if immediate publication was what you wanted.

Delayed sends now also accept either half of the scheduler pair: a host that configures only
`IAmAMessageSchedulerSync` or only `IAmAMessageSchedulerAsync` no longer fails on a cast. The call
prefers the half matching the path it is on. This matches Redis, Kafka, MsSql, MQTT and the in-memory
reference implementation.

### GCP Pub/Sub: `Receive` and `ReceiveAsync` bound the Pull to the caller's timeout (#4240)

`GcpPullMessageConsumer` documented its `timeOut` as "not strictly used by the underlying Google
Pub/Sub client". It is now used: the Pull is bounded to that window, so an empty subscription returns
after the requested time instead of long-polling until a message arrives. An elapsed window is
reported as a normal empty receive.

When `timeOut` is null or non-positive, no deadline of ours is applied and the client's own
per-method expiration stays in force — the behaviour of the overload this replaced. In that case a
`DeadlineExceeded` still means a Pull took longer than the library expects, not that the subscription
is empty, and it is logged and rethrown as before rather than being reported as an empty receive.

**If you relied on `Receive` blocking until a message arrived**, pass a longer `timeOut`, or omit it
to keep the client default.

### MQTT: the producer emits producer telemetry (#4240)

`MqttMessageProducer` emitted no producer events. It now calls `BrighterTracer.WriteProducerEvent` on
both the sync and async send paths, which every other transport producer already did — MQTT was the
only gateway without it.

Verbosity follows the new optional `instrumentationOptions` constructor argument, which defaults to
`InstrumentationOptions.All` as `RmqMessageProducer` and `InMemoryMessageProducer` do. Note that
`All` includes `InstrumentationOptions.RequestBody`, so **message bodies are recorded on producer
spans** unless you pass a narrower option. Construct the producer with, for example,
`InstrumentationOptions.RequestInformation` if message bodies must stay out of your traces.

⚠️ **That only helps for a producer you construct yourself.** The requeue, dead-letter and
invalid-message producers that `MqttMessageConsumer` builds internally do not take the argument, so
they are fixed at `All` and their spans carry message bodies with no supported way to narrow them.
Tracked as [#4365](https://github.com/BrighterCommand/Brighter/issues/4365).


### RMQ.Async: subscriptions declare durable queues by default (#4355)

`RmqSubscription` and `RmqSubscription<T>` in `Paramore.Brighter.MessagingGateway.RMQ.Async` now default
`isDurable` to **`true`**. Previously they defaulted to `false`, which asked the broker for a transient,
non-exclusive queue.

RabbitMQ **4.3** deprecates that combination and **refuses to declare it by default**:

```
INTERNAL_ERROR - Feature `transient_nonexcl_queues` is deprecated.
By default, this feature is not permitted anymore.
```

Because `isDurable: false` was the *default*, an out-of-the-box RMQ.Async consumer could not connect to a
4.3 broker at all - the declare failed and Brighter surfaced a `ChannelFailureException`. The deprecation
notice states the feature will be removed "regardless of the configuration", so permitting it through
broker settings is only a stopgap.

`Paramore.Brighter.MessagingGateway.RMQ.Sync` is **unchanged** and still defaults to `isDurable: false`. It
targets the RabbitMQ 3.x line through the legacy `RabbitMQ.Client` 6.x API, and 3.x permits transient
non-exclusive queues.

#### Breaking: an existing transient queue will fail to redeclare

**Why this default had to move at all.** This is not Brighter changing its mind about a sensible default -
it is **RabbitMQ changing what it supports**. Transient non-exclusive queues were a supported queue shape
for the whole life of the 3.x line; 4.3 deprecates them and refuses to create them, and the deprecation
notice is explicit that they will be removed in a future major version "regardless of the configuration".
A default that a current broker will not accept is not a default we can keep. The cost of moving it is the
migration below, and there is no version of this change that avoids that cost - a broker cannot hold one
queue under two durabilities.

**What goes wrong.** RabbitMQ rejects a `QueueDeclare` whose arguments differ from the queue that already
exists, and durability is one of those arguments. **If you are on RMQ.Async and your queues were created
under the old default, upgrading will fail** when Brighter reopens the channel:

```
PRECONDITION_FAILED - inequivalent arg 'durable' for queue 'my.queue' in vhost '/':
received 'true' but current is 'false'
```

Brighter surfaces that as a `ChannelFailureException`. It happens on the first receive, not at startup, so
it can look like a runtime fault rather than an upgrade step.

**Two ways out.** Either keep the old behaviour explicitly:

```csharp
new RmqSubscription<MyEvent>(
    subscriptionName: new SubscriptionName("MySubscription"),
    channelName: new ChannelName("my.queue"),
    routingKey: new RoutingKey("my.topic"),
    isDurable: false)          // opt back in to a transient queue
```

- which keeps you working on 3.x and on a 4.3 broker explicitly configured to permit the deprecated
feature, but leaves you on a queue shape RabbitMQ intends to remove -

or **drain and delete the existing queue** and let Brighter recreate it as durable. Deleting a queue
discards any messages still in it, so drain it first if that matters. This is the option that leaves you
on a supported queue shape.

**What changes once the queue is durable.** A durable queue survives a broker restart, so messages that
would previously have been discarded with the queue now outlive it. For most deployments that is the
behaviour you wanted; if you were relying on a restart to clear a backlog, you no longer get that.

Note the dead-letter queue is declared with the same durability as its subscription, so both move together
and a partially-migrated pair is not possible.

We hit this inside Brighter's own test suite while making the change: two tests pre-created their queue as
transient and then opened a subscription that now defaults to durable, and failed with exactly the error
above. If it catches the suite that introduced the change, it will catch upgrades.

### MSSQL transport provisions its queue table (#4343)

`OnMissingChannel` on an `MsSqlSubscription` or a `Publication` is now honoured by the MSSQL
messaging gateway, as it already was by PostgreSQL. It is read as a channel is opened and as a
producer is built:

| `MakeChannels` | Behaviour |
|---|---|
| `Create` (the default) | the queue table and its topic index are created if absent, idempotently and safely when several instances start at once |
| `Validate` | the table is checked and a missing one throws `ConfigurationException` at startup, rather than failing on the first send |
| `Assume` | nothing happens and no connection is opened |

Before this, the setting was accepted and never read: `MsSqlMessageProducerFactory` and
`ChannelFactory` provisioned nothing, so every MSSQL transport user ran the DDL themselves. That is
why `MsSqlQueueBuilder` is public, and it remains public and unchanged.

The queue table is resolved through `SCHEMA_NAME()` — the schema the login defaults to — and not
through `RelationalDatabaseConfiguration.SchemaName`. `MsSqlMessageQueue` emits an unqualified
`[{QueueStoreTable}]` in every statement it issues, so it reads through the default schema too;
creating the table anywhere else would put it where the gateway never looks. `SchemaName` continues
to apply to the Outbox and the Inbox, which do qualify their SQL.

#### Behaviour change: `Create` is the default, so an existing application will now try to create its queue table

**If your queue table is provisioned by a migration, a DBA, or anything other than Brighter, set
`MakeChannels` to `Validate` or `Assume`.** Otherwise an application that has run happily for years
will, on upgrade, run an existence probe at startup where nothing ran before — and where the
application's login has DML rights but no DDL rights, SQL Server answers error 262,
`CREATE TABLE permission denied in database`, out of channel open or producer construction.

That case is now raised as a `ConfigurationException` naming the queue table, carrying the provider's
own message, and naming the setting that avoids it. A login with no DDL rights can still use
`Validate`, because checking is one `SELECT` over `sys.tables`.

Errors that will pass on their own — command and lock timeouts, the transport-level family, and the
Azure SQL unavailability and throttling codes — are deliberately left as the provider threw them, on
both the connect and the DDL, because telling their author to reconfigure `MakeChannels` would send
them to fix the wrong thing, and because typing a failover window as a `ConfigurationException`
defeats any policy that retries on `SqlException.Number`.

A wrong server, a malformed connection string and a misspelled database name are *not* in that set
and stay wrapped: those are the first-run mistakes the message exists for.

#### Under `Create`, the queue table name is bounded at 119 characters

SQL Server's identifier limit is 128, but the topic index is named after the table —
`IX_<table>_Topic` — so a longer name creates the table and then fails on the index with *"The
identifier that starts with … is too long"*, leaving a queue table that can never gain its index.
Provisioning refuses such a name up front with a message that explains the arithmetic.

`Validate` builds no identifier and keeps the full 128, so a longer table that already exists can
still be used.

### Azure Service Bus: the channel factory provisions the subscription before handing out a channel (#4309)

`AzureServiceBusChannelFactory` now honours `OnMissingChannel` on an `AzureServiceBusSubscription` as
it creates a channel, from `CreateSyncChannel`, `CreateAsyncChannel` and `CreateAsyncChannelAsync`
alike, in the same way as the AWS and GCP channel factories:

| `MakeChannels` | Behaviour |
|---|---|
| `Create` (the default) | the topic subscription (or, with `UseServiceBusQueue`, the queue) is created if absent, using the subscription's `AzureServiceBusSubscriptionConfiguration` |
| `Validate` | a missing subscription or queue throws `ChannelFailureException` from channel creation |
| `Assume` | nothing happens and no management-API call is made |

Before this, the factory ignored the setting and created nothing on the broker. The subscription was
created by the consumer's first receive, so a message published to the topic between the channel
being created and that first receive arrived at a topic with no subscription, and Azure Service Bus
silently discarded it. Nothing logged and nothing threw. The queue path did not lose messages, because
the producer creates the same queue on send.

#### Behaviour change: provisioning failures now surface at startup

The management-API calls that used to happen on the first receive now happen when the
`ServiceActivator` creates its channels. With `Create` or `Validate`, a missing subscription under
`Validate`, a management API that cannot be reached, or a credential without **Manage** rights on the
namespace now throws `ChannelFailureException` from channel creation, which is to say from
`Dispatcher.Receive()` at startup. Before, the same exception came from the first receive, where the
message pump caught it and retried after `ChannelFailureDelay`. There is no retry around channel
creation.

**If your subscriptions are provisioned by infrastructure-as-code, or your application's credential
has only Send/Listen rights, set `MakeChannels` to `Assume`.** `Validate` also needs Manage rights,
because checking whether a subscription exists is itself a management-API call.

`AzureServiceBusConsumerFactory`, used on its own without the channel factory, is unchanged: its
consumers still provision on first use.

### Outbox: `MaxOutStandingMessages` is now enforced for an async-only outbox (#4552)

The outstanding message count only went through `IAmAnOutboxSync`, so with an outbox that implements
only `IAmAnOutboxAsync` the count stayed at 0 and `MaxOutStandingMessages` was never enforced. It
now counts through the async outbox when there is no sync one. No outbox that ships with Brighter is
async-only, so none of them is affected. If you use a custom or third-party outbox that is
async-only and set `MaxOutStandingMessages` to 0 or more, `PostAsync` and `DepositPostAsync` can now
throw `OutboxLimitReachedException` where they never did before. With the default of -1 (no limit)
nothing changes. The async count blocks a thread-pool thread while it runs, and with the change for
issue #4554 below a mediator has at most one count queued or running.

### Outbox: the outstanding message check is skipped with no limit and no longer piles up (#4554)

With the default `AddProducers` settings (`MaxOutStandingMessages` of -1 and
`MaxOutStandingCheckInterval` of zero), every `Post` and clear queued a background count of the
outstanding messages, even though with no limit the count is never compared with one. Each count
waited on a thread-pool thread for a semaphore shared by every mediator with the same message and
transaction types, and then queried the outbox: a sort of every entry it held for the
`InMemoryOutbox`, a query per `Post` for a relational outbox. With a limit of -1 the count no
longer runs. A limit of 0 is a real limit and is still checked.

With no limit, the span the outbox creates for the count no longer appears under a clear
(`count.outstanding_messages` for the `InMemoryOutbox`, `retrieve.outstanding_messages` for a
relational outbox), the debug line "Outbox outstanding message count is" always reports 0, and the
other debug lines of the check, such as "Current outstanding count is", are no longer logged.

With a limit set, the checks no longer pile up. Each mediator keeps at most one check queued or
running, and a post that comes while one is in flight does not queue another, so with the default
interval of zero a clear queues a check only if the previous one has finished. Before, a burst of
posts could queue a check per post, each holding a thread-pool thread while it waited on the
semaphore. The time of the last check is now recorded when the check is queued rather than when it
starts, so `MaxOutStandingCheckInterval` is measured from then, and the debug lines "Time since last
check is" and "Running outstanding message check" measure from the same point. A post that finds a
check in flight does not log "Running outstanding message check".

## 10.7.0

### Azure Service Bus: dead-letter reason and description (#4196)

When a handler rejects a message consumed from Azure Service Bus, `AzureServiceBusConsumer` now records the rejection reason and description in the broker's native `DeadLetterReason` and `DeadLetterErrorDescription` fields rather than dead-lettering with blank values — so the reason is visible to operators triaging the dead-letter queue instead of living only in logs. Values are truncated to the 4096-character limit Azure Service Bus enforces. A `DeadLetterAsync(lockToken, reason, description)` overload is added to the public `IServiceBusReceiverWrapper`.

### Box Schema Versioning and Migrations (spec 0027)

Brighter's box-provisioning system now ships a versioned migration chain for the Outbox and Inbox tables. New deployments install at `V_latest` directly; deployments installed under spec 0023 (which only had a `V=1` history row) are recognised by the runner and advance to `V_latest` without re-running DDL — the existing `V=1` row is preserved verbatim and the V2..V_latest rows are appended. Deployments with pre-spec-0023 (legacy) tables are bootstrapped via column introspection, gated by a `HeaderBag`/`CommandBody` discriminator, then upgraded to `V_latest` under the existing per-backend migration lock.

#### Source-breaking change: `IAmABoxMigration`

The `IAmABoxMigration` interface (and the `BoxMigration` record) gain three new required members:

* **`IReadOnlyCollection<string> LogicalColumns`** — the cumulative column set the table has after this migration applies. Used by drift detection (the build fails if a column lands on the builder DDL without a matching migration entry) and by version inference for legacy tables (the runner walks the `LogicalColumns` chain to determine which migration to bootstrap from).
* **`string? SourceReference`** — the commit SHA (and PR number where available) that introduced the column. Required from V2 onwards; V1 stays `null`.
* **`string? IdempotencyCheckSql`** — used **only by SQLite**, whose grammar lacks `ALTER TABLE ADD COLUMN IF NOT EXISTS`. The SQLite runner evaluates this scalar as an existence probe and skips `UpScript` when the probe returns `> 0` (still inserting the history row). MSSQL / PostgreSQL / MySQL bake the existence check into the `UpScript` itself and leave `IdempotencyCheckSql` `null`.

External implementors of `IAmABoxMigration` will fail to compile until they add the new members. The change is source-breaking by design: `Paramore.Brighter` targets `netstandard2.0`, which does not support default interface members, so the spec-0023 pattern of adding required surface as a plain abstract member (e.g. `SchemaName`) is reused here. See ADR 0057 "Consequences → Negative" for the rationale.

```csharp
// Before
public class MyMigration : IAmABoxMigration
{
    public int Version => 8;
    public string Description => "Add MyColumn";
    public string UpScript => "ALTER TABLE Outbox ADD COLUMN MyColumn TEXT NULL";
}

// After
public class MyMigration : IAmABoxMigration
{
    public int Version => 8;
    public string Description => "Add MyColumn";
    public string UpScript => "ALTER TABLE Outbox ADD COLUMN MyColumn TEXT NULL";

    // Cumulative column set after this migration applies. The drift test compares this
    // to the live builder DDL — adding a column to the builder without listing it here
    // (or vice versa) fails CI.
    public IReadOnlyCollection<string> LogicalColumns { get; } =
        new[] { /* V1..V7 columns */, "MyColumn" };

    // V2+ migrations carry the commit SHA / PR number that introduced the column.
    public string? SourceReference => "abcd1234 (PR #4xxx)";

    // SQLite-only: existence probe so the runner can skip the ALTER if the column
    // already lives on the table (legacy bootstrap or a half-applied chain).
    // Leave null on MSSQL/PostgreSQL/MySQL — those backends use IF NOT EXISTS in UpScript.
    public string? IdempotencyCheckSql =>
        "SELECT COUNT(*) FROM pragma_table_info('Outbox') WHERE name = 'MyColumn'";
}
```

#### Source-breaking change: `IAmARelationalDatabaseConfiguration.SchemaName`

`IAmARelationalDatabaseConfiguration` gains a new required `string? SchemaName` member used by the box-provisioning runners (and the schema-qualified MSSQL advisory-lock resource — see "Behaviour notes" below). External implementors of the interface will fail to compile until they expose `SchemaName`. The shipped `RelationalDatabaseConfiguration` record in `Paramore.Brighter` already exposes the property and accepts `schemaName:` as an optional named argument, so call sites that use the shipped configuration record require no change.

```csharp
// Before — custom configuration class only needed to cover the message-payload mode
public class MyDatabaseConfiguration : IAmARelationalDatabaseConfiguration
{
    public string ConnectionString { get; }
    public string? OutBoxTableName { get; }
    public string? InboxTableName { get; }
    public bool BinaryMessagePayload { get; }
}

// After — must also expose SchemaName (defaulting to null preserves the V9 default of dbo/public)
public class MyDatabaseConfiguration : IAmARelationalDatabaseConfiguration
{
    public string ConnectionString { get; }
    public string? OutBoxTableName { get; }
    public string? InboxTableName { get; }
    public string? SchemaName { get; }
    public bool BinaryMessagePayload { get; }
}
```

#### Source-breaking change: `UseBoxProvisioning` overload consolidation

The `BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning` extension previously exposed two overlapping ways to set the migration lock timeout: a `TimeSpan? migrationLockTimeout` parameter on the extension method, and `BoxProvisioningOptions.MigrationLockTimeout` assignable from the configure delegate. The dual surface was confusing and the parameter form did not allow backends to read the timeout late.

The fix removes the parameter. Callers set the timeout exclusively through `BoxProvisioningOptions.MigrationLockTimeout` inside the configure delegate. Backend `AddXxxOutbox`/`AddXxxInbox` methods read the option lazily at registration time, so the order of statements inside the configure delegate does not matter. Existing callers that did not pass `migrationLockTimeout` (the typical case — all in-tree call sites and samples used the default) require no change.

```csharp
// Before
builder.UseBoxProvisioning(opts => opts.AddMsSqlOutbox(config), TimeSpan.FromMinutes(2));

// After — order inside the delegate is free
builder.UseBoxProvisioning(opts =>
{
    opts.AddMsSqlOutbox(config);
    opts.MigrationLockTimeout = TimeSpan.FromMinutes(2);
});
```

#### Additive: per-backend advisory-lock abstraction

The session-level migration-lock collaborator is now substitutable per backend, so tests and advanced integrators (custom connection-pool sharing, external lock-key derivation) can plug in their own implementation. Each runner gains two additive optional constructor parameters (the lock interface plus `Microsoft.Extensions.Logging.ILogger?`); existing two-arg construction continues to work unchanged. Lock-key derivation stays at the runner; the abstraction owns the lock SQL. See ADR 0057 §5b.

* **PostgreSQL**: `IPostgreSqlAdvisoryLock` / `PostgreSqlAdvisoryLock` (in `Paramore.Brighter.BoxProvisioning.PostgreSql`). Owns `pg_try_advisory_lock` / `pg_advisory_unlock`. Runner logs a Warning when `pg_advisory_unlock` returns `false` at release time (previously discarded silently).
* **MySQL**: `IMySqlAdvisoryLock` / `MySqlAdvisoryLock` (in `Paramore.Brighter.BoxProvisioning.MySql`). Owns `GET_LOCK` / `RELEASE_LOCK`. Release returns `bool?` (`true` released by us, `false` held by another, `null` did not exist); runner logs a Warning on any non-`true` outcome, naming the result code, table name, and lock key. Lock-key derivation continues to flow through the existing public `MySqlMigrationLockName.For` helper.
* **MSSQL**: `IMsSqlAdvisoryLock` / `MsSqlAdvisoryLock` (in `Paramore.Brighter.BoxProvisioning.MsSql`). Owns the `sp_getapplock` call. Acquire-only — `@LockOwner = 'Transaction'` means the lock auto-releases on the surrounding transaction's commit or rollback, so the abstraction has no `ReleaseAsync`. Each documented `sp_getapplock` negative return code is now translated into a distinguishable exception type so an operator can react with the right strategy: `-1` (timeout) → `TimeoutException`, `-2` (cancelled) → `OperationCanceledException`, `-3` (deadlock victim) → **new** `MigrationLockDeadlockException`, `-999` (parameter validation / call error) → `ArgumentException`. Previously every `< 0` result was collapsed into a generic `TimeoutException`. The 255-character `@Resource` length guard moves into the abstraction's acquire path. Lock-resource derivation `BrighterMigration_{table}` continues to live at the runner.

#### Behaviour notes

* Spec-0023-era `__BrighterMigrationHistory` rows at `MigrationVersion = 1` are still valid. The runner's normal path resumes from `MAX(V)`, the `IsMigrationAppliedAsync` gate skips the V1 row, and V2..V_latest are applied as ALTERs against the existing table. The original V1 description is preserved verbatim.
* `IAmABoxMigrationRunner.MigrateAsync` now takes a `BoxType boxType` argument so the runner can pick the correct discriminator (`HeaderBag` for outbox, `CommandBody` for inbox) when bootstrapping pre-spec-0023 tables. External callers must add the new argument on recompile.
* Spanner remains a degenerate runner: fresh installs stamp `V_latest` and existing tables either no-op (`MAX(V) == V_latest`), bootstrap to `V_latest` via the discriminator gate (no history row yet), or throw `ConfigurationException` (`MAX(V) != V_latest`, manual recovery required). See ADR 0057 §6.
* The MSSQL advisory-lock resource is `BrighterMigration_<schema>.<table>` (previously `BrighterMigration_<table>`). Two same-named tables in different schemas (e.g. `dbo.Outbox` and `billing.Outbox`) now acquire distinct `sp_getapplock` resources and migrate in parallel instead of serialising on a shared lock. The resource still stays well under the 255-character `@Resource` limit for any realistic `<schema>.<table>` pair.
* The SQLite runner emits `PRAGMA journal_mode=WAL` on every migration call by default. WAL is database-file-wide and persistent, so a host application that has deliberately picked DELETE or TRUNCATE journal mode would have its choice silently overridden. Pass `enableWalMode: false` to `AddSqliteOutbox` / `AddSqliteInbox` (or to the `SqliteBoxMigrationRunner` constructor) to skip the pragma and leave the existing journal mode untouched.

See [ADR 0057](docs/adr/0057-box-schema-versioning-and-migrations.md) and [spec 0027](specs/0027-box-schema-versioning-and-migrations/) for full details.

### Box Provisioning RDD role-interface refactor (spec 0028)

A fourth-pass review of PR #4039 surfaced static helper classes and free-standing runners across spec 0027's BoxProvisioning surface. Spec 0028 restructures that surface around Responsibility-Driven-Design role interfaces and a template-method runner base. The change is purely a structural refactor — no behaviour changes — but it is source-breaking for any external implementor of the affected types. The shipped Brighter call-sites and DI extensions absorb the cascade; existing `UseBoxProvisioning` configure-delegate users require no change. See [ADR 0058](docs/adr/0058-box-provisioning-rdd-role-interfaces.md) and [spec 0028](specs/0028-box-provisioning-rdd-role-interfaces/) for full details.

#### Source-breaking change: detection helpers become instance classes (`{Backend}BoxDetectionHelper`)

The static `{Backend}BoxDetectionHelpers` (plural) classes for all five backends become public instance classes `{Backend}BoxDetectionHelper` (singular) implementing the new role interfaces:

* **Relational four** (MSSQL/PostgreSQL/MySQL/SQLite) implement `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` — adds `DetectCurrentVersionAsync` on top of the base interface.
* **Spanner** implements the base interface `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` only — degenerate fresh-install model per ADR 0057 §6, no version inference. `SpannerBoxDetectionHelpers` was `internal`; the new `SpannerBoxDetectionHelper` is `public`.

Method-signature changes on the new instance methods:

* **MSSQL / PostgreSQL / MySQL**: `string schemaName` widens to `string? schemaName` (existing slot). Each impl substitutes the backend default when null (`"dbo"` / `"public"` / `connection.Database`). Positional argument lists at existing call-sites are unchanged.
* **SQLite / Spanner**: gain a `string? schemaName` parameter inserted between `tableName` and `cancellationToken`. Existing positional call-sites that passed `(connection, tableName, cancellationToken, transaction)` must insert an explicit `null` and become `(connection, tableName, null, cancellationToken, transaction)`. Each impl ignores the parameter.
* **All five backends**: `GetTableColumnsAsync` return type changes from `HashSet<string>` to `IReadOnlyCollection<string>` (looser; symmetric with `IAmABoxMigration.LogicalColumns` and netstandard2.0-compatible).

The widened nullability + return-type looseness are licensed by NF1: the spec 0027 surface had not shipped at the time spec 0028 landed (same PR).

```csharp
// Before
var exists = await MsSqlBoxDetectionHelpers.DoesTableExistAsync(
    connection, "Outbox", "dbo", ct, transaction);

// After
var helper = new MsSqlBoxDetectionHelper();
var exists = await helper.DoesTableExistAsync(
    connection, "Outbox", "dbo", ct, transaction);
// or null for schemaName — the helper substitutes "dbo":
var exists = await helper.DoesTableExistAsync(
    connection, "Outbox", null, ct, transaction);
```

#### Source-breaking change: migration catalogues become instance classes (`{Backend}{Box}MigrationCatalog`)

The static `{Backend}{Box}Migrations` classes (eight total — MSSQL/PG/MySQL/SQLite × Outbox/Inbox) become public instance classes `{Backend}{Box}MigrationCatalog` implementing `IAmABoxMigrationCatalog`. Spanner is exempt per ADR 0057 §6 (no migration catalogue).

```csharp
// Before
IReadOnlyList<IAmABoxMigration> migrations = MsSqlOutboxMigrations.All(config);

// After
IAmABoxMigrationCatalog catalog = new MsSqlOutboxMigrationCatalog();
IReadOnlyList<IAmABoxMigration> migrations = catalog.All(config);
// or receive the catalogue via DI (singleton lifetime registered by AddMsSqlOutbox).
```

#### Source-breaking change: payload-mode validators become instance classes (`{Backend}PayloadModeValidator`)

The static `{Backend}PayloadModeValidator` classes for all five backends become public instance classes implementing `IAmABoxPayloadModeValidator<TConnection>` (single-generic, no `TTransaction`). Method-signature changes:

* **MSSQL / PostgreSQL / MySQL**: `string schemaName` widens to `string?`. Existing positional call-sites are unchanged; each impl substitutes the backend default when null.
* **SQLite / Spanner**: gain a `string? schemaName` parameter inserted between `tableName` and `columnName`. Existing positional call-sites that passed `(connection, tableName, columnName, binaryMessagePayload, cancellationToken)` must become `(connection, tableName, null, columnName, binaryMessagePayload, cancellationToken)`. Each impl ignores the parameter.

#### Source-breaking change: provisioner constructor cascade

All ten existing provisioner classes (`{Backend}{Box}Provisioner` × four relational backends × two box-types, plus the `SpannerOutboxProvisioner`/`SpannerInboxProvisioner` pair) gain three new typed constructor parameters reflecting the static→instance conversion:

* `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` for the relational eight (provisioners call `DetectCurrentVersionAsync` during the bootstrap branch). Spanner's pair receives `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` (base interface — no version-detection capability).
* `IAmABoxMigrationCatalog` for the relational eight (Outbox provisioners receive the Outbox catalogue; Inbox provisioners receive the Inbox catalogue). Spanner's pair: omitted per ADR 0057 §6.
* `IAmABoxPayloadModeValidator<TConnection>` for all ten.

External code that constructs provisioners directly must supply the new parameters. Existing call-sites that wire provisioners via `UseBoxProvisioning(opts => opts.Add{Backend}Outbox(config))` are absorbed by the DI extensions — no change required.

#### Source-breaking change: runner constructor cascade and template-method base

The four relational migration runners (`MsSqlBoxMigrationRunner`, `PostgreSqlBoxMigrationRunner`, `MySqlBoxMigrationRunner`, `SqliteBoxMigrationRunner`) now derive from the new abstract base `SqlBoxMigrationRunner<TConnection, TTransaction>`. Each derived runner forwards new constructor parameters to the base:

* `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` — the typed detection helper.
* `IAmARelationalDatabaseConfiguration` — for `OpenConnectionAsync` to read `ConnectionString`, plus access to `OutBoxTableName`/`InBoxTableName`/payload-mode flags.
* `TimeSpan lockTimeout` — per-runner-instance deployment knob, supplied by `Add{Backend}Outbox`/`Add{Backend}Inbox` from `BoxProvisioningOptions.MigrationLockTimeout`.
* `ILogger? logger` — exposed to derived classes as `protected Logger { get; }` and forwarded into per-backend `IAmAProvisioningUnitOfWork<TTransaction>` construction.

The base owns the `MigrateAsync` algorithm — open connection, create UoW, begin UoW (lock + transaction in backend-specific order), ensure history table, re-detect existence under the UoW (TOCTOU defence per ADR 0057 §3), dispatch on detection state (fresh / bootstrap / normal), commit, rollback-on-throw with `CancellationToken.None`, dispose via `await using`. Each derived runner implements only the irreducibly-backend-specific hooks: `OpenConnectionAsync`, `CreateUnitOfWorkAsync`, `LockResourceFor`, `EnsureHistoryTableAsync`, `RunFreshPathAsync`, `RunBootstrapPathAsync`, `RunNormalPathAsync`. The Spanner runner remains free-standing per ADR 0057 §6 (degenerate fresh-install-only).

External code that constructs the relational runners directly must supply the new parameters. The harmonised UoW lifecycle / cancellation / disposal contract is described in ADR 0058 §B.3.

#### Additive: new public types

Spec 0028 introduces the following net-new public surface (all in `Paramore.Brighter.BoxProvisioning` unless noted):

* **Role interfaces** (5): `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`, `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` (extends the base), `IAmABoxMigrationCatalog`, `IAmABoxPayloadModeValidator<TConnection>`, `IAmAProvisioningUnitOfWork<TTransaction>`.
* **Abstract base** (1): `SqlBoxMigrationRunner<TConnection, TTransaction>` implementing `IAmABoxMigrationRunner`.
* **Abstract base** (1, sub-phase A): `SqlBoxProvisioner<TConnection, TTransaction>` — abstract base class in `Paramore.Brighter.BoxProvisioning` for the eight relational provisioners (MSSQL/PG/MySQL/SQLite × Outbox/Inbox). Spanner's pair stays free-standing per ADR 0057 §6.
* **Provisioning UoW implementations** (4 — one per relational backend, in each backend's package): `MsSqlProvisioningUnitOfWork`, `PostgreSqlProvisioningUnitOfWork`, `MySqlProvisioningUnitOfWork`, `SqliteProvisioningUnitOfWork`. Each encapsulates that backend's specific lock+transaction pairing and ordering.
* **Detection-helper instance classes** (5): `MsSqlBoxDetectionHelper`, `PostgreSqlBoxDetectionHelper`, `MySqlBoxDetectionHelper`, `SqliteBoxDetectionHelper`, `SpannerBoxDetectionHelper`.
* **Migration-catalogue instance classes** (8): `{MsSql,PostgreSql,MySql,Sqlite}{Outbox,Inbox}MigrationCatalog` (Spanner exempt).
* **Payload-validator instance classes** (5): `{MsSql,PostgreSql,MySql,Sqlite,Spanner}PayloadModeValidator`.

DI extensions in each `Add{Backend}{Box}` register the detection helper, catalogue, and payload validator as singletons (each role-impl is stateless after construction); existing call-site shape `UseBoxProvisioning(opts => opts.Add{Backend}Outbox(config))` is unchanged.

### Multi-Tenancy Migration History Scope (spec 0029)

The box migration-history table can now be placed **per tenant schema** instead of always landing in the backend default schema. The default behaviour is unchanged — existing deployments keep `__BrighterMigrationHistory` in `dbo` / `public` / the connection-bound database regardless of `SchemaName`. Set `BoxProvisioningOptions.MigrationHistoryScope = MigrationHistoryScope.PerSchema` to opt this deployment into per-schema placement on MSSQL and PostgreSQL. See [ADR 0060](docs/adr/0060-multi-tenancy-migration-history-scope.md) and [spec 0029](specs/0029-multi-tenancy-migrations/) for full details.

```csharp
services
    .AddBrighter()
    .UseBoxProvisioning(opts =>
    {
        opts.MigrationHistoryScope = MigrationHistoryScope.PerSchema;
        opts.AddMsSqlOutbox(configuration);    // history lands in configuration.SchemaName
        opts.AddPostgreSqlInbox(configuration);
    });
```

#### Additive: new public types

* **Enum**: `MigrationHistoryScope` in `Paramore.Brighter.BoxProvisioning` with values `Global` (default — today's behaviour) and `PerSchema`.
* **Property**: `BoxProvisioningOptions.MigrationHistoryScope` (defaults to `MigrationHistoryScope.Global`).

#### Source-breaking change: `IAmABoxMigrationDetectionHelper.DoesHistoryExistAsync` / `GetMaxVersionAsync` gain a `historySchema` parameter

`IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` gains a `string? historySchema` parameter on `DoesHistoryExistAsync` and `GetMaxVersionAsync` (placed after the existing `schemaName`). The derived `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` interface file itself is unchanged — its implementors inherit the new signature through interface inheritance. `null` means "the backend default" — i.e. today's behaviour — so the bundled Brighter detection helpers and call-sites are byte-for-byte unchanged. External implementors of either interface must add the new parameter on recompile; passing `null` preserves existing semantics.

```csharp
// Before
Task<bool> DoesHistoryExistAsync(
    TConnection connection, string tableName, string? schemaName,
    CancellationToken cancellationToken = default,
    TTransaction? transaction = null);

// After
Task<bool> DoesHistoryExistAsync(
    TConnection connection, string tableName, string? schemaName, string? historySchema,
    CancellationToken cancellationToken = default,
    TTransaction? transaction = null);
```

`DetectCurrentVersionAsync` is **unchanged** — it reads box-table columns, not history.

#### Source-breaking change: runner constructor cascade gains an optional `scope` parameter

The four relational runners (`MsSqlBoxMigrationRunner`, `PostgreSqlBoxMigrationRunner`, `MySqlBoxMigrationRunner`, `SqliteBoxMigrationRunner`) and the abstract base `SqlBoxMigrationRunner<TConnection, TTransaction>` gain a final `MigrationHistoryScope scope = MigrationHistoryScope.Global` constructor parameter. The default keeps existing positional call-sites compiling; external code that constructs the runners directly with named arguments past this position will need a small adjustment. `Add{Backend}Outbox`/`Add{Backend}Inbox` absorb the cascade and read `BoxProvisioningOptions.MigrationHistoryScope`; existing DI call-sites are unchanged.

#### Source-breaking change: `EnsureHistoryTableAsync` hook gains a `tableName` parameter

The `protected abstract Task EnsureHistoryTableAsync(...)` hook on `SqlBoxMigrationRunner<TConnection, TTransaction>` gains a `string tableName` parameter. The MSSQL and PostgreSQL hook implementations use it to filter the `Global → PerSchema` auto-seed to this tenant's rows; MySQL and SQLite accept and ignore it. External code that derives from `SqlBoxMigrationRunner<TConnection, TTransaction>` (rare — designed for the four shipped backends) must thread the new parameter through.

#### Behaviour notes

* **Backend support.** Only MSSQL and PostgreSQL honour `PerSchema` placement. MySQL (where schema == database), SQLite (no schema concept), and Spanner (degenerate fresh-install-only model per ADR 0057 §6) treat `PerSchema` as a no-op and keep history in their default location — no exception, so a single `BoxProvisioningOptions` can target a mixed backend set without per-backend branching. The placement decision is surfaced per run via an `Information` log of the form `Box migration history for {BoxTable} resolved to schema {HistorySchema} (scope {Scope})` (on no-op backends `HistorySchema` is the literal `<backend default>`).
* **Global → PerSchema auto-seed.** After flipping a previously-`Global` MSSQL/PG deployment to `PerSchema`, the runner copies this tenant's prior history rows from the legacy default-schema table into the per-schema table under the same advisory lock and transaction as the CREATE — existing migrations are not re-applied. The seed copies all five columns (`MigrationVersion`, `SchemaName`, `BoxTableName`, `Description`, `AppliedAt`) filtered by `(SchemaName, BoxTableName)`, with a composite-primary-key `NOT EXISTS` guard so repeated flips are idempotent. The seed runs on **every** PerSchema provision (so the second box-type to flip — e.g. inbox after outbox — still gets seeded into the per-schema history table the first flip created); the NOT EXISTS guard makes steady-state runs a zero-row no-op. A distinct `Information` log records `Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}` plus an OpenTelemetry `Activity` event `legacy_history_seeded` carrying the row count as the `brighter.box.migration.seed.rows` tag.
* **Permission requirement (every run, not just the first flip).** Because the seed's `INSERT…SELECT` executes on every PerSchema provision, the runner needs `SELECT` on the legacy default-schema history table for the **lifetime of the PerSchema deployment**. Operators who grant `SELECT` only for the initial flip and then revoke it will hit a `ConfigurationException` on every subsequent provision run, with the inner provider exception attached.
* **Reverse flip (`PerSchema → Global`) and legacy-row cleanup are out of scope.** The per-schema history table remains in the tenant's schema if a deployment is later switched back to `Global`; the legacy default-schema rows survive after a PerSchema flip. Both are harmless but storage-redundant; operators wanting to reclaim that storage must run their own ad-hoc DELETE / DROP.
* **Misconfiguration.** Selecting `PerSchema` on a placement backend with a `null` `SchemaName` throws `ConfigurationException` at the entry to the runner. Per-tenant identifiers flow through `Identifiers.AssertSafe` before any DDL is emitted, so an injection-shaped `SchemaName` is rejected at the provisioner entry well before reaching the database.

### Replace Primitive Obsession in Box Provisioning with Value Types (spec 0030)

The box-provisioning contracts now use dedicated value types instead of bare `string`/`int`, following the `Id` template and [ADR 0019 "Avoid Primitive Obsession"](docs/adr/0019-avoid-primitive-obsession.md). See [ADR 0061](docs/adr/0061-box-provisioning-value-types.md) and [spec 0030](specs/0030-primitive_obsession/) for full details.

#### Additive: new value types

Six value types land in `Paramore.Brighter.BoxProvisioning`, each with bidirectional implicit conversions to/from its underlying primitive, so existing string/int call-sites continue to compile unchanged:

* **`BoxTableName`**, **`MigrationDescription`**, **`SqlScript`**, **`SourceReference`** — wrap `string`.
* **`SchemaName`** — wraps `string`; `null` models "not supplied" (e.g. SQLite has no schema).
* **`MigrationVersion`** — wraps `int`, with arithmetic and `IComparable` ordering preserved through the implicit `int` conversion.

The `IAmABoxMigration` / `BoxMigration` and `IAmABoxMigrationRunner` surfaces are retyped to these value types. Because the conversions are implicit and bidirectional, this is **source-compatible** for callers passing primitives; external implementors overriding members will see the value types in the new signatures.

#### Source-nullability ripple: core implicit `operator string` widened to `operator string?`

While applying the value-type pattern we corrected a latent null-safety bug in nine existing core value types — `Id`, `RoutingKey`, `CloudEventsType`, `PartitionKey`, `SubscriptionName`, `TraceContext.TraceParent`/`TraceState` (in `Paramore.Brighter`), and `ConsumerName`/`HostName` (in `Paramore.Brighter.ServiceActivator`). These are **reference-type** records/classes, and a user-defined conversion on a reference type is *not* null-lifted the way a `Nullable<T>` conversion is: `(string?)(T?)null` invoked `operator string` on a null receiver and threw a `NullReferenceException`. Each operator changed from `operator string(T t) => t.Value` to the null-safe `operator string?(T t) => t?.Value`, matching the long-standing `ChannelName` precedent.

* **Binary-compatible.** `string` and `string?` are the same IL type, so already-compiled consumers are unaffected at runtime.
* **Source ripple for downstream NRT consumers.** Because these operators now return `string?`, downstream projects with nullable reference types enabled will see `CS8600`/`CS8604` where the result feeds a non-nullable `string`, e.g.:

  ```csharp
  string topic = someRoutingKey;        // CS8600: converting string? to string
  dict.Add(someId, value);              // CS8604: someId is now string?
  ```

  The fix is the same one applied throughout this PR across ~60 in-box assemblies: take the underlying value explicitly with `.Value` (which is non-nullable), or `?.Value ?? fallback` when the source is itself nullable:

  ```csharp
  string topic = someRoutingKey.Value;          // non-nullable source
  dict.Add(someId.Value, value);
  string reply = header.ReplyTo?.Value ?? "";   // nullable RoutingKey? source
  ```

  No call-site fix is needed unless your code both has NRT enabled and treats warnings as errors.

> Note: `Tenant` (in `Paramore.Brighter.Transformers.JustSaying`) is a `readonly record struct`, not a reference type — its receiver can never be null, so its `operator string` is intentionally left non-nullable.

### Per-message factory scope leak fix; transient handler lifetime now isolates its DI scope (#4252, #4254)

`ServiceProviderLifetimeScope` — the lifetime helper shared by the handler, mapper and transformer factories — previously created a **single** `IServiceScope` per factory and reused it for every transient resolution. For the app-lifetime mapper and transformer factories that scope was never released per message, so a transient mapper or transform accumulated one scope per message for the life of the process — the leak reported in #4252. Because `MapperLifetime` and `TransformerLifetime` both **default to `Transient`** (`ServiceLifetime.Transient`), this was the default code path, not an opt-in one. `GetTransient` now creates a fresh `IServiceScope` per resolution, tracked by the scope's own identity and disposed when the resolution's lease is released, closing the leak: each transient mapper/transform now gets and releases its own scope.

#### Breaking change: `Create`/`Get` return an opaque `Lease<T>`, and `Release` keys on the lease, across six public factory / registry interfaces

Closing the leak on the mapper path required a way to return a mapper to its factory (transformers already had one), and returning a mapper/transform to the *right* resolution's scope required keying release on the resolution rather than the instance. The factory and registry surface therefore now flows an opaque `Lease<T>` (see *Opaque lease* below) from `Create`/`Get` to `Release`:

* `IAmAMessageMapperFactory` — `Lease<IAmAMessageMapper>? Create(Type)`, `void Release(Lease<IAmAMessageMapper>?)`
* `IAmAMessageMapperFactoryAsync` — `Lease<IAmAMessageMapperAsync>? Create(Type)`, `void Release(Lease<IAmAMessageMapperAsync>?)`, `ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>?)`
* `IAmAMessageTransformerFactory` — `Lease<IAmAMessageTransform>? Create(Type)`, `void Release(Lease<IAmAMessageTransform>?)`
* `IAmAMessageTransformerFactoryAsync` — `Lease<IAmAMessageTransformAsync>? Create(Type)`, `void Release(Lease<IAmAMessageTransformAsync>?)`, `ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>?)`
* `IAmAMessageMapperRegistry` — `Lease<IAmAMessageMapper<T>>? Get<T>()`, `void Release<T>(Lease<IAmAMessageMapper<T>>?)`
* `IAmAMessageMapperRegistryAsync` — `Lease<IAmAMessageMapperAsync<T>>? GetAsync<T>()`, `void Release<T>(Lease<IAmAMessageMapperAsync<T>>?)`, `ValueTask ReleaseAsync<T>(Lease<IAmAMessageMapperAsync<T>>?)`

`Release`/`ReleaseAsync` take the lease as `Lease<T>?` and treat a `null` lease as a no-op, so the "over-release is harmless" contract holds for the `null` a caller following the "release what you `Get`" rule may still hold (`Get`/`Create` return `Lease<T>?`) without a hand-written null check.

`Paramore.Brighter` targets `netstandard2.0`, which has no runtime support for default interface members, so these ship without a default body — **any third-party implementation of these interfaces must update to the lease-typed signatures** to compile, and any caller holding a `Create`/`Get` result as a bare mapper/transform must read it through `.Instance` and release the lease. All in-tree implementations are updated; the `Func`-based `SimpleMessageMapperFactory` constructor is unchanged (it wraps the `Func` result in a no-op lease), so its call sites are unaffected.

##### Opaque lease

`Lease<T>` (new, in `Paramore.Brighter`) is a small `sealed class` pairing the resolved `Instance` with an opaque `ReleaseToken`. The token — for the DI-backed factories, the resolution's own `IServiceScope` — lets the factory reclaim exactly the one resolution being released, so a shared instance handed out under a transient lifetime is torn down one resolution at a time and an over-release is a no-op. A factory that opens a per-resolution scope **must** return a lease carrying its release token (`new Lease<T>(instance, token)`); the token-less "reclaims nothing on release" case — a shared instance, or a no-op factory — is built through the named `Lease<T>.Untracked(instance)`. There is no implicit conversion from `T`: a bare `return mapper;` from a custom factory does not compile, so a scope-owning factory cannot silently produce a token-less lease whose `Release` is a no-op (which would reopen the leak this change closes). If you implement `IAmAMessageMapperFactory`/`IAmAMessageTransformerFactory` (or their async forms), return `new Lease<T>(instance, token)` when you open a scope, `Lease<T>.Untracked(instance)` when you hand out a shared instance or reclaim nothing, and `null` when nothing resolves.

The two mapper-registry interfaces also gain a type-resolution member so a caller can ask *"is a mapper registered for this request?"* without creating (and then having to release) one:

* `IAmAMessageMapperRegistry` — `(Type? MapperType, bool IsDefault) ResolveMapperInfo(Type requestType)`
* `IAmAMessageMapperRegistryAsync` — `(Type? MapperType, bool IsDefault) ResolveAsyncMapperInfo(Type requestType)`

Both mirror `Get`/`GetAsync` (factory-aware, same default and generic-definition guards) without instantiating. `TransformPipelineBuilder[Async].HasPipeline` now answers through them, so the outbox send/receive path no longer creates and releases a throwaway probe mapper per message. Same netstandard2.0 rule: third-party registry implementations must add the member to compile.

#### Breaking change: validation/diagnostics constructors take a `Func<MessageMapperRegistry>`

Making registry ownership explicit (the disposer creates the disposable it disposes) changed three `public` signatures. These are compile-time source breaks — the registry parameter moved from an instance to a factory delegate:

| Symbol | Was | Now |
|---|---|---|
| `PipelineValidator` constructor, param 6 | `MessageMapperRegistry? mapperRegistry` | `Func<MessageMapperRegistry>? mapperRegistryFactory` |
| `PipelineDiagnosticWriter` constructor, param 3 | `MessageMapperRegistry? mapperRegistry` | `Func<MessageMapperRegistry>? mapperRegistryFactory` |
| `ConsumerValidationRules.UnwrapTransformResolvable` | `(MessageMapperRegistry, IAmATransformerResolvabilityProbe)` | `(Func<MessageMapperRegistry>, IAmATransformerResolvabilityProbe)` |

Both constructors take the registry positionally among optional parameters, so anyone constructing a `PipelineValidator` or `PipelineDiagnosticWriter` directly — in a test or a custom host — and anyone calling the `public static` `UnwrapTransformResolvable` rule must pass a factory (`() => registry`) instead of the registry. The rule now invokes the factory once and owns/disposes only the registry it created, so it can never dispose a caller's shared registry.

> **`Create`/`Get`/`GetAsync` now return an opaque `Lease<T>`, and `Release` takes that lease.** This is a breaking signature change on the factory and registry surface: `IAmAMessageMapperFactory[Async].Create`, `IAmAMessageTransformerFactory[Async].Create`, and `IAmAMessageMapperRegistry[Async].Get<T>`/`GetAsync<T>` now return a `Lease<T>?` (null when nothing resolves), and `Release`/`ReleaseAsync` take a `Lease<T>` rather than the bare instance. A `Lease<T>` pairs the resolved `Instance` with an opaque `ReleaseToken`; hold the lease from create to release. If you resolve a mapper or transform directly, you must still call `Release` (or `ReleaseAsync`) when finished, **even for a non-disposable mapper such as the default `JsonMessageMapper`** — a transient resolution opens an `IServiceScope` per resolution (it can own the instance's injected dependencies and its own `IServiceProvider`), and that scope is retained until the lease is released or the factory is disposed at host shutdown. All in-tree call sites already release. Note that `SimpleMessageMapperFactory`'s `Release` is a deliberate no-op — the `Func` you supply owns what it returns — so a `Func` that news up a disposable mapper is not disposed for you.

The lease keys release on the **resolution**, not the instance, which designs out a whole bug class: a mapper or transform registered in the container as a `Singleton` (or otherwise handing back one shared instance) while its `MapperLifetime`/`TransformerLifetime` is the default `Transient` opens a fresh scope per resolution over the same object. Keyed by instance identity (the previous model) releasing one resolution could dispose a scope another still-live resolution depended on — a use-after-dispose — and an over-release could pop yet another. Keyed by the lease, `Release(lease)` disposes exactly that resolution's scope, and an over-release of a lease is an **idempotent no-op**. Because the lease's generic argument carries the interface (`Get<T>` returns a `Lease<IAmAMessageMapper<T>>`, `GetAsync<T>` a `Lease<IAmAMessageMapperAsync<T>>`), releasing a dual-interface mapper resolved from `GetAsync` through the sync factory is now a **compile-time type error** rather than a silent leak — no interface cast is needed on the concrete registry:

```csharp
var registry = ServiceCollectionExtensions.MessageMapperRegistry(sp);

var lease = registry.Get<MyCommand>();                        // Lease<IAmAMessageMapper<MyCommand>>?
// ...use lease.Instance...
registry.Release(lease);                                      // binds to the sync overload by lease type

var asyncLease = registry.GetAsync<MyCommand>();              // Lease<IAmAMessageMapperAsync<MyCommand>>?
registry.Release(asyncLease);                                 // ...or ReleaseAsync(asyncLease)
```

#### Deterministic factory disposal at host shutdown

The IoC-backed mapper and transformer factories (`ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`, `ServiceProviderTransformerFactoryAsync`) are created for, and owned by, the objects that use them; they are not registered in the container. Those owners now dispose them, so every per-resolution `IServiceScope` they retain is drained at teardown instead of being held until the process exits:

* `MessageMapperRegistry` is now `IDisposable` and disposes the two mapper factories it was built from.
* `OutboxProducerMediator.Dispose()` now disposes the `MessageMapperRegistry` (cascading to both mapper factories) and the two transformer factories, in addition to closing the producer registry.
* `PipelineValidator` and `PipelineDiagnosticWriter` are now `IDisposable` and dispose the validation/diagnostic-time `MessageMapperRegistry` each builds.

* `Dispatcher` is now `IDisposable` and disposes the `MessageMapperRegistry` (cascading to both mapper factories) and the two transformer factories built for it — the consumer-side counterpart to the mediator's producer-side disposal. The `IDispatcher` **interface** deliberately does not extend `IDisposable`: on the `AddServiceActivator` path the `Dispatcher` is a container singleton, so the container disposes it (and drains its factories) at host shutdown, and adding `IDisposable` to the interface would be a source break for every external implementer for no gain on that path. If you wire a `Dispatcher` up **manually** and hold it as an `IDispatcher`, cast to `Dispatcher` (or `IDisposable`) to dispose it and drain the factories it owns.

All of these owners are registered as container singletons, so the container disposes them — and therefore drains their factories — at host shutdown. Previously none were disposed, so a scope a direct resolver failed to release was held for the life of the process. The `IDisposable` additions themselves are binary-compatible; note, however, that the `PipelineValidator` / `PipelineDiagnosticWriter` constructor and `UnwrapTransformResolvable` signatures **did** change — see *Breaking change: validation/diagnostics constructors* above.

> **Ownership note for manual wiring.** Because `MessageMapperRegistry` is now `IDisposable` and `OutboxProducerMediator.Dispose()` cascades into the `IAmAMessageMapperRegistry` **and both transformer factories** it was given, **disposing a `CommandProcessor`'s external bus now disposes the mapper registry and the transformer factories you handed it.** The standard manual shape is `DispatchBuilder.MessageMappers(registry, registryAsync, transformFactory, transformFactoryAsync)`, so the transform factories are shared just as readily as the registry. In the DI path this is airtight — each mediator gets its own registry and factories newed per resolution — but if you **manually** share a `MessageMapperRegistry` or a `ServiceProviderTransformerFactory` between a `CommandProcessor` external bus and a `Dispatcher`, disposing the command processor disposes objects the dispatcher is still using; subsequent mapper or transform resolutions then throw `ObjectDisposedException` per message. This is a runtime break with no compile-time signal. If you share a registry or transformer factory across independently-disposed owners, give each owner its own, or defer disposal until all owners are done.

**This shutdown disposal is a teardown backstop, not a substitute for `Release`.** It reclaims once, at host shutdown; it does nothing for a host that runs for days. Releasing each mapper/transform you resolve directly — as the pipeline does on every message — is what bounds retention *during* the run. Relying on owner disposal for reclamation along the way just reproduces the unbounded accumulation this fix closes.

#### Breaking change: `IBrighterOptions` gains `IsolateTransientHandlerScope`

`IBrighterOptions` (in `Paramore.Brighter.Extensions.DependencyInjection`) gains a `bool IsolateTransientHandlerScope { get; set; }` member — the opt-out described in the *Behaviour change* below. `Paramore.Brighter.Extensions.DependencyInjection` targets `netstandard2.0`, which has no default interface members, so this ships without a default body: **any third-party implementation of `IBrighterOptions` must add the member to compile.** In-tree there is exactly one implementer (`BrighterOptions`), which defaults it to `true` (isolate), so the change is invisible to CI but a source break for external implementers — the same class of break as the factory/registry interfaces above.

#### Behaviour change: transient handler lifetime isolates its DI scope per handler

On the **default** configuration — `HandlerLifetime.Transient` with `IsolateTransientHandlerScope = true` — two handlers in the same pipeline that each inject a DI-`Scoped` dependency (an EF Core `DbContext`, a unit of work, a transaction provider) now receive **two instances, and therefore two transactions, where they previously received one.** If your handlers relied on a single `DbContext` / one transaction being shared across the pipeline for a message, that unit of work is now split — see the fix below.

This is the only **observable semantic** change in this release, and it is invisible at compile time — no signature change and no exception; only the number of DI-`Scoped` instances a pipeline sees changes. (The interface additions above are the compile-time breaks.) It lands on the **default** `HandlerLifetime` (`Transient`).

A handler pipeline for a single message resolves every handler in the chain — attribute/middleware handlers plus the target handler — through one `IAmALifetime`. Because all transient resolutions used to share that factory's single `IServiceScope`, a dependency **registered in DI as `Scoped`** (an EF Core `DbContext`, a unit of work, a transaction provider) was one shared instance across the whole chain for that message. Now each transient handler is resolved in its **own** `IServiceScope`, so a DI-`Scoped` dependency is a **distinct instance per handler** — the two-contexts / two-transactions consequence above.

This aligns `Transient` with its DI meaning (a transient resolution is genuinely isolated) and is what allows a transient's scope to be its own to create and release, which is what closes the leak above.

**If you rely on a DI-`Scoped` dependency being shared across the handlers in a pipeline** — the unit-of-work / one-transaction-per-message pattern — set the **handler** lifetime to `Scoped`:

```csharp
services.AddBrighter(options =>
{
    options.HandlerLifetime = ServiceLifetime.Scoped; // default is Transient
});
```

Under `Scoped`, every handler in the pipeline shares one `IServiceScope`, so a `Scoped` dependency is a single instance for the message — the pre-fix sharing behaviour.

Switching the **handler** lifetime to `Scoped` is the intended way to share state across a pipeline, and almost certainly what you want if you were depending on the old sharing — under `Transient` it was never a designed behaviour, just a side effect of the shared scope. So we do not expect anyone to need an escape hatch. If you do rely on the pre-#4254 sharing and cannot move to `Scoped` immediately, you can restore it **without changing the handler lifetime** by setting `IsolateTransientHandlerScope = false`:

```csharp
services.AddBrighter(options =>
{
    options.IsolateTransientHandlerScope = false; // default is true; prefer HandlerLifetime.Scoped
});
```

With the flag off, the transient handlers in one pipeline again share a single `IServiceScope` (disposed when the pipeline completes), so a DI-`Scoped` dependency is one shared instance across the chain — the pre-#4254 behaviour — while `Transient` still means a fresh handler instance per resolution. The flag governs **only** transient handlers; it has no effect on `Scoped` or `Singleton` handlers, nor on the mapper and transformer factories, which always isolate (that is the leak fix). It defaults to `true`, so the isolating behaviour described above is the default, and the flag is a compatibility fallback rather than a knob most applications should touch.

#### Other observable changes

A few smaller changes that are unlikely to affect you but are observable:

* **`IAmAMessageMapperRegistry.Get<TRequest>()` no longer registers the default mapper it falls back to.** When no mapper is registered for `TRequest`, `Get<TRequest>()` / `GetAsync<TRequest>()` still returns the default mapper, but now records that resolution in a separate cache rather than writing it into the registration table. So a fallback no longer answers *"a mapper is registered for `TRequest`"*, and a subsequent `Register<TRequest, TMapper>()` for that type **succeeds** where it previously threw `ArgumentException("… already has a mapper")`. An explicit `Register` still always wins on a later `Get`.
* **`ServiceProviderHandlerFactory.Release` no longer disposes the handler itself; it disposes the per-resolution scope instead.** Both `Release` overloads dropped the old `if (handler is IDisposable d) d.Dispose();` — for the Transient and Scoped lifetimes the handler was resolved from a `ServiceProviderLifetimeScope`, and disposing that scope is what disposes the handler, exactly once (disposing it here as well double-disposed it). One case changes observably: a handler **registered in the container as a singleton** (`services.AddSingleton<MyDisposableHandler>()`) but resolved under the **default `HandlerLifetime.Transient`** comes from the root provider, so the per-resolution child scope tracks nothing and disposes nothing — that handler used to be `Dispose()`d after every message and now is not disposed until the root provider is torn down at host shutdown. Disposing a process-wide singleton once per message was itself a bug, so this is a fix, but it is an observable change on the handler path. If you relied on it, register the handler as transient (`services.AddTransient<MyDisposableHandler>()`) so each per-message scope owns and disposes it.
* **Resolving a mapper or transform after its factory is disposed now throws `ObjectDisposedException`.** `ServiceProviderMapperFactory` / `ServiceProviderTransformerFactory` (through `ServiceProviderLifetimeScope.GetOrCreate`) previously returned an instance from an already-disposed factory; they now throw. In practice this only surfaces if an in-flight message resolves a mapper during host shutdown, after the factory has been disposed — a race the `Dispatcher` shutdown drain below now closes on the consumer side. The `ObjectDisposedException` message names the configured lifetime and points at the shared-registry cause rather than an internal type name.
* **`Dispatcher.Dispose()` now stops the pumps and drains their in-flight message before disposing the mapper/transform factories, bounded by a configurable `ShutdownTimeout`.** Because `Dispose()` cascades disposal into the factories (see the ownership item below), a container teardown that raced a still-running pump — the host's `ShutdownTimeout` elapsed before the consumers drained, or the provider was disposed without a graceful stop — could tear the factories down under an in-flight message, surfacing the `ObjectDisposedException` above, which was reclassified as *Unacceptable* and the good message **rejected and discarded**. `Dispose()` now calls `End()` first (which pushes a quit onto each pump so it runs out its current message, acknowledges it, and stops) and waits for the drain before disposing. If the drain exceeds the timeout, disposal proceeds anyway and the interrupted message is left **un-acknowledged** so the broker redelivers it rather than dropping it. The wait is bounded by a new `TimeSpan ShutdownTimeout` (default **10 seconds**), configurable so a consumer with long-running handlers (for example video processing) can allow more time: set it on the `AddServiceActivator` consumer options (`IAmConsumerOptions.ShutdownTimeout`), via `DispatchBuilder.Build(shutdownTimeout: …)`, or the `Dispatcher` constructor's new trailing optional `shutdownTimeout` parameter. **Source break for external implementers:** `IAmConsumerOptions` gains a `ShutdownTimeout` member with no default interface body (the assembly targets `netstandard2.0`); the in-tree `ConsumersOptions` implements it defaulting to 10s. `IAmADispatchBuilder.Build` and the `Dispatcher` constructor gain trailing optional parameters (source-compatible for callers; binary-breaking, so recompile against this version). `Dispatcher` also now implements `IAsyncDisposable` alongside `IDisposable` (an additive, non-breaking change): on the graceful path a host that tears its provider down through `DisposeAsync` — which Microsoft.Extensions.DependencyInjection honours in preference to `IDisposable` — **awaits** the shutdown drain instead of blocking a thread on it, and async-disposes any owned factory that is itself `IAsyncDisposable`. `Dispose()` is retained as the synchronous fallback (MS DI throws if it must synchronously dispose an async-only service); both paths share one run-at-most-once guard, so disposing either way, or both, drains and disposes exactly once.
* **`OutboxProducerMediator.Dispose` no longer propagates a broker-close failure.** `Dispose(bool)` now wraps `_producerRegistry.CloseAll()` in a try/catch that logs and swallows the failure, so a broker that throws while closing its producers no longer escapes from `Dispose` — and, more importantly, no longer skips the subsequent factory disposals (the per-resolution scope drain this owner exists to perform). Previously a `CloseAll()` throw propagated out of `Dispose` and left those factories undisposed.
* **Release each resolution's lease when finished; over-releasing a lease is now a safe no-op.** Release keys on the `Lease<T>` (the resolution), not the instance, so `Release(lease)` disposes exactly that resolution's scope. This designs out the former shared-instance hazard: a mapper or transform registered in the container as a `Singleton` (or otherwise handing back one shared instance) under the default `Transient` `MapperLifetime`/`TransformerLifetime` no longer risks one resolution's release disposing another still-live resolution's scope, and a spurious second `Release` of the same lease — or a `Release(null)`, since `Get`/`Create` return `Lease<T>?` — is an idempotent no-op rather than a pop of another resolution's scope or a `NullReferenceException`. All in-tree call sites already release exactly once; this only concerns code that resolves and releases mappers/transforms directly.
* **`HasPipeline` now answers by resolving the mapper *type*, not by creating a probe instance** (see `ResolveMapperInfo` above). This changes the exception on one narrow misconfiguration — a mapper *type* registered but whose *instance* cannot be built (a `SimpleMessageMapperFactory` `Func` that returns `null`, or a `Register` without a matching container registration). The type is kept in sync with the container on the `AddBrighter` path, so this does not arise there.
  * **Send path** (`OutboxProducerMediator.MapMessage` / `MapMessageAsync`): `HasPipeline` now returns `true` for that type, so building the pipeline fails and throws `ConfigurationException` (with the underlying `InvalidOperationException` as its inner exception) rather than the previous `ArgumentOutOfRangeException("No message mapper defined for request")`.
  * **Reply path** (`CreateRequestFromMessage`): where an unresolvable *async* mapper type previously made the async probe `false` and the reply **fell through to the sync pipeline**, it now throws `ConfigurationException` on the async attempt instead of silently using the sync pipeline. The normal fall-through — no async mapper registered at all — is unchanged.
* **A failed release from a transform scope or pipeline `Dispose`/`DisposeAsync` now surfaces as an `AggregateException`.** The transform lifetime scope drains every tracked transform in one pass and collects any release failures, throwing them together as an `AggregateException` (a single failure is wrapped too); previously the first failure propagated unwrapped. If both a transform-scope disposal and the pipeline's mapper release throw, both are surfaced (the mapper-release failure no longer masks the transform one). Every in-tree caller logs and swallows release failures, so this has no functional impact on the standard paths; it only matters to code that disposes a `TransformPipeline`/`TransformLifetimeScope` directly and catches a specific exception type — catch `AggregateException` (or its `InnerExceptions`).
* **`Dispatcher` and `OutboxProducerMediator` dispose the mapper registry and transform factories only when they own them.** Both types became `IDisposable`/gained disposal of the runtime mapper/transform graph in this release. Ownership is now explicit: the constructors (and `DispatchBuilder.Build`) take `ownsRegistry`/`ownsTransformerFactories`, both **defaulting to `false`**. The DI paths (`AddServiceActivator`/`AddBrighter`) new up a graph solely for their owner and pass `true`, so container teardown disposes it as before. On the **manual-wiring** path — where a `MessageMapperRegistry` is commonly shared between a `Dispatcher` and a `CommandProcessor`'s external bus — the default `false` means neither disposes the shared registry out from under the other; if you construct these directly and want them to own (dispose) the graph, pass `ownsRegistry: true, ownsTransformerFactories: true`.

## Release 10.0.0

With V10 we have made a number of significant changes to Brighter. There are breaking changes that you will need to be aware of. However, most of the changes required are straightforward to make. A summary of the most important changes:

* **Cloud Events**:We now have full support for Cloud Events headers; you can set values in your Publication and have them reflected on messages.
* **Open Telemetry**: We now support the OpenTelemetry Semantic Conventions for Messaging. This will mean that you have different traces to V9, where the OTel conventions were Brighter's own.
* **Default Message Mappers**: There is no need to provide a mapper if your goal is to serialize your body as JSON. You can use a default mapper. You can create your own default mapper for other formats. You only need explicit mappers for complex transform pipelines.
* **Dynamic Message Deserialization**: Previously we required that you used a DataType Channel (one type per channel). Whilst we recommend this, and it remains the default you can now provide a callback to determine the message type from the message itself, such as via the Cloud Events type, before deserializing.
* **Agreement Dispatcher**: We now support a callback for determining the handler to dispatch a Command or Event to. Previously we matched request and handler based on the request type. Whilst this is still a default, you can now add a callback to dynamically determine the handler from the request and the request context.
* **Request Context Improvements**: You can now inject the RequestContext more easily into a pipeline. The RequestContext now supports the `OriginatingMessage` for subscriptions to queues or streams.
* **Reactor and Proactor**: We have made considerable under-the-hood improvements to synchronous and asynchronous message pumps in your consumer. The asynchronous pipeline is now end-to-end.
* **Scheduled Requests/Messaging**: We now support integration with schedulers, like Quartz.NET, Hangfire, or AWS Scheduler. This can be used with requests or messages. We use this support internally, if available, to allow "Requeue with Delay" where the messaging protocol does not natively support it.
* **Nullability**: We have enabled nullable reference types.
* **Simplified Configuration**: We have tried to make configuration simpler, including renaming obscure methods. This needs more work in future releases.

### Cloud Events Support

Full Cloud Events specification support has been added across all supported messaging protocols:

* **Publication**: Support for Cloud Events on the Publication with configurable additional properties
* **Message Mapper**: The Publication is passed into the message mapper, allowing you to read CloudEvents properties
* **Default Mappers**: The default `JsonMessageMapper` writes `binary` Cloud Event headers, and the default `CloudEventJsonMessageMapper` writes `structured` Cloud Events Headers.
* **Transport Integration**: We support writing and reading CloudEvents headers across all supported messaging protocols.
* **Message Routing**: Use Cloud Events type for message deserialization (see below).

### OpenTelemetry Integration

Comprehensive OpenTelemetry support has been added throughout Brighter. We support the [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/concepts/semantic-conventions/):

* **Span Attributes**: OpenTelemetry across all Brighter request handler pipelines.
* **Transport Tracing**: Automatic trace propagation across message boundaries, with support for W3C TraceContext.
* **Outbox Tracing**: Distributed tracing for all outbox implementations.
* **Inbox Tracing**: OpenTelemetry support for all inbox implementations.
* **Claim Check Tracing**: Tracing support for claim check pattern and luggage stores.
* **Instrumentation Control**: Configurable instrumentation options across all tracer operations.

OpenTelemetry integration enables end-to-end distributed tracing across message boundaries, making it easier to diagnose performance issues and understand message flow in distributed systems.

### Default Message Mappers

We no longer require that you implement `IAmAMessageMapper` for each Producer and Consumer message pipeline.

* **Built-in Fallback**: Brighter will attempt to use appropriate default mappers when no explicit mapper is registered
* **JsonMapper**: Automatically handles JSON serialization/deserialization for messages with `binary` CloudEvents support
* **CloudEventsMapper**: Automatically handles JSON serialization/deserialization for messages with `structured` CloudEvents support

You only need to create custom message mappers when you require explicit transforms or have specific serialization requirements. The default mappers can also serve as templates for custom implementations.

```csharp
 services.AddBrighter(options =>
  {
      ... 
  })
  .AddProducers((configure) =>
  {
    ...
  })
  //This is the default mapper type, so you can omit it, but we are explicit for this note to show how to register your own default
  .AutoFromAssemblies([typeof(TaskCreated).Assembly], defaultMessageMapper: typeof(JsonMessageMapper<>), asyncDefaultMessageMapper: typeof(JsonMessageMapper<>));
```

### Dynamic Message Deserialization

Brighter now supports multiple message types on the same channel through dynamic request type resolution. This enables content-based deserialization where the message type is determined at runtime from metadata rather than compile-time generic parameters. We still support the older DataType channel approach. As routing to a handler is based on type, this will decide the handler that receives this message (although see also Agreement Dispatcher).

```csharp
new KafkaSubscription(
    new SubscriptionName("paramore.example.taskstate"),
    channelName: new ChannelName("task.state"),
    routingKey:new RoutingKey("task.update"),
    getRequestType: message => message switch
    {
        var m when m.Header.Type == new CloudEventsType("io.goparamore.task.created") => typeof(TaskCreated),
        var m when m.Header.Type == new CloudEventsType("io.goparamore.task.updated") => typeof(TaskUpdated),
        _ => throw new ArgumentException($"No type mapping found for message with type {message.Header.Type}", nameof(message)),
    },
    groupId: "kafka-TaskReceiverConsole-Sample",
    timeOut: TimeSpan.FromMilliseconds(100),
    offsetDefault: AutoOffsetReset.Earliest,
    commitBatchSize: 5,
    sweepUncommittedOffsetsInterval: TimeSpan.FromMilliseconds(10000),
    messagePumpType: MessagePumpType.Reactor)
```

### Agreement Dispatcher

Brighter now allows you to determine the handler that will be used for a given request dynamically. Whilst we still support the old 1-2-1 mapping, this method can be used for an [Agreement Dispatcher](https://martinfowler.com/eaaDev/AgreementDispatcher.html) where we determine the handler type at runtime not build time.

Note that we do not support auto registration of routes using `AutoFromAssemblies`, you must explicitly add them to the registry. You MUST provide both the mapping function for the agreement dispatcher and a list of possible handler types.

```csharp
registry.RegisterAsync<MyCommand>(((request, context) =>
{
    var myCommand = request as MyCommand;
    if (myCommand?.Value == "first")
        return [typeof(MyImplicitHandlerAsync)];
    
    return [typeof(MyCommandHandlerAsync)];
}), 
    [typeof(MyImplicitHandlerAsync), typeof(MyCommandHandlerAsync)]
);

```

### Request Context Improvements

The CommandProcessor now lets you set the `RequestContext` explicitly when calling `Send`, `Publish`, `DepositPost` etc. This allows you to set properties of the `RequestContext` for transmission to the `RequestHandler` instead of having a new context created by the `RequestContextFactory` for that pipeline.

For consumers, we now add a property to the `RequestContext` that provides the `OriginatingMessage` which allows you to examine properties of the message that was received.

**Breaking Change**: The `IRequestContext` interface has been enhanced to support:

* **Partition Key**: Set message partition keys dynamically 
* **Custom Headers**: Add headers via request context
* **Resilience Context**: Integration with Polly Resilience Pipeline

```csharp
// Set partition key and custom headers via request context
public class MyHandler : RequestHandler<MyCommand>
{
    public override MyCommand Handle(MyCommand command)
    {
        Context.Span.SetAttribute("custom.header", "value");
        Context.PartitionKey = command.TenantId;
        
        return base.Handle(command);
    }
}
```

### Proactor and Reactor

We have made significant changes to Brighter's concurrency models. We now use terminology that derives from the Reactor and Proactor patterns, replacing the previous "blocking" and "non-blocking" terminology with clearer semantic meaning.

* **Reactor Model**: Uses blocking I/O for optimal performance in single-threaded scenarios
* **Proactor Model**: Uses non-blocking I/O for improved throughput when sharing resources across multiple threads

We now have a complete async pipeline for the Proactor and a complete sync pipeline for the Reactor, whereas previously only dispatch was async in the Proactor pipeline. Our synchronization context has been updated to use Stephen Cleary's AsyncEx approach instead of Stephen Toub's original article, providing better error handling and more reliable continuation management.

**Breaking Change**: The `runAsync` flag on Subscription has been renamed to `MessagePumpType` for clarity. Update your subscriptions:

```csharp
// V9
var subscription = new Subscription(typeof(MyHandler), isAsync: true);

// V10
var subscription = new Subscription(typeof(MyHandler), messagePumpType: MessagePumpType.Proactor);
```

### Scheduled Requests/Messaging

The CommandProcessor now supports using a scheduler to delay sending, publishing or posting messages. We support a range of schedulers, such as Quartz.NET, Hangfire and AWS Scheduler.

```csharp

 _commandProcessor.Send(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);

```

```csharp
var schedulerFactory = SchedulerBuilder.Create(new NameValueCollection())
    .UseDefaultThreadPool(x => x.MaxConcurrency = 5)
    .UseJobFactory<BrighterResolver>()
    .Build();

var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
scheduler.Start().GetAwaiter().GetResult();

_scheduler = new QuartzSchedulerFactory(scheduler);

```

### InMemory Options

Brighter has a range of in-memory types that can replace key dependencies such as producers, consumers, schedulers, outboxes and inboxes. Whilst we do not recommend this for production usage, they are robust and can be used for local development and testing.

```csharp
UseScheduler(new InMemorySchedulerFactory())

```

### Nullable Reference Types

**Breaking Change**: Nullable reference types are now enabled across all projects. You may need to update your code to handle nullable warnings:


### Simplified configuration

**Breaking Change**: Builder methods have been renamed for clarity. We used names that historically had value, but are no longer meaningful to most users of Brighter, so we have reverted to a simpler naming convention:

```csharp
// V9
services.AddBrighter()
    .UseExternalBus(...)
    .AddServiceActivator(...);

// V10  
services.AddBrighter()
    .AddProducers(...)
    .AddConsumers(...);
```

**Connection Provider Registration**: Improved registration of connection and transaction provider interfaces

### Polly Resilience Pipeline

**Breaking Change**: New resilience pipeline attributes replace legacy timeout policies 

```csharp
// V9 - Deprecated
[TimeoutPolicy(milliseconds: 5000, step: 1)]
public override MyResult Handle(MyCommand command) { }

// V10 - New approach
[UseResiliencePipeline(policy: "MyPipeline", step: 1)]
public override MyResult Handle(MyCommand command) { }
```

The `TimeoutPolicyAttribute` is now marked as obsolete.

The new approach provides:

* **Full Polly v8 Support**: Access to all Polly resilience strategies
* **CancellationToken Integration**: Proper cancellation token flow from resilience pipelines 
* **Enhanced Context**: Request context integration with Polly's resilience context

### AWS SDK v4 Support

Complete AWS SDK v4 support has been added:

* **SNS/SQS**: Standard and FIFO queue support
* **DynamoDB**: Inbox, Outbox, and Distributed Lock implementations  
* **S3**: Luggage store for claim check pattern

You can now use the latest AWS SDK v4 while maintaining backwards compatibility with v3.

### Transport Improvements

**PostgreSQL Message Broker**: Added support for using PostgreSQL as a message broker ([PR #3612](https://github.com/BrighterCommand/Brighter/pull/3612)), enabling pub/sub messaging patterns directly with PostgreSQL's LISTEN/NOTIFY functionality.

**RabbitMQ Enhancements**:

* **Quorum Queues**: Support for RabbitMQ quorum queues for improved consistency and availability.
* **RabbitMQ 7.x**: We have support for the older RabbitMQ v6 to support synchronous RMQ pipelines and support for the asynchronous pipelines of RabbitMQ client library v7
* **Connection Stability**: Improved connection handling and error recovery.

```csharp
// Configure Quorum queues
var subscription = new RmqSubscription<MyMessage>(
    queueType: QueueType.Quorum,
    isDurable: true,         // Required for quorum queues
    highAvailability: false  // Must be false for quorum queues
);
```

**Kafka Improvements**:

* **Configuration Callback**: Enhanced configuration support through KafkaSubscription callback 
* **Updated Defaults**: Improved default configuration values for better out-of-the-box experience

**AWS Improvements**:

* **SQS Publication Enhancement**: Allow publishing directly to an SQS queue without SNS
* **S3 Claim-Check**: Fixed AWS S3 claim-check implementation

### Sweeper Circuit Breaking

Topic-level circuit breaking has been added to prevent cascade failures:

* **Failure Tracking**: Automatic tracking of dispatch failures per topic
* **Configurable Thresholds**: Set failure thresholds and cooldown periods  
* **Automatic Recovery**: Topics automatically recover after cooldown period
* **Bulk Dispatch Support**: Circuit breaking now properly supports bulk dispatch operations 
* **Per-Transport Integration**: Circuit breaking is integrated with MongoDB 

The bulk dispatch implementation brings circuit breaking inline with single dispatch, allowing individual batches to be retried and providing better control over transport-specific batching behavior.

### Performance Improvements

* **GUID v7**: Support for GUID v7 on .NET 9+ for better database performance
* **Sealed Classes**: Internal classes sealed to reduce virtual dispatch overhead
* **Optimized Collections**: Reduced dictionary lookups and improved collection usage
* **Memory Optimization**: Better memory usage in SQL data readers and stream handling 
* **Source-Generated Logging**: Migrated to source-generated logging for superior performance and stronger typing 
* **Reduced Allocations**: Optimized string comparisons and reduced unnecessary allocations 

GUID v7 provides better database clustering and performance characteristics compared to GUID v4, especially beneficial for high-throughput scenarios with database-backed outboxes and inboxes.

### Test Infrastructure and Developer Experience

**Enhanced Testing**:

* **Colorful Test Output**: Improved test runner with colorful output and GitHub Actions logger support 
* **Better Test Infrastructure**: Enhanced test reliability and coverage across all transport implementations


### Command Processor Dispatching Strategy

Enhanced command processor with support for content-based routing using specification patterns ([PR #3652](https://github.com/BrighterCommand/Brighter/pull/3652)). This enables routing requests based on content rather than just type, supporting more sophisticated message routing scenarios.

### Additional Bug Fixes and Improvements

* **Outbox Sweeper**: Fixed NullReference exception in outbox sweeper ([PR #3683](https://github.com/BrighterCommand/Brighter/pull/3683))
* **ASB Defer Exception**: Fixed issue where Azure Service Bus defer exception caused attempted reject then complete ([PR #3619](https://github.com/BrighterCommand/Brighter/pull/3619))
* **Scheduler Tests**: Fixed scheduler tests for long scheduling windows with proper EntryTimeToLive configuration ([PR #3582](https://github.com/BrighterCommand/Brighter/pull/3582))
* **Quorum Queue Tests**: Enhanced quorum queue testing to properly validate queue creation ([PR #3642](https://github.com/BrighterCommand/Brighter/pull/3642))

### Breaking Changes Summary

For users upgrading from V9 to V10:

1. **Update Subscription Configuration**:
   * Replace `isAsync/runAsync` with `messagePumpType` with options of `MessagePumpType.Proactor` or `MessagePumpType.Reactor`
   * Replace `timeoutInMilliseconds` with `timeOut` which is now a `TimeSpan` type
   * Replace `requeueDelayInMs` with `requeueDelay` which is now a `TimeSpan` type

2. **Handle Nullable Reference Types**:
   * Address nullable warnings in your handlers and commands

3. **Update Builder Calls**:
   * Replace messaging builder methods with `AddProducers()`/`AddConsumers()`

4. **Migrate Policies**:
   * Replace `[TimeoutPolicy]` with `[UseResiliencePipeline]` and Polly configuration ([TimeoutPolicy is deprecated in V10 and will be removed in V11])
   * Replace `[UsePolicy]` with `[UseResiliencePipeline]`

5. **Message ID Changes**:
   * Message and Correlation IDs are now strings (defaulting to GUID strings)

6. **Generic Message Pumps**:
   * Remove generic type parameters if directly instantiating message pumps

7. **Test Framework Changes**:
   * Replace Fluent Assertions with xUnit assertions in your test projects

8. **Default Message Mappers**:
   * Review your message mappers - many can now be removed in favor of default implementations

### Database Schema Updates

If you use Inbox/Outbox patterns, you may need to update your database schemas. New DDL scripts are available in the repository for each supported database provider.

### Migration Guide

For detailed migration guidance, see the [V10 Migration Guide](https://brightercommand.github.io/Brighter/migration/v10) in our documentation.

## Release 9.X

## Binary Serialization Fixes

* MessageBody  nows store the character encoding type (defaults to UTF8) to allow correct conversion back to a string when using Value property
* Use a CharacterEncoding.Raw for binary content (will be a Base64 string for Value)
* Kafka transport payload is now byte[] and not string. This prevents corruption of Kafka 'header' of 5 bytes to store schema registry when used with schema registry support
* DynamoDb now uses a byte[] and not a string for the message body to prevent lossy conversions
* ContentType on Header is set from Body, if not set on the Header

## Kafka Fixes

* Kafka now serliases the ReplyTo Header correctly

## New Transforms

* Compression Transform now available to compress messages using Gzip (or Brotli or Deflate on .NET 6 or 7)

## Release 9.3.6

* Set correct partition key (kafka key) for Kafka messages  
* Add default option for Header bags serialisation
* Set correct span status for Send and SendAsync @easyfy-fredrik
* Note that this version pulls v7 of System.Text.Json which has breaking changes for users of System.Text.Json, see <https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-7/#breaking-changes>

## Release 9.3.0

* Bug with DynamoDb Outbox and the Outbox Sweeper fixed. The Sweeper required a topic argument supplied by a dictionary of args
  * Required adding a Dictionary<string, object> to various interfaces, which defaults to null, hence the minor version bump as these interfaces have new capabilities
* Internal change to move outstanding message box to a semaphore slim over a mutex as thread-safe. Not strictly neededm, but follows our policy of moving to semaphore slim
* Changes to the DynamoDb Outbox implementation as Outstanding Message check was not behaving as expected
* The interfaces around Outbox configuration will likely change in v10 to avoid current split and need to configure on both publication and outbox

## Release 9.1.20

- Bug with Kafka Consumer failing to commit offsets fixed. Caused by Monitor being used for a lock on one thread and released on another, which does not work. Replaced with SemaphoreSlim.
* Behavior of Kafka Consumer offset sweep changed. It now runs every x seconds, and not every x seconds since a flush. This will cause it to run more frequently, but it is easier to reason about.

## Release 9.1.14

* Fixed missing negation operator when checking for AWS resources

## Release 9.1.14

* Renamed MessageStore to Outbox and CommandStore to Inbox for clarity with well-known pattern names outside this team
  * Impact is wide, namespaces, class names and project names, so this is a ***BREAKING CHANGE***
  * Mostly you can search and replace to fix
* Added support for a global inbox via a UseInbox configuration parameter to the Command Processor
  * Will insert an Inbox in all pipelines
  * Can be overridden by a NoGlobalInbox attribute for don't add to pipeline, or an alternative UseInbox attribute to vary config
* The goal here is to be clearer than our own internal names, which don't help folks who were not part of this team
* The Outbox now fills up if a producer fails to send. You can set an upper limit on your producer, which is the maximum outstanding messages that you want in the Outbox before we throw an exception. This is not the same as Outbox size limits or sweeper, which is separate and mainly intended if you don't want the Outbox limit to fail-fast on hitting a limit but keep accumulating results  
* Added caching of attributes on target handlers in the pipeline build step
  * This means we don't do reflection every time we build the pipeline for a request
  * We do still always call the handler factory to instantiate as we don't own handler lifetime, implementer does
  * We added a method to clear the pipeline cache, particularly for testing where you want to test configuration scenarios
* Added ability to persist RabbitMQ messages
* Added subscription to blocked/unblocked RMQ channel events. A warning log is created when a channel becomes blocked and an info log is generated when the channel becomes unblocked.
* Improved the Kafka Client. It now uses the publisher/creator model to ensure that a message is in Brighter format i.e. headers as well as body; updated configuration values; generally improved reliability. This is a breaking change with previous versions of the Kafka client.
* The class BrighterMessaging now only has a default constructor and now has setters on properties. Use the initializer syntax instead - new BrighterMessage{} to avoid having redundant constructor arguments.
* Changes to how we configure transports - renaming classes and extending their functionality
  * Connection is renamed to Subscription
  * Added a matching Publication for producers
  * Base class includes the attributes that Brighter Core (Brighter & ServiceActivator) need
  * Derived classes contain transport specific details
  * On SQSConnection, renamed VisibilityTimeout to LockTimeout to more generically describe its purpose separated from GatewayConfiguration, that now has a marker interface, used to connect to the Gateway and not about how we publish or subscribe
  * We now have the option to declare infrastructure separately and Validate or Assume it exists, still have an option to Create which is the default
  * We think it will be most useful for environments like AWS where there is a price to checking (HTTP call, and often looping through results)  
  * Added support for a range of parameters that we did not have before such as dead letter queues, security etc via these platform specific configuration files  
* Provided a short form of the BrighterMessaging constructor, that queries object provided for async versions of interfaces
* Changed IsAsync to RunAsync on a Subscription for clarity
* Supports an async pipeline: callbacks should happen on the same thread as the handler (and the pump), avoiding thread pool threads
* Fixed issue in SQlite with SQL to mark a message as dispatched

## Release 8.1.1399

* Update nuget libs
* RabbitMQ 6.*
* Fix correlationid no been sent correctly when using SqlCommandStore

## Release 8.1.1036

* Fixes issue when a rabbitmq connection is dropped it sometimes ends up with 2 connections and then does not dispose the ghost connection.
* Fix for System.InvalidOperationException: You cannot enqueue more items than the buffer length #846
* fix for Suppress and log BrokerUnreachableException during ResetConnection #502

## Release 8.0.*

* Added SourceLink debugging and are shipping .pdb files in the nuget package.
* Strong Name in line with Open Source guidance <https://docs.microsoft.com/en-us/dotnet/standard/library-guidance/strong-naming>. Where libraries we rely on are not strong named we don't strong name our code.
* Removed `IAmAPolicyRegistry` and replaced it with `IPolicyRegistry<string>` from Polly, it is a drop in replacement but in a the Polly namespace.
* Removed our `PolicyRegistry` and now use the `PolicyRegistry` from Polly, it is a drop in replacement but in a the Polly namespace.
* Support for Feature Switches on handlers
* Switch Command Sourcing Handler to using an Exists method when checking for duplicate messages
* Rewritten AWS SQS + SNS transport
* Support for DynamoDB Message and Command Stores (Jonny Olliff-Lee @DevJonny)
* Added a Call() method to CommandProcessor to support Request-Reply
* Add a context field to the command store, to allow identification of a context, and share a table across multiple handlers. Note that this is a breaking schema change for users of the command store
* Command Sourcing handler now writes to store only once the handler has successfully completed
* Renamed InputChannelFactory to ChannelFactory as we don't have an OutputChannelFactory any more (and not for some time)
* Channel buffer now only source for message pump, populated via consumer when empty
* Consumers now return an array of messages, default size of 1 but can be up to 10
* Switch RMQ Consumers back to basic consume to support batch delivery
* RMQ now supports batch sizes of up to 10 for consuming messages
* SNS+SQS now supports batch sizes of up to 10 for consuming messages
* Added support for the Outbox pattern via DepositPost and ClearPostBox
* Fixed <https://github.com/BrighterCommand/Brighter/issues/156> to allow different exchange types to be set (was broken by support of delayed exchange)
  
## Release 7.4.0

* Updated to signed version of Polly, works with netcore2.1.
* Fix for Sql CommandStore.
* Fixes to make flaky tests stable.
  
## Release 7.3.0

* Added beta Support for a Redis transport
* Support for Binding a channel to multiple topics
* RMQ Transport: Fixed handling of socket timeout where node we are connected to (not master) partitions from cluster and is paused under the pause minority strategy. Now resets connection successfully.
* RMQ Transport: Fixed issue with OperationInterrupted exception when master node partitions and we are connected to it
* Overall improved reliability of Brighter RMQ transport when connecting to a cluster that experiences a partition
* Fixed an issue where multiple performers did not have distinct names and so could not be tracked
* RMQ changed from push rabbit consumer to just simple pull based.

## Release 7.2.0

* Support for PostgreSql Message Store (Tarun Pothulapati @Pothulapati)
* Support for MySql Message and Command Stores (Derek Comartin @dcomartin)
* Support for Kafka Messaging Gateway - Beta (Wayne Hunsley @whunsley)
* Support for MSSql Messaging Gateway - Beta (Fred Hoogduin @Red-F)

## Release 7.1.0

* Fixes issue with high CPU when failing to connect to RabbitMQ.
* Fixes missing High Availability setting, had to make changes to IAmAChannelFactory.

## Release 7.0.137 - 7.0.143

* Support for .NET Core (NETSTANDARD 1.5)

### **Breaking Changes**

* Configuration no longer supports XML based config sections. We use data structures instead, and expect you to configure mostly in code, initializing those data structures from your config system of choice yourself. We recommend following 12-Factor Apps guidelines and preferring environment variables for items that vary by environment over XML or JSON based configuration files. (We may consider providing config sections in Contrib again, please feedback if this is a critical issue for you. PRs welcome.)
* Dropped CommandProcessor from namespaces and folder names, to shorten, and remove semantic issue that it is not just a Command Processor
* Changed namespaces and folders to be CamelCase
* As a result, your using statements will need revision with this release
* Some namespaces i.e Paramore.Brighter.Policy changed to avoid clashes now CamelCase (has become Paramore.Brighter.Policies)

## Release 6.1.0

* Support for binary message payloads i.e. not just text/plain for JSON or XML. Current support is modelled around use of protobuf over RMQ

## Release 6.0.28

Fix issue with encoding of non-string types and transmission of correlation id <https://github.com/BrighterCommand/Brighter/pull/180>

## Release 6.0.6

- Increase logging level when we stop reading from a queue that cannot be readhttps://github.com/BrighterCommand/Brighter/pull/179
* Peformance issue caused by creation of a logger per requesthandler instance. The logger is now static, but is initialized lazily and can be overridden for TDD or legacy compatibility

## Release 6.0.0

**Breaking Changes**
* CommandProcessorBuilder no longer takes .Logger(logger)
* In the abstract RequestHandler `logger` is now `Logger`
* `RequestLogging` has moved namespace to `paramore.brighter.commandprocessor.logging.Attributes`

### **Bug fixes**:

* Fixed issue #132: concurrent usages of the RabbitMQ messaging gateway would sometimes throw an exception
* Fixed issue #134: We no longer use async/await in the command processor. This caused issues with ASP.NET synchronization contexts, resulting in a deadlock when waiting on the thread that was also being used to run the completion. See <http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html> We wil revisit async when we write *Async versions of the CommandProcesor APIs suitable for using in hosts that can run async code without deadlocking their synchronization context.
* Fixed issue 110: Where we want to log we have two constructors. A constructor that directly takes an iLog that you provide either directly or via your ioC container; a constructor that defaults that to LogProvider.GetCurrentClassLogger
 	* In Production code you should set up your log provider and use the constructors that do not take an ILog reference.
 	* In Test code you should inject the ILog using a fake logger. We don't recommend testing log output, its an implementation detail, unless its an important part of your acceptance criteria for that behavior.
 	* This means that your production code should not need to take a direct dependency on Paramore's ILog implementation.
 	* This is a BREAKING CHANGE because we remove the ability to inject the constructor via the *Builder objects, so as to remove the temptation to do that when you should rely on the LibLog framework to wrap your current logger.

### **Features:**
* Huge feature, Async; added support for SendAsync and PublishAsync to an IHandleRequestsAsync pipeline.
* Basic support for publishing to Azure Service Bus with `paramore.brighter.commandprocessor.messaginggateway.azureservicebus`.

## Release 5

### **Bug Fixes:**

* #100 `CommandProcessor.Post` fails with Object reference not set to an instance of an object.
* Fix RequeueMessage exhaustion to log ERROR.
* #101 Updated `Requeue` method to send a message to a specific queue as opposed to a topic.
* Added a message store write timeout and message gateway timeout on a post; perviously we wait indefinitely (bad Brighter team, no biscuit).
* Replace `Successor` write-only property with `SetSuccessor` method.
* Message Viewer, fixed startup issues.
* Removed a few unused interfaces.
* Correct exceptions namespace to actions.

### **Features:**

* A connection can now be flagged as isDurable in the configuration. Choosing isDurable when using RMQ as the broker will create a durable channel (i.e. does not die if no one is consuming it, and thus continues to subscribe to messages that match it's topic). We think there are sufficient trade-offs with a message store that allows replay to make this setting false by default, but have configured to allow users to make this choice dependent on the characteristics of their consumers (i.e. sufficiently intermittent that messages would be lost).
* #92 Added [Event Store](https://geteventstore.com/ "Event Store") Message Store implementation
* #30 Changed RabbitMQ Messaging Gateway to support multiple performers per connection, fixing the pipeline errors from RabbitMQ Client
* Added a UseCommandSourcing attribute that stores commands to a command store. This is the Event Sourcing paradigm described by Martin Fowler in <http://martinfowler.com/eaaDev/EventSourcing.html> The term Command Sourcing refers to the fact that as described the pattern stores commands (instructions to change state) not events (the results of applying those commands).
 	* This may result in a breaking change that the Id on IRequest requires a setter to allow it to be deserialized
* Added MS SQL Command Store implementation
* Added monitoring attribute, which fires message onto control bus
* Cleaning up code so working with dnx and Portable will be easier
* Message Viewer, Add paging
* Update Code of Conduct to Contributor Covenant 1.1.0
* Add DDL scripts to help create SQL based schemes

**Remove and Depreciated:**

* Flag the method `Repost` on `IAmACommandProcessor` as obsolete, We will probably drop this in the next release. We suggest that you use the message store directly to retrieve a message and then call Post.
* Dropped support for RavenDb as a message store, we feel EventStore covers this scenario better where non-relational stores are an option
* Removed release branch. We just tag a release on master now, so this only existed to support an older version of the library that was pre the tagging strategy. Removed now as confusing to new users of the library.

## Release 4.0.215

1. Fixed an issue where you could not have multiple UsePolicy or FallbackPolicy attributes on a single handler.#
2. We pool connections now, to prevent clients with large number of channels overwhelming servers.
3. Add concept of delayed (deferred) message sending.
4. Implement delayed requeuing using gateway support (when supported).
5. Delayed message provider support for RabbitMQ using [rabbitmq_delayed_message_exchange plugin (3.5+)](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/).
6. Renamed RequeueException to DeferMessageAction and moved it into the command processor project.
7. Fixed and issues with unhandled exceptions from handlers when an event is published not been logged correctly
8. The first early version of a Message Store Viewer has been release as a zip file download

## Release 3.0.129

1. We now support a Fallback method on IHandleRequests<TRequest> which is intended to be used for compensating or emergency action when a Handle method cannot be executed. The [FallbackPolicy] attribute supports the pipeline calling the Fallback method for you, in the event of either any exception bubbling into the handler, or a broken circuit exception bubbling into the handler.
2. Fix issue with RabbitMQ consumers running on a High Availability cluster not cancelling properly after cluster failover.
3. Fixed bug with config section duplication <https://github.com/BrighterCommand/Brighter/issues/52>
4. Added functionality so after a specified number of unacceptable message (unable to read from queue or map message) a connection is shutdown, by default unacceptable message are acked and dropped. <https://github.com/BrighterCommand/Brighter/issues/51>
5. Move RequeueException to paramore.brighter.commandprocessor.exceptions (breaking change).

## Release 3

1. Refactored **IAmAMessagingGateway** into a **IAmAMessageConsumer** and **IAmAMessageProducer** to support differing approaches to producing and consuming messages for a particular flavour of Message-Oriented-Middleware. *These changes are a breaking binary change for users of earlier versions.*
 1. NOTE: IF YOU USE TASK QUEUES PLEASE SAVE YOUR SERVICEACTIVATORCONNECTIONS IN YOUR APP.CONFIG AS THE V2.0.1 BRIGHTER.SERVICEACTIVATOR UNINSTALL WILL DELETE THEM (FIXED FOR V3)
2. Created an **IAmAChannel** abstraction to allow differing Application Layer dependencies for the Work Queue
2. Upgraded Packages we depend on, including RabbitMQ. *There is still an issue with our having a hard dependency on a RabbitMQ client that might vary from your RabbitMQ client version, but as a NuGet package there are few workarounds. We suggest building from source where this issue is problematic, for now.*
3. Significant stability improvements on the RabbitMQ client
 1. Fixed issues around re-connection of the client leading to lost messages.
 2. Fixed issues when explicitly closing and re-opening connections
 3. Provided support for a **RequeueException** to requeue messages that are 'out-of-time' to help with resequencing.
 1. We now dispose of channels aggressively on closure, instead of waiting for garbage collection
2. Moved from [Common.Logging](https://github.com/net-commons/common-logging) to [LibLog](https://github.com/damianh/LibLog)  *These changes are a breaking binary change for users of earlier versions.*
3. We now call Release for all **RequestHandler<>** derived handlers that we construct from an **IAmAHandlerFactory**, not just those that implement IDisposable.
4. Note that the RestMS server is **NOT** ready for production usage. It's primary value, as of today, is an alternative to RabbitMQ for design purposes. It is hoped to produce a stable version for use as a ControlBus in a future release.
