using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the zero-delay requeue templates, both variants: Requeue with TimeSpan.Zero returns true, and the
/// message is polled for with no before-delay absence check and arrives within 5 s, timed from Requeue
/// returning; the Skip is supplied by the ledger. See ADR 0066, Conventions the canonical templates follow.
/// </summary>
public class WhenGeneratingZeroDelayRequeueShouldEmitFirstIterationReceiptBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-15";

    private const string STOPWATCH_START = "Stopwatch.StartNew()";
    private const string REACTOR_ZERO_DELAY_REQUEUE_CALL = "Requeue(received, TimeSpan.Zero)";
    private const string PROACTOR_ZERO_DELAY_REQUEUE_CALL = "RequeueAsync(received, TimeSpan.Zero)";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingZeroDelayRequeueShouldEmitFirstIterationReceiptBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZeroDelayRequeueTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_zero_delay_requeue_file_should_exist_with_correct_name(string variant)
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
            $"{variant} canonical zero-delay-requeue file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR, "Requeue(")]
    [InlineData(PROACTOR, "RequeueAsync(")]
    public async Task When_generating_zero_delay_requeue_should_call_requeue_with_timespanzero(
        string variant, string requeueCall)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("TimeSpan.Zero", content);
        Assert.Contains(requeueCall, content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_zero_delay_requeue_should_assert_requeue_returns_true(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("Assert.True(", content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_zero_delay_requeue_should_use_bounded_retry_loop(string variant)
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
    public async Task When_generating_zero_delay_requeue_should_assert_elapsed_under_five_seconds(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
    }

    [Theory]
    [InlineData(REACTOR, REACTOR_ZERO_DELAY_REQUEUE_CALL)]
    [InlineData(PROACTOR, PROACTOR_ZERO_DELAY_REQUEUE_CALL)]
    public async Task When_generating_zero_delay_requeue_should_start_the_stopwatch_after_requeue_returns(
        string variant, string zeroDelayRequeueCall)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        var requeueIndex = content.IndexOf(zeroDelayRequeueCall, StringComparison.Ordinal);
        var stopwatchIndex = content.IndexOf(STOPWATCH_START, StringComparison.Ordinal);

        Assert.True(requeueIndex >= 0, $"Expected the generated {variant} file to call {zeroDelayRequeueCall}");
        Assert.True(stopwatchIndex >= 0, $"Expected the generated {variant} file to start a {STOPWATCH_START}");
        Assert.True(requeueIndex < stopwatchIndex,
            $"Expected {STOPWATCH_START} to appear AFTER {zeroDelayRequeueCall}, so the requeue "
            + $"call's own duration is excluded from the elapsed window; found the stopwatch at {stopwatchIndex} "
            + $"and the requeue at {requeueIndex}.");
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_not_have_negative_arm_before_loop()
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, REACTOR, TEMPLATE_NAME));
        Assert.DoesNotContain("Assert.Equal(MessageType.MT_NONE", content);
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
