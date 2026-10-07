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
/// Verifies that the generated ConformanceDeferredPump exposes a dispatch count keyed on the
/// originating message identity, reset between tests (R-27(c)(1)-(3), ADR 0077).
///
/// The key is x-original-message-id (Message.OriginalMessageIdHeaderName) when present, else
/// Header.MessageId. It is never the command's own id. Both the sync handler (lines ~45-49) and
/// the async handler (lines ~52-57) read the key from Context.OriginatingMessage and increment
/// the count before throwing DeferMessageAction.
///
/// The counter is a static, thread-safe store (ConcurrentDictionary) so that per-dispatch
/// handler instances share state.
///
/// A ResetDispatchCount entry point exists and is called from each FR-23 test's constructor
/// (today FR-23 Reactor and Proactor) so that counts cannot leak between tests.
/// </summary>
public class WhenGeneratingDeferredPumpShouldEmitDispatchCountKeyedByOriginalMessageId : IDisposable
{
    private const string LEDGER_KEY = "Kafka / Classic";

    private const string FR23_TEMPLATE =
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue";

    // ── Static thread-safe store ─────────────────────────────────────────────
    // The counter must survive handler instances that are newed per dispatch (lines ~128, ~152),
    // so it must be static. ConcurrentDictionary is the canonical .NET way to meet this.
    private const string CONCURRENT_DICTIONARY = "ConcurrentDictionary";

    // ── Key derivation ───────────────────────────────────────────────────────
    // Must use Message.OriginalMessageIdHeaderName ("x-original-message-id") when present,
    // fall back to Header.MessageId otherwise.  Never the command id.
    private const string ORIGINAL_MESSAGE_ID_HEADER = "Message.OriginalMessageIdHeaderName";
    private const string HEADER_MESSAGE_ID_FALLBACK = "Header.MessageId";

    // ── Handler increment ────────────────────────────────────────────────────
    // Both handlers read Context?.OriginatingMessage before throwing DeferMessageAction.
    private const string ORIGINATING_MESSAGE_ACCESS = "Context?.OriginatingMessage";
    private const string DEFER_MESSAGE_ACTION = "throw new DeferMessageAction()";

    // ── Reset and read-by-key entry points ───────────────────────────────────
    private const string RESET_METHOD = "ResetDispatchCount";
    private const string GET_DISPATCH_COUNT = "GetDispatchCount";

    // ── Constructor reset call in each consuming test ────────────────────────
    private const string RESET_CALL_IN_CONSTRUCTOR = "ConformanceDeferredPump.ResetDispatchCount()";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingDeferredPumpShouldEmitDispatchCountKeyedByOriginalMessageId()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"DeferredPumpDispatchCountTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    /// <summary>
    /// Generates the ConformanceDeferredPump shared template and both FR-23 consuming templates,
    /// then asserts all R-27(c)(1)-(3) obligations in one shot:
    /// <list type="number">
    ///   <item>Static, thread-safe dispatch store (ConcurrentDictionary)</item>
    ///   <item>Key derivation: Message.OriginalMessageIdHeaderName when present, else Header.MessageId</item>
    ///   <item>Both sync and async handlers read Context.OriginatingMessage and increment before throw new DeferMessageAction()</item>
    ///   <item>ResetDispatchCount entry point</item>
    ///   <item>GetDispatchCount read-by-key entry point</item>
    ///   <item>FR-23 Reactor constructor calls ConformanceDeferredPump.ResetDispatchCount()</item>
    ///   <item>FR-23 Proactor constructor calls ConformanceDeferredPump.ResetDispatchCount()</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task When_generating_deferred_pump_should_emit_dispatch_count_keyed_by_original_message_id()
    {
        // Arrange
        var ledger = PassLedger("FR-23");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — shared pump: static thread-safe counter
        var pump = await File.ReadAllTextAsync(PumpPath());
        Assert.Contains(CONCURRENT_DICTIONARY, pump);

        // Assert — shared pump: key derivation uses x-original-message-id, falls back to MessageId
        Assert.Contains(ORIGINAL_MESSAGE_ID_HEADER, pump);
        Assert.Contains(HEADER_MESSAGE_ID_FALLBACK, pump);

        // Assert — shared pump: both handlers read Context.OriginatingMessage before throwing
        // (must appear at least twice — once in the sync handler, once in the async handler)
        var originatingMessageCount = CountOccurrences(pump, ORIGINATING_MESSAGE_ACCESS);
        Assert.True(originatingMessageCount >= 2,
            $"Expected Context.OriginatingMessage in at least 2 handler bodies, found {originatingMessageCount}");

        // Assert — increment (via OriginatingMessage access) precedes the defer throw
        var accessIndex = pump.IndexOf(ORIGINATING_MESSAGE_ACCESS, StringComparison.Ordinal);
        var deferIndex = pump.IndexOf(DEFER_MESSAGE_ACTION, StringComparison.Ordinal);
        Assert.True(accessIndex < deferIndex,
            $"Context.OriginatingMessage access (at {accessIndex}) must precede " +
            $"throw new DeferMessageAction() (at {deferIndex})");

        // Assert — reset and read-by-key entry points exist
        Assert.Contains(RESET_METHOD, pump);
        Assert.Contains(GET_DISPATCH_COUNT, pump);

        // Assert — FR-23 Reactor constructor calls ResetDispatchCount()
        var reactorFr23 = await File.ReadAllTextAsync(ReactorFr23Path());
        AssertConstructorContainsReset(reactorFr23, "Reactor FR-23");

        // Assert — FR-23 Proactor constructor calls ResetDispatchCount()
        var proactorFr23 = await File.ReadAllTextAsync(ProactorFr23Path());
        AssertConstructorContainsReset(proactorFr23, "Proactor FR-23");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the test class constructor calls ConformanceDeferredPump.ResetDispatchCount()
    /// and that this call appears before the first [Fact] attribute (i.e., inside the constructor
    /// rather than a test method body).
    /// </summary>
    private static void AssertConstructorContainsReset(string content, string label)
    {
        Assert.Contains(RESET_CALL_IN_CONSTRUCTOR, content);

        var resetIndex = content.IndexOf(RESET_CALL_IN_CONSTRUCTOR, StringComparison.Ordinal);
        var firstFactIndex = content.IndexOf("[Fact", StringComparison.Ordinal);

        Assert.True(resetIndex >= 0, $"{label}: ResetDispatchCount call not found");

        if (firstFactIndex >= 0)
        {
            Assert.True(resetIndex < firstFactIndex,
                $"{label}: ConformanceDeferredPump.ResetDispatchCount() call (at {resetIndex}) " +
                $"must appear before first [Fact] attribute (at {firstFactIndex}), " +
                $"i.e. inside the constructor, not a test method");
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
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

    private string PumpPath() =>
        Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated",
            "ConformanceDeferredPump.cs");

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
