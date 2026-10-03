# Bugfix: RabbitMQ native delayed delivery

**Linked Issue**: #4388
**Status**: Verified

## Symptom

With an `x-delayed-message` exchange configured, a positive requeue delay is discarded in both RMQ gateways.
Messages delivered through the plugin also retain a nonzero `Header.Delayed`, contrary to the transport conformance assertions.
Existing conformance providers exercise scheduler delegation and do not cover the plugin path.

## Suspected Location

- Async `RmqMessageConsumer.cs:454–457` forwards the delay to `RmqMessagePublisher.RequeueMessageAsync`.
- Async `RmqMessagePublisher.cs:140–147` replaces that delay with zero and publishes through the default exchange.
- Sync `RmqMessageConsumer.cs:361–364` and `RmqMessagePublisher.cs:126–136` have the equivalent path.
- Async `RmqMessageCreator.cs:229–276` and Sync `RmqMessageCreator.cs:227–274` convert elapsed negative `x-delay` values to positive durations.
- The creators assign those durations to `MessageHeader.Delayed` at Async line 111 and Sync line 109.
- `ExchangeConfigurationHelper` mutates the shared exchange type while declaring plugin support: Async lines 61–64 and Sync lines 55–58.

These source files are under their respective `src/Paramore.Brighter.MessagingGateway.RMQ.Async/` and `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/` directories.

## Root-Cause Hypothesis

The consumers select the native delay path and forward the timeout correctly, but their publishers always perform an immediate queue-specific requeue.
Setting the delay header alone cannot fix delivery through the default exchange.
The creators interpret the plugin's elapsed delay as retained metadata, while conformance requires a consumed delay instruction to be zero.

The issue suggests forwarding the timeout, using a delayed exchange, normalizing receipt, and adding native conformance configurations.
The suggestion was initially unverified. Independent confirmation supports the causes but finds the proposed exchange change incomplete: it must preserve queue-specific retries.

## Confirmed Root Cause

Both consumers forward the requested timeout, but both publishers replace it with zero and use the default exchange, which cannot apply plugin delay.
The message creators independently turn an elapsed negative `x-delay` into a positive `Header.Delayed`.

A third defect affects normal native-path setup: exchange declaration overwrites the caller's shared `Exchange.Type`.
A later producer, consumer, or reconnect then supplies `x-delayed-message` as `x-delayed-type`.
The plugin rejects this underlying type on creation, though an equivalent redeclaration may succeed;
the regression directly proves the unwanted mutation of public connection settings.

## Evidence

- Independent read-only confirmation traced the timeout through both consumers and publishers at the locations above.
- Each gateway retains the connection settings by reference: Async `RmqMessageGateway.cs:72`, Sync `RmqMessageGateway.cs:68`.
- Each new gateway channel declares its exchange: Async `RmqMessageGateway.cs:153–160`, Sync `RmqMessageGateway.cs:151–154`.
- `ExchangeConfigurationHelper` reads the current type for `x-delayed-type`, then overwrites that same setting with `x-delayed-message`.
  This changes the arguments of subsequent declarations using the same connection settings.
