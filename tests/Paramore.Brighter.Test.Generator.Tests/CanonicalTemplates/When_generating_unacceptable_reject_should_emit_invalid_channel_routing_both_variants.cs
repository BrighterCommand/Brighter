using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the unacceptable reject templates, both variants: a subscription with both routing keys, Reject with
/// Unacceptable, invalid-channel arrival polled in a bounded loop, the rejection reason and original topic
/// asserted, the DLQ's absence checked by a single bounded receive, and a Skip supplied by the ledger.
/// See ADR 0066, Conventions the canonical templates follow.
/// </summary>
public class WhenGeneratingUnacceptableRejectShouldEmitInvalidChannelRoutingBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-5";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingUnacceptableRejectShouldEmitInvalidChannelRoutingBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UnacceptableRejectTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_reactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var reactorPath = ReactorOutputPath(configuration);
        Assert.True(File.Exists(reactorPath),
            $"Reactor canonical unacceptable-reject file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var proactorPath = ProactorOutputPath(configuration);
        Assert.True(File.Exists(proactorPath),
            $"Proactor canonical unacceptable-reject file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_reactor_should_create_subscription_with_both_routing_keys()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("deadLetterRoutingKey:", content);
        Assert.Contains("invalidMessageRoutingKey:", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_proactor_should_create_subscription_with_both_routing_keys()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("deadLetterRoutingKey:", content);
        Assert.Contains("invalidMessageRoutingKey:", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_reactor_should_reject_with_unacceptable_reason()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Reject(", content);
        Assert.Contains("Unacceptable", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_proactor_should_reject_with_unacceptable_reason()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("RejectAsync(", content);
        Assert.Contains("Unacceptable", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_reactor_should_poll_invalid_channel_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("GetMessageFromInvalidChannel", content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(60)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_proactor_should_poll_invalid_channel_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("GetMessageFromInvalidChannelAsync", content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(60)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_reactor_should_assert_unacceptable_reason_on_invalid_channel()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("if (keys.StampsRejectionMetadata)", content);
        Assert.Contains("keys.RejectionReason", content);
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_proactor_should_assert_unacceptable_reason_on_invalid_channel()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("if (keys.StampsRejectionMetadata)", content);
        Assert.Contains("keys.RejectionReason", content);
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_reactor_should_assert_dlq_absence_via_single_bounded_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("GetMessageFromDeadLetterQueue", content);
        Assert.Contains("MT_NONE", content);
    }

    [Fact]
    public async Task When_generating_unacceptable_reject_proactor_should_assert_dlq_absence_via_single_bounded_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("GetMessageFromDeadLetterQueueAsync", content);
        Assert.Contains("MT_NONE", content);
    }

    [Fact]
    public async Task When_ledger_is_pass_reactor_should_emit_fact_without_skip()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("[Fact]", content);
        Assert.DoesNotContain("Skip =", content);
    }

    [Fact]
    public async Task When_ledger_is_deferred_reactor_should_emit_skip_on_fact()
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9876 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #9876", content);
    }

    [Fact]
    public async Task When_ledger_is_pass_proactor_should_emit_fact_without_skip()
    {
        // Arrange
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
