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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// An <see cref="ILoggerProvider"/> that captures log messages so a test can assert which
/// <see cref="ILoggerFactory"/> a Brighter object actually logged through. Once disposed (as the DI
/// container does when its <see cref="ServiceProvider"/> is disposed), logging through it throws
/// <see cref="ObjectDisposedException"/> — this is what surfaces a use-after-dispose if an object is
/// still holding a reference to a factory owned by a disposed container.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _entries = new();
    private readonly object _gate = new();

    public bool IsDisposed { get; private set; }

    public IReadOnlyList<string> Entries
    {
        get { lock (_gate) { return _entries.ToList(); } }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose() => IsDisposed = true;

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (provider.IsDisposed)
                throw new ObjectDisposedException(nameof(CapturingLoggerProvider));

            lock (provider._gate)
                provider._entries.Add(formatter(state, exception));
        }
    }
}
