# Bugfix: ASB channel factory does not provision the topic subscription eagerly, so the first sent message is lost

**Linked Issue**: #4309
**Status**: Fixed

## Symptom

A caller builds an Azure Service Bus producer, builds a channel through `AzureServiceBusChannelFactory`
(topic mode, `MakeChannels = OnMissingChannel.Create`, the default), sends, then receives. Nothing is
received and nothing fails. When the send happens, the topic has no subscription, and an ASB topic with no
subscriptions discards what it is sent. The subscription only appears on the consumer's first
Receive/Acknowledge/Nack/Reject/Purge, which is too late.

The generated conformance tests exposed this (26 failures, down to 2 once the harness provisioned the
subscription). The harness workaround is on master as commit `0e337bf1a`
(`tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/AzureServiceBusMessageGatewayProvider.cs:183-197`).
The hand-written tests also work around it by pre-creating the subscription
(`When_posting_a_message_via_the_producer.cs:65`, before `CreateSyncChannel` at `:71-72`). The gateway
still has the defect, so real callers lose the first message.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannelFactory.cs`
  - `:54-67` `CreateSyncChannel`: `GetAndCheckSubscription`, then `_azureServiceBusConsumerFactory.Create(...)`,
    then `new Channel(...)`. Nothing is provisioned.
  - `:75-88` `CreateAsyncChannel`: same shape (`CreateAsync`, `new ChannelAsync`).
  - `:97-99` `CreateAsyncChannelAsync` is just `Task.FromResult(CreateAsyncChannel(subscription))`.
  - `:101-114` `GetAndCheckSubscription` checks only the subscription type and that `TimeOut` is at least
    400 ms. `MakeChannels` is never read in this file.
  - `:37`, `:43-46`: the only dependency is the concrete `AzureServiceBusConsumerFactory`. The factory has
    no `IAdministrationClientWrapper`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumerFactory.cs`
  - `:37`, `:51-54`: holds a private `IServiceBusClientProvider`.
  - `:63`: builds a new `AdministrationClientWrapper` on each `Create` call. It is never exposed.
  - `:71-96`: chooses the queue or topic consumer from `UseServiceBusQueue`.
  - `:104-109`: `CreateAsync` delegates to `Create`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicConsumer.cs`
  - `:45`: `_subscriptionCreated` cache flag.
  - `:79-122`: `EnsureChannelAsync` is the only code that creates the subscription:
    - returns early if the subscription is already created or the mode is `Assume` (`:81-82`);
    - if the subscription exists, sets the flag (`:86-90`);
    - in `Validate` mode, throws (`:92-96`);
    - otherwise calls `CreateSubscriptionAsync` (`:98`);
    - treats `MessagingEntityAlreadyExists` as success (`:103-107`);
    - on any other error, calls `Reset()` and throws `ChannelFailureException` (`:113-121`).
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs`
  - `EnsureChannelAsync` is `protected abstract` (`:372`). It is called only from AcknowledgeAsync `:106`,
    ReceiveAsync `:177`, NackAsync `:243`, RejectAsync `:300` and the subclasses' PurgeAsync.
  - The sync wrappers go through `BrighterAsyncContext.Run`.
  - The constructor (`:60-74`) does no I/O.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusWrappers/AdministrationClientWrapper.cs:97-130`:
  `CreateSubscriptionAsync` creates the topic first if it is missing (`:101-104`).
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicMessageProducer.cs:63-95`:
  `EnsureChannelExistsAsync` creates only the topic (`:83`), lazily on send
  (`AzureServiceBusMessageProducer.cs:248`).

## Root-Cause Hypothesis

`AzureServiceBusChannelFactory` ignores `Subscription.MakeChannels`. The AWS and GCP channel factories
provision infrastructure before they return a channel; the ASB factory does not. In topic mode the
subscription is created only by `AzureServiceBusTopicConsumer.EnsureChannelAsync` (`:79-122`), which runs
on the first broker operation, not when the channel is built. The producer creates only the topic. So
under `OnMissingChannel.Create`, a message sent between building the channel and the first receive goes to
a topic with no subscription and is silently dropped.

Reference implementations:

- **AWS** (`src/Paramore.Brighter.MessagingGateway.AWSSQS/ChannelFactory.cs`):
  - `CreateAsyncChannelAsync` (`:92-149`) returns straight away on `Assume` (`:108-114`).
  - Otherwise it awaits `EnsureQueueAsync` (`:116-122`) and `EnsureSubscriptionAsync` (`:129-138`), inside
    a retry policy.
  - The sync methods use `BrighterAsyncContext.Run` (`:70-71`, `:82-83`).
