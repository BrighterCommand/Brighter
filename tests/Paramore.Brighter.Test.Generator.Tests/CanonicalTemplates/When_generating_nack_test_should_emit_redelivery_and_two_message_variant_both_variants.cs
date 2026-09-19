using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the canonical nack-redelivery templates emit both a Reactor and a
/// Proactor variant that:
///   - name the file When_nacking_a_message_it_should_be_redelivered;
///   - call channel.Nack / NackAsync on the received message;
///   - assert redelivery INSIDE the bounded retry loop (Stopwatch, 500 ms, 30 s);
///   - assert the redelivered message has the same id as the nacked message;
///   - include a two-message variant proving the nacked message comes back and the one queued
///     behind it is not blocked;
///   - emit the conditional ledger-driven Skip so the Deferred marker is supplied by the
///     conformance ledger, not hard-coded in the template;
///   - do NOT assert any transport mechanism.
/// </summary>
public class WhenGeneratingNackTestShouldEmitRedeliveryAndTwoMessageVariantBothVariants : IDisposable
{
    private const string TEMPLATE_NAME = "When_nacking_a_message_it_should_be_redelivered";
    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR_COLUMN = "FR-16";

    /// <summary>
    /// The three messages the generated suite queues across its two facts. Each must be built with
    /// its own identity, or a nacked message coming back cannot be told from the one behind it.
    /// </summary>
    private const int DISTINCTLY_BUILT_MESSAGES = 3;

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingNackTestShouldEmitRedeliveryAndTwoMessageVariantBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"NackRedeliveryTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_nack_reactor_file_should_exist_with_correct_name()
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
            $"Reactor canonical nack-redelivery file not found at {reactorPath}");
    }

    [Fact]
    public async Task When_generating_nack_proactor_file_should_exist_with_correct_name()
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
            $"Proactor canonical nack-redelivery file not found at {proactorPath}");
    }

    [Fact]
    public async Task When_generating_nack_reactor_should_call_nack_on_received_message()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Nack is called on the received message
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Nack(", content);
    }

    [Fact]
    public async Task When_generating_nack_proactor_should_call_nack_async_on_received_message()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — NackAsync is called on the received message
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("NackAsync(", content);
    }

    [Fact]
    public async Task When_generating_nack_reactor_should_assert_redelivery_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — redelivery assertion sits inside a bounded retry loop
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_nack_proactor_should_assert_redelivery_inside_bounded_retry_loop()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — redelivery assertion sits inside a bounded retry loop
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("Stopwatch", content);
        Assert.Contains("TimeSpan.FromSeconds(30)", content);
        Assert.Contains("500", content);
    }

    [Fact]
    public async Task When_generating_nack_reactor_should_contain_two_message_variant()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the two-message fact is present: the nacked message comes back, and the one
        // queued behind it still arrives. Asserted by the fact's name rather than by a variable
        // name, which is incidental to the behaviour.
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Contains("When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second", content);
    }

    [Fact]
    public async Task When_generating_nack_proactor_should_contain_two_message_variant()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the two-message fact is present: the nacked message comes back, and the one
        // queued behind it still arrives. Asserted by the fact's name rather than by a variable
        // name, which is incidental to the behaviour.
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Contains("When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second", content);
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

    [Fact]
    public async Task When_generating_nack_reactor_should_tell_the_following_message_from_the_nacked_one()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — every message is built with its own id and body. Drawing them from one shared
        // builder makes them the same message, and the nacked message coming back would then be
        // indistinguishable from the one queued behind it.
        var content = await File.ReadAllTextAsync(ReactorOutputPath(configuration));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, "SetMessageId(Id.Random())"));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, ".SetBody("));

        // Assert — the arm names BOTH ids it expects to observe after the nack, rather than
        // settling for the weaker claim that a message arrived. It identifies them by id, not by
        // arrival position, so an unordered transport cannot fail a conforming gateway (NFR-4).
        Assert.Contains("Assert.Contains(nackedMessage.Header.MessageId.Value, observedIds)", content);
        Assert.Contains("Assert.Contains(theOtherMessage.Header.MessageId.Value, observedIds)", content);
    }

    [Fact]
    public async Task When_generating_nack_proactor_should_tell_the_following_message_from_the_nacked_one()
    {
        // Arrange
        var ledger = PassLedger();
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — every message is built with its own id and body. Drawing them from one shared
        // builder makes them the same message, and the nacked message coming back would then be
        // indistinguishable from the one queued behind it.
        var content = await File.ReadAllTextAsync(ProactorOutputPath(configuration));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, "SetMessageId(Id.Random())"));
        Assert.Equal(DISTINCTLY_BUILT_MESSAGES, Occurrences(content, ".SetBody("));

        // Assert — the arm names BOTH ids it expects to observe after the nack, rather than
        // settling for the weaker claim that a message arrived. It identifies them by id, not by
        // arrival position, so an unordered transport cannot fail a conforming gateway (NFR-4).
        Assert.Contains("Assert.Contains(nackedMessage.Header.MessageId.Value, observedIds)", content);
        Assert.Contains("Assert.Contains(theOtherMessage.Header.MessageId.Value, observedIds)", content);
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
