---
id: 0022-generate-metrics-from-traces
title: "Generate Metrics from Traces"
status: Proposed
author:
  - "Brighter Team"
created: 2025-01-03
summary: "Brighter will implement a custom OpenTelemetry processor (BrighterMetricsFromTracesProcessor) that derives standard messaging and database metrics from existing trace spans, registered automatically via AddBrighterInstrumentation when the Brighter meter is enabled."
tags:
  - "observability"
  - "otel"
  - "metrics"
  - "tracing"
---

# 22. Generate Metrics from Traces

Date: 2025-01-03

## Status

Proposed

## Terminology
For consistency OpenTelemetry terms are used over .NET terms:
- Span over Activity
- Attribute over Tag

## Context
[adr/0010-brighter-semantic-conventions](0010-brighter-semantic-conventions.md#command-processor-operations) outlines semantic conventions of interest and how OpenTelemetry traces will be implemented in Brighter. Traces can answer questions to how an individual request performed, it's also useful to see how an operation performs in aggregate. Brighter does not yet provide this out of the box.

## Decision

### Metrics from Traces
OpenTelemetry defines common metrics for several instrumentation domains, the ones applicable to Brighter being messaging and databases.

#### Messaging metrics

| Name | Instrument Type | Unit (UCUM) | Description |
|------|----------------|-------------|-------------|
| `messaging.client.operation.duration` | Histogram | `s` | Duration of messaging operation initiated by a producer or consumer client |
| `messaging.client.sent.messages` | Counter | `{message}` | Number of messages producer attempted to send to the broker |
| `messaging.client.consumed.messages` | Counter | `{message}` | Number of messages that were delivered to the application |
| `messaging.process.duration` | Histogram | `s` | Duration of processing operation |

#### Database metrics

| Name | Instrument Type | Unit (UCUM) | Description |
|------|----------------|-------------|-------------|
| `db.client.operation.duration` | Histogram | `s` | Duration of database client operations |

Brighter can implement a custom [processor](https://github.com/open-telemetry/opentelemetry-collector/blob/main/processor/README.md) to generate metrics from spans. The spans defined in [adr/0010-brighter-semantic-conventions](0010-brighter-semantic-conventions.md) lists operations that can be mapped to metrics. For example, a publish span will be mapped to both the `messaging.client.sent.messages` and `messaging.client.operation.duration` metrics.

Only producer spans contribute to the sent-message counter. In-process event publication does not represent a broker send. Failed producer attempts still count.

Receive-span enrichment records whether the channel delivered a message. Empty polls, failed receives and the pump's quit signal do not increment the consumed-message counter. Their receive spans still contribute to the client-operation duration histogram. Delivery state is stored as an internal activity property, independently of the optional exported message attributes.

### Transport identity

Consumers and channels can expose their transport through the optional `IHaveAMessagingSystem` interface. The built-in channels forward that identity to both pumps, which use it for begin, receive and process spans. The metrics retain the same `messaging.system` attribute.

This adds a public metadata contract without adding required members to existing consumer or channel interfaces, including their .NET Standard 2.0 versions. Custom implementations without the capability retain the internal-bus telemetry default. The new enum values for MQTT, Redis, PostgreSQL and SQL Server use the same transport names as their producers.

### Enriching Metrics
By design, common metric names are not namespaced by application. Metrics should be enriched with service data to enable filtering metrics by application. OpenTelemetry defines [resource attributes](https://opentelemetry.io/docs/specs/semconv/resource/#service) which can be used to identify the service:

| Attribute | Type | Description |
|-----------|------|-------------|
| `service.name` | string | Logical name of the service |
| `service.instance.id` | string | The string ID of the service instance |
| `service.namespace` | string | A namespace for `service.name` |
| `service.version` | string | The version string of the service API or implementation. The format is not defined by these conventions. |

### Managing Cardinality
Spans can have high cardinality attributes like `paramore.brighter.requestid` which can over strain metric collectors such as Prometheus. [adr/0010-brighter-semantic-conventions](0010-brighter-semantic-conventions.md) also allows for custom attributes meaning a denylist defined by Brighter will not catch all attributes which should be filtered out. An allowlist containing the default low cardinality attributes set by Brighter are enough until there is a specific case where span attributes given by the application need passing to metrics.

### Bucket Boundaries
By default, OpenTelemetry [implements the following buckets](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-metrics/#common-metrics) `[ 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 ]` which the application can override with [views](https://opentelemetry.io/docs/concepts/signals/metrics/#views).

### Sampling
Traces are often sampled to control costs ranging from application performance to vendor ingestion. Metrics are not exposed to costs in the same way traces are, with the main lever for histograms being bucket boundaries. [Head-based sampling](https://opentelemetry.io/docs/concepts/sampling/#head-sampling) will distort metrics, applications should prefer to apply a [tail-based sampler](https://opentelemetry.io/docs/concepts/sampling/#tail-sampling) anywhere in the [pipeline](https://opentelemetry.io/docs/collector/architecture/#pipelines) after the Brighter metrics processor.

This means sampling will not reduce costs belonging to tracing before export.

### Meters
[Microsoft docs state](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices):
> Each library or library subcomponent can (and often should) create its own Meter. Consider creating a new Meter rather than reusing an existing one if you anticipate app developers would appreciate being able to easily enable and disable the groups of metrics separately.

All existing spans created by Brighter are will likely want to be toggled together. Therefore, all of these metrics will be under a single meter `Paramore.Brighter`.

### Instrumentation
`AddBrighterInstrumentation` is used to instrument both metrics and traces. A `BrighterMetricsFromTracesProcessor` is registered by default and is only enabled when the brighter meter is registered.

Meter services are resolved when the tracer provider is built, after service registration is complete. `WithTracing` and `WithMetrics` can therefore be registered in either order, including through the combined instrumentation extension. Tracing without metrics remains supported. Consumers and producers must enable the relevant span instrumentation; setting `InstrumentationOptions.All` on `AddConsumers` and `AddProducers` enables the messaging attributes used here.
```
services.AddOpenTelemetry()
  ...
  .WithTracing(builder =>
  {
      // available now
      // modified to register BrighterMetricsFromTracesProcessor by default
	  builder.AddBrighterInstrumentation()

      // proposed equivalent for .SetSampler()
      builder.SetTailSampler(new AlwaysOnSampler()) // Alernatively SetTailSampler<AlwaysOnSampler>()
  })
  .WithMetrics(builder =>
  {
      // proposed - follow the same pattern
	  builder.AddBrighterInstrumentation()
  })
```

## Consequences
- Follow existing standards and patterns where available.
- Applications register Brighter meters to consume metrics
- Brighter uses sensible defaults that are overridable
