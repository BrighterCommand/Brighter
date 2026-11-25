using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the nack templates, both variants: Nack/NackAsync on the received message, redelivery polled in a
/// bounded loop, a two-message fact (found by its name) that tells the nacked message from the one behind
/// it by id, and a Skip supplied by the ledger.
/// </summary>
public class WhenGeneratingNackTestShouldEmitRedeliveryAndTwoMessageVariantBothVariants : IDisposable
{
    private const string TEMPLATE_NAME = "When_nacking_a_message_it_should_be_redelivered";
    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-16";

    // One message for the single-message fact, two for the two-message fact.
    private const int DISTINCTLY_BUILT_MESSAGES = 3;

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingNackTestShouldEmitRedeliveryAndTwoMessageVariantBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"NackRedeliveryTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_nack_file_should_exist_with_correct_name(string variant)
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
            $"{variant} canonical nack-redelivery file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR, "Nack(")]
    [InlineData(PROACTOR, "NackAsync(")]
    public async Task When_generating_nack_should_call_nack_on_received_message(string variant, string nackCall)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains(nackCall, content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_nack_should_assert_redelivery_inside_bounded_retry_loop(string variant)
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
    public async Task When_generating_nack_should_contain_two_message_variant(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second", content);
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

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_nack_should_tell_the_following_message_from_the_nacked_one(string variant)
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

        Assert.Contains("Assert.Contains(nackedMessage.Header.MessageId.Value, observedIds)", content);
        Assert.Contains("Assert.Contains(theOtherMessage.Header.MessageId.Value, observedIds)", content);
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
