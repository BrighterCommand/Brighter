// MIT License
//
// Copyright (c) 2025 Ian Cooper
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
// BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the redelivery arms of the FR-2, FR-15, FR-16 and FR-22 templates assert
/// that the redelivered message presents a HandledCount greater than or equal to the count
/// of the sent message (R-1 vs R-23, C-7; ADR 0077 "Conformance oracle change for the
/// redelivery arms"), then normalise it back to the sent count before calling the transport's
/// message assertion, in both the Reactor and Proactor variants.
///
/// The four templates are:
///   - When_requeuing_a_failed_message_should_be_redelivered              (FR-22)
///   - When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately  (FR-15)
///   - When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay       (FR-2)
///   - When_nacking_a_message_it_should_be_redelivered                              (FR-16)
///
/// Each generated file must contain, in order:
///   Assert.True(redelivered.Header.HandledCount >= message.Header.HandledCount ...)
///   redelivered.Header.HandledCount = message.Header.HandledCount;
///   _messageAssertion.Assert(message, redelivered)
///
/// First-receive assertions in other templates are unchanged (R-2 still applies).
/// </summary>
public class WhenGeneratingRedeliveryArmsShouldAssertHandledCountAtLeastSentBothVariants : IDisposable
{
    private const string LEDGER_KEY = "Kafka / Classic";

    private const string PLAIN_REQUEUE_TEMPLATE =
        "When_requeuing_a_failed_message_should_be_redelivered";

    private const string ZERO_DELAY_TEMPLATE =
        "When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately";

    private const string DELAY_TEMPLATE =
        "When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay";

    private const string NACK_TEMPLATE = "When_nacking_a_message_it_should_be_redelivered";

    /// <summary>
    /// First-receive template: basic send/receive round-trip (FR-1). The received variable is
    /// named <c>received</c> (not <c>redelivered</c>), so R-2's equality assertion is intact.
    /// </summary>
    private const string POSTING_TEMPLATE =
        "When_posting_a_message_via_the_messaging_gateway_should_be_received";

    /// <summary>
    /// The assertion that the redelivered count is at least the sent count (R-1).
    /// Both templates name the variables <c>message</c> (sent) and <c>redelivered</c>.
    /// </summary>
    private const string HANDLED_COUNT_GE_CHECK =
        "Assert.True(redelivered.Header.HandledCount >= message.Header.HandledCount";

    /// <summary>
    /// The normalisation that resets the redelivered count to the sent count so the
    /// transport's message assertion (which compares for equality) still passes.
    /// </summary>
    private const string HANDLED_COUNT_ASSIGNMENT =
        "redelivered.Header.HandledCount = message.Header.HandledCount;";

    /// <summary>
    /// The transport-specific message assertion call that follows the normalisation.
    /// </summary>
    private const string MESSAGE_ASSERTION_CALL =
        "_messageAssertion.Assert(message, redelivered)";

    /// <summary>
    /// The equality assertion on the first-received message (R-2). The posting template calls
    /// <c>_messageAssertion.Assert</c> directly on <c>received</c> — no >= check, no
    /// normalisation — because R-2 requires the first delivery to present exactly 0.
    /// </summary>
    private const string FIRST_RECEIVE_ASSERTION_CALL =
        "_messageAssertion.Assert(message, received)";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingRedeliveryArmsShouldAssertHandledCountAtLeastSentBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"RedeliveryArmsHandledCountTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    // ── Plain requeue (FR-22) — Reactor ─────────────────────────────────────

