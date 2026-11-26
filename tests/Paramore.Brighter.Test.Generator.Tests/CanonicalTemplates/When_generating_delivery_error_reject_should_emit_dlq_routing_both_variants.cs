using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the delivery-error reject templates, both variants: a subscription with a dead-letter routing key,
/// Reject with DeliveryError, DLQ arrival polled in a bounded loop, the original-topic and rejection-reason
/// metadata asserted, and a Skip supplied by the ledger.
/// </summary>
public class WhenGeneratingDeliveryErrorRejectShouldEmitDlqRoutingBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_delivery_error_should_send_to_dlq";

    private const string LEDGER_KEY = "Kafka / Classic";
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

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_delivery_error_reject_file_should_exist_with_correct_name(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var path = OutputPath(_testDirectory, variant, TEMPLATE_NAME);
        Assert.True(File.Exists(path),
            $"{variant} canonical delivery-error reject file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_delivery_error_reject_should_create_subscription_with_dlq_routing_key(
        string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("deadLetterRoutingKey:", content);
    }

    [Theory]
    [InlineData(REACTOR, "Reject(")]
    [InlineData(PROACTOR, "RejectAsync(")]
    public async Task When_generating_delivery_error_reject_should_reject_with_delivery_error_reason(
        string variant, string rejectCall)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains(rejectCall, content);
        Assert.Contains("DeliveryError", content);
    }

    [Theory]
    [InlineData(REACTOR, "GetMessageFromDeadLetterQueue")]
    [InlineData(PROACTOR, "GetMessageFromDeadLetterQueueAsync")]
    public async Task When_generating_delivery_error_reject_should_poll_dlq_inside_bounded_retry_loop(
        string variant, string deadLetterQueueRead)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains(deadLetterQueueRead, content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(60)", content);
        Assert.Contains("500", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_delivery_error_reject_should_assert_original_topic_equals_data_topic(
        string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_delivery_error_reject_should_assert_rejection_reason_key_present(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("keys.RejectionReason", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_ledger_is_pass_should_emit_fact_without_skip(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #5678 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, REACTOR, TEMPLATE_NAME));
        Assert.Contains("Skip = \"Deferred: #5678", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
