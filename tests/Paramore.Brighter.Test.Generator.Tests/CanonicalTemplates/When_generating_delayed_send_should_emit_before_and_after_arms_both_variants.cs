using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the delayed-send templates, both variants: SendWithDelay/SendWithDelayAsync with a 5 s delay, a
/// single receive before the delay that sees nothing, a bounded poll after it, no sleep and no scheduler
/// or broker mechanism, and a Skip supplied by the ledger.
/// </summary>
public class WhenGeneratingDelayedSendShouldEmitBeforeAndAfterArmsBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_sending_a_delayed_message_should_deliver_after_delay";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-9";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingDelayedSendShouldEmitBeforeAndAfterArmsBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"DelayedSendTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_delayed_send_reactor_file_should_exist_with_correct_name()
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
            $"Reactor canonical delayed-send file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_delayed_send_proactor_file_should_exist_with_correct_name()
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
            $"Proactor canonical delayed-send file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_delayed_send_reactor_should_call_send_with_delay_on_producer()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("SendWithDelay(", content);
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
    }

    [Fact]
    public async Task When_generating_delayed_send_proactor_should_call_send_with_delay_async_on_producer()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("SendWithDelayAsync(", content);
        Assert.Contains("TimeSpan.FromSeconds(5)", content);
    }

    [Fact]
    public async Task When_generating_delayed_send_reactor_should_emit_single_before_delay_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("MT_NONE", content);
        Assert.Contains("Receive(", content);
    }

    [Fact]
    public async Task When_generating_delayed_send_proactor_should_emit_single_before_delay_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("MT_NONE", content);
        Assert.Contains("ReceiveAsync(", content);
    }

    [Fact]
    public async Task When_generating_delayed_send_reactor_should_use_bounded_retry_loop_for_after_arm()
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
    public async Task When_generating_delayed_send_proactor_should_use_bounded_retry_loop_for_after_arm()
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
    public async Task When_generating_delayed_send_reactor_should_not_use_thread_sleep_or_task_delay()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.DoesNotContain("Thread.Sleep", content);
        Assert.DoesNotContain("Task.Delay", content);
    }

    [Fact]
    public async Task When_generating_delayed_send_proactor_should_not_use_thread_sleep_or_task_delay()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.DoesNotContain("Thread.Sleep", content);
        Assert.DoesNotContain("Task.Delay", content);
    }

    [Fact]
    public async Task When_generating_delayed_send_reactor_should_not_reference_scheduler_or_mechanism()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.DoesNotContain("scheduler", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RedrivePolicy", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModifyAckDeadline", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChangeInvisibleDuration", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_generating_delayed_send_proactor_should_not_reference_scheduler_or_mechanism()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.DoesNotContain("scheduler", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RedrivePolicy", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModifyAckDeadline", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChangeInvisibleDuration", content, StringComparison.OrdinalIgnoreCase);
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #4567 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #4567", content);
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
