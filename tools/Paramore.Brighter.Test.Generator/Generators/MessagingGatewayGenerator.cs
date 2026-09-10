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
    // The conformance ledger driving the canonical Deferred Skips, resolved once per
    // GenerateAsync(TestConfiguration) call. Never null once resolved: LoadLedgerFromFileSystem
    // throws rather than handing back a null that would silently skip the whole suite.
    // The canonical-template → FR-column map lives in CanonicalBehaviours.
    private IAmAConformanceLedger _ledger = null!;

    /// <summary>
    /// Generates messaging gateway test files for the configured gateway(s) in the provided <paramref name="configuration"/>.
    /// Uses <see cref="TestConfiguration.MessagingGateway"/> for a single gateway or <see cref="TestConfiguration.MessagingGateways"/> for multiple.
    /// </summary>
    /// <param name="configuration">The root test configuration containing messaging gateway settings and destination folder.</param>
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        // Resolve the conformance ledger once for this generation run. Throws rather than
        // returning null: see ConformanceLedger.LoadFrom.
        _ledger = ledger ?? LoadLedgerFromFileSystem();

        foreach (var suite in Suites(configuration))
        {
            await GenerateAsync(
                configuration,
                suite.Prefix,
                suite.TemplateFolderName,
                suite.Model,
                suite.Ignore,
                suite.PrepareModel
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
    /// the Reactor, Proactor and Shared suites of each configured gateway.
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

                // Templates build a namespace suffix from the model's prefix, which is dot-qualified
                // where the destination folder name is not.
                suites.AddRange(SuitesFor(
                    configuration,
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
    /// The three suites - Reactor, Proactor and Shared - rendered for a single messaging gateway.
    /// </summary>
    /// <param name="configuration">The root configuration the model inherits unset values from.</param>
    /// <param name="messagingGatewayConfiguration">The gateway whose suites are described.</param>
    /// <param name="folderName">The destination folder name for this gateway.</param>
    /// <param name="modelPrefix">The prefix the templates should read from the model.</param>
    /// <returns>The suites for the gateway.</returns>
    /// <remarks>
    /// <para>
    /// All three suites share one copy of the configuration, carrying the prefix the templates need
    /// and the values inherited from the root. The caller's own configuration object is left alone,
    /// so describing the work does not perform part of it - and because the inheritance happens
    /// here, the model a plan reasons about is the model a generation renders.
    /// </para>
    /// <para>
    /// The Shared suite renders support types the Reactor and Proactor tests both reference, so it
    /// lands in the gateway's <c>Generated</c> folder rather than a variant folder beneath it, and
    /// takes neither the capability-flag <c>ignore</c> nor the ledger's per-template
    /// <c>prepareModel</c>: its templates are not conformance behaviours, so there is no capability
    /// that could withdraw one and no ledger cell that could defer one.
    /// </para>
    /// </remarks>
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

        // Narrower than the gate above: a transport can honour an explicit Validate and still
        // complete silently against infrastructure that is not there, so this skips assume_channel
        // alone and leaves validate_channel generated.
        if (
            !configuration.HasSupportToDetectMissingInfrastructureOnAssume
            && fileName.Contains("assume_channel")
        )
        {
            return true;
        }

        return false;
    }

    // ── Ledger integration ───────────────────────────────────────────────────

    /// <summary>
    /// Loads the conformance ledger by walking up from <see cref="AppContext.BaseDirectory"/>
    /// until <c>specs/0036-…/conformance-status.md</c> is found.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the ledger cannot be located. See <see cref="ConformanceLedger.LoadFrom"/> for
    /// why this fails rather than generating an ungated suite.
    /// </exception>
    private static IAmAConformanceLedger LoadLedgerFromFileSystem()
        => ConformanceLedger.LoadFrom(AppContext.BaseDirectory);

    /// <summary>
    /// Sets the <see cref="MessagingGatewayConfiguration.Skip"/> property on the model before
    /// each Reactor/Proactor template render. For canonical templates whose base name is in
    /// <see cref="CanonicalBehaviours.TEMPLATE_FR_COLUMNS"/> and whose configuration declares a
    /// <see cref="MessagingGatewayConfiguration.LedgerKey"/>, the ledger determines whether a
    /// Deferred Skip string is emitted. For all other templates the property is set to the empty
    /// string (not null) so that `{% if Skip != empty %}` evaluates to false in the template.
    /// </summary>
    private void SetCanonicalSkip(string templateFileName, object model)
    {
        if (model is not MessagingGatewayConfiguration config) return;

        var baseName = Path.GetFileName(templateFileName).Replace(".cs.liquid", "");

        var frColumn = CanonicalBehaviours.FrColumnFor(baseName);

        if (frColumn != null && !string.IsNullOrEmpty(config.LedgerKey))
        {
            config.Skip = _ledger.GetSkip(config.LedgerKey, frColumn, CanonicalBehaviours.BehaviourFor(frColumn));
        }
        else
        {
            // Empty string rather than null: Liquid's `nil != empty` is TRUE, so null would
            // cause `{% if Skip != empty %}` to render even when Skip carries no value.
            // An empty string satisfies `"" == empty` and correctly suppresses the block.
            config.Skip = string.Empty;
        }
    }
}
