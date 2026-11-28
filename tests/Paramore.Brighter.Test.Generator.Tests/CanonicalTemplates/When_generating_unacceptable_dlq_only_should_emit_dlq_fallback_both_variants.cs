using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.GeneratedPollLoops;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the unacceptable-with-no-invalid-channel templates, both variants: a subscription with a dead-letter
/// routing key only, Reject with Unacceptable, DLQ arrival polled in a bounded loop, the rejection reason
/// asserted on the DLQ message, and a Skip supplied by the ledger.
/// </summary>
public class WhenGeneratingUnacceptableDlqOnlyShouldEmitDlqFallbackBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-6";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingUnacceptableDlqOnlyShouldEmitDlqFallbackBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UnacceptableDlqFallbackTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_unacceptable_dlq_only_file_should_exist_with_correct_name(string variant)
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
            $"{variant} canonical DLQ-fallback file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_unacceptable_dlq_only_should_create_subscription_with_dlq_key_only(string variant)
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
        Assert.DoesNotContain("invalidMessageRoutingKey:", content);
    }

    [Theory]
    [InlineData(REACTOR, "Reject(")]
    [InlineData(PROACTOR, "RejectAsync(")]
    public async Task When_generating_unacceptable_dlq_only_should_reject_with_unacceptable_reason(
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
    [InlineData(REACTOR, "GetMessageFromDeadLetterQueue")]
    [InlineData(PROACTOR, "GetMessageFromDeadLetterQueueAsync")]
    public async Task When_generating_unacceptable_dlq_only_should_poll_dlq_inside_bounded_retry_loop(
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
        var pollLoops = GeneratedPollLoops.In(content);
        Assert.NotEmpty(pollLoops);
        Assert.All(pollLoops, loop => Assert.Equal(RejectionArrival, loop));
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_unacceptable_dlq_only_should_assert_unacceptable_reason_on_dlq_message(
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
        Assert.Contains("keys.RejectionReason", content);
        Assert.Contains("RejectionReason.Unacceptable.ToString()", content);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #1234 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, REACTOR, TEMPLATE_NAME));
        Assert.Contains("Skip = \"Deferred: #1234", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
