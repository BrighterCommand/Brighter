#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// A logger provider, added to <c>ApplicationLogging.LoggerFactory</c>, whose loggers record the entries of the
/// categories the current test captures. The <see cref="TimedOutboxStaleMessageMonitor"/>'s category is captured
/// after <see cref="Reset"/>. Its state is safe to read and write from pool threads.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private static readonly string s_monitorCategory = typeof(TimedOutboxStaleMessageMonitor).FullName!;

    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();
    private readonly ConcurrentDictionary<string, byte> _capturedCategories = new();

    /// <summary>Creates a provider that captures the monitor's category.</summary>
    public CapturingLoggerProvider() => Reset();

    /// <summary>The entries recorded since the last <see cref="Reset"/> or <see cref="ClearEntries"/>, in order.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToList();

    /// <summary>Clears the entries and captures the monitor's category alone.</summary>
    public void Reset()
    {
        ClearEntries();
        _capturedCategories.Clear();
        _capturedCategories[s_monitorCategory] = 0;
    }

    /// <summary>Clears the entries and keeps the captured categories.</summary>
    public void ClearEntries()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    internal bool Captures(string category) => _capturedCategories.ContainsKey(category);

    internal void Record(CapturedLogEntry entry) => _entries.Enqueue(entry);
}
