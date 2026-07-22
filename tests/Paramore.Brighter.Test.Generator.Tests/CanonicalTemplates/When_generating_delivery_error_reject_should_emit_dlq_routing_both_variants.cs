using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical delivery-error reject templates (FR-4) emit both a Reactor and a
/// Proactor variant that:
///   - name the file When_rejecting_message_with_delivery_error_should_send_to_dlq (NFR-1);
///   - create the subscription with a deadLetterRoutingKey named argument (AC-4, FR-1(2));
///   - call _channel.Reject with a DeliveryError MessageRejectionReason (AC-4);
///   - poll for DLQ arrival inside the bounded retry loop (Stopwatch, 500 ms, 30 s — NFR-2);
///   - assert the original-topic metadata key equals the data topic (AC-4);
///   - assert the rejection-reason metadata key is present (AC-4);
///   - emit the conditional ledger-driven Skip so the Deferred marker is supplied by the
///     conformance ledger, not hard-coded in the template (FR-21).
/// </summary>
public class WhenGeneratingDeliveryErrorRejectShouldEmitDlqRoutingBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_delivery_error_should_send_to_dlq";

    private const string LEDGER_KEY = "Kafka / Standard";
    private const string FR_COLUMN = "FR-4";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingDeliveryErrorRejectShouldEmitDlqRoutingBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"DeliveryErrorRejectTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_reactor_file_should_exist_with_correct_name()
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
            $"Reactor canonical delivery-error reject file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Proactor file exists at the NFR-1 mandated path
        var proactorPath = ProactorOutputPath(configuration);
        Assert.True(File.Exists(proactorPath),
            $"Proactor canonical delivery-error reject file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_reactor_should_create_subscription_with_dlq_routing_key()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — subscription must name deadLetterRoutingKey explicitly (AC-4, FR-1(2))
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("deadLetterRoutingKey:", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_proactor_should_create_subscription_with_dlq_routing_key()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — subscription must name deadLetterRoutingKey explicitly (AC-4, FR-1(2))
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("deadLetterRoutingKey:", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_reactor_should_reject_with_delivery_error_reason()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reject is called with DeliveryError (AC-4)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Reject(", content);
        Assert.Contains("DeliveryError", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_proactor_should_reject_with_delivery_error_reason()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectAsync is called with DeliveryError (AC-4, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("RejectAsync(", content);
        Assert.Contains("DeliveryError", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_reactor_should_poll_dlq_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — DLQ arrival polled inside the bounded retry loop (NFR-2, AC-4)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("GetMessageFromDeadLetterQueue", content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_proactor_should_poll_dlq_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — DLQ arrival polled inside the bounded retry loop (NFR-2, AC-4)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("GetMessageFromDeadLetterQueueAsync", content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_reactor_should_assert_original_topic_equals_data_topic()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — original-topic key assertion via per-transport key names (AC-4)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_proactor_should_assert_original_topic_equals_data_topic()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — original-topic key assertion via per-transport key names (AC-4)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_reactor_should_assert_rejection_reason_key_present()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — rejection-reason entry is asserted present (AC-4)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("keys.RejectionReason", content);
    }

    [Fact]
    public async Task When_generating_delivery_error_reject_proactor_should_assert_rejection_reason_key_present()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — rejection-reason entry is asserted present (AC-4)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("keys.RejectionReason", content);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #5678 (sign-off: @maintainer)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact, Skip = "Deferred: #5678 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #5678", content);
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
