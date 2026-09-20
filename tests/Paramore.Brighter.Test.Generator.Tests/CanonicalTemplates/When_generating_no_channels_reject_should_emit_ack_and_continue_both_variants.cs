using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical no-channels-configured templates emit both a Reactor and a
/// Proactor variant that:
///   - name the file When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log;
///   - create the subscription with NEITHER a deadLetterRoutingKey nor an invalidMessageRoutingKey;
///   - call channel.Reject / RejectAsync with DeliveryError and assert the return is true;
///   - assert the following message's receipt INSIDE the bounded retry loop (Stopwatch, 500 ms,
///     30 s);
///   - build the rejected message and the one queued behind it with their own id and body, and
///     identify the message that arrives, so a rejected message that came back cannot pass for
///     the one that should have followed it;
///   - emit the conditional ledger-driven Skip so the Deferred marker is supplied by the
///     conformance ledger, not hard-coded in the template;
///   - do NOT assert logging (_and_log suffix retained for naming continuity only).
/// </summary>
public class WhenGeneratingNoChannelsRejectShouldEmitAckAndContinueBothVariants : IDisposable
{
    private const string TEMPLATE_NAME =
        "When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log";

    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-7";

    /// <summary>
    /// The two messages the generated test queues: the one it rejects, and the one queued behind
    /// it. Each must be built with its own identity, or the two are the same message.
    /// </summary>
    private const int DISTINCTLY_BUILT_MESSAGES = 2;

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingNoChannelsRejectShouldEmitAckAndContinueBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"NoChannelsRejectTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_no_channels_reject_reactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor file exists at the mandated path
        var reactorPath = ReactorOutputPath(configuration);
        Assert.True(File.Exists(reactorPath),
            $"Reactor canonical no-channels file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_no_channels_reject_proactor_file_should_exist_with_correct_name()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Proactor file exists at the mandated path
        var proactorPath = ProactorOutputPath(configuration);
        Assert.True(File.Exists(proactorPath),
            $"Proactor canonical no-channels file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_no_channels_reject_reactor_should_create_subscription_with_neither_routing_key()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — subscription must have neither deadLetterRoutingKey nor invalidMessageRoutingKey
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.DoesNotContain("deadLetterRoutingKey:", content);
        Assert.DoesNotContain("invalidMessageRoutingKey:", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_proactor_should_create_subscription_with_neither_routing_key()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — subscription must have neither deadLetterRoutingKey nor invalidMessageRoutingKey
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.DoesNotContain("deadLetterRoutingKey:", content);
        Assert.DoesNotContain("invalidMessageRoutingKey:", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_reactor_should_reject_with_delivery_error_and_assert_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reject is called with DeliveryError and return is asserted true
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Reject(", content);
        Assert.Contains("DeliveryError", content);
        Assert.Contains("Assert.True(rejected", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_proactor_should_reject_async_with_delivery_error_and_assert_true()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — RejectAsync is called with DeliveryError and return is asserted true
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("RejectAsync(", content);
        Assert.Contains("DeliveryError", content);
        Assert.Contains("Assert.True(rejected", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_reactor_should_poll_for_the_following_message_inside_a_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the following message's receipt is polled inside the bounded retry loop
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_proactor_should_poll_for_the_following_message_inside_a_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the following message's receipt is polled inside the bounded retry loop
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_reactor_should_tell_the_following_message_from_the_rejected_one()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the rejected message and the one queued behind it are each built with their own
        // id and body. Sharing a builder without this makes them the same message, and a rejected
        // message that came back would then be indistinguishable from the one that should follow.
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, "SetMessageId(Id.Random())"));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, ".SetBody("));

        // Assert — the arm identifies the message that follows by id rather than assuming it is
        // the one sent second, and forbids the rejected message coming back. Identifying by id is
        // what lets this hold on a transport that does not order its deliveries (NFR-4).
        Assert.Contains("_messageAssertion.Assert(theOtherMessage, receivedOther)", content);
        Assert.Contains("Assert.NotEqual(rejectedMessage.Header.MessageId, next.Header.MessageId)", content);
    }

    [Fact]
    public async Task When_generating_no_channels_reject_proactor_should_tell_the_following_message_from_the_rejected_one()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the rejected message and the one queued behind it are each built with their own
        // id and body. Sharing a builder without this makes them the same message, and a rejected
        // message that came back would then be indistinguishable from the one that should follow.
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, "SetMessageId(Id.Random())"));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, ".SetBody("));

        // Assert — the arm identifies the message that follows by id rather than assuming it is
        // the one sent second, and forbids the rejected message coming back. Identifying by id is
        // what lets this hold on a transport that does not order its deliveries (NFR-4).
        Assert.Contains("_messageAssertion.Assert(theOtherMessage, receivedOther)", content);
        Assert.Contains("Assert.NotEqual(rejectedMessage.Header.MessageId, next.Header.MessageId)", content);
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

        // Assert — [Fact] present; Skip absent (conditional pattern renders nothing when Skip is empty)
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
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9999 (sign-off: @iancooper)"
            });
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — [Fact, Skip = "Deferred: #9999 ..."] is emitted
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Skip = \"Deferred: #9999", content);
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

    /// <summary>
    /// Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="content"/>,
    /// so a fact can assert the exact number of build sites rather than merely that one exists.
    /// </summary>
    private static int Occurrences(string content, string needle)
    {
        var count = 0;
        for (var at = content.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = content.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

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
