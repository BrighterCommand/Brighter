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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Generates messaging gateway test code from Liquid templates based on <see cref="MessagingGatewayConfiguration"/>.
/// Supports both single and multiple gateway configurations, producing Reactor and Proactor test variants,
/// and conditionally skipping tests for unsupported gateway features.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
/// <param name="ledger">
/// Optional conformance ledger override. When null the generator loads the ledger from the
/// checked-in file by walking up from <see cref="AppContext.BaseDirectory"/>. Pass an
/// <see cref="InMemoryConformanceLedger"/> in tests to control per-cell values.
/// </param>
public class MessagingGatewayGenerator(
    ILogger<MessagingGatewayGenerator> logger,
    IAmAConformanceLedger? ledger = null)
    : BaseGenerator(logger)
{
    // Assigned at the start of GenerateAsync, before any template renders; LoadFrom throws rather
    // than return null.
    private IAmAConformanceLedger _ledger = null!;

    /// <inheritdoc />
    protected override void PrepareGeneration() => _ledger = ledger ?? LoadLedgerFromFileSystem();

    /// <inheritdoc />
    protected override IReadOnlyList<GenerationSuite> Suites(TestConfiguration configuration)
    {
        var suites = new List<GenerationSuite>();

        if (configuration.MessagingGateway != null)
        {
            suites.AddRange(SuitesFor(
                configuration,
                configuration.MessagingGateway,
                folderName: configuration.MessagingGateway.Prefix,
                modelPrefix: configuration.MessagingGateway.Prefix));
        }
        else if (configuration.MessagingGateways != null)
        {
            foreach (var (key, messagingGatewayConfiguration) in configuration.MessagingGateways)
            {
                logger.LogInformation("Describing messaging gateway test suites for {GatewayName}", key);
                var folderName = string.IsNullOrEmpty(messagingGatewayConfiguration.Prefix)
                    ? key
                    : messagingGatewayConfiguration.Prefix;

                var namespaceSuffix = $".{folderName}";
                suites.AddRange(SuitesFor(
                    configuration,
                    messagingGatewayConfiguration,
                    folderName: folderName,
                    modelPrefix: namespaceSuffix));
            }
        }
        else
        {
            logger.LogInformation("No messaging gateway configured");
        }

        return suites;
    }

    // The Shared suite holds support types both variants reference, so it lands in the gateway's
    // Generated folder and takes no ignore or prepareModel: none of its templates is a conformance behaviour.
    private IEnumerable<GenerationSuite> SuitesFor(
        TestConfiguration configuration,
        MessagingGatewayConfiguration messagingGatewayConfiguration, string folderName, string modelPrefix)
    {
        var model = messagingGatewayConfiguration.WithPrefix(modelPrefix).WithDefaultsFrom(configuration);

        foreach (var variant in new[] { "Reactor", "Proactor" })
        {
            yield return new GenerationSuite(
                Path.Combine("MessagingGateway", folderName, "Generated", variant),
                Path.Combine("MessagingGateway", variant),
                model,
                filename => SkipTest(model, filename),
                SetCanonicalSkip
            );
        }

        yield return new GenerationSuite(
            Path.Combine("MessagingGateway", folderName, "Generated"),
            Path.Combine("MessagingGateway", "Shared"),
            model
        );
    }

    private static bool SkipTest(MessagingGatewayConfiguration configuration, string fileName)
    {
        if (
            !configuration.HasSupportToPublishConfirmation
            && fileName.Contains("confirming_posting")
        )
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

        if (
            !configuration.HasSupportToValidateInfrastructure
            && (fileName.Contains("assume_channel") || fileName.Contains("validate_channel"))
        )
        {
            return true;
        }

        if (
            !configuration.HasSupportToDetectMissingInfrastructureOnAssume
            && fileName.Contains("assume_channel")
        )
        {
            return true;
        }

        return false;
    }

    private static IAmAConformanceLedger LoadLedgerFromFileSystem()
        => ConformanceLedger.LoadFrom(AppContext.BaseDirectory);

    // Skip is the ledger's value for a canonical template on a configuration with a LedgerKey;
    // otherwise empty, never null, since Liquid treats nil != empty as true.
    private void SetCanonicalSkip(string templatePath, object model)
    {
        if (model is not MessagingGatewayConfiguration config) return;

        var baseName = Path.GetFileName(templatePath).Replace(".cs.liquid", "");

        var frColumn = CanonicalBehaviours.FrColumnFor(baseName);

        if (frColumn != null && !string.IsNullOrEmpty(config.LedgerKey))
        {
            config.Skip = _ledger.GetSkip(config.LedgerKey, frColumn, CanonicalBehaviours.BehaviourFor(frColumn));
        }
        else
        {
            config.Skip = string.Empty;
        }
    }
}
