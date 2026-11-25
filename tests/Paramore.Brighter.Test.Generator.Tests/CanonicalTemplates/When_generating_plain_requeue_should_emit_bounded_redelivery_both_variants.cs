using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the plain requeue templates, both variants: Requeue/RequeueAsync with no delay returns true,
/// redelivery is polled in a bounded loop, and the Skip is supplied by the ledger.
/// </summary>
public class WhenGeneratingPlainRequeueShouldEmitBoundedRedeliveryBothVariants : IDisposable
{
    private const string TEMPLATE_NAME = "When_requeuing_a_failed_message_should_be_redelivered";
    private const string LEDGER_KEY = "Kafka / Classic";
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

        // Assert
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

        // Assert
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

        // Assert
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

        // Assert
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

        // Assert
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

        // Assert
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

        // Assert
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

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #1234 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #1234", content);
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
