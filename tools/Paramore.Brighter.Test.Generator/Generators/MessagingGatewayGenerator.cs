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
    // Canonical-template base name → conformance-ledger FR column (FR-21 / ADR 0067).
    // Only templates whose names appear here receive the ledger-driven Deferred Skip.
    // This list is authoritative: a template absent from it is treated as non-canonical.
    private static readonly Dictionary<string, string> CANONICAL_TEMPLATE_FR_COLUMNS = new()
    {
        ["When_requeuing_a_failed_message_should_be_redelivered"]                          = "FR-22",
        ["When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay"]        = "FR-2",
        ["When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately"]   = "FR-15",
        ["When_rejecting_message_with_delivery_error_should_send_to_dlq"]                  = "FR-4",
        ["When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel"] = "FR-5",
        ["When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq"] = "FR-6",
        ["When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log"]  = "FR-7",
        ["When_rejecting_message_with_unknown_reason_should_send_to_dlq"]                  = "FR-17",
        ["When_rejecting_message_should_include_metadata"]                                  = "FR-8",
        ["When_sending_a_delayed_message_should_deliver_after_delay"]                      = "FR-9",
        ["When_nacking_a_message_it_should_be_redelivered"]                                = "FR-16",
    };

    // Human-readable behaviour label for each FR column, used in the Skip string.
    private static readonly Dictionary<string, string> FR_COLUMN_BEHAVIOURS = new()
    {
        ["FR-2"]  = "requeue with delay",
        ["FR-4"]  = "reject with delivery error to DLQ",
        ["FR-5"]  = "reject with unacceptable reason to invalid channel",
        ["FR-6"]  = "fallback: unacceptable, DLQ-only",
        ["FR-7"]  = "no channels configured: acknowledge and log",
        ["FR-8"]  = "rejection metadata stamping",
        ["FR-9"]  = "delayed send",
        ["FR-15"] = "explicit zero-delay requeue",
        ["FR-16"] = "Nack redelivers",
        ["FR-17"] = "reject with None reason to DLQ",
        ["FR-22"] = "canonical plain requeue",
    };

    // The four legacy templates gated by the three capability flags below.
    // This list is closed: nothing is ever added. It is also the deletion work list for step C.
    private static readonly string[] LegacyGatedTemplates =
    [
        "When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery",
        "When_requeuing_a_failed_message_should_receive_message_again",
        "When_requeuing_a_failed_message_with_delay_should_receive_message_again",
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue",
    ];

    // Resolved once per GenerateAsync(TestConfiguration) call.
    private IAmAConformanceLedger? _ledger;

    /// <summary>
    /// Generates messaging gateway test files for the configured gateway(s) in the provided <paramref name="configuration"/>.
    /// Uses <see cref="TestConfiguration.MessagingGateway"/> for a single gateway or <see cref="TestConfiguration.MessagingGateways"/> for multiple.
    /// </summary>
    /// <param name="configuration">The root test configuration containing messaging gateway settings and destination folder.</param>
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        // Resolve the conformance ledger once for this generation run.
        _ledger = ledger ?? LoadLedgerFromFileSystem();

        if (configuration.MessagingGateway != null)
        {
            var prefix = configuration.MessagingGateway.Prefix;
            var prepareModel = _ledger != null ? (Action<string, object>)SetCanonicalSkip : null;

            await GenerateAsync(
                configuration,
                Path.Combine("MessagingGateway", prefix, "Generated", "Reactor"),
                Path.Combine("MessagingGateway", "Reactor"),
                configuration.MessagingGateway,
                filename => SkipTest(configuration.MessagingGateway, filename),
                prepareModel
            );

            await GenerateAsync(
                configuration,
                Path.Combine("MessagingGateway", prefix, "Generated", "Proactor"),
                Path.Combine("MessagingGateway", "Proactor"),
                configuration.MessagingGateway,
                filename => SkipTest(configuration.MessagingGateway, filename),
                prepareModel
            );

            await GenerateAsync(
                configuration,
                Path.Combine("MessagingGateway", prefix, "Generated"),
                Path.Combine("MessagingGateway", "Shared"),
                configuration.MessagingGateway
            );
        }
        else if (configuration.MessagingGateways != null)
        {
            foreach (var (key, messagingGatewayConfiguration) in configuration.MessagingGateways)
            {
                logger.LogInformation("Generating messaging gateway test for {GatewayName}", key);
                var prefix = messagingGatewayConfiguration.Prefix;
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = key;
                }

                messagingGatewayConfiguration.Prefix = $".{prefix}";
                var prepareModel = _ledger != null ? (Action<string, object>)SetCanonicalSkip : null;

                await GenerateAsync(
                    configuration,
                    Path.Combine("MessagingGateway", prefix, "Generated", "Reactor"),
                    Path.Combine("MessagingGateway", "Reactor"),
                    messagingGatewayConfiguration,
                    filename => SkipTest(messagingGatewayConfiguration, filename),
                    prepareModel
                );

                await GenerateAsync(
                    configuration,
                    Path.Combine("MessagingGateway", prefix, "Generated", "Proactor"),
                    Path.Combine("MessagingGateway", "Proactor"),
                    messagingGatewayConfiguration,
                    filename => SkipTest(messagingGatewayConfiguration, filename),
                    prepareModel
                );

                await GenerateAsync(
                    configuration,
                    Path.Combine("MessagingGateway", prefix, "Generated"),
                    Path.Combine("MessagingGateway", "Shared"),
                    messagingGatewayConfiguration
                );
            }
        }
        else
        {
            logger.LogInformation("No messaging gateway configured");
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

        // The three capability gates are consulted only for the closed set of legacy templates;
        // canonical templates are ungated by construction, regardless of flag values.
        var templateBaseName = Path.GetFileName(fileName).Replace(".cs.liquid", "");
        if (Array.Exists(LegacyGatedTemplates, name => name == templateBaseName))
        {
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

            if (!configuration.HasSupportToRequeue && fileName.Contains("requeuing"))
            {
                return true;
            }
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
        Func<string, bool>? ignore = null,
        Action<string, object>? prepareModel = null)
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

        return base.GenerateAsync(configuration, prefix, templateFolderName, model, ignore, prepareModel);
    }

    // ── Ledger integration ───────────────────────────────────────────────────

    /// <summary>
    /// Loads the conformance ledger by walking up from <see cref="AppContext.BaseDirectory"/>
    /// until <c>specs/0036-…/conformance-status.md</c> is found.
    /// Returns null when the ledger cannot be located (no Skip is applied).
    /// </summary>
    private static IAmAConformanceLedger? LoadLedgerFromFileSystem()
    {
        var path = ConformanceLedger.FindLedgerPath(AppContext.BaseDirectory);
        return path != null ? new ConformanceLedger(path) : null;
    }

    /// <summary>
    /// Sets the <see cref="MessagingGatewayConfiguration.Skip"/> property on the model before
    /// each Reactor/Proactor template render. For canonical templates whose base name is in
    /// <see cref="CANONICAL_TEMPLATE_FR_COLUMNS"/> and whose configuration declares a
    /// <see cref="MessagingGatewayConfiguration.LedgerKey"/>, the ledger determines whether a
    /// Deferred Skip string is emitted. For all other templates the property is set to the empty
    /// string (not null) so that `{% if Skip != empty %}` evaluates to false in the template.
    /// </summary>
    private void SetCanonicalSkip(string templateFileName, object model)
    {
        if (model is not MessagingGatewayConfiguration config) return;

        var baseName = Path.GetFileName(templateFileName).Replace(".cs.liquid", "");

        if (CANONICAL_TEMPLATE_FR_COLUMNS.TryGetValue(baseName, out var frColumn)
            && !string.IsNullOrEmpty(config.LedgerKey)
            && _ledger != null)
        {
            var behaviourName = FR_COLUMN_BEHAVIOURS.GetValueOrDefault(frColumn, frColumn);
            config.Skip = _ledger.GetSkip(config.LedgerKey, frColumn, behaviourName);
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
