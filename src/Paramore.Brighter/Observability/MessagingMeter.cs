#region Licence

/* The MIT License (MIT)
Copyright © 2025 Tim Salva <tim@jtsalva.dev>

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

using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Meter for generating messaging metrics from traces following OpenTelemetry Semantic Conventions 1.29.0
/// Unstable due to depending on experimental parts of the OpenTelemetry spec
/// </summary>
public sealed class MessagingMeter(
    IMeterFactory meterFactory,
    MeterProvider meterProvider)
    : IAmABrighterMessagingMeter
{
    private readonly KeyValuePair<string, object?>[] _serviceAttributes = meterProvider.GetServiceAttributes();

    private readonly Histogram<double> _clientOperationDurationHistogram = meterFactory
        .Create(BrighterSemanticConventions.MeterName)
        .CreateHistogram<double>(
            name: "messaging.client.operation.duration",
            description: "Duration of messaging operation initiated by a producer or consumer client.",
            unit: "s");

    private readonly Counter<int> _sentMessagesCounter = meterFactory
        .Create(BrighterSemanticConventions.MeterName)
        .CreateCounter<int>(
            name: "messaging.client.sent.messages",
            description: "Number of messages producer attempted to send to the broker.",
            unit: "{message}");

    private readonly Counter<int> _consumedMessagesCounter = meterFactory
        .Create(BrighterSemanticConventions.MeterName)
        .CreateCounter<int>(
            name: "messaging.client.consumed.messages",
            description: "Number of messages that were delivered to the application.",
            unit: "{message}");

    private readonly Histogram<double> _processedMessagesHistogram = meterFactory
        .Create(BrighterSemanticConventions.MeterName)
        .CreateHistogram<double>(
            name: "messaging.process.duration",
            description: "Duration of processing operation.",
            unit: "s");

#if NET8_0_OR_GREATER
    private static readonly FrozenSet<string> s_clientOperationDurationHistogramAllowedTags = new[]
#else
    private static readonly HashSet<string> s_clientOperationDurationHistogramAllowedTags = new()
#endif
    {
        BrighterSemanticConventions.MessagingOperationName,
        BrighterSemanticConventions.MessagingSystem,
        BrighterSemanticConventions.ErrorType,
        BrighterSemanticConventions.ConsumerGroupName,
        BrighterSemanticConventions.MessagingDestination,
        BrighterSemanticConventions.MessagingDestinationSubscriptionName,
        BrighterSemanticConventions.MessagingDestinationTemplate,
        BrighterSemanticConventions.MessagingOperationType,
        BrighterSemanticConventions.ServerAddress,
        BrighterSemanticConventions.MessagingDestinationPartitionId,
        BrighterSemanticConventions.ServerPort
#if NET8_0_OR_GREATER
    }.ToFrozenSet();
#else
    };
#endif

#if NET8_0_OR_GREATER
    private static readonly FrozenSet<string> s_sentMessagesCounterAllowedTags = new[]
#else
    private static readonly HashSet<string> s_sentMessagesCounterAllowedTags = new()
#endif
    {
        BrighterSemanticConventions.MessagingOperationName,
        BrighterSemanticConventions.MessagingSystem,
        BrighterSemanticConventions.ErrorType,
        BrighterSemanticConventions.MessagingDestination,
        BrighterSemanticConventions.MessagingDestinationTemplate,
        BrighterSemanticConventions.ServerAddress,
        BrighterSemanticConventions.MessagingDestinationPartitionId,
        BrighterSemanticConventions.ServerPort
#if NET8_0_OR_GREATER
    }.ToFrozenSet();
#else
    };
#endif

#if NET8_0_OR_GREATER
    private static readonly FrozenSet<string> s_consumedMessagesCounterAllowedTags = new[]
#else
    private static readonly HashSet<string> s_consumedMessagesCounterAllowedTags = new()
#endif
    {
        BrighterSemanticConventions.MessagingOperationName,
        BrighterSemanticConventions.MessagingSystem,
        BrighterSemanticConventions.ErrorType,
        BrighterSemanticConventions.ConsumerGroupName,
        BrighterSemanticConventions.MessagingDestination,
        BrighterSemanticConventions.MessagingDestinationSubscriptionName,
        BrighterSemanticConventions.MessagingDestinationTemplate,
        BrighterSemanticConventions.ServerAddress,
        BrighterSemanticConventions.MessagingDestinationPartitionId,
        BrighterSemanticConventions.ServerPort
#if NET8_0_OR_GREATER
    }.ToFrozenSet();
#else
    };
#endif

#if NET8_0_OR_GREATER
    private static readonly FrozenSet<string> s_processedMessagesHistogramAllowedTags = new[]
#else
    private static readonly HashSet<string> s_processedMessagesHistogramAllowedTags = new()
#endif
    {
        BrighterSemanticConventions.MessagingOperationName,
        BrighterSemanticConventions.MessagingSystem,
        BrighterSemanticConventions.ErrorType,
        BrighterSemanticConventions.ConsumerGroupName,
        BrighterSemanticConventions.MessagingDestination,
        BrighterSemanticConventions.MessagingDestinationSubscriptionName,
        BrighterSemanticConventions.MessagingDestinationTemplate,
        BrighterSemanticConventions.ServerAddress,
        BrighterSemanticConventions.MessagingDestinationPartitionId,
        BrighterSemanticConventions.ServerPort
#if NET8_0_OR_GREATER
    }.ToFrozenSet();
#else
    };
#endif

    public void RecordClientOperation(Activity activity)
    {
        _clientOperationDurationHistogram.Record(
            activity.Duration.TotalSeconds,
            [..activity.TagObjects.Filter(s_clientOperationDurationHistogramAllowedTags), .._serviceAttributes]);
    }

    public void AddClientSentMessage(Activity activity)
    {
        _sentMessagesCounter.Add(1, [..activity.TagObjects.Filter(s_sentMessagesCounterAllowedTags), .._serviceAttributes]);
    }

    public void AddClientConsumedMessage(Activity activity)
    {
        _consumedMessagesCounter.Add(1, [..activity.TagObjects.Filter(s_consumedMessagesCounterAllowedTags), .._serviceAttributes]);
    }

    public void RecordProcess(Activity activity)
    {
        _processedMessagesHistogram.Record(
            activity.Duration.TotalSeconds,
            [..activity.TagObjects.Filter(s_processedMessagesHistogramAllowedTags), .._serviceAttributes]);
    }

    public bool Enabled =>
        _clientOperationDurationHistogram.Enabled ||
        _sentMessagesCounter.Enabled ||
        _consumedMessagesCounter.Enabled ||
        _processedMessagesHistogram.Enabled;
}
