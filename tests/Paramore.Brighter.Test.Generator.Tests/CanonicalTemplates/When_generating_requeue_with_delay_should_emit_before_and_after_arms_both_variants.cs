using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical requeue-with-delay templates (FR-2) emit both a Reactor and a
/// Proactor variant that:
///   - pass a non-null positive TimeSpan (5 s) to Requeue/RequeueAsync (AC-12, FR-2);
///   - assert Requeue returns true (AC-2);
///   - assert an immediate single bounded receive before the delay yields MT_NONE (AC-2, AC-20
///     exemption — the before-D arm is a single receive, NOT the bounded retry loop);
///   - assert arrival INSIDE the bounded retry loop (500 ms poll, 30 s ceiling — NFR-2) after
///     the delay (AC-2, the after-D arm);
///   - contain no reference to a scheduler, native-delay API, or redrive policy (AC-21, NFR-3);
///   - emit the conditional ledger-driven Skip pattern so the Deferred marker is supplied
///     by the conformance ledger, not hard-coded in the template (FR-21).
/// </summary>
public class WhenGeneratingRequeueWithDelayShouldEmitBeforeAndAfterArmsBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay";

    private const string LEDGER_KEY = "Kafka / Standard";
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

    [Fact]
    public async Task When_generating_requeue_with_delay_reactor_file_should_exist_with_correct_name()
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
            $"Reactor canonical requeue-with-delay file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_proactor_file_should_exist_with_correct_name()
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
            $"Proactor canonical requeue-with-delay file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_reactor_should_pass_positive_timespan_to_requeue()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Requeue is called with a non-null positive TimeSpan (AC-12, FR-2)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
        Assert.Contains("Requeue(", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_reactor_should_assert_requeue_returns_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the return value of Requeue is captured and asserted true (AC-2)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Assert.True(", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_reactor_should_emit_single_before_delay_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — before-D arm: a single Receive asserting MT_NONE (AC-2, AC-20 exemption;
        // this must NOT be inside the bounded Stopwatch retry loop)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("MT_NONE", content);
        Assert.Contains("Receive(", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_reactor_should_use_bounded_retry_loop_for_after_arm()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — after-D arm uses the bounded retry loop (NFR-2, AC-20)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_reactor_should_not_reference_scheduler_or_mechanism()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — no mechanism assertions (AC-21, NFR-3)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.DoesNotContain("scheduler", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RedrivePolicy", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModifyAckDeadline", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChangeInvisibleDuration", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_proactor_should_pass_positive_timespan_to_requeue_async()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RequeueAsync is called with a non-null positive TimeSpan (AC-12, FR-2, FR-14)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
        Assert.Contains("RequeueAsync(", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_proactor_should_assert_requeue_returns_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the return value of RequeueAsync is captured and asserted true (AC-2)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Assert.True(", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_proactor_should_emit_single_before_delay_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — before-D arm: a single ReceiveAsync asserting MT_NONE (AC-2, AC-20 exemption)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("MT_NONE", content);
        Assert.Contains("ReceiveAsync(", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_proactor_should_use_bounded_retry_loop_for_after_arm()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — after-D arm uses the bounded retry loop (NFR-2, AC-20)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_requeue_with_delay_proactor_should_not_reference_scheduler_or_mechanism()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — no mechanism assertions (AC-21, NFR-3)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.DoesNotContain("scheduler", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RedrivePolicy", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModifyAckDeadline", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChangeInvisibleDuration", content, StringComparison.OrdinalIgnoreCase);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #4567 (sign-off: @maintainer)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact, Skip = "Deferred: #4567 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #4567", content);
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
