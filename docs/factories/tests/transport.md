# Generating a Conformance Suite for a Transport

This page covers generating the test suite for `Paramore.Brighter.MessagingGateway.X`. Read the [overview](./README.md) first for how to invoke the generator and the rules about generated code, and [Writing a Transport](../transports/transports.md) for the interfaces you are implementing.

## The Workflow

1. Add `test-configuration.json` to `tests/Paramore.Brighter.X.Tests/`.
2. Declare which optional broker features you support.
3. Run the generator from inside that directory.
4. Implement the generated provider interface, in both Reactor (sync) and Proactor (async) form.
5. Run the suite against a real broker until it is green.

## Step 1 — Write the Configuration

A worked example, [`tests/Paramore.Brighter.RMQ.Async.Tests/test-configuration.json`](../../../tests/Paramore.Brighter.RMQ.Async.Tests/test-configuration.json), which tests classic and quorum queues as two variants of the same gateway:

```json
{
  "Namespace": "Paramore.Brighter.RMQ.Async.Tests",
  "MessageAssertion": "RmqMessageAssertion",
  "MessagingGateways": {
    "Classic": {
      "Publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqClassicMessageGatewayProvider",
      "Category": "RMQ",
      "CollectionName": "Classic",
      "HasSupportToPublishConfirmation": true,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToValidateBrokerExistence": true,
      "HasSupportToRequeue": true,
      "ReceiveMessageTimeoutInMilliseconds": 4000
    },
    "Quorum": {
      "Publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqQuorumMessageGatewayProvider",
      "Category": "RMQ",
      "CollectionName": "Quorum",
      "HasSupportToPublishConfirmation": true,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToValidateBrokerExistence": true,
      "HasSupportToRequeue": true,
      "ReceiveMessageTimeoutInMilliseconds": 4000
    }
  }
}
```

Use singular `MessagingGateway` for one gateway ([Redis](../../../tests/Paramore.Brighter.Redis.Tests/test-configuration.json) does), or plural `MessagingGateways` when one broker has variants worth testing separately. Unlike the outbox generator, **both forms emit the full Reactor and Proactor suites**, so the choice here is genuinely just one-versus-many.

### `MessagingGatewayConfiguration` schema

Defined in [`Configuration/MessagingGatewayConfiguration.cs`](../../../tools/Paramore.Brighter.Test.Generator/Configuration/MessagingGatewayConfiguration.cs).

