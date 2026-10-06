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

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Generates inbox test code from Liquid templates based on <see cref="InboxConfiguration"/>
/// Supports both single and multiple inbox configurations, producing synchronous and asynchronous test variants.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public class InboxGenerator(ILogger<InboxGenerator> logger) : BaseGenerator(logger)
{
    /// <summary>
    /// Generates inbox test files for the configured inbox(es) in the provided <paramref name="configuration"/>.
    /// Uses <see cref="TestConfiguration.Inbox"/> for a single inbox or <see cref="TestConfiguration.Inboxes"/> for multiple.
    /// </summary>
    /// <param name="configuration">The root test configuration containing inbox settings and destination folder.</param>
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
    /// <param name="configuration">The root test configuration containing inbox settings and destination folder.</param>
    /// <returns>The inbox test files this configuration owns.</returns>
    public IReadOnlyList<PlannedFile> Plan(TestConfiguration configuration)
    {
        var planned = new List<PlannedFile>();
        foreach (var suite in Suites(configuration))
        {
            planned.AddRange(Plan(configuration, suite.Prefix, suite.TemplateFolderName, suite.Ignore));
        }

        return planned;
    }

    /// <summary>
    /// Describes every rendering this generator performs for <paramref name="configuration"/>:
    /// the Sync and Async suites of each configured inbox.
    /// </summary>
    /// <remarks>
    /// Both <see cref="GenerateAsync(TestConfiguration)"/> and <see cref="Plan(TestConfiguration)"/>
    /// walk this list, so the files the generator writes and the files it claims to own are one
    /// description rather than two that can drift apart.
    /// </remarks>
    private IReadOnlyList<GenerationSuite> Suites(TestConfiguration configuration)
    {
        var suites = new List<GenerationSuite>();

        if (configuration.Inbox != null)
        {
            if (string.IsNullOrEmpty(configuration.Inbox.Prefix))
            {
                logger.LogWarning("Inbox configuration has no prefix; skipping");
            }
            else
            {
                suites.AddRange(SuitesFor(
                    configuration,
                    configuration.Inbox,
                    folderName: configuration.Inbox.Prefix,
                    modelPrefix: $".{configuration.Inbox.Prefix}"));
            }
        }
        else if (configuration.Inboxes != null)
        {
            foreach (var (key, inboxConfiguration) in configuration.Inboxes)
            {
                logger.LogInformation("Describing inbox test suites for {InboxName}", key);
                var folderName = string.IsNullOrEmpty(inboxConfiguration.Prefix)
                    ? key
                    : inboxConfiguration.Prefix;

                // Templates build a namespace suffix from the model's prefix, which is dot-qualified
                // where the destination folder name is not.
                suites.AddRange(SuitesFor(
                    configuration,
                    inboxConfiguration,
                    folderName: folderName,
                    modelPrefix: $".{folderName}"));
            }
        }
        else
        {
            logger.LogInformation("No inbox configured");
        }

        return suites;
    }

    /// <summary>
    /// The suites - Sync and Async - rendered for a single inbox.
    /// </summary>
    /// <param name="configuration">The root configuration the model inherits unset values from.</param>
    /// <param name="inboxConfiguration">The inbox whose suites are described.</param>
    /// <param name="folderName">The destination folder name for this inbox.</param>
    /// <param name="modelPrefix">The prefix the templates should read from the model.</param>
    /// <returns>The suites for the inbox.</returns>
    /// <remarks>
    /// Both suites share one copy of the configuration, carrying the prefix the templates need
    /// and the values inherited from the root. The caller's own configuration object is left alone,
    /// so describing the work does not perform part of it - and because the inheritance happens
    /// here, the model a plan reasons about is the model a generation renders.
    /// </remarks>
    private static IEnumerable<GenerationSuite> SuitesFor(
        TestConfiguration configuration,
        InboxConfiguration inboxConfiguration, string folderName, string modelPrefix)
    {
        var model = inboxConfiguration.WithPrefix(modelPrefix).WithDefaultsFrom(configuration);

        if (!string.IsNullOrEmpty(model.InboxProvider))
        {
            yield return new GenerationSuite(
                Path.Combine("Inbox", folderName, "Generated", "Sync"),
                Path.Combine("Inbox", "Sync"),
                model
            );
        }

        if (!string.IsNullOrEmpty(model.InboxProviderAsync))
        {
            yield return new GenerationSuite(
                Path.Combine("Inbox", folderName, "Generated", "Async"),
                Path.Combine("Inbox", "Async"),
                model
            );
        }
    }
}
