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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// A logger provider, added to <c>ApplicationLogging.LoggerFactory</c>, whose loggers record the entries of the
/// categories the current test captures. The <see cref="TimedOutboxStaleMessageMonitor"/>'s category is captured
/// after <see cref="Reset"/>. Its state is safe to read and write from pool threads. It can hold the thread that logs one
/// entry until the test releases it.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private static readonly string s_monitorCategory = typeof(TimedOutboxStaleMessageMonitor).FullName!;

    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();
    private readonly ConcurrentDictionary<string, byte> _capturedCategories = new();
    private readonly object _holdLock = new();
    private Func<CapturedLogEntry, bool>? _hold;
    private (Func<CapturedLogEntry, bool> Match, Exception Exception)? _throw;
    private ManualResetEventSlim _released = new(true);
    private TaskCompletionSource<bool> _entryHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Creates a provider that captures the monitor's category.</summary>
    public CapturingLoggerProvider() => Reset();

    /// <summary>The entries recorded since the last <see cref="Reset"/> or <see cref="ClearEntries"/>, in order.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToList();

    /// <summary>Completes once the thread that logs the entry given to <see cref="HoldEntry"/> is held.</summary>
    public Task EntryHeld => _entryHeld.Task;

    /// <summary>Clears the entries, releases any held entry, drops any throw still to come and captures the monitor's category alone.</summary>
    public void Reset()
    {
        ReleaseHeldEntry();
        lock (_holdLock)
            _throw = null;
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

    /// <summary>
    /// Makes the thread that logs the next captured entry matching <paramref name="match"/> wait, after the entry is
    /// recorded, until <see cref="ReleaseHeldEntry"/>.
    /// </summary>
    /// <param name="match">Selects the entry to hold.</param>
    public void HoldEntry(Func<CapturedLogEntry, bool> match)
    {
        lock (_holdLock)
        {
            _hold = match;
            _released = new ManualResetEventSlim(false);
            _entryHeld = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Makes the logger throw <paramref name="exception"/>, once, instead of recording the next captured entry matching
    /// <paramref name="match"/>.
    /// </summary>
    /// <param name="match">Selects the entry to throw on.</param>
    /// <param name="exception">The exception the logger throws.</param>
    public void ThrowOnce(Func<CapturedLogEntry, bool> match, Exception exception)
    {
        lock (_holdLock)
            _throw = (match, exception);
    }

    /// <summary>Lets the held thread, and any entry still to be held, go on.</summary>
    public void ReleaseHeldEntry()
    {
        lock (_holdLock)
        {
            _hold = null;
            _released.Set();
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    internal bool Captures(string category) => _capturedCategories.ContainsKey(category);

    internal void Record(CapturedLogEntry entry)
    {
        ThrowIfAsked(entry);
        _entries.Enqueue(entry);
        WaitIfHeld(entry);
    }

    private void ThrowIfAsked(CapturedLogEntry entry)
    {
        Exception exception;
        lock (_holdLock)
        {
            if (_throw is not { } toThrow || !toThrow.Match(entry))
                return;

            _throw = null;
            exception = toThrow.Exception;
        }

        throw exception;
    }

    private void WaitIfHeld(CapturedLogEntry entry)
    {
        ManualResetEventSlim released;
        lock (_holdLock)
        {
            if (_hold is null || !_hold(entry))
                return;

            _hold = null;
            released = _released;
            _entryHeld.TrySetResult(true);
        }

        released.Wait();
    }
}
