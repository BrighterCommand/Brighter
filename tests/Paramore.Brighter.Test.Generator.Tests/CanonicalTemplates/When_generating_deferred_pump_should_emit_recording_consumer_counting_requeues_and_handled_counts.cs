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
/// Verifies that the generated ConformanceDeferredPump emits recording consumer decorators
/// that count requeues and record the HandledCount on each receive (R-27(c)(5)-(6), ADR 0077).
///
/// The decorators:
///   - implement <c>IAmAMessageConsumerSync</c> / <c>IAmAMessageConsumerAsync</c>
///   - forward every interface member to an inner consumer
///   - count <c>Requeue</c>/<c>RequeueAsync</c> by the key from the dispatch count (task 3.2)
///   - record the integer value of <c>Header.HandledCount</c> for each message returned by
///     <c>Receive</c>/<c>ReceiveAsync</c>, in delivery order, before the pump sees it (a value
///     copy, not a reference — the pump increments the same header via UpdateHandledCount)
///   - are reset and read through the same static surface as the dispatch count
///   - are exposed through factory helpers that build <c>Channel</c>/<c>ChannelAsync</c>
///
/// These decorators are test-infrastructure only (C-10: not mocks, not production counters).
/// They are emitted from ConformanceDeferredPump.cs.liquid so no new suite is needed.
/// </summary>
public class WhenGeneratingDeferredPumpShouldEmitRecordingConsumerCountingRequeuesAndHandledCounts
    : IDisposable
{
    private const string LEDGER_KEY = "Kafka / Classic";

    // ── Sync decorator ─────────────────────────────────────────────────────────
    private const string SYNC_DECORATOR_CLASS = "RecordingConsumerSync";
    private const string SYNC_IMPLEMENTS = ": IAmAMessageConsumerSync";

    // ── Async decorator ────────────────────────────────────────────────────────
    private const string ASYNC_DECORATOR_CLASS = "RecordingConsumerAsync";
    private const string ASYNC_IMPLEMENTS = ": IAmAMessageConsumerAsync";

    // ── Sync forwarding — every member of IAmAMessageConsumerSync ──────────────
    private const string SYNC_FORWARD_ACKNOWLEDGE = "_inner.Acknowledge(";
    private const string SYNC_FORWARD_REJECT = "_inner.Reject(";
    private const string SYNC_FORWARD_PURGE = "_inner.Purge()";
    private const string SYNC_FORWARD_RECEIVE = "_inner.Receive(";
    private const string SYNC_FORWARD_NACK = "_inner.Nack(";
    private const string SYNC_FORWARD_REQUEUE = "_inner.Requeue(";
    private const string SYNC_FORWARD_DISPOSE = "_inner.Dispose()";

    // ── Async forwarding — every member of IAmAMessageConsumerAsync ────────────
    private const string ASYNC_FORWARD_ACKNOWLEDGE = "_inner.AcknowledgeAsync(";
    private const string ASYNC_FORWARD_REJECT = "_inner.RejectAsync(";
    private const string ASYNC_FORWARD_PURGE = "_inner.PurgeAsync(";
    private const string ASYNC_FORWARD_RECEIVE = "_inner.ReceiveAsync(";
    private const string ASYNC_FORWARD_NACK = "_inner.NackAsync(";
    private const string ASYNC_FORWARD_REQUEUE = "_inner.RequeueAsync(";
    private const string ASYNC_FORWARD_DISPOSE = "_inner.DisposeAsync()";

    // ── Requeue count — counted by KeyOf, read by key ─────────────────────────
    private const string REQUEUE_COUNT_FIELD = "s_requeueCount";
    private const string GET_REQUEUE_COUNT = "GetRequeueCount";

    // ── HandledCount log — integer value copied from each received message ─────
    private const string HANDLED_COUNT_LOG_FIELD = "s_handledCountLog";
    private const string GET_HANDLED_COUNT_LOG = "GetHandledCountLog";
    private const string HANDLED_COUNT_PROPERTY = "HandledCount";

    // ── Reset clears requeue count and handled count log ──────────────────────
    private const string RESET_CLEARS_REQUEUE = "s_requeueCount.Clear()";
    private const string RESET_CLEARS_HANDLED_COUNT = "s_handledCountLog.Clear()";

    // ── Factory helpers — wrap decorator in Channel/ChannelAsync ───────────────
    private const string CREATE_RECORDING_CHANNEL = "CreateRecordingChannel(";
    private const string CREATE_RECORDING_CHANNEL_ASYNC = "CreateRecordingChannelAsync(";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingDeferredPumpShouldEmitRecordingConsumerCountingRequeuesAndHandledCounts()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"DeferredPumpRecordingConsumerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    /// <summary>
    /// Generates the ConformanceDeferredPump shared template and asserts all R-27(c)(5)-(6)
    /// obligations:
    /// <list type="number">
    ///   <item>Sync recording decorator implements IAmAMessageConsumerSync</item>
    ///   <item>Async recording decorator implements IAmAMessageConsumerAsync</item>
    ///   <item>Both decorators forward every interface member to the inner consumer</item>
    ///   <item>Requeue/RequeueAsync increment a per-key requeue count via KeyOf</item>
    ///   <item>Receive/ReceiveAsync record the integer HandledCount from each message (value copy)</item>
    ///   <item>ResetDispatchCount also clears the requeue count and HandledCount log</item>
    ///   <item>GetRequeueCount and GetHandledCountLog read entry points exist</item>
    ///   <item>Factory helpers CreateRecordingChannel / CreateRecordingChannelAsync exist</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task When_generating_deferred_pump_should_emit_recording_consumer_counting_requeues_and_handled_counts()
    {
        // Arrange
        var ledger = PassLedger("FR-23");
        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — sync decorator implements IAmAMessageConsumerSync
        var pump = await File.ReadAllTextAsync(PumpPath());
        Assert.Contains(SYNC_DECORATOR_CLASS, pump);
        Assert.Contains(SYNC_IMPLEMENTS, pump);

        // Assert — async decorator implements IAmAMessageConsumerAsync
        Assert.Contains(ASYNC_DECORATOR_CLASS, pump);
        Assert.Contains(ASYNC_IMPLEMENTS, pump);

        // Assert — sync decorator forwards every interface member (forwarding is not a counter —
        // C-10 forbids a mock standing in for the real transport)
        Assert.Contains(SYNC_FORWARD_ACKNOWLEDGE, pump);
        Assert.Contains(SYNC_FORWARD_REJECT, pump);
        Assert.Contains(SYNC_FORWARD_PURGE, pump);
        Assert.Contains(SYNC_FORWARD_RECEIVE, pump);
        Assert.Contains(SYNC_FORWARD_NACK, pump);
        Assert.Contains(SYNC_FORWARD_REQUEUE, pump);
        Assert.Contains(SYNC_FORWARD_DISPOSE, pump);

        // Assert — async decorator forwards every interface member
        Assert.Contains(ASYNC_FORWARD_ACKNOWLEDGE, pump);
        Assert.Contains(ASYNC_FORWARD_REJECT, pump);
        Assert.Contains(ASYNC_FORWARD_PURGE, pump);
        Assert.Contains(ASYNC_FORWARD_RECEIVE, pump);
        Assert.Contains(ASYNC_FORWARD_NACK, pump);
        Assert.Contains(ASYNC_FORWARD_REQUEUE, pump);
        Assert.Contains(ASYNC_FORWARD_DISPOSE, pump);

        // Assert — requeue count: static field exists, GetRequeueCount read entry point exists
        Assert.Contains(REQUEUE_COUNT_FIELD, pump);
        Assert.Contains(GET_REQUEUE_COUNT, pump);

        // Assert — HandledCount log: static field exists, GetHandledCountLog read entry point exists
        Assert.Contains(HANDLED_COUNT_LOG_FIELD, pump);
        Assert.Contains(GET_HANDLED_COUNT_LOG, pump);

        // Assert — HandledCount is accessed from messages returned by Receive (value copy before pump sees it)
        var syncReceiveIndex = pump.IndexOf(SYNC_FORWARD_RECEIVE, StringComparison.Ordinal);
        Assert.True(syncReceiveIndex >= 0, $"Expected '{SYNC_FORWARD_RECEIVE}' in the pump");
        var handledCountAfterSyncReceive = pump.IndexOf(
            HANDLED_COUNT_PROPERTY,
            syncReceiveIndex,
            StringComparison.Ordinal);
        Assert.True(handledCountAfterSyncReceive > syncReceiveIndex,
            $"Expected '{HANDLED_COUNT_PROPERTY}' to be read after '{SYNC_FORWARD_RECEIVE}' " +
            $"(HandledCount must be copied as an integer from each received message before the pump sees it)");

        // Assert — ResetDispatchCount clears requeue count and handled count log
        Assert.Contains(RESET_CLEARS_REQUEUE, pump);
        Assert.Contains(RESET_CLEARS_HANDLED_COUNT, pump);

        // Assert — factory helpers build Channel/ChannelAsync over the recording decorator
        Assert.Contains(CREATE_RECORDING_CHANNEL, pump);
        Assert.Contains(CREATE_RECORDING_CHANNEL_ASYNC, pump);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
