# Bugfix: Make MQTT consumer producer instrumentation configurable

**Linked Issue**: #4365
**Status**: Verified

## Symptom

The MQTT consumer creates requeue, dead-letter, and invalid-message producers
without supplying their instrumentation options. Applications cannot configure
those producers through the consumer configuration or consumer factory.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessagingGatewayConfiguration.cs`
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs`
- `src/Paramore.Brighter.MessagingGateway.MQTT/MqttMessageConsumerFactory.cs`

## Root-Cause Hypothesis

The three internal producer construction sites omit the optional instrumentation
argument, leaving `InstrumentationOptions.All` in force.

## Confirmed Root Cause

The configuration gap is confirmed. The consumer configuration exposes no
instrumentation setting, and all three producer constructors use the default.

The issue's additional claim that these producers currently trace message bodies
was not reproduced. Their `Span` property is never assigned, and
`BrighterTracer.WriteProducerEvent` returns immediately for a null span.

## Evidence

A broker-backed diagnostic exercised synchronous and asynchronous requeue,
dead-letter rejection, and invalid-message rejection with an active `Activity`.
All six sends reached an in-process MQTTnet TCP broker, with no producer events.
Control sends using a directly constructed producer with an explicitly assigned
span recorded an event: `All` included the body, while `All & ~RequestBody` did not.

## Scope Notes

- Expose the setting on `MqttMessagingGatewayConsumerConfiguration`. Both consumer
  factory methods already forward this object, so constructor signatures remain
  unchanged and directly constructed consumers use the same configuration path.
- Preserve `All` as the default. Capture the setting at consumer construction so
  all lazy producers use the same selection even if the configuration is mutated.
- Do not assign spans, create additional events, or alter connection lifetimes,
  routing, scheduling, message payloads, or the separate synchronous-connect issue.
- This fixes option reachability, not the separate missing-span behavior.

## Regression Test

- `tests/Paramore.Brighter.MQTT.Tests/When_creating_mqtt_consumer_configuration_should_default_to_all_instrumentation.cs`
- `tests/Paramore.Brighter.MQTT.Tests/When_configuring_mqtt_consumer_instrumentation_should_preserve_selected_options.cs`

Before implementation, these tests failed to compile with CS1061 because the
configuration property did not exist. After implementation, all four cases passed
on .NET 9 and .NET 10: the default, explicit `All`, `All & ~RequestBody`, and `None`.

These tests guard the public configuration contract, not private producer fields.
Forwarding was also checked by code tracing and temporary runtime inspection of
the producers created through both factory paths (see Verification). An absent-body
assertion alone cannot establish forwarding because these producers have no span.

## Fix

Added the documented `InstrumentationOptions` configuration property, captured it
in a readonly consumer field, and passed it to the three internal producers.

For example, configure the consumer factory with:

```csharp
var configuration = new MqttMessagingGatewayConsumerConfiguration
{
    Hostname = "localhost",
    TopicPrefix = "orders",
    InstrumentationOptions = InstrumentationOptions.All & ~InstrumentationOptions.RequestBody
};
var factory = new MqttMessageConsumerFactory(configuration);
```

## Verification

- Full `Paramore.Brighter.MQTT.Tests` suite against the repository's Mosquitto
  Docker Compose broker: 71 passed, 6 skipped, 0 failed on each of .NET 9 and
  .NET 10. The six existing skips cover Nack redelivery and requeue-budget
  exhaustion, deferred under #4240 and #4351; none was added by this fix.
- MQTT gateway Release build passed for .NET 8, .NET 9, and .NET 10 with no warnings
  or errors.
- Repeated the broker-backed diagnostic with consumer settings `All`,
  `All & ~RequestBody`, and `None`. Each run delivered all six internal sends and
  both direct-producer control sends. Internal producers continued to record no
  events, preserving the existing missing-span behavior.
- Reviewed both factory creation paths and all three internal producer constructor
  calls. No constructor signature, dependency, tracing-span assignment, or
  connection-lifetime changes were introduced.
- A temporary diagnostic outside the repository used reflection to read the
  options stored in each producer after its real send path initialized it. It
  checked all three producers through both sync and async factories for the
  implicit default, explicit `All`, `All & ~RequestBody`, and `None`: 24 matching
  runtime values. Two further runs changed the configuration after consumer
  construction but before the first send; all 12 producer values retained the
  original selection. All 36 runtime checks passed, and all 48 broker publishes
  arrived, including the direct-producer controls. No private fields or spans were
  modified by this inspection.
- `git diff --check` passed. The dedicated test broker was stopped after testing.

The private-field diagnostic is not part of the committed regression suite; it is
implementation-coupled runtime evidence. The permanent tests do not guard against
removing an internal forwarding argument. Observing the option's telemetry effect
through a consumer still requires resolving the separate span gap, which is
deliberately outside this fix.
