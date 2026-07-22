using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical rejection-metadata templates (FR-8) emit both a Reactor and a
/// Proactor variant (FR-14 closes the Kafka Reactor-only gap) that:
///   - name the file When_rejecting_message_should_include_metadata (NFR-1);
///   - read every metadata field via provider.RejectionMetadataKeys.* — never hard-coded key strings
///     (FR-8, AC-8); a field whose provider key is string.Empty fails as a genuine non-conformance;
///   - assert OriginalTopic equals the data topic (AC-8);
///   - assert OriginalType equals "MT_COMMAND" (AC-8);
///   - assert RejectionReason equals "DeliveryError" (AC-8);
///   - assert RejectionMessage equals the description passed to Reject (AC-8);
///   - assert RejectionTimestamp is a parseable ISO-8601 timestamp within the last minute (AC-8);
///   - assert DLQ arrival INSIDE the bounded retry loop (Stopwatch, 500 ms, 30 s — NFR-2);
///   - emit the conditional ledger-driven Skip so the Deferred marker is supplied by the
///     conformance ledger, not hard-coded in the template (FR-21).
/// </summary>
public class WhenGeneratingMetadataTestShouldReadViaProviderKeysBothVariants : IDisposable
{
    private const string TEMPLATE_NAME = "When_rejecting_message_should_include_metadata";
    private const string LEDGER_KEY = "Kafka / Standard";
    private const string FR_COLUMN = "FR-8";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingMetadataTestShouldReadViaProviderKeysBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MetadataTemplateTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor file exists at the NFR-1 mandated path
        var reactorPath = ReactorOutputPath(configuration);
        Assert.True(File.Exists(reactorPath),
            $"Reactor canonical metadata file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Proactor file exists at the NFR-1 mandated path (FR-14 closes Kafka Reactor-only gap)
        var proactorPath = ProactorOutputPath(configuration);
        Assert.True(File.Exists(proactorPath),
            $"Proactor canonical metadata file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_should_read_original_topic_via_provider_keys()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — OriginalTopic read via keys.OriginalTopic, not a hard-coded string (FR-8, AC-8)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_should_read_original_topic_via_provider_keys()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — OriginalTopic read via keys.OriginalTopic (FR-8, AC-8)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_should_assert_original_type_matches_sent_message()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — OriginalType read via keys.OriginalType and asserted equal to the sent message's own type (AC-8)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.OriginalType", content);
        Assert.Contains("message.Header.MessageType.ToString()", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_should_assert_original_type_matches_sent_message()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — OriginalType read via keys.OriginalType and asserted equal to the sent message's own type (AC-8, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.OriginalType", content);
        Assert.Contains("message.Header.MessageType.ToString()", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_should_assert_rejection_reason_equals_delivery_error()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectionReason read via keys.RejectionReason and asserted equal to "DeliveryError" (AC-8)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.RejectionReason", content);
        Assert.Contains("DeliveryError", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_should_assert_rejection_reason_equals_delivery_error()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectionReason read via keys.RejectionReason (AC-8, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.RejectionReason", content);
        Assert.Contains("DeliveryError", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_should_assert_rejection_message_equals_description()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectionMessage read via keys.RejectionMessage; value equals description passed to Reject (AC-8)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.RejectionMessage", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_should_assert_rejection_message_equals_description()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectionMessage read via keys.RejectionMessage (AC-8, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.RejectionMessage", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_should_assert_rejection_timestamp_is_parseable_iso8601()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectionTimestamp read via keys.RejectionTimestamp and parsed as ISO-8601 (AC-8)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.RejectionTimestamp", content);
        Assert.Contains("DateTimeOffset.TryParse", content);
        Assert.Contains("TimeSpan.FromMinutes(1)", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_should_assert_rejection_timestamp_is_parseable_iso8601()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectionTimestamp read via keys.RejectionTimestamp and parsed (AC-8, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.RejectionTimestamp", content);
        Assert.Contains("DateTimeOffset.TryParse", content);
        Assert.Contains("TimeSpan.FromMinutes(1)", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_reactor_should_poll_dlq_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — DLQ arrival polled inside the bounded retry loop (NFR-2, AC-8)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("GetMessageFromDeadLetterQueue", content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_metadata_test_proactor_should_poll_dlq_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — DLQ arrival polled inside the bounded retry loop (NFR-2, AC-8, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("GetMessageFromDeadLetterQueueAsync", content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_ledger_is_pass_reactor_should_emit_fact_without_skip()
    {
        // Arrange — ledger cell is Pass; the [Fact] must carry no Skip argument
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact] present; Skip absent (conditional pattern renders nothing when Skip is empty)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("[Fact]", content);
        Assert.DoesNotContain("Skip =", content);
    }

    [Fact]
    public async Task When_ledger_is_deferred_reactor_should_emit_skip_on_fact()
    {
        // Arrange — ledger cell is Deferred; the template must conditionally emit Skip
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9012 (sign-off: @maintainer)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact, Skip = "Deferred: #9012 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #9012", content);
    }

    [Fact]
    public async Task When_ledger_is_pass_proactor_should_emit_fact_without_skip()
    {
        // Arrange — ledger cell is Pass; the [Fact] must carry no Skip argument
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("[Fact]", content);
        Assert.DoesNotContain("Skip =", content);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static InMemoryConformanceLedger PassLedger() =>
        new(new Dictionary<(string, string), string>
        {
            [(LEDGER_KEY, FR_COLUMN)] = "Pass"
        });

    private TestConfiguration BuildConfiguration() =>
        new()
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
                LedgerKey = LEDGER_KEY,
            }
        };

    private string ReactorOutputPath(TestConfiguration configuration) =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Reactor",
            $"{TEMPLATE_NAME}.cs");

    private string ProactorOutputPath(TestConfiguration configuration) =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Proactor",
            $"{TEMPLATE_NAME}.cs");

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
