using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;


namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical unacceptable-reject templates emit both a Reactor and a
/// Proactor variant that:
///   - name the file When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel;
///   - create the subscription with BOTH a deadLetterRoutingKey AND an invalidMessageRoutingKey
///     named argument;
///   - call _channel.Reject with an Unacceptable MessageRejectionReason;
///   - poll for invalid-channel arrival INSIDE the bounded retry loop (Stopwatch, 500 ms, 60 s);
///   - assert the rejection reason equals "Unacceptable" and the original-topic equals the data topic;
///   - assert DLQ absence via a SINGLE bounded GetMessageFromDeadLetterQueue call outside the retry
///     loop, asserting MT_NONE (a single receive outside the retry loop);
///   - emit the conditional ledger-driven Skip so the Deferred marker is supplied by the
///     conformance ledger, not hard-coded in the template.
/// </summary>
public class WhenGeneratingUnacceptableRejectShouldEmitInvalidChannelRoutingBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-5";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingUnacceptableRejectShouldEmitInvalidChannelRoutingBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UnacceptableRejectTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Test]
    public async Task When_generating_unacceptable_reject_reactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor file exists at the mandated path
        var reactorPath = ReactorOutputPath(configuration);
        await Assert.That(File.Exists(reactorPath)).IsTrue().Because($"Reactor canonical unacceptable-reject file not found at {reactorPath}");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Proactor file exists at the mandated path
        var proactorPath = ProactorOutputPath(configuration);
        await Assert.That(File.Exists(proactorPath)).IsTrue().Because($"Proactor canonical unacceptable-reject file not found at {proactorPath}");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_reactor_should_create_subscription_with_both_routing_keys()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — subscription must name both deadLetterRoutingKey and invalidMessageRoutingKey
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("deadLetterRoutingKey:");
        await Assert.That(content).Contains("invalidMessageRoutingKey:");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_proactor_should_create_subscription_with_both_routing_keys()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — subscription must name both deadLetterRoutingKey and invalidMessageRoutingKey
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        await Assert.That(content).Contains("deadLetterRoutingKey:");
        await Assert.That(content).Contains("invalidMessageRoutingKey:");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_reactor_should_reject_with_unacceptable_reason()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reject is called with Unacceptable
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("Reject(");
        await Assert.That(content).Contains("Unacceptable");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_proactor_should_reject_with_unacceptable_reason()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectAsync is called with Unacceptable
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        await Assert.That(content).Contains("RejectAsync(");
        await Assert.That(content).Contains("Unacceptable");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_reactor_should_poll_invalid_channel_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — invalid-channel arrival polled inside the bounded retry loop
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("GetMessageFromInvalidChannel");
        await Assert.That(content).Contains("Stopwatch");
        await Assert.That(content).Contains("TimeSpan.FromSeconds(60)");
        await Assert.That(content).Contains("500");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_proactor_should_poll_invalid_channel_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — invalid-channel arrival polled inside the bounded retry loop
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        await Assert.That(content).Contains("GetMessageFromInvalidChannelAsync");
        await Assert.That(content).Contains("Stopwatch");
        await Assert.That(content).Contains("TimeSpan.FromSeconds(60)");
        await Assert.That(content).Contains("500");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_reactor_should_assert_unacceptable_reason_on_invalid_channel()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — rejection reason "Unacceptable" and original-topic assertion
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("if (keys.StampsRejectionMetadata)");
        await Assert.That(content).Contains("keys.RejectionReason");
        await Assert.That(content).Contains("keys.OriginalTopic");
        await Assert.That(content).Contains("_publication.Topic!.Value");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_proactor_should_assert_unacceptable_reason_on_invalid_channel()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — rejection reason "Unacceptable" and original-topic assertion
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        await Assert.That(content).Contains("if (keys.StampsRejectionMetadata)");
        await Assert.That(content).Contains("keys.RejectionReason");
        await Assert.That(content).Contains("keys.OriginalTopic");
        await Assert.That(content).Contains("_publication.Topic!.Value");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_reactor_should_assert_dlq_absence_via_single_bounded_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — DLQ absence: single bounded GetMessageFromDeadLetterQueue call asserting MT_NONE
        // (a single receive outside the retry loop)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("GetMessageFromDeadLetterQueue");
        await Assert.That(content).Contains("MT_NONE");
    }

    [Test]
    public async Task When_generating_unacceptable_reject_proactor_should_assert_dlq_absence_via_single_bounded_receive()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — DLQ absence: single bounded GetMessageFromDeadLetterQueueAsync call asserting MT_NONE
        // (a single receive outside the retry loop)
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        await Assert.That(content).Contains("GetMessageFromDeadLetterQueueAsync");
        await Assert.That(content).Contains("MT_NONE");
    }

    [Test]
    public async Task When_ledger_is_pass_reactor_should_emit_fact_without_skip()
    {
        // Arrange — ledger cell is Pass; the [Test] must carry no Skip argument
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Test] present; Skip absent (conditional pattern renders nothing when Skip is empty)
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("[Test]");
        await Assert.That(content).DoesNotContain("[Skip(");
    }

    [Test]
    public async Task When_ledger_is_deferred_reactor_should_emit_skip_on_fact()
    {
        // Arrange — ledger cell is Deferred; the template must conditionally emit Skip
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9876 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Test, Skip = "Deferred: #9876 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        await Assert.That(content).Contains("Skip(\"Deferred: #9876");
    }

    [Test]
    public async Task When_ledger_is_pass_proactor_should_emit_fact_without_skip()
    {
        // Arrange — ledger cell is Pass; the [Test] must carry no Skip argument
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        await Assert.That(content).Contains("[Test]");
        await Assert.That(content).DoesNotContain("[Skip(");
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
