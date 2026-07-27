#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Produces a human-readable diagnostic report of all configured pipelines.
/// Logs a summary line at Information level with counts, and detailed
/// per-pipeline descriptions at Debug level.
/// </summary>
/// <param name="logger">The logger to write diagnostic output to.</param>
/// <param name="pipelineBuilder">The pipeline builder used to describe handler pipelines.</param>
/// <param name="mapperRegistry">Optional mapper registry for resolving publication/subscription mapper info.</param>
/// <param name="publications">Optional publications to describe.</param>
/// <param name="subscriptions">Optional subscriptions to describe.</param>
public class PipelineDiagnosticWriter(
    ILogger logger,
    PipelineBuilder<IRequest> pipelineBuilder,
    MessageMapperRegistry? mapperRegistry = null,
    IEnumerable<Publication>? publications = null,
    IEnumerable<Subscription>? subscriptions = null) : IAmAPipelineDiagnosticWriter, IDisposable
{
    //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange:
    //an owner and the container disposing concurrently then dispose the registry exactly once
    private int _disposed;

    /// <summary>
    /// Disposes the mapper registry this writer built to describe publication transforms.
    /// </summary>
    /// <remarks>
    /// The writer is a singleton that owns its diagnostic-time <see cref="MessageMapperRegistry"/>; the
    /// container disposes the writer at shutdown. Cascading to the registry drains the mapper factory (and
    /// any scope it holds) rather than retaining it until the process exits. Idempotent.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        mapperRegistry?.Dispose();
    }

    /// <inheritdoc />
    public void Describe()
    {
        var descriptions = pipelineBuilder.Describe().ToList();
        var publicationList = publications?.ToList() ?? new List<Publication>();
        var subscriptionList = subscriptions?.ToList() ?? new List<Subscription>();

        LogSummary(descriptions.Count, publicationList.Count, subscriptionList.Count);
        LogHandlerPipelines(descriptions);
        LogPublications(publicationList);
        LogSubscriptions(subscriptionList);
    }

    private void LogSummary(int handlerCount, int publicationCount, int subscriptionCount)
    {
        var parts = new List<string>();

        if (handlerCount > 0)
            parts.Add($"{handlerCount} handler pipeline{(handlerCount != 1 ? "s" : "")}");
        if (publicationCount > 0)
            parts.Add($"{publicationCount} publication{(publicationCount != 1 ? "s" : "")}");
        if (subscriptionCount > 0)
            parts.Add($"{subscriptionCount} subscription{(subscriptionCount != 1 ? "s" : "")}");

        if (parts.Count > 0)
            logger.LogInformation("Brighter: {Summary} configured", string.Join(", ", parts));
    }

    private void LogHandlerPipelines(List<HandlerPipelineDescription> descriptions)
    {
        if (descriptions.Count == 0) return;

        logger.LogDebug("=== Handler Pipelines ===");

        foreach (var d in descriptions)
        {
            var asyncLabel = d.IsAsync ? "async" : "sync";
            logger.LogDebug("  {HandlerName} ({AsyncLabel})", d.HandlerType.Name, asyncLabel);

            var steps = d.BeforeSteps
                .Select(s => $"[{s.AttributeType.Name}({s.Step})]");
            var chain = string.Join(" → ", steps) +
                        (d.BeforeSteps.Count > 0 ? " → " : "") +
                        d.HandlerType.Name;

            logger.LogDebug("    Pipeline: {Chain}", chain);
        }
    }

    private void LogPublications(List<Publication> publicationList)
    {
        if (publicationList.Count == 0) return;

        logger.LogDebug("=== Publications (Outgoing) ===");

        foreach (var pub in publicationList)
        {
            var requestTypeName = pub.RequestType?.Name ?? "(no RequestType)";
            var topic = pub.Topic?.Value ?? "(no topic)";
            logger.LogDebug("  {RequestType} → {Topic}", requestTypeName, topic);

            if (mapperRegistry != null && pub.RequestType != null)
            {
                var transformDesc = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, pub.RequestType);
                if (transformDesc != null)
                {
                    var mapperLabel = transformDesc.IsDefaultMapper ? "default" : "custom";
                    logger.LogDebug("    Mapper:     {MapperType} ({MapperLabel})",
                        transformDesc.MapperType.Name, mapperLabel);

                    var transforms = transformDesc.WrapTransforms;
                    if (transforms.Count > 0)
                    {
                        var transformChain = string.Join(", ",
                            transforms.Select(t => $"[{t.AttributeType.Name}({t.Step})]"));
                        logger.LogDebug("    Transforms: {Transforms}", transformChain);
                    }
                    else
                    {
                        logger.LogDebug("    Transforms: (none)");
                    }
                }
            }
        }
    }

    private void LogSubscriptions(List<Subscription> subscriptionList)
    {
        if (subscriptionList.Count == 0) return;

        logger.LogDebug("=== Subscriptions (Incoming) ===");

        foreach (var sub in subscriptionList)
        {
            logger.LogDebug("  {SubscriptionName} ({PumpType})", sub.Name, sub.MessagePumpType);
            logger.LogDebug("    Channel:  {ChannelName} → {RoutingKey}",
                sub.ChannelName, sub.RoutingKey);
        }
    }
}
