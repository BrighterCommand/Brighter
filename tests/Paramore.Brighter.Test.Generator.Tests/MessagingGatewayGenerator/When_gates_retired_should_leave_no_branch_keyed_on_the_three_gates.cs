using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.MessagingGatewayGenerator;

/// <summary>
/// Gate test for Phase 5 step 2 of the ADR 0066 "Step C" cleanup.
///
/// Asserts:
///   1. <see cref="Generators.MessagingGatewayGenerator"/> (source) contains no branch referencing
///      <c>HasSupportToDelayedMessages</c>, <c>HasSupportToDeadLetterQueue</c>, or
///      <c>HasSupportToRequeue</c> (FR-10(4), AC-10(c)).
///   2. The three retained gates (<c>confirming_posting</c>, <c>no_broker_created</c>,
///      <c>assume_channel</c>/<c>validate_channel</c>) still skip their templates when their
///      flags are false — proving the retained branches survived the cleanup.
///   3. A canonical template whose NAME contains the legacy substrings is still generated — the
///      substring-matching hazard guard (ADR 0066). Carried over from the retired
///      When_gate_flags_are_false_should_skip_only_legacy_templates, which this test replaced;
///      it is the one assertion that file made which nothing else covers.
/// </summary>
public class WhenGatesRetiredShouldLeaveNoBranchKeyedOnTheThreeGates : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;
    private readonly string _repoRoot;

    public WhenGatesRetiredShouldLeaveNoBranchKeyedOnTheThreeGates()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"MessagingGatewayGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();

        _repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from AppContext.BaseDirectory. " +
                "Expected to walk up and find 'tests/Paramore.Brighter.Kafka.Tests'.");
    }

    [Fact]
    public void When_gates_retired_should_leave_no_branch_keyed_on_the_three_gates()
    {
        // Arrange — locate the source file to scan
        var generatorSource = Path.Combine(
            _repoRoot,
            "tools", "Paramore.Brighter.Test.Generator",
            "Generators", "MessagingGatewayGenerator.cs");

        Assert.True(File.Exists(generatorSource),
            $"Source file not found at expected path: {generatorSource}");

        // Act
        var sourceText = File.ReadAllText(generatorSource);

        // Assert — the three retired gates are absent from the source (FR-10(4), AC-10(c))
        Assert.False(sourceText.Contains("HasSupportToDelayedMessages"),
            "HasSupportToDelayedMessages must not appear — its gate branches are retired (ADR 0066 Step C)");

        Assert.False(sourceText.Contains("HasSupportToDeadLetterQueue"),
            "HasSupportToDeadLetterQueue must not appear — its gate branch is retired (ADR 0066 Step C)");

        Assert.False(sourceText.Contains("HasSupportToRequeue"),
            "HasSupportToRequeue must not appear — its gate branch is retired (ADR 0066 Step C)");

        // Assert — the LegacyGatedTemplates array that housed the closed list is also gone
        Assert.False(sourceText.Contains("LegacyGatedTemplates"),
            "LegacyGatedTemplates array must be deleted along with the branches it guarded (ADR 0066 Step C)");
    }

    [Fact]
    public async Task When_retained_gate_flags_are_false_should_still_skip_retained_gate_templates()
    {
        // Arrange — set all retained capability gates to false. The three retired gates are not set
        // at all: they no longer exist as a concept in SkipTest, so only the retained gate branches
        // can govern skipping
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            MessageAssertion = "TestMessageAssertion",
            MessagingGateway = new MessagingGatewayConfiguration
            {
                Prefix = "Test",
                Namespace = "MyApp.Tests",
                MessageGatewayProvider = "TestProvider",
                Publication = "Publication",
                Subscription = "Subscription",
                HasSupportToPublishConfirmation = false,
                HasSupportToValidateBrokerExistence = false,
                HasSupportToValidateInfrastructure = false,
            }
        };
        var generator = new Generators.MessagingGatewayGenerator(_logger);
        var reactorOutput = Path.Combine(_testDirectory, "MessagingGateway", "Test", "Generated", "Reactor");

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — positive control: an ungated template WAS emitted, so the absence assertions
        // below cannot pass vacuously on a generation run that produced nothing
        Assert.True(
            File.Exists(Path.Combine(reactorOutput,
                "When_posting_a_message_via_the_messaging_gateway_should_be_received.cs")),
            "ungated template must still be generated — otherwise the skip assertions prove nothing");

        // Assert — confirming_posting template is still skipped when HasSupportToPublishConfirmation is false
        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_confirming_posting_a_message_should_receive_publish_confirmation.cs")),
            "confirming_posting template must remain gated by HasSupportToPublishConfirmation");

        // Assert — no_broker_created template is still skipped when HasSupportToValidateBrokerExistence is false
        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_posting_a_message_but_no_broker_created_should_throw_exception.cs")),
            "no_broker_created template must remain gated by HasSupportToValidateBrokerExistence");

        // Assert — assume_channel and validate_channel templates are still skipped when HasSupportToValidateInfrastructure is false
        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_infrastructure_missing_and_assume_channel_should_throw_exception.cs")),
            "assume_channel template must remain gated by HasSupportToValidateInfrastructure");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_infrastructure_missing_and_validate_channel_should_throw_exception.cs")),
            "validate_channel template must remain gated by HasSupportToValidateInfrastructure");
    }

    /// <summary>
    /// The legacy gating mechanism matched a closed list of template names. If anyone ever
    /// reintroduces gating by SUBSTRING instead, a canonical template whose name happens to contain
    /// "requeuing" and "with_delay" — as the real FR-2 template
    /// (When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay) does — would be
    /// silently gated off. This plants such a template and proves it is emitted.
    /// </summary>
    [Fact]
    public async Task When_a_canonical_template_name_contains_legacy_substrings_should_still_be_generated()
    {
        // Arrange — plant a canonical template whose name contains both "requeuing" and "with_delay"
        // but which is NOT one of the four legacy names (ADR 0066 step A, "hypothetical canonical")
        var plantedTemplate = Path.Combine(
            AppContext.BaseDirectory, "Templates", "MessagingGateway", "Reactor",
            "When_requeuing_a_canonical_message_with_delay_should_confirm_redelivery.cs.liquid");
        File.WriteAllText(plantedTemplate, "// canonical template placeholder");

        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            MessageAssertion = "TestMessageAssertion",
            MessagingGateway = new MessagingGatewayConfiguration
            {
                Prefix = "Test",
                Namespace = "MyApp.Tests",
                MessageGatewayProvider = "TestProvider",
                Publication = "Publication",
                Subscription = "Subscription",
                HasSupportToPublishConfirmation = false,
                HasSupportToValidateBrokerExistence = false,
                HasSupportToValidateInfrastructure = false,
            }
        };
        var generator = new Generators.MessagingGatewayGenerator(_logger);
        var reactorOutput = Path.Combine(_testDirectory, "MessagingGateway", "Test", "Generated", "Reactor");

        try
        {
            // Act
            await generator.GenerateAsync(configuration);

            // Assert — nothing gated it: substring matching must never determine gate applicability
            Assert.True(
                File.Exists(Path.Combine(reactorOutput,
                    "When_requeuing_a_canonical_message_with_delay_should_confirm_redelivery.cs")),
                "Canonical template containing 'requeuing' and 'with_delay' must not be gated — " +
                "substring matching must never determine gate applicability");
        }
        finally
        {
            File.Delete(plantedTemplate);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from <paramref name="startDir"/> until it finds a directory containing
    /// <c>tests/Paramore.Brighter.Kafka.Tests</c>, which is a reliable marker for the repo root.
    /// </summary>
    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var marker = Path.Combine(dir.FullName, "tests", "Paramore.Brighter.Kafka.Tests");
            if (Directory.Exists(marker))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
