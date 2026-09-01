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
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Generates inbox test code from Liquid templates based on <see cref="InboxConfiguration"/u003e.
/// Supports both single and multiple inbox configurations, producing synchronous and asynchronous test variants.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public class InboxGenerator(ILogger<InboxGenerator> logger) : BaseGenerator(logger)
{
    /// <summary>
    /// Generates inbox test files for the configured inbox(es) in the provided <paramref name="configuration"/u003e.
    /// Uses <see cref="TestConfiguration.Inbox"/u003e for a single inbox or <see cref="TestConfiguration.Inboxes"/u003e for multiple.
    /// </summary>
    /// <param name="configuration">The root test configuration containing inbox settings and destination folder.</param>
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        if (configuration.Inbox != null)
        {
            await GenerateInboxAsync(configuration, configuration.Inbox);
        }
        else if (configuration.Inboxes != null)
        {
            foreach (var (key, inboxConfiguration) in configuration.Inboxes)
            {
                logger.LogInformation("Generating inbox test for {InboxName}", key);
                await GenerateInboxAsync(configuration, inboxConfiguration, key);
            }
        }
        else
        {
            logger.LogInformation("No inbox configured");
        }
    }

    private async Task GenerateInboxAsync(
        TestConfiguration configuration,
        InboxConfiguration inboxConfiguration,
        string? key = null)
    {
        var prefix = inboxConfiguration.Prefix;
        if (string.IsNullOrEmpty(prefix))
        {
            prefix = key;
        }

        if (string.IsNullOrEmpty(prefix))
        {
            logger.LogWarning("Inbox configuration has no prefix and no key was provided; skipping");
            return;
        }

        inboxConfiguration.Prefix = $".{prefix}";

        if (!string.IsNullOrEmpty(inboxConfiguration.InboxProvider))
        {
            await GenerateAsync(
                configuration,
                Path.Combine("Inbox", prefix, "Generated", "Sync"),
                Path.Combine("Inbox", "Sync"),
                inboxConfiguration
            );
        }

        if (!string.IsNullOrEmpty(inboxConfiguration.InboxProviderAsync))
        {
            await GenerateAsync(
                configuration,
                Path.Combine("Inbox", prefix, "Generated", "Async"),
                Path.Combine("Inbox", "Async"),
                inboxConfiguration
            );
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Applies default values from the root <paramref name="configuration"/u003e to the
    /// <see cref="InboxConfiguration"/u003e model when its own values are not set,
    /// including <see cref="InboxConfiguration.Namespace"/u003e.
    /// </remarks>
    protected override Task GenerateAsync(
        TestConfiguration configuration,
        string prefix,
        string templateFolderName,
        object model,
        Func<string, bool>? ignore = null)
    {
        if (model is InboxConfiguration inboxConfiguration)
        {
            if (string.IsNullOrEmpty(inboxConfiguration.Namespace))
            {
                inboxConfiguration.Namespace = configuration.Namespace;
            }
        }

        return base.GenerateAsync(configuration, prefix, templateFolderName, model, ignore);
    }
}
