#region Licence

/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Paramore.Brighter.Extensions.Diagnostics;

namespace Paramore.Brighter.Extensions.OpenTelemetry;

/// <summary>
/// Extension methods for <see cref="global::OpenTelemetry.OpenTelemetryBuilder"/> that add
/// Brighter tracing and metrics instrumentation in a single call.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
    /// <summary>
    /// Adds Brighter instrumentation for both tracing and metrics to the OpenTelemetry builder.
    /// </summary>
    /// <remarks>
    /// This is a convenience method that calls
    /// <see cref="BrighterTracerBuilderExtensions.AddBrighterInstrumentation(TracerProviderBuilder)"/> and
    /// <see cref="BrighterMetricsBuilderExtensions.AddBrighterInstrumentation(MeterProviderBuilder)"/>.
    /// The <c>.WithTracing()</c> and <c>.WithMetrics()</c> callbacks are additive, so users can chain
    /// additional calls to add exporters and other instrumentation.
    /// </remarks>
    /// <param name="builder">The <see cref="global::OpenTelemetry.OpenTelemetryBuilder"/> to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    public static global::OpenTelemetry.OpenTelemetryBuilder AddBrighterInstrumentation(
        this global::OpenTelemetry.OpenTelemetryBuilder builder)
    {
        builder.WithTracing(tracing => tracing.AddBrighterInstrumentation());
        builder.WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        return builder;
    }
}
