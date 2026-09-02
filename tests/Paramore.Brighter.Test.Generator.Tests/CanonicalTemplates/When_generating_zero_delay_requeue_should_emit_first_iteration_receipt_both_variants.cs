using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical zero-delay-requeue templates (FR-15, AC-16) emit both a Reactor and a
/// Proactor variant that:
///   - pass TimeSpan.Zero explicitly to Requeue/RequeueAsync (AC-16, FR-15);
///   - assert Requeue returns true (AC-16);
///   - assert the message arrives INSIDE a bounded receive-retry loop (500 ms poll, 30 s ceiling
///     — NFR-2, AC-20) — this is a POSITIVE first-iteration assertion, not an AC-20-exemption
///     single receive expecting MT_NONE (no before-zero-delay negative arm);
///   - assert elapsed time from the Requeue call to receipt is less than 5 s (AC-16);
///   - emit the conditional ledger-driven Skip pattern so the Deferred marker is supplied
///     by the conformance ledger, not hard-coded in the template (FR-21).
/// </summary>
public class WhenGeneratingZeroDelayRequeueShouldEmitFirstIterationReceiptBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-15";

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

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor file exists at the expected path (NFR-1)
        var reactorPath = ReactorOutputPath();
        Assert.True(File.Exists(reactorPath),
            $"Reactor canonical zero-delay-requeue file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Proactor file exists at the expected path (NFR-1)
        var proactorPath = ProactorOutputPath();
        Assert.True(File.Exists(proactorPath),
            $"Proactor canonical zero-delay-requeue file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_call_requeue_with_timespanzero()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Requeue is called with TimeSpan.Zero explicitly (AC-16, FR-15)
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
        Assert.Contains("TimeSpan.Zero", content);
        Assert.Contains("Requeue(", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_assert_requeue_returns_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the return value of Requeue is captured and asserted true (AC-16)
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
        Assert.Contains("Assert.True(", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_use_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — receipt assertion is inside a bounded retry loop (NFR-2, AC-20, AC-16)
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_assert_elapsed_under_five_seconds()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — elapsed time from Requeue call to receipt is asserted less than 5 s (AC-16)
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_not_have_negative_arm_before_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — no AC-20-exemption single receive expecting MT_NONE before the loop;
        // FR-15 is a positive first-iteration arrival, not a before-delay negative arm (AC-16)
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
        Assert.DoesNotContain("Assert.Equal(MessageType.MT_NONE", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_proactor_should_call_requeue_async_with_timespanzero()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RequeueAsync is called with TimeSpan.Zero explicitly (AC-16, FR-15, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath());
        Assert.Contains("TimeSpan.Zero", content);
        Assert.Contains("RequeueAsync(", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_proactor_should_assert_requeue_returns_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the return value of RequeueAsync is captured and asserted true (AC-16)
        var content = await File.ReadAllTextAsync(ProactorOutputPath());
        Assert.Contains("Assert.True(", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_proactor_should_use_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — receipt assertion is inside a bounded retry loop (NFR-2, AC-20, AC-16)
        var content = await File.ReadAllTextAsync(ProactorOutputPath());
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_proactor_should_assert_elapsed_under_five_seconds()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — elapsed time from RequeueAsync call to receipt is asserted less than 5 s (AC-16)
        var content = await File.ReadAllTextAsync(ProactorOutputPath());
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
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

        // Assert — [Fact] present; Skip absent (the conditional pattern renders nothing when Skip is empty)
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9876 (sign-off: @maintainer)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact, Skip = "Deferred: #9876 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath());
        Assert.Contains("Skip = \"Deferred: #9876", content);
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
        var content = await File.ReadAllTextAsync(ProactorOutputPath());
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

    private string ReactorOutputPath() =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Reactor",
            $"{TEMPLATE_NAME}.cs");

    private string ProactorOutputPath() =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Proactor",
            $"{TEMPLATE_NAME}.cs");

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
