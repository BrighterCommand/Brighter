#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Generates outbox test code from Liquid templates based on <see cref="OutboxConfiguration"/>.
/// Supports both single and multiple outbox configurations, producing synchronous and asynchronous test variants,
/// and conditionally skipping tests for outboxes that do not support transactions.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public class OutboxGenerator(ILogger<OutboxGenerator> logger) : BaseGenerator(logger)
{
    /// <summary>
    /// Generates outbox test files for the configured outbox(es) in the provided <paramref name="configuration"/>.
    /// Uses <see cref="TestConfiguration.Outbox"/> for a single outbox or <see cref="TestConfiguration.Outboxes"/> for multiple.
    /// </summary>
    /// <param name="configuration">The root test configuration containing outbox settings and destination folder.</param>
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        foreach (var suite in Suites(configuration))
        {
            await GenerateAsync(
                configuration,
                suite.Prefix,
                suite.TemplateFolderName,
                suite.Model,
                suite.Ignore
            );
        }
    }

    /// <summary>
    /// Computes every file <see cref="GenerateAsync(TestConfiguration)"/> would write for
    /// <paramref name="configuration"/>, without writing any of them.
    /// </summary>
    /// <param name="configuration">The root test configuration containing outbox settings and destination folder.</param>
    /// <returns>The outbox test files this configuration owns.</returns>
    public IReadOnlyList<PlannedFile> Plan(TestConfiguration configuration)
    {
        var planned = new List<PlannedFile>();
        foreach (var suite in Suites(configuration))
        {
            planned.AddRange(Plan(configuration, suite.Prefix, suite.TemplateFolderName, suite.Ignore));
        }

        return planned;
    }

    // The one description GenerateAsync and Plan both walk: the Sync, Async, Causation,
    // OutstandingCount and optional TopicCounting suites of each outbox.
    private IReadOnlyList<GenerationSuite> Suites(TestConfiguration configuration)
    {
        var suites = new List<GenerationSuite>();

        if (configuration.Outbox != null)
        {
            suites.AddRange(SuitesFor(
                configuration,
                configuration.Outbox,
                folderName: configuration.Outbox.Prefix,
                modelPrefix: configuration.Outbox.Prefix));
        }
        else if (configuration.Outboxes != null)
        {
            foreach (var (key, outboxConfiguration) in configuration.Outboxes)
            {
                logger.LogInformation("Describing outbox test suites for {OutboxName}", key);
                var folderName = string.IsNullOrEmpty(outboxConfiguration.Prefix)
                    ? key
                    : outboxConfiguration.Prefix;

                var namespaceSuffix = $".{folderName}";
                suites.AddRange(SuitesFor(
                    configuration,
                    outboxConfiguration,
                    folderName: folderName,
                    modelPrefix: namespaceSuffix));
            }
        }
        else
        {
            logger.LogInformation("No outbox configured");
        }

        return suites;
    }

    private static IEnumerable<GenerationSuite> SuitesFor(
        TestConfiguration configuration,
        OutboxConfiguration outboxConfiguration, string folderName, string modelPrefix)
    {
        var model = outboxConfiguration.WithPrefix(modelPrefix).WithDefaultsFrom(configuration);

        foreach (var variant in new[] { "Sync", "Async", "Causation" })
        {
            yield return new GenerationSuite(
                Path.Combine("Outbox", folderName, "Generated", variant),
                Path.Combine("Outbox", variant),
                model,
                filename => SkipTest(model, filename)
            );
        }

        yield return new GenerationSuite(
            Path.Combine("Outbox", folderName, "Generated", "OutstandingCount"),
            Path.Combine("Outbox", "OutstandingCount"),
            model,
            _ => false
        );

        if (model.SupportsTopicCounting)
        {
            yield return new GenerationSuite(
                Path.Combine("Outbox", folderName, "Generated", "TopicCounting"),
                Path.Combine("Outbox", "TopicCounting"),
                model,
                _ => false
            );
        }
    }

    private static bool SkipTest(OutboxConfiguration outboxConfiguration, string fileName)
    {
        if (
            !outboxConfiguration.SupportsTransactions
            && fileName.Contains("Transaction", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            return true;
        }

        return false;
    }

}
