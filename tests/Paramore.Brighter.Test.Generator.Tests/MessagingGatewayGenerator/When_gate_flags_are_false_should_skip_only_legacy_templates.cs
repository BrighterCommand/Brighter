using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.MessagingGatewayGenerator;

public class WhenGateFlagsAreFalseShouldSkipOnlyLegacyTemplates : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;
    private readonly string _canonicalTemplatePath;

    public WhenGateFlagsAreFalseShouldSkipOnlyLegacyTemplates()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"MessagingGatewayGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();

        // Place a canonical template whose name contains both "requeuing" and "with_delay"
        // but is NOT one of the four legacy template names — the "hypothetical canonical" test case
        // from ADR 0066 step A. The legacy closed-list check must not gate this file.
        var reactorTemplatesDir = Path.Combine(AppContext.BaseDirectory, "Templates", "MessagingGateway", "Reactor");
        _canonicalTemplatePath = Path.Combine(
            reactorTemplatesDir,
            "When_requeuing_a_canonical_message_with_delay_should_confirm_redelivery.cs.liquid");
        File.WriteAllText(_canonicalTemplatePath, "// canonical template placeholder");
    }

    [Fact]
    public async Task When_gate_flags_are_false_should_skip_only_legacy_templates()
    {
        // Arrange
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

        // Assert — the four legacy templates are NOT generated. Their capability gates were retired in
        // ADR 0066 step C; the templates themselves are deleted, so these now guard against a restore.
        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs")),
            "Legacy delayed-message template is deleted and must never be generated again");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_requeuing_a_failed_message_should_receive_message_again.cs")),
            "Legacy requeue template is deleted and must never be generated again");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs")),
            "Legacy requeue-with-delay template is deleted and must never be generated again");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs")),
            "Legacy dead-letter-queue template is deleted and must never be generated again");

        // Assert — a canonical template whose name contains both "requeuing" and "with_delay" IS generated.
        // The closed legacy list is gone, so nothing can gate it; this guards against substring matching
        // being reintroduced as a gating mechanism (ADR 0066 "the substring-matching hazard")
        Assert.True(
            File.Exists(Path.Combine(reactorOutput,
                "When_requeuing_a_canonical_message_with_delay_should_confirm_redelivery.cs")),
            "Canonical template containing 'requeuing' and 'with_delay' must not be gated — " +
            "substring matching must never determine gate applicability");

        // Assert — retained gates (confirming_posting, no_broker_created, assume_channel, validate_channel)
        // still behave as before: their templates are skipped when the corresponding flag is false
        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_confirming_posting_a_message_should_receive_publish_confirmation.cs")),
            "confirming_posting template must remain gated by HasSupportToPublishConfirmation");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_posting_a_message_but_no_broker_created_should_throw_exception.cs")),
            "no_broker_created template must remain gated by HasSupportToValidateBrokerExistence");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_infrastructure_missing_and_assume_channel_should_throw_exception.cs")),
            "assume_channel template must remain gated by HasSupportToValidateInfrastructure");

        Assert.False(
            File.Exists(Path.Combine(reactorOutput,
                "When_infrastructure_missing_and_validate_channel_should_throw_exception.cs")),
            "validate_channel template must remain gated by HasSupportToValidateInfrastructure");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        if (File.Exists(_canonicalTemplatePath))
        {
            File.Delete(_canonicalTemplatePath);
        }
    }
}
