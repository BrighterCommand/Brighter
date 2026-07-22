using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical plain-requeue templates (FR-22) emit both a Reactor and a
/// Proactor variant that call Requeue/RequeueAsync with no positive delay, assert the return
/// is true, and assert redelivery inside a bounded receive-retry loop (500 ms poll, 30 s
/// ceiling — NFR-2, AC-20). The [Fact] conditional Skip pattern must be present so the
/// ledger-driven mechanism (prior task) drives the Deferred marker without the template
/// hard-coding any marker itself.
/// </summary>
public class WhenGeneratingPlainRequeueShouldEmitBoundedRedeliveryBothVariants : IDisposable
{
    private const string TEMPLATE_NAME = "When_requeuing_a_failed_message_should_be_redelivered";
    private const string LEDGER_KEY = "Kafka / Standard";
    private const string FR_COLUMN = "FR-22";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingPlainRequeueShouldEmitBoundedRedeliveryBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"PlainRequeueTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_plain_requeue_reactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor file exists at the expected path (NFR-1)
        var reactorPath = ReactorOutputPath(configuration);
        Assert.True(File.Exists(reactorPath),
            $"Reactor canonical plain-requeue file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_plain_requeue_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Proactor file exists at the expected path (NFR-1)
        var proactorPath = ProactorOutputPath(configuration);
        Assert.True(File.Exists(proactorPath),
            $"Proactor canonical plain-requeue file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_plain_requeue_reactor_should_call_requeue_with_no_delay()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Requeue is called without a positive delay argument (AC-25, FR-22)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Requeue(received)", content);
        Assert.DoesNotContain("TimeSpan.FromSeconds", content.Replace("TimeSpan.FromSeconds(30)", string.Empty));
    }

    [Fact]
    public async Task When_generating_plain_requeue_reactor_should_assert_requeue_returns_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the return value of Requeue is captured and asserted true (AC-25)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Assert.True(", content);
    }

    [Fact]
    public async Task When_generating_plain_requeue_reactor_should_use_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — a bounded retry loop with wall-clock ceiling is present (NFR-2, AC-20)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_plain_requeue_proactor_should_call_requeue_async_with_no_delay()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RequeueAsync is called without a positive delay argument (AC-25, FR-22, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("RequeueAsync(received)", content);
    }

    [Fact]
    public async Task When_generating_plain_requeue_proactor_should_assert_requeue_returns_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the return value of RequeueAsync is captured and asserted true (AC-25)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Assert.True(", content);
    }

    [Fact]
    public async Task When_generating_plain_requeue_proactor_should_use_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — a bounded retry loop with wall-clock ceiling is present (NFR-2, AC-20)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #1234 (sign-off: @maintainer)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact, Skip = "Deferred: #1234 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #1234", content);
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
