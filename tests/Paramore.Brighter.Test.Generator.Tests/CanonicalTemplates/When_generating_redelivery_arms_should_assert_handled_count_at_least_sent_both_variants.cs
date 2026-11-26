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
using Xunit;
using static Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates.CanonicalTemplateSetup;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Pins the redelivery arm of the plain, zero-delay, delayed and nack requeue templates, both variants:
/// assert the redelivered HandledCount is at least the sent one, normalise it, then call the transport's
/// message assertion, in that order. The posting template's first receive keeps its exact equality check.
/// See ADR 0077, Conformance oracle change for the redelivery arms.
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

    private const string POSTING_TEMPLATE =
        "When_posting_a_message_via_the_messaging_gateway_should_be_received";

    private const string HANDLED_COUNT_GE_CHECK =
        "Assert.True(redelivered.Header.HandledCount >= message.Header.HandledCount";

    private const string HANDLED_COUNT_ASSIGNMENT =
        "redelivered.Header.HandledCount = message.Header.HandledCount;";

    private const string MESSAGE_ASSERTION_CALL =
        "_messageAssertion.Assert(message, redelivered)";

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

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_plain_requeue_should_assert_redelivered_handled_count_is_at_least_sent_count(
        string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, "FR-22");
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, PLAIN_REQUEUE_TEMPLATE));
        AssertRedeliveryArmShape(content, $"{variant} plain-requeue");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_zero_delay_requeue_should_assert_redelivered_handled_count_is_at_least_sent_count(
        string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, "FR-15");
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, ZERO_DELAY_TEMPLATE));
        AssertRedeliveryArmShape(content, $"{variant} zero-delay requeue");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_delay_requeue_should_assert_redelivered_handled_count_is_at_least_sent_count(
        string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, "FR-2");
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, DELAY_TEMPLATE));
        AssertRedeliveryArmShape(content, $"{variant} delayed requeue");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_nack_should_assert_redelivered_handled_count_is_at_least_sent_count(
        string variant)
    {
        // Arrange
        var ledger = PassLedger(LEDGER_KEY, "FR-16");
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, NACK_TEMPLATE));
        AssertRedeliveryArmShape(content, $"{variant} nack redelivery");
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_posting_should_not_add_handled_count_ge_check_to_first_receive(string variant)
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(new Dictionary<(string, string), string>());
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, POSTING_TEMPLATE));
        Assert.DoesNotContain(HANDLED_COUNT_GE_CHECK, content);
        Assert.DoesNotContain(HANDLED_COUNT_ASSIGNMENT, content);
    }

    [Theory]
    [InlineData(REACTOR)]
    [InlineData(PROACTOR)]
    public async Task When_generating_posting_should_still_call_equality_assertion_on_first_received_message(
        string variant)
    {
        // Arrange
        var ledger = new InMemoryConformanceLedger(new Dictionary<(string, string), string>());
        var configuration = BuildConfiguration(_testDirectory, LEDGER_KEY);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var content = await File.ReadAllTextAsync(OutputPath(_testDirectory, variant, POSTING_TEMPLATE));
        AssertFirstReceiveIntact(content, $"{variant} posting");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AssertRedeliveryArmShape(string content, string label)
    {
        Assert.Contains(HANDLED_COUNT_GE_CHECK, content);
        Assert.Contains(HANDLED_COUNT_ASSIGNMENT, content);
        Assert.Contains(MESSAGE_ASSERTION_CALL, content);

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

    private static void AssertFirstReceiveIntact(string content, string label)
    {
        Assert.Contains(FIRST_RECEIVE_ASSERTION_CALL, content);

        Assert.DoesNotContain(HANDLED_COUNT_GE_CHECK, content);
        Assert.DoesNotContain(HANDLED_COUNT_ASSIGNMENT, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
