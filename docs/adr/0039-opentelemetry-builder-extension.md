# 39. OpenTelemetry Builder Extension

Date: 2026-02-18

## Status

Proposed

## Context

Brighter's `Extensions.Diagnostics` package provides separate `TracerProviderBuilder.AddBrighterInstrumentation()` and `MeterProviderBuilder.AddBrighterInstrumentation()` extension methods. Users must call both individually inside `.WithTracing()` and `.WithMetrics()` blocks:

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddBrighterInstrumentation())
    .WithMetrics(m => m.AddBrighterInstrumentation());
```

The OpenTelemetry .NET SDK provides `OpenTelemetryBuilder` (from `OpenTelemetry.Extensions.Hosting`) as the recommended single entry point for instrumentation libraries to wire signals. Many instrumentation packages in the ecosystem offer a combined extension on this builder.

## Decision

Add a new package `Paramore.Brighter.Extensions.OpenTelemetry` that provides a single extension method on `OpenTelemetryBuilder`:

```csharp
services.AddOpenTelemetry()
    .AddBrighterInstrumentation()
    .WithTracing(t => t.AddOtlpExporter())
    .WithMetrics(m => m.AddOtlpExporter());
```

The method delegates to the existing `TracerProviderBuilder` and `MeterProviderBuilder` extensions from `Extensions.Diagnostics`. The `.WithTracing()` and `.WithMetrics()` callbacks are additive, so users can chain additional calls for exporters and other instrumentation.

The package targets `net8.0;net9.0;net10.0` only (not `netstandard2.0`) because `OpenTelemetryBuilder` requires the .NET Generic Host. No exporter dependencies are bundled — users choose their own.

## Consequences

- Users get a simpler setup path for the common case of enabling both tracing and metrics.
- The existing per-signal extensions in `Extensions.Diagnostics` remain available for users who need finer control or don't use the generic host.
- One additional NuGet package to publish and maintain, though it contains minimal code and delegates entirely to `Extensions.Diagnostics`.
