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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Produces a human-readable diagnostic report of all configured pipelines.
/// Logs a summary line at Information level with counts, and detailed
/// per-pipeline descriptions at Debug level.
/// </summary>
/// <param name="logger">The logger to write diagnostic output to.</param>
/// <param name="pipelineBuilder">The pipeline builder used to describe handler pipelines.</param>
/// <param name="publications">Optional publications to describe.</param>
/// <param name="subscriptions">Optional subscriptions to describe.</param>
public class PipelineDiagnosticWriter(
    ILogger logger,
    PipelineBuilder<IRequest> pipelineBuilder,
    IEnumerable<Publication>? publications = null,
    IEnumerable<Subscription>? subscriptions = null) : IAmAPipelineDiagnosticWriter
{
    private readonly ILogger _logger = logger;
    private readonly PipelineBuilder<IRequest> _pipelineBuilder = pipelineBuilder;
    private readonly IEnumerable<Publication>? _publications = publications;
    private readonly IEnumerable<Subscription>? _subscriptions = subscriptions;

    /// <inheritdoc />
    public void Describe()
    {
        var descriptions = _pipelineBuilder.Describe().ToList();
        var publicationList = _publications?.ToList() ?? new List<Publication>();
        var subscriptionList = _subscriptions?.ToList() ?? new List<Subscription>();

        LogSummary(descriptions.Count, publicationList.Count, subscriptionList.Count);
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
            _logger.LogInformation("Brighter: {Summary} configured", string.Join(", ", parts));
    }
}
