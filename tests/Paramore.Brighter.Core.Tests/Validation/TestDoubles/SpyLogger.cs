#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// A simple in-memory logger that captures log entries for test assertions.
/// </summary>
public class SpyLogger : ILogger
{
    private readonly List<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.AsReadOnly();

    public IEnumerable<LogEntry> InformationEntries =>
        _entries.Where(e => e.LogLevel == LogLevel.Information);

    public IEnumerable<LogEntry> DebugEntries =>
        _entries.Where(e => e.LogLevel == LogLevel.Debug);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}

public record LogEntry(LogLevel LogLevel, string Message);

/// <summary>
/// A generic wrapper around <see cref="SpyLogger"/> that implements <see cref="ILogger{T}"/>
/// for use with classes that require a typed logger.
/// </summary>
public class SpyLogger<T> : ILogger<T>
{
    private readonly SpyLogger _inner = new();

    public IReadOnlyList<LogEntry> Entries => _inner.Entries;

    public IEnumerable<LogEntry> WarningEntries =>
        _inner.Entries.Where(e => e.LogLevel == LogLevel.Warning);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