- **GCP** (`src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubChannelFactory.cs`):
  - `CreateSyncChannel` (`:26-37`) runs `EnsureSubscriptionExistsAsync` inside `BrighterAsyncContext.Run`.
  - `CreateAsyncChannelAsync` awaits `EnsureSubscriptionExistsAsync` (`:74`).
  - That method honours Assume, Validate and Create.

**Suggested fix (UNVERIFIED — to be proven or refuted in /bugfix:confirm):** make `AzureServiceBusChannelFactory`
honour `OnMissingChannel.Create` up front in `CreateSyncChannel`, `CreateAsyncChannel` and
`CreateAsyncChannelAsync`, following AWS and GCP:

- make `CreateAsyncChannelAsync` truly async;
- have the sync paths use `BrighterAsyncContext.Run`;
- keep `GetAndCheckSubscription` first, so the type and timeout checks still throw before any I/O.

**Queue mode** is probably not affected by message loss. `AzureServiceBusQueueConsumer.EnsureChannelAsync`
(`:89-131`, flag `_queueCreated` `:47`) is equally lazy, but the queue producer creates the same queue on
send, and a queue keeps messages without needing a subscriber.

### Open questions for confirm

1. **How the factory gets to the admin client.** There are three options:
   - (a) an internal provision method on `AzureServiceBusConsumerFactory`;
   - (b) an internal entry point on `AzureServiceBusConsumer` that runs `EnsureChannelAsync`;
   - (c) inject an `IAdministrationClientWrapper` or `IServiceBusClientProvider` into the channel factory.
     This is a public API change.

   Option (b) reuses the existing Create/Validate/Assume and AlreadyExists handling. It also sets the
   consumer's cache flag, so the first receive skips the existence check, and it covers queue mode for free.
2. **Validate and Assume.** `Assume` stays a no-op. Should `Validate` also be checked up front, as AWS and
   GCP do? If so, a missing subscription throws `ChannelFailureException` at channel creation (consumer
   start-up) instead of on the first receive.
3. **Queue mode.** Provision the queue up front too, for symmetry? This would be harmless.
4. **Errors.** `ChannelFailureException` would now come from channel creation. There is no retry policy,
   unlike AWS.
5. **Sync paths** need `BrighterAsyncContext.Run`. This matches AWS, GCP and the consumer's own sync
   wrappers.
6. **Regression test.** ASB has no emulator.
   - Seam option 1: `AzureServiceBusConsumerFactory(IServiceBusClientProvider)` is public, and a test
     provider could return a recording `ServiceBusAdministrationClient` subclass. There is a precedent:
     `TestDoubles/InMemoryServiceBusAdministrationClient.cs:29`.
   - Seam option 2: inject `Fakes/FakeAdministrationClient.cs`. Caveat: its `CreateSubscriptionAsync` throws
     if the topic is missing.
   - Existing factory tests that must stay network-free:
     - `AzureServiceBusChannelFactoryTests.cs:11-48` (timeout checks; default `MakeChannels = Create`).
       These must still throw before any I/O.
     - `When_subscription_matches_should_create_sync_channel.cs` (uses `Assume`).
   - A live check: a send-before-receive test without the pre-create.
7. **Harness duplication.** After the fix, `AzureServiceBusMessageGatewayProvider.EnsureSubscriptionExistsAsync`
   (`:183-197`) and the pre-creates in the hand-written tests are redundant. Remove them here, or leave them?
8. **Samples.** The ASB task-queue sample uses `Assume`, so it is unaffected. Callers on the default `Create`
   now make admin calls at start-up rather than on the first receive. They need the same Manage rights as
   before; only the timing changes.

## Confirmed Root Cause

**Verdict: CONFIRMED.** `AzureServiceBusChannelFactory` does three things and nothing else:

- validates the subscription type and timeout (`:101-114`);
- builds a consumer (`:58-59`, `:79-80`);
- wraps the consumer in a `Channel` (`:61-66`) or `ChannelAsync` (`:82-87`).

`CreateAsyncChannelAsync` is `Task.FromResult(CreateAsyncChannel(...))` (`:97-99`). `MakeChannels` does
not appear in the file.

In topic mode, the only code that creates the subscription is `AzureServiceBusTopicConsumer.EnsureChannelAsync`
(`AzureServiceBusTopicConsumer.cs:79-122`, the create call is at `:98`). It is reached only from:

- `AcknowledgeAsync` (`AzureServiceBusConsumer.cs:106`)
- `ReceiveAsync` (`:177`)
- `NackAsync` (`:243`)
- `RejectAsync` (`:300`)
- `PurgeAsync` (`AzureServiceBusTopicConsumer.cs:76`)

