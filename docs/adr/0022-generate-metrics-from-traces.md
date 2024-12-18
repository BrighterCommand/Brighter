# 22. Generate Metrics from Traces

Date: 2024-xx-xx

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
The spans defined in [adr/0010-brighter-semantic-conventions](0010-brighter-semantic-conventions.md) lists the operations: send, publish, deposit, and clear. These same units of work can be applied to metrics. Brighter can implement a custom [processor](https://github.com/open-telemetry/opentelemetry-collector/blob/main/processor/README.md) to record span durations into histograms.

Where a span named `<request type> <operation>` is mapped to a histogram in the form `brighter.<operation>.duration` with `<request type>` as an attribute along with any other attributes on the span.

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

All existing spans are related by being command processor operations which applications will likely want to toggle together. Therefore, all of these metrics will be under a single meter.

### Instrumentation
Metrics use the same source as traces, the existing `AddBrighterInstrumentation` can be used for both metrics and traces.
```
services.AddOpenTelemetry()
  ...
  // available now
  .WithTracing(builder =>
  {
	  builder.AddBrighterInstrumentation()
  })
  
  // proposed - follow the same pattern
  .WithMetrics(builder =>
  {
	  builder.AddBrighterInstrumentation()
  })
```


A `BrighterMetricsProcessor` will process all spans with a Brighter source and generate histograms for command processor operations.
```
.WithTracing(builder =>
{
	builder.AddBrighterMetricsFromTracesProcessor()
})
```

## Consequences
- Follow existing standards and patterns where available.
- Applications register Brighter meters to consume metrics
- Brighter uses sensible defaults that are overridable




