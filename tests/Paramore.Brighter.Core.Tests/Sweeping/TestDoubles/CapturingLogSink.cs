using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog.Core;
using Serilog.Events;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A process-wide Serilog sink that keeps only events carrying an exception at Warning or above. The sweeper logs from
/// a dedicated thread that the AsyncLocal test correlator never sees, so a test finds its events by
/// <c>SourceContext</c> and by a marker it put in the exception message of the double it throws from.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    public static CapturingLogSink Instance { get; } = new();

    private readonly ConcurrentQueue<CapturedLogEvent> _events = new();

    private CapturingLogSink()
    {
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Exception is null || logEvent.Level < LogEventLevel.Warning) return;

        var sourceContext = logEvent.Properties.TryGetValue("SourceContext", out var value) && value is ScalarValue { Value: string text}
            ? text
            : null;
        _events.Enqueue(new CapturedLogEvent(logEvent.Level, sourceContext, logEvent.RenderMessage(), logEvent.Exception));
    }

    /// <summary>The captured events from <paramref name="sourceContext"/> whose exception message contains <paramref name="marker"/>.</summary>
    public IReadOnlyList<CapturedLogEvent> Find(string sourceContext, string marker) =>
        _events
            .Where(e => e.SourceContext == sourceContext && e.Exception.Message.Contains(marker, StringComparison.Ordinal))
            .ToList();
}
