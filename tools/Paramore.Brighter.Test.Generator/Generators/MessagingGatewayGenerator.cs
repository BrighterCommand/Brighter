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
/// Generates messaging gateway test code from Liquid templates based on <see cref="MessagingGatewayConfiguration"/>.
/// Supports both single and multiple gateway configurations, producing Reactor and Proactor test variants,
/// and conditionally skipping tests for unsupported gateway features.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public class MessagingGatewayGenerator(ILogger<MessagingGatewayGenerator> logger)
    : BaseGenerator(logger)
{
    /// <summary>
    /// Generates messaging gateway test files for the configured gateway(s) in the provided <paramref name="configuration"/>.
    /// Uses <see cref="TestConfiguration.MessagingGateway"/> for a single gateway or <see cref="TestConfiguration.MessagingGateways"/> for multiple.
    /// </summary>
    /// <param name="configuration">The root test configuration containing messaging gateway settings and destination folder.</param>
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
    /// <param name="configuration">The root test configuration containing messaging gateway settings and destination folder.</param>
    /// <returns>The messaging gateway test files this configuration owns.</returns>
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
    /// the Reactor and Proactor suites of each configured gateway.
    /// </summary>
    /// <remarks>
    /// Both <see cref="GenerateAsync(TestConfiguration)"/> and <see cref="Plan(TestConfiguration)"/>
    /// walk this list, so the files the generator writes and the files it claims to own are one
    /// description rather than two that can drift apart.
    /// </remarks>
    private IReadOnlyList<GenerationSuite> Suites(TestConfiguration configuration)
    {
        var suites = new List<GenerationSuite>();

        if (configuration.MessagingGateway != null)
        {
            suites.AddRange(SuitesFor(
                configuration.MessagingGateway,
                folderName: configuration.MessagingGateway.Prefix,
                modelPrefix: configuration.MessagingGateway.Prefix));
        }
        else if (configuration.MessagingGateways != null)
        {
            foreach (var (key, messagingGatewayConfiguration) in configuration.MessagingGateways)
            {
                logger.LogInformation("Planning messaging gateway test for {GatewayName}", key);
                var folderName = string.IsNullOrEmpty(messagingGatewayConfiguration.Prefix)
                    ? key
                    : messagingGatewayConfiguration.Prefix;

                // Templates build a namespace suffix from the model's prefix, which is dot-qualified
                // where the destination folder name is not.
                suites.AddRange(SuitesFor(
                    messagingGatewayConfiguration,
                    folderName: folderName,
                    modelPrefix: $".{folderName}"));
            }
        }
        else
        {
            logger.LogInformation("No messaging gateway configured");
        }

        return suites;
    }

    /// <summary>
    /// The two suites - Reactor and Proactor - rendered for a single messaging gateway.
    /// </summary>
    /// <param name="messagingGatewayConfiguration">The gateway whose suites are described.</param>
    /// <param name="folderName">The destination folder name for this gateway.</param>
    /// <param name="modelPrefix">The prefix the templates should read from the model.</param>
    /// <returns>The suites for the gateway.</returns>
    /// <remarks>
    /// Both suites share one copy of the configuration, carrying the prefix the templates need. The
    /// caller's own configuration object is left alone, so describing the work does not perform part
    /// of it.
    /// </remarks>
    private static IEnumerable<GenerationSuite> SuitesFor(
        MessagingGatewayConfiguration messagingGatewayConfiguration, string folderName, string modelPrefix)
    {
        var model = messagingGatewayConfiguration.WithPrefix(modelPrefix);

        foreach (var variant in new[] { "Reactor", "Proactor" })
        {
            yield return new GenerationSuite(
                Path.Combine("MessagingGateway", folderName, "Generated", variant),
                Path.Combine("MessagingGateway", variant),
                model,
                filename => SkipTest(model, filename)
            );
        }
    }

    /// <summary>
    /// Determines whether a test template should be skipped based on the gateway's feature support flags.
    /// </summary>
    /// <param name="configuration">The messaging gateway configuration describing supported features.</param>
    /// <param name="fileName">The template file name to evaluate.</param>
    /// <returns><c>true</c> if the template should be skipped; otherwise, <c>false</c>.</returns>
    private static bool SkipTest(MessagingGatewayConfiguration configuration, string fileName)
    {
        if (
            !configuration.HasSupportToPublishConfirmation
            && fileName.Contains("confirming_posting")
        )
        {
            return true;
        }

        if (!configuration.HasSupportToDelayedMessages && fileName.Contains("delayed_message"))
        {
            return true;
        }

        if (!configuration.HasSupportToDelayedMessages && fileName.Contains("with_delay"))
        {
            return true;
        }

        if (!configuration.HasSupportToDeadLetterQueue && fileName.Contains("dead_letter_queue"))
        {
            return true;
        }

        if (
            !configuration.HasSupportToValidateBrokerExistence
            && fileName.Contains("no_broker_created")
        )
        {
            return true;
        }

        if (!configuration.HasSupportToRequeue && fileName.Contains("requeuing"))
        {
            return true;
        }

        if (
            !configuration.HasSupportToValidateInfrastructure
            && (fileName.Contains("assume_channel") || fileName.Contains("validate_channel"))
        )
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Applies default values from the root <paramref name="configuration"/> to the
    /// <see cref="MessagingGatewayConfiguration"/> model when its own values are not set,
    /// including <see cref="MessagingGatewayConfiguration.MessageBuilder"/>,
    /// <see cref="MessagingGatewayConfiguration.Namespace"/>, and <see cref="MessagingGatewayConfiguration.MessageAssertion"/>.
    /// </remarks>
    protected override Task GenerateAsync(
        TestConfiguration configuration,
        string prefix,
        string templateFolderName,
        object model,
        Func<string, bool>? ignore = null
    )
    {
        if (model is MessagingGatewayConfiguration messagingGatewayConfiguration)
        {
            if (string.IsNullOrEmpty(messagingGatewayConfiguration.MessageBuilder))
            {
                messagingGatewayConfiguration.MessageBuilder = configuration.MessageBuilder;
            }

            if (string.IsNullOrEmpty(messagingGatewayConfiguration.Namespace))
            {
                messagingGatewayConfiguration.Namespace = configuration.Namespace;
            }

            if (string.IsNullOrEmpty(messagingGatewayConfiguration.MessageAssertion))
            {
                messagingGatewayConfiguration.MessageAssertion = configuration.MessageAssertion;
            }
        }

        return base.GenerateAsync(configuration, prefix, templateFolderName, model, ignore);
    }
}