- The [plugin validator](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/blob/main/src/rabbit_exchange_type_delayed_message.erl#L64-L80) rejects `x-delayed-message` as the underlying delayed type.
- `tools/Paramore.Brighter.Test.Generator/Templates/DefaultMessageAssertion.cs.liquid:60` requires zero `Header.Delayed` on receipt.
  SQS receipt (`SqsMessageCreator.cs:104`) and Service Bus receipt (`AzureServiceBusMessageCreator.cs:120`) also explicitly use zero.

User approved the diagnosis and initial regression tests before implementation. Broker runs reproduced
immediate direct/fanout retries, nonzero consumed delays, and shared exchange-type mutation.
Topic cases initially failed before first delivery under Khepri; switching the dedicated fixture to
Mnesia resolved that upstream plugin limitation. Topic failures were not counted as Brighter RED evidence.

## Scope Notes

- Republishing to the original topic can deliver retries to unrelated subscribers. Publishing to the queue name on the configured exchange can miss the destination because its bindings use subscription routing keys.
- Preserve immediate queue-specific requeue for zero and negative timeouts, including configurations without the plugin or a scheduler.
- Preserve publish-before-ack ordering, message metadata, and the logical topic header.
- `src/Paramore.Brighter/MessageHeader.cs:216` documents `Delayed` as the requested period. Reconcile this wording with the receipt contract.
- Correct the confirmed shared `Exchange.Type` mutation and cover repeated declarations using the same connection settings.
- Keep scheduler conformance coverage and add separate native configurations with a plugin-enabled broker.

Implemented routing approach: a separate direct delayed retry exchange with queue-specific bindings.
Unlike the original exchange, its routing must not depend on topic wildcards or fanout behavior.
The retry exchange is `<configured-exchange>.requeue`, uses `x-delayed-type=direct`, inherits exchange durability,
and has `autoDelete=false`. Bind each queue to it by queue name.
Create should create and bind; Validate should check without creating; Assume should use pre-provisioned topology.

The existing consumer path does not forward its `MakeChannels` setting to exchange setup (Async consumer line 485; Sync line 413).
Avoid extending that inconsistency to new retry topology. Broader provisioning cleanup is outside this fix unless required by a focused regression.

Normalizing `Header.Delayed` changes behavior for callers reading the elapsed duration. Retain raw `x-delay` in `Header.Bag` for diagnostics.
The issue's claim that every other transport already clears delay is too broad: Kafka and Redis also parse delay metadata.
The proposed change follows the conformance contract for RMQ and does not redefine other transports.

## Regression Test

Initial tests are in `MessagingGateway/NativeDelay/When_requeuing_with_native_delay_should_redeliver_only_to_the_original_queue.cs`
in both `tests/Paramore.Brighter.RMQ.Async.Tests/` and `tests/Paramore.Brighter.RMQ.Sync.Tests/`.

They exercise the public producer and consumer against the real plugin without a scheduler:

- Delayed requeue: receive and acknowledge the initial copy on a second subscribed queue, then retry the first queue's copy.
  Assert no early redelivery, eventual delivery after three seconds, preserved message metadata, and no retry on the other queue.
  Cover direct, topic, and fanout exchanges; the second subscription uses a wildcard for topic and a different routing key for fanout.
- Delayed send: assert no early delivery, eventual delivery, zero `Header.Delayed`, and the retained negative raw `x-delay` diagnostic header.
- Shared settings: create a producer and consumer from the same connection settings, then prove both can connect and deliver without changing the configured exchange type.

The async fixture has 14 cases across Classic and Quorum queues. The sync fixture has seven Classic cases.
All 21 initial cases passed after implementation on the pinned Mnesia broker.
Separate connection settings in the timing tests allow those tests to reach the delay defects independently of the shared-settings defect.
Each test uses unique queues and exchanges and cleans them up when disposed.

The fixtures now contain 19 async and 12 sync cases, adding negative requeue and Validate/Assume coverage.
Generated variants cover omitted/zero retries and the complete existing conformance suite.
The three native providers reuse the existing setup but supply a plugin-enabled exchange and no scheduler.
Sync configuration changed from a single gateway to `Classic` and `NativeClassic`; its existing generated
files were regenerated under `MessagingGateway/Classic`, with no hand edits to generated output.

## Fix

- Both publishers forward the native retry delay and publish positive retries through the dedicated exchange.
- Both consumers provision queue-specific retry bindings according to `MakeChannels`.
- Both creators clear `Header.Delayed`; the original wire header remains in `Header.Bag`.
- Exchange declaration selects the wire type without mutating `Exchange.Type`.
- Existing scheduler providers retain their default behavior; native derived providers disable the scheduler.
- Native configurations, generated tests, ledger rows, and audit counts are checked in together.
- A separate CI job builds the pinned broker and runs the native category; stock jobs exclude that category.
- XML documentation and `docker/RabbitMQ/README.md` explain the topology, provisioning, and receipt contract.

## Verification

- Initial RED: all 21 cases failed before implementation; direct/fanout delay, receipt normalization,
  and configuration mutation failed on their relevant assertions. Topic routing required the fixture correction above.
- .NET 10 native generated conformance: 84 async and 42 sync cases passed.
- .NET 10 focused regressions including provisioning: 19 async and 12 sync cases passed.
- Generator and conformance audits: 297 passed.
- .NET 10 existing transport regression: async 212 passed / 2 existing skips; sync 107 passed / 1 existing skip.
- .NET 9 combined stock/native runs: async 315 passed / 2 existing skips; sync 161 passed / 1 existing skip.
- Both target frameworks therefore pass 476 transport cases, with the same three existing skips.
- Mutual-TLS acceptance tests requiring their own Docker certificate fixture were excluded from these runs.
- The staged whitespace check reports trailing spaces emitted by the existing generated-test templates.
  Handwritten changes are clean; generated output is retained as produced by the generator.

## Infrastructure and Related Work

`docker/RabbitMQ/Dockerfile` already pins RabbitMQ 4.2.6 and delayed-message plugin 4.2.0-rc.1, with a plugin checksum.
The normal compose file and CI jobs use stock images. Native tests need separate broker setup and CI selection.
`docker-compose-rmq-native-delay.yaml` builds that pinned image and publishes AMQP on localhost port 5673.
The new fixtures use that port by default; `RMQ_NATIVE_DELAY_URI` can override it.
Their `Requires=RabbitMQDelayedPlugin` trait identifies the separate infrastructure requirement.
CI selection and a dedicated native job are included. The pinned image was built and exercised locally.
The compose fixture disables Khepri on first boot because the plugin fails topic routing with that metadata store.
The upstream plugin is no longer maintained and requires a compatible RabbitMQ version; this issue does not introduce support for RabbitMQ 4.3+.
See the [plugin repository](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange).

PR #4106 was closed without merge after the maintainer identified the conformance work as handling its scope.
Issue #4105 remains open. Neither is an accepted instruction to remove the native API as part of this fix.

Implementation branch: `bugfix/4388-rmq-native-delay`, based on upstream `master`.
