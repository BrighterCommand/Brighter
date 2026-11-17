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
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// A logger for one category that, while its <see cref="CapturingLoggerProvider"/> captures that category, is enabled
/// for every level and records each entry; for any other category it is not enabled and records nothing.
/// </summary>
public sealed class CapturingLogger : ILogger
{
    private readonly string _category;
    private readonly CapturingLoggerProvider _provider;

    /// <summary>Creates a logger for <paramref name="category"/> that records through <paramref name="provider"/>.</summary>
    /// <param name="category">The category of the logger.</param>
    /// <param name="provider">The provider that decides whether the category is captured and holds the entries.</param>
    public CapturingLogger(string category, CapturingLoggerProvider provider)
    {
        _category = category;
        _provider = provider;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;
        _provider.Record(new CapturedLogEntry(_category, logLevel, formatter(state, exception), exception));
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _provider.Captures(_category);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
