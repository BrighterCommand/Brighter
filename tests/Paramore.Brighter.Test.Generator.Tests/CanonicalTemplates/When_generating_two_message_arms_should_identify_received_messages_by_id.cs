using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies NFR-4 (Ordering neutrality) for the two canonical arms that queue more than one
/// message — FR-16's nack-redelivery variant and FR-7's no-channels rejection.
///
/// Delivery order belongs to the transport, not to Brighter, and the targeted transports do not
/// share one: SNS/SQS Standard and GCP Pub/Sub without an ordering key guarantee nothing. An arm
/// that pairs a named sent message with whichever message happened to arrive first is therefore
/// asserting the broker's ordering guarantee rather than the gateway's behaviour, and fails a
/// conforming gateway on an unordered transport — which is exactly what `aws-ci` observed.
///
/// So each arm MUST identify the messages it receives by id, and state its claims over the set of
/// ids observed within the NFR-2 bound, never over their arrival positions.
///
/// These facts deliberately leave the SINGLE-message nack arm alone. With one message in flight
/// there is no ordering question, so `_messageAssertion.Assert(message, redelivered)` is a correct
/// positional assertion and is asserted here to survive — a fix that strips identity assertions
/// wholesale would break the very thing FR-16 exists to prove.
/// </summary>
public class TwoMessageArmsIdentifyReceivedMessagesByIdTests : IDisposable
{
    private const string NACK_TEMPLATE = "When_nacking_a_message_it_should_be_redelivered";
    private const string NO_CHANNELS_TEMPLATE =
        "When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log";

    private const string LEDGER_KEY = "Kafka / Classic";

    /// <summary>
    /// The id-identification idiom. Its absence is what let the arms read a message's identity off
    /// its arrival position instead.
    /// </summary>
    private const string IDENTIFIES_BY_ID = "Header.MessageId";

    /// <summary>
    /// The defective pairings: a named sent message asserted against a positionally-received one.
    /// </summary>
    private const string NACKED_PAIRED_POSITIONALLY =
        "_messageAssertion.Assert(nackedMessage, redelivered)";
    private const string FOLLOWING_PAIRED_POSITIONALLY =
        "_messageAssertion.Assert(followingMessage, receivedFollowing)";

    /// <summary>
    /// The single-message nack arm's positional assertion, which is legitimate and must remain.
    /// </summary>
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

        // Assert — the two-message arm identifies what it received by id, not by position
        var content = await File.ReadAllTextAsync(OutputPath("Reactor", NACK_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(NACKED_PAIRED_POSITIONALLY, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);

        // Assert — and the single-message arm, where order cannot arise, keeps its identity check
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

        // Assert — the two-message arm identifies what it received by id, not by position
        var content = await File.ReadAllTextAsync(OutputPath("Proactor", NACK_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(NACKED_PAIRED_POSITIONALLY, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);

        // Assert — and the single-message arm, where order cannot arise, keeps its identity check
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

        // Assert — the rejected message is whichever arrived first, so the message that follows is
        // identified by id rather than assumed to be the one sent second
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

        // Assert — the rejected message is whichever arrived first, so the message that follows is
        // identified by id rather than assumed to be the one sent second
        var content = await File.ReadAllTextAsync(OutputPath("Proactor", NO_CHANNELS_TEMPLATE));
        Assert.Contains(IDENTIFIES_BY_ID, content);
        Assert.DoesNotContain(FOLLOWING_PAIRED_POSITIONALLY, content);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A ledger that proves both behaviours, so neither arm is emitted with a Skip and the
    /// generated body is present to be read.
    /// </summary>
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
