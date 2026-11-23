using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins that the two arms queuing more than one message, nack and no-channels rejection, identify what
/// they receive by id, never by arrival position (ADR 0066, Conventions the canonical templates follow).
/// The single-message nack arm keeps its positional assertion: with one message in flight there is no order.
/// </summary>
public class TwoMessageArmsIdentifyReceivedMessagesByIdTests : IDisposable
{
    private const string NACK_TEMPLATE = "When_nacking_a_message_it_should_be_redelivered";
    private const string NO_CHANNELS_TEMPLATE =
        "When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log";

    private const string LEDGER_KEY = "Kafka / Classic";

    private const string IDENTIFIES_BY_ID = "Header.MessageId";

    private const string NACKED_PAIRED_POSITIONALLY =
        "_messageAssertion.Assert(nackedMessage, redelivered)";
    private const string FOLLOWING_PAIRED_POSITIONALLY =
        "_messageAssertion.Assert(followingMessage, receivedFollowing)";

    private const string SINGLE_MESSAGE_ASSERTION =
        "_messageAssertion.Assert(message, redelivered)";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public TwoMessageArmsIdentifyReceivedMessagesByIdTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"OrderingNeutralityTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_nack_reactor_should_identify_received_messages_by_id()
    {
        // Arrange
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, PassLedger());

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath("Reactor", NACK_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(NACKED_PAIRED_POSITIONALLY, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);

        Assert.Contains(SINGLE_MESSAGE_ASSERTION, content);
    }

    [Fact]
    public async Task When_generating_nack_proactor_should_identify_received_messages_by_id()
    {
        // Arrange
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, PassLedger());

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath("Proactor", NACK_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(NACKED_PAIRED_POSITIONALLY, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);

        Assert.Contains(SINGLE_MESSAGE_ASSERTION, content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_reactor_should_identify_received_messages_by_id()
    {
        // Arrange
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, PassLedger());

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath("Reactor", NO_CHANNELS_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_proactor_should_identify_received_messages_by_id()
    {
        // Arrange
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, PassLedger());

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath("Proactor", NO_CHANNELS_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static InMemoryConformanceLedger PassLedger() =>
        new(new Dictionary<(string, string), string>
        {
            [(LEDGER_KEY, "FR-16")] = "Pass",
            [(LEDGER_KEY, "FR-7")] = "Pass"
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

    private string OutputPath(string variant, string templateName) =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", variant,
            $"{templateName}.cs");

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