    [Fact]
    public async Task When_generating_plain_requeue_reactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-22");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the redelivery arm asserts HandledCount >= sent count, normalises, then asserts
        var content = await File.ReadAllTextAsync(TemplatePath("Reactor", PLAIN_REQUEUE_TEMPLATE));
        AssertRedeliveryArmShape(content, "Reactor plain-requeue");
    }

    [Fact]
    public async Task When_generating_plain_requeue_proactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-22");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the redelivery arm asserts HandledCount >= sent count, normalises, then asserts
        var content = await File.ReadAllTextAsync(TemplatePath("Proactor", PLAIN_REQUEUE_TEMPLATE));
        AssertRedeliveryArmShape(content, "Proactor plain-requeue");
    }

    // ── Zero-delay requeue (FR-15) — Reactor ────────────────────────────────

    [Fact]
    public async Task When_generating_zero_delay_requeue_reactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-15");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the redelivery arm asserts HandledCount >= sent count, normalises, then asserts
        var content = await File.ReadAllTextAsync(TemplatePath("Reactor", ZERO_DELAY_TEMPLATE));
        AssertRedeliveryArmShape(content, "Reactor zero-delay requeue");
    }

    [Fact]
    public async Task When_generating_zero_delay_requeue_proactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-15");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the redelivery arm asserts HandledCount >= sent count, normalises, then asserts
        var content = await File.ReadAllTextAsync(TemplatePath("Proactor", ZERO_DELAY_TEMPLATE));
        AssertRedeliveryArmShape(content, "Proactor zero-delay requeue");
    }

    // ── Delayed requeue (FR-2) — Reactor ────────────────────────────────────

    [Fact]
    public async Task When_generating_delay_requeue_reactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-2");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the redelivery arm asserts HandledCount >= sent count, normalises, then asserts
        var content = await File.ReadAllTextAsync(TemplatePath("Reactor", DELAY_TEMPLATE));
        AssertRedeliveryArmShape(content, "Reactor delayed requeue");
    }

    [Fact]
    public async Task When_generating_delay_requeue_proactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-2");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the redelivery arm asserts HandledCount >= sent count, normalises, then asserts
        var content = await File.ReadAllTextAsync(TemplatePath("Proactor", DELAY_TEMPLATE));
        AssertRedeliveryArmShape(content, "Proactor delayed requeue");
    }

    // ── Nack (FR-16) — Reactor ──────────────────────────────────────────────

    [Fact]
    public async Task When_generating_nack_reactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-16");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the single-message nack redelivery arm asserts HandledCount >= sent count
        var content = await File.ReadAllTextAsync(TemplatePath("Reactor", NACK_TEMPLATE));
        AssertRedeliveryArmShape(content, "Reactor nack redelivery");
    }

    [Fact]
    public async Task When_generating_nack_proactor_should_assert_redelivered_handled_count_is_at_least_sent_count()
    {
        // Arrange
        var ledger = PassLedger("FR-16");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the single-message nack redelivery arm asserts HandledCount >= sent count
        var content = await File.ReadAllTextAsync(TemplatePath("Proactor", NACK_TEMPLATE));
        AssertRedeliveryArmShape(content, "Proactor nack redelivery");
    }

    // ── First-receive (R-2) — posting template, both variants ───────────────
    // These facts characterise UNCHANGED behaviour: the posting template (FR-1) must NOT
    // contain the >= check or the normalisation assignment, and must still call the
    // transport's equality assertion on the first-received message.  A mutation that adds
    // those lines to the posting template must make these facts fail.

    [Fact]
    public async Task When_generating_posting_reactor_should_not_add_handled_count_ge_check_to_first_receive()
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(new Dictionary<(string, string), string>());
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — no >= check on the first-received variable (R-2: first delivery is exact 0)
        var content = await File.ReadAllTextAsync(TemplatePath("Reactor", POSTING_TEMPLATE));
        Assert.DoesNotContain(HANDLED_COUNT_GE_CHECK, content);
        Assert.DoesNotContain(HANDLED_COUNT_ASSIGNMENT, content);
    }

    [Fact]
    public async Task When_generating_posting_proactor_should_not_add_handled_count_ge_check_to_first_receive()
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(new Dictionary<(string, string), string>());
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — no >= check on the first-received variable (R-2: first delivery is exact 0)
        var content = await File.ReadAllTextAsync(TemplatePath("Proactor", POSTING_TEMPLATE));
        Assert.DoesNotContain(HANDLED_COUNT_GE_CHECK, content);
        Assert.DoesNotContain(HANDLED_COUNT_ASSIGNMENT, content);
    }

    [Fact]
    public async Task When_generating_posting_reactor_should_still_call_equality_assertion_on_first_received_message()
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(new Dictionary<(string, string), string>());
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — _messageAssertion.Assert is still called directly on the received message;
        // R-2's equality check is intact because no normalisation precedes it.
        var content = await File.ReadAllTextAsync(TemplatePath("Reactor", POSTING_TEMPLATE));
        AssertFirstReceiveIntact(content, "Reactor posting");
    }

    [Fact]
    public async Task When_generating_posting_proactor_should_still_call_equality_assertion_on_first_received_message()
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(new Dictionary<(string, string), string>());
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — _messageAssertion.Assert is still called directly on the received message;
        // R-2's equality check is intact because no normalisation precedes it.
        var content = await File.ReadAllTextAsync(TemplatePath("Proactor", POSTING_TEMPLATE));
        AssertFirstReceiveIntact(content, "Proactor posting");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the three-statement redelivery-arm shape required by ADR 0077:
    /// <list type="number">
    ///   <item><c>Assert.True(redelivered.Header.HandledCount &gt;= message.Header.HandledCount …)</c></item>
    ///   <item><c>redelivered.Header.HandledCount = message.Header.HandledCount;</c></item>
    ///   <item><c>_messageAssertion.Assert(message, redelivered)</c></item>
    /// </list>
    /// and that they appear in that order.
    /// </summary>
    private static void AssertRedeliveryArmShape(string content, string label)
    {
        // All three statements must be present
        Assert.Contains(HANDLED_COUNT_GE_CHECK, content);
        Assert.Contains(HANDLED_COUNT_ASSIGNMENT, content);
        Assert.Contains(MESSAGE_ASSERTION_CALL, content);

        // They must appear in the correct order: >= check, then assignment, then assertion call
        var geCheckIndex = content.IndexOf(HANDLED_COUNT_GE_CHECK, StringComparison.Ordinal);
        var assignmentIndex = content.IndexOf(HANDLED_COUNT_ASSIGNMENT, StringComparison.Ordinal);
        var assertCallIndex = content.IndexOf(MESSAGE_ASSERTION_CALL, StringComparison.Ordinal);

        Assert.True(
            geCheckIndex < assignmentIndex,
            $"{label}: >= check (at {geCheckIndex}) must precede the HandledCount assignment (at {assignmentIndex})");

        Assert.True(
            assignmentIndex < assertCallIndex,
            $"{label}: HandledCount assignment (at {assignmentIndex}) must precede _messageAssertion.Assert call (at {assertCallIndex})");
    }

    private static InMemoryConformanceLedger PassLedger(string frColumn) =>
        new(new Dictionary<(string, string), string>
        {
            [(LEDGER_KEY, frColumn)] = "Pass"
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

    /// <summary>
    /// Verifies the first-receive shape required by R-2:
    /// <list type="number">
    ///   <item><c>_messageAssertion.Assert(message, received)</c> is present — equality check intact</item>
    ///   <item><c>HANDLED_COUNT_GE_CHECK</c> is absent — no >= relaxation on first receive</item>
    ///   <item><c>HANDLED_COUNT_ASSIGNMENT</c> is absent — no normalisation before assertion</item>
    /// </list>
    /// </summary>
    private static void AssertFirstReceiveIntact(string content, string label)
    {
        // R-2: equality assertion must still be called on the first-received message
        Assert.Contains(FIRST_RECEIVE_ASSERTION_CALL, content);

        // The >= check and normalisation must NOT appear — they belong only in redelivery arms
        Assert.DoesNotContain(HANDLED_COUNT_GE_CHECK, content);
        Assert.DoesNotContain(HANDLED_COUNT_ASSIGNMENT, content);
    }

    private string TemplatePath(string variant, string templateName) =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", variant,
            $"{templateName}.cs");

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
