#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Extensions.OpenTelemetry;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

internal sealed class InMemoryMetricsFromTraces : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly MeterProvider _meterProvider;
    private readonly TracerProvider _tracerProvider;
    private readonly List<Metric> _metrics = [];
    private readonly Activity? _previousActivity = Activity.Current;

    public IAmABrighterTracer Tracer { get; }
    public List<Activity> Spans { get; } = [];

    public InMemoryMetricsFromTraces(bool tracingFirst = false, bool combinedInstrumentation = false)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var builder = services.AddOpenTelemetry();

        if (combinedInstrumentation)
        {
            builder.AddBrighterInstrumentation()
                .WithTracing(tracing => tracing.AddInMemoryExporter(Spans))
                .WithMetrics(metrics => metrics.AddInMemoryExporter(_metrics));
        }
        else if (tracingFirst)
        {
            builder.WithTracing(tracing => tracing.AddBrighterInstrumentation().AddInMemoryExporter(Spans));
            builder.WithMetrics(metrics => metrics.AddBrighterInstrumentation().AddInMemoryExporter(_metrics));
        }
        else
        {
            builder.WithMetrics(metrics => metrics.AddBrighterInstrumentation().AddInMemoryExporter(_metrics));
            builder.WithTracing(tracing => tracing.AddBrighterInstrumentation().AddInMemoryExporter(Spans));
        }

        _services = services.BuildServiceProvider();
        if (tracingFirst || combinedInstrumentation)
        {
            _tracerProvider = _services.GetRequiredService<TracerProvider>();
            _meterProvider = _services.GetRequiredService<MeterProvider>();
        }
        else
        {
            _meterProvider = _services.GetRequiredService<MeterProvider>();
            _tracerProvider = _services.GetRequiredService<TracerProvider>();
        }
        Tracer = _services.GetRequiredService<IAmABrighterTracer>();
    }

    public bool Flush() => _tracerProvider.ForceFlush() && _meterProvider.ForceFlush();

    public long CounterTotal(string name)
    {
        var metric = _metrics.LastOrDefault(metric => metric.Name == name);
        if (metric is null) return 0;

        long total = 0;
        foreach (var point in metric.GetMetricPoints())
            total += point.GetSumLong();
        return total;
    }

    public long HistogramCount(string name)
    {
        var metric = _metrics.LastOrDefault(metric => metric.Name == name);
        if (metric is null) return 0;

        long count = 0;
        foreach (var point in metric.GetMetricPoints())
            count += point.GetHistogramCount();
        return count;
    }

    public List<object?> TagValues(string metricName, string tagName)
    {
        var values = new List<object?>();
        var metric = _metrics.LastOrDefault(metric => metric.Name == metricName);
        if (metric is null) return values;

        foreach (var point in metric.GetMetricPoints())
        {
            foreach (var tag in point.Tags)
            {
                if (tag.Key == tagName)
                    values.Add(tag.Value);
            }
        }
        return values;
    }

    public void Dispose()
    {
        _services.Dispose();
        Activity.Current = _previousActivity;
    }
}
