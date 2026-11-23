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
/// Pins the ConformanceDeferredPump template's dispatch count: a static, thread-safe store keyed by the
/// original message id when present, else the message id; incremented by both handlers before they defer;
/// with reset and read entry points, and reset from the constructor of each requeue-too-many-times test.
/// See ADR 0077, What the FR-23 harness can assert.
/// </summary>
public class WhenGeneratingDeferredPumpShouldEmitDispatchCountKeyedByOriginalMessageId : IDisposable
{
    private const string LEDGER_KEY = "Kafka / Classic";

    private const string FR23_TEMPLATE =
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue";

    private const string CONCURRENT_DICTIONARY = "ConcurrentDictionary";

    private const string ORIGINAL_MESSAGE_ID_HEADER = "Message.OriginalMessageIdHeaderName";
    private const string HEADER_MESSAGE_ID_FALLBACK = "Header.MessageId";

    private const string ORIGINATING_MESSAGE_ACCESS = "Context?.OriginatingMessage";
    private const string DEFER_MESSAGE_ACTION = "throw new DeferMessageAction()";

    private const string RESET_METHOD = "ResetDispatchCount";
    private const string GET_DISPATCH_COUNT = "GetDispatchCount";

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

    [Fact]
    public async Task When_generating_deferred_pump_should_emit_dispatch_count_keyed_by_original_message_id()
    {
        // Arrange
        var ledger = PassLedger("FR-23");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var pump = await File.ReadAllTextAsync(PumpPath());
        Assert.Contains(CONCURRENT_DICTIONARY, pump);

        Assert.Contains(ORIGINAL_MESSAGE_ID_HEADER, pump);
        Assert.Contains(HEADER_MESSAGE_ID_FALLBACK, pump);

        var originatingMessageCount = CountOccurrences(pump, ORIGINATING_MESSAGE_ACCESS);
        Assert.True(originatingMessageCount >= 2,
            $"Expected Context.OriginatingMessage in at least 2 handler bodies, found {originatingMessageCount}");

        var accessIndex = pump.IndexOf(ORIGINATING_MESSAGE_ACCESS, StringComparison.Ordinal);
        var deferIndex = pump.IndexOf(DEFER_MESSAGE_ACTION, StringComparison.Ordinal);
        Assert.True(accessIndex < deferIndex,
            $"Context.OriginatingMessage access (at {accessIndex}) must precede " +
            $"throw new DeferMessageAction() (at {deferIndex})");

        Assert.Contains(RESET_METHOD, pump);
        Assert.Contains(GET_DISPATCH_COUNT, pump);

        var reactorFr23 = await File.ReadAllTextAsync(ReactorFr23Path());
        AssertConstructorContainsReset(reactorFr23, "Reactor FR-23");

        var proactorFr23 = await File.ReadAllTextAsync(ProactorFr23Path());
        AssertConstructorContainsReset(proactorFr23, "Proactor FR-23");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Appearing before the first [Fact] stands in for "inside the constructor".
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
