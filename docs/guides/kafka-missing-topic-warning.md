# Kafka missing-topic startup warning

Kafka's Consumer group protocol (KIP-848) permits subscriptions to topics that do
not exist without reporting the subscription error produced by Classic. With
`OnMissingChannel.Assume`, a missing topic can therefore look like an empty channel.
Brighter does not add a broker lookup to Assume to distinguish these situations.

## Enable the warning

Register the Kafka subscription rule with your application's services before
calling `ValidatePipelines()` on the Brighter builder:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;

services.AddSingleton<ISpecification<Subscription>>(
    KafkaConsumerValidationRules.MissingTopicDetection());

// After configuring Brighter's consumers, handlers, and mappers:
brighterBuilder.ValidatePipelines();
```

Keep the application's normal consumer registration and hosting setup. The rule
is loaded from the Kafka assembly through the existing subscription-specification
registration; core does not reference Kafka. Referencing the Kafka package alone
does not register this rule, and disabling pipeline validation disables the
startup warning.

## What it reports

A subscription declaring `ConsumerGroupProtocol` and `OnMissingChannel.Assume`
produces a warning naming the subscription and topic. Classic/default protocol,
Validate, Create, and non-Kafka subscriptions do not produce this warning.

The warning explains that a missing topic will not produce a subscription error
and may appear to be an empty channel. It does **not** say that the topic is
missing. Warning severity does not prevent startup, even with
`ValidatePipelines(throwOnError: true)`.

The rule reads subscription settings only. It does not connect to Kafka, construct
a client, change the provisioning policy, or invoke `ConfigHook`. It recognizes
the declared `ConsumerGroupProtocol`; protocol selection made only by a custom
`IGroupProtocol` implementation or overridden inside `ConfigHook` is not evaluated.
Prefer declaring the protocol on `KafkaSubscription.GroupProtocol` so the static
check reflects the intended configuration.

## Choosing a policy

Keep Assume when infrastructure is provisioned separately and runtime existence
checks are unwanted. Monitor message flow and investigate unexpected inactivity,
but do not treat every empty receive as proof of a missing topic: idle consumers
and rebalances are normal.

Use `OnMissingChannel.Validate` when an explicit infrastructure check is required
and the application has permission to perform it. Kafka's Validate path checks
topic existence and configured partition/replication counts. Create retains its
existing provisioning behavior. Neither policy is changed by registering the rule.

See [the design decision](../adr/0073-kafka-consumer-protocol-missing-topic-detection.md)
for the reasoning behind preserving Assume's no-check behavior.
