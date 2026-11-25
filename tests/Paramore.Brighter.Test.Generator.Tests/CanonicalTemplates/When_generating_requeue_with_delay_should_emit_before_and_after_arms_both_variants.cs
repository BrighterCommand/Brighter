using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the requeue-with-delay templates, both variants: Requeue/RequeueAsync with a 5 s delay returns true,
/// a single receive before the delay sees nothing, a bounded poll after it, no scheduler or broker
/// mechanism, and a Skip supplied by the ledger.
/// </summary>
public class WhenGeneratingRequeueWithDelayShouldEmitBeforeAndAfterArmsBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-2";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingRequeueWithDelayShouldEmitBeforeAndAfterArmsBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"RequeueWithDelayTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_requeue_with_delay_file_should_exist_with_correct_name(string variant)
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
            $"{variant} canonical requeue-with-delay file not found at {path}");
    }

    [Theory]
    [InlineData(REACTOR, "Requeue(")]
    [InlineData(PROACTOR, "RequeueAsync(")]
    public async Task When_generating_requeue_with_delay_should_pass_positive_timespan_to_requeue(
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
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
        Assert.Contains(requeueCall, content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_requeue_with_delay_should_assert_requeue_returns_true(string variant)
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
    [InlineData(REACTOR, "Receive(")]
    [InlineData(PROACTOR, "ReceiveAsync(")]
    public async Task When_generating_requeue_with_delay_should_emit_single_before_delay_receive(
        string variant, string receiveCall)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.Contains("MT_NONE", content);
        Assert.Contains(receiveCall, content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_requeue_with_delay_should_use_bounded_retry_loop_for_after_arm(string variant)
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
    public async Task When_generating_requeue_with_delay_should_not_reference_scheduler_or_mechanism(string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, FR_COLUMN);
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, TEMPLATE_NAME));
        Assert.DoesNotContain("scheduler", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RedrivePolicy", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModifyAckDeadline", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChangeInvisibleDuration", content, StringComparison.OrdinalIgnoreCase);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #4567 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, REACTOR, TEMPLATE_NAME));
        Assert.Contains("Skip = \"Deferred: #4567", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
