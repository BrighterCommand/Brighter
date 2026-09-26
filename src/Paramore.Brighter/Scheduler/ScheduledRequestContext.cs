#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Text.Json;
using System.Text.Json.Serialization;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Scheduler;

/// <summary>
/// The portable message metadata and trace context captured for a scheduled request.
/// </summary>
/// <remarks>
/// Unrelated context bag entries and runtime services are deliberately excluded. Header and CloudEvents
/// extension values retain their types. Supported values are null, strings, characters, booleans,
/// integral types, finite floating-point numbers, decimals, Guid, DateTime, DateTimeOffset, TimeSpan,
/// Uri and byte arrays. Dictionaries must use ordinal or ordinal-ignore-case key comparison.
/// Unsupported metadata fails when scheduling; it is never silently discarded.
/// </remarks>
public class ScheduledRequestContext
{
    /// <summary>The dynamic message headers.</summary>
    /// <value>The header names and supported values, or null when absent.</value>
    [JsonConverter(typeof(ScheduledRequestContextBagConverter))]
    public Dictionary<string, object>? Headers { get; set; }

    /// <summary>The additional CloudEvents properties.</summary>
    /// <value>The extension names and supported values, or null when absent.</value>
    [JsonConverter(typeof(ScheduledRequestContextBagConverter))]
    public Dictionary<string, object>? CloudEventsAdditionalProperties { get; set; }

    /// <summary>The message partition key.</summary>
    /// <value>The <see cref="Brighter.PartitionKey"/> used for routing, or null when absent.</value>
    public PartitionKey? PartitionKey { get; set; }

    /// <summary>The job identifier.</summary>
    /// <value>The job <see cref="Id"/>, or null when absent.</value>
    public Id? JobId { get; set; }

    /// <summary>The workflow identifier.</summary>
    /// <value>The workflow <see cref="Id"/>, or null when absent.</value>
    public Id? WorkflowId { get; set; }

    /// <summary>The identifier of the request that caused this operation.</summary>
    /// <value>The originating request <see cref="Id"/>, or null when absent.</value>
    public Id? CausationId { get; set; }

    /// <summary>The parent span identifier.</summary>
    /// <value>The serialized activity identifier, or null when tracing is absent.</value>
    public string? TraceParent { get; set; }

    /// <summary>The vendor-specific trace state.</summary>
    /// <value>The serialized trace state, or null when absent.</value>
    public string? TraceState { get; set; }

    /// <summary>The trace baggage propagated to the scheduled operation.</summary>
    /// <value>The baggage names and values, or null when tracing is absent.</value>
    public Dictionary<string, string?>? Baggage { get; set; }

    /// <summary>
    /// Captures the supported request metadata as an independent JSON snapshot.
    /// </summary>
    /// <param name="context">The request context to capture.</param>
    /// <returns>The serialized snapshot, or null when no context was supplied.</returns>
    /// <exception cref="JsonException">The metadata contains an unsupported value or key comparer.</exception>
    public static string? Serialize(IRequestContext? context)
    {
        if (context == null)
            return null;

        var span = context.Span;
        var snapshot = new ScheduledRequestContext
        {
            Headers = context.GetHeaders(),
            CloudEventsAdditionalProperties = context.GetCloudEventAdditionalProperties(),
            PartitionKey = context.Bag.ContainsKey(RequestContextBagNames.PartitionKey) ? context.GetPartitionKey() : null,
            JobId = context.GetJobId(),
            WorkflowId = context.GetWorkflowId(),
            CausationId = context.Bag.TryGetValue(RequestContextBagNames.CausationId, out var causationId) ? causationId as Id : null,
            TraceParent = span?.Id,
            TraceState = span?.TraceStateString
        };

        if (span != null)
        {
            snapshot.Baggage = new Dictionary<string, string?>();
            foreach (var entry in span.Baggage)
            {
                // Activity enumerates the most recently added value first.
                if (!snapshot.Baggage.ContainsKey(entry.Key))
                    snapshot.Baggage.Add(entry.Key, entry.Value);
            }
        }

        return JsonSerializer.Serialize(snapshot, JsonSerialisationOptions.Options);
    }

    internal RequestContext Restore()
    {
        var context = new RequestContext();
        if (Headers != null)
            context.Bag[RequestContextBagNames.Headers] = Headers;
        if (CloudEventsAdditionalProperties != null)
            context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = CloudEventsAdditionalProperties;
        if (PartitionKey != null)
            context.Bag[RequestContextBagNames.PartitionKey] = PartitionKey;
        if (JobId != null)
            context.Bag[RequestContextBagNames.JobId] = JobId;
        if (WorkflowId != null)
            context.Bag[RequestContextBagNames.WorkflowId] = WorkflowId;
        if (CausationId != null)
            context.Bag[RequestContextBagNames.CausationId] = CausationId;
        return context;
    }
}
