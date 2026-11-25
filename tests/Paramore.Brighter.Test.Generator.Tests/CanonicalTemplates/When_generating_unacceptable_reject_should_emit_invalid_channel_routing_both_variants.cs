using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

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

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_unacceptable_reject_file_should_exist_with_correct_name(string variant)
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
            $"{variant} canonical unacceptable-reject file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_unacceptable_reject_should_create_subscription_with_both_routing_keys(
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
        Assert.Contains("invalidMessageRoutingKey:", content);
    }

    [Theory]
    [InlineData(REACTOR, "Reject(")]
    [InlineData(PROACTOR, "RejectAsync(")]
    public async Task When_generating_unacceptable_reject_should_reject_with_unacceptable_reason(
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
        Assert.Contains("Unacceptable", content);
    }

    [Theory]
    [InlineData(REACTOR, "GetMessageFromInvalidChannel")]
    [InlineData(PROACTOR, "GetMessageFromInvalidChannelAsync")]
    public async Task When_generating_unacceptable_reject_should_poll_invalid_channel_inside_bounded_retry_loop(
        string variant, string invalidChannelRead)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains(invalidChannelRead, content);
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(60)", content);
        Assert.Contains("500", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_unacceptable_reject_should_assert_unacceptable_reason_on_invalid_channel(
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
        Assert.Contains("if (keys.StampsRejectionMetadata)", content);
        Assert.Contains("keys.RejectionReason", content);
        Assert.Contains("keys.OriginalTopic", content);
        Assert.Contains("_publication.Topic!.Value", content);
    }

    [Theory]
    [InlineData(REACTOR, "GetMessageFromDeadLetterQueue")]
    [InlineData(PROACTOR, "GetMessageFromDeadLetterQueueAsync")]
    public async Task When_generating_unacceptable_reject_should_assert_dlq_absence_via_single_bounded_receive(
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
        Assert.Contains("MT_NONE", content);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9876 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, REACTOR, TEMPLATE_NAME));
        Assert.Contains("Skip = \"Deferred: #9876", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