| Property | Type | Default | Notes |
|---|---|---|---|
| `Publication` | string | `""` | **Required.** Your `Publication`-derived type. |
| `Subscription` | string | `""` | **Required.** Your `Subscription`-derived type. |
| `MessageGatewayProvider` | string? | `null` | **Required.** The type *you* write. |
| `Prefix` | string | `""` | Falls back to the dictionary key. |
| `Namespace` | string? | inherits root | |
| `MessageBuilder` | string? | inherits root | |
| `MessageAssertion` | string? | inherits root | Override when your broker cannot round-trip a `Message` faithfully. |
| `Category` | string? | `null` | Renders `[Trait("Category", ...)]`. |
| `CollectionName` | string? | `null` | Renders `[Collection(...)]`. |
| `ReceiveMessageTimeoutInMilliseconds` | int | `300` | See [Timeouts](#timeouts). |
| `MessageConfirmationTimeoutInMilliseconds` | int | `1000` | How long to wait for a publish confirmation. |
| `DelayBetweenReceiveMessageInMilliseconds` | int? | `null` | Pause between receive attempts, for brokers that dislike tight polling. |

### Feature flags

These decide which tests are emitted. Declare only what your broker genuinely does — a flag you cannot honour is a test you cannot pass.

| Flag | Default | Enables |
|---|---|---|
| `HasSupportToPublishConfirmation` | `false` | Publish-confirmation test. |
| `HasSupportToDelayedMessages` | `false` | Delayed-delivery and requeue-with-delay tests. |
| `HasSupportToDeadLetterQueue` | `false` | DLQ test, and `GetMessageFromDeadLetterQueue` on your provider. |
| `HasSupportToValidateBrokerExistence` | `false` | Posting-to-a-missing-broker test. |
| `HasSupportToRequeue` | `false` | Both requeue tests. |
| `HasSupportToValidateInfrastructure` | **`true`** | The two `OnMissingChannel` validation tests. |

**`HasSupportToValidateInfrastructure` is the trap: it is opt-*out*, while every other flag is opt-*in*.** Omit it and you get the infrastructure-validation tests whether or not your gateway implements `OnMissingChannel.Validate`. Redis sets it `false` explicitly.

> **⚠️ `HasSupportToDelayedMessages` and `HasSupportToDeadLetterQueue` are under redesign — [#4240](https://github.com/BrighterCommand/Brighter/issues/4240).**
>
> These two flags read as if they gate *native* broker support, but delayed send/requeue and dead-lettering are things **Brighter provides for every transport**: it falls back to its own scheduler when there is no native delay, and provisions a dead-letter producer driven by the universal `Reject` flow when there is no native DLQ. So these are really conformance obligations, not optional capabilities, and the plan is to make their tests universal and ungated. Set them to match today's generated suite, but expect them to change; do not read "supported" here as "native".

### How skipping works

By **substring match on the template filename**, from [`MessagingGatewayGenerator.cs`](../../../tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs):

```csharp
private static bool SkipTest(MessagingGatewayConfiguration configuration, string fileName)
{
    if (!configuration.HasSupportToPublishConfirmation && fileName.Contains("confirming_posting")) return true;
    if (!configuration.HasSupportToDelayedMessages && fileName.Contains("delayed_message")) return true;
    if (!configuration.HasSupportToDelayedMessages && fileName.Contains("with_delay")) return true;
    if (!configuration.HasSupportToDeadLetterQueue && fileName.Contains("dead_letter_queue")) return true;
    if (!configuration.HasSupportToValidateBrokerExistence && fileName.Contains("no_broker_created")) return true;
    if (!configuration.HasSupportToRequeue && fileName.Contains("requeuing")) return true;
    if (!configuration.HasSupportToValidateInfrastructure
        && (fileName.Contains("assume_channel") || fileName.Contains("validate_channel"))) return true;
    return false;
}
```

The mapping is stringly-typed, so a new template's filename MUST contain the fragment its flag matches on or it will always be emitted.

Remember that **turning a flag off does not delete the test file** it previously produced. Delete it by hand.

## Step 2 — Generate

```bash
cd tests/Paramore.Brighter.RMQ.Async.Tests
dotnet run --project ../../tools/Paramore.Brighter.Test.Generator
```

Output, per variant:

```
MessagingGateway/<Key>/Generated/Reactor/    # sync tests + IAmAMessageGatewayReactorProvider.cs
MessagingGateway/<Key>/Generated/Proactor/   # async tests + IAmAMessageGatewayProactorProvider.cs
```

with namespace `<Namespace>.MessagingGateway.<Key>.Reactor` / `.Proactor`. With the singular form and no `Prefix`, this collapses to `MessagingGateway/Generated/...`.

## Step 3 — Implement the Provider

The generator emits the interface; you write the wrapper. `IAmAMessageGatewayReactorProvider`, with `{{ Publication }}` and `{{ Subscription }}` substituted for your types:

```csharp
public interface IAmAMessageGatewayReactorProvider
{
    RoutingKey GetOrCreateRoutingKey([CallerMemberName] string testName = null!);
    ChannelName GetOrCreateChannelName([CallerMemberName] string testName = null!);

    RmqPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create);
    RmqSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName, OnMissingChannel makeChannel, bool setupDeadLetterQueue = false);

    IAmAMessageProducerSync CreateProducer(RmqPublication publication);
    IAmAChannelSync CreateChannel(RmqSubscription subscription);

    void CleanUp(IAmAMessageProducerSync? producer, IAmAChannelSync? channel, IEnumerable<Message> messages);
    Message GetMessageFromDeadLetterQueue(RmqSubscription subscription);
}
```

`IAmAMessageGatewayProactorProvider` is the same shape in async form (`IAmAMessageProducerAsync`, `IAmAChannelAsync`, `Task`-returning).

Points that matter:

- `GetOrCreateRoutingKey` / `GetOrCreateChannelName` take `[CallerMemberName]`, so each test gets its **own** topic and queue. This is what stops tests interfering when they share a broker; your implementation MUST derive a distinct, valid name per test name. Prefixing with a GUID per run is common — see the examples below.
- `CreatePublication` / `CreateSubscription` MUST honour the `OnMissingChannel` argument, which is what the infrastructure-validation tests exercise.
- `CleanUp` receives the producer, channel, and messages so you can tear down broker-side resources.
- `GetMessageFromDeadLetterQueue` is only called when `HasSupportToDeadLetterQueue` is `true`; throw `NotImplementedException` otherwise.

You MUST implement both Reactor and Proactor providers per variant, named to match `MessageGatewayProvider`. Worked examples: [`RmqClassicMessageGatewayProvider`](../../../tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/RmqClassicMessageGatewayProvider.cs), [`KafkaMessageGatewayProvider`](../../../tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaMessageGatewayProvider.cs), [`SnsStandardMessageGatewayProvider`](../../../tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsStandardMessageGatewayProvider.cs).

## Step 4 — Run

```bash
docker compose -f docker-compose-rmq.yaml up -d
dotnet test tests/Paramore.Brighter.RMQ.Async.Tests --filter "Category=RMQ"
```

### Timeouts

`ReceiveMessageTimeoutInMilliseconds` defaults to `300`, which suits only in-process or local stores. Under-setting it is the usual cause of a flaky suite. Values in use today:

| Transport | Timeout (ms) |
|---|---|
| MSSQL, PostgreSQL | `300` (default) |
| RMQ, AWS | `4000` |
| GCP | `10000` |
| Kafka | `15000` |

Start generous. Tighten only once green.

## What Gets Tested

Twelve behaviours, in both Reactor and Proactor form. Which you get depends on your flags:

| Test | Requires flag |
|---|---|
| `When_posting_a_message_via_the_messaging_gateway_should_be_received` | always |
| `When_a_message_consumer_reads_multiple_messages_should_receive_all_messages` | always |
| `When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception` | always |
| `When_sending_a_message_should_propagate_activity_context` | always |
| `When_posting_a_message_but_no_broker_created_should_throw_exception` | `HasSupportToValidateBrokerExistence` |
| `When_confirming_posting_a_message_should_receive_publish_confirmation` | `HasSupportToPublishConfirmation` |
| `When_requeuing_a_failed_message_should_receive_message_again` | `HasSupportToRequeue` |
| `When_requeuing_a_failed_message_with_delay_should_receive_message_again` | `HasSupportToRequeue` **and** `HasSupportToDelayedMessages` (its filename matches both `requeuing` and `with_delay`) |
| `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` | `HasSupportToDeadLetterQueue` |
| `When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery` | `HasSupportToDelayedMessages` |
| `When_infrastructure_missing_and_assume_channel_should_throw_exception` | `HasSupportToValidateInfrastructure` |
| `When_infrastructure_missing_and_validate_channel_should_throw_exception` | `HasSupportToValidateInfrastructure` |

The four unconditional tests are the floor: send and receive, receive a batch, post concurrently, and propagate tracing context. Everything else is a capability you opt into.
