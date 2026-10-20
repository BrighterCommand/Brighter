using System;
using Serilog.Events;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>A warning or error log event that carried an exception, as <see cref="CapturingLogSink"/> recorded it.</summary>
public sealed record CapturedLogEvent(LogEventLevel Level, string? SourceContext, string Message, Exception Exception);
