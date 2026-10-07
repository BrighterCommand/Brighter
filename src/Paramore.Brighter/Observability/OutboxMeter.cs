#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Records Outbox metrics directly on OpenTelemetry instruments, independently of tracing and sampling.
/// </summary>
public sealed partial class OutboxMeter : IAmABrighterOutboxMeter, IDisposable
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<OutboxMeter>();

    private static readonly double[] s_publishDurationBoundaries =
    [
        0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1,
        2.5, 5, 7.5, 10, 30, 60, 300, 600
    ];

    private readonly Meter _meter;
    private readonly Counter<long> _addedMessages;
    private readonly Counter<long> _clearedMessages;
    private readonly Histogram<double> _publishDuration;
    private readonly KeyValuePair<string, object?>[] _serviceAttributes;

    /// <summary>
    /// Creates an <see cref="OutboxMeter"/>. Pass a <see cref="MeterProvider"/> built through DI
    /// (<c>AddOpenTelemetry()</c>) to include service-resource attributes on every recording.
    /// When null, only the two Outbox-specific attributes are recorded.
    /// </summary>
    public OutboxMeter(MeterProvider? meterProvider = null)
    {
        _serviceAttributes = meterProvider?.GetServiceAttributes() ?? [];
        _meter = new Meter(BrighterSemanticConventions.MeterName);

        _addedMessages = _meter.CreateCounter<long>(
            BrighterSemanticConventions.OutboxAddedMessages,
            unit: "{message}",
            description: "Number of messages written to the Outbox.");

        _clearedMessages = _meter.CreateCounter<long>(
            BrighterSemanticConventions.OutboxClearedMessages,
            unit: "{message}",
            description: "Number of messages recorded as dispatched from the Outbox.");

        _publishDuration = _meter.CreateHistogram<double>(
            BrighterSemanticConventions.OutboxPublishDuration,
            unit: "s",
            description: "Time a message waited between being created for the Outbox and being recorded as dispatched.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_publishDurationBoundaries });
    }

    /// <inheritdoc />
    public bool Enabled =>
        _addedMessages.Enabled || _clearedMessages.Enabled || _publishDuration.Enabled;

    /// <inheritdoc />
    public void AddMessageAdded(RoutingKey destination)
    {
        try
        {
            var tags = BuildTags(destination);
            _addedMessages.Add(1, in tags);
        }
        catch (Exception ex)
        {
            Log.OutboxMetricsFault(s_logger, ex);
        }
    }

    /// <inheritdoc />
    public void AddMessageCleared(RoutingKey destination, OutboxClearSource clearSource)
    {
        try
        {
            var tags = BuildTags(destination, clearSource);
            _clearedMessages.Add(1, in tags);
        }
        catch (Exception ex)
        {
            Log.OutboxMetricsFault(s_logger, ex);
        }
    }

    /// <inheritdoc />
    public void RecordPublishDuration(TimeSpan wait, RoutingKey destination, OutboxClearSource clearSource)
    {
        try
        {
            var seconds = Math.Max(0, wait.TotalSeconds);
            var tags = BuildTags(destination, clearSource);
            _publishDuration.Record(seconds, in tags);
        }
        catch (Exception ex)
        {
            Log.OutboxMetricsFault(s_logger, ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();

    private TagList BuildTags(RoutingKey destination)
    {
        var tags = new TagList
        {
            { BrighterSemanticConventions.MessagingDestination, destination.Value }
        };
        foreach (var attr in _serviceAttributes)
            tags.Add(attr.Key, attr.Value);
        return tags;
    }

    private TagList BuildTags(RoutingKey destination, OutboxClearSource clearSource)
    {
        var tags = new TagList
        {
            { BrighterSemanticConventions.MessagingDestination, destination.Value },
            { BrighterSemanticConventions.OutboxClearSource, clearSource.ToClearSourceName() }
        };
        foreach (var attr in _serviceAttributes)
            tags.Add(attr.Key, attr.Value);
        return tags;
    }

    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Warning,
            "Outbox metrics fault; the metric has been dropped but the Outbox operation was unaffected")]
        public static partial void OutboxMetricsFault(ILogger logger, Exception ex);
    }
}