The producer only ever creates the topic (`AzureServiceBusTopicMessageProducer.cs:83`), and does so lazily
when it sends. So with the default `MakeChannels = Create` (`AzureServiceBusSubscription.cs:76`), a send
that happens after the channel is built but before the first receive goes to a topic with no subscription,
and Azure Service Bus discards it.

## Evidence

- [x] **Code trace: there is no broker I/O anywhere on the channel-creation path.**
  - Channel factory: `:54-67`, `:75-88`, `:97-99`, `:101-114`.
  - `AzureServiceBusConsumerFactory.Create` (`:61-97`) only constructs objects: the admin wrapper, the
    receiver and sender providers, the producer and the consumer.
  - `ServiceBusReceiverProvider` (`:32`) and `ServiceBusSenderProvider` (`:12`) call `GetServiceBusClient()`
    eagerly, but they create no receiver or sender.
  - The consumer constructors (`AzureServiceBusConsumer.cs:60-74`, topic `:57-66`, queue `:56-63`) and the
    `Channel`/`ChannelAsync` constructors only assign fields.
- [x] **No other code creates the subscription before the first receive.**
  - ServiceActivator `ConsumerFactory.cs:100` / `:124` just call the channel factory.
  - `Dispatcher` (`Open`, `Receive`, `CreateConsumer`) does no provisioning and never calls `Purge`.
  - There is no ASB DI or hosted-service bootstrap.
  - `CombinedChannelFactory` only delegates.
- [x] **Azure Service Bus behaviour:** a topic with no subscriptions discards what it is sent. The CI
  evidence fits this: the generated tests went from 26 failures to 2 once the harness pre-created the
  subscription.
- [ ] **Red repro, network-free (to be written by /bugfix:test).**
  - Seam: a test `IServiceBusClientProvider` whose `GetServiceBusAdministrationClient()` returns one
    **shared** recording `ServiceBusAdministrationClient` subclass. Build it on
    `TestDoubles/InMemoryServiceBusAdministrationClient`. Note that `InMemoryServiceBusClient` returns a
    new admin client on every call (`:42`), so it cannot be used as-is.
  - The fake overrides `TopicExistsAsync` / `SubscriptionExistsAsync` (returning false),
    `CreateTopicAsync(CreateTopicOptions)` and `CreateSubscriptionAsync(CreateSubscriptionOptions, CreateRuleOptions)`.
    For queue mode, it also overrides `QueueExistsAsync` and `CreateQueueAsync(CreateQueueOptions)`.
  - Build the responses with `Response.FromValue(..., fakeResponse)`.
  - Assertion that fails today: after `CreateSyncChannel` / `CreateAsyncChannel` / `CreateAsyncChannelAsync`
    with the default `Create`, the recorder holds `("topic", "chan")`. Today it is empty.
  - Open point: whether these SDK methods are `virtual` has not been proven. The compile will settle it.

## Suggested-Fix Assessment

**PARTIAL.** The production shape is confirmed. The plan to remove the pre-creates is wrong for two
hand-written tests.

- **(1) Internal entry point plus a call from the factory: confirmed.**
  - `Create` can only return `AzureServiceBusQueueConsumer` or `AzureServiceBusTopicConsumer`, so a
    pattern-match to `AzureServiceBusConsumer` is safe.
  - There is no `InternalsVisibleTo`, and none is needed: the test goes through the public factory.
  - `EnsureChannelAsync` never touches a receiver, so it is safe to call before any receive.
  - It sets the per-consumer cache flag, so the first receive afterwards skips the existence check.
  - The Proactor uses the **sync** `CreateAsyncChannel` (`ConsumerFactory.cs:124`), so all three factory
    methods must provision. The sync ones do it through `BrighterAsyncContext.Run`.
- **(2) Create and Validate up front, Assume a no-op: confirmed.**
  - Both `EnsureChannelAsync` implementations already do exactly this, in topic and queue mode.
  - **Behaviour change:** `ChannelFailureException` (missing subscription under Validate, a transient admin
    error, or missing Manage rights) now comes from channel creation. Nothing catches it there, so it
    escapes `Dispatcher.Receive()` / `Open()` at start-up.
  - Today the pumps catch it on receive and retry after `ChannelFailureDelay`.
  - This matches AWS and GCP, but AWS wraps provisioning in a retry policy and this fix adds none.
  - Record the change in the release notes.
- **(3) Test seam: confirmed**, subject to the virtual-method check and a shared recording admin client.
- **(4) Remove the pre-creates: partial.** See the per-site verdicts below.

## Scope Notes

**Production changes**

- `AzureServiceBusChannelFactory.cs` `:54-67`, `:75-88`, `:97-99`: run `GetAndCheckSubscription` first, so
  the existing tests still throw before any I/O. Then create the consumer, ensure the channel, and build the
  channel. Make `CreateAsyncChannelAsync` truly async.
