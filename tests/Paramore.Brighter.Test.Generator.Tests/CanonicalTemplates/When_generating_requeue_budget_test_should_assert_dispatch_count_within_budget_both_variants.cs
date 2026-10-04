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
/// Verifies that the generated FR-23 (Reactor and Proactor) templates assert the dispatch count
/// within the delivery budget and stamp the DeliveryError rejection reason on the dead-letter
/// message (R-27(c)(4), R-4, AC-3, AC-36 last two clauses, NFR-7).
///
/// Specifically asserts all obligations in one shot:
/// <list type="number">
///   <item>The dispatch count is read via ConformanceDeferredPump.GetDispatchCount(ConformanceDeferredPump.KeyOf(message))</item>
///   <item>The dispatch count assertion (&lt;= _subscription.RequeueCount) appears after await pumping</item>
///   <item>When keys.StampsRejectionMetadata is true, the generated code asserts
///         dlqMessage.Header.Bag[keys.RejectionReason] == "DeliveryError", with the assertion
///         inside the StampsRejectionMetadata guard (R-5: the route was Brighter-managed)</item>
///   <item>The existing HandledCount &gt;= RequeueCount - 1 assertion is kept in both variants</item>
/// </list>
/// </summary>
public class WhenGeneratingRequeueBudgetTestShouldAssertDispatchCountWithinBudgetBothVariants : IDisposable
{
    private const string LEDGER_KEY = "Kafka / Classic";
    private const string FR23_TEMPLATE =
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue";

    // ── Dispatch count assertions ────────────────────────────────────────────
    // The template must call GetDispatchCount using KeyOf(message) of the SENT message.
    private const string GET_DISPATCH_COUNT = "GetDispatchCount";
    private const string KEY_OF_MESSAGE = "ConformanceDeferredPump.KeyOf(message)";
    private const string DISPATCH_WITHIN_BUDGET = "<= _subscription.RequeueCount";

    // ── Ordering: dispatch count read comes after await pumping ──────────────
    private const string AWAIT_PUMPING = "await pumping";

    // ── RejectionReason assertion inside StampsRejectionMetadata guard ───────
    private const string STAMPS_REJECTION_METADATA = "keys.StampsRejectionMetadata";
    private const string KEYS_REJECTION_REASON = "keys.RejectionReason";
    private const string DELIVERY_ERROR_VALUE = "\"DeliveryError\"";

    // ── Kept assertion: HandledCount >= RequeueCount - 1 ────────────────────
    private const string HANDLED_COUNT_BOUND = "HandledCount >= deliveriesExpected";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingRequeueBudgetTestShouldAssertDispatchCountWithinBudgetBothVariants()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"RequeueBudgetTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    /// <summary>
    /// Generates both FR-23 templates and asserts all R-27(c)(4) obligations in one shot:
    /// <list type="number">
    ///   <item>Dispatch count is read with GetDispatchCount(ConformanceDeferredPump.KeyOf(message))</item>
    ///   <item>Dispatch count assertion (&lt;= _subscription.RequeueCount) appears after await pumping in both variants</item>
    ///   <item>StampsRejectionMetadata guard contains keys.RejectionReason == "DeliveryError"</item>
    ///   <item>HandledCount &gt;= deliveriesExpected is kept in both variants</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task When_generating_requeue_budget_test_should_assert_dispatch_count_within_budget_both_variants()
    {
        // Arrange
        var ledger = PassLedger("FR-23");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor variant
        var reactor = await File.ReadAllTextAsync(ReactorFr23Path());
        AssertVariantObligations(reactor, "Reactor");

        // Assert — Proactor variant
        var proactor = await File.ReadAllTextAsync(ProactorFr23Path());
        AssertVariantObligations(proactor, "Proactor");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts all R-27(c)(4) obligations for one variant (Reactor or Proactor).
    /// </summary>
    private static void AssertVariantObligations(string content, string label)
    {
        // 1. Dispatch count is read using the correct key derivation
        Assert.True(content.Contains(GET_DISPATCH_COUNT),
            $"{label}: generated file must call {GET_DISPATCH_COUNT}");
        Assert.True(content.Contains(KEY_OF_MESSAGE),
            $"{label}: dispatch count key must use {KEY_OF_MESSAGE}");

        // 2. Dispatch count is bounded by RequeueCount
        Assert.True(content.Contains(DISPATCH_WITHIN_BUDGET),
            $"{label}: must assert dispatch count {DISPATCH_WITHIN_BUDGET}");

        // 3. Dispatch count assertion appears AFTER await pumping (R-4: window closes after quit-and-await)
        var awaitPumpingIndex = content.IndexOf(AWAIT_PUMPING, StringComparison.Ordinal);
        var dispatchCountIndex = content.IndexOf(GET_DISPATCH_COUNT, StringComparison.Ordinal);

        Assert.True(awaitPumpingIndex >= 0,
            $"{label}: '{AWAIT_PUMPING}' not found");
        Assert.True(dispatchCountIndex >= 0,
            $"{label}: '{GET_DISPATCH_COUNT}' not found");
        Assert.True(dispatchCountIndex > awaitPumpingIndex,
            $"{label}: {GET_DISPATCH_COUNT} (at {dispatchCountIndex}) must appear after " +
            $"'{AWAIT_PUMPING}' (at {awaitPumpingIndex})");

        // 4. StampsRejectionMetadata guard contains keys.RejectionReason == "DeliveryError"
        Assert.True(content.Contains(STAMPS_REJECTION_METADATA),
            $"{label}: must contain {STAMPS_REJECTION_METADATA} guard");
        Assert.True(content.Contains(KEYS_REJECTION_REASON),
            $"{label}: must assert via {KEYS_REJECTION_REASON}");
        Assert.True(content.Contains(DELIVERY_ERROR_VALUE),
            $"{label}: must assert {DELIVERY_ERROR_VALUE}");

        // RejectionReason assertion must be INSIDE the StampsRejectionMetadata guard
        var stampsIndex = content.IndexOf(STAMPS_REJECTION_METADATA, StringComparison.Ordinal);
        var rejectionReasonIndex = content.IndexOf(KEYS_REJECTION_REASON, StringComparison.Ordinal);
        Assert.True(rejectionReasonIndex > stampsIndex,
            $"{label}: {KEYS_REJECTION_REASON} (at {rejectionReasonIndex}) must appear after " +
            $"{STAMPS_REJECTION_METADATA} (at {stampsIndex}) — assertion must be inside the guard");

        // 5. Existing HandledCount >= RequeueCount - 1 assertion is kept (R-5 bound)
        Assert.True(content.Contains(HANDLED_COUNT_BOUND),
            $"{label}: must retain '{HANDLED_COUNT_BOUND}' assertion");
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

    private string ReactorFr23Path() =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Reactor",
            $"{FR23_TEMPLATE}.cs");

    private string ProactorFr23Path() =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Proactor",
            $"{FR23_TEMPLATE}.cs");

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
