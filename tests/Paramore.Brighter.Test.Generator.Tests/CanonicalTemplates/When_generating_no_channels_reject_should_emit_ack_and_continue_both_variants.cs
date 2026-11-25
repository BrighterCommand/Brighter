using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the no-channels reject templates, both variants: a subscription with neither routing key, Reject with
/// DeliveryError returning true, and the message queued behind it received, told from the rejected one by id,
/// with a Skip supplied by the ledger. Logging is not asserted; the <c>_and_log</c> suffix is only the name.
/// See ADR 0066, Conventions the canonical templates follow.
/// </summary>
public class WhenGeneratingNoChannelsRejectShouldEmitAckAndContinueBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-7";

    // One for the rejected message, one for the message queued behind it.
    private const int DISTINCTLY_BUILT_MESSAGES = 2;

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingNoChannelsRejectShouldEmitAckAndContinueBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"NoChannelsRejectTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_no_channels_reject_file_should_exist_with_correct_name(string variant)
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
            $"{variant} canonical no-channels file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_no_channels_reject_should_create_subscription_with_neither_routing_key(
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
        Assert.DoesNotContain("deadLetterRoutingKey:", content);
        Assert.DoesNotContain("invalidMessageRoutingKey:", content);
    }

    [Theory]
    [InlineData(REACTOR, "Reject(")]
    [InlineData(PROACTOR, "RejectAsync(")]
    public async Task When_generating_no_channels_reject_should_reject_with_delivery_error_and_assert_true(
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
        Assert.Contains("Assert.True(rejected", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_no_channels_reject_should_poll_for_the_following_message_inside_a_bounded_retry_loop(
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
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_no_channels_reject_should_tell_the_following_message_from_the_rejected_one(
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
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, "SetMessageId(Id.Random())"));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, ".SetBody("));

        Assert.Contains("_messageAssertion.Assert(theOtherMessage, receivedOther)", content);
        Assert.Contains("Assert.NotEqual(rejectedMessage.Header.MessageId, next.Header.MessageId)", content);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9999 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, REACTOR, TEMPLATE_NAME));
        Assert.Contains("Skip = \"Deferred: #9999", content);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int Occurrences(string content, string needle)
    {
        var count = 0;
        for (var at = content.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = content.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