- `AzureServiceBusConsumer.cs:372`: add an internal entry point over `EnsureChannelAsync`.
- Queue mode is covered by the same path.

**Pre-create removal (user decision 4), with per-site verdicts**

- `AzureServiceBusMessageGatewayProvider.EnsureSubscriptionExistsAsync` (`:165-193`), called at `:197` and `:343`:
  - **Redundant, so remove it.**
  - It also used the default configuration rather than `subscription.Configuration`, so removing it is more
    faithful to what the subscription asks for.
- `When_posting_a_message_via_the_producer.cs:65`: **redundant, so remove it.**
- `When_consuming_a_message_via_the_consumer.cs:94`:
  - **Not redundant as written.** It pre-creates with a custom `_subscriptionConfiguration` (MaxDeliveryCount,
    LockDuration, TTL, SqlFilter), but the subscription it passes to the factory (`:42-46`) has none.
  - It can be removed only if `subscriptionConfiguration: _subscriptionConfiguration` is passed into the
    subscription. Otherwise `When_A_Subscription_is_created_the_properties_are_set_as_Expected` fails.
- `When_posting_a_large_message_via_the_producer.cs:66-67` (queue and topic):
  - **Not redundant, so keep them.** They set `maxMessageSizeInKilobytes: 3000`, and the consumer path never
    sets a maximum size.
- `When_posting_a_large_message_via_the_producer.cs:68` (subscription): **redundant, so remove it.**
- `FakeAdministrationClient` seeding in the `Proactor`/`Reactor` consumer unit tests: not a pre-create for
  this bug. Those tests build consumers directly. Leave them.

**Not in scope (parity notes only)**

- `AzureServiceBusConsumerFactory.Create` used directly stays lazy, which is the consumer contract.
- RMQ.Async declares lazily in the consumer, and an exchange with no bound queue drops messages, so it has a
  similar gap.
- Kafka retains messages in its log, so it loses nothing.
- Postgres, MsSql, AWS and GCP already provision when the channel is created.

## Regression Test

All in `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/`, network-free, through the public
`AzureServiceBusChannelFactory` with `TestDoubles/InMemoryServiceBusAdministrationClient` (records created
topics, subscriptions and queues; counts requests) supplied by `TestDoubles/InMemoryServiceBusClientProvider`
(new; hands out one shared admin client).

| Test | How RED was observed |
|---|---|
| `When_creating_a_sync_channel_with_create_should_create_the_topic_subscription` | failed before the fix (`Collection: []`) |
| `When_creating_an_async_channel_with_create_should_create_the_topic_subscription` | failed before the fix (`Collection: []`) |
| `When_creating_an_async_channel_asynchronously_with_create_should_create_the_topic_subscription` | characterisation; failed with `CreateAsyncChannel`'s provisioning call removed |
| `When_creating_a_channel_with_validate_for_a_missing_subscription_should_throw` | characterisation; failed (no exception) with `CreateSyncChannel`'s provisioning call removed |
| `When_creating_a_channel_with_assume_should_make_no_administration_requests` | characterisation; failed (4 requests) with the `Assume` early return removed from `AzureServiceBusTopicConsumer.EnsureChannelAsync` |
| `When_creating_a_queue_channel_with_create_should_create_the_queue` | characterisation; failed with `CreateSyncChannel`'s provisioning call removed |

## Fix

- `AzureServiceBusConsumer`: `internal Task EnsureChannelExistsAsync()` exposes the existing
  `EnsureChannelAsync` (Create creates, Validate throws `ChannelFailureException`, Assume does nothing; topic
  and queue consumers alike) to the channel factory. No public API change.
- `AzureServiceBusChannelFactory`: after `GetAndCheckSubscription` (so the type and timeout checks still
  throw before any I/O) and creating the consumer, each method ensures the channel before returning it.
  `CreateSyncChannel` and `CreateAsyncChannel` use `BrighterAsyncContext.Run`; `CreateAsyncChannelAsync`
  validates synchronously and then awaits in a private async method.
- Pre-creates removed: the conformance harness's `EnsureSubscriptionExistsAsync`, and the subscription
  pre-creates in `When_posting_a_message_via_the_producer` and `When_posting_a_large_message_via_the_producer`
  (whose 3000 KB queue/topic pre-creates stay). `When_consuming_a_message_via_the_consumer` now passes its
  custom configuration into the subscription instead of pre-creating with it.
- Release note: `release_notes.md`, "Azure Service Bus: the channel factory provisions the subscription before
  handing out a channel (#4309)", including the startup failure-mode change.
- Not verified locally against a broker: the 38 broker-backed tests in the project need ASB credentials;
  the `azure-ci` job is their check.
