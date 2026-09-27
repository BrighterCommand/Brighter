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

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

/// <summary>
/// An <see cref="ILoggerFactory"/> that buffers all log entries emitted by any logger it creates,
/// sharing the buffer across all category names. Used in tests that observe logging through
/// <see cref="Paramore.Brighter.Logging.ApplicationLogging.LoggerFactory"/>.
///
/// Thread-safety: the internal list is guarded by a lock so that loggers called from
/// concurrent threads (e.g. async gateway helpers) do not race. <see cref="Entries"/>
/// returns a snapshot copy under the lock, so callers iterate a stable list.
/// </summary>
internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    private readonly List<CapturedEntry> _entries = [];
    private readonly object _lock = new();

    /// <summary>Gets a snapshot of all log entries captured by every logger this factory created.</summary>
    public IReadOnlyList<CapturedEntry> Entries
    {
        get
        {
            lock (_lock)
                return _entries.ToArray();
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new InnerLogger(_entries, _lock);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) { }

    /// <inheritdoc />
    public void Dispose() { }

    private sealed class InnerLogger(List<CapturedEntry> entries, object lockObj) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (lockObj)
                entries.Add(new CapturedEntry(logLevel, formatter(state, exception), exception));
        }
    }
}
